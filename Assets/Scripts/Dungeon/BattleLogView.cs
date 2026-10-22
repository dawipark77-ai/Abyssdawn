using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-10-22] 방금 싸운 전투의 전체 기록.
///  - BattleLogArchive: 전투 화면의 모든 메시지 줄을 모은다 (전투가 시작되면 새로 비움 → 늘 "마지막 전투").
///  - BattleLogView: 던전 메뉴 줄의 "전투 설정" 오른쪽에 "전투 기록" 버튼을 만들고, 누르면 기록 창(스크롤)을 연다.
/// 창 모양은 상세 페이지와 같다 (검은 반투명 + 금테 이중선, 바깥/닫기/ESC 로 닫힘).
/// </summary>
public static class BattleLogArchive
{
    private static readonly List<string> _lines = new List<string>();
    public static IReadOnlyList<string> Lines => _lines;
    public static string BattleTitle { get; private set; } = "";

    /// <summary>전투 시작 — 이전 기록을 지우고 새로 모은다.</summary>
    public static void Begin(string title)
    {
        _lines.Clear();
        BattleTitle = title ?? "";
        PlayLog.BattleStart(BattleTitle); // [2026-10-22]
    }

    public static void Add(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        _lines.Add(line); // 같은 글이라도 다른 동료일 수 있어 (쥐 두 마리 등) 거르지 않는다
        PlayLog.BattleLine(line); // [2026-10-22] 플레이 로그 — 전투 중 모든 행동
    }
}

public class BattleLogView : MonoBehaviour
{
    private const int SortingOrder = 95;
    private const string MenuPanelName = "MenuPanel";
    private const string BattleSetName = "BattleSet";

    private static BattleLogView _instance;
    private GameObject _root;
    private TextMeshProUGUI _title, _body;
    private ScrollRect _scroll;

    /// <summary>던전 메뉴 줄에 "전투 기록" 버튼을 붙인다 (MapManager.Start 에서 한 번).</summary>
    public static void AttachMenuButton()
    {
        GameObject battleSet = null;
        foreach (var t in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == BattleSetName && t.parent != null && t.parent.name == MenuPanelName) { battleSet = t.gameObject; break; }
        if (battleSet == null) { Debug.LogWarning("[BattleLogView] 'MenuPanel/BattleSet' 을 찾지 못해 전투 기록 버튼을 만들지 않았습니다."); return; }
        if (battleSet.transform.parent.Find("BattleLog") != null) return;

        var go = Instantiate(battleSet, battleSet.transform.parent, false);
        go.name = "BattleLog";
        var src = (RectTransform)battleSet.transform;
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = src.anchoredPosition + new Vector2(src.rect.width + 4f, 0f);
        foreach (var b in go.GetComponentsInChildren<Button>(true))
        {
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(Show);
        }
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.text = "전투 기록";
        // 이 버튼이 다른 창을 켜는 스크립트를 복제해 오지 않게 (메뉴 토글 등)
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is Graphic) && !(mb is Selectable) && !(mb is UIBehaviour)) Destroy(mb);
        Debug.Log("[BattleLogView] '전투 기록' 버튼 추가");
        AttachCompanionListButton(go);
    }

    /// <summary>
    /// [2026-10-22] "전투 기록" 옆 "동료 명단" — 드퀘5식 대기 동료(후보) 명단 (CompanionRosterView).
    /// 예전 후보 화면(Canvas/Tactics)은 StatusMenuPanel 의 "전술" 버튼으로만 열렸는데, 그 메뉴가 숨겨져 들어갈 길이 없었다.
    /// </summary>
    private static void AttachCompanionListButton(GameObject battleLogButton)
    {
        if (battleLogButton.transform.parent.Find("CompanionList") != null) return;
        var go = Instantiate(battleLogButton, battleLogButton.transform.parent, false);
        go.name = "CompanionList";
        var src = (RectTransform)battleLogButton.transform;
        ((RectTransform)go.transform).anchoredPosition = src.anchoredPosition + new Vector2(src.rect.width + 4f, 0f);
        foreach (var b in go.GetComponentsInChildren<Button>(true))
        {
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(CompanionRosterView.Toggle); // [2026-10-22] 드퀘5식 명단 (옛 Tactics 화면 대신)
        }
        foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.text = "동료 명단";
        // [2026-10-22] 순서: … 전투 설정 · 동료 명단 · 전투 기록 (전투 기록이 맨 오른쪽)
        var logRt = (RectTransform)battleLogButton.transform;
        var listRt = (RectTransform)go.transform;
        Vector2 p = logRt.anchoredPosition; logRt.anchoredPosition = listRt.anchoredPosition; listRt.anchoredPosition = p;
        Debug.Log("[BattleLogView] '동료 명단' 버튼 추가");
    }

    public static void Show()
    {
        if (_instance == null)
        {
            var go = new GameObject("[BattleLogView]");
            _instance = go.AddComponent<BattleLogView>();
            _instance.Build();
        }
        _instance.Fill();
    }

    public static void Hide()
    {
        if (_instance != null && _instance._root != null) _instance._root.SetActive(false);
    }

    private void Update()
    {
        if (_root != null && _root.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

    private void Build()
    {
        TMP_FontAsset font = null;
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.font != null && t.font.name.Contains("NotoSansKR")) { font = t.font; break; }

        var canvasGo = new GameObject("BattleLogCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var dim = NewImage((RectTransform)canvasGo.transform, "Dim", new Color(0f, 0f, 0f, 0.75f), true);
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<ClickToClose>();
        _root = dim.gameObject;

        var panel = NewImage(dim.rectTransform, "Panel", new Color(0f, 0f, 0f, 0.99f), true);
        AddGoldFrame(panel.rectTransform, "Frame", new Color(0.86f, 0.74f, 0.45f, 0.95f), 3f);
        AddGoldFrame(panel.rectTransform, "FrameInner", new Color(0.86f, 0.74f, 0.45f, 0.30f), -8f);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(980f, 1400f);

        _title = NewText(prt, "Title", font, 44f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.96f, 0.87f, 0.58f));
        PlaceTop(_title.rectTransform, 0f, 30f, 980f, 60f);
        var line = NewImage(prt, "Divider", new Color(0.96f, 0.87f, 0.58f, 0.75f), false);
        PlaceTop(line.rectTransform, 140f, 100f, 700f, 3f);

        // 기록 — 스크롤 (끌거나 휠)
        var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        vp.transform.SetParent(prt, false);
        var vrt = (RectTransform)vp.transform;
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = new Vector2(50f, 40f); vrt.offsetMax = new Vector2(-50f, -120f);
        var vimg = vp.GetComponent<Image>(); vimg.color = new Color(0f, 0f, 0f, 0f); vimg.raycastTarget = true;
        _body = NewText(vrt, "Log", font, 30f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.92f, 0.92f, 0.92f));
        _body.textWrappingMode = TextWrappingModes.Normal;
        _body.lineSpacing = 8f;
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scroll = vp.AddComponent<ScrollRect>();
        _scroll.content = brt; _scroll.viewport = vrt;
        _scroll.horizontal = false; _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 40f;

        var closeBg = NewImage(prt, "Close", Color.white, true);
        Sprite frame = DungeonHud.PopupSprite;
        if (frame != null) { closeBg.sprite = frame; closeBg.type = Image.Type.Sliced; closeBg.pixelsPerUnitMultiplier = 3f; }
        else closeBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        var crt = closeBg.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, -56f); crt.sizeDelta = new Vector2(220f, 72f);
        closeBg.gameObject.AddComponent<Button>().onClick.AddListener(Hide);
        var cl = NewText(crt, "Label", font, 30f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        cl.text = "닫기"; Stretch(cl.rectTransform);

        _root.SetActive(false);
    }

    private void Fill()
    {
        var lines = BattleLogArchive.Lines;
        _title.text = string.IsNullOrEmpty(BattleLogArchive.BattleTitle) ? "전투 기록" : $"전투 기록 — {BattleLogArchive.BattleTitle}";
        _body.text = lines.Count == 0 ? "<color=#888888>아직 기록된 전투가 없다.</color>" : string.Join("\n", lines);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        if (_scroll != null) _scroll.verticalNormalizedPosition = 0f; // 마지막 줄부터 (위로 올려 처음부터 읽기)
    }

    // ─────────────────────────────────────────

    private static Image NewImage(RectTransform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI NewText(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = color;
        t.richText = true; t.raycastTarget = false;
        return t;
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

    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

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
