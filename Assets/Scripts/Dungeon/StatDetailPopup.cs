using System.Collections.Generic;
using Abyssdawn;
using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-10-22] 상태창의 "상세" 버튼으로 여는 능력치 상세 페이지 (주인공 · 동료 공용).
///  - 제목 + 금색 구분선, 아래로 칸(기본 정보 · 능력치 · 전투 수치)을 쌓는다. 각 칸은 두 줄 기둥으로 "이름 … 값" 을 나열
///  - 값은 전투 계산식(BattleManager)과 같은 식으로 지금 상태에서 계산한다
///  - InfoDetailPopup 과 같은 모양: 검은 반투명 + 금테 이중선, 바깥/닫기/ESC 로 닫힘
/// </summary>
public class StatDetailPopup : MonoBehaviour
{
    private const int SortingOrder = 95; // 던전 HUD 오버레이(90)·상태창보다 위
    private const float PanelW = 980f, RowH = 50f, ColGap = 40f;

    private static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);
    private static readonly Color GoldDim = new Color(0.72f, 0.64f, 0.45f, 1f);

    public class Section
    {
        public string header;
        public readonly List<(string label, string value)> rows = new List<(string, string)>();
        public Section(string header) { this.header = header; }
        public Section Add(string label, string value) { rows.Add((label, value)); return this; }
    }

    private static StatDetailPopup _instance;
    private GameObject _root;
    private RectTransform _panel, _content;
    private TextMeshProUGUI _title, _subtitle;
    private TMP_FontAsset _font;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.activeSelf;

    public static void Show(string title, string subtitle, List<Section> sections)
    {
        if (_instance == null)
        {
            var go = new GameObject("[StatDetailPopup]");
            _instance = go.AddComponent<StatDetailPopup>();
            _instance.Build();
        }
        _instance.Fill(title, subtitle, sections);
    }

    public static void Hide()
    {
        if (_instance != null && _instance._root != null) _instance._root.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

    // ─────────────────────────────────────────
    // 주인공 / 동료 내용
    // ─────────────────────────────────────────

    public static void ShowHero(PlayerStats p)
    {
        if (p == null) return;
        var info = new Section("기본 정보")
            .Add("직업", Ko.Job(p.jobClass))
            .Add("레벨", p.level.ToString())
            .Add("경험치", $"{p.exp} / {p.maxExp}")
            .Add("스킬 포인트", p.skillPoints.ToString())
            .Add("능력치 포인트", p.FreeStatPoints.ToString());

        var stats = new Section("능력치")
            .Add("최대 HP", WithBonus(p.maxHP, p.GetHPBonus()))
            .Add("최대 MP", WithBonus(p.maxMP, p.GetMPBonus()))
            .Add("힘 (STR)", WithBonus(p.Attack, p.GetAttackBonus()))
            .Add("방어 (DEF)", WithBonus(p.Defense, p.GetDefenseBonus()))
            .Add("마력 (MAG)", WithBonus(p.Magic, p.GetMagicBonus()))
            .Add("마법 방어", p.MagicDefense.ToString())
            .Add("민첩 (AGI)", WithBonus(p.Agility, p.GetAgilityBonus()))
            .Add("행운 (LUK)", WithBonus(p.Luck, p.GetLuckBonus()));

        // 전투 수치 — BattleManager 의 CheckCritical · ComputeHitChanceCore · 회피 계산과 같은 항목
        bool hero = Disposition.IsHero(p);
        float crit = BattleManager.DefaultCriticalChance + p.Luck
                   + (hero ? Disposition.CritBonus : 0f) + p.GetMemoryCritBonus() + p.GetClassCritBonus()
                   + p.ApplyStatModifiers(ModStatType.CritChance, 0f);
        float acc = p.Luck * 0.002f + p.GetPassiveAccuracyBonus() + p.GetClassWeaponAccuracyBonus(null) + p.GetOathAccuracyBonus(null)
                  - p.GetClassAccuracyPenalty() + (hero ? Disposition.AccuracyBonus - Disposition.TrapAccuracyPenalty : 0f)
                  + p.GetEquipmentAccuracyBonus();
        float eva = Mathf.Clamp(p.ApplyStatModifiers(ModStatType.Evasion, 0f) + p.GetPassiveEvasionBonus()
                  + (hero ? Disposition.EvasionBonus : 0f) - p.GetClassEvasionPenalty(), 0f, 0.8f);
        var combat = new Section("전투 수치")
            .Add("치명타 확률", $"{crit:0.#}%")
            .Add("명중 보정", Pct(acc))
            .Add("회피", $"{eva * 100f:0.#}%")
            .Add("방어 관통", $"{Mathf.Clamp01(p.ApplyStatModifiers(ModStatType.ArmorPen, 0f)) * 100f:0.#}%")
            .Add("받는 피해", $"×{p.ApplyStatModifiers(ModStatType.DamageTaken, 1f):0.##}")
            .Add("받는 회복", $"×{p.ApplyStatModifiers(ModStatType.HealingReceived, 1f):0.##}")
            .Add("마법 증폭", $"×{p.MagicAmplify:0.##}")
            .Add("역류 억제", $"{p.TotalBackflowSuppression * 100f:0.#}%");

        Show(p.playerName, $"Lv {p.level}  ·  {Ko.Job(p.jobClass)}", new List<Section> { info, stats, combat });
    }

    public static void ShowCompanion(MonsterSO so, int currentHP, int currentMP)
    {
        if (so == null) return;
        var w = so.EquippedWeapon;
        int wAtk = w != null ? w.attackBonus : 0, wAgi = w != null ? w.agiBonus : 0;
        var info = new Section("기본 정보")
            .Add("종족", RaceName(so.Race))
            .Add("등급", TypeName(so.Type))
            .Add("레벨", so.MonsterLevel.ToString())
            .Add("본능", so.BasicAttackOverride != null ? so.BasicAttackOverride.skillName : "—")
            .Add("무기", w != null ? w.itemName : "—");

        var stats = new Section("능력치")
            .Add("HP", $"{currentHP} / {so.HP}")
            .Add("MP", $"{currentMP} / {so.MP}")
            .Add("힘 (STR)", WithBonus(so.ATK + wAtk, wAtk))
            .Add("방어 (DEF)", so.DEF.ToString())
            .Add("마력 (MAG)", so.MAG.ToString())
            .Add("민첩 (AGI)", WithBonus(so.AGI + wAgi, wAgi))
            .Add("행운 (LUK)", so.LUK.ToString());

        var combat = new Section("전투 수치")
            .Add("치명타 확률", $"{BattleManager.DefaultCriticalChance + so.LUK:0.#}%")
            .Add("명중 보정", Pct(so.LUK * 0.002f + (w != null ? w.accuracyBonus : 0f)))
            .Add("액티브 스킬", $"{CountNonNull(so.ActiveSkills)}개")
            .Add("패시브 스킬", $"{CountNonNull(so.PassiveSkills)}개");

        Show(so.MonsterName, $"Lv {so.MonsterLevel}  ·  동료", new List<Section> { info, stats, combat });
    }

    private static string WithBonus(int value, int bonus) =>
        bonus != 0 ? $"{value}<color=#B8A57A>({bonus:+#;-#;0})</color>" : value.ToString();

    private static string Pct(float v) => $"{(v >= 0f ? "+" : "")}{v * 100f:0.#}%p";

    private static int CountNonNull<T>(IReadOnlyList<T> list) where T : Object
    {
        int n = 0;
        if (list != null) foreach (var x in list) if (x != null) n++;
        return n;
    }

    public static string RaceName(MonsterRace r)
    {
        switch (r)
        {
            case MonsterRace.Beast: return "야수";
            case MonsterRace.Undead: return "언데드";
            case MonsterRace.Elemental: return "정령";
            case MonsterRace.Humanoid: return "인간형";
            case MonsterRace.DemiHuman: return "아인";
            case MonsterRace.DemonicBeast: return "마수";
            case MonsterRace.Demon: return "악마";
            case MonsterRace.Dragonkin: return "용족";
            case MonsterRace.Vermin: return "벌레";
            case MonsterRace.Plant: return "식물";
            case MonsterRace.Construct: return "구조물";
            default: return "—";
        }
    }

    public static string TypeName(MonsterType t) => t == MonsterType.Boss ? "보스" : t == MonsterType.Elite ? "정예" : "일반";
    public static string RowName(MonsterRowPreference r) => r == MonsterRowPreference.Front ? "전열" : r == MonsterRowPreference.Back ? "후열" : "어디든";
    public static string AiName(AIPattern a) =>
        a == AIPattern.Defensive ? "신중함" : a == AIPattern.Support ? "보조" : a == AIPattern.Cunning ? "교활함" : "공격적";

    // ─────────────────────────────────────────
    // 만들기
    // ─────────────────────────────────────────

    private void Build()
    {
        _font = FindKoreanFont();
        var canvasGo = new GameObject("StatDetailCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 리니어 색 공간이라 95% 검정도 뒤 글자가 비쳐 읽힌다 — 상세 페이지는 글이 많아 바닥을 더 진하게
        Image dim = NewImage((RectTransform)canvasGo.transform, "Dim", new Color(0f, 0f, 0f, 0.75f), true);
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<ClickToClose>();
        _root = dim.gameObject;

        Image panel = NewImage(dim.rectTransform, "Panel", new Color(0f, 0f, 0f, 0.99f), true);
        AddGoldFrame(panel.rectTransform, "Frame", new Color(0.86f, 0.74f, 0.45f, 0.95f), 3f);
        AddGoldFrame(panel.rectTransform, "FrameInner", new Color(0.86f, 0.74f, 0.45f, 0.30f), -8f);
        _panel = panel.rectTransform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);

        _title = NewText(_panel, "Title", 46f, FontStyles.Bold, TextAlignmentOptions.Center, Gold);
        PlaceTop(_title.rectTransform, 0f, 34f, PanelW, 60f);
        _subtitle = NewText(_panel, "Subtitle", 28f, FontStyles.Normal, TextAlignmentOptions.Center, GoldDim);
        PlaceTop(_subtitle.rectTransform, 0f, 94f, PanelW, 40f);
        Image line = NewImage(_panel, "Divider", new Color(Gold.r, Gold.g, Gold.b, 0.75f), false);
        PlaceTop(line.rectTransform, (PanelW - 700f) * 0.5f, 144f, 700f, 3f);

        _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        _content.SetParent(_panel, false);
        Stretch(_content);

        Image closeBg = NewImage(_panel, "Close", Color.white, true);
        Sprite frame = DungeonHud.PopupSprite;
        if (frame != null) { closeBg.sprite = frame; closeBg.type = Image.Type.Sliced; closeBg.pixelsPerUnitMultiplier = 3f; }
        else closeBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        var crt = closeBg.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0f, -56f); crt.sizeDelta = new Vector2(220f, 72f);
        closeBg.gameObject.AddComponent<Button>().onClick.AddListener(Hide);
        var closeLabel = NewText(crt, "Label", 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        closeLabel.text = "닫기";
        Stretch(closeLabel.rectTransform);

        _root.SetActive(false);
    }

    private void Fill(string title, string subtitle, List<Section> sections)
    {
        _title.text = title ?? "";
        _subtitle.text = subtitle ?? "";
        foreach (Transform c in _content) Destroy(c.gameObject);

        float y = 170f, colW = (PanelW - 2 * 60f - ColGap) * 0.5f;
        foreach (var s in sections)
        {
            if (s == null || s.rows.Count == 0) continue;
            var h = NewText(_content, "Header", 32f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Gold);
            h.text = s.header; h.characterSpacing = 2f;
            PlaceTop(h.rectTransform, 60f, y, PanelW - 120f, 46f);
            Image sub = NewImage(_content, "HeaderLine", new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.45f), false);
            PlaceTop(sub.rectTransform, 60f, y + 48f, PanelW - 120f, 2f);
            y += 60f;

            int perCol = Mathf.CeilToInt(s.rows.Count / 2f);
            for (int i = 0; i < s.rows.Count; i++)
            {
                int col = i / perCol, row = i % perCol;
                float x = 60f + col * (colW + ColGap), ry = y + row * RowH;
                var l = NewText(_content, "Label", 30f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, GoldDim);
                l.text = s.rows[i].label;
                PlaceTop(l.rectTransform, x, ry, colW * 0.6f, RowH);
                var v = NewText(_content, "Value", 30f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, Color.white);
                v.text = s.rows[i].value;
                PlaceTop(v.rectTransform, x + colW * 0.4f, ry, colW * 0.6f, RowH);
            }
            y += perCol * RowH + 30f;
        }
        _panel.sizeDelta = new Vector2(PanelW, y + 20f);

        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
    }

    // ─────────────────────────────────────────
    // 상태창에 붙이는 "상세" 버튼
    // ─────────────────────────────────────────

    /// <summary>parent 의 오른쪽 위 안쪽에 작은 "상세" 버튼을 만든다 (이미 있으면 그것을 돌려줌).</summary>
    public static Button AttachButton(RectTransform parent, System.Action onClick, float w = 110f, float h = 44f, float inset = 8f)
    {
        if (parent == null) return null;
        var existing = parent.Find("DetailButton");
        if (existing != null) return existing.GetComponent<Button>();
        var go = new GameObject("DetailButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-inset, -inset); rt.sizeDelta = new Vector2(w, h);
        var img = go.GetComponent<Image>(); img.color = new Color(0.10f, 0.08f, 0.05f, 1f);
        var b = go.GetComponent<Button>(); b.targetGraphic = img;
        b.onClick.AddListener(() => onClick?.Invoke());
        ButtonSkin.Apply(b);
        var t = NewText(rt, "Label", 26f, FontStyles.Bold, TextAlignmentOptions.Center, Gold, FindKoreanFont());
        t.text = "상세";
        Stretch(t.rectTransform);
        return b;
    }

    // ─────────────────────────────────────────
    // 작은 도우미
    // ─────────────────────────────────────────

    private static TMP_FontAsset FindKoreanFont()
    {
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.font != null && t.font.name.Contains("NotoSansKR")) return t.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static void AddGoldFrame(RectTransform parent, string name, Color color, float outset)
    {
        var sp = Resources.Load<Sprite>("UI/GlowBorder");
        if (sp == null) return;
        var img = NewImage(parent, name, color, false);
        img.sprite = sp; img.type = Image.Type.Sliced; img.fillCenter = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset); rt.offsetMax = new Vector2(outset, outset);
    }

    private static Image NewImage(RectTransform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TextMeshProUGUI NewText(RectTransform parent, string name, float size, FontStyles style, TextAlignmentOptions align, Color color) =>
        NewText(parent, name, size, style, align, color, _font);

    private static TextMeshProUGUI NewText(RectTransform parent, string name, float size, FontStyles style, TextAlignmentOptions align, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true; t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>부모 왼쪽 위 기준 (아래로 +).</summary>
    private static void PlaceTop(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }

    private class ClickToClose : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (e.pointerCurrentRaycast.gameObject == gameObject) Hide();
        }
    }
}
