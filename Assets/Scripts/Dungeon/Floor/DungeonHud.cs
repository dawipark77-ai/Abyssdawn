using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 던전 화면 정보 표시 — 전부 코드로 만든다 (씬에 배치할 것 없음). MapManager 가 자동 생성.
///  - 위 가운데: 층 번호 (B3). 전체 지도 중에는 탐험률도 (B3 · Mapped 42%)
///  - 그 아래: 위험도 게이지 (초록 → 노랑 → 빨강, 세계수의 미궁식)
///  - 알림 문구 (보물 발견, 함정 등) — 잠깐 떴다 사라짐, 여러 개면 차례로
///  - 화면 번쩍임 (함정 피해 등)
///  - 확인창 (계단, 샘 등) — 열려 있는 동안 이동 잠금
///
/// 한글 글꼴이 프로젝트에 없어(LiberationSans 만 있음) 화면 문구는 영어로 쓴다.
/// </summary>
public class DungeonHud : MonoBehaviour
{
    private static DungeonHud _instance;
    public static DungeonHud Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<DungeonHud>();
            return _instance;
        }
    }

    /// <summary>없으면 만든다.</summary>
    public static DungeonHud Ensure()
    {
        if (Instance != null) return _instance;
        var go = new GameObject("DungeonHUD");
        _instance = go.AddComponent<DungeonHud>();
        return _instance;
    }

    public static readonly Color DangerSafe = new Color(0.35f, 0.9f, 0.4f);
    public static readonly Color DangerWarn = new Color(1f, 0.82f, 0.2f);
    public static readonly Color DangerHigh = new Color(1f, 0.25f, 0.2f);

    private const float ToastSeconds = 2.2f;

    // 정보 캔버스는 기존 UI(순서 0) 아래, 알림·확인창 캔버스는 맨 위
    private const int InfoSortingOrder = -1;
    private const int OverlaySortingOrder = 90;

    private RectTransform _infoRoot, _overlayRoot;
    private TextMeshProUGUI _floorText;
    private RectTransform _floorBg;
    private Image _dangerBg, _dangerFill;
    private float _danger;

    private CanvasGroup _toastGroup;
    private TextMeshProUGUI _toastText;
    private readonly Queue<string> _toasts = new Queue<string>();
    private Coroutine _toastRoutine;

    private Image _flash;
    private Coroutine _flashRoutine;

    private GameObject _dialog;
    private TextMeshProUGUI _dialogText, _yesText, _noText;
    private Action<bool> _dialogCallback;

    public bool IsDialogOpen => _dialog != null && _dialog.activeSelf;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
        DungeonEncounter.OnDangerChanged += SetDanger;
        SetDanger(DungeonEncounter.Danger);
    }

    private void OnDestroy()
    {
        DungeonEncounter.OnDangerChanged -= SetDanger;
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // 위험도가 빨강이면 게이지가 맥박처럼 깜빡인다
        if (_dangerFill != null && _danger >= 0.8f)
        {
            Color c = DangerHigh;
            c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 8f);
            _dangerFill.color = c;
        }

        if (!IsDialogOpen) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) CloseDialog(true);
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) CloseDialog(false);
    }

    // ─────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────

    /// <summary>층 번호. explored 가 0 이상이면 탐험률도 함께 (전체 지도용).</summary>
    public void SetFloor(int floor, float explored = -1f)
    {
        if (_floorText == null) return;
        bool withMap = explored >= 0f;
        bool complete = explored >= 0.9999f;
        _floorText.text = withMap
            ? $"B{floor}  <size=70%><color={(complete ? "#FFD24A" : "#BBBBBB")}>Mapped {(complete ? 100 : Mathf.FloorToInt(explored * 100f))}%</color></size>"
            : $"B{floor}";
        _floorBg.sizeDelta = new Vector2(withMap ? 520f : 220f, _floorBg.sizeDelta.y);
    }

    public void SetDangerVisible(bool visible)
    {
        if (_dangerBg != null) _dangerBg.gameObject.SetActive(visible);
    }

    public void SetDanger(float danger)
    {
        _danger = Mathf.Clamp01(danger);
        if (_dangerFill == null) return;
        _dangerFill.rectTransform.anchorMax = new Vector2(_danger, 1f);
        _dangerFill.color = _danger < 0.5f ? DangerSafe : _danger < 0.8f ? DangerWarn : DangerHigh;
    }

    /// <summary>잠깐 떴다 사라지는 알림. 여러 개면 차례로 보여준다. TMP 서식 태그 사용 가능.</summary>
    public void Toast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        _toasts.Enqueue(message);
        if (_toastRoutine == null) _toastRoutine = StartCoroutine(ToastLoop());
    }

    /// <summary>화면 전체가 잠깐 번쩍인다 (함정 피해 등).</summary>
    public void Flash(Color color, float duration = 0.35f)
    {
        if (_flash == null) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(color, duration));
    }

    /// <summary>예/아니오 확인창. 열려 있는 동안 플레이어 이동 잠금. Enter/Space = 예, Esc = 아니오.</summary>
    public void Confirm(string message, string yesLabel, string noLabel, Action<bool> onResult)
    {
        if (IsDialogOpen) CloseDialog(false);
        _dialogText.text = message;
        _yesText.text = yesLabel;
        _noText.text = noLabel;
        _dialogCallback = onResult;
        _dialog.SetActive(true);
        _dialog.transform.SetAsLastSibling();

        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
    }

    private void CloseDialog(bool result)
    {
        if (!IsDialogOpen) return;
        _dialog.SetActive(false);
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.UnlockInput(this);

        Action<bool> cb = _dialogCallback;
        _dialogCallback = null;
        if (cb != null) cb(result);
    }

    // ─────────────────────────────────────────
    // 동작
    // ─────────────────────────────────────────

    private IEnumerator ToastLoop()
    {
        while (_toasts.Count > 0)
        {
            _toastText.text = _toasts.Dequeue();
            _toastGroup.gameObject.SetActive(true);
            yield return Fade(_toastGroup, 0f, 1f, 0.15f);
            // 뒤에 기다리는 알림이 있으면 조금 빨리 넘긴다
            yield return new WaitForSeconds(_toasts.Count > 0 ? ToastSeconds * 0.6f : ToastSeconds);
            yield return Fade(_toastGroup, 1f, 0f, 0.25f);
        }
        _toastGroup.gameObject.SetActive(false);
        _toastRoutine = null;
    }

    private static IEnumerator Fade(CanvasGroup g, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        g.alpha = to;
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        _flash.gameObject.SetActive(true);
        float startA = color.a;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            color.a = startA * (1f - t / duration);
            _flash.color = color;
            yield return null;
        }
        _flash.gameObject.SetActive(false);
        _flashRoutine = null;
    }

    // ─────────────────────────────────────────
    // 화면 만들기
    // ─────────────────────────────────────────

    private void Build()
    {
        _infoRoot = CreateCanvas("Info", InfoSortingOrder, false);
        _overlayRoot = CreateCanvas("Overlay", OverlaySortingOrder, true);

        // 층 번호 (위 가운데, 오른쪽 위 MapButton·왼쪽 위 글자와 겹치지 않게 가운데)
        _floorBg = CreateImage(_infoRoot, "FloorLabel", new Color(0f, 0f, 0f, 0.5f), false).rectTransform;
        SetAnchor(_floorBg, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(220f, 84f));
        _floorText = CreateText(_floorBg, "Text", 58, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(_floorText.rectTransform);

        // 위험도 게이지
        _dangerBg = CreateImage(_infoRoot, "DangerGauge", new Color(0f, 0f, 0f, 0.6f), false);
        SetAnchor(_dangerBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(220f, 14f));
        _dangerFill = CreateImage(_dangerBg.rectTransform, "Fill", DangerSafe, false);
        RectTransform fill = _dangerFill.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(2f, 2f);
        fill.offsetMax = new Vector2(-2f, -2f);

        // 화면 번쩍임
        _flash = CreateImage(_overlayRoot, "Flash", Color.clear, false);
        Stretch(_flash.rectTransform);
        _flash.gameObject.SetActive(false);

        // 알림 문구 (가로형 팝업 배경)
        Image toastBg = CreatePopupPanel(_overlayRoot, "Toast", false);
        SetAnchor(toastBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(980f, 160f));
        _toastGroup = toastBg.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;
        _toastText = CreateText(toastBg.rectTransform, "Text", 38, FontStyles.Normal, TextAlignmentOptions.Center);
        Stretch(_toastText.rectTransform, 44f);
        toastBg.gameObject.SetActive(false);

        BuildDialog();
    }

    private void BuildDialog()
    {
        // 화면 전체를 덮는 어두운 막 — 뒤의 버튼(이동 패드 등)이 눌리지 않게 막는다
        Image blocker = CreateImage(_overlayRoot, "Dialog", new Color(0f, 0f, 0f, 0.55f), true);
        Stretch(blocker.rectTransform);
        _dialog = blocker.gameObject;

        // 가로형 팝업 배경 (SubPanelBackground)
        Image panel = CreatePopupPanel(blocker.rectTransform, "Panel", true);
        SetAnchor(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 360f));

        _dialogText = CreateText(panel.rectTransform, "Message", 42, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform msg = _dialogText.rectTransform;
        msg.anchorMin = new Vector2(0f, 0.36f);
        msg.anchorMax = new Vector2(1f, 1f);
        msg.offsetMin = new Vector2(60f, 0f);
        msg.offsetMax = new Vector2(-60f, -40f);

        _yesText = CreateButton(panel.rectTransform, "Yes", new Vector2(-165f, 42f), new Color(0.48f, 0.35f, 0.1f, 0.95f), new Color(1f, 0.92f, 0.65f), () => CloseDialog(true));
        _noText = CreateButton(panel.rectTransform, "No", new Vector2(165f, 42f), new Color(0.1f, 0.1f, 0.1f, 0.85f), new Color(0.85f, 0.85f, 0.85f), () => CloseDialog(false));

        _dialog.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 가로형 팝업 배경 — 모든 팝업(확인창·알림)에 공통으로 쓴다
    // ─────────────────────────────────────────

    public const string PopupBackgroundResource = "UI/SubPanelBackground";
    // 모서리 장식(약 50px)이 늘어나지 않도록 9분할(슬라이스)할 테두리 폭 (원본 픽셀)
    private const float PopupBorder = 56f;
    private static Sprite _popupSprite;

    /// <summary>
    /// Resources/UI/SubPanelBackground 를 9분할 스프라이트로 만든다 (원본 이미지 설정은 건드리지 않음).
    /// 이미지를 못 찾으면 null → 어두운 단색 패널로 대신한다.
    /// </summary>
    public static Sprite PopupSprite
    {
        get
        {
            if (_popupSprite != null) return _popupSprite;
            Texture2D tex = Resources.Load<Texture2D>(PopupBackgroundResource);
            if (tex == null)
            {
                Debug.LogWarning($"[DungeonHud] 팝업 배경 'Resources/{PopupBackgroundResource}' 을(를) 찾지 못해 단색 배경을 씁니다.");
                return null;
            }
            _popupSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                                         SpriteMeshType.FullRect, new Vector4(PopupBorder, PopupBorder, PopupBorder, PopupBorder));
            _popupSprite.name = "SubPanelBackground (sliced)";
            return _popupSprite;
        }
    }

    private static Image CreatePopupPanel(RectTransform parent, string name, bool raycast)
    {
        Sprite sprite = PopupSprite;
        Image img = CreateImage(parent, name, sprite != null ? Color.white : new Color(0.07f, 0.07f, 0.09f, 0.96f), raycast);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
        }
        return img;
    }

    private RectTransform CreateCanvas(string name, int order, bool interactive)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 씬의 기존 Canvas 와 같은 기준
        if (interactive) go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
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

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    private static TextMeshProUGUI CreateButton(RectTransform parent, string name, Vector2 bottomPos, Color bg, Color fg, Action onClick)
    {
        Image img = CreateImage(parent, name, bg, true);
        SetAnchor(img.rectTransform, new Vector2(0.5f, 0f), bottomPos, new Vector2(280f, 88f));
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => onClick());
        TextMeshProUGUI label = CreateText(img.rectTransform, "Label", 40, FontStyles.Bold, TextAlignmentOptions.Center);
        label.color = fg;
        Stretch(label.rectTransform);
        return label;
    }

    private static void SetAnchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
