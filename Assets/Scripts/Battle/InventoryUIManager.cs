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
    [SerializeField] TextMeshProUGUI qtyText;

    [Header("Detail Panel — Stat List")]
    public Transform statListContainer;

    [Header("상세 패널 — 메인 스탯 / 저주 / 가격")]
    [SerializeField] TextMeshProUGUI mainStatText;
    [SerializeField] Transform       statusEffectRow;
    [SerializeField] TextMeshProUGUI priceText;
    [SerializeField] GameObject      statusEffectPrefab;

    [Header("StatRow — Prefab")]
    [SerializeField] GameObject statRowPrefab;

    [Header("StatRow — Layout")]
    [SerializeField] float statRowHeight         = 28f;
    [SerializeField] float statRowSpacing        = 6f;
    [SerializeField] int   statListPaddingTop    = 4;
    [SerializeField] int   statListPaddingBottom = 4;
    [SerializeField] int   statListSideMargin    = 24;

    [Header("StatRow — Font Size")]
    [SerializeField] int labelFontSize = 17;
    [SerializeField] int valueFontSize = 15;
    [SerializeField] int arrowFontSize = 13;

    [Header("StatRow — Colors")]
    [SerializeField] Color labelColor    = new Color(0.75f, 0.75f, 0.80f, 1f);
    [SerializeField] Color valueNeutral  = new Color(0.60f, 0.60f, 0.65f, 1f);
    [SerializeField] Color valuePositive = new Color(0.22f, 0.82f, 0.22f, 1f);
    [SerializeField] Color valueNegative = new Color(0.90f, 0.22f, 0.22f, 1f);
    [SerializeField] Color valueText     = new Color(0.85f, 0.75f, 0.40f, 1f);
    [SerializeField] Color arrowColor    = new Color(0.50f, 0.50f, 0.50f, 1f);

    [Header("Detail Panel — Action Buttons")]
    public Button          primaryButton;
    public TextMeshProUGUI primaryButtonText;
    public Button          discardButton;

    // ─── 닫기 버튼 ────────────────────────────────────────────
    [Header("닫기 버튼")]
    public Button closeButton;

    // ─── 인벤토리 데이터 ──────────────────────────────────────
    [Header("인벤토리 아이템 목록")]
    public List<EquipmentData> equipmentItems = new List<EquipmentData>();

    // Resources.LoadAll 로 자동 로드 — Inspector 등록 불필요
    private List<EquipmentData>    allEquipmentDatabase  = new List<EquipmentData>();
    private List<ConsumableItemSO> allConsumableDatabase = new List<ConsumableItemSO>();

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

    // StatPos/StatNeg/StatZero는 SerializeField(valuePositive/valueNegative/valueNeutral)로 이동
    private Color StatPos  => valuePositive;
    private Color StatNeg  => valueNegative;
    private Color StatZero => valueNeutral;

    // ═════════════════════════════════════════════════════════
    //  초기화
    // ═════════════════════════════════════════════════════════

    private void Awake()
    {
        detailPanelRT = detailPanel.GetComponent<RectTransform>();
        ForceHideDetail();

        if (itemSlotTemplate != null)
            itemSlotTemplate.SetActive(false);

        LoadItemDatabases();
    }

    private void LoadItemDatabases()
    {
        var eqArr = Resources.LoadAll<EquipmentData>("Item_Equipments/Equipments");
        allEquipmentDatabase = new List<EquipmentData>(eqArr);

        var conArr = Resources.LoadAll<ConsumableItemSO>("Item_Equipments/Items");
        allConsumableDatabase = new List<ConsumableItemSO>(conArr);

        Debug.Log($"[DB] 장비 로드: {allEquipmentDatabase.Count}개  " +
                  $"(경로: Resources/Item_Equipments/Equipments)");
        Debug.Log($"[DB] 소비 아이템 로드: {allConsumableDatabase.Count}개  " +
                  $"(경로: Resources/Item_Equipments/Items)");

        if (allEquipmentDatabase.Count == 0)
            Debug.LogWarning("[DB] 장비 SO 로드 실패 — Resources/Item_Equipments/Equipments 경로 및 .asset 파일 확인 필요");
        if (allConsumableDatabase.Count == 0)
            Debug.LogWarning("[DB] 소비 아이템 SO 로드 실패 — Resources/Item_Equipments/Items 경로 및 .asset 파일 확인 필요");
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
            PopulateConsumableGrid();
            PopulateEquipmentGrid();
            return;
        }

        PopulateEquipmentGrid();
    }

    private void PopulateEquipmentGrid()
    {
        var filtered = GetFilteredEquipment();
        Debug.Log($"[Grid] PopulateEquipmentGrid — {filtered.Count}개  " +
                  $"(allEquipDB: {allEquipmentDatabase.Count}, equipItems: {equipmentItems.Count})");
        foreach (var item in filtered)
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

        Debug.Log($"[Grid] PopulateConsumableGrid — source: {(source != null ? source.Count + "개" : "null")}");

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

            // 일반 소비 아이템 — DB에 있으면 수량 0이어도 표시 (미보유 상태로)
            foreach (var item in source)
            {
                if (item == null || item.isDawnChalice) continue;
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
        if (detailTypeText != null) detailTypeText.text  = item.isDawnChalice ? "Special Consumable" : "Consumable";
        if (detailDescText != null) detailDescText.text  = item.description;

        if (qtyText != null)
        {
            if (item.isChargeable)
                qtyText.text = $"{item.currentCharges}/{item.maxCharges}";
            else
            {
                int qty = consumableInventory != null ? consumableInventory.GetQuantity(item) : 0;
                qtyText.text = $"x{qty}";
            }
        }

        // 소모품: MainStatText / StatusEffectRow 비활성화
        if (mainStatText     != null) mainStatText.gameObject.SetActive(false);
        if (statusEffectRow  != null) statusEffectRow.gameObject.SetActive(false);

        // 판매 가격
        if (priceText != null)
            priceText.text = item.sellPrice > 0 ? $"{item.sellPrice} G" : "—";

        BuildConsumableStatRows(item);
        RefreshConsumablePrimaryButton(item);
    }

    private void BuildConsumableStatRows(ConsumableItemSO item)
    {
        if (statListContainer == null) return;
        foreach (Transform child in statListContainer) Destroy(child.gameObject);

        if (item.hpRecoveryPercent > 0f)
            AddStatRowFloat("HP Recovery", item.hpRecoveryPercent * 100f, "%");
        if (item.mpRecoveryPercent > 0f)
            AddStatRowFloat("MP Recovery", item.mpRecoveryPercent * 100f, "%");
        if (item.attackBuffPercent > 0f)
            AddStatRowFloat("ATK Buff", item.attackBuffPercent * 100f, "%");
        if (item.agilityBuff != 0)
            AddStatRowFloat("AGI Buff", item.agilityBuff);
        if (item.evasionBuff > 0f)
            AddStatRowFloat("Evasion Buff", item.evasionBuff * 100f, "%");
        if (item.escapeChanceBuff > 0f)
            AddStatRowFloat("Escape Chance", item.escapeChanceBuff * 100f, "%");
        if (item.mpPenaltyPercent > 0f)
            AddStatRowFloat("MP Penalty", -item.mpPenaltyPercent * 100f, "%");
        if (item.buffDuration > 0)
            AddStatRowFloat("Duration", item.buffDuration, "turns");
        if (item.cureTypes != null && item.cureTypes.Count > 0)
        {
            string cureList = string.Join(", ", item.cureTypes);
            AddStatRowText("Cures", cureList);
        }
    }

    private void RefreshConsumablePrimaryButton(ConsumableItemSO item)
    {
        if (primaryButton == null) return;

        if (primaryButtonText != null)
            primaryButtonText.text = "Use";

        bool hasItem  = consumableInventory != null && consumableInventory.HasItem(item);
        bool hasCharge = !item.isChargeable || item.currentCharges > 0;
        // usableInBattle = false이면 비활성 (맵 전용) / 충전형은 충전 0이면 비활성
        primaryButton.interactable = hasItem && item.usableInBattle && hasCharge;
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

        // MainStatText — 무기면 ATK, 방어구면 DEF 큰 글씨
        if (mainStatText != null)
        {
            mainStatText.gameObject.SetActive(true);
            bool isWeapon = item.equipmentType == EquipmentType.Hand ||
                            item.equipmentType == EquipmentType.TwoHanded;
            if (isWeapon)
                mainStatText.text = $"ATK  {(item.attackBonus >= 0 ? "+" : "")}{item.attackBonus}";
            else
                mainStatText.text = $"DEF  {(item.defenseBonus >= 0 ? "+" : "")}{item.defenseBonus}";
        }

        // StatusEffectRow — weaponCurses 표시
        BuildWeaponCurseRow(item);

        // 판매 가격
        if (priceText != null)
            priceText.text = item.sellPrice > 0 ? $"{item.sellPrice} G" : "—";

        BuildStatRows(item);
        RefreshPrimaryButton(item);
    }

    // weaponCurses + armorBreakData를 StatusEffectRow에 Instantiate
    private void BuildWeaponCurseRow(EquipmentData item)
    {
        if (statusEffectRow == null) return;

        // 기존 자식 제거
        foreach (Transform child in statusEffectRow)
            Destroy(child.gameObject);

        Debug.Log("armorBreakData: " + item.armorBreakData);
        Debug.Log("weaponCurses count: " + (item.weaponCurses != null ? item.weaponCurses.Count : 0));

        bool hasCurses    = item.weaponCurses != null && item.weaponCurses.Count > 0;
        bool hasArmorBreak = item.armorBreakData != null;
        statusEffectRow.gameObject.SetActive(hasCurses || hasArmorBreak);

        if (statusEffectPrefab == null)
        {
            Debug.LogWarning("[BuildWeaponCurseRow] statusEffectPrefab is null — Inspector 연결 확인 필요");
            return;
        }

        // ── weaponCurses ──────────────────────────────────────
        if (hasCurses)
        {
            foreach (var curse in item.weaponCurses)
            {
                if (curse == null) continue;

                var go = Instantiate(statusEffectPrefab, statusEffectRow, false);

                var itemIconT = go.transform.Find("ItemIcon (Image)");
                if (itemIconT != null)
                {
                    var img = itemIconT.GetComponent<Image>();
                    if (img != null) img.sprite = curse.itemIcon;
                }

                var flatIconT = go.transform.Find("Faltcon (Image)");
                if (flatIconT != null)
                {
                    var img = flatIconT.GetComponent<Image>();
                    if (img != null) img.sprite = curse.flatIcon;
                }

                var chanceT = go.transform.Find("Percent");
                if (chanceT != null)
                {
                    var tmp = chanceT.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                        tmp.text = $"{Mathf.RoundToInt(curse.physicalApplyChance * 100f)}%";
                }
            }
        }

        // ── armorBreakData ────────────────────────────────────
        if (hasArmorBreak)
        {
            var ab = item.armorBreakData;
            var go = Instantiate(statusEffectPrefab, statusEffectRow, false);
            Debug.Log("armorBreak prefab spawned: " + go.name);

            var itemIconT = go.transform.Find("ItemIcon (Image)");
            if (itemIconT != null)
            {
                var img = itemIconT.GetComponent<Image>();
                if (img != null) img.sprite = ab.icon;
            }

            var flatIconT = go.transform.Find("Faltcon (Image)");
            if (flatIconT != null)
            {
                var img = flatIconT.GetComponent<Image>();
                if (img != null) img.sprite = ab.icon;
            }

            var chanceT = go.transform.Find("Percent");
            if (chanceT != null)
            {
                var tmp = chanceT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.text = $"{Mathf.RoundToInt(ab.coefficient * 100f)}%";
            }
        }
    }

    private void BuildStatRows(EquipmentData item)
    {
        if (statListContainer == null) return;
        foreach (Transform child in statListContainer) Destroy(child.gameObject);

        // 장착중이면 비교 없이 단독 표시, 미장착이면 현재 슬롯 장비와 비교
        EquipmentData current = IsEquipped(item) ? null : GetEquippedInSameSlot(item);

        bool isWeapon = item.equipmentType == EquipmentType.Hand ||
                        item.equipmentType == EquipmentType.TwoHanded;

        // MainStatText에 표시된 대표 수치는 StatList에서 제외
        if (!isWeapon)
            AddCompareRow("ATK", current?.attackBonus  ?? 0, item.attackBonus);
        if (isWeapon)
            AddCompareRow("DEF", current?.defenseBonus ?? 0, item.defenseBonus);

        AddCompareRow("MAG",    current?.magicBonus ?? 0, item.magicBonus);
        AddCompareRow("Max HP", current?.hpBonus    ?? 0, item.hpBonus);
        AddCompareRow("Max MP", current?.mpBonus    ?? 0, item.mpBonus);
        AddCompareRow("AGI",    current?.agiBonus   ?? 0, item.agiBonus);
        AddCompareRow("LCK",    current?.luckBonus  ?? 0, item.luckBonus);

        float curAcc = current?.accuracyBonus  ?? 0f;
        float curMp  = current?.mpBonusPercent ?? 0f;
        if (curAcc != 0f || item.accuracyBonus != 0f)
            AddCompareRowF("Accuracy", curAcc * 100f, item.accuracyBonus * 100f, "%");
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

    // float 비교 행 (장비 스탯 비교)
    private void AddCompareRowF(string label, float curVal, float newVal, string suffix = "")
    {
        if (curVal == 0f && newVal == 0f) return;

        float diff      = newVal - curVal;
        Color diffColor = diff > 0f ? StatPos : (diff < 0f ? StatNeg : StatZero);

        string newStr   = FormatStat(newVal, suffix);
        string valueStr = diff == 0f
            ? newStr
            : diff > 0f ? $"{newStr} (+{FormatStat(diff, suffix)})"
                        : $"{newStr} ({FormatStat(diff, suffix)})";

        var row = SpawnRow(label);
        HideRowChildren(row.transform, "Current", "Arrow", "New"); // 비교 슬롯 비활성화, Value만 사용

        // Value가 프리팹에서 비활성화 상태일 수 있으므로 명시적으로 활성화
        var valueT = row.transform.Find("Value");
        if (valueT != null) valueT.gameObject.SetActive(true);

        ConfigureTMP(GetOrCreateTMP(row.transform, "Label", true),
                     label,     labelFontSize, TextAlignmentOptions.Left,  labelColor);
        ConfigureTMP(GetOrCreateTMP(row.transform, "Value", true),
                     valueStr,  valueFontSize, TextAlignmentOptions.Right, diffColor);
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

        var row = SpawnRow(label);
        HideRowChildren(row.transform, "Current", "Arrow", "New"); // 단순 행엔 비교 슬롯 불필요

        ConfigureTMP(GetOrCreateTMP(row.transform, "Label", true),
                     label, labelFontSize, TextAlignmentOptions.Left,  labelColor);
        ConfigureTMP(GetOrCreateTMP(row.transform, "Value", false),
                     val,   valueFontSize, TextAlignmentOptions.Right, color);
    }

    private void AddStatRowText(string label, string value)
    {
        var row = SpawnRow(label);
        HideRowChildren(row.transform, "Current", "Arrow", "New"); // 단순 행엔 비교 슬롯 불필요

        ConfigureTMP(GetOrCreateTMP(row.transform, "Label", true),
                     label, labelFontSize, TextAlignmentOptions.Left,  labelColor);
        ConfigureTMP(GetOrCreateTMP(row.transform, "Value", false),
                     value, valueFontSize, TextAlignmentOptions.Right, valueText);
    }

    private void AddStatRowFloat(string label, float value)
    {
        string sign  = value >= 0f ? "+" : "";
        string val   = (value % 1 == 0) ? $"{sign}{(int)value}" : $"{sign}{value:F1}";
        Color  color = value > 0f ? StatPos : (value < 0f ? StatNeg : StatZero);

        var row = SpawnRow(label);
        HideRowChildren(row.transform, "Current", "Arrow", "New"); // 단순 행엔 비교 슬롯 불필요

        ConfigureTMP(GetOrCreateTMP(row.transform, "Label", true),
                     label, labelFontSize, TextAlignmentOptions.Left,  labelColor);
        ConfigureTMP(GetOrCreateTMP(row.transform, "Value", false),
                     val,   valueFontSize, TextAlignmentOptions.Right, color);
    }

    // ── StatRow 헬퍼 ─────────────────────────────────────────

    // 행 오브젝트 생성 (프리팹 우선, 없으면 코드 생성 폴백)
    private GameObject SpawnRow(string label)
    {
        if (statRowPrefab != null)
        {
            // 프리팹 경로 — 프리팹 자체 설정(HLG·RT) 유지, 이름만 교체
            var row      = Instantiate(statRowPrefab, statListContainer, false);
            row.name     = $"StatRow_{label}";
            return row;
        }

        // 코드 생성 폴백 — SerializeField 값 적용
        var fallback = new GameObject($"StatRow_{label}", typeof(RectTransform),
                                                          typeof(HorizontalLayoutGroup));
        fallback.transform.SetParent(statListContainer, false);

        var hlg = fallback.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing               = statRowSpacing;
        hlg.childForceExpandWidth = true;

        var rt       = fallback.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(0f, statRowHeight);
        rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);

        return fallback;
    }

    // 프리팹 자식에서 TMP 조회 — 없으면 새로 생성
    private TextMeshProUGUI GetOrCreateTMP(Transform parent, string childName, bool expand)
    {
        var t = parent.Find(childName);
        if (t != null)
            return t.GetComponent<TextMeshProUGUI>()
                ?? t.gameObject.AddComponent<TextMeshProUGUI>();

        var obj = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var le           = obj.AddComponent<LayoutElement>();
        le.flexibleWidth = expand ? 1f : 0f;
        le.minWidth      = expand ? 0f : 60f;
        return obj.GetComponent<TextMeshProUGUI>();
    }

    // TMP 텍스트·색상은 항상 적용 / 폰트 크기·정렬은 폴백에서만 적용
    private void ConfigureTMP(TextMeshProUGUI tmp, string text,
                               int fontSize, TextAlignmentOptions align, Color color)
    {
        tmp.text  = text;   // 텍스트 내용: 항상 코드가 채움
        tmp.color = color;  // 색상: 양수/음수/Cures 등 런타임 판단 → 항상 적용

        if (statRowPrefab == null) // 레이아웃 스타일은 폴백(코드 생성)에서만 적용
        {
            tmp.fontSize  = fontSize;
            tmp.alignment = align;
        }
    }

    // 아이템 타입에 불필요한 자식 오브젝트 비활성화
    private void HideRowChildren(Transform rowT, params string[] namesToHide)
    {
        foreach (var n in namesToHide)
        {
            var t = rowT.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    // ═════════════════════════════════════════════════════════
    //  장착 / 해제 / 버리기
    // ═════════════════════════════════════════════════════════

    private void RefreshPrimaryButton(EquipmentData item)
    {
        if (primaryButton == null) return;
        bool equipped = IsEquipped(item);
        if (primaryButtonText != null)
            primaryButtonText.text = equipped ? "Unequip" : "Equip";

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
    //  유틸
    // ═════════════════════════════════════════════════════════

    private string LocalizeType(EquipmentType type)
    {
        switch (type)
        {
            case EquipmentType.Hand:       return "One-Handed";
            case EquipmentType.TwoHanded:  return "Two-Handed";
            case EquipmentType.Armour:     return "Armour";
            case EquipmentType.Accessory:  return "Accessory";
            default:                       return type.ToString();
        }
    }
}
