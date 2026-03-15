using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

public class InventoryUIManager : MonoBehaviour
{
    public enum InventoryTab { All, Equipment, Consumable }

    // ─── 패널 루트 ───────────────────────────────────────────
    [Header("루트 패널")]
    public GameObject inventoryRoot;

    // ─── 탭 ──────────────────────────────────────────────────
    [Header("탭 버튼")]
    public Button tabAll;
    public Button tabEquipment;
    public Button tabConsumable;

    [Header("탭 텍스트 (색상 피드백용)")]
    public TextMeshProUGUI tabAllText;
    public TextMeshProUGUI tabEquipmentText;
    public TextMeshProUGUI tabConsumableText;

    // ─── 그리드 ──────────────────────────────────────────────
    [Header("아이템 그리드")]
    public ScrollRect gridScrollRect;
    public Transform  gridContent;
    public GameObject itemSlotTemplate;

    // ─── 하단 상세 패널 ───────────────────────────────────────
    [Header("상세 패널")]
    public GameObject detailPanel;
    public float      detailPanelHeight = 400f;
    public float      animDuration      = 0.22f;

    [Header("상세 패널 — 아이템 정보")]
    public Image            detailIcon;
    public TextMeshProUGUI  detailNameText;
    public TextMeshProUGUI  detailTypeText;
    public TextMeshProUGUI  detailDescText;

    [Header("상세 패널 — 스탯 목록")]
    public Transform statListContainer;

    [Header("상세 패널 — 액션 버튼")]
    public Button          primaryButton;
    public TextMeshProUGUI primaryButtonText;
    public Button          discardButton;

    // ─── 닫기 버튼 ────────────────────────────────────────────
    [Header("닫기 버튼")]
    public Button closeButton;

    // ─── 인벤토리 데이터 ──────────────────────────────────────
    [Header("인벤토리 아이템 목록")]
    public List<EquipmentData> equipmentItems = new List<EquipmentData>();

    [Header("아이템 데이터베이스 (Inspector에서 SO 드래그 등록)")]
    public List<EquipmentData>    allEquipmentDatabase  = new List<EquipmentData>();
    public List<ConsumableItemSO> allConsumableDatabase = new List<ConsumableItemSO>();

    // ─── 외부 참조 ───────────────────────────────────────────
    [Header("시스템 참조")]
    public EquipmentManager    equipmentManager;
    public PlayerStats         playerStats;
    public ConsumableInventory consumableInventory;

    // ─── 내부 상태 ───────────────────────────────────────────
    private InventoryTab        currentTab         = InventoryTab.All;
    private EquipmentData       selectedItem       = null;
    private ConsumableItemSO    selectedConsumable = null;
    private bool                detailPanelVisible = false;
    private RectTransform       detailPanelRT;
    private Coroutine           animCoroutine;

    private static readonly Color TabActive   = new Color(0.56f, 0.42f, 0.12f, 1f);
    private static readonly Color TabInactive = new Color(0.18f, 0.18f, 0.25f, 1f);
    private static readonly Color TextActive  = Color.white;
    private static readonly Color TextDim     = new Color(0.65f, 0.65f, 0.70f, 1f);
    private static readonly Color StatPos     = new Color(0.22f, 0.82f, 0.22f, 1f);
    private static readonly Color StatNeg     = new Color(0.90f, 0.22f, 0.22f, 1f);
    private static readonly Color StatZero    = new Color(0.60f, 0.60f, 0.65f, 1f);

    // ═════════════════════════════════════════════════════════
    //  초기화
    // ═════════════════════════════════════════════════════════

    private void Awake()
    {
        detailPanelRT = detailPanel.GetComponent<RectTransform>();
        ForceHideDetail();

        if (itemSlotTemplate != null)
            itemSlotTemplate.SetActive(false);
    }

    private void Start()
    {
        tabAll.onClick       .AddListener(() => SwitchTab(InventoryTab.All));
        tabEquipment.onClick .AddListener(() => SwitchTab(InventoryTab.Equipment));
        tabConsumable.onClick.AddListener(() => SwitchTab(InventoryTab.Consumable));

        if (closeButton   != null) closeButton  .onClick.AddListener(OnCloseButtonClicked);
        if (discardButton != null) discardButton.onClick.AddListener(OnDiscardClicked);

        if (consumableInventory != null)
            consumableInventory.OnInventoryChanged += RefreshGrid;

        RefreshGrid();
        UpdateTabVisuals();
    }

    // ═════════════════════════════════════════════════════════
    //  외부 공개 API
    // ═════════════════════════════════════════════════════════

    public void OpenInventory()
    {
        inventoryRoot.SetActive(true);
        ForceHideDetail();
        RefreshGrid();
        UpdateTabVisuals();
    }

    public void CloseInventory()
    {
        ForceHideDetail();
        inventoryRoot.SetActive(false);
    }

    public void AddItem(EquipmentData item)
    {
        if (item != null && !equipmentItems.Contains(item))
            equipmentItems.Add(item);
    }

    public void RemoveItem(EquipmentData item)
    {
        equipmentItems.Remove(item);
    }

    // ═════════════════════════════════════════════════════════
    //  탭 전환
    // ═════════════════════════════════════════════════════════

    private void SwitchTab(InventoryTab tab)
    {
        if (currentTab == tab) return;
        currentTab = tab;
        ForceHideDetail();
        RefreshGrid();
        UpdateTabVisuals();
    }

    private void UpdateTabVisuals()
    {
        SetTabColor(tabAll,        tabAllText,        currentTab == InventoryTab.All);
        SetTabColor(tabEquipment,  tabEquipmentText,  currentTab == InventoryTab.Equipment);
        SetTabColor(tabConsumable, tabConsumableText, currentTab == InventoryTab.Consumable);
    }

    private void SetTabColor(Button btn, TextMeshProUGUI label, bool active)
    {
        var img = btn.GetComponent<Image>();
        if (img   != null) img.color   = active ? TabActive   : TabInactive;
        if (label != null) label.color = active ? TextActive  : TextDim;
    }

    // ═════════════════════════════════════════════════════════
    //  그리드 갱신
    // ═════════════════════════════════════════════════════════

    private void RefreshGrid()
    {
        foreach (Transform child in gridContent)
        {
            if (child.gameObject == itemSlotTemplate) continue;
            Destroy(child.gameObject);
        }

        if (currentTab == InventoryTab.Consumable)
        {
            PopulateConsumableGrid();
            return;
        }

        // 전체 탭: 장비 + 소비 모두 표시
        if (currentTab == InventoryTab.All)
        {
            PopulateEquipmentGrid();
            PopulateConsumableGrid();
            return;
        }

        PopulateEquipmentGrid();
    }

    private void PopulateEquipmentGrid()
    {
        foreach (var item in GetFilteredEquipment())
        {
            var slotObj = CreateSlot();
            var slot    = slotObj.GetComponent<InventorySlot>();
            if (slot == null) continue;

            EquipmentData captured = item;
            slot.Setup(item, () => OnItemClicked(captured));

            // 장착중이면 골드 테두리
            if (IsEquipped(item))
                slot.SetRareBorder(true);
        }
    }

    private void PopulateConsumableGrid()
    {
        // allConsumableDatabase 우선 사용, 없으면 ConsumableInventory fallback
        var source = (allConsumableDatabase != null && allConsumableDatabase.Count > 0)
            ? allConsumableDatabase
            : null;

        // 새벽의 잔 — 항상 첫 번째 (isDawnChalice 기준)
        if (source != null)
        {
            foreach (var item in source)
            {
                if (item == null || !item.isDawnChalice) continue;
                int qty = consumableInventory != null
                    ? consumableInventory.GetQuantity(item)
                    : 0;
                var slotObj = CreateSlot();
                var slot    = slotObj.GetComponent<InventorySlot>();
                if (slot != null)
                {
                    var cap = item;
                    slot.SetupConsumable(cap, qty, () => OnConsumableClicked(cap));
                }
            }

            // 일반 소비 아이템
            foreach (var item in source)
            {
                if (item == null || item.isDawnChalice) continue;
                int qty = consumableInventory != null
                    ? consumableInventory.GetQuantity(item)
                    : 0;
                // 보유 수량 없고 영구 아이템도 아니면 숨김
                if (qty <= 0 && !item.isPermanent) continue;
                var slotObj = CreateSlot();
                var slot    = slotObj.GetComponent<InventorySlot>();
                if (slot != null)
                {
                    var cap = item;
                    slot.SetupConsumable(cap, qty, () => OnConsumableClicked(cap));
                }
            }
        }
        else if (consumableInventory != null)
        {
            // fallback: ConsumableInventory.slots 직접 사용
            if (consumableInventory.dawnChaliceItem != null)
            {
                var chalice = consumableInventory.dawnChaliceItem;
                var slotObj = CreateSlot();
                var slot    = slotObj.GetComponent<InventorySlot>();
                if (slot != null)
                    slot.SetupConsumable(chalice, consumableInventory.dawnChaliceCharges,
                        () => OnConsumableClicked(chalice));
            }
            foreach (var s in consumableInventory.slots)
            {
                if (s.item == null || s.quantity <= 0) continue;
                var slotObj = CreateSlot();
                var slot    = slotObj.GetComponent<InventorySlot>();
                if (slot != null)
                {
                    var captured = s;
                    slot.SetupConsumable(captured.item, captured.quantity,
                        () => OnConsumableClicked(captured.item));
                }
            }
        }
    }

    private List<EquipmentData> GetFilteredEquipment()
    {
        var db = (allEquipmentDatabase != null && allEquipmentDatabase.Count > 0)
            ? allEquipmentDatabase
            : equipmentItems;

        return db.FindAll(e => e != null);
    }

    private GameObject CreateSlot()
    {
        if (itemSlotTemplate != null)
        {
            var clone = Instantiate(itemSlotTemplate, gridContent);
            clone.SetActive(true);
            return clone;
        }

        var obj = new GameObject("ItemSlot", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(gridContent, false);
        obj.AddComponent<InventorySlot>();
        return obj;
    }

    // ═════════════════════════════════════════════════════════
    //  아이템 클릭 → 상세 패널
    // ═════════════════════════════════════════════════════════

    private void OnItemClicked(EquipmentData item)
    {
        selectedItem       = item;
        selectedConsumable = null;
        PopulateDetailPanel(item);

        if (!detailPanelVisible) AnimateDetail(true);
        else detailPanel.SetActive(true);
    }

    private void OnConsumableClicked(ConsumableItemSO item)
    {
        selectedConsumable = item;
        selectedItem       = null;
        PopulateDetailPanelConsumable(item);

        if (!detailPanelVisible) AnimateDetail(true);
        else detailPanel.SetActive(true);
    }

    private void PopulateDetailPanelConsumable(ConsumableItemSO item)
    {
        if (detailIcon     != null) detailIcon.sprite    = item.itemIcon != null ? item.itemIcon : item.flatIcon;
        if (detailNameText != null) detailNameText.text  = item.itemName;
        if (detailTypeText != null) detailTypeText.text  = item.isDawnChalice ? "특수 소비" : "소비 아이템";
        if (detailDescText != null) detailDescText.text  = item.description;

        BuildConsumableStatRows(item);
        RefreshConsumablePrimaryButton(item);
    }

    private void BuildConsumableStatRows(ConsumableItemSO item)
    {
        if (statListContainer == null) return;
        foreach (Transform child in statListContainer) Destroy(child.gameObject);

        if (item.hpRecoveryPercent > 0f)
            AddStatRowFloat("HP 회복", item.hpRecoveryPercent * 100f, "%");
        if (item.mpRecoveryPercent > 0f)
            AddStatRowFloat("MP 회복", item.mpRecoveryPercent * 100f, "%");
        if (item.attackBuffPercent > 0f)
            AddStatRowFloat("공격력 버프", item.attackBuffPercent * 100f, "%");
        if (item.agilityBuff != 0)
            AddStatRowFloat("민첩 버프", item.agilityBuff);
        if (item.evasionBuff > 0f)
            AddStatRowFloat("회피율 버프", item.evasionBuff * 100f, "%");
        if (item.escapeChanceBuff > 0f)
            AddStatRowFloat("도주 확률", item.escapeChanceBuff * 100f, "%");
        if (item.mpPenaltyPercent > 0f)
            AddStatRowFloat("MP 소모 패널티", -item.mpPenaltyPercent * 100f, "%");
        if (item.buffDuration > 0)
            AddStatRowFloat("지속 턴수", item.buffDuration, "턴");
        if (item.cureTypes != null && item.cureTypes.Count > 0)
        {
            string cureList = string.Join(", ", item.cureTypes);
            AddStatRowText("해제 가능 상태이상", cureList);
        }

        int qty = consumableInventory != null ? consumableInventory.GetQuantity(item) : 0;
        AddStatRowFloat("보유 수량", qty, "개");
    }

    private void RefreshConsumablePrimaryButton(ConsumableItemSO item)
    {
        if (primaryButton == null) return;

        if (primaryButtonText != null)
            primaryButtonText.text = "사용";

        bool hasItem  = consumableInventory != null && consumableInventory.HasItem(item);
        // usableInBattle = false이면 비활성 (맵 전용)
        primaryButton.interactable = hasItem && item.usableInBattle;
        primaryButton.onClick.RemoveAllListeners();
        primaryButton.onClick.AddListener(() => OnUseConsumableClicked(item));

        // 버리기 버튼: isPermanent면 비활성
        if (discardButton != null)
            discardButton.interactable = !item.isPermanent;
    }

    private void PopulateDetailPanel(EquipmentData item)
    {
        if (detailIcon     != null) detailIcon.sprite = item.equipmentIcon;
        if (detailNameText != null) detailNameText.text = item.equipmentName;
        if (detailTypeText != null) detailTypeText.text = LocalizeType(item.equipmentType);
        if (detailDescText != null) detailDescText.text = item.description;

        BuildStatRows(item);
        RefreshPrimaryButton(item);
    }

    private void BuildStatRows(EquipmentData item)
    {
        if (statListContainer == null) return;
        foreach (Transform child in statListContainer) Destroy(child.gameObject);

        // 장착중이면 비교 없이 단독 표시, 미장착이면 현재 슬롯 장비와 비교
        EquipmentData current = IsEquipped(item) ? null : GetEquippedInSameSlot(item);

        AddCompareRow("공격력", current?.attackBonus   ?? 0, item.attackBonus);
        AddCompareRow("방어력", current?.defenseBonus  ?? 0, item.defenseBonus);
        AddCompareRow("마법력", current?.magicBonus    ?? 0, item.magicBonus);
        AddCompareRow("최대 HP", current?.hpBonus      ?? 0, item.hpBonus);
        AddCompareRow("최대 MP", current?.mpBonus      ?? 0, item.mpBonus);
        AddCompareRow("민첩",   current?.agiBonus      ?? 0, item.agiBonus);
        AddCompareRow("행운",   current?.luckBonus     ?? 0, item.luckBonus);

        float curAcc = current?.accuracyBonus    ?? 0f;
        float curMp  = current?.mpBonusPercent   ?? 0f;
        if (curAcc != 0f || item.accuracyBonus != 0f)
            AddCompareRowF("명중률", curAcc * 100f, item.accuracyBonus * 100f, "%");
        if (curMp != 0f || item.mpBonusPercent != 0f)
            AddCompareRowF("MP%", curMp * 100f, item.mpBonusPercent * 100f, "%");
    }

    // 현재 선택된 장비가 들어갈 슬롯의 기존 장비 반환
    private EquipmentData GetEquippedInSameSlot(EquipmentData item)
    {
        if (equipmentManager == null || item == null) return null;
        switch (item.equipmentType)
        {
            case EquipmentType.Hand:
                return equipmentManager.rightHand ?? equipmentManager.leftHand;
            case EquipmentType.TwoHanded:
                return equipmentManager.rightHand;
            case EquipmentType.Armour:
                return equipmentManager.body;
            case EquipmentType.Accessory:
                return equipmentManager.accessory1 ?? equipmentManager.accessory2;
            default:
                return null;
        }
    }

    // 정수 비교 행 (currentVal == 0 && newVal == 0이면 숨김)
    private void AddCompareRow(string label, int currentVal, int newVal)
    {
        if (currentVal == 0 && newVal == 0) return;
        AddCompareRowF(label, currentVal, newVal);
    }

    // float 비교 행
    private void AddCompareRowF(string label, float curVal, float newVal, string suffix = "")
    {
        if (curVal == 0f && newVal == 0f) return;

        float diff      = newVal - curVal;
        Color diffColor = diff > 0f ? StatPos : (diff < 0f ? StatNeg : StatZero);

        string curStr  = FormatStat(curVal, suffix);
        string newStr  = FormatStat(newVal, suffix);
        string diffStr = diff == 0f ? ""
            : diff > 0f ? $" (+{FormatStat(diff, suffix)})"
                        : $" ({FormatStat(diff, suffix)})";

        var row = new GameObject($"StatRow_{label}", typeof(RectTransform),
                                                     typeof(HorizontalLayoutGroup));
        row.transform.SetParent(statListContainer, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f; hlg.childForceExpandWidth = false;
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);

        CreateTMPChild(row.transform, "Label",   label,                17,
                       TextAlignmentOptions.Left,  new Color(0.75f, 0.75f, 0.80f, 1f), true);
        CreateTMPChild(row.transform, "Current", curStr,               15,
                       TextAlignmentOptions.Right, StatZero, false);
        CreateTMPChild(row.transform, "Arrow",   "→",                  13,
                       TextAlignmentOptions.Center, new Color(0.5f, 0.5f, 0.5f, 1f), false);
        CreateTMPChild(row.transform, "New",     newStr + diffStr,     15,
                       TextAlignmentOptions.Left,  diffColor, false);
    }

    private string FormatStat(float val, string suffix = "")
        => val % 1 == 0 ? $"{(int)val}{suffix}" : $"{val:F1}{suffix}";

    private void AddStatRowFloat(string label, float value, string suffix = "")
    {
        string sign  = value >= 0f ? "+" : "";
        string val   = (value % 1 == 0)
            ? $"{sign}{(int)value}{suffix}"
            : $"{sign}{value:F1}{suffix}";
        Color  color = value > 0f ? StatPos : (value < 0f ? StatNeg : StatZero);

        var row = new GameObject($"StatRow_{label}", typeof(RectTransform),
                                                     typeof(HorizontalLayoutGroup));
        row.transform.SetParent(statListContainer, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f; hlg.childForceExpandWidth = false;
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);

        CreateTMPChild(row.transform, "Label", label, 18, TextAlignmentOptions.Left,
                       new Color(0.75f, 0.75f, 0.80f, 1f), true);
        CreateTMPChild(row.transform, "Value", val, 18, TextAlignmentOptions.Right, color, false);
    }

    private void AddStatRowText(string label, string value)
    {
        var row = new GameObject($"StatRow_{label}", typeof(RectTransform),
                                                     typeof(HorizontalLayoutGroup));
        row.transform.SetParent(statListContainer, false);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f; hlg.childForceExpandWidth = false;
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);

        CreateTMPChild(row.transform, "Label", label, 18, TextAlignmentOptions.Left,
                       new Color(0.75f, 0.75f, 0.80f, 1f), true);
        CreateTMPChild(row.transform, "Value", value, 15, TextAlignmentOptions.Right,
                       new Color(0.85f, 0.75f, 0.40f, 1f), false);
    }

    private void AddStatRowFloat(string label, float value)
    {
        var row = new GameObject($"StatRow_{label}", typeof(RectTransform),
                                                     typeof(HorizontalLayoutGroup));
        row.transform.SetParent(statListContainer, false);

        var hlg              = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing          = 8f;
        hlg.childForceExpandWidth = false;

        var rt               = row.GetComponent<RectTransform>();
        rt.sizeDelta         = new Vector2(0f, 28f);

        CreateTMPChild(row.transform, "Label", label, 18, TextAlignmentOptions.Left,
                       new Color(0.75f, 0.75f, 0.80f, 1f), true);

        string sign  = value >= 0f ? "+" : "";
        string val   = (value % 1 == 0) ? $"{sign}{(int)value}" : $"{sign}{value:F1}";
        Color  color = value > 0f ? StatPos : (value < 0f ? StatNeg : StatZero);
        CreateTMPChild(row.transform, "Value", val, 18, TextAlignmentOptions.Right, color, false);
    }

    private void CreateTMPChild(Transform parent, string name, string text,
                                int fontSize, TextAlignmentOptions align, Color color, bool expand)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        var tmp         = obj.GetComponent<TextMeshProUGUI>();
        tmp.text        = text;
        tmp.fontSize    = fontSize;
        tmp.alignment   = align;
        tmp.color       = color;

        var le          = obj.AddComponent<LayoutElement>();
        le.flexibleWidth = expand ? 1f : 0f;
        le.minWidth      = expand ? 0f : 60f;
    }

    // ═════════════════════════════════════════════════════════
    //  장착 / 해제 / 버리기
    // ═════════════════════════════════════════════════════════

    private void RefreshPrimaryButton(EquipmentData item)
    {
        if (primaryButton == null) return;
        bool equipped = IsEquipped(item);
        if (primaryButtonText != null)
            primaryButtonText.text = equipped ? "해제" : "장착";

        primaryButton.interactable = true;
        primaryButton.onClick.RemoveAllListeners();
        if (equipped)
            primaryButton.onClick.AddListener(() => OnUnequipClicked(item));
        else
            primaryButton.onClick.AddListener(() => OnEquipClicked(item));

        // 장비는 항상 버리기 가능
        if (discardButton != null)
            discardButton.interactable = true;
    }

    private bool IsEquipped(EquipmentData item)
    {
        if (equipmentManager == null || item == null) return false;
        return equipmentManager.rightHand  == item ||
               equipmentManager.leftHand   == item ||
               equipmentManager.body       == item ||
               equipmentManager.accessory1 == item ||
               equipmentManager.accessory2 == item;
    }

    private void OnEquipClicked(EquipmentData item)
    {
        if (equipmentManager != null) equipmentManager.EquipItem(item);
        PopulateDetailPanel(item);
    }

    private void OnUnequipClicked(EquipmentData item)
    {
        if (equipmentManager != null)
        {
            if      (equipmentManager.rightHand  == item) equipmentManager.UnequipItem("RightHand");
            else if (equipmentManager.leftHand   == item) equipmentManager.UnequipItem("LeftHand");
            else if (equipmentManager.body       == item) equipmentManager.UnequipItem("Body");
            else if (equipmentManager.accessory1 == item) equipmentManager.UnequipItem("Accessory1");
            else if (equipmentManager.accessory2 == item) equipmentManager.UnequipItem("Accessory2");
        }
        PopulateDetailPanel(item);
    }

    private void OnUseConsumableClicked(ConsumableItemSO item)
    {
        if (consumableInventory == null || !consumableInventory.HasItem(item)) return;
        consumableInventory.RemoveItem(item);
        // 실제 효과 적용은 BattleManager 또는 PlayerStats에서 처리
        Debug.Log($"[Inventory] 사용: {item.itemName}");
        PopulateDetailPanelConsumable(item);
    }

    private void OnDiscardClicked()
    {
        if (selectedConsumable != null)
        {
            consumableInventory?.RemoveItem(selectedConsumable);
            selectedConsumable = null;
            AnimateDetail(false);
            RefreshGrid();
            return;
        }

        if (selectedItem == null) return;
        equipmentItems.Remove(selectedItem);
        selectedItem = null;
        AnimateDetail(false);
        RefreshGrid();
    }

    // ═════════════════════════════════════════════════════════
    //  닫기 버튼 — 패널 먼저 닫고, 패널이 닫혀있으면 인벤토리 닫기
    // ═════════════════════════════════════════════════════════

    private void OnCloseButtonClicked()
    {
        if (detailPanelVisible)
            AnimateDetail(false);
        else
            CloseInventory();
    }

    // ═════════════════════════════════════════════════════════
    //  패널 애니메이션
    // ═════════════════════════════════════════════════════════

    private void AnimateDetail(bool show)
    {
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(DetailPanelAnim(show));
    }

    private void ForceHideDetail()
    {
        if (animCoroutine != null) { StopCoroutine(animCoroutine); animCoroutine = null; }
        detailPanelVisible = false;
        detailPanel.SetActive(false);
        if (detailPanelRT != null)
            detailPanelRT.anchoredPosition = new Vector2(0f, -detailPanelHeight);
    }

    private IEnumerator DetailPanelAnim(bool show)
    {
        detailPanel.SetActive(true);
        detailPanelVisible = show;

        float fromY = show ? -detailPanelHeight : 0f;
        float toY   = show ? 0f : -detailPanelHeight;

        detailPanelRT.anchoredPosition = new Vector2(0f, fromY);

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animDuration));
            detailPanelRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(fromY, toY, t));
            yield return null;
        }

        detailPanelRT.anchoredPosition = new Vector2(0f, toY);
        if (!show) detailPanel.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════
    //  에디터 전용 — DB 자동 등록 (컴포넌트 우클릭 메뉴)
    // ═════════════════════════════════════════════════════════

#if UNITY_EDITOR
    [ContextMenu("자동 등록 ▶ 전체 DB 채우기")]
    private void AutoFillAll()
    {
        AutoFillEquipmentDatabase();
        AutoFillConsumableDatabase();
    }

    [ContextMenu("자동 등록 ▶ 장비 DB 채우기")]
    private void AutoFillEquipmentDatabase()
    {
        allEquipmentDatabase.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EquipmentData");
        foreach (var guid in guids)
        {
            string path  = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
            if (asset != null) allEquipmentDatabase.Add(asset);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[InventoryUIManager] 장비 {allEquipmentDatabase.Count}개 자동 등록 완료");
    }

    [ContextMenu("자동 등록 ▶ 소비 아이템 DB 채우기")]
    private void AutoFillConsumableDatabase()
    {
        allConsumableDatabase.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ConsumableItemSO");
        foreach (var guid in guids)
        {
            string path  = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableItemSO>(path);
            if (asset != null) allConsumableDatabase.Add(asset);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[InventoryUIManager] 소비 아이템 {allConsumableDatabase.Count}개 자동 등록 완료");
    }
#endif

    // ═════════════════════════════════════════════════════════
    //  유틸
    // ═════════════════════════════════════════════════════════

    private string LocalizeType(EquipmentType type)
    {
        switch (type)
        {
            case EquipmentType.Hand:       return "한손 무기";
            case EquipmentType.TwoHanded:  return "양손 무기";
            case EquipmentType.Armour:     return "방어구";
            case EquipmentType.Accessory:  return "장신구";
            default:                       return type.ToString();
        }
    }
}
