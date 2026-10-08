using System;
using System.Collections.Generic;
using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-07] 던전 아이템 창 (ITEM LIST · 소지품) — 2열 카드 + 스크롤 + 아래 상세 칸.
/// 화면은 하이라키(Canvas/ItemList_Panel)에 직접 만들어져 있어 위치·크기·색·이미지를 마음대로 고칠 수 있다.
/// 이 스크립트는 아래 참조에 연결된 것만 쓴다 (빈 칸은 건너뜀).
///  - 메뉴의 ITEM 버튼 → Open()
///  - 카테고리 탭: 전체 / 무기 / 방어구 / 장신구 / 회복 / 치료 / 전투 / 특수
///  - 카드 누름 → 아래 상세 칸에 이름·분류·설명·수치 3칸, [버리기] [사용·장착]
///  - ESC 또는 X 버튼 → 닫기. 열려 있는 동안 던전 이동 잠금.
/// 데이터: 소비 아이템 = ConsumableInventory, 장비 = EquipmentBag + 장착 중 장비.
/// </summary>
public class ItemListPanel : MonoBehaviour
{
    public enum Filter { All, Weapon, Armour, Accessory, Recovery, Cure, Battle, Special }

    [Serializable]
    public class CategoryTab
    {
        public Filter filter;
        public Button button;
        [Tooltip("선택된 탭 표시 (밑줄 등). 선택되면 켜짐")]
        public GameObject selectedMark;
        [Tooltip("탭 아이콘 — 선택되지 않으면 흐리게")]
        public Image icon;
    }

    [Header("루트")]
    [Tooltip("켜고 끄는 창 전체 (보통 이 오브젝트)")]
    public GameObject root;
    public Button closeButton;

    [Header("카테고리 탭")]
    public List<CategoryTab> tabs = new List<CategoryTab>();
    public Color tabIconOn = Color.white;
    public Color tabIconOff = new Color(0.62f, 0.55f, 0.42f, 0.75f);

    [Header("목록 (스크롤)")]
    public ScrollRect scroll;
    [Tooltip("카드가 들어갈 부모 (Grid Layout Group)")]
    public RectTransform content;
    [Tooltip("카드 견본 — 꺼둔 채로 두면 실행 중 복제해서 쓴다")]
    public ItemListCard cardTemplate;
    [Tooltip("목록이 비었을 때 문구")]
    public TextMeshProUGUI emptyText;

    [Header("상세 칸")]
    public GameObject detailRoot;
    [Tooltip("아무것도 선택하지 않았을 때 문구")]
    public GameObject detailHint;
    public Image detailIcon;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailTag;
    public TextMeshProUGUI detailDesc;
    [Tooltip("수치 칸 3개의 값 / 이름")]
    public TextMeshProUGUI[] statValues = new TextMeshProUGUI[3];
    public TextMeshProUGUI[] statLabels = new TextMeshProUGUI[3];
    public Button discardButton;
    public Button useButton;
    public TextMeshProUGUI useButtonLabel;

    [Header("상세 팝업 (기존 인벤토리의 DetailPanel)")]
    [Tooltip("카드를 누르면 이 인벤토리의 DetailPanel 을 팝업으로 띄운다. 비워두면 씬에서 찾음. 없으면 아래 상세 칸만 갱신")]
    public InventoryUIManager detailPopup;
    [Tooltip("끄면 카드를 눌러도 팝업 없이 아래 상세 칸만 바뀐다")]
    public bool openDetailPopupOnClick = true;

    [Header("아래 줄")]
    public TextMeshProUGUI bagText;
    public TextMeshProUGUI goldText;

    // ── 내부 ──
    private class Entry
    {
        public ConsumableItemSO consumable;
        public EquipmentData equipment;
        public int quantity;
        public bool equippedCopy; // [2026-10-09] 같은 장비 여러 개 — 카드 한 장 = 한 개. 이 카드가 장착 중인 쪽인지
    }

    private Filter _filter = Filter.All;
    private Entry _selected;
    private readonly List<KeyValuePair<Entry, ItemListCard>> _cards = new List<KeyValuePair<Entry, ItemListCard>>();
    private bool _subscribed;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (discardButton != null) discardButton.onClick.AddListener(OnDiscard);
        if (useButton != null) useButton.onClick.AddListener(OnUse);
        foreach (CategoryTab t in tabs)
        {
            if (t == null || t.button == null) continue;
            Filter f = t.filter;
            t.button.onClick.AddListener(() => SetFilter(f));
        }
    }

    private void OnDestroy() { Unsubscribe(); }

    private void Update()
    {
        if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;
        InventoryUIManager popup = Popup();
        if (popup != null && popup.IsDetailPopupOpen) popup.CloseDetailPopup(); // 팝업이 먼저 닫힘
        else Close();
    }

    // ─────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────

    public void Open()
    {
        if (root == null) root = gameObject;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
        Subscribe();
        _selected = null;
        Refresh();
        ScrollToTop();
    }

    public void Close()
    {
        if (root == null || !root.activeSelf) return;
        InventoryUIManager popup = Popup();
        if (popup != null && popup.IsDetailPopupOpen) popup.CloseDetailPopup();
        root.SetActive(false);
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.UnlockInput(this);
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        if (ConsumableInventory.Instance != null) ConsumableInventory.Instance.OnInventoryChanged += MarkDirty;
        EquipmentBag.OnChanged += MarkDirty;
        PlayerWallet.OnChanged += OnGoldChanged;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;
        if (ConsumableInventory.Instance != null) ConsumableInventory.Instance.OnInventoryChanged -= MarkDirty;
        EquipmentBag.OnChanged -= MarkDirty;
        PlayerWallet.OnChanged -= OnGoldChanged;
    }

    // 아이템을 한꺼번에 여러 개 얻으면 변경 알림이 여러 번 온다 → 프레임 끝에 한 번만 다시 그림
    private bool _dirty;
    private void MarkDirty() { _dirty = true; }

    private void LateUpdate()
    {
        if (_dirty) { _dirty = false; Refresh(); }
    }

    private void OnGoldChanged(int _) { RefreshFooter(); }

    private void SetFilter(Filter f)
    {
        _filter = f;
        _selected = null;
        Refresh();
        ScrollToTop();
    }

    private void ScrollToTop()
    {
        if (scroll == null) return;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
    }

    // ─────────────────────────────────────────
    // 목록
    // ─────────────────────────────────────────

    public void Refresh()
    {
        if (!this || root == null || !root.activeSelf) return;

        foreach (CategoryTab t in tabs)
        {
            if (t == null) continue;
            bool on = t.filter == _filter;
            if (t.selectedMark != null) t.selectedMark.SetActive(on);
            if (t.icon != null) t.icon.color = on ? tabIconOn : tabIconOff;
        }

        // 지난 카드 지우기 (견본은 남김)
        _cards.Clear();
        if (content != null)
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform c = content.GetChild(i);
                if (cardTemplate != null && c == cardTemplate.transform) continue;
                c.gameObject.SetActive(false); // Destroy 는 프레임 끝에 일어나므로 바로 숨겨 배치에서 뺀다
                Destroy(c.gameObject);
            }

        List<Entry> list = BuildList();
        // 선택했던 아이템이 아직 목록에 있으면 그대로 유지
        if (_selected != null)
        {
            Entry still = list.Find(e => e.consumable == _selected.consumable && e.equipment == _selected.equipment);
            _selected = still;
        }

        if (content != null && cardTemplate != null)
            foreach (Entry e in list)
            {
                ItemListCard card = Instantiate(cardTemplate, content, false);
                card.gameObject.name = "Card_" + ItemName(e);
                card.gameObject.SetActive(true);
                Entry captured = e;
                card.Setup(Icon(e), ItemName(e), SubLine(e), QtyLine(e), e.equipment != null && e.equippedCopy,
                           e.consumable != null && e.consumable.isDawnChalice, () => Select(captured));
                card.SetSelected(e == _selected);
                _cards.Add(new KeyValuePair<Entry, ItemListCard>(e, card));
            }

        if (emptyText != null) emptyText.gameObject.SetActive(list.Count == 0);
        RefreshDetail();
        RefreshFooter();
    }

    private List<Entry> BuildList()
    {
        var list = new List<Entry>();
        ConsumableInventory inv = ConsumableInventory.Instance;

        // 새벽의 잔: 항상 맨 앞 (특수)
        if (inv != null && (_filter == Filter.All || _filter == Filter.Special))
        {
            ConsumableItemSO chalice = inv.dawnChaliceItem != null ? inv.dawnChaliceItem : Resources.Load<ConsumableItemSO>("Item_Equipments/Items/Dawn_Chalice");
            if (chalice != null) list.Add(new Entry { consumable = chalice, quantity = inv.dawnChaliceCharges });
        }

        // 소비 아이템: 분류 → 이름 순
        if (inv != null)
        {
            var slots = new List<ConsumableInventory.ConsumableSlot>();
            foreach (var s in inv.slots)
                if (s != null && s.item != null && !s.item.isDawnChalice && s.quantity > 0 && Matches(s.item)) slots.Add(s);
            slots.Sort((a, b) =>
            {
                int c = ((int)a.item.itemCategory).CompareTo((int)b.item.itemCategory);
                return c != 0 ? c : string.Compare(a.item.itemName, b.item.itemName, StringComparison.OrdinalIgnoreCase);
            });
            foreach (var s in slots) list.Add(new Entry { consumable = s.item, quantity = s.quantity });
        }

        // 장비: 가진 것 + 장착 중인 것 (장착 중 먼저)
        var equips = new List<EquipmentData>();
        EquipmentManager mgr = Manager();
        if (mgr != null) foreach (var e in mgr.GetEquippedItems()) if (e != null && !equips.Contains(e)) equips.Add(e);
        foreach (var e in EquipmentBag.Items) if (e != null && !equips.Contains(e)) equips.Add(e);
        // [2026-10-09] 같은 장비는 가진 개수만큼 카드 (장착 중인 것 먼저)
        foreach (var e in equips)
        {
            if (!Matches(e)) continue;
            int worn = EquipmentBag.EquippedCount(mgr, e);
            int total = Mathf.Max(EquipmentBag.Count(e), worn);
            for (int i = 0; i < total; i++) list.Add(new Entry { equipment = e, quantity = 1, equippedCopy = i < worn });
        }
        return list;
    }

    private bool Matches(ConsumableItemSO c)
    {
        switch (_filter)
        {
            case Filter.All: return true;
            case Filter.Recovery: return c.itemCategory == ItemCategory.HpRecovery || c.itemCategory == ItemCategory.MpRecovery;
            case Filter.Cure: return c.itemCategory == ItemCategory.StatusCure;
            case Filter.Battle: return c.itemCategory == ItemCategory.BattleSupport;
            case Filter.Special: return c.itemCategory == ItemCategory.Special;
            default: return false;
        }
    }

    private bool Matches(EquipmentData e)
    {
        switch (_filter)
        {
            case Filter.All: return true;
            case Filter.Weapon: return e.equipmentType == EquipmentType.Hand || e.equipmentType == EquipmentType.TwoHanded;
            case Filter.Armour: return e.equipmentType == EquipmentType.Armour;
            case Filter.Accessory: return e.equipmentType == EquipmentType.Accessory;
            default: return false;
        }
    }

    private void Select(Entry e)
    {
        _selected = e;
        foreach (var kv in _cards) kv.Value.SetSelected(kv.Key == e);
        RefreshDetail();

        // [2026-10-08] 기존 인벤토리의 상세 팝업 (정보가 더 많음: 장비 비교·저주·가격 등)
        InventoryUIManager popup = openDetailPopupOnClick ? Popup() : null;
        if (popup == null) return;
        if (e.equipment != null) popup.ShowDetailPopup(e.equipment, e.equippedCopy, Refresh);
        else if (e.consumable != null) popup.ShowDetailPopup(e.consumable, Refresh);
    }

    private InventoryUIManager Popup()
    {
        if (detailPopup == null) detailPopup = FindFirstObjectByType<InventoryUIManager>(FindObjectsInactive.Include);
        return detailPopup;
    }

    // ─────────────────────────────────────────
    // 상세 칸
    // ─────────────────────────────────────────

    private void RefreshDetail()
    {
        bool has = _selected != null;
        if (detailHint != null) detailHint.SetActive(!has);
        if (detailRoot != null) detailRoot.SetActive(has);
        if (!has) return;

        Entry e = _selected;
        if (detailIcon != null) { detailIcon.sprite = Icon(e); detailIcon.enabled = detailIcon.sprite != null; detailIcon.preserveAspect = true; }
        if (detailName != null) detailName.text = ItemName(e);
        if (detailTag != null) detailTag.text = TagLine(e);
        if (detailDesc != null)
        {
            string d = e.consumable != null ? e.consumable.description : e.equipment.description;
            detailDesc.text = string.IsNullOrEmpty(d) ? "" : "“" + d.Trim() + "”";
        }

        string[] v = new string[3], l = new string[3];
        if (e.consumable != null) ConsumableStats(e, v, l); else EquipmentStats(e.equipment, v, l);
        for (int i = 0; i < 3; i++)
        {
            if (statValues != null && i < statValues.Length && statValues[i] != null) statValues[i].text = v[i] ?? "—";
            if (statLabels != null && i < statLabels.Length && statLabels[i] != null) statLabels[i].text = l[i] ?? "";
        }

        // 버튼
        if (e.consumable != null)
        {
            ConsumableItemSO c = e.consumable;
            bool canUse = c.usableOnMap && (c.isDawnChalice ? e.quantity > 0 : e.quantity > 0 && (!c.isChargeable || c.currentCharges > 0));
            SetUse("사용 · USE", canUse);
            if (discardButton != null) discardButton.interactable = !c.isPermanent && !c.isDawnChalice;
        }
        else
        {
            bool equipped = e.equippedCopy;
            SetUse(equipped ? "해제 · UNEQUIP" : "장착 · EQUIP", Manager() != null);
            if (discardButton != null) discardButton.interactable = true;
        }
    }

    private void SetUse(string label, bool interactable)
    {
        if (useButtonLabel != null) useButtonLabel.text = label;
        if (useButton != null) useButton.interactable = interactable;
    }

    private static void ConsumableStats(Entry e, string[] v, string[] l)
    {
        ConsumableItemSO c = e.consumable;
        // 1칸: 주 효과
        if (c.hpRecoveryPercent > 0f) { v[0] = "+" + Mathf.RoundToInt(c.hpRecoveryPercent * 100f) + "% HP"; l[0] = "회복"; }
        else if (c.mpRecoveryPercent > 0f) { v[0] = "+" + Mathf.RoundToInt(c.mpRecoveryPercent * 100f) + "% MP"; l[0] = "회복"; }
        else if (c.cureTypes != null && c.cureTypes.Count > 0) { v[0] = c.cureTypes.Count >= 5 ? "전부" : CureNames(c); l[0] = "치료"; }
        else if (c.attackBuffPercent > 0f) { v[0] = "ATK +" + Mathf.RoundToInt(c.attackBuffPercent * 100f) + "%"; l[0] = "강화"; }
        else if (c.agilityBuff != 0) { v[0] = "SPD +" + c.agilityBuff; l[0] = "강화"; }
        else if (c.evasionBuff > 0f) { v[0] = "EVA +" + Mathf.RoundToInt(c.evasionBuff * 100f) + "%"; l[0] = "강화"; }
        else if (c.escapeChanceBuff > 0f) { v[0] = "+" + Mathf.RoundToInt(c.escapeChanceBuff * 100f) + "%"; l[0] = "도망"; }
        else { v[0] = "—"; l[0] = "효과"; }

        // 2칸: 보유 / 충전
        if (c.isDawnChalice)
        {
            ConsumableInventory inv = ConsumableInventory.Instance;
            v[1] = (inv != null ? inv.dawnChaliceCharges : 0) + " / " + (inv != null ? inv.dawnChaliceMaxCharges : c.maxCharges);
            l[1] = "충전";
        }
        else if (c.isChargeable) { v[1] = c.currentCharges + " / " + c.maxCharges; l[1] = "충전"; }
        else { v[1] = e.quantity + " / " + Mathf.Max(e.quantity, c.maxStack); l[1] = "보유"; }

        // 3칸: 지속 턴 또는 판매가
        if (c.buffDuration > 0) { v[2] = c.buffDuration + "턴"; l[2] = "지속"; }
        else { v[2] = c.sellPrice > 0 ? c.sellPrice + " G" : "—"; l[2] = "판매가"; }
    }

    private static void EquipmentStats(EquipmentData q, string[] v, string[] l)
    {
        bool weapon = q.equipmentType == EquipmentType.Hand || q.equipmentType == EquipmentType.TwoHanded;
        v[0] = weapon ? Signed(q.attackBonus) : Signed(q.defenseBonus);
        l[0] = weapon ? "공격력" : "방어력";
        if (q.hpBonus != 0) { v[1] = Signed(q.hpBonus); l[1] = "HP"; }
        else if (q.agiBonus != 0) { v[1] = Signed(q.agiBonus); l[1] = "민첩"; }
        else if (q.magicBonus != 0) { v[1] = Signed(q.magicBonus); l[1] = "마력"; }
        else { v[1] = weapon ? Signed(q.defenseBonus) : Signed(q.attackBonus); l[1] = weapon ? "방어력" : "공격력"; }
        v[2] = q.sellPrice > 0 ? q.sellPrice + " G" : "—";
        l[2] = "판매가";
    }

    private static string Signed(int n) { return (n >= 0 ? "+" : "") + n; }

    private static string CureNames(ConsumableItemSO c)
    {
        var names = new List<string>();
        foreach (var t in c.cureTypes) names.Add(DungeonFieldStatus.NameOf(t));
        return string.Join(", ", names.ToArray());
    }

    // ─────────────────────────────────────────
    // 버튼 동작
    // ─────────────────────────────────────────

    private void OnUse()
    {
        Entry e = _selected;
        if (e == null) return;

        if (e.equipment != null)
        {
            EquipmentManager mgr = Manager();
            if (mgr == null) return;
            if (e.equippedCopy) Unequip(mgr, e.equipment);
            else if (EquipmentBag.HasSpare(mgr, e.equipment)) mgr.EquipItem(e.equipment);
            Refresh();
            return;
        }

        ConsumableInventory inv = ConsumableInventory.Instance;
        ConsumableItemSO c = e.consumable;
        if (inv == null || c == null || !c.usableOnMap || !inv.HasItem(c)) return;
        PlayerStats hero = Hero();
        if (hero == null) { Debug.LogWarning("[ItemListPanel] 주인공 PlayerStats 를 찾지 못했습니다."); return; }
        if (!inv.UseItem(c)) return;
        var fx = ConsumableEffectApplier.ApplyEffects(hero, c);
        Debug.Log($"[ItemListPanel] {c.itemName} 사용 — HP +{fx.hpHealed}, MP +{fx.mpHealed}" +
                  (fx.curesApplied != null && fx.curesApplied.Count > 0 ? ", 해제 " + string.Join(", ", fx.curesApplied) : ""));
        Refresh(); // 인벤토리 이벤트로도 불리지만, 새벽의 잔 충전처럼 이벤트가 없는 변화도 있어 한 번 더
    }

    private void OnDiscard()
    {
        Entry e = _selected;
        if (e == null) return;
        if (e.consumable != null)
        {
            if (e.consumable.isPermanent || e.consumable.isDawnChalice) return;
            if (ConsumableInventory.Instance != null) ConsumableInventory.Instance.RemoveItem(e.consumable);
        }
        else
        {
            EquipmentManager mgr = Manager();
            if (mgr != null && e.equippedCopy) Unequip(mgr, e.equipment);
            EquipmentBag.Remove(e.equipment); // 한 개만
        }
        _selected = null;
        Refresh();
    }

    private static void Unequip(EquipmentManager mgr, EquipmentData item)
    {
        if (mgr.rightHand == item) mgr.UnequipItem("RightHand");
        else if (mgr.leftHand == item) mgr.UnequipItem("LeftHand");
        else if (mgr.body == item) mgr.UnequipItem("Body");
        else if (mgr.accessory1 == item) mgr.UnequipItem("Accessory1");
        else if (mgr.accessory2 == item) mgr.UnequipItem("Accessory2");
        // 해제한 장비는 가방에 그대로 남는다 (가방 목록에 장착 중인 것도 들어 있음 — 다시 넣으면 개수가 늘어남)
    }

    // ─────────────────────────────────────────
    // 아래 줄
    // ─────────────────────────────────────────

    private void RefreshFooter()
    {
        if (goldText != null) goldText.text = PlayerWallet.Gold.ToString("N0") + " G";
        if (bagText != null)
        {
            int kinds = 0, total = 0;
            ConsumableInventory inv = ConsumableInventory.Instance;
            if (inv != null)
                foreach (var s in inv.slots)
                    if (s != null && s.item != null && !s.item.isDawnChalice && s.quantity > 0) { kinds++; total += s.quantity; }
            bagText.text = $"소비 {kinds}종 · {total}개   장비 {EquipmentBag.Items.Count}";
        }
    }

    // ─────────────────────────────────────────
    // 표시 문구
    // ─────────────────────────────────────────

    private static string ItemName(Entry e)
    {
        if (e.consumable != null) return e.consumable.itemName;
        return !string.IsNullOrEmpty(e.equipment.equipmentName) ? e.equipment.equipmentName : e.equipment.name;
    }

    private static Sprite Icon(Entry e)
    {
        if (e.consumable != null) return e.consumable.itemIcon != null ? e.consumable.itemIcon : e.consumable.flatIcon;
        return e.equipment.flatIcon != null ? e.equipment.flatIcon : e.equipment.equipmentIcon;
    }

    private string SubLine(Entry e)
    {
        if (e.consumable != null)
        {
            string[] v = new string[3], l = new string[3];
            ConsumableStats(e, v, l);
            return CategoryName(e.consumable) + "  ·  " + v[0];
        }
        EquipmentData q = e.equipment;
        bool weapon = q.equipmentType == EquipmentType.Hand || q.equipmentType == EquipmentType.TwoHanded;
        return EquipTypeName(q) + "  ·  " + (weapon ? "ATK " + Signed(q.attackBonus) : "DEF " + Signed(q.defenseBonus));
    }

    private string QtyLine(Entry e)
    {
        if (e.consumable != null)
        {
            if (e.consumable.isDawnChalice)
            {
                ConsumableInventory inv = ConsumableInventory.Instance;
                return (inv != null ? inv.dawnChaliceCharges : 0) + "/" + (inv != null ? inv.dawnChaliceMaxCharges : e.consumable.maxCharges);
            }
            return "×" + e.quantity;
        }
        return e.equippedCopy ? "장착" : "";
    }

    private string TagLine(Entry e)
    {
        if (e.consumable != null)
        {
            ConsumableItemSO c = e.consumable;
            string tag = c.isDawnChalice ? "<color=#7FB6FF>특수 · 유일</color>" : "<color=#C9A86A>" + CategoryName(c) + " · 소비</color>";
            if (!c.usableOnMap) tag += "  <color=#FF8A70>전투 전용</color>";
            return tag;
        }
        return "<color=#C9A86A>" + EquipTypeName(e.equipment) + " · 장비</color>" + (e.equippedCopy ? "  <color=#FFD24A>장착 중</color>" : "");
    }

    private static string CategoryName(ConsumableItemSO c)
    {
        if (c.isDawnChalice) return "특수";
        switch (c.itemCategory)
        {
            case ItemCategory.HpRecovery: return "HP 회복";
            case ItemCategory.MpRecovery: return "MP 회복";
            case ItemCategory.StatusCure: return "치료";
            case ItemCategory.BattleSupport: return "전투 보조";
            default: return "특수";
        }
    }

    private static string EquipTypeName(EquipmentData q)
    {
        switch (q.equipmentType)
        {
            case EquipmentType.Hand: return "한손 무기";
            case EquipmentType.TwoHanded: return "양손 무기";
            case EquipmentType.Armour: return "방어구";
            default: return "장신구";
        }
    }

    // ─────────────────────────────────────────

    private static EquipmentManager Manager()
    {
        return FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
    }

    private static bool IsEquipped(EquipmentData item)
    {
        EquipmentManager m = Manager();
        if (m == null || item == null) return false;
        return m.rightHand == item || m.leftHand == item || m.body == item || m.accessory1 == item || m.accessory2 == item;
    }

    private static PlayerStats Hero()
    {
        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            if (p != null && !p.IsRecruitedCompanion) return p;
        return FindFirstObjectByType<PlayerStats>();
    }
}
