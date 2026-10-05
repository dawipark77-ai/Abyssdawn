using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 창. 전멸하면 BattleManager 가 띄운다.
///   - Start Over : 처음(B1)부터 새 탐험. 나중에 타이틀 화면이 생기면 그쪽으로 보낼 자리.
///   - Last Save  : 마지막으로 저장한 곳에서 다시. 저장이 없으면 눌리지 않음.
/// Resources/UI/GameOverPanel 프리팹이 있으면 그것을 쓰고, 없으면 코드로 만든다 (팝업 규칙: 가로형 PopupFrame).
/// 버튼은 이름으로 연결 — RestartButton / LoadButton. 문구는 Title, Message, SaveInfo (있으면 채움).
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public const string PrefabResource = "UI/GameOverPanel";
    private const int SortingOrder = 500; // 전투 UI·파티 카드 위

    private CanvasGroup _group;
    private bool _chosen;

    /// <summary>게임 오버 창을 띄운다. 버튼은 한 번만 눌린다.</summary>
    public static GameOverScreen Show(bool hasSave, string saveInfo, Action onRestart, Action onLoadSave)
    {
        var canvasGo = new GameObject("GameOverCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var root = (RectTransform)canvasGo.transform;
        GameObject prefab = Resources.Load<GameObject>(PrefabResource);
        GameObject panel = prefab != null ? Instantiate(prefab, root, false) : BuildPanel(root);
        panel.name = "GameOverPanel";

        var screen = canvasGo.AddComponent<GameOverScreen>();
        screen._group = canvasGo.AddComponent<CanvasGroup>();
        screen._group.alpha = 0f;
        screen.Bind(panel, hasSave, saveInfo, onRestart, onLoadSave);
        return screen;
    }

    private void Bind(GameObject panel, bool hasSave, string saveInfo, Action onRestart, Action onLoadSave)
    {
        int bound = 0;
        foreach (Button b in panel.GetComponentsInChildren<Button>(true))
        {
            if (b.name == "RestartButton")
            {
                b.onClick.AddListener(() => Choose(onRestart));
                bound++;
            }
            else if (b.name == "LoadButton")
            {
                b.interactable = hasSave;
                if (hasSave) b.onClick.AddListener(() => Choose(onLoadSave));
                else
                {
                    // 저장이 없으면 버튼 전체(테두리·글자 포함)를 흐리게 — 눌리지 않는 것이 보이도록
                    var cg = b.GetComponent<CanvasGroup>();
                    if (cg == null) cg = b.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0.4f;
                }
                bound++;
            }
        }
        if (bound < 2)
            Debug.LogWarning($"[GameOverScreen] 버튼 {bound}/2개만 찾았습니다. 이름이 RestartButton / LoadButton 인 Button 이 있어야 합니다.");

        foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(true))
            if (t.name == "SaveInfo")
                t.text = hasSave ? (saveInfo ?? "") : "<color=#888888>No save yet</color>";
    }

    private void Choose(Action action)
    {
        if (_chosen) return; // 두 번 눌림 방지
        _chosen = true;
        if (_group != null) _group.interactable = false;
        action?.Invoke();
    }

    private void Update()
    {
        // 천천히 나타남 (시간 정지 중에도 동작하도록 unscaled)
        if (_group != null && _group.alpha < 1f)
            _group.alpha = Mathf.Min(1f, _group.alpha + Time.unscaledDeltaTime / 0.8f);
    }

    // ─────────────────────────────────────────
    // 코드로 만드는 기본 창 (프리팹을 처음 만들 때도 이것을 저장)
    // ─────────────────────────────────────────

    public static GameObject BuildPanel(RectTransform parent)
    {
        Image blocker = CreateImage(parent, "GameOverPanel", new Color(0f, 0f, 0f, 0.78f), true);
        Stretch(blocker.rectTransform);

        Sprite frame = DungeonHud.PopupSprite;
        Image panel = CreateImage(blocker.rectTransform, "Panel", frame != null ? Color.white : new Color(0.07f, 0.07f, 0.09f, 0.96f), true);
        if (frame != null) { panel.sprite = frame; panel.type = Image.Type.Sliced; panel.fillCenter = true; }
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(1000f, 620f);

        TextMeshProUGUI title = CreateText(prt, "Title", 72, FontStyles.Bold);
        title.text = "<color=#FF6B6B>YOU HAVE FALLEN</color>";
        Place(title.rectTransform, 0f, -50f, 900f, 96f);

        TextMeshProUGUI msg = CreateText(prt, "Message", 34, FontStyles.Normal);
        msg.text = "<color=#BBBBBB>The abyss claims another light.</color>";
        Place(msg.rectTransform, 0f, -150f, 900f, 56f);

        CreateChoice(prt, "RestartButton", "Start Over", "<color=#BBBBBB>Begin a new journey\nfrom B1</color>", -225f, false);
        CreateChoice(prt, "LoadButton", "Last Save", "", 225f, true);

        return blocker.gameObject;
    }

    private static void CreateChoice(RectTransform parent, string name, string label, string desc, float x, bool withSaveInfo)
    {
        const float w = 400f, h = 270f;
        Image bg = CreateImage(parent, name, new Color(0.12f, 0.1f, 0.06f, 0.95f), true);
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -240f);
        rt.sizeDelta = new Vector2(w, h);

        Image border = CreateImage(rt, "Border", new Color(0.85f, 0.68f, 0.3f, 0.9f), false);
        Stretch(border.rectTransform, -3f);
        border.transform.SetAsFirstSibling();
        Image inner = CreateImage(rt, "Inner", new Color(0.12f, 0.1f, 0.06f, 1f), false);
        Stretch(inner.rectTransform);
        inner.transform.SetSiblingIndex(1);

        var btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        btn.colors = colors;

        TextMeshProUGUI l = CreateText(rt, "Label", 52, FontStyles.Bold);
        l.text = label;
        l.color = new Color(1f, 0.92f, 0.65f);
        Place(l.rectTransform, 0f, -34f, w - 20f, 72f);

        TextMeshProUGUI d = CreateText(rt, withSaveInfo ? "SaveInfo" : "Desc", 28, FontStyles.Normal);
        d.text = desc;
        Place(d.rectTransform, 0f, -125f, w - 30f, 110f);
    }

    private static Image CreateImage(RectTransform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    private static void Place(RectTransform rt, float x, float topY, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, topY);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
