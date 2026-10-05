using System.Collections.Generic;
using UnityEngine;

namespace AbyssdawnBattle
{
    public enum ModStatType
    {
        Attack, Defense, Magic, Accuracy, HealingReceived, StatusResist,
        CritChance,   // 크리티컬 확률. Flat(퍼센트포인트 가산) 전용 설계 — 버프/디버프 에셋은 Flat으로 만들 것.
        CritDamage,   // 크리티컬 데미지 배율. PercentMult 권장.
        Speed,        // 턴 순서 계산용 속도(민첩) 배율.
        Evasion,      // 회피율 (방어자 기준, 명중률 감소)
        ArmorPen,     // 방어 관통 (공격자 기준). Flat 전용 — 0.08 = 적 방어력 8% 무시. 최대 1.
        DamageTaken   // 받는 피해 배율 (방어자 기준). PercentMult — 0.5 = 받는 피해 절반. (Brace, Last Stand)
    }
    // HealingReceived는 "받는 회복" 전용. 주는 회복은 별도 타입으로 나중에 추가.

    public enum StatModType { Flat = 100, PercentAdd = 200, PercentMult = 300 }
    // 숫자는 계산 순서. 낮은 값이 먼저 적용된다.

    [System.Serializable]
    public class StatModifier
    {
        public ModStatType statType;
        public StatModType modType = StatModType.PercentMult;
        [Tooltip("Flat: 고정치(+5 등). PercentAdd: 가산%(0.2 = +20%, 서로 더한 뒤 1회 적용). PercentMult: 곱산 배율(1.2 = ×1.2, 각각 곱함)")]
        public float value = 1f;
    }

    // 런타임 능동 부여용 (직렬화 안 함)
    public class ActiveStatModifier
    {
        public StatModifier modifier;
        public object source;          // 부여 출처 (스킬 SO, 장비 등)
        public int remainingTurns;     // -1 = 수동 제거 전까지 영구
    }

    public static class StatCalculator
    {
        /// <summary>계산 순서 고정: Base → Flat 합산 → PercentAdd 합산 후 1회 → PercentMult 순차 곱 → Clamp.</summary>
        public static float CalculateFinalStat(float baseValue, IEnumerable<StatModifier> mods,
                                               float minMultOfBase = 0f, float maxMultOfBase = 3f)
        {
            float flat = 0f, percentAdd = 0f, result;
            var mults = new List<float>();
            if (mods != null)
                foreach (var m in mods)
                {
                    if (m == null) continue;
                    switch (m.modType)
                    {
                        case StatModType.Flat:        flat += m.value; break;
                        case StatModType.PercentAdd:  percentAdd += m.value; break;
                        case StatModType.PercentMult: mults.Add(Mathf.Max(0f, m.value)); break;
                    }
                }
            result = baseValue + flat;
            result *= (1f + percentAdd);
            foreach (var mult in mults) result *= mult;
            // 폭주 방지: 기본 스탯 대비 하한/상한 (baseValue가 0 이하일 땐 클램프 생략)
            if (baseValue > 0f)
                result = Mathf.Clamp(result, baseValue * minMultOfBase, baseValue * maxMultOfBase);
            return result;
        }
    }

    // StatusEffectInstance는 global namespace 소속(EnemyStats.cs) — using 추가 없이 참조.
    public static class StatModifierQuery
    {
        public static List<StatModifier> Collect(
            List<StatusEffectInstance> statusEffects,
            List<ActiveStatModifier> activeMods,
            ModStatType type)
        {
            var list = new List<StatModifier>();
            if (statusEffects != null)
                foreach (var se in statusEffects)
                    if (se?.data?.statModifiers != null)
                        foreach (var m in se.data.statModifiers)
                            if (m != null && m.statType == type) list.Add(m);
            if (activeMods != null)
                foreach (var am in activeMods)
                    if (am?.modifier != null && am.modifier.statType == type)
                        list.Add(am.modifier);
            return list;
        }
    }
}
