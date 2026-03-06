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
            
            return bonuses.Count > 0 ? string.Join(", ", bonuses) : "No bonuses";
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








