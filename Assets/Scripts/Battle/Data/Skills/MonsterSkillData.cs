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
    [CreateAssetMenu(fileName = "New Monster Skill", menuName = "Battle/Monster Skill Data")]
    public class MonsterSkillData : SkillData
    {
        [Header("Basic Attack Override (Bite 전용)")]
        public bool isBasicAttackOverride;
        public float atkMultiplierOverride;
        public float randomRollMaxOverride;
    }
}
