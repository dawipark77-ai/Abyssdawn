using UnityEngine;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 몬스터 전용 스킬 데이터 (SkillData 상속).
    ///
    /// SkillData의 공통 필드(skillID/skillName/description/damageType/scalingStat/
    ///   targeting(targetFaction·allowedCasterSlots·allowedTargetSlots)/cost/power/
    ///   critBonusPercent/preemptive/curseEffect/curseApplyChance 등)를 그대로 상속.
    /// 아래는 몬스터 평타 대체(Bite 등) 전용 override 필드만 추가한다.
    ///
    /// 용도: 평타 처리/ExecuteEnemyTurn 단계에서 MonsterRace==Beast 등 조건일 때 자동 적용되는 보정값.
    ///   (예: 물기 = 야수 전용 평타 대체)
    /// </summary>
    /// <summary>몬스터 스킬 자동 분류 (디자이너가 hp/mpCost 책정 시 참고용).</summary>
    public enum MonsterSkillCategory
    {
        Physical,       // 물리 공격
        MagicAttack,    // 마법 공격
        Buff,           // 아군 강화
        Debuff,         // 적 약화/상태이상
        Heal            // 회복
    }

    [CreateAssetMenu(fileName = "New Monster Skill", menuName = "Battle/Monster Skill Data")]
    public class MonsterSkillData : SkillData
    {
        [Header("Slot Push/Pull (밀기/당기기)")]
        [Tooltip("0=없음. 양수=대상을 후열 방향으로 N칸. 음수=전열 방향으로 N칸.")]
        public int slotShiftAmount = 0;
        [Tooltip("밀기 발동 확률 (0~1). 1=명중 시 항상.")]
        [Range(0f, 1f)]
        public float slotShiftChance = 1f;

        [Header("Basic Attack Override (Bite 전용)")]
        public bool isBasicAttackOverride;
        public float atkMultiplierOverride;
        public float randomRollMaxOverride;

        /// <summary>
        /// 기존 필드(damageType / targeting.targetFaction / effects / curseEffect)를 조합한 자동 분류.
        /// 읽기 전용 — 실행 로직은 사용하지 않으며, 디자이너가 hp/mpCost 책정 시 참고용.
        /// ※ get-only 프로퍼티라 Unity Inspector에는 노출되지 않음 (ReadOnly 어트리뷰트 미보유).
        /// </summary>
        public MonsterSkillCategory Category
        {
            get
            {
                // 회복: damageType None + targetFaction Ally/Self + effects에 Recovery 포함
                if (damageType == DamageType.None &&
                    (targeting.targetFaction == TargetFaction.Ally || targeting.targetFaction == TargetFaction.Self) &&
                    effects != null && effects.Exists(e => e.effectType == EffectType.Recovery))
                    return MonsterSkillCategory.Heal;

                // 버프: damageType None + targetFaction Ally/Self + (effects에 Buff* 포함 또는 curseEffect.statModifiers에 이로운 배율 포함)
                if (damageType == DamageType.None &&
                    (targeting.targetFaction == TargetFaction.Ally || targeting.targetFaction == TargetFaction.Self) &&
                    (
                        (effects != null && effects.Exists(e => e.effectType.ToString().StartsWith("Buff"))) ||
                        (curseEffect != null && curseEffect.statModifiers != null &&
                         curseEffect.statModifiers.Exists(m =>
                             (m.modType == AbyssdawnBattle.StatModType.PercentMult && m.value > 1f) ||
                             (m.modType == AbyssdawnBattle.StatModType.Flat && m.value > 0f)))
                    ))
                    return MonsterSkillCategory.Buff;

                // 디버프: targetFaction Enemy + (curseEffect 있음 또는 effects에 Debuff* 포함 또는 curseEffect.statModifiers에 해로운 배율 포함)
                if (targeting.targetFaction == TargetFaction.Enemy &&
                    ((curseEffect != null && curseApplyChance > 0f) ||
                     (effects != null && effects.Exists(e => e.effectType.ToString().StartsWith("Debuff"))) ||
                     (curseEffect != null && curseEffect.statModifiers != null &&
                      curseEffect.statModifiers.Exists(m =>
                          (m.modType == AbyssdawnBattle.StatModType.PercentMult && m.value < 1f) ||
                          (m.modType == AbyssdawnBattle.StatModType.Flat && m.value < 0f)))))
                {
                    // 데미지도 같이 있으면 공격 스킬의 부가효과로 보고, 데미지 타입을 우선한다
                    if (damageType == DamageType.Physical) return MonsterSkillCategory.Physical;
                    if (damageType == DamageType.Magic) return MonsterSkillCategory.MagicAttack;
                    return MonsterSkillCategory.Debuff;
                }

                // 순수 공격: damageType 기준
                if (damageType == DamageType.Magic) return MonsterSkillCategory.MagicAttack;
                return MonsterSkillCategory.Physical; // 기본값
            }
        }
    }
}
