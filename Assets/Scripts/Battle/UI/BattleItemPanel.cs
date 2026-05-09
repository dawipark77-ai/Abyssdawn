using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle;

/// <summary>
/// 전투 중 Item 메뉴용 패널. BattleUIManager가 Item 버튼에서 <see cref="Open"/> 호출.
/// 패널이 활성화되는 동안 ConsumableInventory의 일반 아이템 슬롯을 동적으로 생성/갱신합니다.
/// </summary>
public class BattleItemPanel : MonoBehaviour
{
    [Tooltip("씬 시작 시 패널 끄기 (레이아웃 편집 끄려면 인스펙터에서 해제)")]
    [SerializeField] private bool hideOnAwake = true;

    [Header("Dynamic Slot Generation")]
    [Tooltip("ConsumableItemSlot 프리팹")]
    public GameObject itemSlotPrefab;

    [Tooltip("Scroll View의 Content 트랜스폼")]
    public Transform dynamicSlotsContainer;

    [Header("Optional - 고정 슬롯")]
    [Tooltip("새벽의 잔 고정 슬롯 (선택)")]
    public BattleItemSlot fixedDawnChaliceSlot;

    [Header("Manual Layout Settings")]
    [Tooltip("열 개수 (1=세로, 2=2열 좌우)")]
    [Range(1, 4)]
    public int columnCount = 2;

    [Tooltip("슬롯 간 가로 간격 (열 사이)")]
    public float horizontalSpacing = 10f;

    [Tooltip("슬롯 간 세로 간격 (행 사이)")]
    public float verticalSpacing = 10f;

    [Tooltip("첫 슬롯의 X 시작 위치 (Container 기준 anchored X)")]
    public float startXOffset = 0f;

    [Tooltip("첫 슬롯의 Y 시작 위치 (Container 기준 anchored Y, 보통 0이나 음수)")]
    public float startYOffset = 0f;

    [Tooltip("슬롯 한 개의 폭 (px). 0이면 prefab의 sizeDelta.x 사용")]
    public float slotWidth = 0f;

    [Tooltip("슬롯 한 개의 높이 (px). 0이면 prefab의 sizeDelta.y 사용")]
    public float slotHeight = 0f;

    [Tooltip("새벽의 잔(고정 슬롯)도 자동 배치에 포함할지 여부")]
    public bool includeFixedSlotInLayout = true;

    private readonly List<BattleItemSlot> spawnedSlots = new List<BattleItemSlot>();

    private void Awake()
    {
        if (hideOnAwake)
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (ConsumableInventory.Instance != null)
            ConsumableInventory.Instance.OnInventoryChanged += RefreshAllSlots;
        RefreshAllSlots();
    }

    private void OnDisable()
    {
        if (ConsumableInventory.Instance != null)
            ConsumableInventory.Instance.OnInventoryChanged -= RefreshAllSlots;
    }

    public void RefreshAllSlots()
    {
        ClearDynamicSlots();
        SpawnDynamicSlots();
    }

    private void ClearDynamicSlots()
    {
        foreach (var slot in spawnedSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        spawnedSlots.Clear();
    }

    private void SpawnDynamicSlots()
    {
        if (ConsumableInventory.Instance == null)
        {
            Debug.LogWarning("[BattleItemPanel] ConsumableInventory.Instance가 null입니다.");
            return;
        }

        if (itemSlotPrefab == null)
        {
            Debug.LogError("[BattleItemPanel] itemSlotPrefab이 할당되지 않았습니다!");
            return;
        }

        if (dynamicSlotsContainer == null)
        {
            Debug.LogError("[BattleItemPanel] dynamicSlotsContainer가 할당되지 않았습니다!");
            return;
        }

        var sortedSlots = GetSortedSlots();
        int createdCount = 0;

        foreach (var slot in sortedSlots)
        {
            // GetSortedSlots에서 이미 null/isDawnChalice/quantity<=0 필터링됨
            GameObject slotObj = Instantiate(itemSlotPrefab, dynamicSlotsContainer);
            var slotComponent = slotObj.GetComponent<BattleItemSlot>();

            if (slotComponent != null)
            {
                slotComponent.Bind(slot.item);
                spawnedSlots.Add(slotComponent);
                createdCount++;
            }
            else
            {
                Debug.LogError("[BattleItemPanel] Prefab에 BattleItemSlot 컴포넌트가 없습니다!");
                Destroy(slotObj);
            }
        }

        ArrangeSlots();
        Debug.Log($"[BattleItemPanel] 동적 슬롯 {createdCount}개 생성 완료");
    }

    /// <summary>
    /// ConsumableInventory.slots에서 표시 대상만 필터링한 뒤 정렬해 반환합니다.
    /// 정렬 순서:
    ///   1차: HP Potion / Mana Potion 강제 우선 (GetItemPriority)
    ///   2차: itemCategory enum 정수 오름차순
    ///   3차: itemName 사전순 (대소문자 무시)
    /// 새벽의 잔과 quantity ≤ 0 슬롯은 제외.
    /// </summary>
    private List<ConsumableInventory.ConsumableSlot> GetSortedSlots()
    {
        var sorted = new List<ConsumableInventory.ConsumableSlot>();

        if (ConsumableInventory.Instance == null) return sorted;

        foreach (var s in ConsumableInventory.Instance.slots)
        {
            if (s == null || s.item == null) continue;
            if (s.item.isDawnChalice) continue;
            if (s.quantity <= 0) continue;
            sorted.Add(s);
        }

        sorted.Sort((a, b) =>
        {
            // 1차: HP Potion / Mana Potion 강제 우선순위
            int aPriority = GetItemPriority(a.item);
            int bPriority = GetItemPriority(b.item);
            if (aPriority != bPriority) return aPriority.CompareTo(bPriority);

            // 2차: 카테고리 순
            int catCompare = ((int)a.item.itemCategory).CompareTo((int)b.item.itemCategory);
            if (catCompare != 0) return catCompare;

            // 3차: 이름순
            return string.Compare(a.item.itemName, b.item.itemName, System.StringComparison.OrdinalIgnoreCase);
        });

        return sorted;
    }

    /// <summary>
    /// 아이템명 기반 강제 우선순위.
    /// 0=HP Potion, 1=Mana Potion, 999=그 외(카테고리/이름순으로 정렬).
    /// 비교는 공백/언더스코어 제거 후 소문자 매칭.
    /// </summary>
    private int GetItemPriority(ConsumableItemSO item)
    {
        if (item == null) return 999;

        // itemName으로 강제 우선순위 (대소문자 무시, 공백/언더스코어 무시)
        string normalized = item.itemName.Replace(" ", "").Replace("_", "").ToLower();

        if (normalized == "hppotion")   return 0;
        if (normalized == "manapotion") return 1;

        return 999; // 그 외는 카테고리/이름순으로
    }

    /// <summary>
    /// LayoutGroup 없이 슬롯 RectTransform.anchoredPosition을 코드로 직접 배치합니다.
    /// columnCount 기반의 그리드 배치 — 1=세로 한 줄, 2=2열, 3=3열...
    /// Prefab의 sizeDelta는 건드리지 않고 위치만 잡으므로 디자인 그대로 보존됩니다.
    /// Container의 Pivot이 (x, 1) — 좌상단 — 인 일반적인 Scroll View Content 기준으로 설계됨.
    /// </summary>
    private void ArrangeSlots()
    {
        if (dynamicSlotsContainer == null) return;

        // 정렬 대상 슬롯 리스트 만들기
        List<RectTransform> targets = new List<RectTransform>();

        // 새벽의 잔 (고정 슬롯) 먼저 추가
        if (includeFixedSlotInLayout && fixedDawnChaliceSlot != null
            && fixedDawnChaliceSlot.transform.parent == dynamicSlotsContainer)
        {
            var rt = fixedDawnChaliceSlot.GetComponent<RectTransform>();
            if (rt != null) targets.Add(rt);
        }

        // 동적 슬롯 추가
        foreach (var slot in spawnedSlots)
        {
            if (slot == null) continue;
            var rt = slot.GetComponent<RectTransform>();
            if (rt != null) targets.Add(rt);
        }

        // 슬롯 크기 결정
        float useWidth  = slotWidth  > 0 ? slotWidth  : 0f;
        float useHeight = slotHeight > 0 ? slotHeight : 0f;

        // 첫 번째 슬롯에서 자동으로 크기 가져오기 (slotWidth/Height가 0인 경우)
        if (targets.Count > 0)
        {
            if (useWidth  <= 0) useWidth  = targets[0].sizeDelta.x;
            if (useHeight <= 0) useHeight = targets[0].sizeDelta.y;
        }

        // 그리드 배치
        int cols = Mathf.Max(1, columnCount);
        int rows = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;

            float x = startXOffset + col * (useWidth  + horizontalSpacing);
            float y = -(startYOffset + row * (useHeight + verticalSpacing));

            targets[i].anchoredPosition = new Vector2(x, y);

            rows = row + 1;
        }

        // Container 높이 갱신 (스크롤용)
        var containerRT = dynamicSlotsContainer.GetComponent<RectTransform>();
        if (containerRT != null)
        {
            float totalHeight = startYOffset + rows * useHeight + (rows - 1) * verticalSpacing;
            containerRT.sizeDelta = new Vector2(containerRT.sizeDelta.x, totalHeight);
        }
    }

    /// <summary>
    /// Transform.Find는 비활성 트리에서 실패할 수 있고, ItemPanel에 이 컴포넌트가 없으면 FindFirstObjectByType도 실패합니다.
    /// SubPanels가 꺼져 있어도 <see cref="GameObject.Find"/> + 자식 전체 검색으로 ItemPanel 오브젝트를 찾습니다.
    /// </summary>
    public static GameObject FindItemPanelGameObjectInScene()
    {
        BattleItemPanel bip = Object.FindFirstObjectByType<BattleItemPanel>(FindObjectsInactive.Include);
        if (bip != null)
            return bip.gameObject;

        GameObject sub = GameObject.Find("SubPanels");
        if (sub == null)
            return null;

        foreach (Transform t in sub.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "ItemPanel")
                return t.gameObject;
        }

        return null;
    }

    /// <summary><see cref="FindItemPanelGameObjectInScene"/>와 동일 이유로 이름 기준 검색.</summary>
    public static GameObject FindSkillPanelGameObjectInScene()
    {
        GameObject sub = GameObject.Find("SubPanels");
        if (sub == null)
            return null;

        foreach (Transform t in sub.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "SkillPanel")
                return t.gameObject;
        }

        return null;
    }

    /// <summary>패널만 켜면 부모(SubPanels 등)가 꺼져 있으면 화면에 안 나옵니다. 스킬 패널과 동일하게 부모 체인을 먼저 켭니다.</summary>
    public static void ActivateWithParents(GameObject panel)
    {
        if (panel == null) return;
        Transform p = panel.transform.parent;
        while (p != null)
        {
            if (!p.gameObject.activeSelf)
                p.gameObject.SetActive(true);
            p = p.parent;
        }
        panel.SetActive(true);
    }

    public void Open()
    {
        ActivateWithParents(gameObject);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}
