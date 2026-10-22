using System.Collections.Generic;
using Abyssdawn;
using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-15] 동료 몬스터 상태창 — 새 카드 (StatusPanel 의 Companion_SLot2~4 를 코드로 다시 그린다).
///  - 예전 칸(BasicPanel·StatusPanel·BattleSkills·Panel)은 숨기고, 같은 자리에 새 카드를 만든다
///    (예전 칸의 BattleSkills 에 주인공용 스크립트가 붙어 3·4번 칸에 주인공 아이콘이 뜨던 문제도 이것으로 사라짐)
///  - [2026-10-22] 주인공 상태창과 같은 짜임·모양 (검은 반투명 상자 + 금테 이중선)
///    왼쪽: 기본 정보(그림·이름·Lv·종족·HP/MP) · 능력치 · 장비(든 무기)
///    오른쪽: 본능(주인공의 성향 자리) · 종족(주인공의 종의 기억 자리) · 패시브/액티브 스킬
///  - 아래: 상세(능력치 상세 페이지) · Position(자리 이동) · Release(풀어 주기 — 예/아니오 확인)
/// 카드 순서 = ActiveRoster 순서 (CompanionSlotView 와 같다).
/// </summary>
public class CompanionCardView : MonoBehaviour
{
    // 색은 StatusPanelStyle(주인공 상태창)과 같게
    private static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);
    private static readonly Color GoldDim = new Color(0.72f, 0.64f, 0.45f, 1f);
    private static readonly Color Cream = Color.white;
    private static readonly Color Muted = new Color(0.6f, 0.58f, 0.54f);
    private static readonly Color BoxColor = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color FrameBright = new Color(0.86f, 0.74f, 0.45f, 0.95f);
    private static readonly Color FrameInner = new Color(0.86f, 0.74f, 0.45f, 0.30f);
    private static readonly Color FrameDim = new Color(0.72f, 0.64f, 0.45f, 0.55f);
    private static readonly Color EmptySlot = new Color(0.10f, 0.09f, 0.08f, 0.9f);

    private static readonly string[] SlotNames = { "Companion_SLot2", "Companion_SLot3", "Companion_SLot4" };
    private static readonly string[] StatNames = { "HP", "MP", "STR", "DEF", "MAG", "AGI", "LUK" };

    private class Card
    {
        public RectTransform root;
        public Image portrait;
        public TextMeshProUGUI name, level, race, hpText, mpText, instinctName, weaponName, weaponDesc, raceText, emptyText;
        public TextMeshProUGUI[] statValues = new TextMeshProUGUI[7];
        public GameObject emptyBox;
        public Image instinctIcon, weaponIcon;
        public Image[] passives = new Image[3], actives = new Image[6];
        public GameObject body, head;
        public Button detailButton, positionButton, releaseButton;
    }

    private readonly List<Card> _cards = new List<Card>();
    private TMP_FontAsset _font;
    private GameObject _picker;

    private void OnEnable()
    {
        if (_cards.Count == 0) Build();
        Refresh();
    }

    private TMP_FontAsset Font()
    {
        if (_font != null) return _font;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name == "NotoSansKR SDF") { _font = f; break; }
        return _font;
    }

    // ─────────────────────────────────────────
    // 만들기
    // ─────────────────────────────────────────

    private void Build()
    {
        for (int i = 0; i < SlotNames.Length; i++)
        {
            Transform slot = FindDeep(transform, SlotNames[i]);
            if (slot == null) { _cards.Add(null); continue; }
            _cards.Add(BuildCard((RectTransform)slot, i));
        }
    }

    private Card BuildCard(RectTransform slot, int index)
    {
        // 예전 칸이 차지하던 영역 (BasicPanel 위 ~ Panel 아래) 을 그대로 쓴다
        var basic = slot.Find("BasicPanel") as RectTransform;
        var buttons = slot.Find("Panel") as RectTransform;
        var skills = slot.Find("BattleSkills") as RectTransform;
        Rect area = AreaOf(slot, basic, skills, buttons);
        foreach (Transform c in slot) c.gameObject.SetActive(false);

        var card = new Card();
        card.root = NewRect("CompanionCard", slot);
        card.root.anchorMin = card.root.anchorMax = new Vector2(0.5f, 0.5f); card.root.pivot = new Vector2(0f, 1f);
        card.root.sizeDelta = area.size; card.root.localPosition = new Vector3(area.xMin, area.yMax, 0f);
        // 주인공 상태창처럼 두 기둥: 왼쪽(기본 정보 · 능력치 · 장비) / 오른쪽(본능 · 종족 · 스킬)
        float W = area.width, H = area.height, G = 20f;
        float LW = Mathf.Round(W * 0.56f), RW = W - LW - G, RX = LW + G;
        float btnH = 100f, bottom = H - btnH - 24f; // 상자들이 내려올 수 있는 끝 (아래는 버튼 줄)

        // ── 왼쪽 1) 기본 정보: 그림 + 이름 + Lv + 종족 + HP/MP ──
        const float basicH = 330f;
        var head = Panel(card.root, "BasicPanel", 0f, 0f, LW, basicH);
        card.head = head.gameObject;
        var pback = SlotBack(head, 24f, 40f, 250f);
        card.portrait = NewImage("Portrait", pback, Color.white); Inset(card.portrait.rectTransform, 8f); card.portrait.preserveAspect = true;
        float tx = 296f, tw = LW - tx - 20f;
        card.name = Text(head, "", tx, 34f, tw, 60f, 44f, Gold, TextAlignmentOptions.MidlineLeft);
        card.name.fontStyle = FontStyles.Bold;
        card.level = Text(head, "", tx, 98f, tw, 40f, 28f, GoldDim, TextAlignmentOptions.MidlineLeft);
        card.race = Text(head, "", tx, 138f, tw, 40f, 28f, GoldDim, TextAlignmentOptions.MidlineLeft);
        card.hpText = LabelValue(head, "HP :", tx, 196f, tw);
        card.mpText = LabelValue(head, "MP :", tx, 250f, tw);

        // 동료가 없을 때 문구 (카드 가운데) — [2026-10-18] 뒤가 비치지 않게 다른 칸과 같은 어두운 상자 위에
        card.emptyBox = Panel(card.root, "EmptyBox", 0f, 0f, W, H).gameObject;
        card.emptyText = Text(card.root, "<color=#777777>빈 자리</color>\n<size=70%><color=#666666>전투 후 몬스터를 영입하세요.</color></size>", 0f, 0f, W, H, 44f, Muted, TextAlignmentOptions.Center);
        card.emptyText.textWrappingMode = TextWrappingModes.Normal;

        card.body = new GameObject("Body", typeof(RectTransform));
        card.body.transform.SetParent(card.root, false);
        var body = (RectTransform)card.body.transform; Stretch(body);

        // ── 왼쪽 2) 능력치 (주인공의 StatusPanel 처럼 "이름 : 값" 줄) ──
        float statsY = basicH + G, rowH = 62f, statsH = 40f + StatNames.Length * rowH;
        var stats = Panel(body, "StatusPanel", 0f, statsY, LW, statsH);
        for (int k = 0; k < StatNames.Length; k++)
            card.statValues[k] = LabelValue(stats, StatNames[k] + " :", 40f, 20f + k * rowH, LW - 80f);

        // ── 왼쪽 3) 장비 (든 무기 — 그림 + 이름 + 설명) ──
        float eqY = statsY + statsH + G;
        var eq = Panel(body, "EquipmentPanel", 0f, eqY, LW, Mathf.Max(200f, bottom - eqY));
        Header(eq, "무기", 0f, 14f, LW);
        card.weaponIcon = IconSlot(eq, 30f, 70f, 100f);
        card.weaponName = Text(eq, "", 150f, 70f, LW - 180f, 50f, 32f, Cream, TextAlignmentOptions.MidlineLeft);
        card.weaponName.fontStyle = FontStyles.Bold;
        card.weaponDesc = Text(eq, "", 150f, 122f, LW - 180f, Mathf.Max(60f, bottom - eqY - 140f), 24f, GoldDim, TextAlignmentOptions.TopLeft);
        card.weaponDesc.textWrappingMode = TextWrappingModes.Normal;

        // ── 오른쪽 1) 본능 (주인공의 "성향" 자리: 제목 + 아이콘 + 이름) ──
        const float instH = 170f;
        var inst = Panel(body, "Instinct", RX, 0f, RW, instH);
        Header(inst, "본능", 0f, 12f, RW);
        card.instinctIcon = IconSlot(inst, 30f, 66f, 84f);
        card.instinctName = Text(inst, "", 134f, 66f, RW - 154f, 84f, 30f, Cream, TextAlignmentOptions.MidlineLeft);
        card.instinctName.fontStyle = FontStyles.Bold;

        // ── 오른쪽 2) 종족 (주인공의 "종의 기억" 자리) ──
        float raceY = instH + G, raceH = 250f;
        var race = Panel(body, "Race", RX, raceY, RW, raceH);
        Header(race, "종족", 0f, 12f, RW);
        card.raceText = Text(race, "", 30f, 66f, RW - 60f, raceH - 80f, 28f, Cream, TextAlignmentOptions.TopLeft);
        card.raceText.lineSpacing = 18f;

        // ── 오른쪽 3) 스킬 (패시브 3칸 · 액티브 3×2칸, 주인공과 같은 배치) ──
        float skY = raceY + raceH + G;
        var sk = Panel(body, "BattleSkills", RX, skY, RW, bottom - skY);
        float icon = 100f, gap = 22f, sx = (RW - (3 * icon + 2 * gap)) * 0.5f;
        Header(sk, "패시브 스킬", 0f, 22f, RW);
        for (int k = 0; k < 3; k++) card.passives[k] = IconSlot(sk, sx + k * (icon + gap), 82f, icon);
        float ay = 82f + icon + 34f;
        Header(sk, "액티브 스킬", 0f, ay, RW);
        for (int k = 0; k < 6; k++) card.actives[k] = IconSlot(sk, sx + (k % 3) * (icon + gap), ay + 60f + (k / 3) * (icon + gap), icon);

        // ── 아래) 버튼: 상세 · 자리 바꾸기 · 놓아주기 ──
        float by = H - btnH, bw = (W - 2 * G) / 3f;
        card.detailButton = BigButton(body, "Detail", "상세", 0f, by, bw, btnH);
        card.positionButton = BigButton(body, "Position", "자리 바꾸기", bw + G, by, bw, btnH);
        card.releaseButton = BigButton(body, "Release", "놓아주기", 2f * (bw + G), by, bw, btnH);
        int captured = index;
        card.detailButton.onClick.AddListener(() => OpenDetail(captured));
        card.positionButton.onClick.AddListener(() => OpenPositionPicker(captured));
        card.releaseButton.onClick.AddListener(() =>
        {
            var rc = GetComponent<ReleaseController>();
            if (rc != null) rc.RequestRelease(true, captured);
        });
        return card;
    }

    // ─────────────────────────────────────────
    // 채우기
    // ─────────────────────────────────────────

    public void Refresh()
    {
        var roster = CompanionPartyPersistence.ActiveRoster;
        for (int i = 0; i < _cards.Count; i++)
        {
            var c = _cards[i];
            if (c == null) continue;
            var entry = roster != null && i < roster.Count ? roster[i] : null;
            MonsterSO so = entry != null ? CompanionPartyPersistence.LoadCompanion(entry.resourcePath) : null;
            bool has = so != null;
            c.body.SetActive(has);
            c.head.SetActive(has);
            c.emptyText.gameObject.SetActive(!has);
            if (c.emptyBox != null) c.emptyBox.SetActive(!has);
            if (!has) { c.name.text = c.level.text = c.race.text = c.hpText.text = c.mpText.text = ""; continue; }

            c.portrait.sprite = so.Sprite;
            c.portrait.color = so.Sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            c.name.text = so.MonsterName;
            c.level.text = $"Lv {so.MonsterLevel}  ·  동료";
            c.race.text = $"{StatDetailPopup.RaceName(so.Race)}  ·  {StatDetailPopup.TypeName(so.Type)}";
            c.hpText.text = $"{entry.currentHP}/{so.HP}";
            c.mpText.text = $"{entry.currentMP}/{so.MP}";

            // 능력치 — 무기 보너스는 주인공처럼 (+n) 으로
            var w = so.EquippedWeapon;
            int wAtk = w != null ? w.attackBonus : 0, wAgi = w != null ? w.agiBonus : 0;
            int[] vals = { so.HP, so.MP, so.ATK + wAtk, so.DEF, so.MAG, so.AGI + wAgi, so.LUK };
            int[] bonus = { 0, 0, wAtk, 0, 0, wAgi, 0 };
            for (int k = 0; k < c.statValues.Length; k++)
                c.statValues[k].text = bonus[k] != 0 ? $"{vals[k]}({bonus[k]:+#;-#;0})" : vals[k].ToString();

            // 무기
            SetIcon(c.weaponIcon, w != null ? (w.equipmentIcon != null ? w.equipmentIcon : w.icon) : null);
            c.weaponName.text = w != null ? w.itemName : "Empty";
            c.weaponName.color = w != null ? Cream : new Color(0.5f, 0.5f, 0.52f, 1f);
            c.weaponDesc.text = w != null ? w.description : "";

            var instinct = so.BasicAttackOverride;
            SetIcon(c.instinctIcon, instinct != null ? instinct.skillIcon : null);
            c.instinctName.text = instinct != null ? instinct.skillName : "<color=#777777>—</color>";
            SkillData capturedInstinct = instinct;
            DetailClickTarget.Attach(c.instinctIcon, () => DetailClickTarget.ForSkill(capturedInstinct));

            c.raceText.text =
                $"<color=#B8A57A>종족</color>   {StatDetailPopup.RaceName(so.Race)}\n" +
                $"<color=#B8A57A>성격</color>   {StatDetailPopup.AiName(so.AIPattern)}";

            for (int k = 0; k < c.actives.Length; k++)
            {
                SkillData s = so.ActiveSkills != null && k < so.ActiveSkills.Count ? so.ActiveSkills[k] : null;
                SetIcon(c.actives[k], s != null ? s.skillIcon : null, s != null ? s.skillName : null);
                SkillData cs = s;
                DetailClickTarget.Attach(c.actives[k], () => DetailClickTarget.ForSkill(cs));
            }
            for (int k = 0; k < c.passives.Length; k++)
            {
                PassiveData pd = so.PassiveSkills != null && k < so.PassiveSkills.Count ? so.PassiveSkills[k] : null;
                SetIcon(c.passives[k], pd != null ? pd.passiveIcon : null);
            }
        }
    }

    private static void OpenDetail(int rosterIndex)
    {
        var roster = CompanionPartyPersistence.ActiveRoster;
        var entry = roster != null && rosterIndex < roster.Count ? roster[rosterIndex] : null;
        MonsterSO so = entry != null ? CompanionPartyPersistence.LoadCompanion(entry.resourcePath) : null;
        if (so != null) StatDetailPopup.ShowCompanion(so, entry.currentHP, entry.currentMP);
    }

    /// <summary>아이콘을 넣는다. 스킬은 있는데 그림이 없으면(fallbackName) 칸 안에 이름을 작게 써서 빈 칸과 구별한다.</summary>
    private void SetIcon(Image img, Sprite s, string fallbackName = null)
    {
        bool nameOnly = s == null && !string.IsNullOrEmpty(fallbackName);
        img.sprite = s;
        img.color = s != null ? Color.white : new Color(1f, 1f, 1f, nameOnly ? 0.001f : 0f); // 거의 투명해도 눌리게
        img.raycastTarget = s != null || nameOnly;

        var label = img.transform.Find("NameLabel") as RectTransform;
        if (nameOnly && label == null)
        {
            var t = Text((RectTransform)img.transform, "", 0f, 0f, 10f, 10f, 22f, Cream, TextAlignmentOptions.Center);
            t.name = "NameLabel"; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Truncate;
            Stretch(t.rectTransform);
            label = t.rectTransform;
        }
        if (label != null)
        {
            label.gameObject.SetActive(nameOnly);
            if (nameOnly) label.GetComponent<TextMeshProUGUI>().text = fallbackName;
        }
    }

    // ─────────────────────────────────────────
    // 자리 이동 — 4칸 중 고르면 맞바꿈 (빈 칸이면 이동)
    // ─────────────────────────────────────────

    private void OpenPositionPicker(int rosterIndex)
    {
        if (_picker != null) Destroy(_picker);
        int from = CompanionPartyPersistence.RosterIndexToMainLineSlot(rosterIndex);
        if (from < 0) return;
        var card = _cards[rosterIndex];
        if (card == null) return;

        _picker = new GameObject("PositionPicker", typeof(RectTransform), typeof(Image), typeof(Button));
        _picker.transform.SetParent(card.root, false);
        var rt = (RectTransform)_picker.transform; Stretch(rt);
        _picker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        _picker.GetComponent<Button>().onClick.AddListener(() => Destroy(_picker)); // 바깥을 누르면 닫힘

        float W = card.root.sizeDelta.x;
        var box = Panel(rt, "Box", 60f, 380f, W - 120f, 560f);
        Text(box, "어느 자리로 옮길까요?", 0f, 24f, W - 120f, 56f, 36f, Gold, TextAlignmentOptions.Center);
        var occ = CompanionPartyPersistence.BuildMainLineOccupancy();
        float bw = (W - 120f - 5 * 20f) / 4f;
        for (int s = 0; s < occ.Length; s++)
        {
            string who = occ[s].IsHero ? "주인공" : occ[s].IsEmpty ? "빈 자리" :
                (CompanionPartyPersistence.LoadCompanion(occ[s].entry.resourcePath)?.MonsterName ?? "?");
            string label = $"{s + 1}\n<size=60%>{who}</size>";
            var b = BigButton(box, "Slot" + (s + 1), label, 20f + s * (bw + 20f), 110f, bw, 200f);
            int target = s;
            if (target == from) { b.interactable = false; b.GetComponentInChildren<TextMeshProUGUI>().color = Muted; continue; }
            b.onClick.AddListener(() =>
            {
                bool ok = CompanionPartyPersistence.SwapMainLine(from, target);
                Debug.Log($"[CompanionCard] 자리 이동 {from + 1} → {target + 1} = {ok}");
                Destroy(_picker);
                var bar = FindFirstObjectByType<DungeonPartyBar>(); if (bar != null) bar.Refresh();
                var csv = GetComponent<CompanionSlotView>(); if (csv != null) csv.Refresh();
                Refresh();
                if (DungeonHud.Instance != null) DungeonHud.Instance.Toast($"파티 순서 변경: {from + 1}번 ↔ {target + 1}번");
            });
        }
        Text(box, "<color=#888888>전열: 1 · 2   후열: 3 · 4</color>", 0f, 340f, W - 120f, 44f, 26f, Muted, TextAlignmentOptions.Center);
        var cancel = BigButton(box, "Cancel", "Cancel", (W - 120f - 300f) * 0.5f, 420f, 300f, 96f);
        cancel.onClick.AddListener(() => Destroy(_picker));
    }

    // ─────────────────────────────────────────
    // 작은 도우미 (좌표는 부모의 왼쪽 위 기준, 아래로 +)
    // ─────────────────────────────────────────

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    /// <summary>예전 칸들의 바깥 사각형 (slot 기준 로컬 좌표).</summary>
    private static Rect AreaOf(RectTransform slot, params RectTransform[] parts)
    {
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        var c = new Vector3[4];
        foreach (var p in parts)
        {
            if (p == null) continue;
            p.GetWorldCorners(c);
            foreach (var w in c)
            {
                Vector3 l = slot.InverseTransformPoint(w);
                xMin = Mathf.Min(xMin, l.x); xMax = Mathf.Max(xMax, l.x); yMin = Mathf.Min(yMin, l.y); yMax = Mathf.Max(yMax, l.y);
            }
        }
        if (xMin == float.MaxValue) return new Rect(-420f, -1450f, 840f, 1450f);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
        return img;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    private static void Inset(RectTransform rt, float d) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(d, d); rt.offsetMax = new Vector2(-d, -d); }

    /// <summary>[2026-10-22] 주인공 상태창 상자와 같게: 검은 반투명(85%) + 바깥 금선 + 안쪽 가는 금선.</summary>
    private RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h)
    {
        var box = NewImage(name, parent, BoxColor); Place(box.rectTransform, x, y, w, h);
        box.raycastTarget = true; // 뒤(던전)로 클릭이 새지 않게
        AddFrame(box.rectTransform, "BoxFrame", FrameBright, 3f);
        AddFrame(box.rectTransform, "BoxFrameInner", FrameInner, -6f);
        return box.rectTransform;
    }

    private static Sprite _border;

    /// <summary>GlowBorder 9분할 금선 (가운데 비움). outset 음수면 안쪽.</summary>
    private static void AddFrame(RectTransform parent, string name, Color color, float outset)
    {
        if (_border == null) _border = Resources.Load<Sprite>("UI/GlowBorder");
        if (_border == null) return;
        var img = NewImage(name, parent, color);
        img.sprite = _border; img.type = Image.Type.Sliced; img.fillCenter = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset); rt.offsetMax = new Vector2(outset, outset);
    }

    /// <summary>상자 머리글 (가운데, 금색 굵게 — 주인공 상태창 머리글과 같음).</summary>
    private void Header(RectTransform parent, string text, float x, float y, float w)
    {
        var t = Text(parent, text, x, y, w, 46f, 32f, Gold, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold; t.characterSpacing = 2f;
    }

    /// <summary>"HP :" 같은 흐린 금색 이름 + 오른쪽 흰 값. 값 글자를 돌려준다.</summary>
    private TextMeshProUGUI LabelValue(RectTransform parent, string label, float x, float y, float w)
    {
        var l = Text(parent, label, x, y, w * 0.5f, 52f, 32f, GoldDim, TextAlignmentOptions.MidlineLeft);
        l.fontStyle = FontStyles.Bold;
        var v = Text(parent, "", x + w * 0.35f, y, w * 0.65f, 52f, 32f, Cream, TextAlignmentOptions.MidlineRight);
        v.fontStyle = FontStyles.Bold;
        return v;
    }

    /// <summary>아이콘 칸 뒤판: 어두운 판 + 옅은 금테 (주인공 스킬 칸과 같음).</summary>
    private static RectTransform SlotBack(RectTransform parent, float x, float y, float size)
    {
        var back = NewImage("SlotBack", parent, EmptySlot); Place(back.rectTransform, x, y, size, size);
        AddFrame(back.rectTransform, "SlotFrame", FrameDim, 2f);
        return back.rectTransform;
    }

    private TextMeshProUGUI Text(RectTransform parent, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (Font() != null) t.font = Font();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true; t.raycastTarget = false;
        Place(t.rectTransform, x, y, w, h);
        return t;
    }

    private Image IconSlot(RectTransform parent, float x, float y, float size)
    {
        var back = SlotBack(parent, x, y, size);
        var icon = NewImage("Icon", back, Color.white); Inset(icon.rectTransform, 4f); icon.preserveAspect = true;
        return icon;
    }

    private Button BigButton(RectTransform parent, string name, string label, float x, float y, float w, float h)
    {
        var frame = NewImage(name, parent, Gold); Place(frame.rectTransform, x, y, w, h);
        frame.raycastTarget = true;
        var bg = NewImage("Bg", frame.rectTransform, new Color(0.10f, 0.08f, 0.05f, 1f)); Inset(bg.rectTransform, 4f);
        var b = frame.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        var cols = b.colors; cols.highlightedColor = new Color(1.3f, 1.2f, 1f, 1f); cols.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f); b.colors = cols;
        var t = Text(frame.rectTransform, label, 0f, 0f, w, h, 36f, Gold, TextAlignmentOptions.Center);
        t.textWrappingMode = TextWrappingModes.Normal;
        ButtonSkin.Apply(b);              // [2026-10-17] 금테 패널 배경 (안쪽 Bg 에 입힘)
        frame.color = new Color(0f, 0f, 0f, 0f); // 바깥 금색 사각형은 숨김 (클릭 영역은 그대로)
        Inset(bg.rectTransform, 0f);
        return b;
    }
}
