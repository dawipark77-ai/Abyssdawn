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

    public bool IsDialogOpen => (_dialog != null && _dialog.activeSelf) || IsLevelUpOpen;

    // ── 레벨업 선택 화면 ──
    private GameObject _levelUp;
    private TextMeshProUGUI _lvTitle, _lvRandom, _lvPrompt;
    private readonly TextMeshProUGUI[] _lvStatLabels = new TextMeshProUGUI[5];
    private PlayerStats _lvHero;
    private int _lvDismissedAtLevel = -1;
    private float _lvNextCheck;
    private static readonly StatType[] LvStats = { StatType.Attack, StatType.Defense, StatType.Magic, StatType.Agility, StatType.Luck };
    private static readonly string[] LvStatNames = { "STR", "DEF", "MAG", "AGI", "LUK" };

    public bool IsLevelUpOpen => _levelUp != null && _levelUp.activeSelf;

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

        CheckLevelUp();

        if (_dialog == null || !_dialog.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) CloseDialog(true);
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) CloseDialog(false);
    }

    // ─────────────────────────────────────────
    // 레벨업 선택 화면: 무작위 성장 1개(자동) + 자유 스탯 1개(선택)
    // ─────────────────────────────────────────

    /// <summary>
    /// 주인공에게 자유 스탯 포인트가 있으면 선택 화면을 띄운다 (전투에서 레벨업하고 던전으로 돌아왔을 때 포함).
    /// 다른 확인창·창(상태·인벤토리)·전체 지도가 열려 있으면 닫힐 때까지 기다린다.
    /// "Later" 를 누르면 다음 레벨업 전까지 다시 띄우지 않는다 (상태창의 + 버튼으로 나중에 분배 가능).
    /// </summary>
    private void CheckLevelUp()
    {
        if (IsLevelUpOpen || Time.unscaledTime < _lvNextCheck) return;
        _lvNextCheck = Time.unscaledTime + 0.25f;

        if (_dialog != null && _dialog.activeSelf) return;
        if (DungeonPanelGroup.Instance != null && DungeonPanelGroup.Instance.AnyOpen) return;
        var fullMap = FindFirstObjectByType<DungeonFullMap>();
        if (fullMap != null && fullMap.IsOpen) return;

        DungeonGridPlayer gridPlayer = CurrentPlayer;
        PlayerStats hero = gridPlayer != null ? gridPlayer.GetComponent<PlayerStats>() : null;
        if (hero == null || hero.FreeStatPoints <= 0 || hero.level <= _lvDismissedAtLevel) return;
        ShowLevelUp(hero);
    }

    public void ShowLevelUp(PlayerStats hero)
    {
        if (hero == null || _levelUp == null) return;
        _lvHero = hero;

        // 아직 보여주지 않은 이 주인공의 레벨업 기록 → 무작위 성장 내용
        var parts = new List<string>();
        for (int i = PlayerStats.PendingLevelUpNotes.Count - 1; i >= 0; i--)
        {
            PlayerStats.LevelUpNote n = PlayerStats.PendingLevelUpNotes[i];
            if (n.playerName != hero.playerName) continue;
            string line = $"Lv {n.level}: " + (n.hasRandomStat ? $"<color=#FFD24A>{StatName(n.randomStat)} +1</color>" : "-");
            if (n.hpGain > 0) line += $"  <color=#FF8C8C>HP +{n.hpGain}</color>";
            if (n.mpGain > 0) line += $"  <color=#8CB4FF>MP +{n.mpGain}</color>";
            parts.Insert(0, line);
            PlayerStats.PendingLevelUpNotes.RemoveAt(i);
        }
        _lvRandom.text = parts.Count > 0
            ? "Random growth\n" + string.Join("\n", parts)
            : "Random growth applied";

        RefreshLevelUp();
        _levelUp.SetActive(true);
        _levelUp.transform.SetAsLastSibling();
        DungeonGridPlayer p = CurrentPlayer;
        if (p != null) p.LockInput(_levelUp);
    }

    private static DungeonGridPlayer CurrentPlayer => FindFirstObjectByType<DungeonGridPlayer>();

    private void RefreshLevelUp()
    {
        if (_lvHero == null) return;
        _lvTitle.text = $"<color=#FFD24A>LEVEL UP!</color>  Lv {_lvHero.level}";
        int pts = _lvHero.FreeStatPoints;
        _lvPrompt.text = $"Choose a stat to raise  <color=#FFD24A>({pts} point{(pts == 1 ? "" : "s")})</color>";
        int[] values = { _lvHero.Attack, _lvHero.Defense, _lvHero.Magic, _lvHero.Agility, _lvHero.Luck };
        for (int i = 0; i < LvStats.Length; i++)
        {
            string extra = LvStats[i] == StatType.Defense ? "\n<size=70%><color=#FF8C8C>+3 HP</color></size>"
                         : LvStats[i] == StatType.Magic ? "\n<size=70%><color=#8CB4FF>+3 MP</color></size>" : "\n<size=70%> </size>";
            _lvStatLabels[i].text = $"<b>{LvStatNames[i]}</b>\n{values[i]} <color=#8CFF8C>+1</color>{extra}";
        }
    }

    private void OnLevelUpStatChosen(int index)
    {
        if (_lvHero == null) return;
        _lvHero.AllocateFreePoint(LvStats[index]);
        Debug.Log($"[DungeonHud] 레벨업 자유 스탯: {LvStatNames[index]} +1 (남은 포인트 {_lvHero.FreeStatPoints})");
        if (_lvHero.FreeStatPoints > 0) RefreshLevelUp();
        else CloseLevelUp(false);
    }

    private void CloseLevelUp(bool later)
    {
        if (!IsLevelUpOpen) return;
        if (later && _lvHero != null) _lvDismissedAtLevel = _lvHero.level;
        _levelUp.SetActive(false);
        DungeonGridPlayer p = CurrentPlayer;
        if (p != null) p.UnlockInput(_levelUp);
        _lvHero = null;
    }

    private static string StatName(StatType t)
    {
        for (int i = 0; i < LvStats.Length; i++) if (LvStats[i] == t) return LvStatNames[i];
        return t.ToString();
    }

    private void BuildLevelUp()
    {
        Image blocker = CreateImage(_overlayRoot, "LevelUp", new Color(0f, 0f, 0f, 0.6f), true);
        Stretch(blocker.rectTransform);
        _levelUp = blocker.gameObject;

        Image panel = CreatePopupPanel(blocker.rectTransform, "Panel", true);
        SetAnchor(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 720f));

        _lvTitle = CreateText(panel.rectTransform, "Title", 56, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(_lvTitle.rectTransform, 0f, -50f, 880f, 80f);

        _lvRandom = CreateText(panel.rectTransform, "Random", 34, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(_lvRandom.rectTransform, 0f, -135f, 880f, 170f);

        _lvPrompt = CreateText(panel.rectTransform, "Prompt", 36, FontStyles.Normal, TextAlignmentOptions.Center);
        Place(_lvPrompt.rectTransform, 0f, -320f, 880f, 60f);

        // 스탯 버튼 5개 (가로 한 줄)
        const float w = 164f, gap = 14f;
        float startX = -(w * 5 + gap * 4) / 2f + w / 2f;
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            Image btnImg = CreateImage(panel.rectTransform, LvStatNames[i], new Color(0.12f, 0.1f, 0.06f, 0.92f), true);
            var rt = btnImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(startX + i * (w + gap), -395f);
            rt.sizeDelta = new Vector2(w, 180f);
            Image border = CreateImage(rt, "Border", new Color(0.85f, 0.68f, 0.3f, 0.9f), false);
            Stretch(border.rectTransform, -3f);
            border.transform.SetAsFirstSibling();
            Image inner = CreateImage(rt, "Inner", new Color(0.12f, 0.1f, 0.06f, 1f), false);
            Stretch(inner.rectTransform);
            inner.transform.SetSiblingIndex(1);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => OnLevelUpStatChosen(index));
            _lvStatLabels[i] = CreateText(rt, "Label", 38, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(_lvStatLabels[i].rectTransform, 6f);
        }

        // 나중에 (포인트는 남고 상태창 + 버튼으로 분배 가능)
        TextMeshProUGUI later = CreateButton(panel.rectTransform, "Later", new Vector2(0f, 40f), new Color(0.1f, 0.1f, 0.1f, 0.85f),
                                             new Color(0.8f, 0.8f, 0.8f), () => CloseLevelUp(true));
        later.fontSize = 32;
        later.text = "Later";
        ((RectTransform)later.transform.parent).sizeDelta = new Vector2(220f, 70f);

        _levelUp.SetActive(false);
    }

    private static void Place(RectTransform rt, float x, float topY, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, topY);
        rt.sizeDelta = new Vector2(w, h);
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
        CloseDialog(false);
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
        if (_dialog == null || !_dialog.activeSelf) return;
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

        // 화면 테두리 (금색 이중선 + 모서리 장식). 배경을 무엇으로 바꿔도 테두리는 그대로 — 맨 먼저 만들어 다른 정보 뒤에
        BuildScreenFrame();

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
        BuildLevelUp();
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

    // ─────────────────────────────────────────
    // 화면 테두리
    // ─────────────────────────────────────────

    public const string ScreenFrameResource = "UI/ScreenFrame";
    // 모서리 장식이 늘어나지 않도록 9분할할 테두리 폭 (원본 픽셀, 장식 약 100px + 여유)
    private const float ScreenFrameBorder = 120f;
    // 테두리 이미지가 이 폭(캔버스 기준 1080)일 때 원본 비율 그대로 보인다
    private const float ScreenFrameDesignWidth = 1080f;

    /// <summary>
    /// Resources/UI/ScreenFrame (배경을 투명하게 오려낸 금색 테두리)을 화면 전체에 9분할로 깐다.
    /// 곧은 선만 늘어나고 모서리 장식은 원래 모양 그대로. 터치는 막지 않는다.
    /// 정보 캔버스(기존 UI 아래)에 있으므로 창(상태·인벤토리 등)이 열리면 그 뒤에 가려진다.
    /// </summary>
    private void BuildScreenFrame()
    {
        Texture2D tex = Resources.Load<Texture2D>(ScreenFrameResource);
        if (tex == null)
        {
            Debug.LogWarning($"[DungeonHud] 화면 테두리 'Resources/{ScreenFrameResource}' 을(를) 찾지 못해 테두리 없이 진행합니다.");
            return;
        }
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(ScreenFrameBorder, ScreenFrameBorder, ScreenFrameBorder, ScreenFrameBorder));
        sprite.name = "ScreenFrame (sliced)";

        Image frame = CreateImage(_infoRoot, "ScreenFrame", Color.white, false);
        frame.sprite = sprite;
        frame.type = Image.Type.Sliced;
        frame.fillCenter = false; // 가운데는 어차피 투명 — 그리지 않음
        frame.pixelsPerUnitMultiplier = tex.width / ScreenFrameDesignWidth;
        Stretch(frame.rectTransform);
        frame.transform.SetAsFirstSibling();
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
