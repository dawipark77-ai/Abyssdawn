using System.Collections.Generic;
using System.Text;
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

    [Header("Identity")]
    [SerializeField] private TMP_Text monsterName;
    [SerializeField] private TMP_Text monsterLevel;
    [SerializeField] private TMP_Text monsterRace;
    [SerializeField] private TMP_Text monsterType;
    [SerializeField] private Image instinctIcon;
    [SerializeField] private TMP_Text curseStat;

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
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (hideOnAwake) Root.SetActive(false);
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

        SetText(curseStat, BuildStatusEffectText(e));

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
        return _battleManager != null ? _battleManager.criticalChance : 25f;
    }

    /// <summary>
    /// 현재 걸려 있는 저주 전부. 두 경로를 모두 읽는다:
    ///  ① activeStatusEffects — 출혈/독/점화/스턴 등 (ApplyStatusEffect 경로)
    ///  ② activeStatModifiers — 눈긁기처럼 statModifiers를 가진 저주 (ApplyCurseEffects의 스택 경로)
    /// ②는 출처 스킬별로 묶어 스택 수를 표시하고, 이로운 버프(격분 등)는 제외한다.
    /// 표기: 이름 (남은 턴) / 이름 xN (남은 턴)
    /// </summary>
    private string BuildStatusEffectText(EnemyStats e)
    {
        var sb = new StringBuilder();

        // ① 상태이상 인스턴스
        if (e.activeStatusEffects != null)
        {
            foreach (var se in e.activeStatusEffects)
            {
                if (se == null || se.data == null) continue;
                AppendEntry(sb, $"{GetEffectLabel(se.data)} ({se.remainingTurns})");
            }
        }

        // ② 스탯 디버프 — 출처(source)별로 묶기, 등장 순서 유지
        if (e.activeStatModifiers != null)
        {
            var order = new List<object>();
            var entryCount = new Dictionary<object, int>();
            var maxTurns = new Dictionary<object, int>();

            foreach (var am in e.activeStatModifiers)
            {
                if (am == null || am.modifier == null || am.source == null) continue;
                if (!IsHarmful(am.modifier)) continue;

                if (!entryCount.ContainsKey(am.source))
                {
                    order.Add(am.source);
                    entryCount[am.source] = 0;
                    maxTurns[am.source] = am.remainingTurns;
                }
                entryCount[am.source]++;
                maxTurns[am.source] = Mathf.Max(maxTurns[am.source], am.remainingTurns);
            }

            foreach (var src in order)
            {
                string label = src.ToString();
                int modsPerStack = 1;

                if (src is AbyssdawnBattle.SkillData sd)
                {
                    if (sd.curseEffect != null)
                    {
                        label = GetEffectLabel(sd.curseEffect);
                        if (sd.curseEffect.statModifiers != null && sd.curseEffect.statModifiers.Count > 0)
                            modsPerStack = sd.curseEffect.statModifiers.Count;
                    }
                    else
                    {
                        label = sd.skillName;
                    }
                }

                int stacks = Mathf.Max(1, entryCount[src] / modsPerStack);
                string stackText = stacks > 1 ? $" x{stacks}" : "";
                string turnText = maxTurns[src] >= 0 ? $" ({maxTurns[src]})" : "";
                AppendEntry(sb, $"{label}{stackText}{turnText}");
            }
        }

        return sb.Length > 0 ? sb.ToString() : "-";
    }

    private static string GetEffectLabel(StatusEffectSO effect)
    {
        return string.IsNullOrEmpty(effect.variantId) ? effect.effectType.ToString() : effect.variantId;
    }

    private static void AppendEntry(StringBuilder sb, string entry)
    {
        if (sb.Length > 0) sb.Append(", ");
        sb.Append(entry);
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
