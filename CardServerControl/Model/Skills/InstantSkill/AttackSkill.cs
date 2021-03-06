﻿using CardServerControl.Model.Cards;
using CardServerControl.Model.Skills.ContinuedSkill;
using System.Collections.Generic;
using LitJson;

namespace CardServerControl.Model.Skills.InstantSkill
{
    class AttackSkill : Skill
    {
        protected int skillDamageValue = 0;//技能伤害
        protected List<StateSkill> appendStateList;//攻击技能附加的状态

        public AttackSkill(int skillID, string skillName, int skillDamageValue,int skillEnergyCost, List<StateSkill> appendStateList = null)
            :base(skillID, skillName)
        {
            this.skillDamageValue = skillDamageValue;
            this.skillEnergyCost = skillEnergyCost;
            this.appendStateList = appendStateList;
        }

        public void SetBasicDamage(int value)
        {
            this.skillDamageValue = value;
        }

        /// <summary>
        /// 返回造成的伤害
        /// </summary>
        public virtual int GetCalculatedDamage()
        {
            return this.skillDamageValue;
        }
        /// <summary>
        /// 根据技能来源（技能释放者）计算伤害
        /// </summary>
        public virtual int GetCalculatedDamage(PlayerCard from)
        {
            return this.GetCalculatedDamage();
        }

        /// <summary>
        /// 技能释放
        /// </summary>
        public override void OnUse(PlayerCard from, PlayerCard target)
        {
            int skillDamage = this.GetCalculatedDamage();

            
            target.GetDamage(skillDamage);//造成伤害
            from.currentEnergy -= this.skillEnergyCost;//消耗能量

            LogsSystem.Instance.Print(string.Format("{0}对{1}释放了技能{2}，造成伤害{3}点。", from.cardName, target.cardName, this.skillName, skillDamage), LogLevel.GAMEDETAIL);

            //附加状态
            if (appendStateList != null && appendStateList.Count != 0)
            {
                foreach (StateSkill state in appendStateList)
                {
                    target.AppendState(state);
                }
            }
        }

        public override string GenerateSkillAppendData()
        {
            JsonData data = new JsonData();
            data["damage"] = GetCalculatedDamage();
            data["energy"] = this.skillEnergyCost;

            return data.ToJson();
        }
    }
}
