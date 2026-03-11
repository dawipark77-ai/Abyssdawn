using UnityEngine;

namespace AbyssdawnBattle
{
    public enum PathSkillType
    {
        Passive,
        ActiveAttack,
        ActiveSupport,
        ActiveHeal,
        ActiveManeuver,
        ActiveUtility
    }

    public enum PathSkillCostType
    {
        None,
        HPPercent,
        MPPercent,
        ActionOnly
    }

    /// <summary>
    /// 직업별 Path 스킬 데이터 (티어/설명만 담는 순수 데이터 SO)
    /// 실제 효과는 전투 시스템에서 이 데이터를 참고하여 구현.
    /// </summary>
    [CreateAssetMenu(fileName = "PathSkill_", menuName = "Abyssdawn/Path Skill", order = 21)]
    public class PathSkillData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("스킬 ID (예: Warrior_T1_BloodPrice)")]
        public string skillID;

        [Tooltip("스킬 이름")]
        public string skillName;

        [Tooltip("해당 스킬이 속한 직업")]
        public JobClassType jobClass;

        [Tooltip("티어 (1티어, 2티어 등)")]
        public int tier = 1;

        [Header("분류 / 코스트")]
        public PathSkillType type = PathSkillType.Passive;
        public PathSkillCostType costType = PathSkillCostType.None;

        [Tooltip("코스트 값 (HP% 또는 MP% 등으로 해석)")]
        public float costValue = 0f;

        [Header("설명")]
        [TextArea(3, 8)]
        public string effectDescription;

        [Tooltip("대가 / 패널티 설명 (선택)")]
        [TextArea(2, 6)]
        public string drawbackDescription;
    }
}

