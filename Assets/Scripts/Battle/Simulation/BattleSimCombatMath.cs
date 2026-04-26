using UnityEngine;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 헤드리스 시뮬 전용. 본편 <c>BattleSystem</c>/<c>SlotBalanceTable</c>와 별도의 밸런스 공식.
    /// </summary>
    public static class BattleSimCombatMath
    {
        public const float DefaultCriticalChanceBase = 25f;
        public const float CriticalDamageMultiplier = 1.5f;

        /// <summary>가드 중 받는 피해 배율 (1 = 감소 없음). 0.8 = 20% 경감.</summary>
        public const float SimGuardIncomingDamageMultiplier = 0.8f;

        private static readonly float[] SlotHitByIndex =
        {
            0f,
            0.95f,
            0.90f,
            0.75f,
            0.65f
        };

        /// <summary>피격자 슬롯별 받는 피해 배율 — 전열(1~2)×0.90, 후열(3~4)×1.10.</summary>
        private static readonly float[] SlotDmgByIndex =
        {
            0f,
            0.90f,
            0.90f,
            1.10f,
            1.10f
        };

        private static int ResolveSlotIndex(BattleSlot slot)
        {
            switch (slot)
            {
                case BattleSlot.Slot1:
                case BattleSlot.Slot2:
                case BattleSlot.Slot3:
                case BattleSlot.Slot4:
                    return (int)slot;
                case BattleSlot.Slot5:
                case BattleSlot.Slot6:
                    return 3;
                case BattleSlot.Slot7:
                    return 4;
                case BattleSlot.Center:
                case BattleSlot.None:
                default:
                    return 1;
            }
        }

        public static float GetSimSlotHitMultiplier(BattleSlot defenderSlot)
        {
            int i = ResolveSlotIndex(defenderSlot);
            if (i < 1 || i >= SlotHitByIndex.Length)
                i = 1;
            return SlotHitByIndex[i];
        }

        public static float GetSimSlotDamageMultiplier(BattleSlot defenderSlot)
        {
            int i = ResolveSlotIndex(defenderSlot);
            if (i < 1 || i >= SlotDmgByIndex.Length)
                i = 1;
            return SlotDmgByIndex[i];
        }

        public static float ComputeAgilityHitModifier(float attackerAgility, float defenderAgility)
        {
            float raw = 0.8f + (attackerAgility - defenderAgility) * 0.02f;
            return Mathf.Clamp(raw, 0.6f, 1.2f);
        }

        /// <summary>
        /// HitChance = Base × AGI보정 × SlotHit × Equip, clamp 0.45~0.95. Base = 스킬 accuracy 또는 1.
        /// </summary>
        public static float ComputeHitChance(
            SkillData usedSkill,
            float attackerAgility,
            float defenderAgility,
            float attackerLuck,
            float passiveAccuracyBonus,
            float itemAccuracyBonus,
            BattleSlot defenderSlot,
            float equipAccuracyMultiplier = 1f)
        {
            float baseAcc = usedSkill != null ? usedSkill.accuracy : 1f;
            float agiMod = ComputeAgilityHitModifier(attackerAgility, defenderAgility);
            float slotHit = GetSimSlotHitMultiplier(defenderSlot);
            float equip = equipAccuracyMultiplier <= 0f ? 1f : equipAccuracyMultiplier;
            float p = baseAcc * agiMod * slotHit * equip;
            return Mathf.Clamp(p, 0.45f, 0.95f);
        }

        public static bool RollHit(
            SkillData usedSkill,
            float attackerAgility,
            float defenderAgility,
            float attackerLuck,
            float passiveAccuracyBonus,
            float itemAccuracyBonus,
            BattleSlot defenderSlot,
            System.Random rng,
            float equipAccuracyMultiplier = 1f)
        {
            float chance = ComputeHitChance(
                usedSkill,
                attackerAgility,
                defenderAgility,
                attackerLuck,
                passiveAccuracyBonus,
                itemAccuracyBonus,
                defenderSlot,
                equipAccuracyMultiplier);
            return rng.NextDouble() < chance;
        }

        public static bool RollCritical(int luck, float criticalChanceBase, System.Random rng)
        {
            double roll = rng.NextDouble() * 100.0;
            return roll < criticalChanceBase + luck;
        }

        /// <summary>
        /// Damage = max(ATK×0.3, floor(ATK×1.6 − DEF×0.6)) × 피격슬롯(전열×0.90·후열×1.10) × Crit × EquipDmg.
        /// 최종값은 최소 1.
        /// </summary>
        public static int CalculateSimMeleeDamage(
            int atk,
            int def,
            bool isCritical,
            BattleSlot defenderSlot,
            float equipDamageMultiplier = 1f)
        {
            int flooredLinear = Mathf.FloorToInt(atk * 1.6f - def * 0.6f);
            float coreBase = Mathf.Max(atk * 0.3f, flooredLinear);
            float slotDmg = GetSimSlotDamageMultiplier(defenderSlot);
            float crit = isCritical ? CriticalDamageMultiplier : 1f;
            float equip = equipDamageMultiplier <= 0f ? 1f : equipDamageMultiplier;
            float v = coreBase * slotDmg * crit * equip;
            return Mathf.Max(1, Mathf.FloorToInt(v));
        }
    }
}
