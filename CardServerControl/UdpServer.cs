﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using MySql.Data.MySqlClient;
using System.Data;

namespace CardServerControl
{
    class UdpServer
    {
        #region 单例模式
        private static UdpServer _instance;
        public static UdpServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UdpServer();
                }
                return _instance;
            }
        }
        #endregion


        public UdpClient receiveClient;
        public UdpClient sendClient;
        private IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
        private int localPort = 23333;//服务端端口
        private int remotePort = 22233;//客户端端口
        private LogsSystem log;
        private Thread thread;

        public UdpServer()
        {
            this.receiveClient = new UdpClient(localPort);
            this.sendClient = new UdpClient();
            log = LogsSystem.Instance;
        }

        public void Connect()
        {
            try
            {
                log.Print("[服务器]正在创建服务器连接...");
                if (thread == null)
                {
                    thread = new Thread(new ThreadStart(Listen));
                    thread.Start();
                }
                else
                {
                    log.Print("[服务器]服务器创建失败:服务器已经被创建", LogLevel.WARN);
                }
            }
            catch (Exception ex)
            {
                log.Print("[服务器]服务器创建失败，发生异常：" + ex, LogLevel.ERROR);
            }
        }

        private void Listen()
        {
            log.Print("[服务器]开始监听端口：" + localPort);
            this.thread.IsBackground = true;//将线程设为后台线程,当前台线程全部关闭后会自动关闭后台线程

            while (true)
            {
                //接受
                IPEndPoint remoteEP = null;
                byte[] receivedInf = receiveClient.Receive(ref remoteEP);
                string Text = Encoding.UTF8.GetString(receivedInf);
                log.Print(string.Format("[远程{0}]{1}", remoteEP.ToString(), Text));

                //响应
                remoteEP.Port = remotePort;
                byte[] response = ResponsePacket(Text, remoteEP);
                sendClient.Send(response, response.Length, remoteEP);
                log.Print(string.Format("[发送至{0}]{1}", remoteEP.ToString(), Encoding.UTF8.GetString(response)));
            }
        }

        public void StopListen()
        {
            if (this.thread != null)
            {
                log.Print("[服务器]关闭监听端口：" + localPort);
                this.thread.Abort();
                log.Print("[线程]" + this.thread.ThreadState.ToString());
                this.thread = null;
                PlayerManager.Instance.ClearPlayerList();
            }
            else
            {
                log.Print("[服务器]失败:服务器已经被关闭", LogLevel.WARN);
            }
        }


        /// <summary>
        /// 处理数据并根据数据返回相应的数据包
        /// </summary>
        /// <param name="text">收到的文本</param>
        /// <param name="toIped">发送给对方的IP地址</param>
        /// <returns>数据包</returns>
        private byte[] ResponsePacket(string text, IPEndPoint toIped)
        {
            string[] tempArgs = text.Split(new char[] { ' ' });
            string ticks = tempArgs[0];
            long ping = Math.Abs(GetTimeStamp() - Convert.ToInt64(ticks));//返回数据包从发送到接受所用的时间(ms)

            //这里写信息处理代码-------
            string responseText = "";//返回的文本
            string operationName = tempArgs[1];//操作名

            //添加数据头
            AddArguments(ref responseText, ping.ToString());
            AddArguments(ref responseText, operationName);

            string[] arguments = GetOperationArguments(tempArgs);
            if (operationName == "login")
            {
                //==========================================登陆==========================================
                string username = arguments[0];
                string password = arguments[1];

                //查询数据库
                string command = string.Format("SELECT count(*) FROM account WHERE Account = '{0}' AND Password = '{1}'", username, password);
                DataSet ds = MySQLHelper.GetDataSet(MySQLHelper.Conn, CommandType.Text, command, null);

                //如果密码错误或者服务器已满都返回登陆失败
                if ((Convert.ToInt32(ds.Tables[0].Rows[0]["count(*)"]) != 0) && PlayerManager.Instance.CanLogin())
                {
                    //登陆成功
                    LogsSystem.Instance.Print(string.Format("账户{0}[{1}]已登录到系统", username, toIped.Address.ToString()));

                    //为数据表创建uuid并写入
                    string uuid = System.Guid.NewGuid().ToString();
                    command = string.Format("UPDATE account SET UUID = '{0}',LastLogin = '{1}' WHERE Account = '{2}' AND Password = '{3}'", uuid, GetTimeStamp(), username, password);
                    MySQLHelper.ExecuteNonQuery(MySQLHelper.Conn, CommandType.Text, command, null);

                    //添加到服务器的用户列表
                    PlayerManager.Instance.PlayerLogin(username, uuid, toIped.Address.ToString());

                    AddArguments(ref responseText, "true");
                    AddArguments(ref responseText, username);
                    AddArguments(ref responseText, password);
                    AddArguments(ref responseText, uuid);
                }
                else
                {
                    //登陆失败
                    AddArguments(ref responseText, "false");
                }
            }
            else if (operationName == "chat")
            {
                string message = arguments[0];
                string uuid = arguments[1];
                string toUUID = "";
                if (arguments.Length > 2)
                {
                    toUUID = arguments[2];
                }

                LogsSystem.Instance.Print(string.Format("[用户]{0}:{1}", PlayerManager.Instance.GetPlayerNameByUUID(uuid), message));
                if (string.IsNullOrEmpty(toUUID))
                {
                    //发送给所有玩家
                    List<Player> playerList = PlayerManager.Instance.GetPlayerList();
                    foreach (Player player in playerList)
                    {
                        if (player.UUID != uuid)
                        {
                            //-------------------------------注意这里是同步发送所以后期可能需要修改
                            string sendTxt = string.Format("{0} chat true {1} {2}", GetTimeStamp().ToString(), message, uuid);
                            byte[] sendByte = Encoding.UTF8.GetBytes(sendTxt);
                            sendClient.Send(sendByte, sendByte.Length, player.IPAddress, remotePort);
                        }
                    }
                }
                else
                {
                    //发送给特定玩家
                    //私聊
                    List<Player> playerList = PlayerManager.Instance.GetPlayerList();
                    foreach (Player player in playerList)
                    {
                        if (player.UUID == toUUID)
                        {
                            string sendTxt = string.Format("{0} privatechat true {1} {2}", GetTimeStamp().ToString(), message, uuid);
                            byte[] sendByte = Encoding.UTF8.GetBytes(sendTxt);
                            sendClient.Send(sendByte, sendByte.Length, player.IPAddress, remotePort);
                        }
                    }
                }

                //发给发送方
                AddArguments(ref responseText, "true");
            }

            //结束----
            byte[] response = Encoding.UTF8.GetBytes(responseText);
            return response;
        }

        public void SendToAllPlayer(byte[] messageByte)
        {
            List<Player> playerList = PlayerManager.Instance.GetPlayerList();
            foreach (Player player in playerList)
            {
                //-------------------------------注意这里是同步发送所以后期可能需要修改
                sendClient.Send(messageByte, messageByte.Length, player.IPAddress, remotePort);
            }
        }













        /// <summary>
        /// 获取操作参数
        /// </summary>
        /// <param name="main">全部参数</param>
        /// <returns>返回去除时间戳、操作名、返回值后的具体操作参数</returns>
        public static string[] GetOperationArguments(string[] main)
        {
            string[] args = new string[main.Length - 3];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = main[i + 3];
            }
            return args;
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="addedString">被添加参数的字符串</param>
        /// <param name="arguments">添加的参数</param>
        public static void AddArguments(ref string addedString, string argument)
        {
            addedString += " " + argument;
        }

        /// <summary>
        /// 获得时间戳（格林威治时间）
        /// 精确到ms，有±1ms的误差
        /// </summary>
        /// <returns>时间戳字符串</returns>
        public static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds * 1000);
        }
    }
}