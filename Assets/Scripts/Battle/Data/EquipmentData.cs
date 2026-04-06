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
    public class EquipmentData : BaseItemSO
    {
        // description → BaseItemSO.description (필드명 동일, 직렬화 보존)
        // equipmentName / equipmentIcon 은 BaseItemSO.itemName / icon 과 이름이 달라 그대로 유지

        [Header("━━━━━━━━━━ Basic Info ━━━━━━━━━━")]
        [Tooltip("Equipment name")]
        public string equipmentName;

        [Tooltip("Icon shown in the inventory screen")]
        public Sprite equipmentIcon;

        [Tooltip("Flat icon for battle UI (falls back to equipmentIcon if empty)")]
        public Sprite flatIcon;

        [Space(5)]
        [Tooltip("Equipment type: Hand, TwoHanded, Armour, Accessory")]
        public EquipmentType equipmentType;

        [Tooltip("Two-handed weapon flag (automatically true for TwoHanded type)")]
        public bool isTwoHanded = false;

        [Tooltip("Weapon category for skill compatibility checks.\nNone = not a weapon (armor/accessory) or universal.\nSet to Sword/Dagger/Bow etc. for category-restricted skills.")]
        public WeaponCategory weaponCategory = WeaponCategory.None;
        
        [Space(5)]
        [Header("━━━━━━━━━━ Armour Category (Armour type only) ━━━━━━━━━━")]
        [Tooltip("Armour category: Light Armour or Heavy Armour.\nSelect this first, then choose the subtype below.")]
        public ArmourCategory armourCategory = ArmourCategory.None;
        
        [Tooltip("Armour subtype.\nLight: Cloth, Leather\nHeavy: Plate, FullArmour\nClasses and skills may grant bonuses based on armour type.")]
        public ArmourType armourType = ArmourType.None;

        [Header("━━━━━━━━━━ Base Stat Bonuses ━━━━━━━━━━")]
        [Tooltip("Attack bonus")]
        public int attackBonus = 0;
        
        [Tooltip("Defense bonus")]
        public int defenseBonus = 0;
        
        [Tooltip("Magic bonus")]
        public int magicBonus = 0;

        [Space(10)]
        [Header("━━━━━━━━━━ Additional Stat Bonuses ━━━━━━━━━━")]
        [Tooltip("Max HP bonus")]
        public int hpBonus = 0;
        
        [Tooltip("Max MP flat bonus")]
        public int mpBonus = 0;
        
        [Tooltip("Max MP percent bonus (0.05 = +5%). Increases max MP by base MP × this value.")]
        [Range(0f, 1f)]
        public float mpBonusPercent = 0f;
        
        [Tooltip("Agility bonus")]
        public int agiBonus = 0;
        
        [Tooltip("Luck bonus")]
        public int luckBonus = 0;
        
        [Tooltip("Accuracy bonus (-1.0 ~ 1.0, e.g. 0.1 = +10%, -0.05 = -5%)")]
        [Range(-1f, 1f)]
        public float accuracyBonus = 0f;

        [Space(10)]
        [Header("━━━━━━━━━━ Armor Break ━━━━━━━━━━")]
        [Tooltip("Assign an ArmorBreakDataSO to enable armor break logic.\nLeave empty to disable.")]
        public ArmorBreakDataSO armorBreakData;

        [Tooltip("[Legacy] Direct coefficient input. Used only when armorBreakData SO is not assigned.")]
        [Range(0f, 0.1f)]
        public float armorBreakCoefficient = 0f;

        /// <summary>
        /// Returns the effective armor break coefficient (SO takes priority over legacy float).
        /// </summary>
        public float GetArmorBreakCoefficient()
        {
            return armorBreakData != null ? armorBreakData.coefficient : armorBreakCoefficient;
        }

        [Space(10)]
        [Header("━━━━━━━━━━ Shield Block ━━━━━━━━━━")]
        [Tooltip("Assign a BlockDataSO to enable block logic.\nLeave empty to disable.")]
        public BlockDataSO blockData;

        [Space(10)]
        [Header("━━━━━━━━━━ Special Effects ━━━━━━━━━━")]
        [Tooltip("Weapon effect skill automatically available when this equipment is equipped")]
        public SkillData weaponEffect;
        
        [Space(5)]
        [Tooltip("Skill that can be learned or used while this equipment is worn")]
        public SkillData skill;

        [Space(10)]
        [Header("━━━━━━━━━━ Weapon Status Effects ━━━━━━━━━━")]
        [Tooltip("Status effects applied to the enemy on a successful hit.\nAdd StatusEffectSOs from the Curse folder.\nApplication chance is defined by physicalApplyChance inside each SO.")]
        public List<StatusEffectSO> weaponCurses = new List<StatusEffectSO>();

        [Space(10)]
        [Header("━━━━━━━━━━ Magic System ━━━━━━━━━━")]
        [Tooltip("Magic damage amplification multiplier. 1.0 = default, 1.2 = +20% magic damage.\nMultiplied together across all equipped items.")]
        [Range(1f, 3f)]
        public float magicAmplify = 1f;

        [Tooltip("Backflow suppression rate (0.0 ~ 1.0). 0.3 = reduces backflow chance by 30%.\nAdded across all equipped items, capped at 0.8.")]
        [Range(0f, 1f)]
        public float backflowSuppression = 0f;

        [Space(10)]
        [Header("━━━━━━━━━━ Economy ━━━━━━━━━━")]
        [Tooltip("Sell price at shop (gold)")]
        public int sellPrice = 0;
    }
}


