using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle;

/// <summary>
/// 소비 아이템 수량 관리, stackGroup 합산 제한, 새벽의 잔 특수 충전 로직을 담당합니다.
/// </summary>
public class ConsumableInventory : MonoBehaviour
{
    // ─── 싱글톤 ───────────────────────────────────────────────
    public static ConsumableInventory Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── 런타임 슬롯 ──────────────────────────────────────────
    [System.Serializable]
    public class ConsumableSlot
    {
        public ConsumableItemSO item;
        public int quantity;

        public ConsumableSlot(ConsumableItemSO item, int qty)
        {
            this.item     = item;
            this.quantity = qty;
        }
    }

    [Header("소지 아이템 목록")]
    public List<ConsumableSlot> slots = new List<ConsumableSlot>();

    [Header("새벽의 잔 설정")]
    [Tooltip("새벽의 잔 SO를 여기에 연결하세요.")]
    public ConsumableItemSO dawnChaliceItem;
    public int dawnChaliceCharges    = 0;
    public int dawnChaliceMaxCharges = 3;

    // ─── 이벤트 ───────────────────────────────────────────────
    public event System.Action OnInventoryChanged;

    // ═════════════════════════════════════════════════════════
    //  공개 API — 아이템 추가
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// 아이템을 count개 추가합니다. 실제 추가된 수량을 반환합니다.
    /// </summary>
    public int AddItem(ConsumableItemSO item, int count = 1)
    {
        if (item == null || count <= 0) return 0;

        if (item.isDawnChalice)
            return AddDawnChalice(count);

        int canAdd = GetAddableCount(item, count);
        if (canAdd <= 0) return 0;

        ConsumableSlot slot = GetSlot(item);
        if (slot == null)
        {
            slot = new ConsumableSlot(item, 0);
            slots.Add(slot);
        }
        slot.quantity += canAdd;
        OnInventoryChanged?.Invoke();
        return canAdd;
    }

    /// <summary>
    /// 아이템을 count개 제거합니다. 제거 성공 여부를 반환합니다.
    /// </summary>
    public bool RemoveItem(ConsumableItemSO item, int count = 1)
    {
        if (item == null) return false;

        if (item.isDawnChalice)
        {
            if (dawnChaliceCharges <= 0) return false;
            dawnChaliceCharges -= count;
            dawnChaliceCharges  = Mathf.Max(0, dawnChaliceCharges);
            OnInventoryChanged?.Invoke();
            return true;
        }

        ConsumableSlot slot = GetSlot(item);
        if (slot == null || slot.quantity < count) return false;

        slot.quantity -= count;
        // 영구 아이템은 수량 0이 돼도 슬롯 유지
        if (slot.quantity <= 0 && !item.isPermanent)
            slots.Remove(slot);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ═════════════════════════════════════════════════════════
    //  공개 API — 조회
    // ═════════════════════════════════════════════════════════

    public int GetQuantity(ConsumableItemSO item)
    {
        if (item == null) return 0;
        if (item.isDawnChalice) return dawnChaliceCharges;
        return GetSlot(item)?.quantity ?? 0;
    }

    public bool HasItem(ConsumableItemSO item, int count = 1)
        => GetQuantity(item) >= count;

    /// <summary>
    /// 아이템 1개를 사용합니다. 수량이 있으면 감소 처리 후 true를 반환합니다.
    /// </summary>
    public bool UseItem(ConsumableItemSO item)
    {
        if (item == null) return false;
        if (!HasItem(item)) return false;
        return RemoveItem(item, 1);
    }

    /// <summary>
    /// stackGroup 합산 수량을 반환합니다.
    /// </summary>
    public int GetGroupQuantity(string stackGroup)
    {
        if (string.IsNullOrEmpty(stackGroup)) return 0;
        int total = 0;
        foreach (var s in slots)
        {
            if (s.item != null && s.item.stackGroup == stackGroup)
                total += s.quantity;
        }
        return total;
    }

    // ═════════════════════════════════════════════════════════
    //  새벽의 잔 — 5층 보충 포인트
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// 5층마다 있는 보충 장소에서 호출합니다. 최대치까지 충전합니다.
    /// </summary>
    public void RefillDawnChalice()
    {
        if (dawnChaliceItem == null) return;
        dawnChaliceCharges = dawnChaliceMaxCharges;
        Debug.Log($"[ConsumableInventory] 새벽의 잔 충전 완료: {dawnChaliceCharges}/{dawnChaliceMaxCharges}");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 현재 층수가 보충 장소 층인지 확인합니다. (5의 배수)
    /// </summary>
    public static bool IsRefillFloor(int floor) => floor > 0 && floor % 5 == 0;

    // ═════════════════════════════════════════════════════════
    //  내부 헬퍼
    // ═════════════════════════════════════════════════════════

    private int AddDawnChalice(int count)
    {
        int canAdd = Mathf.Min(count, dawnChaliceMaxCharges - dawnChaliceCharges);
        if (canAdd <= 0) return 0;
        dawnChaliceCharges += canAdd;
        OnInventoryChanged?.Invoke();
        return canAdd;
    }

    /// <summary>
    /// 스택 제한을 고려해 실제로 추가 가능한 수량을 계산합니다.
    /// stackGroup이 있으면 그룹 합산으로 제한합니다.
    /// </summary>
    private int GetAddableCount(ConsumableItemSO item, int wantToAdd)
    {
        int currentQty;
        int cap;

        if (!string.IsNullOrEmpty(item.stackGroup))
        {
            currentQty = GetGroupQuantity(item.stackGroup);
            cap        = item.maxStack;
        }
        else
        {
            currentQty = GetSlot(item)?.quantity ?? 0;
            cap        = item.maxStack;
        }

        return Mathf.Max(0, Mathf.Min(wantToAdd, cap - currentQty));
    }

    private ConsumableSlot GetSlot(ConsumableItemSO item)
        => slots.Find(s => s.item == item);
}
