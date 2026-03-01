using UnityEngine;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 종의 특성 (Species Trait) - 종의 기억 3개 수집 시 활성화되는 특수 효과
    /// </summary>
    [CreateAssetMenu(fileName = "Trait_", menuName = "Abyssdawn/Traits Of Species Data", order = 4)]
    public class TraitsOfSpeciesData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("특성 ID (예: Human_Trait_Endurance)")]
        public string traitID;
        
        [Tooltip("특성 이름 (한글)")]
        public string traitName;
        
        [Tooltip("특성 이름 (영어)")]
        public string traitNameEnglish;
        
        [Tooltip("어떤 종족의 특성인가")]
        public SpeciesType species;
        
        [Tooltip("특성 아이콘")]
        public Sprite traitIcon;

        [Header("Activation Requirement")]
        [Tooltip("활성화에 필요한 종의 기억 개수")]
        public int requiredMemoryCount = 3;
        
        [Tooltip("같은 종족의 기억만 카운트되는가?")]
        public bool requireSameSpecies = true;

        [Header("Stat Bonuses")]
        [Tooltip("HP 보정치 (%)")]
        public float hpBonusPercent = 0f;
        
        [Tooltip("HP 고정 보정치")]
        public int hpBonus = 0;
        
        [Tooltip("공격력 보정치")]
        public int attackBonus = 0;
        
        [Tooltip("방어력 보정치")]
        public int defenseBonus = 0;
        
        [Tooltip("민첩 보정치")]
        public int agilityBonus = 0;
        
        [Tooltip("마력 보정치")]
        public int magicBonus = 0;
        
        [Tooltip("행운 보정치")]
        public int luckBonus = 0;

        [Header("Description")]
        [TextArea(3, 6)]
        [Tooltip("특성 효과 설명")]
        public string description;
        
        [TextArea(2, 4)]
        [Tooltip("특성 해설/배경 스토리")]
        public string flavorText;

        [Header("Special Effects")]
        [Tooltip("특수 효과 태그 (예: Regeneration, CriticalBoost 등)")]
        public string[] specialEffectTags;

        /// <summary>
        /// 이 특성이 활성화되었는지 확인
        /// </summary>
        public bool IsActive(MemoryOfSpeciesData[] equippedMemories)
        {
            if (equippedMemories == null || equippedMemories.Length < requiredMemoryCount)
                return false;

            int count = 0;
            foreach (var memory in equippedMemories)
            {
                if (memory == null) continue;
                
                if (requireSameSpecies && memory.species == species)
                    count++;
                else if (!requireSameSpecies)
                    count++;
            }

            return count >= requiredMemoryCount;
        }

        /// <summary>
        /// 특성 정보를 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            return $"[{species} Trait] {traitNameEnglish}: {description}";
        }
    }
}
