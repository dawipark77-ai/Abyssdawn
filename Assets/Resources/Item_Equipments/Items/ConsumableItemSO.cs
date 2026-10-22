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

        [Header("Growth (2026-10-08 — 데이터만, 사용 효과는 아직 연결 안 됨)")]
        [Tooltip("쓰면 얻는 스킬 포인트 (예: 기억의 서 1). 아직 ConsumableEffectApplier 에 연결하지 않음")]
        public int skillPointGain = 0;

        [Header("Side Effect (2026-10-08) — 확률로 상태이상 (예: 약초 20% 독 3턴)")]
        [Tooltip("부작용이 생길 확률 (0 = 없음)")]
        [Range(0f, 1f)] public float sideEffectChance = 0f;
        [Tooltip("부작용 상태이상 종류")]
        public StatusEffectType sideEffectType = StatusEffectType.Poison;
        [Tooltip("부작용 지속 턴 (던전에서는 한 걸음 = 1턴)")]
        public int sideEffectTurns = 3;

        [Header("Light (2026-10-22) — 횃불")]
        [Tooltip("던전에서 쓰면 이 걸음 수 동안 빛을 든다 (시야 3 → 5칸, 함정 감지 ↑). 0 = 빛 아이템 아님")]
        public int lightSteps = 0;

        [Header("Return (2026-10-22) — 귀향의 거울")]
        [Tooltip("던전에서 쓰면 가장 가까운 위쪽 마을 층의 마을 문 앞으로 돌아가 마을을 연다")]
        public bool returnsToTown = false;

        [Header("Revive (2026-10-22) — 쓰러진 아군을 되살림")]
        [Tooltip("쓰러진(HP 0) 아군을 최대 HP 의 이 비율로 되살린다. 0 = 부활 아이템 아님. 살아 있는 아군에게는 쓸 수 없다")]
        [Range(0f, 1f)] public float reviveHpPercent = 0f;

        /// <summary>[2026-10-22] 잡템 — 쓸 수 없고 팔기만 하는 물건 (쥐뼈 · 박쥐 날개 · 함정 부품 등). 아이템 창에는 안 보이고 상점 팔기에만 나온다.</summary>
        public bool IsJunk => !usableInBattle && !usableOnMap && !isDawnChalice && !isPermanent;

        /// <summary>부활 아이템인지 (쓰러진 아군에게만 쓴다).</summary>
        public bool IsRevive => reviveHpPercent > 0f;

        [Header("Loot (2026-10-08)")]
        [Tooltip("켜면 보물상자에서 나오지 않음 (예: 정화수 — 너무 강해 일단 뺌)")]
        public bool excludeFromChests = false;

        [Header("Penalty")]
        [Range(0f, 1f)] public float mpPenaltyPercent;

        [Header("Inventory")]
        public int  maxStack      = 50; // [2026-10-09] 소비 아이템은 종류당 50개까지
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
        [Tooltip("Buy price at the town shop (gold). 0 = not sold")]
        public int buyPrice = 0;
        [Tooltip("Sell price at shop (gold)")]
        public int sellPrice = 0;
    }
}
