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
    private TextMeshProUGUI _dangerPercent;   // 위험 예지(탐험 스킬)가 있을 때만
    private TextMeshProUGUI _fieldStatusText; // 함정 상태이상 (독 21 · 실명 30 …)
    private float _nextSkillCheck;

    private CanvasGroup _toastGroup;
    private TextMeshProUGUI _toastText;
    private readonly Queue<string> _toasts = new Queue<string>();
    private Coroutine _toastRoutine;

    private Image _flash;
    private Coroutine _flashRoutine;

    private GameObject _dialog;
    private TextMeshProUGUI _dialogText, _yesText, _noText;
    private Action<bool> _dialogCallback;

    public bool IsDialogOpen => (_dialog != null && _dialog.activeSelf) || IsLevelUpOpen || IsSlotPickOpen;

    // ── 스킬 칸 선택 (칸이 다 찼을 때 교체) ──
    private GameObject _slotPick;
    private TextMeshProUGUI _slotPickTitle;
    private readonly List<GameObject> _slotPickButtons = new List<GameObject>();
    private Action<int> _slotPickCallback;
    public bool IsSlotPickOpen => _slotPick != null && _slotPick.activeSelf;

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

    // ── 마을 메뉴 (여관 · 상점 · 저장) ──
    private GameObject _town;
    private Action _townInn, _townShop, _townSave, _townLeave;
    public bool IsTownOpen => _town != null && _town.activeSelf;

    // ── 상점 (마을 위에 뜸) ──
    private GameObject _shop, _shopRowTemplate;
    private RectTransform _shopRows;
    private ScrollRect _shopScroll;
    private TextMeshProUGUI _shopGold, _shopEmpty;
    private Image _shopBuyTab, _shopSellTab;
    private bool _shopSelling;
    private TownShop.Kind _shopKind = TownShop.Kind.General; // [2026-10-08] 잡화점 / 무기점
    private TextMeshProUGUI _shopTitle;
    public bool IsShopOpen => _shop != null && _shop.activeSelf;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
        DungeonEncounter.OnDangerChanged += SetDanger;
        SetDanger(DungeonEncounter.Danger);
        DungeonFieldStatus.OnChanged += RefreshFieldStatus;
        RefreshFieldStatus();
    }

    private void OnDestroy()
    {
        DungeonEncounter.OnDangerChanged -= SetDanger;
        DungeonFieldStatus.OnChanged -= RefreshFieldStatus;
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

        // 위험 예지를 배웠는지 가끔 확인 (배운 직후 % 표시가 켜지게)
        if (Time.unscaledTime >= _nextSkillCheck) { _nextSkillCheck = Time.unscaledTime + 1f; RefreshDangerPercent(); }

        if (IsShopOpen && (_dialog == null || !_dialog.activeSelf) && Input.GetKeyDown(KeyCode.Escape)) { CloseShop(); return; }
        if (IsTownOpen && (_dialog == null || !_dialog.activeSelf) && Input.GetKeyDown(KeyCode.Escape)) { CloseTown(); return; }
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
        if (IsTownOpen) return;
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

    /// <summary>함정 상태이상 표시 갱신 (위험도 게이지 아래 한 줄).</summary>
    public void RefreshFieldStatus()
    {
        if (_fieldStatusText == null) return;
        string s = DungeonFieldStatus.Summary();
        _fieldStatusText.text = s;
        _fieldStatusText.gameObject.SetActive(!string.IsNullOrEmpty(s));
    }

    private void RefreshDangerPercent()
    {
        if (_dangerPercent == null) return;
        bool show = _dangerBg != null && _dangerBg.gameObject.activeSelf && FieldSkills.Has(FieldSkills.DangerSense);
        _dangerPercent.gameObject.SetActive(show);
        if (show) _dangerPercent.text = Mathf.RoundToInt(_danger * 100f) + "%";
    }

    public void SetDanger(float danger)
    {
        _danger = Mathf.Clamp01(danger);
        RefreshDangerPercent();
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

    /// <summary>
    /// 스킬 칸 선택창. 칸마다 버튼(지금 들어 있는 스킬 이름, 빈 칸은 "Empty") + "Later".
    /// 고른 칸 번호(0부터)로 onPick 호출, Later/Esc 는 -1. 열려 있는 동안 플레이어 이동 잠금.
    /// </summary>
    public void ChooseSkillSlot(string title, IList<string> slotLabels, Action<int> onPick)
    {
        Choose(title, slotLabels, "Later", true, onPick);
    }

    /// <summary>여러 선택지 창 (2열 버튼 + 맨 아래 취소). numbered 면 버튼 앞에 번호. 취소 = -1.</summary>
    public void Choose(string title, IList<string> slotLabels, string cancelLabel, bool numbered, Action<int> onPick)
    {
        if (_slotPick == null) BuildSlotPick();
        CloseDialog(false);
        foreach (var b in _slotPickButtons) { b.SetActive(false); Destroy(b); } // Destroy 는 프레임 끝 — 그 전에 바로 숨김
        _slotPickButtons.Clear();

        _slotPickTitle.text = title;
        RectTransform panel = (RectTransform)_slotPick.transform.Find("Panel");
        int count = slotLabels != null ? slotLabels.Count : 0;
        int rows = (count + 1) / 2;
        // 위: 제목 영역 / 가운데: 2열 버튼 / 아래: 취소 버튼
        const float topArea = 250f, btnH = 92f, gapY = 14f, bottomArea = 170f;
        float panelH = topArea + rows * btnH + Mathf.Max(0, rows - 1) * gapY + bottomArea;
        panel.sizeDelta = new Vector2(1000f, panelH);

        for (int i = 0; i < count; i++)
        {
            int index = i;
            int col = i % 2, row = i / 2;
            float x = col == 0 ? -225f : 225f;
            float y = panelH - topArea - btnH - row * (btnH + gapY); // 버튼 아래 끝 (아래 기준 좌표)
            TextMeshProUGUI label = CreateButton(panel, $"Slot{i + 1}", new Vector2(x, y), new Color(0.16f, 0.16f, 0.2f, 0.95f), Color.white, () => CloseSlotPick(index));
            ((RectTransform)label.transform.parent).sizeDelta = new Vector2(430f, 92f);
            label.fontSize = 32;
            label.fontStyle = FontStyles.Normal;
            label.text = numbered ? $"<color=#C9A44C>{i + 1}</color>  {slotLabels[i]}" : slotLabels[i];
            _slotPickButtons.Add(label.transform.parent.gameObject);
        }
        TextMeshProUGUI later = CreateButton(panel, "Cancel", new Vector2(0f, 40f), new Color(0.1f, 0.1f, 0.1f, 0.85f), new Color(0.85f, 0.85f, 0.85f), () => CloseSlotPick(-1));
        later.text = cancelLabel;
        _slotPickButtons.Add(later.transform.parent.gameObject);

        _slotPickCallback = onPick;
        _slotPick.SetActive(true);
        _slotPick.transform.SetAsLastSibling();
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
    }

    private void CloseSlotPick(int index)
    {
        if (_slotPick == null || !_slotPick.activeSelf) return;
        _slotPick.SetActive(false);
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.UnlockInput(this);
        Action<int> cb = _slotPickCallback;
        _slotPickCallback = null;
        if (cb != null) cb(index);
    }

    private void BuildSlotPick()
    {
        Image blocker = CreateImage(_overlayRoot, "SkillSlotPick", new Color(0f, 0f, 0f, 0.55f), true);
        Stretch(blocker.rectTransform);
        _slotPick = blocker.gameObject;
        Image panel = CreatePopupPanel(blocker.rectTransform, "Panel", true);
        SetAnchor(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 600f));
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _slotPickTitle = CreateText(panel.rectTransform, "Title", 38, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform t = _slotPickTitle.rectTransform;
        t.anchorMin = new Vector2(0f, 1f);
        t.anchorMax = new Vector2(1f, 1f);
        t.pivot = new Vector2(0.5f, 1f);
        t.anchoredPosition = new Vector2(0f, -50f);
        t.sizeDelta = new Vector2(-100f, 185f);
        _slotPick.SetActive(false);
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
            _toastGroup.transform.SetAsLastSibling(); // 마을 메뉴·확인창 위에 보이게
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

        // 화면 테두리 (금색 이중선 + 모서리 장식). [2026-10-05] 기본 끔 — 다시 쓰려면 ShowScreenFrame = true
        if (ShowScreenFrame) BuildScreenFrame();

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

        // 위험도 % (위험 예지) — 게이지 오른쪽
        _dangerPercent = CreateText(_dangerBg.rectTransform, "Percent", 26, FontStyles.Bold, TextAlignmentOptions.Left);
        RectTransform pr = _dangerPercent.rectTransform;
        pr.anchorMin = new Vector2(1f, 0.5f);
        pr.anchorMax = new Vector2(1f, 0.5f);
        pr.pivot = new Vector2(0f, 0.5f);
        pr.anchoredPosition = new Vector2(10f, 0f);
        pr.sizeDelta = new Vector2(90f, 36f);
        _dangerPercent.textWrappingMode = TextWrappingModes.NoWrap;
        _dangerPercent.gameObject.SetActive(false);

        // 함정 상태이상 한 줄 — 게이지 아래
        _fieldStatusText = CreateText(_infoRoot, "FieldStatus", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchor(_fieldStatusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(900f, 44f));
        _fieldStatusText.textWrappingMode = TextWrappingModes.NoWrap;
        _fieldStatusText.gameObject.SetActive(false);

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
        BuildTown();
        BuildShop();
        _dialog.transform.SetAsLastSibling(); // 확인창은 마을 메뉴 위에 뜬다
    }

    // ─────────────────────────────────────────
    // 상점 — 사기 / 팔기 탭. 규칙은 TownShop, 화면은 프리팹(Resources/UI/ShopPanel) 또는 코드 기본 모양.
    // 프리팹을 꾸밀 때 지킬 이름: Rows(목록 부모) 안의 RowTemplate(한 줄 견본: Icon, Name, Info, Price, Owned, ActionButton/Label),
    //                         Gold, Empty, BuyTab, SellTab, Close
    // ─────────────────────────────────────────

    public const string ShopPanelResource = "UI/ShopPanel";

    /// <summary>잡화점 (소비 아이템).</summary>
    public void ShowShop() { ShowShop(TownShop.Kind.General); }

    /// <summary>[2026-10-08] 잡화점 또는 무기점을 연다 (같은 상점 창 — 제목과 목록만 바뀜).</summary>
    public void ShowShop(TownShop.Kind kind)
    {
        _shopKind = kind;
        if (_shopTitle != null) _shopTitle.text = kind == TownShop.Kind.Arms ? "<color=#FFD24A>ARMS</color>" : "<color=#FFD24A>GENERAL STORE</color>";
        OpenShopPanel();
    }

    private void OpenShopPanel()
    {
        if (_shop == null) return;
        _shopSelling = false;
        _shop.SetActive(true);
        _shop.transform.SetAsLastSibling();
        _dialog.transform.SetAsLastSibling();
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(_shop);
        RefreshShop();
        ShopScrollToTop();
    }

    public void CloseShop()
    {
        if (!IsShopOpen) return;
        _shop.SetActive(false);
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.UnlockInput(_shop);
    }

    private void BuildShop()
    {
        GameObject prefab = Resources.Load<GameObject>(ShopPanelResource);
        _shop = prefab != null ? Instantiate(prefab, _overlayRoot, false) : BuildShopPanel(_overlayRoot);
        _shop.name = "ShopPanel";

        Transform rows = FindChildRecursive(_shop.transform, "Rows");
        _shopRows = rows as RectTransform;
        Transform tmpl = FindChildRecursive(_shop.transform, "RowTemplate");
        _shopRowTemplate = tmpl != null ? tmpl.gameObject : null;
        if (_shopRowTemplate != null) _shopRowTemplate.SetActive(false);
        _shopGold = FindText(_shop.transform, "Gold");
        _shopTitle = FindText(_shop.transform, "Title");
        _shopEmpty = FindText(_shop.transform, "Empty");

        foreach (Button b in _shop.GetComponentsInChildren<Button>(true))
        {
            switch (b.name)
            {
                case "BuyTab": _shopBuyTab = b.targetGraphic as Image; b.onClick.AddListener(() => { _shopSelling = false; RefreshShop(); ShopScrollToTop(); }); break;
                case "SellTab": _shopSellTab = b.targetGraphic as Image; b.onClick.AddListener(() => { _shopSelling = true; RefreshShop(); ShopScrollToTop(); }); break;
                case "Close": b.onClick.AddListener(CloseShop); break;
            }
        }
        if (_shopRows == null || _shopRowTemplate == null)
            Debug.LogWarning("[DungeonHud] 상점 창에 Rows / RowTemplate 이 없습니다. 목록을 표시할 수 없습니다.");
        else
            _shopScroll = WrapShopRowsInScroll(_shopRows);
        _shop.SetActive(false);
    }

    private void ShopScrollToTop()
    {
        if (_shopScroll == null) return;
        Canvas.ForceUpdateCanvases();
        _shopScroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// [2026-10-07] 상점 목록 스크롤. Rows 가 차지하던 자리에 Viewport(잘라내기)를 만들고 Rows 를 그 안의 Content 로 옮긴다.
    /// 오른쪽에 세로 스크롤바 — 목록이 칸에 다 들어가면 자동으로 숨는다. 마우스 휠·드래그로도 움직인다.
    /// 프리팹에 이미 ScrollRect 가 있으면 그것을 쓴다.
    /// </summary>
    private static ScrollRect WrapShopRowsInScroll(RectTransform rows)
    {
        ScrollRect existing = rows.GetComponentInParent<ScrollRect>(true);
        if (existing != null) return existing;

        var parent = (RectTransform)rows.parent;
        int sibling = rows.GetSiblingIndex();

        // Viewport — Rows 의 원래 자리·크기 그대로
        var viewportGo = new GameObject("RowsViewport", typeof(RectTransform));
        var viewport = (RectTransform)viewportGo.transform;
        viewport.SetParent(parent, false);
        viewport.SetSiblingIndex(sibling);
        viewport.anchorMin = rows.anchorMin;
        viewport.anchorMax = rows.anchorMax;
        viewport.pivot = rows.pivot;
        viewport.anchoredPosition = rows.anchoredPosition;
        viewport.sizeDelta = rows.sizeDelta;
        viewportGo.AddComponent<RectMask2D>();
        // 줄 사이 빈틈에서도 휠·드래그가 먹도록 투명한 판정 그림
        var hit = viewportGo.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        // Rows → Content: 위에 붙고 줄 수만큼 길어짐
        rows.SetParent(viewport, false);
        rows.anchorMin = new Vector2(0f, 1f);
        rows.anchorMax = new Vector2(1f, 1f);
        rows.pivot = new Vector2(0.5f, 1f);
        rows.anchoredPosition = Vector2.zero;
        rows.sizeDelta = Vector2.zero;
        var fitter = rows.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = rows.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 세로 스크롤바 — Viewport 오른쪽 바깥
        const float barW = 20f, barGap = 12f;
        Image barBg = CreateImage(parent, "RowsScrollbar", new Color(0.1f, 0.08f, 0.05f, 0.85f), true);
        RectTransform bar = barBg.rectTransform;
        bar.SetSiblingIndex(sibling + 1);
        bar.anchorMin = viewport.anchorMin;
        bar.anchorMax = viewport.anchorMax;
        bar.pivot = new Vector2(0f, viewport.pivot.y);
        bar.anchoredPosition = new Vector2(viewport.anchoredPosition.x + viewport.sizeDelta.x * (1f - viewport.pivot.x) + barGap,
                                           viewport.anchoredPosition.y);
        bar.sizeDelta = new Vector2(barW, viewport.sizeDelta.y);

        var area = new GameObject("Sliding Area", typeof(RectTransform));
        var areaRt = (RectTransform)area.transform;
        areaRt.SetParent(bar, false);
        Stretch(areaRt);
        Image handle = CreateImage(areaRt, "Handle", new Color(0.85f, 0.68f, 0.3f, 0.9f), true);
        Stretch(handle.rectTransform);

        var scrollbar = barBg.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scroll = viewportGo.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = rows;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    private void RefreshShop()
    {
        if (_shopGold != null) _shopGold.text = $"<color=#FFD24A>{PlayerWallet.Gold} G</color>";
        if (_shopBuyTab != null) _shopBuyTab.color = _shopSelling ? ShopTabOff : ShopTabOn;
        if (_shopSellTab != null) _shopSellTab.color = _shopSelling ? ShopTabOn : ShopTabOff;
        if (_shopRows == null || _shopRowTemplate == null) return;

        // 지난 목록 지우기 (견본은 남김)
        for (int i = _shopRows.childCount - 1; i >= 0; i--)
        {
            Transform c = _shopRows.GetChild(i);
            if (c.gameObject != _shopRowTemplate) Destroy(c.gameObject);
        }

        List<TownShop.Entry> list = _shopSelling ? TownShop.SellList(_shopKind) : TownShop.BuyList(_shopKind);
        if (_shopEmpty != null)
        {
            _shopEmpty.gameObject.SetActive(list.Count == 0);
            _shopEmpty.text = _shopSelling ? "Nothing to sell." : "Nothing for sale.";
        }
        foreach (TownShop.Entry e in list)
        {
            GameObject row = Instantiate(_shopRowTemplate, _shopRows, false);
            row.name = "Row_" + e.asset.name;
            row.SetActive(true);
            int price = _shopSelling ? e.SellPrice : e.BuyPrice;

            Transform icon = FindChildRecursive(row.transform, "Icon");
            Image iconImg = icon != null ? icon.GetComponent<Image>() : null;
            if (iconImg != null) { iconImg.sprite = e.Icon; iconImg.enabled = e.Icon != null; iconImg.preserveAspect = true; }
            SetText(row.transform, "Name", e.Name);
            SetText(row.transform, "Info", TownShop.InfoText(e));
            SetText(row.transform, "Price", $"<color=#FFD24A>{price} G</color>");
            SetText(row.transform, "Owned", TownShop.OwnedText(e));

            Transform act = FindChildRecursive(row.transform, "ActionButton");
            Button btn = act != null ? act.GetComponent<Button>() : null;
            if (btn != null)
            {
                // [2026-10-09] 같은 장비도 다시 살 수 있음 (쌍수) — 30개까지
                bool full = !_shopSelling && e.equipment != null && !EquipmentBag.CanAdd(e.equipment);
                SetText(act, "Label", _shopSelling ? "Sell" : full ? "Full" : "Buy");
                bool canBuy = _shopSelling || (!full && PlayerWallet.Gold >= price);
                btn.interactable = canBuy;
                TownShop.Entry captured = e;
                bool selling = _shopSelling;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    string msg = selling ? TownShop.Sell(captured) : TownShop.Buy(captured);
                    if (!string.IsNullOrEmpty(msg)) Toast(msg);
                    RefreshShop();
                    // [2026-10-09] 장비를 샀으면 바로 장착할지 묻는다 (자동 장착 없음)
                    if (!selling && captured.equipment != null && msg != null && msg.StartsWith("Bought"))
                    {
                        AbyssdawnBattle.EquipmentData bought = captured.equipment;
                        Confirm($"Equip <b>{captured.Name}</b> now?", "Equip", "Later", yes =>
                        {
                            if (yes && TownShop.Equip(bought)) Toast($"<color=#9FFF9F>Equipped {captured.Name}.</color>");
                            RefreshShop();
                        });
                    }
                });
            }
        }
    }

    private static readonly Color ShopTabOn = new Color(0.48f, 0.35f, 0.1f, 0.95f);
    private static readonly Color ShopTabOff = new Color(0.1f, 0.1f, 0.1f, 0.85f);

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform c in root)
        {
            if (c.name == name) return c;
            Transform f = FindChildRecursive(c, name);
            if (f != null) return f;
        }
        return null;
    }

    private static TextMeshProUGUI FindText(Transform root, string name)
    {
        Transform t = FindChildRecursive(root, name);
        if (t == null) return null;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        return tmp != null ? tmp : t.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static void SetText(Transform root, string name, string text)
    {
        TextMeshProUGUI t = FindText(root, name);
        if (t != null) t.text = text;
    }

    /// <summary>코드로 만드는 기본 상점 창. 프리팹을 처음 만들 때도 이것을 저장했다.</summary>
    public static GameObject BuildShopPanel(RectTransform parent)
    {
        Image blocker = CreateImage(parent, "ShopPanel", new Color(0f, 0f, 0f, 0.6f), true);
        Stretch(blocker.rectTransform);

        Image panel = CreatePopupPanel(blocker.rectTransform, "Panel", true);
        SetAnchor(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1500f));

        TextMeshProUGUI title = CreateText(panel.rectTransform, "Title", 58, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = "<color=#FFD24A>SHOP</color>";
        Place(title.rectTransform, 0f, -46f, 880f, 76f);

        TextMeshProUGUI gold = CreateText(panel.rectTransform, "Gold", 40, FontStyles.Bold, TextAlignmentOptions.Right);
        gold.text = "<color=#FFD24A>0 G</color>";
        Place(gold.rectTransform, 300f, -56f, 300f, 60f);

        // 탭
        for (int i = 0; i < 2; i++)
        {
            string n = i == 0 ? "BuyTab" : "SellTab";
            Image tab = CreateImage(panel.rectTransform, n, i == 0 ? ShopTabOn : ShopTabOff, true);
            Place(tab.rectTransform, i == 0 ? -150f : 150f, -140f, 280f, 76f);
            var b = tab.gameObject.AddComponent<Button>();
            b.targetGraphic = tab;
            TextMeshProUGUI l = CreateText(tab.rectTransform, "Label", 38, FontStyles.Bold, TextAlignmentOptions.Center);
            l.text = i == 0 ? "Buy" : "Sell";
            Stretch(l.rectTransform);
        }

        // 목록
        var rowsGo = new GameObject("Rows", typeof(RectTransform));
        rowsGo.transform.SetParent(panel.rectTransform, false);
        var rows = (RectTransform)rowsGo.transform;
        Place(rows, 0f, -240f, 900f, 1080f);
        var layout = rowsGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        // 한 줄 견본
        Image row = CreateImage(rows, "RowTemplate", new Color(0.12f, 0.1f, 0.06f, 0.92f), false);
        row.rectTransform.sizeDelta = new Vector2(900f, 140f);
        Image rowBorder = CreateImage(row.rectTransform, "Border", new Color(0.85f, 0.68f, 0.3f, 0.6f), false);
        Stretch(rowBorder.rectTransform, -2f);
        rowBorder.transform.SetAsFirstSibling();
        Image rowInner = CreateImage(row.rectTransform, "Inner", new Color(0.12f, 0.1f, 0.06f, 1f), false);
        Stretch(rowInner.rectTransform);
        rowInner.transform.SetSiblingIndex(1);

        Image icon = CreateImage(row.rectTransform, "Icon", Color.white, false);
        RowPlace(icon.rectTransform, 20f, 0f, 100f, 100f);
        TextMeshProUGUI name = CreateText(row.rectTransform, "Name", 40, FontStyles.Bold, TextAlignmentOptions.Left);
        RowPlace(name.rectTransform, 140f, 26f, 440f, 56f);
        TextMeshProUGUI info = CreateText(row.rectTransform, "Info", 28, FontStyles.Normal, TextAlignmentOptions.Left);
        info.color = new Color(0.75f, 0.75f, 0.75f);
        RowPlace(info.rectTransform, 140f, -30f, 440f, 50f);
        TextMeshProUGUI price = CreateText(row.rectTransform, "Price", 36, FontStyles.Bold, TextAlignmentOptions.Right);
        RowPlace(price.rectTransform, 560f, 26f, 140f, 50f);
        TextMeshProUGUI owned = CreateText(row.rectTransform, "Owned", 26, FontStyles.Normal, TextAlignmentOptions.Right);
        owned.color = new Color(0.75f, 0.75f, 0.75f);
        RowPlace(owned.rectTransform, 560f, -30f, 140f, 44f);

        Image act = CreateImage(row.rectTransform, "ActionButton", new Color(0.48f, 0.35f, 0.1f, 0.95f), true);
        RowPlace(act.rectTransform, 724f, 0f, 156f, 84f);
        var actBtn = act.gameObject.AddComponent<Button>();
        actBtn.targetGraphic = act;
        TextMeshProUGUI actLabel = CreateText(act.rectTransform, "Label", 36, FontStyles.Bold, TextAlignmentOptions.Center);
        actLabel.text = "Buy";
        actLabel.color = new Color(1f, 0.92f, 0.65f);
        Stretch(actLabel.rectTransform);

        TextMeshProUGUI empty = CreateText(panel.rectTransform, "Empty", 36, FontStyles.Normal, TextAlignmentOptions.Center);
        empty.text = "Nothing to sell.";
        empty.color = new Color(0.7f, 0.7f, 0.7f);
        Place(empty.rectTransform, 0f, -420f, 880f, 60f);
        empty.gameObject.SetActive(false);

        TextMeshProUGUI close = CreateButton(panel.rectTransform, "Close", new Vector2(0f, 44f), new Color(0.1f, 0.1f, 0.1f, 0.85f),
                                             new Color(0.85f, 0.85f, 0.85f), null);
        close.text = "Close";
        close.fontSize = 34;
        ((RectTransform)close.transform.parent).sizeDelta = new Vector2(240f, 76f);

        return blocker.gameObject;
    }

    /// <summary>한 줄 안에서 왼쪽 기준 x, 세로 가운데 기준 y 로 배치.</summary>
    private static void RowPlace(RectTransform rt, float leftX, float centerY, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftX, centerY);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ─────────────────────────────────────────
    // 마을 메뉴 — 여관 · 상점 · 저장 (가로형 팝업). 열려 있는 동안 이동 잠금.
    // ─────────────────────────────────────────

    /// <summary>마을 메뉴를 연다. 각 버튼은 해당 동작을 부르고 메뉴는 열린 채로 둔다 (Leave 만 닫음).</summary>
    public void ShowTown(Action onInn, Action onShop, Action onSave, Action onLeave)
    {
        _townInn = onInn; _townShop = onShop; _townSave = onSave; _townLeave = onLeave;
        _town.SetActive(true);
        _dialog.transform.SetAsLastSibling();
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(_town);
    }

    public void CloseTown()
    {
        if (!IsTownOpen) return;
        _town.SetActive(false);
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.UnlockInput(_town);
        Action leave = _townLeave;
        _townInn = _townShop = _townSave = _townLeave = null;
        if (leave != null) leave();
    }

    /// <summary>
    /// 마을 창 프리팹 (Resources/UI/TownPanel.prefab). 있으면 그대로 쓰고, 없으면 코드로 기본 모양을 만든다.
    /// 프리팹은 마음대로 꾸며도 된다 — 버튼만 이름이 "Inn" / "Shop" / "Save" / "Leave" 인 Button 이면 동작이 연결된다.
    /// </summary>
    public const string TownPanelResource = "UI/TownPanel";

    private void BuildTown()
    {
        GameObject prefab = Resources.Load<GameObject>(TownPanelResource);
        if (prefab != null)
        {
            _town = Instantiate(prefab, _overlayRoot, false);
            _town.name = prefab.name;
        }
        else
        {
            _town = BuildTownPanel(_overlayRoot);
        }
        BindTownButtons(_town);
        _town.SetActive(false);
    }

    /// <summary>이름으로 버튼을 찾아 마을 동작을 연결한다 (프리팹 배치를 바꿔도 이름만 같으면 됨).</summary>
    private void BindTownButtons(GameObject root)
    {
        int bound = 0;
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            switch (b.name)
            {
                case "Inn": b.onClick.AddListener(() => OnTownCommand(0)); bound++; break;
                case "Shop": b.onClick.AddListener(() => OnTownCommand(1)); bound++; break;
                case "Arms": b.onClick.AddListener(() => OnTownCommand(3)); bound++; break; // [2026-10-08] 무기점
                case "Save": b.onClick.AddListener(() => OnTownCommand(2)); bound++; break;
                case "Leave": b.onClick.AddListener(CloseTown); bound++; break;
            }
        }
        if (bound < 4)
            Debug.LogWarning($"[DungeonHud] 마을 창에서 버튼 {bound}/4개만 찾았습니다. 이름이 Inn / Shop / Save / Leave 인 Button 이 있어야 합니다.");
    }

    /// <summary>코드로 만드는 기본 마을 창 (화면 전체 어두운 막 + 가운데 가로형 팝업). 프리팹을 처음 만들 때도 이것을 저장했다.</summary>
    public static GameObject BuildTownPanel(RectTransform parent)
    {
        Image blocker = CreateImage(parent, "TownPanel", new Color(0f, 0f, 0f, 0.55f), true);
        Stretch(blocker.rectTransform);

        Image panel = CreatePopupPanel(blocker.rectTransform, "Panel", true);
        SetAnchor(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 600f));

        TextMeshProUGUI title = CreateText(panel.rectTransform, "Title", 58, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = "<color=#FFD24A>TOWN</color>";
        Place(title.rectTransform, 0f, -46f, 880f, 76f);
        TextMeshProUGUI sub = CreateText(panel.rectTransform, "Subtitle", 32, FontStyles.Normal, TextAlignmentOptions.Center);
        sub.text = "<color=#BBBBBB>A moment of light above the abyss.</color>";
        Place(sub.rectTransform, 0f, -122f, 880f, 50f);

        // 명령 3개 (가로 한 줄)
        string[] names = { "Inn", "Shop", "Save" };
        string[] descs = { "Rest and fully\nrecover the party", "Buy and sell\nsupplies", "Record your\njourney" };
        const float w = 270f, gap = 24f;
        float startX = -(w * 3 + gap * 2) / 2f + w / 2f;
        for (int i = 0; i < 3; i++)
        {
            Image btnImg = CreateImage(panel.rectTransform, names[i], new Color(0.12f, 0.1f, 0.06f, 0.92f), true);
            var rt = btnImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(startX + i * (w + gap), -200f);
            rt.sizeDelta = new Vector2(w, 230f);
            Image border = CreateImage(rt, "Border", new Color(0.85f, 0.68f, 0.3f, 0.9f), false);
            Stretch(border.rectTransform, -3f);
            border.transform.SetAsFirstSibling();
            Image inner = CreateImage(rt, "Inner", new Color(0.12f, 0.1f, 0.06f, 1f), false);
            Stretch(inner.rectTransform);
            inner.transform.SetSiblingIndex(1);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            TextMeshProUGUI label = CreateText(rt, "Label", 50, FontStyles.Bold, TextAlignmentOptions.Center);
            label.text = names[i];
            label.color = new Color(1f, 0.92f, 0.65f);
            Place(label.rectTransform, 0f, -30f, w - 20f, 70f);
            TextMeshProUGUI desc = CreateText(rt, "Desc", 28, FontStyles.Normal, TextAlignmentOptions.Center);
            desc.text = "<color=#BBBBBB>" + descs[i] + "</color>";
            Place(desc.rectTransform, 0f, -110f, w - 24f, 100f);
        }

        // 마을 나가기 (명령이 아니라 창 닫기)
        TextMeshProUGUI leave = CreateButton(panel.rectTransform, "Leave", new Vector2(0f, 40f), new Color(0.1f, 0.1f, 0.1f, 0.85f),
                                             new Color(0.85f, 0.85f, 0.85f), null);
        leave.text = "Leave";
        leave.fontSize = 34;
        ((RectTransform)leave.transform.parent).sizeDelta = new Vector2(240f, 76f);

        return blocker.gameObject;
    }

    private void OnTownCommand(int index)
    {
        if (_dialog != null && _dialog.activeSelf) return; // 확인창이 떠 있으면 무시
        if (index == 3) { ShowShop(TownShop.Kind.Arms); return; } // 무기점
        Action a = index == 0 ? _townInn : index == 1 ? _townShop : _townSave;
        if (a != null) a();
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
    /// <summary>팝업 테두리 스프라이트 에셋 (SubPanelBackground 복사본, 9분할 테두리 56 설정). 프리팹에 저장할 수 있는 형태.</summary>
    public const string PopupFrameSpriteResource = "UI/PopupFrame";

    public static Sprite PopupSprite
    {
        get
        {
            if (_popupSprite != null) return _popupSprite;
            // 에셋이 있으면 그것을 (프리팹·씬에 저장 가능). 없으면 원본 텍스처를 실행 중에 9분할
            Sprite asset = Resources.Load<Sprite>(PopupFrameSpriteResource);
            if (asset != null) { _popupSprite = asset; return _popupSprite; }
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
    /// <summary>메인맵 화면 전체 금색 테두리 표시 여부. [2026-10-05] 사용자 요청으로 끔.</summary>
    public static bool ShowScreenFrame = false;
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
        if (onClick != null) button.onClick.AddListener(() => onClick());
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
