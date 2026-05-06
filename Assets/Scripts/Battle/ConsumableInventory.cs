using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle;

/// <summary>
/// 소비 아이템 수량 관리, stackGroup 합산 제한, 새벽의 잔 특수 충전 로직을 담당합니다.
/// </summary>
public class ConsumableInventory : MonoBehaviour
{
    // ─── 싱글톤 (lazy 초기화 + 씬 영속) ────────────────────────
    private static ConsumableInventory _instance;

    /// <summary>
    /// 플레이 세션 동안 새벽의 잔 차지의 단일 저장소.
    /// 인스펙터 기본값·씬 재로드 시 새 컴포넌트가 생겨도 여기 값이 진실이 된다.
    /// </summary>
    private static int s_sessionDawnCharges = int.MinValue;

    private const int SessionDawnUnset = int.MinValue;

    public static ConsumableInventory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ConsumableInventory>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    GameObject go = new GameObject("[Auto-Created] ConsumableInventory");
                    _instance = go.AddComponent<ConsumableInventory>();
                    // [ROLLBACK 2026-05-06] DontDestroyOnLoad 제거 — 던전 씬의 InventoryUIManager 인스펙터 참조가 깨지는 문제 해결
                    Debug.LogWarning("[ConsumableInventory] 씬에서 컴포넌트를 찾지 못해 자동 생성했습니다. 적절한 GameObject에 수동 부착을 권장합니다.");
                }
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("디버그 - 테스트용")]
    [Tooltip("게임 시작 시 모든 아이템 자동 지급")]
    public bool grantAllItemsOnStart = false;

    [Tooltip("디버그 키로 모든 아이템 지급 (F12)")]
    public bool enableDebugKey = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // 씬에 ConsumableInventory가 또 있으면: DontDestroyOnLoad로 살아 있는 영속 인스턴스가 이미 있음.
            // Destroy(gameObject)는 같은 오브젝트에 붙은 다른 컴포넌트/UI까지 날려 인벤 참조가 깨지므로 컴포넌트만 제거한다.
            Destroy(this);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);   // 씬 전환 시에도 dawnChaliceCharges 유지

        ApplySessionDawnChargesOnAwake();
        Debug.Log($"[ConsumableInventory] Awake — Instance 설정, dawnChaliceItem={(dawnChaliceItem != null ? dawnChaliceItem.itemName : "NULL")}, charges={dawnChaliceCharges}/{dawnChaliceMaxCharges} (session={s_sessionDawnCharges})");
    }

    /// <summary>
    /// 세션 저장값이 있으면 복원, 없으면 풀충전으로 세션 시작.
    /// </summary>
    private void ApplySessionDawnChargesOnAwake()
    {
        if (s_sessionDawnCharges != SessionDawnUnset)
            dawnChaliceCharges = Mathf.Clamp(s_sessionDawnCharges, 0, dawnChaliceMaxCharges);
        else
        {
            dawnChaliceCharges = dawnChaliceMaxCharges;
            s_sessionDawnCharges = dawnChaliceCharges;
        }
    }

    private void FlushSessionDawnCharges()
    {
        s_sessionDawnCharges = dawnChaliceCharges;
    }

    private void Start()
    {
        AutoInitializeDawnChalice();

        if (grantAllItemsOnStart)
            GrantAllTestItems();
    }

    private void Update()
    {
        if (enableDebugKey && Input.GetKeyDown(KeyCode.F12))
            GrantAllTestItems();
    }

    /// <summary>
    /// 새벽의 잔을 최대치로 초기화. 세션에 저장값이 이미 있으면 무시 (force=true로 강제).
    /// </summary>
    public void AutoInitializeDawnChalice(bool force = false)
    {
        if (!force && s_sessionDawnCharges != SessionDawnUnset)
        {
            Debug.Log("[ConsumableInventory] AutoInitializeDawnChalice — 세션 저장값 있음, 건너뜀 (force=true로 강제 가능)");
            return;
        }

        if (dawnChaliceItem == null)
        {
            Debug.LogWarning("[ConsumableInventory] dawnChaliceItem이 할당되지 않았습니다. Inspector에서 SO를 연결하세요.");
            return;
        }

        int before = dawnChaliceCharges;
        dawnChaliceCharges = dawnChaliceMaxCharges;
        FlushSessionDawnCharges();
        Debug.Log($"[ConsumableInventory] AutoInitializeDawnChalice — {before} → {dawnChaliceCharges}/{dawnChaliceMaxCharges}");
        OnInventoryChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            FlushSessionDawnCharges();
            _instance = null;
        }
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
            FlushSessionDawnCharges();
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
        FlushSessionDawnCharges();
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
        FlushSessionDawnCharges();
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

    // ═════════════════════════════════════════════════════════
    //  디버그 — 테스트용 일괄 지급
    // ═════════════════════════════════════════════════════════

    [ContextMenu("DEBUG: Grant All Test Items")]
    public void GrantAllTestItems()
    {
        var all = Resources.LoadAll<ConsumableItemSO>("Item_Equipments/Items");
        int totalAdded = 0;

        foreach (var so in all)
        {
            if (so.isDawnChalice) continue; // 새벽의 잔은 이미 있음

            int added = AddItem(so, so.maxStack);
            totalAdded += added;
            Debug.Log($"[Grant] {so.itemName}: +{added}/{so.maxStack}");
        }

        Debug.Log($"[Grant] 총 {totalAdded}개 아이템 지급 완료");
    }
}
