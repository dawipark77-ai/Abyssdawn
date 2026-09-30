using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Abyssdawn;
using AbyssdawnBattle;

/// <summary>
/// 적 상태창. Show(EnemyStats)로 열고 Hide()로 닫는다.
/// 기본 스탯(MonsterSO 기준)과 현재 스탯(StatModifier 반영)을 나란히 표시.
/// 참조는 전부 Inspector에서 연결 — 비어 있는 필드는 조용히 건너뛴다.
/// </summary>
public class EnemyStatusPanel : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("켜고 끌 대상. 비우면 이 GameObject 자체. (권장: 하위 Canvas)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool hideOnAwake = true;
    [Tooltip("열려 있는 동안 매 프레임 갱신 (버프/HP 변화 실시간 반영)")]
    [SerializeField] private bool refreshWhileOpen = true;
    [Tooltip("상태창 Canvas 표시 순서. 다른 전투 UI(기본 0, 카드 버튼 5)보다 커야 맨 앞에 뜬다.")]
    [SerializeField] private int sortingOrder = 100;

    [Header("Identity")]
    [SerializeField] private TMP_Text monsterName;
    [SerializeField] private TMP_Text monsterLevel;
    [SerializeField] private TMP_Text monsterRace;
    [SerializeField] private TMP_Text monsterType;
    [SerializeField] private Image instinctIcon;

    [Header("Curse Icons (CurseStat)")]
    [Tooltip("현재 걸린 저주 아이콘이 자동 배치될 영역 (CurseStat). 아이콘은 코드가 만든다.")]
    [SerializeField] private RectTransform curseIconContainer;
    [SerializeField] private float curseIconSize = 64f;
    [SerializeField] private float curseIconSpacing = 6f;
    [Tooltip("아이콘 위에 남은 턴(오른쪽 아래) / 스택 수(오른쪽 위) 표시")]
    [SerializeField] private bool showCurseCounts = true;
    [Tooltip("저주 에셋에 아이콘이 없을 때 대신 쓸 이미지. 비우면 회색 사각형")]
    [SerializeField] private Sprite fallbackCurseIcon;

    [Header("HP / MP")]
    [SerializeField] private TMP_Text hp;
    [SerializeField] private TMP_Text mp;

    [Header("Base Stats")]
    [SerializeField] private TMP_Text atk;
    [SerializeField] private TMP_Text def;
    [SerializeField] private TMP_Text mag;
    [SerializeField] private TMP_Text agi;
    [SerializeField] private TMP_Text luk;

    [Header("Skill Icons")]
    [SerializeField] private Image[] passiveIcons = new Image[3];
    [SerializeField] private Image[] activeIcons = new Image[6];

    [Header("Current Stats")]
    [SerializeField] private TMP_Text curAtk;
    [SerializeField] private TMP_Text curDef;
    [SerializeField] private TMP_Text curMag;
    [SerializeField] private TMP_Text curAgi;
    [SerializeField] private TMP_Text curLuk;
    [SerializeField] private TMP_Text curAcc;
    [SerializeField] private TMP_Text curEva;
    [SerializeField] private TMP_Text curCri;
    [SerializeField] private TMP_Text curMagResist;
    [SerializeField] private TMP_Text curPhysResist;
    [SerializeField] private TMP_Text curStatusResist;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color buffedColor = new Color(0.45f, 1f, 0.45f);
    [SerializeField] private Color debuffedColor = new Color(1f, 0.45f, 0.45f);

    private EnemyStats _target;
    private BattleManager _battleManager;

    public bool IsOpen => Root.activeSelf;
    public EnemyStats Target => _target;

    private GameObject Root => panelRoot != null ? panelRoot : gameObject;

    private void Awake()
    {
        BringToFront();
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (hideOnAwake) Root.SetActive(false);
    }

    /// <summary>
    /// 상태창 캔버스를 다른 모든 전투 UI보다 앞에 고정.
    /// 캔버스들의 Sort Order가 같으면 Unity가 앞뒤를 보장하지 않으므로 코드로 명시한다.
    /// </summary>
    private void BringToFront()
    {
        Canvas canvas = Root.GetComponent<Canvas>();
        if (canvas == null) canvas = Root.GetComponentInChildren<Canvas>(true);
        if (canvas == null) canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogWarning("[EnemyStatusPanel] Canvas를 찾지 못해 표시 순서를 올리지 못했습니다.");
            return;
        }

        canvas.overrideSorting = true;   // 다른 Canvas 안에 들어가 있어도 독자 순서 사용
        canvas.sortingOrder = sortingOrder;
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
    }

    private void Update()
    {
        if (!refreshWhileOpen || _target == null || !IsOpen) return;
        Refresh();
    }

    public void Show(EnemyStats target)
    {
        if (target == null) return;
        _target = target;
        Root.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        _target = null;
        Root.SetActive(false);
    }

    public void Refresh()
    {
        if (_target == null) { Hide(); return; }

        EnemyStats e = _target;
        MonsterSO so = e.sourceMonster;

        // ── 식별 ──
        SetText(monsterName, e.enemyName);
        SetText(monsterLevel, so != null ? $"Lv. {so.MonsterLevel}" : "Lv. ?");
        SetText(monsterRace, so != null ? so.Race.ToString() : "-");
        SetText(monsterType, so != null ? so.Type.ToString() : "-");

        var instinct = so != null ? so.BasicAttackOverride : null;
        SetIcon(instinctIcon, instinct != null ? instinct.skillIcon : null);

        RefreshCurseIcons(e);

        // ── HP / MP ──
        SetText(hp, $"{e.currentHP}/{e.maxHP}");
        SetText(mp, $"{e.currentMP}/{e.maxMP}");

        // ── 기본 스탯 (Init 시 MonsterSO에서 복사된 값) ──
        SetText(atk, e.attack.ToString());
        SetText(def, e.defense.ToString());
        SetText(mag, e.magic.ToString());
        SetText(agi, e.Agility.ToString());
        SetText(luk, e.luck.ToString());

        // ── 스킬 아이콘 ──
        FillPassiveIcons(so);
        FillActiveIcons(so);

        // ── 현재 스탯 (StatModifier 반영) ──
        SetCompared(curAtk, e.ApplyStatModifiers(ModStatType.Attack, e.attack), e.attack);
        SetCompared(curDef, e.ApplyStatModifiers(ModStatType.Defense, e.defense), e.defense);
        SetCompared(curMag, e.ApplyStatModifiers(ModStatType.Magic, e.magic), e.magic);
        SetCompared(curAgi, e.ApplyStatModifiers(ModStatType.Speed, e.Agility), e.Agility);
        SetCompared(curLuk, e.luck, e.luck); // Luck 전용 ModStatType 없음

        // 명중 배율 (1.0 = 100%)
        float acc = e.ApplyStatModifiers(ModStatType.Accuracy, 1f);
        SetPercent(curAcc, acc * 100f, 100f);

        // 회피 — 전투 계산과 동일하게 0~0.8 클램프
        float eva = Mathf.Clamp(e.ApplyStatModifiers(ModStatType.Evasion, 0f), 0f, 0.8f);
        SetPercent(curEva, eva * 100f, 0f);

        // 크리티컬 확률 — BattleManager.CheckCritical과 동일: criticalChance + luck + CritChance(Flat)
        float baseCrit = GetBaseCriticalChance() + e.luck;
        float cri = Mathf.Clamp(baseCrit + e.ApplyStatModifiers(ModStatType.CritChance, 0f), 0f, 100f);
        SetPercent(curCri, cri, baseCrit);

        // 저항 — 필드는 "받는 배율"(1 = 정상 피해)이라 저항률 = (1 - 배율)로 표시
        SetPercent(curMagResist, (1f - e.magResist) * 100f, (1f - e.magResist) * 100f);
        SetPercent(curPhysResist, (1f - e.physResist) * 100f, (1f - e.physResist) * 100f);

        float statusMult = e.ApplyStatModifiers(ModStatType.StatusResist, e.statusResist);
        SetPercent(curStatusResist, (1f - statusMult) * 100f, (1f - e.statusResist) * 100f);
    }

    // ─────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────

    private float GetBaseCriticalChance()
    {
        if (_battleManager == null) _battleManager = FindFirstObjectByType<BattleManager>();
        return _battleManager != null ? _battleManager.criticalChance : BattleManager.DefaultCriticalChance;
    }

    // ─────────────────────────────────────────
    // 저주 아이콘 (CurseStat)
    // ─────────────────────────────────────────

    private struct CurseEntry
    {
        public Sprite icon;
        public int turns;   // -1 = 영구
        public int stacks;
    }

    private class CurseIconSlot
    {
        public GameObject root;
        public RectTransform rect;
        public Image image;
        public TextMeshProUGUI turns;
        public TextMeshProUGUI stacks;
    }

    private readonly List<CurseEntry> _curseEntries = new List<CurseEntry>();
    private readonly List<CurseIconSlot> _curseSlots = new List<CurseIconSlot>();
    private readonly List<object> _srcOrder = new List<object>();
    private readonly Dictionary<object, int> _srcCount = new Dictionary<object, int>();
    private readonly Dictionary<object, int> _srcTurns = new Dictionary<object, int>();

    /// <summary>
    /// 현재 걸린 저주 아이콘을 CurseStat 영역에 왼쪽 위부터 채운다 (넘치면 다음 줄).
    /// 아이콘 GameObject는 한 번 만들면 재사용하고, 남는 칸은 숨긴다.
    /// </summary>
    private void RefreshCurseIcons(EnemyStats e)
    {
        if (curseIconContainer == null) return;

        CollectCurses(e, _curseEntries);

        float step = curseIconSize + curseIconSpacing;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((curseIconContainer.rect.width + curseIconSpacing) / step));

        for (int i = 0; i < _curseEntries.Count; i++)
        {
            CurseEntry entry = _curseEntries[i];
            CurseIconSlot slot = GetOrCreateCurseSlot(i);

            slot.root.SetActive(true);
            slot.rect.sizeDelta = new Vector2(curseIconSize, curseIconSize);
            slot.rect.anchoredPosition = new Vector2((i % perRow) * step, -(i / perRow) * step);

            Sprite icon = entry.icon != null ? entry.icon : fallbackCurseIcon;
            slot.image.sprite = icon;
            slot.image.color = icon != null ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.9f);

            SetText(slot.turns, showCurseCounts && entry.turns >= 0 ? entry.turns.ToString() : "");
            SetText(slot.stacks, showCurseCounts && entry.stacks > 1 ? $"x{entry.stacks}" : "");
        }

        for (int i = _curseEntries.Count; i < _curseSlots.Count; i++)
            _curseSlots[i].root.SetActive(false);
    }

    /// <summary>
    /// 현재 걸린 저주 전부. 두 경로를 모두 읽는다:
    ///  ① activeStatusEffects — 출혈/독/점화/스턴 등 (ApplyStatusEffect 경로)
    ///  ② activeStatModifiers — 눈긁기처럼 statModifiers를 가진 저주 (ApplyCurseEffects의 스택 경로)
    /// ②는 출처 스킬별로 묶어 스택 수를 세고, 이로운 버프(격분 등)는 제외한다.
    /// 아이콘은 적 카드(UpdateEnemyStatusIcons)와 같은 규칙: flatIcon 우선, 없으면 itemIcon.
    /// </summary>
    private void CollectCurses(EnemyStats e, List<CurseEntry> result)
    {
        result.Clear();

        // ① 상태이상 인스턴스
        if (e.activeStatusEffects != null)
        {
            foreach (var se in e.activeStatusEffects)
            {
                if (se == null || se.data == null) continue;
                result.Add(new CurseEntry { icon = GetEffectIcon(se.data), turns = se.remainingTurns, stacks = 1 });
            }
        }

        // ② 스탯 디버프 — 출처(source)별로 묶기, 등장 순서 유지
        if (e.activeStatModifiers == null) return;

        _srcOrder.Clear();
        _srcCount.Clear();
        _srcTurns.Clear();

        foreach (var am in e.activeStatModifiers)
        {
            if (am == null || am.modifier == null || am.source == null) continue;
            if (!IsHarmful(am.modifier)) continue;

            if (!_srcCount.ContainsKey(am.source))
            {
                _srcOrder.Add(am.source);
                _srcCount[am.source] = 0;
                _srcTurns[am.source] = am.remainingTurns;
            }
            _srcCount[am.source]++;
            _srcTurns[am.source] = Mathf.Max(_srcTurns[am.source], am.remainingTurns);
        }

        foreach (var src in _srcOrder)
        {
            StatusEffectSO effect = (src as AbyssdawnBattle.SkillData)?.curseEffect;

            int modsPerStack = 1;
            if (effect != null && effect.statModifiers != null && effect.statModifiers.Count > 0)
                modsPerStack = effect.statModifiers.Count;

            result.Add(new CurseEntry
            {
                icon = effect != null ? GetEffectIcon(effect) : null,
                turns = _srcTurns[src],
                stacks = Mathf.Max(1, _srcCount[src] / modsPerStack)
            });
        }
    }

    private static Sprite GetEffectIcon(StatusEffectSO effect)
    {
        return effect.flatIcon != null ? effect.flatIcon : effect.itemIcon;
    }

    private CurseIconSlot GetOrCreateCurseSlot(int index)
    {
        while (_curseSlots.Count <= index)
        {
            var go = new GameObject($"CurseIcon_{_curseSlots.Count}", typeof(RectTransform), typeof(Image));
            go.layer = curseIconContainer.gameObject.layer;
            go.transform.SetParent(curseIconContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); // 왼쪽 위 기준

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;

            _curseSlots.Add(new CurseIconSlot
            {
                root = go,
                rect = rt,
                image = img,
                turns = CreateCountLabel(go.transform, "Turns", new Vector2(1f, 0f), TextAlignmentOptions.BottomRight),
                stacks = CreateCountLabel(go.transform, "Stacks", new Vector2(1f, 1f), TextAlignmentOptions.TopRight)
            });
        }
        return _curseSlots[index];
    }

    private TextMeshProUGUI CreateCountLabel(Transform parent, string name, Vector2 corner, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(curseIconSize, curseIconSize * 0.5f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = curseIconSize * 0.35f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.text = "";
        return tmp;
    }

    /// <summary>
    /// 보유자에게 불리한 모디파이어인지. 대부분 스탯은 "낮아지면 불리".
    /// 예외: StatusResist는 곱해지는 확률 배율이라 "높아지면 불리"(저주가 더 잘 걸림).
    /// </summary>
    private static bool IsHarmful(StatModifier mod)
    {
        float neutral = mod.modType == StatModType.PercentMult ? 1f : 0f;
        if (Mathf.Approximately(mod.value, neutral)) return false;

        bool lowers = mod.value < neutral;
        return mod.statType == ModStatType.StatusResist ? !lowers : lowers;
    }

    private void FillPassiveIcons(MonsterSO so)
    {
        if (passiveIcons == null) return;
        var list = so != null ? so.PassiveSkills : null;
        for (int i = 0; i < passiveIcons.Length; i++)
        {
            var p = (list != null && i < list.Count) ? list[i] : null;
            SetIcon(passiveIcons[i], p != null ? p.passiveIcon : null);
        }
    }

    private void FillActiveIcons(MonsterSO so)
    {
        if (activeIcons == null) return;
        var list = so != null ? so.ActiveSkills : null;
        for (int i = 0; i < activeIcons.Length; i++)
        {
            var s = (list != null && i < list.Count) ? list[i] : null;
            SetIcon(activeIcons[i], s != null ? s.skillIcon : null);
        }
    }

    private static void SetText(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }

    /// <summary>아이콘이 없으면 슬롯 자체를 숨긴다 (빈 흰 사각형 방지).</summary>
    private static void SetIcon(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.gameObject.SetActive(sprite != null);
    }

    private void SetCompared(TMP_Text t, float final, float baseValue)
    {
        if (t == null) return;
        t.text = Mathf.RoundToInt(final).ToString();
        t.color = PickColor(final, baseValue);
    }

    private void SetPercent(TMP_Text t, float percent, float basePercent)
    {
        if (t == null) return;
        t.text = $"{percent:0}%";
        t.color = PickColor(percent, basePercent);
    }

    private Color PickColor(float value, float baseValue)
    {
        if (value > baseValue + 0.01f) return buffedColor;
        if (value < baseValue - 0.01f) return debuffedColor;
        return normalColor;
    }
}
