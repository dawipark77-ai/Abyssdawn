using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 소비 아이템 정렬 카테고리. enum 정수 값이 정렬 우선순위가 됩니다.
    /// HpRecovery(0) → MpRecovery(1) → StatusCure(2) → BattleSupport(3) → Special(4)
    /// </summary>
    public enum ItemCategory
    {
        HpRecovery    = 0,
        MpRecovery    = 1,
        StatusCure    = 2,
        BattleSupport = 3,
        Special       = 4,
    }

    [CreateAssetMenu(fileName = "ConsumableItem_", menuName = "Abyssdawn/Consumable Item", order = 30)]
    public class ConsumableItemSO : BaseItemSO
    {
        // itemName   → BaseItemSO.itemName  (필드명 동일, 직렬화 보존)
        // description → BaseItemSO.description (필드명 동일, 직렬화 보존)
        // itemIcon / flatIcon 은 BaseItemSO.icon 과 이름이 달라 그대로 유지

        [Header("정렬 카테고리")]
        [Tooltip("아이템 정렬 순서 결정 (HP회복 → MP회복 → 해독 → 전투보조 → 특수)")]
        public ItemCategory itemCategory = ItemCategory.BattleSupport;

        [Header("Icons")]
        public Sprite itemIcon;   // 인벤토리 화면 아이콘
        [FormerlySerializedAs("icon")]
        public Sprite flatIcon;   // 전투 UI 플랫 아이콘

        [Header("Recovery")]
        [Range(0f, 1f)] public float hpRecoveryPercent;
        [Range(0f, 1f)] public float mpRecoveryPercent;

        [Header("Status Cure")]
        public List<StatusEffectType> cureTypes = new List<StatusEffectType>();

        [Header("Buffs")]
        [Range(0f, 1f)] public float attackBuffPercent;
        public int     agilityBuff;
        [Range(0f, 1f)] public float evasionBuff;
        [Range(0f, 1f)] public float escapeChanceBuff;
        public int     buffDuration;

        [Header("Penalty")]
        [Range(0f, 1f)] public float mpPenaltyPercent;

        [Header("Inventory")]
        public int  maxStack      = 5;
        public bool usableInBattle = true;
        public bool usableOnMap    = true;

        [Header("Special Settings")]
        [Tooltip("If true, the item remains in the inventory even at 0 quantity. (e.g. Dawn Chalice)")]
        public bool isPermanent = false;

        [Tooltip("If true, can only be recharged at designated refill locations every 5 floors.")]
        public bool isDawnChalice = false;

        [Tooltip("Items sharing the same string share a combined stack limit. e.g. HealthPotion")]
        public string stackGroup = "";

        [Header("Charges")]
        [Tooltip("If true, this item uses a charge system instead of a quantity stack.")]
        public bool isChargeable = false;
        [Tooltip("Maximum number of charges when fully refilled.")]
        public int  maxCharges   = 0;
        [Tooltip("Current remaining charges.")]
        public int  currentCharges = 0;

        [Header("Economy")]
        [Tooltip("Sell price at shop (gold)")]
        public int sellPrice = 0;
    }
}
