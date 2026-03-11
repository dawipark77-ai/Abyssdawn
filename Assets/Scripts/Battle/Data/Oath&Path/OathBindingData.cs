using UnityEngine;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 직업별 서약(Oath) & 제약(Binding) 데이터
    /// - 실제 전투 로직은 개별 시스템에서 이 데이터를 참조하여 구현
    /// </summary>
    public enum OathBindingStrength
    {
        Weak,       // 약한 제약
        Medium,     // 중간 제약
        Strong      // 강한 제약
    }

    public enum JobClassType
    {
        Warrior,
        Monk,
        Mage,
        Thief,
        Sister
    }

    [CreateAssetMenu(fileName = "OathBinding_", menuName = "Abyssdawn/Oath & Binding", order = 20)]
    public class OathBindingData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("서약 ID (예: Warrior_IronDiscipline)")]
        public string oathID;

        [Tooltip("서약 이름 (영문 또는 한글)")]
        public string oathName;

        [Tooltip("해당 서약이 속한 직업")]
        public JobClassType jobClass;

        [Header("서약 설명")]
        [Tooltip("서약 효과에 대한 상세 설명 (UI 표기용)")]
        [TextArea(3, 6)]
        public string oathDescription;

        [Header("제약 정보")]
        [Tooltip("제약 강도 (약함/중간/강함)")]
        public OathBindingStrength bindingStrength;

        [Tooltip("제약 효과에 대한 상세 설명 (UI 표기용)")]
        [TextArea(2, 5)]
        public string bindingDescription;
    }
}

