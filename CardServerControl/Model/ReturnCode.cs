﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardServerControl.Model
{
    class ReturnCode
    {
        public const int Success = 0;
        public const int Failed = -1;
        public const int Refuse = 40;//拒绝就是不经过验证。服务器直接拒绝请求
        public const int Repeal = 80;
        public const int Request = 1;
        public const int Pass = 2;
    }
}