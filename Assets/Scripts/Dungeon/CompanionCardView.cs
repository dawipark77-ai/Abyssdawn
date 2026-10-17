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
///  - 머리: 몬스터 그림 · 이름 · Lv · HP/MP 막대
///  - 본능(기본 공격) · 능력치 · 패시브/액티브 스킬 (아이콘을 누르면 상세)
///  - 아래: Position(자리 이동 — 칸을 골라 맞바꿈) · Release(풀어 주기 — 예/아니오 확인)
/// 카드 순서 = ActiveRoster 순서 (CompanionSlotView 와 같다).
/// </summary>
public class CompanionCardView : MonoBehaviour
{
    private static readonly Color Gold = new Color(0.86f, 0.74f, 0.47f);
    private static readonly Color GoldDim = new Color(0.62f, 0.52f, 0.33f);
    private static readonly Color Cream = new Color(0.93f, 0.90f, 0.82f);
    private static readonly Color Muted = new Color(0.6f, 0.58f, 0.54f);
    private static readonly Color PanelBg = new Color(0.05f, 0.04f, 0.03f, 0.96f);
    private static readonly Color HpColor = new Color(0.85f, 0.25f, 0.22f);
    private static readonly Color MpColor = new Color(0.30f, 0.55f, 0.95f);

    private static readonly string[] SlotNames = { "Companion_SLot2", "Companion_SLot3", "Companion_SLot4" };

    private class Card
    {
        public RectTransform root;
        public Image portrait;
        public TextMeshProUGUI name, level, hpText, mpText, mpLabel, instinctName, stats, emptyText;
        public GameObject emptyBox;
        public Image hpFill, mpFill, instinctIcon;
        public Image[] passives = new Image[3], actives = new Image[6];
        public GameObject body, head;
        public Button positionButton, releaseButton;
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
        float W = area.width, y = 0f;

        // ── 1) 머리: 그림 + 이름 + Lv + HP/MP 막대 ──
        var head = Panel(card.root, "Head", 0f, y, W, 300f); y += 300f + 16f;
        card.head = head.gameObject;
        var pframe = NewImage("PortraitFrame", head, Gold); Place(pframe.rectTransform, 22f, 22f, 256f, 256f);
        var pbg = NewImage("PortraitBg", pframe.rectTransform, new Color(0.08f, 0.07f, 0.06f, 1f)); Inset(pbg.rectTransform, 4f);
        card.portrait = NewImage("Portrait", pbg.rectTransform, Color.white); Inset(card.portrait.rectTransform, 8f); card.portrait.preserveAspect = true;
        float tx = 300f, tw = W - tx - 24f;
        card.name = Text(head, "", tx, 26f, tw, 64f, 50f, Gold, TextAlignmentOptions.MidlineLeft);
        card.level = Text(head, "", tx, 86f, tw, 40f, 30f, Muted, TextAlignmentOptions.MidlineLeft);
        Text(head, "HP", tx, 134f, 80f, 30f, 24f, Muted, TextAlignmentOptions.MidlineLeft);
        card.hpFill = Bar(head, tx, 166f, tw, 30f, HpColor, out card.hpText);
        card.mpLabel = Text(head, "MP", tx, 204f, 80f, 30f, 24f, Muted, TextAlignmentOptions.MidlineLeft);
        card.mpFill = Bar(head, tx, 236f, tw, 30f, MpColor, out card.mpText);

        // 동료가 없을 때 문구 (카드 가운데) — [2026-10-18] 뒤가 비치지 않게 다른 칸과 같은 어두운 상자 위에
        card.emptyBox = Panel(card.root, "EmptyBox", 0f, 0f, W, area.height).gameObject;
        card.emptyText = Text(card.root, "<color=#777777>빈 자리</color>\n<size=70%><color=#666666>전투 후 몬스터를 영입하세요.</color></size>", 0f, 0f, W, area.height, 44f, Muted, TextAlignmentOptions.Center);
        card.emptyText.textWrappingMode = TextWrappingModes.Normal;

        card.body = new GameObject("Body", typeof(RectTransform));
        card.body.transform.SetParent(card.root, false);
        var body = (RectTransform)card.body.transform; Stretch(body);

        // ── 2) 본능 + 능력치 ──
        var mid = Panel(body, "Stats", 0f, y, W, 300f); y += 300f + 16f;
        Text(mid, "Instinct", 24f, 18f, 260f, 40f, 28f, GoldDim, TextAlignmentOptions.MidlineLeft);
        var iframe = NewImage("InstinctFrame", mid, Gold); Place(iframe.rectTransform, 24f, 66f, 112f, 112f);
        card.instinctIcon = NewImage("Instinct", iframe.rectTransform, Color.white); Inset(card.instinctIcon.rectTransform, 4f); card.instinctIcon.preserveAspect = true;
        card.instinctName = Text(mid, "", 24f, 186f, 260f, 96f, 26f, Cream, TextAlignmentOptions.TopLeft);
        card.instinctName.textWrappingMode = TextWrappingModes.Normal;
        var line = NewImage("Divider", mid, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.5f)); Place(line.rectTransform, 300f, 24f, 2f, 252f);
        card.stats = Text(mid, "", 330f, 20f, W - 354f, 260f, 34f, Cream, TextAlignmentOptions.TopLeft);
        card.stats.lineSpacing = 12f;

        // ── 3) 스킬 ──
        float skillH = Mathf.Max(380f, area.height - y - 140f);
        var sk = Panel(body, "Skills", 0f, y, W, skillH); y += skillH + 16f;
        Text(sk, "패시브 스킬", 0f, 18f, W, 46f, 34f, Gold, TextAlignmentOptions.Center);
        float icon = 104f, gap = 18f;
        float px = (W - (3 * icon + 2 * gap)) * 0.5f;
        for (int k = 0; k < 3; k++) card.passives[k] = IconSlot(sk, px + k * (icon + gap), 74f, icon);
        Text(sk, "액티브 스킬", 0f, 74f + icon + 26f, W, 46f, 34f, Gold, TextAlignmentOptions.Center);
        float ax = (W - (6 * icon + 5 * gap)) * 0.5f;
        for (int k = 0; k < 6; k++) card.actives[k] = IconSlot(sk, ax + k * (icon + gap), 74f + icon + 82f, icon);

        // ── 4) 버튼 ──
        float bw = (W - 3 * 24f) * 0.5f;
        card.positionButton = BigButton(body, "Position", "자리 바꾸기", 24f, y, bw, 100f);
        card.releaseButton = BigButton(body, "Release", "놓아주기", 48f + bw, y, bw, 100f);
        int captured = index;
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
            c.portrait.transform.parent.parent.gameObject.SetActive(has);
            c.name.text = has ? so.MonsterName : "";
            c.level.text = has ? $"Lv {so.MonsterLevel}  ·  동료" : "";
            c.hpFill.transform.parent.gameObject.SetActive(has);
            c.mpFill.transform.parent.gameObject.SetActive(has && so.MP > 0);
            c.mpLabel.gameObject.SetActive(has && so.MP > 0);
            if (!has) { c.hpText.text = c.mpText.text = ""; continue; }

            c.portrait.sprite = so.Sprite;
            c.portrait.color = so.Sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            SetBar(c.hpFill, c.hpText, entry.currentHP, so.HP);
            SetBar(c.mpFill, c.mpText, entry.currentMP, so.MP);

            var instinct = so.BasicAttackOverride;
            c.instinctIcon.sprite = instinct != null ? instinct.skillIcon : null;
            c.instinctIcon.color = instinct != null && instinct.skillIcon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            c.instinctName.text = instinct != null ? instinct.skillName : "<color=#777777>—</color>";
            SkillData capturedInstinct = instinct;
            DetailClickTarget.Attach(c.instinctIcon, () => DetailClickTarget.ForSkill(capturedInstinct));

            c.stats.text =
                $"<color=#9E8A5E>STR</color>  {so.ATK}\n" +
                $"<color=#9E8A5E>DEF</color>  {so.DEF}\n" +
                $"<color=#9E8A5E>MAG</color>  {so.MAG}\n" +
                $"<color=#9E8A5E>AGI</color>  {so.AGI}\n" +
                $"<color=#9E8A5E>LUK</color>  {so.LUK}";

            for (int k = 0; k < c.actives.Length; k++)
            {
                SkillData s = so.ActiveSkills != null && k < so.ActiveSkills.Count ? so.ActiveSkills[k] : null;
                SetIcon(c.actives[k], s != null ? s.skillIcon : null);
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

    private static void SetBar(Image fill, TextMeshProUGUI text, int cur, int max)
    {
        float k = max > 0 ? Mathf.Clamp01(cur / (float)max) : 0f;
        var rt = fill.rectTransform; rt.anchorMax = new Vector2(k, 1f);
        text.text = $"{cur} / {max}";
    }

    private static void SetIcon(Image img, Sprite s)
    {
        img.sprite = s;
        img.color = s != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = s != null;
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

    /// <summary>어두운 바탕 + 옅은 금테 패널.</summary>
    private RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h)
    {
        var frame = NewImage(name, parent, new Color(Gold.r, Gold.g, Gold.b, 0.75f)); Place(frame.rectTransform, x, y, w, h);
        var bg = NewImage("Bg", frame.rectTransform, PanelBg); Inset(bg.rectTransform, 3f);
        return frame.rectTransform;
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

    private Image Bar(RectTransform parent, float x, float y, float w, float h, Color color, out TextMeshProUGUI label)
    {
        // [2026-10-17] 게이지 없이 수치만 (HP 12 / 23) — 막대는 투명, 글자를 크게
        var bg = NewImage("Bar", parent, new Color(0f, 0f, 0f, 0f)); Place(bg.rectTransform, x, y, w, h);
        var fill = NewImage("Fill", bg.rectTransform, new Color(0f, 0f, 0f, 0f));
        var frt = fill.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.pivot = new Vector2(0f, 0.5f); frt.offsetMin = frt.offsetMax = Vector2.zero;
        label = Text(bg.rectTransform, "", 0f, -4f, w, h + 8f, 34f, new Color(0.95f, 0.92f, 0.85f, 1f), TextAlignmentOptions.MidlineLeft);
        return fill;
    }

    private Image IconSlot(RectTransform parent, float x, float y, float size)
    {
        var frame = NewImage("SlotFrame", parent, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.8f)); Place(frame.rectTransform, x, y, size, size);
        var bg = NewImage("SlotBg", frame.rectTransform, new Color(0.03f, 0.03f, 0.03f, 1f)); Inset(bg.rectTransform, 3f);
        var icon = NewImage("Icon", bg.rectTransform, Color.white); Inset(icon.rectTransform, 4f); icon.preserveAspect = true;
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
