using System;
using System.Collections;
using System.Collections.Generic;
using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-14] 타이틀 다음 화면 — 드래곤 퀘스트식 "모험의 서" + 새 게임 캐릭터 만들기.
///   1) 모험의 서: 모험을 계속한다 / 새로운 모험의 서를 만든다 / 모험의 서를 지운다 + 칸 목록 + 아래 대화창
///   2) 새 게임: 검은 화면에 장 제목 자막 → 아래 대화창에서 해설 → 종의 기억 3개 → 성향(아직 틀만) → 직업 → 던전
/// 화면은 실행할 때 코드로 만든다 (씬에는 이 컴포넌트 하나 + 참조만). 색·창은 아비스던 것(팝업 테두리, 금색, 양피지색).
/// 언어는 타이틀의 언어 버튼 값(PlayerPrefs "Language": KR/EN)을 따른다.
/// </summary>
public class PrologueScreen : MonoBehaviour
{
    [Header("참조 (TitleSceneBuilder 가 채움)")]
    public TMP_FontAsset font;
    public TMP_FontAsset headingFont;
    public Sprite background;
    public Sprite frame;

    [Header("새 게임에서 고르는 것")]
    public List<MemoryOfSpeciesData> memoryChoices = new List<MemoryOfSpeciesData>();
    public int memoriesToPick = 3;
    public List<DispositionData> dispositionChoices = new List<DispositionData>();
    public List<CharacterClass> jobChoices = new List<CharacterClass>();
    [Tooltip("최종 상태창 능력치 계산용 주인공 원본 (HeroData). 복사본으로만 계산하고 원본은 건드리지 않음")]
    public PlayerStatData heroTemplate;

    [Header("씬")]
    public string dungeonScene = "Abyssdawn_Dungeon_2D 08";
    public string titleScene = "Abyssdawn_Title";

    // 색 (아비스던 디자인 토큰)
    private static readonly Color Cream = Hex("#f3e7c8");
    private static readonly Color Gold = Hex("#d4a761");
    private static readonly Color GoldDim = Hex("#b38a4c");
    private static readonly Color Muted = Hex("#8f7b54");
    private static readonly Color Parch = Hex("#e4d3a8");
    private static readonly Color Disabled = new Color(0.45f, 0.42f, 0.36f, 1f);
    private static readonly Color SelectTint = new Color(0.83f, 0.65f, 0.38f, 0.16f);

    private bool KR => PlayerPrefs.GetString(TitleScreen.LanguageKey, "KR") == "KR";
    private string T(string kr, string en) => KR ? kr : en;

    // ── 화면 요소 ──
    private RectTransform _root;
    private Sprite _arrow;
    private RectTransform _logLayer, _createLayer;
    private Image _black;
    private TextMeshProUGUI _caption;

    private TextMeshProUGUI[] _menuRows = new TextMeshProUGUI[3];
    private RectTransform _menuCursor;
    private readonly List<SlotRow> _slotRows = new List<SlotRow>();
    private RectTransform _slotCursor;

    private RectTransform _msgWindow;
    private TextMeshProUGUI _msgText;
    private ScrollRect _msgScroll;

    /// <summary>[2026-10-18] 대화창 글을 맨 위부터 보이게.</summary>
    private void MsgScrollTop()
    {
        if (_msgScroll == null) return;
        Canvas.ForceUpdateCanvases();
        _msgScroll.verticalNormalizedPosition = 1f;
    }
    private RectTransform _msgNext; // ▼
    private RectTransform _yesNo;
    private TextMeshProUGUI _yesText, _noText;
    private RectTransform _yesNoCursor;
    private RectTransform _choiceWindow;

    private class SlotRow { public TextMeshProUGUI no, name, lv, place, time; public Button button; public bool used; }

    private enum MenuMode { Continue = 0, Create = 1, Delete = 2 }
    private MenuMode _mode;
    private bool _busy;

    // 대화창 진행
    private bool _typing, _waitingTap, _tapped;
    private Action<bool> _yesNoAnswer;

    // ─────────────────────────────────────────
    // 시작
    // ─────────────────────────────────────────

    private void Start()
    {
        _arrow = MakeArrowSprite();
        BuildCanvas();
        BuildLogScreen();
        BuildMessageWindow();
        BuildCreateLayer();
        RefreshSlots();
        _mode = AnyUsed() ? MenuMode.Continue : MenuMode.Create;
        SelectMenu(_mode, false);
        FocusMenu();
        StartCoroutine(FadeFromBlack());
    }

    private void Update()
    {
        // 드퀘처럼 커서가 깜빡인다
        bool on = Mathf.FloorToInt(Time.unscaledTime / 0.45f) % 2 == 0;
        SetAlpha(_menuCursor, on || _nav != _menuHits ? 1f : 0.25f);
        SetAlpha(_slotCursor, on ? 1f : 0.25f);
        SetAlpha(_yesNoCursor, on ? 1f : 0.25f);
        if (_msgNext != null) _msgNext.gameObject.SetActive(_waitingTap && on);
        HandleKeyboard();
    }

    // ─────────────────────────────────────────
    // 키보드 (PC): W/S·↑/↓ 이동, Enter·Space 결정, Esc·Backspace 취소
    // ─────────────────────────────────────────

    private List<Button> _nav;           // 지금 키보드로 고르는 줄들
    private RectTransform _navCursor;
    private float _navOffset;
    private int _navIndex;
    private Action _navCancel;
    private int _yesIdx;                 // 0 = 예, 1 = 아니오
    private readonly List<Button> _menuHits = new List<Button>();
    private readonly List<Button> _slotHits = new List<Button>();
    private readonly List<Button> _choiceNav = new List<Button>();

    private void SetNav(List<Button> buttons, RectTransform cursor, float offsetX, int index, Action cancel)
    {
        _nav = buttons; _navCursor = cursor; _navOffset = offsetX; _navCancel = cancel;
        _navIndex = Mathf.Clamp(index, 0, Mathf.Max(0, buttons.Count - 1));
        if (buttons.Count > 0) PlaceCursor(cursor, (RectTransform)buttons[_navIndex].transform, offsetX);
    }

    private void FocusMenu()
    {
        _slotCursor.gameObject.SetActive(false);
        SetNav(_menuHits, _menuCursor, 40f, (int)_mode, null);
    }

    private void FocusSlots(int index)
    {
        SetNav(_slotHits, _slotCursor, 30f, index, () => { FocusMenu(); SelectMenu(_mode, false); });
    }

    private void HandleKeyboard()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        bool up = kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
        bool down = kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
        bool ok = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        bool cancel = kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame;

        // 1) 대화 진행
        if (_typing || _waitingTap) { if (ok) OnMessageTap(); return; }
        // 2) 예 / 아니오
        if (_yesNoAnswer != null && _yesNo.gameObject.activeSelf)
        {
            if (up || down)
            {
                _yesIdx = 1 - _yesIdx;
                PlaceCursor(_yesNoCursor, (_yesIdx == 0 ? _yesText : _noText).rectTransform);
            }
            if (ok) OnYesNo(_yesIdx == 0);
            else if (cancel) OnYesNo(false);
            return;
        }
        // 3) 종족 탭 (A/D · ←/→)
        if (_tabPrev != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)) { _tabPrev(); return; }
        if (_tabNext != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) { _tabNext(); return; }
        // 4) 줄 고르기
        if (_nav == null || _nav.Count == 0) return;
        if (up || down)
        {
            _navIndex = (_navIndex + (down ? 1 : -1) + _nav.Count) % _nav.Count;
            PlaceCursor(_navCursor, (RectTransform)_nav[_navIndex].transform, _navOffset);
        }
        if (ok && _nav[_navIndex] != null && _nav[_navIndex].interactable) _nav[_navIndex].onClick.Invoke();
        else if (cancel) _navCancel?.Invoke();
    }

    // ─────────────────────────────────────────
    // 모험의 서
    // ─────────────────────────────────────────

    private void SelectMenu(MenuMode m, bool fromTap)
    {
        bool any = AnyUsed();
        if (fromTap && (m == MenuMode.Continue || m == MenuMode.Delete) && !any)
        {
            ShowMessage(T("모험의 서가 하나도 없습니다.", "There are no adventure logs."));
            return;
        }
        _mode = m;
        PlaceCursor(_menuCursor, _menuRows[(int)m].rectTransform);
        switch (m)
        {
            case MenuMode.Continue: ShowMessage(T("불러올 모험의 서를 골라 주십시오.", "Please select an adventure log to load.")); break;
            case MenuMode.Create:   ShowMessage(T("새로운 모험을 기록할\n모험의 서를 골라 주십시오.", "Select a log to record your new adventure.")); break;
            case MenuMode.Delete:   ShowMessage(T("지울 모험의 서를 골라 주십시오.", "Select an adventure log to delete.")); break;
        }
        // [2026-10-14] 메뉴를 고르면 커서가 모험의 서 목록으로 옮겨 간다 (드퀘식) — 새로 만들기는 첫 빈 칸, 나머지는 첫 기록
        if (fromTap)
        {
            int first = 0;
            for (int i = 0; i < _slotRows.Count; i++)
                if (_slotRows[i].used == (m != MenuMode.Create)) { first = i; break; }
            FocusSlots(first);
        }
    }

    private void OnSlot(int i)
    {
        if (_busy) return;
        var row = _slotRows[i];
        if (_nav != _slotHits) FocusSlots(i); // 마우스로 바로 칸을 눌렀을 때도 목록에 초점
        _navIndex = i;
        PlaceCursor(_slotCursor, _slotHits[i].transform as RectTransform, 30f);
        switch (_mode)
        {
            case MenuMode.Continue:
                if (!row.used) { ShowMessage(T("그 모험의 서는 비어 있습니다.", "That adventure log is empty.")); return; }
                _busy = true;
                _nav = null;
                StartCoroutine(ContinueRoutine(i));
                break;
            case MenuMode.Create:
                if (row.used)
                    AskYesNo(T($"모험의 서 {i + 1}에 이미 모험이 기록되어 있습니다.\n덮어쓰시겠습니까?", $"Adventure Log {i + 1} already holds a journey.\nOverwrite it?"),
                        yes => { if (yes) StartCreate(i); else SelectMenu(_mode, false); });
                else StartCreate(i);
                break;
            case MenuMode.Delete:
                if (!row.used) { ShowMessage(T("그 모험의 서는 비어 있습니다.", "That adventure log is empty.")); return; }
                AskYesNo(T($"모험의 서 {NumEulReul(i + 1)} 정말 지우시겠습니까?\n<size=80%><color=#AAAAAA>한 번 지우면 되돌릴 수 없습니다.</color></size>",
                           $"Really delete Adventure Log {i + 1}?\n<size=80%><color=#AAAAAA>It cannot be undone.</color></size>"),
                    yes =>
                    {
                        if (yes)
                        {
                            SaveSystem.Delete(i);
                            RefreshSlots();
                            ShowMessage(T($"모험의 서 {NumEulReul(i + 1)} 지웠습니다.", $"Adventure Log {i + 1} has been deleted."));
                            if (!AnyUsed()) SelectMenu(MenuMode.Create, false);
                        }
                        else SelectMenu(_mode, false);
                    });
                break;
        }
    }

    private IEnumerator ContinueRoutine(int i)
    {
        var d = SaveSystem.Peek(i);
        yield return Say(T("모험의 서를 불러옵니다……", "Loading your adventure..."), false);
        yield return new WaitForSecondsRealtime(0.5f);
        GameStartRequest.Clear();
        GameStartRequest.mode = GameStartRequest.Mode.Continue;
        GameStartRequest.slot = i;
        EncounterTransition.LoadSceneWithFade(d != null && !string.IsNullOrEmpty(d.sceneName) ? d.sceneName : dungeonScene);
    }

    private bool AnyUsed()
    {
        for (int i = 0; i < SaveSystem.SlotCount; i++) if (SaveSystem.HasSaveIn(i)) return true;
        return false;
    }

    private void RefreshSlots()
    {
        bool any = AnyUsed();
        _menuRows[0].color = any ? Cream : Disabled;
        _menuRows[2].color = any ? Cream : Disabled;
        for (int i = 0; i < _slotRows.Count; i++)
        {
            var r = _slotRows[i];
            var d = SaveSystem.Peek(i);
            r.used = d != null;
            r.no.text = $"{i + 1}:";
            if (d == null)
            {
                r.name.text = T("비어 있음", "Unused");
                r.lv.text = r.place.text = r.time.text = "";
                r.no.color = r.name.color = Disabled;
                continue;
            }
            r.no.color = r.name.color = Cream;
            r.name.text = JobDisplayName(d.job);
            r.lv.text = $"Lv. {Mathf.Max(1, d.heroLevel)}";
            r.place.text = $"B{Mathf.Max(1, d.currentFloor)}";
            r.time.text = SaveSystem.FormatPlayTime(d.playSeconds);
        }
    }

    private string JobDisplayName(string jobAsset)
    {
        foreach (var c in Resources.LoadAll<CharacterClass>("Classes"))
            if (c != null && c.name == jobAsset) return JobName(c);
        return string.IsNullOrEmpty(jobAsset) ? "—" : jobAsset.Replace("Class_", "");
    }

    private string JobName(CharacterClass c)
    {
        if (c == null) return "—";
        if (KR)
            switch (c.className)
            {
                case "Warrior": return "전사";
                case "Monk": return "몽크";
                case "Wizard": case "Mage": return "마법사";
                case "Thief": return "도적";
                case "Sister": return "수녀";
            }
        return c.className;
    }

    // ─────────────────────────────────────────
    // 새 게임 — 캐릭터 만들기
    // ─────────────────────────────────────────

    private void StartCreate(int slot)
    {
        _busy = true;
        _nav = null;
        StartCoroutine(CreateRoutine(slot));
    }

    private IEnumerator CreateRoutine(int slot)
    {
        // 어둠 → 장 제목 자막
        _createLayer.gameObject.SetActive(true);
        _createLayer.SetAsLastSibling();
        _msgWindow.SetAsLastSibling();
        _yesNo.SetAsLastSibling();
        HideMessage();
        yield return Fade(_black, 0f, 1f, 0.8f);
        _logLayer.gameObject.SetActive(false);

        _caption.text = T("제1장 · 성배의 서약", "Chapter I · The Chalice's Oath");
        yield return FadeText(_caption, 0f, 1f, 1.2f);
        yield return new WaitForSecondsRealtime(1.6f);
        yield return FadeText(_caption, 1f, 0f, 1.0f);

        // 해설
        _msgWindow.gameObject.SetActive(true);
        yield return Say(T("……눈을 떴다.", "...Your eyes open."));
        yield return Say(T("그러나 아무것도 보이지 않는다.\n차갑고 축축한 돌바닥의 감촉만이 손끝에 닿는다.", "Yet you see nothing.\nOnly cold, damp stone beneath your fingertips."));
        yield return Say(T("이름도, 걸어온 길도 떠오르지 않는다.\n……기억나는 것은……", "Your name, the road behind you — gone.\n...All you remember is..."));

        // [2026-10-14] 기억 → 성향 → 직업 → 서약 → 최종 상태창 "정말 이대로가 맞습니까?" — 아니오면 기억부터 다시
        var pickedMemories = new List<MemoryOfSpeciesData>();
        DispositionData disposition = null;
        CharacterClass job = null;
        AbyssdawnBattle.OathBindingData oath = null;
        while (true)
        {
            // 1) 종의 기억 3개
            while (true)
            {
                pickedMemories.Clear();
                yield return PickMemories(pickedMemories);
                bool? ok = null;
                string names = string.Join(", ", pickedMemories.ConvertAll(m => MemoryName(m)));
                AskYesNo(T($"{names}.\n이 기억이 틀림없습니까?", $"{names}.\nAre these your memories?"), y => ok = y);
                while (ok == null) yield return null;
                if (ok.Value) break;
            }
            HideChoice();
            yield return Say(T("……그래. 이 몸은 분명 나의 것이었다.", "...Yes. This body was surely your own."));
            yield return TraitAwakening(TraitFor(pickedMemories)); // [2026-10-15] 같은 갈래 3개면 깨어나는 장면

            // 2) 성향
            yield return Say(T("그리고 마음 깊은 곳에 남아 있는 것은……", "And deep within your heart remains..."));
            disposition = null;
            yield return PickDisposition(d => disposition = d);
            HideChoice();

            // 3) 직업 (기초 장비도 보여 줌)
            yield return Say(T("손바닥의 굳은살이 말해 준다. 나는……", "The calluses on your palms tell you. You were..."));
            job = null;
            while (job == null)
            {
                CharacterClass picked = null;
                yield return PickJob(c => picked = c);
                bool? ok = null;
                var jobDetail = DetailPopup(picked.classIcon, JobName(picked), JobDescription(picked)); // [2026-10-15] 상세 팝업
                string gear = GearNames(picked);
                AskYesNo(T($"{Ro(JobName(picked))} 살아왔다." + (gear.Length > 0 ? $"\n<size=80%>손에 남은 것: {gear}</size>" : "") + "\n이것이 맞습니까?",
                           $"You lived as a {JobName(picked)}." + (gear.Length > 0 ? $"\n<size=80%>You still hold: {gear}</size>" : "") + "\nIs that right?"), y => ok = y);
                while (ok == null) yield return null;
                if (jobDetail != null) Destroy(jobDetail.gameObject);
                if (ok.Value) job = picked;
            }
            HideChoice();

            // 4) 서약 (직업에 서약이 있으면 2개 중 1개)
            oath = null;
            if (job.oaths != null && job.oaths.Count > 0)
            {
                // [2026-10-17] 서약은 고르지 않아도 된다 — 나중에 성소에서 파기·재서약 가능
                bool? remember = null;
                AskYesNo(T("서약이 기억나는가?", "Do you remember an oath?") + "\n<size=70%><color=#AAAAAA>"
                         + T("서약을 하든 하지 않든, 나중에 성소에서 서약을 파기하거나 다시 맹세할 수 있다.",
                             "Whether you swear or not, you can break or renew an oath later at a sanctuary.") + "</color></size>", y => remember = y);
                while (remember == null) yield return null;
                if (!remember.Value) yield return Say(T("……맹세는 떠오르지 않았다. 아무것에도 묶이지 않은 채 나아간다.", "...No vow surfaces. You go on, bound to nothing."));
                else yield return Say(T("몸에 새겨진 맹세가 희미하게 떠오른다……", "A vow carved into your body faintly surfaces..."));
                while (remember.Value && oath == null)
                {
                    AbyssdawnBattle.OathBindingData picked = null;
                    yield return PickOath(job, o => picked = o);
                    bool? ok = null;
                    AskYesNo($"<color=#D4A761>{picked.oathName}</color>\n<size=75%><color=#FF9F6B>{T("제약", "Binding")}: {picked.bindingDescription}</color></size>\n" + T("이 서약을 지키겠습니까?", "Will you keep this oath?"), y => ok = y);
                    while (ok == null) yield return null;
                    if (ok.Value) oath = picked;
                }
                HideChoice();
            }

            // 5) 최종 상태창 + 확인
            ShowFinalStatus(pickedMemories, disposition, job, oath);
            bool? final = null;
            AskYesNo(T("정말 이대로가 맞습니까?", "Is this truly who you are?"), y => final = y);
            while (final == null) yield return null;
            HideFinalStatus();
            if (final.Value) break;
            yield return Say(T("……기억이 다시 흐려진다. 처음부터 더듬어 본다.", "...The memory blurs again. You search from the beginning."));
        }
        yield return Say(T("기억의 조각을 끌어안고, 어둠 속에서 몸을 일으켰다.", "Clutching the shards of memory, you rise in the dark."));

        // 던전으로
        GameStartRequest.Clear();
        GameStartRequest.mode = GameStartRequest.Mode.NewGame;
        GameStartRequest.slot = slot;
        GameStartRequest.memories.AddRange(pickedMemories);
        GameStartRequest.job = job;
        GameStartRequest.disposition = disposition;
        GameStartRequest.oath = oath;
        HideMessage();
        EncounterTransition.LoadSceneWithFade(dungeonScene);
    }

    // ── 최종 상태창 (고른 것 전부 + 시작 장비를 입은 최종 능력치) ──
    private RectTransform _finalWindow;

    private void ShowFinalStatus(List<MemoryOfSpeciesData> mems, DispositionData disp, CharacterClass job, AbyssdawnBattle.OathBindingData oath)
    {
        HideFinalStatus();
        PlayerStats ps = MakePreview(mems, job, oath, out GameObject tmp);
        float top = 140f, h = 1920f - 90f - 330f - 40f - top;
        _finalWindow = Window("FinalStatus", _createLayer, 60f, top, 960f, h);
        Text(_finalWindow, T("나의 모습", "Who You Are"), 0f, 34f, 960f, 70f, 46f, Gold, TextAlignmentOptions.Center);
        var line = NewImage("Divider", _finalWindow, null); Place(line.rectTransform, 60f, 112f, 840f, 2f); line.color = new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.6f);

        // 왼쪽: 고른 것들
        float y = 140f;
        System.Action<string, string> Row = (k, v) =>
        {
            Text(_finalWindow, k, 70f, y, 200f, 50f, 32f, GoldDim, TextAlignmentOptions.TopLeft);
            var t = Text(_finalWindow, v, 250f, y, 650f, 50f, 34f, Cream, TextAlignmentOptions.TopLeft);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.ForceMeshUpdate();
            y += Mathf.Max(56f, t.preferredHeight + 14f);
        };
        string tn = job != null ? (KR && !string.IsNullOrEmpty(job.traitNameKR) ? job.traitNameKR : job.traitName) : "";
        Row(T("직업", "Job"), JobName(job) + (string.IsNullOrEmpty(tn) ? "" : $"  <size=80%><color=#D4A761>({tn})</color></size>"));
        if (oath != null) Row(T("서약", "Oath"), oath.oathName + $"\n<size=75%><color=#FF9F6B>{T("제약", "Binding")}: {oath.bindingDescription}</color></size>");
        Row(T("성향", "Disposition"), disp != null ? DispName(disp) : T("없음", "None"));
        string memNames = string.Join(", ", mems.ConvertAll(m => MemoryName(m)));
        var trait = ps.statData != null ? ps.statData.activeTrait : null;
        Row(T("종의 기억", "Memories"), memNames + (trait != null ? $"\n<size=80%><color=#D4A761>{T("종의 특성", "Species Trait")}: {(KR ? trait.traitName : trait.traitNameEnglish)}</color></size>" : ""));
        string gear = GearNames(job);
        Row(T("장비", "Gear"), gear.Length > 0 ? gear : T("없음", "None"));
        // [2026-10-18] 시작 소모품
        var items = new List<string>();
        if (job != null && job.startingConsumables != null)
            foreach (var it in job.startingConsumables)
                if (it != null && it.item != null && it.count > 0) items.Add($"{it.item.itemName} ×{it.count}");
        Row(T("소지품", "Items"), items.Count > 0 ? string.Join(", ", items) : T("없음", "None"));

        // 아래: 능력치 (시작 장비·기억·직업 모두 적용된 값)
        y += 10f;
        var line2 = NewImage("Divider2", _finalWindow, null); Place(line2.rectTransform, 60f, y, 840f, 2f); line2.color = new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.4f);
        y += 20f;
        string[] keys = { "HP", "MP", "STR", "DEF", "MAG", "AGI", "LUK" };
        int[] vals = { ps.maxHP, ps.maxMP, ps.Attack, ps.Defense, ps.Magic, ps.Agility, ps.Luck };
        for (int i = 0; i < keys.Length; i++)
        {
            float cx = i % 2 == 0 ? 70f : 500f;
            float cy = y + (i / 2) * 58f;
            Text(_finalWindow, keys[i], cx, cy, 200f, 54f, 36f, GoldDim, TextAlignmentOptions.MidlineLeft);
            Text(_finalWindow, vals[i].ToString(), cx + 180f, cy, 200f, 54f, 40f, Cream, TextAlignmentOptions.MidlineRight);
        }
        Destroy(tmp);
        _finalWindow.SetAsLastSibling();
        _msgWindow.SetAsLastSibling();
        _yesNo.SetAsLastSibling();
    }

    private void HideFinalStatus()
    {
        if (_finalWindow != null) Destroy(_finalWindow.gameObject);
        _finalWindow = null;
    }

    /// <summary>원본 HeroData 복사본에 고른 것·시작 장비를 넣고, 꺼진 오브젝트의 PlayerStats 로 게임과 같은 공식으로 계산.</summary>
    private PlayerStats MakePreview(List<MemoryOfSpeciesData> mems, CharacterClass job, AbyssdawnBattle.OathBindingData oath, out GameObject go)
    {
        PlayerStatData data = heroTemplate != null ? Instantiate(heroTemplate) : ScriptableObject.CreateInstance<PlayerStatData>();
        data.currentJob = job;
        data.currentOath = oath;
        data.memorySlot1 = mems.Count > 0 ? mems[0] : null;
        data.memorySlot2 = mems.Count > 1 ? mems[1] : null;
        data.memorySlot3 = mems.Count > 2 ? mems[2] : null;
        data.rightHand = data.leftHand = data.body = data.accessory1 = data.accessory2 = null;
        data.ResetSkillsForNewRun();
        EquipmentBag.EquipStartingGear(data);
        data.CheckAndActivateTrait();
        go = new GameObject("StatPreview");
        go.SetActive(false); // Awake 를 돌리지 않음 (주인공 등록 등 부작용 없음)
        var ps = go.AddComponent<PlayerStats>();
        ps.statData = data;
        ps.characterClass = job;
        return ps;
    }

    // ── 서약 고르기 ──
    private IEnumerator PickOath(CharacterClass job, Action<AbyssdawnBattle.OathBindingData> onPicked)
    {
        AbyssdawnBattle.OathBindingData tapped = null;
        ChoiceWindow(out RectTransform list, out TextMeshProUGUI header, job.oaths.Count, withDecide: false, out _, out _, rowH: 230f);
        header.text = T("서약", "Oath");
        RectTransform cursor = NewCursor(list);
        for (int i = 0; i < job.oaths.Count; i++)
        {
            var o = job.oaths[i];
            var row = ChoiceRow(list, i, null, o.oathName, $"{o.oathDescription}\n<color=#FF9F6B>{T("제약", "Binding")}: {o.bindingDescription}</color>", "", out _);
            row.onClick.AddListener(() => { PlaceCursor(cursor, (RectTransform)row.transform, 38f); tapped = o; ShowMessage(OathDetail(o)); });
            _choiceNav.Add(row);
        }
        SetNav(_choiceNav, cursor, 38f, 0, null);
        ShowMessage(T("지킬 서약을 고르시오. 서약에는 제약이 따른다.", "Choose your oath. Every oath carries a binding."));
        while (tapped == null) yield return null;
        onPicked(tapped);
    }

    private static string OathLine(AbyssdawnBattle.OathBindingData o) => (o.oathDescription ?? "").Replace("\n", "  ·  ");

    private string OathDetail(AbyssdawnBattle.OathBindingData o)
    {
        string strength = o.bindingStrength.ToString();
        return $"<color=#D4A761>{o.oathName}</color>\n<size=80%>{o.oathDescription}</size>\n<size=80%><color=#FF9F6B>{T("제약", "Binding")} ({strength}): {o.bindingDescription}</color></size>";
    }

    private string GearNames(CharacterClass c)
    {
        if (c == null || c.startingEquipment == null) return "";
        var names = new List<string>();
        foreach (var e in c.startingEquipment) if (e != null) names.Add(e.equipmentName);
        return string.Join(", ", names);
    }

    // ── 종의 기억 고르기 (여러 개) — [2026-10-14] 종족 탭 (인간 · 엘프 · 오크), 섞어서 고를 수 있음 ──
    private Action _tabPrev, _tabNext; // 키보드 A/D · ←/→

    private IEnumerator PickMemories(List<MemoryOfSpeciesData> result)
    {
        var chosen = new List<MemoryOfSpeciesData>();
        bool done = false;

        // 종족별로 나누기 (데이터 순서 유지)
        var species = new List<SpeciesType>();
        foreach (var m in memoryChoices) if (m != null && !species.Contains(m.species)) species.Add(m.species);
        int maxRows = 0;
        foreach (var sp in species) { int c = memoryChoices.FindAll(x => x != null && x.species == sp).Count; if (c > maxRows) maxRows = c; }

        // [2026-10-18] 모든 갈래를 한 화면에 — 갈래마다 금테 하나, 그 안에 아이콘 2·1·2 (주사위 5 모양)
        //   왼쪽 위 = 갈래 Ⅰ(인간), 오른쪽 위 = Ⅱ(엘프), 왼쪽 아래 = Ⅲ(오크). 누르면 상세 팝업 → "이걸로 고르시겠습니까?"
        const float groupW = 440f, groupH = 516f, groupGap = 18f;
        int groupRows = Mathf.Max(1, (species.Count + 1) / 2);
        float listH = groupRows * groupH + (groupRows - 1) * groupGap;
        ChoiceWindow(out RectTransform list, out TextMeshProUGUI header, 1, withDecide: true, out Button decide, out TextMeshProUGUI decideText, rowH: listH);
        MemoryOfSpeciesData tappedMem = null;

        RectTransform cursor = null;
        var shown = new List<MemoryOfSpeciesData>();
        var tints = new List<Image>();
        Action refresh = () =>
        {
            header.text = T($"종의 기억  ({chosen.Count}/{memoriesToPick})", $"Memories  ({chosen.Count}/{memoriesToPick})");
            for (int i = 0; i < shown.Count; i++) tints[i].color = chosen.Contains(shown[i]) ? SelectTint : new Color(0, 0, 0, 0);
            decide.interactable = chosen.Count == memoriesToPick;
            decideText.color = decide.interactable ? Gold : Disabled;
        };
        cursor = NewCursor(list);
        const float cell = 112f, nameH = 34f, colGap = 76f, rowStride = 152f, padTop = 46f;
        for (int g = 0; g < species.Count; g++)
        {
            float gx = 30f + (g % 2) * (groupW + groupGap), gy = (g / 2) * (groupH + groupGap);
            var group = NewImage("MemoryGroupFrame", list, frame); group.type = Image.Type.Sliced; group.pixelsPerUnitMultiplier = 3f;
            Place(group.rectTransform, gx, gy, groupW, groupH);
            var label = Text(list, BranchName(g), gx + 18f, gy + 6f, 90f, 34f, 22f, GoldDim, TextAlignmentOptions.MidlineLeft);
            var members = memoryChoices.FindAll(x => x != null && x.species == species[g]);
            for (int k = 0; k < members.Count && k < 5; k++)
            {
                var m = members[k];
                // 2 · 1 · 2 배치
                int row = k < 2 ? 0 : (k == 2 ? 1 : 2);
                float side = (k == 0 || k == 3) ? -1f : (k == 2 ? 0f : 1f); // 왼쪽 · 가운데 · 오른쪽
                float cx = groupW * 0.5f + side * (cell + colGap) * 0.5f;
                float x = gx + cx - cell * 0.5f, y = gy + padTop + row * rowStride;
                var tint = NewImage("Tint", list, null); Place(tint.rectTransform, x - 10f, y - 6f, cell + 20f, cell + nameH + 12f);
                tint.color = new Color(0, 0, 0, 0);
                shown.Add(m); tints.Add(tint);
                var box = NewImage("IconBox", list, frame); box.type = Image.Type.Sliced; box.pixelsPerUnitMultiplier = 3f;
                Place(box.rectTransform, x, y, cell, cell);
                if (m.memoryIcon != null)
                {
                    var ic = NewImage("Icon", box.rectTransform, m.memoryIcon); Stretch(ic.rectTransform);
                    ic.rectTransform.offsetMin = new Vector2(7f, 7f); ic.rectTransform.offsetMax = new Vector2(-7f, -7f);
                    ic.preserveAspect = true;
                }
                var nm = Text(list, MemoryName(m), x - 34f, y + cell + 2f, cell + 68f, nameH, 26f, Cream, TextAlignmentOptions.Center);
                nm.enableAutoSizing = true; nm.fontSizeMin = 16f; nm.fontSizeMax = 26f;
                var hit = Hit(list, x - 10f, y - 6f, cell + 20f, cell + nameH + 12f);
                _choiceNav.Add(hit);
                hit.onClick.AddListener(() => { PlaceCursor(cursor, (RectTransform)hit.transform, 30f); tappedMem = m; });
            }
        }
        _choiceNav.Add(decide);
        SetNav(_choiceNav, cursor, 30f, 0, null);
        decide.onClick.AddListener(() => { if (chosen.Count == memoriesToPick) done = true; });
        _tabPrev = _tabNext = null;
        refresh();
        // [2026-10-15] 종족 이름은 쓰지 않는다 — 기억을 잃은 용사가 몸에 남은 조각을 더듬는 것 (갈래 I·II·III)
        ShowMessage(T($"기억을 잃었다. 몸에 남은 기억의 조각을 {memoriesToPick}가지 고르시오.\n<size=75%><color=#AAAAAA>갈래를 섞어도 된다. 같은 갈래 3개면 무언가가 깨어난다.</color></size>",
                      $"Your memory is lost. Choose {memoriesToPick} fragments your body still remembers.\n<size=75%><color=#AAAAAA>You may mix branches. Three of one branch awaken something within.</color></size>"));
        while (!done)
        {
            if (tappedMem != null)
            {
                // [2026-10-17] 상세 팝업 (큰 그림 + 이름 + 수치·설명) → 고를지 / 내려놓을지 묻는다
                var m = tappedMem; tappedMem = null;
                string flavor = !string.IsNullOrEmpty(m.flavorText) ? m.flavorText : m.description;
                var detail = DetailPopup(m.memoryIcon, MemoryName(m),
                    $"<color=#8F7B54>{m.subtitle}</color>\n\n" + StatLines(m) + (string.IsNullOrEmpty(flavor) ? "" : $"\n\n<color=#AAAAAA>{flavor}</color>"));
                bool has = chosen.Contains(m);
                bool? ok = null;
                AskYesNo(has ? T("이 기억을 내려놓겠습니까?", "Let go of this memory?") : T("이걸로 고르시겠습니까?", "Choose this one?"), y => ok = y);
                while (ok == null) yield return null;
                if (detail != null) Destroy(detail.gameObject);
                if (ok.Value)
                {
                    if (has) chosen.Remove(m);
                    else if (chosen.Count < memoriesToPick) chosen.Add(m);
                    else ShowMessage(T($"기억은 {memoriesToPick}가지까지만 떠올릴 수 있다.", $"You can recall only {memoriesToPick} memories."));
                }
                if (ok.Value && (has || chosen.Contains(m))) ShowMessage(MemoryDetail(m) + SetHint(chosen));
                refresh();
            }
            yield return null;
        }
        _tabPrev = _tabNext = null;
        result.AddRange(chosen);
    }

    /// <summary>같은 종족 3개면 깨어나는 종의 특성 안내.</summary>
    private string SetHint(List<MemoryOfSpeciesData> chosen)
    {
        if (chosen.Count != memoriesToPick) return "";
        var sp = chosen[0].species;
        foreach (var m in chosen) if (m.species != sp) return $"\n<size=75%><color=#AAAAAA>{T("섞인 기억 — 깨어나는 것은 없다", "Mixed memories — nothing awakens")}</color></size>";
        var t = TraitFor(chosen);
        return t != null ? $"\n<size=75%><color=#D4A761>{T("종의 특성", "Species Trait")}: {(KR ? t.traitName : t.traitNameEnglish)}</color></size>" : "";
    }

    /// <summary>같은 갈래 3개면 그 특성, 아니면 null.</summary>
    private AbyssdawnBattle.TraitsOfSpeciesData TraitFor(List<MemoryOfSpeciesData> chosen)
    {
        if (chosen == null || chosen.Count != memoriesToPick || chosen[0] == null) return null;
        var sp = chosen[0].species;
        foreach (var m in chosen) if (m == null || m.species != sp) return null;
        return Resources.Load<AbyssdawnBattle.TraitsOfSpeciesData>(sp == SpeciesType.Human ? "Traits/Human_LastStand" : $"Traits/{sp}_Trait");
    }

    /// <summary>[2026-10-15] 갈래 탭 이름 — 문양 + 로마 숫자 (종족 이름 대신).</summary>
    private static string BranchName(int i)
    {
        string[] n = { "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ" };
        return "◆ " + (i < n.Length ? n[i] : (i + 1).ToString());
    }

    /// <summary>[2026-10-15] 종의 특성이 깨어나는 장면 — 화면이 어두워지고 특성 그림이 떠오르며 이름이 새겨진다.</summary>
    private IEnumerator TraitAwakening(AbyssdawnBattle.TraitsOfSpeciesData t)
    {
        if (t == null) yield break;
        var veil = NewImage("AwakenVeil", _createLayer, null); Stretch(veil.rectTransform);
        veil.color = new Color(0f, 0f, 0f, 0f); veil.raycastTarget = true;
        Image icon = null;
        if (t.traitIcon != null)
        {
            icon = NewImage("AwakenIcon", _createLayer, t.traitIcon);
            icon.preserveAspect = true;
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460f, 460f); rt.anchoredPosition = new Vector2(0f, 360f);
            icon.color = new Color(1f, 1f, 1f, 0f);
        }
        var name = Text(_createLayer, KR ? t.traitName : t.traitNameEnglish, 0f, 0f, 1080f, 110f, 66f, Gold, TextAlignmentOptions.Center);
        var nrt = name.rectTransform; nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0.5f); nrt.anchoredPosition = new Vector2(0f, 60f);
        name.characterSpacing = 10f; name.alpha = 0f;

        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 0.6f) { veil.color = new Color(0f, 0f, 0f, 0.88f * a); yield return null; }
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 1.1f)
        {
            float e = 1f - (1f - a) * (1f - a);
            if (icon != null) { icon.color = new Color(1f, 1f, 1f, e); icon.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, e); }
            name.alpha = Mathf.Clamp01(a * 1.6f - 0.6f);
            yield return null;
        }
        if (icon != null) { icon.color = Color.white; icon.rectTransform.localScale = Vector3.one; }
        name.alpha = 1f;
        yield return Say(T($"몸 깊은 곳에서 무언가가 깨어난다……\n<color=#D4A761>종의 특성 · {t.traitName}</color>",
                           $"Something stirs deep within your body...\n<color=#D4A761>Trait · {t.traitNameEnglish}</color>"));
        for (float a = 1f; a > 0f; a -= Time.unscaledDeltaTime / 0.6f)
        {
            veil.color = new Color(0f, 0f, 0f, 0.88f * a);
            if (icon != null) icon.color = new Color(1f, 1f, 1f, a);
            name.alpha = a;
            yield return null;
        }
        Destroy(veil.gameObject); if (icon != null) Destroy(icon.gameObject); Destroy(name.gameObject);
    }

    private string SpeciesName(SpeciesType sp)
    {
        if (!KR) return sp.ToString();
        switch (sp)
        {
            case SpeciesType.Human: return "인간";
            case SpeciesType.Elf: return "엘프";
            case SpeciesType.Orc: return "오크";
            case SpeciesType.Dwarf: return "드워프";
            case SpeciesType.Halfling: return "하플링";
        }
        return sp.ToString();
    }

    // ── 성향 고르기 (데이터가 없으면 "아직 준비 중" 한 줄로 지나감) ──
    private IEnumerator PickDisposition(Action<DispositionData> onPicked)
    {
        if (dispositionChoices == null || dispositionChoices.Count == 0)
        {
            ChoiceWindow(out RectTransform list, out TextMeshProUGUI header, 1, withDecide: false, out _, out _);
            header.text = T("성향", "Disposition");
            ChoiceRow(list, 0, null, T("—  준비 중  —", "—  Coming soon  —"), T("성향은 아직 정해지지 않았습니다.", "Dispositions are not yet defined."), "", out _).interactable = false;
            yield return Say(T("……마음은 아직 짙은 안개 속에 있다.", "...Your heart still lies in a thick fog."));
            onPicked(null);
            yield break;
        }
        DispositionData picked = null;
        while (picked == null)
        {
            DispositionData tapped = null;
            ChoiceWindow(out RectTransform list, out TextMeshProUGUI header, dispositionChoices.Count, withDecide: false, out _, out _, rowH: 112f);
            header.text = T("성향", "Disposition");
            RectTransform cursor = NewCursor(list);
            for (int i = 0; i < dispositionChoices.Count; i++)
            {
                var d = dispositionChoices[i];
                var row = ChoiceRow(list, i, d.icon, DispName(d), "", "", out _); // [2026-10-14] 목록엔 종류만 — 누르면 상세 팝업
                row.onClick.AddListener(() => { PlaceCursor(cursor, (RectTransform)row.transform, 38f); tapped = d; });
                _choiceNav.Add(row);
            }
            SetNav(_choiceNav, cursor, 38f, 0, null);
            ShowMessage(T("마음 깊은 곳의 성향을 고르시오.", "Choose your disposition."));
            while (tapped == null) yield return null;
            bool? ok = null;
            // [2026-10-14] 상세 팝업 (그림 · 이름 · 얻는 것 / 잃는 것) + 오른쪽 아래 "이게 맞습니까?"
            var detail = DispositionDetail(tapped);
            AskYesNo($"<color=#D4A761>{DispName(tapped)}</color>\n{T("이게 맞습니까?", "Is this right?")}", y => ok = y);
            while (ok == null) yield return null;
            if (detail != null) Destroy(detail.gameObject);
            if (ok.Value) picked = tapped;
        }
        onPicked(picked);
    }

    /// <summary>성향 상세 팝업 — 고르기 창 위에 큰 그림 + 이름 + 설명.</summary>
    private RectTransform DispositionDetail(DispositionData d)
        => d == null ? null : DetailPopup(d.icon, DispName(d), DispDesc(d));

    /// <summary>[2026-10-15] 고르기 상세 팝업 (성향·직업 공용). 그림이 없으면 이름 글자를 그림 칸에.</summary>
    private RectTransform DetailPopup(Sprite icon, string title, string body)
    {
        var w = Window("ChoiceDetail", _createLayer, 60f, 150f, 960f, 1300f);
        w.SetAsLastSibling();
        var fill = NewImage("Fill", w, null); Stretch(fill.rectTransform); // 뒤의 목록이 비치지 않게
        fill.rectTransform.offsetMin = new Vector2(10f, 10f); fill.rectTransform.offsetMax = new Vector2(-10f, -10f);
        fill.color = new Color(0.02f, 0.02f, 0.02f, 1f);
        var box = NewImage("IconBox", w, frame); box.type = Image.Type.Sliced; box.pixelsPerUnitMultiplier = 3f;
        Place(box.rectTransform, 260f, 70f, 440f, 550f);
        if (icon != null)
        {
            var ic = NewImage("Icon", box.rectTransform, icon); Stretch(ic.rectTransform);
            ic.rectTransform.offsetMin = new Vector2(12f, 12f); ic.rectTransform.offsetMax = new Vector2(-12f, -12f);
            ic.preserveAspect = true;
        }
        else
        {
            var nt = Text(box.rectTransform, title, 0f, 0f, 440f, 550f, 72f, GoldDim, TextAlignmentOptions.Center);
            nt.textWrappingMode = TextWrappingModes.Normal;
        }
        Text(w, title, 0f, 650f, 960f, 80f, 56f, Gold, TextAlignmentOptions.Center);
        var line = NewImage("Divider", w, null); Place(line.rectTransform, 120f, 745f, 720f, 2f); line.color = new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.6f);
        var desc = Text(w, body, 90f, 775f, 780f, 480f, 36f, Cream, TextAlignmentOptions.Top);
        desc.textWrappingMode = TextWrappingModes.Normal;
        desc.enableAutoSizing = true; desc.fontSizeMin = 22f; desc.fontSizeMax = 36f; // [2026-10-17] 넘치지 않게
        desc.lineSpacing = 14f;
        return w;
    }

    private string DispName(DispositionData d) => d == null ? "" : KR && !string.IsNullOrEmpty(d.dispositionNameKR) ? d.dispositionNameKR : d.dispositionName;
    private string DispDesc(DispositionData d) => d == null ? "" : KR && !string.IsNullOrEmpty(d.descriptionKR) ? d.descriptionKR : d.description;

    // ── 직업 고르기 ──
    private IEnumerator PickJob(Action<CharacterClass> onPicked)
    {
        CharacterClass tapped = null;
        ChoiceWindow(out RectTransform list, out TextMeshProUGUI header, jobChoices.Count, withDecide: false, out _, out _, rowH: 132f);
        header.text = T("직업", "Job");
        RectTransform cursor = NewCursor(list);
        for (int i = 0; i < jobChoices.Count; i++)
        {
            var c = jobChoices[i];
            var row = ChoiceRow(list, i, c != null ? c.classIcon : null, JobName(c), "", "", out _); // [2026-10-15] 목록엔 그림+이름만 — 누르면 상세 팝업
            row.onClick.AddListener(() => { PlaceCursor(cursor, (RectTransform)row.transform, 38f); tapped = c; });
            _choiceNav.Add(row);
        }
        SetNav(_choiceNav, cursor, 38f, 0, null);
        ShowMessage(T("살아온 길을 고르시오.", "Choose the path you walked."));
        while (tapped == null) yield return null;
        onPicked(tapped);
    }

    private string JobDescription(CharacterClass c)
    {
        if (c == null) return "";
        string desc = c.description;
        if (KR)
            switch (c.className)
            {
                case "Warrior": desc = "근접 전투에 특화된 전사."; break;
                case "Thief": desc = "민첩과 행운으로 싸우는 도적."; break;
                case "Monk": desc = "맨몸과 내공으로 싸우는 무도가."; break;
            }
        string tn = KR && !string.IsNullOrEmpty(c.traitNameKR) ? c.traitNameKR : c.traitName;
        string td = KR && !string.IsNullOrEmpty(c.traitDescriptionKR) ? c.traitDescriptionKR : c.traitDescription;
        if (!string.IsNullOrEmpty(tn)) desc += $"\n<color=#D4A761>{T("특성", "Trait")}: {tn}</color> — {td}";
        string gear = GearNames(c);
        if (gear.Length > 0) desc += $"\n<color=#9FC8FF>{T("기초 장비", "Starting gear")}: {gear}</color>";
        return desc;
    }

    private string MemoryName(MemoryOfSpeciesData m)
    {
        if (m == null) return "—";
        // [2026-10-14] 종 이름은 붙이지 않는다 — 그냥 "턱", "입술" (사용자 지시)
        if (!KR) return m.memoryName;
        string part = m.memoryName;
        switch (m.memoryName)
        {
            case "Chin": part = "턱"; break;
            case "Eyes": part = "눈"; break;
            case "Lips": part = "입술"; break;
            case "Shoulder": part = "어깨"; break;
            case "Skin": part = "피부"; break;
            case "Ear": case "Pointed Ear": part = "뾰족한 귀"; break;
            case "Hair": part = "머리카락"; break;
            case "Iris": part = "홍채"; break;
            case "Skeleton": part = m.species == SpeciesType.Elf ? "가벼운 뼈" : "뼈"; break;
            case "Stomach": part = "위장"; break;
            case "Tusk": part = "엄니"; break;
            case "SubcutaneousFat": part = "피하지방"; break;
            case "RedBloodCells": part = "적혈구"; break;
            case "KneeJoint": part = "무릎 관절"; break;
            case "Greenskin": part = "그린스킨"; break;
        }
        return part;
    }

    private string MemorySubtitle(MemoryOfSpeciesData m) => m != null ? m.subtitle : "";

    private string MemoryDetail(MemoryOfSpeciesData m)
    {
        if (m == null) return "";
        string flavor = !string.IsNullOrEmpty(m.flavorText) ? m.flavorText : m.description;
        return $"<color=#D4A761>{MemoryName(m)}</color>  <size=80%><color=#8F7B54>{m.subtitle}</color></size>\n<size=85%>{StatLines(m)}</size>"
             + (string.IsNullOrEmpty(flavor) ? "" : $"\n<size=75%><color=#AAAAAA>{flavor}</color></size>");
    }

    // ─────────────────────────────────────────
    // 대화창 · 예/아니오
    // ─────────────────────────────────────────

    private void ShowMessage(string text)
    {
        _msgWindow.gameObject.SetActive(true);
        _msgText.text = text;
        _msgText.maxVisibleCharacters = 99999;
        if (_yesNoAnswer == null) _msgText.rectTransform.sizeDelta = new Vector2(840f, _msgText.rectTransform.sizeDelta.y);
        MsgScrollTop();
        _waitingTap = false;
    }

    private void HideMessage()
    {
        _msgText.text = "";
        _msgWindow.gameObject.SetActive(false);
        _yesNo.gameObject.SetActive(false);
    }

    /// <summary>한 글자씩 → (waitTap 이면) ▼ 가 깜빡이고 대화창을 누르면 다음으로.</summary>
    private IEnumerator Say(string text, bool waitTap = true)
    {
        _msgWindow.gameObject.SetActive(true);
        _msgText.text = text;
        _msgText.ForceMeshUpdate();
        MsgScrollTop();
        int total = _msgText.textInfo.characterCount;
        _typing = true; _tapped = false;
        for (int i = 0; i <= total; i++)
        {
            if (_tapped) { i = total; _tapped = false; }
            _msgText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(0.035f);
        }
        _msgText.maxVisibleCharacters = 99999;
        _typing = false;
        if (!waitTap) yield break;
        _waitingTap = true; _tapped = false;
        while (!_tapped) yield return null;
        _waitingTap = false; _tapped = false;
    }

    private void OnMessageTap()
    {
        if (_typing || _waitingTap) _tapped = true;
    }

    private void AskYesNo(string question, Action<bool> answer)
    {
        ShowMessage(question);
        _msgText.rectTransform.sizeDelta = new Vector2(580f, _msgText.rectTransform.sizeDelta.y); // 오른쪽 예/아니오 자리 비움
        _yesNoAnswer = answer;
        _yesIdx = 0;
        _yesNo.gameObject.SetActive(true);
        _yesNo.SetAsLastSibling();
        PlaceCursor(_yesNoCursor, _yesText.rectTransform);
    }

    private void OnYesNo(bool yes)
    {
        PlaceCursor(_yesNoCursor, (yes ? _yesText : _noText).rectTransform);
        _yesNo.gameObject.SetActive(false);
        _msgText.rectTransform.sizeDelta = new Vector2(840f, _msgText.rectTransform.sizeDelta.y);
        var a = _yesNoAnswer; _yesNoAnswer = null;
        a?.Invoke(yes);
    }

    // ─────────────────────────────────────────
    // 화면 만들기
    // ─────────────────────────────────────────

    private void BuildCanvas()
    {
        var cgo = new GameObject("PrologueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f; // 가로 폭에 맞춤 (던전 씬과 같게)
        _root = (RectTransform)cgo.transform;

        // 배경: 타이틀과 같은 던전 복도를 더 어둡게
        var bg = NewImage("Background", _root, background);
        Stretch(bg.rectTransform);
        var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = background != null ? background.rect.width / background.rect.height : 0.5625f;
        var dim = NewImage("Dim", _root, null);
        Stretch(dim.rectTransform);
        dim.color = new Color(0.02f, 0.016f, 0.012f, 0.55f);
    }

    private void BuildLogScreen()
    {
        _logLayer = NewRect("LogLayer", _root); Stretch(_logLayer);

        // 위: 메뉴
        var menu = Window("MenuWindow", _logLayer, 60f, 150f, 960f, 330f);
        string[] items =
        {
            T("모험을 계속한다.", "Continue your adventure."),
            T("새로운 모험의 서를 만든다.", "Create a new adventure log."),
            T("모험의 서를 지운다.", "Delete an adventure log."),
        };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            _menuRows[i] = Text(menu, items[i], 120f, 52f + i * 82f, 800f, 76f, 46f, Cream, TextAlignmentOptions.MidlineLeft);
            var mh = Hit(menu, 40f, 52f + i * 82f, 880f, 76f);
            mh.onClick.AddListener(() => { if (!_busy && _yesNoAnswer == null) { _navIndex = idx; SelectMenu((MenuMode)idx, true); } });
            _menuHits.Add(mh);
        }
        _menuCursor = NewCursor(menu);

        // 가운데: 모험의 서 목록
        var log = Window("LogWindow", _logLayer, 60f, 520f, 960f, 560f);
        var head = Text(log, T("모험의 서", "Adventure Log"), 0f, 34f, 960f, 70f, 46f, Gold, TextAlignmentOptions.Center);
        if (headingFont != null && !KR) head.font = headingFont;
        var line = NewImage("Divider", log, null); Place(line.rectTransform, 60f, 112f, 840f, 2f); line.color = new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.6f);
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int idx = i;
            float y = 140f + i * 128f;
            var r = new SlotRow
            {
                no = Text(log, $"{i + 1}:", 110f, y, 80f, 110f, 44f, Cream, TextAlignmentOptions.MidlineLeft),
                name = Text(log, "", 180f, y, 260f, 110f, 44f, Cream, TextAlignmentOptions.MidlineLeft),
                lv = Text(log, "", 430f, y, 200f, 110f, 42f, Cream, TextAlignmentOptions.MidlineLeft),
                place = Text(log, "", 630f, y, 120f, 110f, 42f, Cream, TextAlignmentOptions.MidlineLeft),
                time = Text(log, "", 740f, y, 170f, 110f, 42f, Cream, TextAlignmentOptions.MidlineRight),
            };
            r.button = Hit(log, 40f, y, 880f, 110f);
            _slotHits.Add(r.button);
            r.button.onClick.AddListener(() => { if (_yesNoAnswer == null) OnSlot(idx); });
            _slotRows.Add(r);
        }
        _slotCursor = NewCursor(log);
        _slotCursor.gameObject.SetActive(false);

        // 타이틀로
        var back = Text(_logLayer, T("◀ 타이틀로", "◀ Title"), 60f, 60f, 300f, 70f, 36f, Muted, TextAlignmentOptions.MidlineLeft);
        Hit(_logLayer, 50f, 50f, 300f, 90f).onClick.AddListener(() => { if (!_busy) EncounterTransition.LoadSceneWithFade(titleScene); });
    }

    private void BuildMessageWindow()
    {
        // 아래 대화창 (화면 아래에 붙음)
        _msgWindow = NewRect("MessageWindow", _root);
        _msgWindow.anchorMin = new Vector2(0f, 0f); _msgWindow.anchorMax = new Vector2(1f, 0f); _msgWindow.pivot = new Vector2(0.5f, 0f);
        _msgWindow.offsetMin = new Vector2(60f, 90f); _msgWindow.offsetMax = new Vector2(-60f, 90f + 330f);
        var img = _msgWindow.gameObject.AddComponent<Image>();
        img.sprite = frame; img.type = Image.Type.Sliced; img.raycastTarget = true;
        var btn = _msgWindow.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnMessageTap);
        // [2026-10-18] 글이 넘치면 대화창 안에서 스크롤 (휠·끌기). 누르기는 그대로 대화창 버튼으로 간다.
        var vp = NewRect("MsgViewport", _msgWindow); Place(vp, 60f, 44f, 860f, 248f);
        vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0f);
        _msgText = Text(vp, "", 0f, 0f, 840f, 240f, 44f, Cream, TextAlignmentOptions.TopLeft);
        _msgText.textWrappingMode = TextWrappingModes.Normal;
        _msgText.lineSpacing = 12f;
        _msgText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _msgScroll = vp.gameObject.AddComponent<ScrollRect>();
        _msgScroll.content = _msgText.rectTransform; _msgScroll.viewport = vp;
        _msgScroll.horizontal = false; _msgScroll.vertical = true;
        _msgScroll.movementType = ScrollRect.MovementType.Clamped; _msgScroll.scrollSensitivity = 30f;
        var next = NewImage("Next", _msgWindow, _arrow);
        next.color = Gold;
        next.rectTransform.anchorMin = next.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        next.rectTransform.sizeDelta = new Vector2(34f, 34f);
        next.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        next.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f); // ▼
        _msgNext = next.rectTransform;
        _msgNext.gameObject.SetActive(false);

        // 예 / 아니오 (대화창 오른쪽 위)
        _yesNo = NewRect("YesNo", _root);
        _yesNo.anchorMin = _yesNo.anchorMax = new Vector2(1f, 0f); _yesNo.pivot = new Vector2(1f, 0f);
        _yesNo.sizeDelta = new Vector2(280f, 250f); _yesNo.anchoredPosition = new Vector2(-80f, 90f + 40f); // [2026-10-14] 대화창 안 오른쪽
        var yimg = _yesNo.gameObject.AddComponent<Image>(); yimg.sprite = frame; yimg.type = Image.Type.Sliced;
        _yesText = Text(_yesNo, T("예", "Yes"), 120f, 50f, 180f, 70f, 46f, Cream, TextAlignmentOptions.MidlineLeft);
        _noText = Text(_yesNo, T("아니오", "No"), 120f, 130f, 180f, 70f, 46f, Cream, TextAlignmentOptions.MidlineLeft);
        Hit(_yesNo, 30f, 45f, 260f, 80f).onClick.AddListener(() => OnYesNo(true));
        Hit(_yesNo, 30f, 125f, 260f, 80f).onClick.AddListener(() => OnYesNo(false));
        _yesNoCursor = NewCursor(_yesNo);
        _yesNo.gameObject.SetActive(false);
    }

    private void BuildCreateLayer()
    {
        _createLayer = NewRect("CreateLayer", _root); Stretch(_createLayer);
        _black = NewImage("Black", _createLayer, null); Stretch(_black.rectTransform);
        _black.color = new Color(0f, 0f, 0f, 0f);
        _black.raycastTarget = true; // 만드는 동안 뒤의 모험의 서를 누르지 못하게
        _caption = Text(_createLayer, "", 0f, 0f, 1080f, 120f, 50f, Gold, TextAlignmentOptions.Center);
        _caption.rectTransform.anchorMin = _caption.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _caption.rectTransform.anchoredPosition = new Vector2(0f, 120f);
        _caption.characterSpacing = 8f;
        if (headingFont != null && !KR) _caption.font = headingFont;
        _caption.alpha = 0f;
        _createLayer.gameObject.SetActive(false);
    }

    /// <summary>가운데 고르기 창 (머리글 + 줄 count 개 + 필요하면 아래 "결정").</summary>
    private float _rowH = 132f; // 줄 높이 (효과 글자가 있는 종의 기억은 더 높게)

    private void ChoiceWindow(out RectTransform list, out TextMeshProUGUI header, int count, bool withDecide, out Button decide, out TextMeshProUGUI decideText, float rowH = 132f)
    {
        HideChoice();
        _rowH = rowH;
        float h = 150f + count * rowH + (withDecide ? 120f : 40f);
        // [2026-10-15] 화면 중간 위에서 시작 (목록이 길면 대화창 위까지 올라감)
        _choiceWindow = Window("ChoiceWindow", _createLayer, 60f, Mathf.Max(140f, Mathf.Min(300f, 1920f - 90f - 330f - 40f - h)), 960f, h);
        header = Text(_choiceWindow, "", 0f, 36f, 960f, 70f, 44f, Gold, TextAlignmentOptions.Center);
        var line = NewImage("Divider", _choiceWindow, null); Place(line.rectTransform, 60f, 112f, 840f, 2f); line.color = new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.6f);
        list = NewRect("List", _choiceWindow);
        Place(list, 0f, 130f, 960f, count * rowH);
        decide = null; decideText = null;
        if (withDecide)
        {
            decideText = Text(_choiceWindow, T("결정", "Decide"), 0f, 130f + count * rowH + 10f, 960f, 90f, 46f, Disabled, TextAlignmentOptions.Center);
            decide = Hit(_choiceWindow, 330f, 130f + count * rowH + 10f, 300f, 90f);
        }
    }

    private void HideChoice()
    {
        if (_choiceWindow != null) Destroy(_choiceWindow.gameObject);
        _choiceWindow = null;
        _choiceNav.Clear();
        if (_nav == _choiceNav) _nav = null;
    }

    /// <summary>고르기 줄: 그림(없으면 그림 칸 위에 이름 글자) + 이름 + 부제 + 오른쪽 수치.</summary>
    private Button ChoiceRow(RectTransform list, int i, Sprite icon, string name, string sub, string stats, out Image tint)
    {
        float rowH = _rowH, y = i * rowH;
        tint = NewImage("Tint", list, null); Place(tint.rectTransform, 40f, y + 6f, 880f, rowH - 12f);
        tint.color = new Color(0, 0, 0, 0);
        // 그림 칸 (줄 가운데)
        var box = NewImage("IconBox", list, frame); Place(box.rectTransform, 110f, y + (rowH - 104f) * 0.5f, 104f, 104f);
        box.type = Image.Type.Sliced; box.pixelsPerUnitMultiplier = 3f;
        if (icon != null)
        {
            var ic = NewImage("Icon", box.rectTransform, icon); Stretch(ic.rectTransform);
            ic.rectTransform.offsetMin = new Vector2(8f, 8f); ic.rectTransform.offsetMax = new Vector2(-8f, -8f);
            ic.preserveAspect = true;
        }
        else
        {
            var t = Text(box.rectTransform, name, 0f, 0f, 104f, 104f, 22f, Gold, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(6f, 6f); t.rectTransform.offsetMax = new Vector2(-6f, -6f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = 24f;
        }
        if (string.IsNullOrEmpty(stats))
        {
            bool tall = rowH > 140f; // 서약·직업처럼 설명이 여러 줄이면 이름 바로 아래부터 넓게
            Text(list, name, 240f, tall ? y + 10f : y + rowH * 0.10f, 420f, 56f, 42f, Cream, TextAlignmentOptions.MidlineLeft);
            var s = Text(list, sub, 240f, tall ? y + 64f : y + rowH * 0.53f, 660f, tall ? rowH - 76f : rowH * 0.38f, 28f, Muted, tall ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft);
            s.textWrappingMode = TextWrappingModes.Normal; s.enableAutoSizing = true; s.fontSizeMin = 20f; s.fontSizeMax = 28f;
        }
        else
        {
            // [2026-10-14] 효과를 크게 — 이름 줄(이름 + 오른쪽에 부제), 그 아래 두 줄: 기본 수치 / 레벨업·성장
            Text(list, name, 240f, y + 12f, 300f, 54f, 42f, Cream, TextAlignmentOptions.MidlineLeft);
            var s = Text(list, sub, 450f, y + 12f, 460f, 54f, 26f, Muted, TextAlignmentOptions.MidlineRight);
            s.enableAutoSizing = true; s.fontSizeMin = 20f; s.fontSizeMax = 26f;
            SplitStats(stats, out string basic, out string growth);
            var b1 = Text(list, basic, 240f, y + 68f, 670f, 46f, 34f, Gold, TextAlignmentOptions.MidlineLeft);
            b1.enableAutoSizing = true; b1.fontSizeMin = 24f; b1.fontSizeMax = 34f;
            if (!string.IsNullOrEmpty(growth))
            {
                var b2 = Text(list, growth, 240f, y + 114f, 670f, 42f, 30f, Parch, TextAlignmentOptions.MidlineLeft);
                b2.enableAutoSizing = true; b2.fontSizeMin = 22f; b2.fontSizeMax = 30f;
            }
        }
        return Hit(list, 40f, y + 6f, 880f, rowH - 12f);
    }

    /// <summary>[2026-10-20] 종의 기억 수치: 바로 붙는 수치(금색) 한 줄 / 레벨업 성장·확률(하늘색) 한 줄.</summary>
    private static string StatLines(MemoryOfSpeciesData m)
    {
        SplitStats(m.GetStatSummary(), out string basic, out string growth);
        string a = basic.Length > 0 ? $"<color=#D4A761>{basic}</color>" : "";
        string b = growth.Length > 0 ? $"<color=#8CC8FF>{growth}</color>" : "";
        return a.Length > 0 && b.Length > 0 ? a + "\n" + b : a + b;
    }

    /// <summary>"DEF +1, AGI -1, Lv업 DEF확률 +20%p" → 기본 수치 "DEF +1   AGI -1" / 레벨업·성장 "Lv업 DEF확률 +20%p"</summary>
    private static void SplitStats(string stats, out string basic, out string growth)
    {
        var a = new List<string>(); var b = new List<string>();
        foreach (var part in stats.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
            (part.Contains("Lv") || part.Contains("레벨") || part.Contains("/") ? b : a).Add(part.Trim());
        basic = string.Join("    ", a);
        growth = string.Join("    ", b);
    }

    // ─────────────────────────────────────────
    // 작은 도우미
    // ─────────────────────────────────────────

    private RectTransform Window(string name, RectTransform parent, float x, float y, float w, float h)
    {
        var img = NewImage(name, parent, frame);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true; // 창 뒤를 누르지 않게
        Place(img.rectTransform, x, y, w, h);
        return img.rectTransform;
    }

    private TextMeshProUGUI Text(RectTransform parent, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        Place(t.rectTransform, x, y, w, h);
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false; t.richText = true;
        t.text = text;
        return t;
    }

    private Button Hit(RectTransform parent, float x, float y, float w, float h)
    {
        var img = NewImage("Hit", parent, null);
        Place(img.rectTransform, x, y, w, h);
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        return b;
    }

    private RectTransform NewCursor(RectTransform parent)
    {
        var img = NewImage("Cursor", parent, _arrow);
        img.color = Gold;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(34f, 34f);
        img.gameObject.SetActive(false); // 고르기 전에는 숨김 (PlaceCursor 가 켠다)
        return img.rectTransform;
    }

    // ── 한국어 조사 (받침에 따라) ──
    private static bool HasFinal(char c, out bool rieul)
    {
        rieul = false;
        if (c < 0xAC00 || c > 0xD7A3) return false;
        int jong = (c - 0xAC00) % 28;
        rieul = jong == 8;
        return jong != 0;
    }

    /// <summary>"전사로", "몽크로" / 받침이 있으면 "으로" (ㄹ 받침은 "로").</summary>
    private static string Ro(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        bool rieul;
        return word + (HasFinal(word[word.Length - 1], out rieul) && !rieul ? "으로" : "로");
    }

    /// <summary>숫자 뒤 을/를 — 일·삼·육·칠·팔·영 → 을, 이·사·오·구 → 를.</summary>
    private static string NumEulReul(int n)
    {
        int d = Mathf.Abs(n) % 10;
        return n + (d == 2 || d == 4 || d == 5 || d == 9 ? "를" : "을");
    }

    /// <summary>커서를 줄 왼쪽에 (줄 RectTransform 과 같은 부모 기준, 왼쪽 위 기준 좌표).</summary>
    private void PlaceCursor(RectTransform cursor, RectTransform row, float offsetX = -40f)
    {
        if (cursor == null || row == null) return;
        cursor.gameObject.SetActive(true);
        Vector3 worldLeftMid = row.TransformPoint(new Vector3(row.rect.xMin, row.rect.center.y, 0f));
        Vector3 local = cursor.parent.InverseTransformPoint(worldLeftMid);
        var parentRt = (RectTransform)cursor.parent;
        // 부모의 왼쪽 위 기준 좌표로 바꿈
        Vector2 fromTopLeft = new Vector2(local.x - parentRt.rect.xMin, local.y - parentRt.rect.yMax);
        cursor.anchoredPosition = new Vector2(fromTopLeft.x + offsetX, fromTopLeft.y);
        cursor.SetAsLastSibling();
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetAlpha(RectTransform rt, float a)
    {
        if (rt == null) return;
        var g = rt.GetComponent<Graphic>();
        if (g != null) { var c = g.color; c.a = a; g.color = c; }
    }

    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    /// <summary>드퀘 커서 ▶ — 작은 삼각형 그림을 코드로 만든다.</summary>
    private static Sprite MakeArrowSprite()
    {
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // 왼쪽 변이 세로, 오른쪽 끝이 뾰족한 삼각형 (가장자리 1px 부드럽게)
                float half = (S - 1) * 0.5f;
                float edge = half * (1f - (x + 0.5f) / S);
                float d = edge - Mathf.Abs(y + 0.5f - S * 0.5f);
                byte a = (byte)(Mathf.Clamp01(d + 0.5f) * 255);
                px[y * S + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    private IEnumerator FadeFromBlack()
    {
        var img = NewImage("FadeIn", _root, null); Stretch(img.rectTransform);
        img.color = Color.black;
        yield return Fade(img, 1f, 0f, 0.6f);
        Destroy(img.gameObject);
    }

    private static IEnumerator Fade(Image img, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            var c = img.color; c.a = Mathf.Lerp(from, to, t / seconds); img.color = c;
            yield return null;
        }
        var e = img.color; e.a = to; img.color = e;
    }

    private static IEnumerator FadeText(TMP_Text t, float from, float to, float seconds)
    {
        for (float x = 0f; x < seconds; x += Time.unscaledDeltaTime) { t.alpha = Mathf.Lerp(from, to, x / seconds); yield return null; }
        t.alpha = to;
    }
}
