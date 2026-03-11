using UnityEngine;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 무기 카테고리 - 각 무기 트리(Lore)가 어느 무기용 스킬인지 구분하기 위함
    /// </summary>
    public enum WeaponCategory
    {
        None,
        Sword,
        Dagger,
        Katana,
        Dual,      // 한손 무기 2개 전용
        Spear,
        Polearm,
        Bow,
        Axe,
        Hammer,
        Shield,
        Fist
    }

    /// <summary>
    /// 무기 전용 Lore 스킬 데이터
    /// - 실제 전투 효과는 전투 시스템에서 이 데이터를 참조하여 구현
    /// - PathSkillData와 동일한 타입/코스트 개념을 재사용
    /// </summary>
    [CreateAssetMenu(fileName = "LoreSkill_", menuName = "Abyssdawn/Weapon Lore Skill", order = 30)]
    public class WeaponLoreSkillData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("스킬 ID (예: Dagger_FlickeringBlade)")]
        public string skillID;

        [Tooltip("스킬 이름")]
        public string skillName;

        [Tooltip("어느 무기 카테고리 전용 스킬인지")]
        public WeaponCategory weaponCategory = WeaponCategory.None;

        [Tooltip("트리 상 티어 (1,2,3 등)")]
        public int tier = 1;

        [Header("분류 / 코스트")]
        public PathSkillType type = PathSkillType.Passive;
        public PathSkillCostType costType = PathSkillCostType.None;
        public float costValue = 0f; // HP% / MP% / 행동 소모 등

        [Header("설명")]
        [TextArea(3, 10)]
        public string effectDescription;

        [Tooltip("대가 / 패널티 설명 (선택)")]
        [TextArea(2, 6)]
        public string drawbackDescription;
    }
}

