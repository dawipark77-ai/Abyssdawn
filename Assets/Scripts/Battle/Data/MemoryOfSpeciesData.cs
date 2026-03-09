using UnityEngine;
using System.Collections.Generic;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 종족 타입
    /// </summary>
    public enum SpeciesType
    {
        Human,      // 인간
        Elf,        // 엘프
        Orc,        // 오크
        Halfling,   // 하플링
        Dwarf       // 드워프
    }

    /// <summary>
    /// 종의 기억 데이터
    /// 생물학적 특징을 스탯 보정으로 표현
    /// </summary>
    [CreateAssetMenu(fileName = "Memory_", menuName = "Abyssdawn/Memory of Species", order = 10)]
    public class MemoryOfSpeciesData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("고유 식별자 (예: Human_Skin)")]
        public string memoryID;
        
        [Tooltip("종의 기억 이름 (영문)")]
        public string memoryName;
        
        [Tooltip("부제/특징 (한글 가능)")]
        public string subtitle;
        
        [Tooltip("종족 타입")]
        public SpeciesType species = SpeciesType.Human;
        
        [Header("Icon")]
        [Tooltip("종의 기억 아이콘")]
        public Sprite memoryIcon;
        
        [Header("Stat Bonuses")]
        [Tooltip("최대 HP 보정 (%)")]
        [Range(-50f, 50f)]
        public float hpBonusPercent = 0f;
        
        [Tooltip("HP 보정 (절대값)")]
        public int hpBonus = 0;
        
        [Tooltip("공격력 보정")]
        [Range(-10, 10)]
        public int attackBonus = 0;
        
        [Tooltip("방어력 보정")]
        [Range(-10, 10)]
        public int defenseBonus = 0;
        
        [Tooltip("민첩 보정")]
        [Range(-10, 10)]
        public int agilityBonus = 0;
        
        [Tooltip("마력 보정")]
        [Range(-10, 10)]
        public int magicBonus = 0;
        
        [Tooltip("행운 보정")]
        [Range(-10, 10)]
        public int luckBonus = 0;
        
        [Header("Level Growth (레벨업 성장치)")]
        [Tooltip("레벨당 HP 성장치 (예: 0.2 = 레벨당 +0.2 HP)")]
        [Range(-5f, 5f)]
        public float hpGrowthPerLevel = 0f;
        
        [Tooltip("레벨당 MP 성장치 (예: 0.1 = 레벨당 +0.1 MP)")]
        [Range(-5f, 5f)]
        public float mpGrowthPerLevel = 0f;
        
        [Tooltip("레벨당 공격력 성장치 (예: 0.3 = 레벨당 +0.3 공격력)")]
        [Range(-5f, 5f)]
        public float attackGrowthPerLevel = 0f;
        
        [Tooltip("레벨당 방어력 성장치 (예: 0.2 = 레벨당 +0.2 방어력)")]
        [Range(-5f, 5f)]
        public float defenseGrowthPerLevel = 0f;
        
        [Tooltip("레벨당 마력 성장치 (예: 0.2 = 레벨당 +0.2 마력)")]
        [Range(-5f, 5f)]
        public float magicGrowthPerLevel = 0f;
        
        [Tooltip("레벨당 민첩 성장치 (예: 0.1 = 레벨당 +0.1 민첩)")]
        [Range(-5f, 5f)]
        public float agilityGrowthPerLevel = 0f;
        
        [Tooltip("레벨당 행운 성장치 (예: 0.2 = 레벨당 +0.2 행운)")]
        [Range(-5f, 5f)]
        public float luckGrowthPerLevel = 0f;
        
        [Header("Description")]
        [Tooltip("득실 정보 (효과 상세 설명)")]
        [TextArea(3, 6)]
        public string description;
        
        [Tooltip("해설 (인용문/플레이버 텍스트)")]
        [TextArea(2, 4)]
        public string flavorText;
        
        [Header("Species Trait (Set Bonus)")]
        [Tooltip("특성 발동에 필요한 종의 기억 개수")]
        public int requiredMemoriesForTrait = 3;
        
        [Tooltip("종의 특성 이름 (3개 모였을 때)")]
        public string traitName;
        
        [Tooltip("종의 특성 설명")]
        [TextArea(2, 4)]
        public string traitDescription;
        
        /// <summary>
        /// 총 스탯 보정치 요약
        /// </summary>
        public string GetStatSummary()
        {
            List<string> bonuses = new List<string>();
            
            if (hpBonusPercent != 0)
                bonuses.Add($"HP {(hpBonusPercent > 0 ? "+" : "")}{hpBonusPercent}%");
            if (hpBonus != 0)
                bonuses.Add($"HP {(hpBonus > 0 ? "+" : "")}{hpBonus}");
            if (attackBonus != 0)
                bonuses.Add($"ATK {(attackBonus > 0 ? "+" : "")}{attackBonus}");
            if (defenseBonus != 0)
                bonuses.Add($"DEF {(defenseBonus > 0 ? "+" : "")}{defenseBonus}");
            if (agilityBonus != 0)
                bonuses.Add($"AGI {(agilityBonus > 0 ? "+" : "")}{agilityBonus}");
            if (magicBonus != 0)
                bonuses.Add($"MAG {(magicBonus > 0 ? "+" : "")}{magicBonus}");
            if (luckBonus != 0)
                bonuses.Add($"LUK {(luckBonus > 0 ? "+" : "")}{luckBonus}");
            
            // 레벨업 성장치 표시
            if (hpGrowthPerLevel != 0)
                bonuses.Add($"HP +{hpGrowthPerLevel:F1}/레벨");
            if (mpGrowthPerLevel != 0)
                bonuses.Add($"MP +{mpGrowthPerLevel:F1}/레벨");
            if (attackGrowthPerLevel != 0)
                bonuses.Add($"ATK +{attackGrowthPerLevel:F1}/레벨");
            if (defenseGrowthPerLevel != 0)
                bonuses.Add($"DEF +{defenseGrowthPerLevel:F1}/레벨");
            if (magicGrowthPerLevel != 0)
                bonuses.Add($"MAG +{magicGrowthPerLevel:F1}/레벨");
            if (agilityGrowthPerLevel != 0)
                bonuses.Add($"AGI +{agilityGrowthPerLevel:F1}/레벨");
            if (luckGrowthPerLevel != 0)
                bonuses.Add($"LUK +{luckGrowthPerLevel:F1}/레벨");
            
            return bonuses.Count > 0 ? string.Join(", ", bonuses) : "No bonuses";
        }
        
        /// <summary>
        /// 특정 레벨에서의 성장치 보정치 계산
        /// </summary>
        /// <param name="level">현재 레벨</param>
        /// <returns>레벨에 따른 총 성장치 보정치</returns>
        public (float hp, float mp, float attack, float defense, float magic, float agility, float luck) GetGrowthBonusesAtLevel(int level)
        {
            return (
                hp: hpGrowthPerLevel * level,
                mp: mpGrowthPerLevel * level,
                attack: attackGrowthPerLevel * level,
                defense: defenseGrowthPerLevel * level,
                magic: magicGrowthPerLevel * level,
                agility: agilityGrowthPerLevel * level,
                luck: luckGrowthPerLevel * level
            );
        }
        
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        public override string ToString()
        {
            return $"[{species}] {memoryName}: {GetStatSummary()}";
        }
    }
}









