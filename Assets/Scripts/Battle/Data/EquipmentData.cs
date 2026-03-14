using UnityEngine;
using System.Collections.Generic;

namespace AbyssdawnBattle
{
    public enum EquipmentType { Hand, TwoHanded, Armour, Accessory }
    
    /// <summary>
    /// 방어구 카테고리 (Armour 타입 장비에만 적용)
    /// 경갑(Light Armour) 또는 중갑(Heavy Armour)을 먼저 선택
    /// </summary>
    public enum ArmourCategory
    {
        None,           // 갑옷이 아니거나 미지정
        LightArmour,    // 경갑
        HeavyArmour     // 중갑
    }
    
    /// <summary>
    /// 방어구 세부 타입 (ArmourCategory 선택 후 세부 타입 선택)
    /// Light Armour: Cloth(천), Leather(가죽)
    /// Heavy Armour: Plate(판금), FullArmour(전신갑)
    /// </summary>
    public enum ArmourType 
    { 
        None,           // 미지정
        Cloth,          // 경갑 - 천
        Leather,        // 경갑 - 가죽
        Plate,          // 중갑 - 판금
        FullArmour      // 중갑 - 전신갑
    }

    [CreateAssetMenu(fileName = "Equipment_", menuName = "Abyssdawn/Equipment Data", order = 2)]
    public class EquipmentData : ScriptableObject
    {
        [Header("━━━━━━━━━━ 기본 정보 ━━━━━━━━━━")]
        [Tooltip("장비 이름")]
        public string equipmentName;
        
        [Tooltip("장비 아이콘")]
        public Sprite equipmentIcon;
        
        [TextArea(2, 4)]
        [Tooltip("장비 설명")]
        public string description;
        
        [Space(5)]
        [Tooltip("장비 타입: Hand(한손), TwoHanded(양손), Armour(갑옷), Accessory(장신구)")]
        public EquipmentType equipmentType;
        
        [Tooltip("양손 무기 여부 (TwoHanded 타입이면 자동으로 true로 간주)")]
        public bool isTwoHanded = false;
        
        [Space(5)]
        [Header("━━━━━━━━━━ 방어구 분류 (Armour 타입 장비에만 적용) ━━━━━━━━━━")]
        [Tooltip("방어구 카테고리: 경갑(Light Armour) 또는 중갑(Heavy Armour)\n" +
                 "먼저 경갑/중갑을 선택한 후, 아래에서 세부 타입을 선택하세요.")]
        public ArmourCategory armourCategory = ArmourCategory.None;
        
        [Tooltip("방어구 세부 타입\n" +
                 "경갑: Cloth(천), Leather(가죽)\n" +
                 "중갑: Plate(판금), FullArmour(전신갑)\n" +
                 "직업/스킬이 장비 종류에 따라 보정치를 받을 수 있습니다.")]
        public ArmourType armourType = ArmourType.None;

        [Header("━━━━━━━━━━ 기본 스탯 보너스 ━━━━━━━━━━")]
        [Tooltip("공격력 보정치")]
        public int attackBonus = 0;
        
        [Tooltip("방어력 보정치")]
        public int defenseBonus = 0;
        
        [Tooltip("마법력 보정치")]
        public int magicBonus = 0;

        [Space(10)]
        [Header("━━━━━━━━━━ 추가 스탯 보너스 ━━━━━━━━━━")]
        [Tooltip("최대 HP 보정치")]
        public int hpBonus = 0;
        
        [Tooltip("최대 MP 고정 보정치")]
        public int mpBonus = 0;
        
        [Tooltip("최대 MP 퍼센트 보정치 (0.05 = +5%). 기본 MP × 이 값만큼 최대 MP 증가.")]
        [Range(0f, 1f)]
        public float mpBonusPercent = 0f;
        
        [Tooltip("민첩 보정치")]
        public int agiBonus = 0;
        
        [Tooltip("행운 보정치")]
        public int luckBonus = 0;
        
        [Tooltip("명중률 보정치 (-1.0 ~ 1.0, 예: 0.1 = 10% 증가, -0.05 = 5% 감소)")]
        [Range(-1f, 1f)]
        public float accuracyBonus = 0f;

        [Space(10)]
        [Header("━━━━━━━━━━ 방어구 파괴 ━━━━━━━━━━")]
        [Tooltip("방어구 파괴 데이터 SO를 연결하세요.\n" +
                 "끼우면 자동으로 방어구 파괴 로직이 적용됩니다.\n" +
                 "null이면 방어구 파괴 없음.")]
        public ArmorBreakDataSO armorBreakData;

        [Tooltip("[레거시] 직접 계수 입력 방식. armorBreakData SO가 없을 때만 사용됩니다.")]
        [Range(0f, 0.1f)]
        public float armorBreakCoefficient = 0f;

        /// <summary>
        /// 실제 방어구 파괴 계수 반환 (SO 우선, 없으면 레거시 float)
        /// </summary>
        public float GetArmorBreakCoefficient()
        {
            return armorBreakData != null ? armorBreakData.coefficient : armorBreakCoefficient;
        }

        [Space(10)]
        [Header("━━━━━━━━━━ 방패 블록 ━━━━━━━━━━")]
        [Tooltip("블록 데이터 SO를 연결하세요.\n" +
                 "끼우면 자동으로 블록 로직이 적용됩니다.\n" +
                 "null이면 블록 없음.")]
        public BlockDataSO blockData;

        [Space(10)]
        [Header("━━━━━━━━━━ 특수 효과 ━━━━━━━━━━")]
        [Tooltip("장비 시 자동으로 사용 가능한 무기 효과 스킬")]
        public SkillData weaponEffect;
        
        [Space(5)]
        [Tooltip("장비 착용 시 배우거나 사용할 수 있는 스킬")]
        public SkillData skill;

        [Space(10)]
        [Header("━━━━━━━━━━ 무기 상태이상 효과 ━━━━━━━━━━")]
        [Tooltip("이 무기로 공격 명중 시 적에게 부여하는 상태이상 목록.\n" +
                 "Curse 폴더의 StatusEffectSO를 여러 개 추가할 수 있습니다.\n" +
                 "부여 확률은 각 SO 내부의 physicalApplyChance 값을 사용합니다.")]
        public List<StatusEffectSO> weaponCurses = new List<StatusEffectSO>();
    }
}


