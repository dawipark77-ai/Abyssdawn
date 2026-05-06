using System.Collections.Generic;
using UnityEngine;

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

        var inventory = ConsumableInventory.Instance;
        int createdCount = 0;

        foreach (var slot in inventory.slots)
        {
            if (slot == null || slot.item == null) continue;
            if (slot.item.isDawnChalice) continue;          // 고정 슬롯에서 처리
            if (slot.quantity <= 0) continue;

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

        Debug.Log($"[BattleItemPanel] 동적 슬롯 {createdCount}개 생성 완료");
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
