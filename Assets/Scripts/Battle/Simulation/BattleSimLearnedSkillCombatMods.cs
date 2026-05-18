using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 시뮬 유닛의 <see cref="BattleSimUnit.SimSkills"/>에 담긴 스킬을 본편 <see cref="PlayerStats"/> / <see cref="BattleManager"/> 규칙에 맞춰 전투 수치에 반영합니다.
    /// </summary>
    public static class BattleSimLearnedSkillCombatMods
    {
        public static bool SimSkillsContainsByName(IList<SkillData> skills, string skillName)
        {
            if (skills == null || string.IsNullOrEmpty(skillName)) return false;
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                if (s != null && s.skillName == skillName) return true;
            }

            return false;
        }

        public static int SumSimEquipmentAttackBonus(BattleSimUnit u)
        {
            if (u == null) return 0;
            int t = 0;
            Add(u.SimEquipRightHand);
            Add(u.SimEquipLeftHand);
            Add(u.SimEquipBody);
            Add(u.SimEquipAccessory1);
            Add(u.SimEquipAccessory2);
            return t;

            void Add(EquipmentData e)
            {
                if (e != null) t += e.attackBonus;
            }
        }

        /// <summary><see cref="PlayerStats.GetPassiveAccuracyBonus"/>와 동일한 규칙(패시브 스킬의 PassiveAccuracy 합 + 기본 검술 +5%).</summary>
        public static float GetPassiveAccuracyBonusFromSimSkills(BattleSimUnit u)
        {
            if (u?.SimSkills == null) return 0f;
            float total = 0f;
            foreach (var passive in u.SimSkills)
            {
                if (passive == null || !passive.IsPassive) continue;
                if (passive.Effects == null) continue;
                foreach (var effect in passive.Effects)
                {
                    if (effect == null) continue;
                    if (effect.effectType == EffectType.PassiveAccuracy)
                        total += effect.effectAmount;
                }
            }

            if (SimSkillsContainsByName(u.SimSkills, "Basic Swordsmanship"))
                total += 0.05f;

            return total;
        }

        /// <summary><see cref="PlayerStats.GetBasicSwordsmanshipAttackBonus"/>와 동일(검 장착 가정).</summary>
        public static int GetBasicSwordsmanshipAttackBonus(BattleSimUnit u)
        {
            if (u == null || !SimSkillsContainsByName(u.SimSkills, "Basic Swordsmanship")) return 0;
            int lv = Mathf.Max(1, u.SimDungeonPartyLevel);
            return 2 + Mathf.FloorToInt(lv * 0.25f);
        }

        /// <summary>전열 + 기본 검술 시 받는 피해 감소율(<see cref="PlayerStats.GetAdditionalDamageReductionFromPassives"/>).</summary>
        public static float GetBasicSwordsmanshipDamageTakenReduction(BattleSimUnit defender)
        {
            if (defender == null) return 0f;
            if (!SimSkillsContainsByName(defender.SimSkills, "Basic Swordsmanship")) return 0f;
            if (!SlotHelper.IsFrontRow((int)defender.Slot)) return 0f;
            return 0.05f;
        }

        /// <summary>적 전열(슬롯 1~2) 여부 — Sharp Edge 보너스용.</summary>
        public static bool IsFrontRowEnemyForSharpEdge(BattleSlot defenderSlot)
        {
            int si = (int)defenderSlot;
            return si == 1 || si == 2;
        }

        /// <summary><see cref="BattleManager.ApplyOffensivePassiveBonuses"/> 중 Sharp Edge 분기와 동일.</summary>
        public static int ApplySharpEdgeDamageMultiplier(
            BattleSimUnit attacker,
            BattleSimUnit target,
            int baseDamage,
            bool defenderIsEnemy)
        {
            if (attacker == null || target == null || baseDamage <= 0) return baseDamage;
            if (!defenderIsEnemy) return baseDamage;
            if (!SimSkillsContainsByName(attacker.SimSkills, "Sharp Edge")) return baseDamage;

            float penPercent = 0.08f;
            penPercent += SumSimEquipmentAttackBonus(attacker) * 0.001f;
            if (IsFrontRowEnemyForSharpEdge(target.Slot))
                penPercent += 0.05f;

            return Mathf.Max(1, Mathf.FloorToInt(baseDamage * (1f + penPercent)));
        }
    }
}
