using System.Text;
using Abyssdawn;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-10-22] 동료 명단 — 드퀘5 "마차" 화면처럼.
///  - 왼쪽: 대기 동료(후보) 목록 — 이름 · 레벨 · HP 만 간단히
///  - 오른쪽: 고른 동료의 상세 능력치
///  - 아래 "교체": 화면 아래 캐릭터 카드(현재 파티)를 골라 맞바꾼다. 파티에 빈 칸이 있으면 "파티에 넣기"로 바로 들어간다.
/// 메뉴 줄 "동료 명단" 버튼이 연다 (BattleLogView.AttachCompanionListButton). 바깥·닫기·ESC 로 닫는다.
/// </summary>
public class CompanionRosterView : MonoBehaviour
{
    private const int SortingOrder = 95;
    private static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);
    private static readonly Color GoldLine = new Color(0.86f, 0.74f, 0.45f, 0.95f);
    private static readonly Color Cream = new Color(0.94f, 0.92f, 0.86f, 1f);
    private static readonly Color Muted = new Color(0.55f, 0.53f, 0.48f, 1f);
    private static readonly Color Label = new Color(0.72f, 0.65f, 0.48f, 1f);

    private static CompanionRosterView _instance;
    private GameObject _root;
    private TMP_FontAsset _font;
    // 왼쪽 목록 줄: 0~2 = 파티 동료(ActiveRoster 0~2), 3~6 = 대기 동료(WaitlistPaths 0~3)
    private const int MainRows = CompanionPartyPersistence.MaxActive;
    private const int RowCount = CompanionPartyPersistence.MaxActive + CompanionPartyPersistence.MaxWaitlist;
    private readonly Image[] _rowBg = new Image[RowCount];
    private readonly TextMeshProUGUI[] _rowText = new TextMeshProUGUI[RowCount];
    private readonly TextMeshProUGUI[] _rowCursor = new TextMeshProUGUI[RowCount];
    private int _pickWaitFor = -1; // 파티 동료를 대기로 보낼 때: 그 동료의 ActiveRoster 인덱스 (대기 자리를 고르는 중)
    private int _pickMainFor = -1; // [2026-10-22] 대기 동료를 파티로 넣을 때: 그 동료의 WaitlistPaths 인덱스 (파티 자리를 고르는 중)
    private TextMeshProUGUI _detailTitle, _detailBody, _swapLabel, _hint;
    private Button _swapButton;
    private int _selected;

    public static void Toggle()
    {
        if (_instance != null && _instance._root != null && _instance._root.activeSelf) { Hide(); return; }
        Show();
    }

    public static void Show()
    {
        if (_instance == null)
        {
            var go = new GameObject("[CompanionRosterView]");
            _instance = go.AddComponent<CompanionRosterView>();
            _instance.Build();
        }
        _instance.Open();
    }

    public static void Hide()
    {
        if (_instance == null || _instance._root == null) return;
        _instance._root.SetActive(false);
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.UnlockInput(_instance);
    }

    private void Update()
    {
        if (_root != null && _root.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

    // ─────────────────────────────────────────

    private void Build()
    {
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.font != null && t.font.name.Contains("NotoSansKR")) { _font = t.font; break; }

        var canvasGo = new GameObject("CompanionRosterCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 어두운 바탕 (화면 아래 카드 줄은 보이게 위쪽만) — 누르면 닫힘
        var dim = NewImage((RectTransform)canvasGo.transform, "Dim", new Color(0f, 0f, 0f, 0.6f), true);
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<ClickToClose>();
        _root = dim.gameObject;

        // 창 — 카드 줄·메뉴 줄 위
        var panel = NewImage(dim.rectTransform, "Panel", new Color(0f, 0f, 0f, 1f), true); // 불투명 (선형 색공간이라 0.95 도 뒤가 비친다)
        Frame(panel.rectTransform, GoldLine, 3f);
        Frame(panel.rectTransform, new Color(0.86f, 0.74f, 0.45f, 0.30f), -8f);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.sizeDelta = new Vector2(1010f, 1300f); // [2026-10-22] 1060 → 1300 (위로 — 무기·스킬이 늘어날 자리)
        prt.anchoredPosition = new Vector2(0f, 470f);

        var title = NewText(prt, "Title", 42f, FontStyles.Bold, TextAlignmentOptions.Center, Gold);
        title.text = "동료 명단";
        Place(title.rectTransform, 0f, 22f, 1010f, 56f, new Vector2(0.5f, 1f));
        var line = NewImage(prt, "Divider", new Color(0.96f, 0.87f, 0.58f, 0.75f), false);
        Place(line.rectTransform, 0f, 86f, 640f, 3f, new Vector2(0.5f, 1f));

        // ── 왼쪽: 파티 동료 3 · 구분선 · 대기 동료 4 ──
        const float leftX = 30f, leftW = 330f, top = 110f;
        var listBox = NewImage(prt, "List", new Color(0f, 0f, 0f, 0f), false);
        PlaceTL(listBox.rectTransform, leftX, top, leftW, 1030f);
        Frame(listBox.rectTransform, GoldLine, 0f);
        var headMain = NewText(listBox.rectTransform, "HeadMain", 26f, FontStyles.Bold, TextAlignmentOptions.Center, Label);
        headMain.text = "파티 동료";
        PlaceTL(headMain.rectTransform, 0f, 8f, leftW, 36f);
        var sep = NewImage(listBox.rectTransform, "Separator", new Color(0.86f, 0.74f, 0.45f, 0.55f), false);
        PlaceTL(sep.rectTransform, 16f, 306f, leftW - 32f, 2f);
        var headWait = NewText(listBox.rectTransform, "HeadWait", 26f, FontStyles.Bold, TextAlignmentOptions.Center, Label);
        headWait.text = "대기 동료";
        PlaceTL(headWait.rectTransform, 0f, 314f, leftW, 36f);
        for (int i = 0; i < RowCount; i++)
        {
            int idx = i;
            float y = i < MainRows ? 46f + i * 84f : 354f + (i - MainRows) * 84f;
            var bg = NewImage(listBox.rectTransform, $"Row{i}", new Color(0f, 0f, 0f, 0f), true);
            PlaceTL(bg.rectTransform, 10f, y, leftW - 20f, 80f);
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => OnRowClicked(idx));
            _rowBg[i] = bg;
            var cur = NewText(bg.rectTransform, "Cursor", 26f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Gold);
            cur.text = "▶";
            PlaceTL(cur.rectTransform, 4f, 0f, 30f, 80f);
            _rowCursor[i] = cur;
            var txt = NewText(bg.rectTransform, "Text", 27f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Cream);
            txt.lineSpacing = -4f;
            PlaceTL(txt.rectTransform, 34f, 0f, leftW - 60f, 80f);
            _rowText[i] = txt;
        }

        // ── 오른쪽: 상세 능력치 ──
        const float rightX = leftX + leftW + 20f;
        float rightW = 1010f - rightX - 30f;
        var detail = NewImage(prt, "Detail", new Color(0f, 0f, 0f, 0f), false);
        PlaceTL(detail.rectTransform, rightX, top, rightW, 1030f);
        Frame(detail.rectTransform, GoldLine, 0f);
        _detailTitle = NewText(detail.rectTransform, "Name", 36f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Gold);
        PlaceTL(_detailTitle.rectTransform, 28f, 14f, rightW - 56f, 56f);
        _detailBody = NewText(detail.rectTransform, "Stats", 29f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Cream);
        _detailBody.lineSpacing = 22f;
        PlaceTL(_detailBody.rectTransform, 28f, 80f, rightW - 56f, 940f);

        // ── 아래: 교체 · 닫기 ──
        _swapButton = MakeButton(prt, "Swap", "교체", -170f, out _swapLabel);
        _swapButton.onClick.AddListener(OnSwap);
        MakeButton(prt, "Close", "닫기", 170f, out _).onClick.AddListener(Hide);
        _hint = NewText(prt, "Hint", 24f, FontStyles.Normal, TextAlignmentOptions.Center, Muted);
        Place(_hint.rectTransform, 0f, 1150f, 960f, 32f, new Vector2(0.5f, 1f));

        _root.SetActive(false);
    }

    private void Open()
    {
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        _pickWaitFor = -1; _pickMainFor = -1;
        // 처음엔 대기 동료 → 없으면 파티 동료 중 첫 번째를 고른 채로
        if (Occupied(_selected) == false)
        {
            _selected = 0;
            for (int i = MainRows; i < RowCount; i++) if (Occupied(i)) { _selected = i; break; }
            if (!Occupied(_selected)) for (int i = 0; i < MainRows; i++) if (Occupied(i)) { _selected = i; break; }
        }
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
        Refresh();
    }

    private static bool IsMainRow(int row) => row >= 0 && row < MainRows;
    private static int WaitIndex(int row) => row - MainRows;

    private static CompanionPartyPersistence.Entry MainEntry(int row)
    {
        var r = CompanionPartyPersistence.ActiveRoster;
        return IsMainRow(row) && row < r.Count ? r[row] : null;
    }

    private static CompanionPartyPersistence.WaitEntry WaitEntryAt(int row)
    {
        var w = CompanionPartyPersistence.WaitlistPaths;
        int wi = WaitIndex(row);
        return !IsMainRow(row) && wi >= 0 && wi < w.Count ? w[wi] : null;
    }

    private static bool Occupied(int row) => row >= 0 && row < RowCount && (IsMainRow(row) ? MainEntry(row) != null : WaitEntryAt(row) != null);

    private void OnRowClicked(int row)
    {
        // 파티 동료를 대기로 보내는 중 → 대기 칸(빈 칸 포함)을 누르면 그 자리로
        if (_pickWaitFor >= 0 && !IsMainRow(row))
        {
            int ri = _pickWaitFor, wi = WaitIndex(row);
            _pickWaitFor = -1;
            var outE = MainEntry(ri);
            var inW = WaitEntryAt(row);
            string outName = outE != null ? CompanionPartyPersistence.LoadCompanion(outE.resourcePath)?.MonsterName ?? "동료" : "동료";
            string inName = inW != null ? CompanionPartyPersistence.LoadCompanion(inW.resourcePath)?.MonsterName : null;
            if (CompanionPartyPersistence.SwapActiveWait(ri, wi))
            {
                Toast(inName != null ? $"{Ko.Eun(outName)} 대기로 가고, {Ko.I(inName)} 파티에 들어왔다." : $"{Ko.Eun(outName)} 대기로 갔다.");
                PlayLog.Event("동료", inName != null ? $"교체: {inName} ↔ {outName}" : $"{outName} 대기로");
            }
            _selected = row;
            AfterChange();
            return;
        }
        // [2026-10-22] 대기 동료를 파티로 넣는 중 → 파티 칸(빈 칸 포함)을 누르면 그 자리로 (동료가 있으면 맞바꿈)
        if (_pickMainFor >= 0 && IsMainRow(row))
        {
            int wi = _pickMainFor, ri = row;
            _pickMainFor = -1;
            var inW = CompanionPartyPersistence.WaitlistPaths[wi];
            var outE = MainEntry(ri);
            string inName = inW != null ? CompanionPartyPersistence.LoadCompanion(inW.resourcePath)?.MonsterName ?? "동료" : "동료";
            string outName = outE != null ? CompanionPartyPersistence.LoadCompanion(outE.resourcePath)?.MonsterName ?? "동료" : null;
            int slotNo = CompanionPartyPersistence.RosterIndexToMainLineSlot(ri) + 1;
            if (CompanionPartyPersistence.SwapActiveWait(ri, wi))
            {
                Toast(outName != null ? $"{Ko.I(inName)} No.{slotNo}에 들어오고, {Ko.Eun(outName)} 대기로 갔다." : $"{Ko.I(inName)} No.{slotNo}에 들어왔다.");
                PlayLog.Event("동료", outName != null ? $"교체: {inName} ↔ {outName} (No.{slotNo})" : $"{inName} 파티 No.{slotNo}에 넣음");
            }
            _selected = row;
            AfterChange();
            return;
        }
        _pickWaitFor = -1; _pickMainFor = -1;
        _selected = row;
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < RowCount; i++)
        {
            bool main = IsMainRow(i);
            MonsterSO so = null; int hp = 0, maxHp = 0;
            if (main) { var e = MainEntry(i); so = e != null ? CompanionPartyPersistence.LoadCompanion(e.resourcePath) : null; if (so != null) { hp = Mathf.Clamp(e.currentHP, 0, so.HP); maxHp = so.HP; } }
            else { var e = WaitEntryAt(i); so = e != null ? CompanionPartyPersistence.LoadCompanion(e.resourcePath) : null; if (so != null) { hp = e.HPOr(so.HP); maxHp = so.HP; } }
            bool on = i == _selected;
            bool target = (_pickWaitFor >= 0 && !main) || (_pickMainFor >= 0 && main); // 고르는 중인 쪽 칸 전부 금빛
            _rowBg[i].color = new Color(0f, 0f, 0f, 0f); // [2026-10-22] 황금색 바탕 없음 — 고른 줄은 ▶, 고를 수 있는 줄은 ▷ 커서만
            _rowCursor[i].enabled = on || target;
            _rowCursor[i].text = target ? "▷" : "▶";
            if (so == null) { _rowText[i].text = target ? "<color=#F5DE94>— 빈 자리 —</color>" : "<color=#666666>— 빈 자리 —</color>"; continue; }
            string hpCol = hp <= 0 ? "#FF6B6B" : "#DDDDDD";
            string no = main ? $"<color=#B8A57A>No.{CompanionPartyPersistence.RosterIndexToMainLineSlot(i) + 1}</color> " : "";
            _rowText[i].text = $"{no}{so.MonsterName}\n<size=80%><color=#F5DE94>Lv{so.MonsterLevel}</color>   <color={hpCol}>HP {hp}/{maxHp}</color></size>";
        }

        bool selMain = IsMainRow(_selected);
        var selMainE = MainEntry(_selected);
        var selWaitE = WaitEntryAt(_selected);
        string path = selMain ? selMainE?.resourcePath : selWaitE?.resourcePath;
        var sso = !string.IsNullOrEmpty(path) ? CompanionPartyPersistence.LoadCompanion(path) : null;
        if (sso == null)
        {
            _detailTitle.text = "";
            _detailBody.text = "<color=#777777>빈 자리.\n\n<size=85%>전투 후 몬스터를 영입하면\n파티가 가득 찼을 때 대기로 온다.</size></color>";
            _swapButton.interactable = false;
            _swapLabel.color = Muted;
            _swapLabel.text = "교체";
            _hint.text = !string.IsNullOrEmpty(_notice) ? $"<color=#FFE6A0>{_notice}</color>" : "";
            _notice = null;
            return;
        }

        int curHp = selMain ? Mathf.Clamp(selMainE.currentHP, 0, sso.HP) : selWaitE.HPOr(sso.HP);
        int curMp = selMain ? Mathf.Clamp(selMainE.currentMP, 0, sso.MP) : selWaitE.MPOr(sso.MP);
        _detailTitle.text = sso.MonsterName + (selMain ? "  <size=70%><color=#B8A57A>파티</color></size>" : "  <size=70%><color=#888888>대기</color></size>");
        var wpn = sso.EquippedWeapon;
        int wAtk = wpn != null ? wpn.attackBonus : 0, wAgi = wpn != null ? wpn.agiBonus : 0;
        var sb = new StringBuilder();
        Row(sb, "레벨", sso.MonsterLevel.ToString());
        Row(sb, "종족", StatDetailPopup.RaceName(sso.Race));
        Row(sb, "등급", StatDetailPopup.TypeName(sso.Type));
        Row(sb, "HP", $"{curHp} / {sso.HP}");
        Row(sb, "MP", $"{curMp} / {sso.MP}");
        Row(sb, "힘", Bonus(sso.ATK + wAtk, wAtk));
        Row(sb, "방어", sso.DEF.ToString());
        Row(sb, "마력", sso.MAG.ToString());
        Row(sb, "민첩", Bonus(sso.AGI + wAgi, wAgi));
        Row(sb, "행운", sso.LUK.ToString());
        Row(sb, "성격", StatDetailPopup.AiName(sso.AIPattern));
        Row(sb, "본능", sso.BasicAttackOverride != null ? sso.BasicAttackOverride.skillName : "—");
        Row(sb, "무기", wpn != null ? wpn.itemName : "—");
        var skills = new StringBuilder();
        if (sso.ActiveSkills != null)
            foreach (var s in sso.ActiveSkills) if (s != null) skills.Append(skills.Length > 0 ? ", " : "").Append(s.skillName);
        sb.Append("\n<color=#B8A57A>스킬</color>\n").Append(skills.Length > 0 ? skills.ToString() : "—");
        _detailBody.text = sb.ToString();

        _swapButton.interactable = true;
        _swapLabel.color = Color.white;
        string hint;
        if (selMain)
        {
            _swapLabel.text = _pickWaitFor >= 0 ? "그만두기" : "대기로";
            hint = _pickWaitFor >= 0 ? "왼쪽 대기 동료 칸을 고른다 — 빈 자리면 그냥 대기로 간다." : "대기로: 대기 동료와 바꾸거나 빈 대기 자리로 보낸다.";
        }
        else
        {
            _swapLabel.text = _pickMainFor >= 0 ? "그만두기" : "파티로";
            hint = _pickMainFor >= 0 ? "왼쪽 파티 동료 칸을 고른다 — 빈 자리면 그 자리로, 동료가 있으면 맞바꾼다." : "파티로: 왼쪽 파티 자리를 골라 넣는다.";
        }
        _hint.text = !string.IsNullOrEmpty(_notice) ? $"<color=#FFE6A0>{_notice}</color>" : hint;
        _notice = null;
    }

    private static int FirstEmptyRosterIndex()
    {
        var r = CompanionPartyPersistence.ActiveRoster;
        for (int i = 0; i < CompanionPartyPersistence.MaxActive; i++)
            if (i >= r.Count || r[i] == null) return i;
        return -1;
    }

    private void OnSwap()
    {
        // 파티 동료 → 대기 자리 고르기 (다시 누르면 그만두기)
        if (IsMainRow(_selected))
        {
            if (MainEntry(_selected) == null) return;
            _pickMainFor = -1;
            _pickWaitFor = _pickWaitFor >= 0 ? -1 : _selected;
            Refresh();
            return;
        }

        int wi = WaitIndex(_selected);
        var w = CompanionPartyPersistence.WaitlistPaths;
        if (wi < 0 || wi >= w.Count || w[wi] == null) return;
        // [2026-10-22] 명단을 닫지 않고, 왼쪽 파티 칸을 눌러 넣는다 (다시 누르면 그만두기)
        _pickWaitFor = -1;
        _pickMainFor = _pickMainFor >= 0 ? -1 : wi;
        Refresh();
    }

    private void AfterChange()
    {
        if (DungeonPartyBar.Instance != null) DungeonPartyBar.Instance.Refresh();
        Refresh();
    }

    /// <summary>결과 알림 — 창 아래 안내 줄에 (토스트는 이 창 뒤에 가려진다).</summary>
    private string _notice;
    private void Toast(string msg) { _notice = msg; }

    private static void Row(StringBuilder sb, string label, string value)
    {
        sb.Append("<color=#B8A57A>").Append(label).Append("</color><pos=34%>").Append(value).Append('\n');
    }

    private static string Bonus(int value, int bonus) => bonus != 0 ? $"{value} <color=#8CFF8C>({bonus:+#;-#;0})</color>" : value.ToString();

    // ─────────────────────────────────────────

    private Button MakeButton(RectTransform parent, string name, string label, float x, out TextMeshProUGUI text)
    {
        var bg = NewImage(parent, name, Color.white, true);
        Sprite frame = DungeonHud.PopupSprite;
        if (frame != null) { bg.sprite = frame; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 3f; }
        else bg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 68f);
        rt.sizeDelta = new Vector2(280f, 76f);
        var b = bg.gameObject.AddComponent<Button>();
        text = NewText(rt, "Label", 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        text.text = label;
        Stretch(text.rectTransform);
        return b;
    }

    private Image NewImage(RectTransform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TextMeshProUGUI NewText(RectTransform parent, string name, float size, FontStyles style, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = color;
        t.richText = true; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    /// <summary>금테 선 (GlowBorder 9분할, 가운데 비움). outset 음수면 안쪽.</summary>
    private static void Frame(RectTransform parent, Color color, float outset)
    {
        var sp = Resources.Load<Sprite>("UI/GlowBorder");
        if (sp == null) return;
        var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sp; img.type = Image.Type.Sliced; img.fillCenter = false; img.color = color; img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset); rt.offsetMax = new Vector2(outset, outset);
    }

    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    /// <summary>부모의 위쪽 가운데 기준 (x = 가운데로부터, y = 위에서 아래로).</summary>
    private static void Place(RectTransform rt, float x, float y, float w, float h, Vector2 anchor)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>부모의 왼쪽 위 기준.</summary>
    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
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
