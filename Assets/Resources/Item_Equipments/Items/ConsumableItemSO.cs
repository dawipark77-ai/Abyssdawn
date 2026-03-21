using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    [CreateAssetMenu(fileName = "ConsumableItem_", menuName = "Abyssdawn/Consumable Item", order = 30)]
    public class ConsumableItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string itemName;
        [TextArea(2, 4)]
        public string description;

        [Header("Icons")]
        public Sprite itemIcon;   // inventory screen icon
        [FormerlySerializedAs("icon")]
        public Sprite flatIcon;   // battle UI flat icon

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

        [Header("Economy")]
        [Tooltip("Sell price at shop (gold)")]
        public int sellPrice = 0;
    }
}
