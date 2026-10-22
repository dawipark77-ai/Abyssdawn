using System;
using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

/// <summary>
/// 던전(걷는 중) 상태이상 — 함정이 건다. 주인공 한 명에게만. static 이라 씬 전환(전투)에도 유지되고 저장 파일에도 들어간다.
///  - 독      : 30걸음, 3걸음마다 최대 HP 3%
///  - 출혈    : 24걸음, 4걸음마다 4%
///  - 화상    : 16걸음, 2걸음마다 3%   (전투의 점화 Ignite)
///  - 실명    : 40걸음, 시야 -2칸
///  - 충격(Stun) : 다음 전투 시작 때 1턴 기절하고 사라짐
/// [2026-10-08] 걷는 중 피해로도 쓰러진다 (HP 0 → 게임 오버, MapManager.CheckHeroDeath).
/// 전투가 시작되면 걸려 있는 것을 전투 상태이상으로도 건다 (BattleManager.ApplyDungeonCarryOver).
/// 해제: 해독제·붕대·냉각제·정화수 (ConsumableEffectApplier), 숨고르기(출혈), 여관.
/// </summary>
public static class DungeonFieldStatus
{
    public const int UntilNextBattle = -1;

    private class Rule { public int steps, every; public float percent; public string name, color; }

    private static readonly Dictionary<StatusEffectType, Rule> Rules = new Dictionary<StatusEffectType, Rule>
    {
        { StatusEffectType.Poison, new Rule { steps = 30, every = 3, percent = 0.03f, name = "독", color = "#7CFC7C" } },
        { StatusEffectType.Bleed,  new Rule { steps = 24, every = 4, percent = 0.04f, name = "출혈", color = "#FF5A5A" } },
        { StatusEffectType.Ignite, new Rule { steps = 16, every = 2, percent = 0.03f, name = "점화", color = "#FF9A3C" } },
        { StatusEffectType.Blind,  new Rule { steps = 40, every = 0, percent = 0f, name = "실명", color = "#B0B0FF" } },
        { StatusEffectType.Stun,   new Rule { steps = UntilNextBattle, every = 0, percent = 0f, name = "뇌진탕", color = "#FFD700" } },
    };

    /// <summary>전투에 걸 때 쓰는 상태이상 에셋 (Resources 경로).</summary>
    private static readonly Dictionary<StatusEffectType, string> BattleAssets = new Dictionary<StatusEffectType, string>
    {
        { StatusEffectType.Poison, "Curse/Curse_Poison" },
        { StatusEffectType.Bleed,  "Curse/Bleed1_Dagger" },
        { StatusEffectType.Ignite, "Curse/Curse_Ignite" },
        { StatusEffectType.Blind,  "Curse/Debuff_BlindScratch" },
        { StatusEffectType.Stun,   "Curse/Curse_Stun" },
    };

    public const int BlindSightPenalty = 2;

    // 남은 걸음 (UntilNextBattle = 다음 전투까지)
    private static readonly Dictionary<StatusEffectType, int> _left = new Dictionary<StatusEffectType, int>();
    private static readonly Dictionary<StatusEffectType, int> _walked = new Dictionary<StatusEffectType, int>();
    // [2026-10-07] 함정이 건 짧은 상태이상: 몇 걸음마다 피해를 줄지 (기본 규칙 대신). 예: 함정 출혈 = 3턴, 매 턴 피해
    private static readonly Dictionary<StatusEffectType, int> _everyOverride = new Dictionary<StatusEffectType, int>();

    public static event Action OnChanged;

    public static bool Has(StatusEffectType type) => _left.ContainsKey(type);
    public static bool Any => _left.Count > 0 || _comp.Count > 0; // [2026-10-21] 동료 것도 있으면 걸음마다 진행

    /// <summary>[2026-10-08] 지금 걸린 던전 상태이상의 아이콘 (전투용 상태이상 에셋의 아이콘을 그대로 씀). 상태 창·파티 바 표시용.</summary>
    public static List<KeyValuePair<StatusEffectType, Sprite>> ActiveIcons()
    {
        var list = new List<KeyValuePair<StatusEffectType, Sprite>>();
        foreach (var type in _left.Keys)
        {
            Sprite s = null;
            if (BattleAssets.TryGetValue(type, out string path))
            {
                var so = Resources.Load<StatusEffectSO>(path);
                if (so != null) s = so.flatIcon != null ? so.flatIcon : so.itemIcon;
            }
            list.Add(new KeyValuePair<StatusEffectType, Sprite>(type, s));
        }
        return list;
    }

    /// <summary>[2026-10-08] 이 종류를 전투에서 걸 때 쓰는 상태이상 에셋 (없으면 null).</summary>
    public static StatusEffectSO BattleAssetFor(StatusEffectType type)
    {
        return BattleAssets.TryGetValue(type, out string path) ? Resources.Load<StatusEffectSO>(path) : null;
    }

    public static string NameOf(StatusEffectType type) => Rules.TryGetValue(type, out Rule r) ? r.name : type.ToString();

    /// <summary>걸기 (이미 있으면 남은 걸음을 더 긴 쪽으로).</summary>
    public static void Add(StatusEffectType type)
    {
        if (!Rules.TryGetValue(type, out Rule r)) return;
        int cur;
        if (_left.TryGetValue(type, out cur))
            _left[type] = (cur == UntilNextBattle || r.steps == UntilNextBattle) ? UntilNextBattle : Mathf.Max(cur, r.steps);
        else
        {
            _left[type] = r.steps;
            _walked[type] = 0;
        }
        _everyOverride.Remove(type);
        OnChanged?.Invoke();
    }

    /// <summary>
    /// [2026-10-07] 걸음 수·피해 간격을 직접 정해 걸기 (함정 출혈 3턴 등). 한 걸음(제자리 한 턴) = 1턴.
    /// 이미 더 긴 일반 상태이상이 걸려 있으면 그것을 유지한다.
    /// </summary>
    public static void Add(StatusEffectType type, int turns, int damageEvery)
    {
        if (!Rules.ContainsKey(type) || turns <= 0) return;
        int cur;
        if (_left.TryGetValue(type, out cur) && !_everyOverride.ContainsKey(type) && (cur == UntilNextBattle || cur > turns))
            return;
        _left[type] = turns;
        _walked[type] = 0;
        _everyOverride[type] = Mathf.Max(1, damageEvery);
        OnChanged?.Invoke();
    }

    public static void Remove(StatusEffectType type)
    {
        if (_left.Remove(type)) { _walked.Remove(type); _everyOverride.Remove(type); OnChanged?.Invoke(); }
    }

    public static void Clear()
    {
        if (_left.Count == 0 && _comp.Count == 0) return;
        _left.Clear();
        _walked.Clear();
        _everyOverride.Clear();
        _comp.Clear(); // [2026-10-21] 동료 것도
        OnChanged?.Invoke();
    }

    // ─────────────────────────────────────────
    // [2026-10-21] 동료 상태이상 — 전투에서 걸린 출혈·독·점화·실명이 남은 턴만큼 맵에서도 이어진다.
    //   동료 고유 번호(CompanionPartyPersistence.Entry.id) 별로. 한 걸음 = 1턴, 매 턴 피해 (주인공 규칙과 같은 % · 동료 최대 HP 기준).
    //   다음 전투가 시작되면 남은 턴을 다시 건다. 동료 HP 가 0 이 되면 쓰러진다 (주인공과 달리 게임 오버는 아님).
    // ─────────────────────────────────────────
    private class CompStat { public int left, walked, every; }
    private static readonly Dictionary<int, Dictionary<StatusEffectType, CompStat>> _comp = new Dictionary<int, Dictionary<StatusEffectType, CompStat>>();

    /// <summary>전투 → 맵으로 넘기는 상태이상 종류.</summary>
    public static readonly StatusEffectType[] CarriedTypes = { StatusEffectType.Bleed, StatusEffectType.Poison, StatusEffectType.Ignite, StatusEffectType.Blind };

    public static bool CompanionHas(int id, StatusEffectType type) { Dictionary<StatusEffectType, CompStat> d; return _comp.TryGetValue(id, out d) && d.ContainsKey(type); }

    /// <summary>[2026-10-23] 고유 번호로 동료 몬스터를 찾아 면역인지 (파티 · 대기 모두)</summary>
    private static bool CompanionImmune(int id, StatusEffectType type)
    {
        string path = null;
        foreach (var e in CompanionPartyPersistence.ActiveRoster) if (e != null && e.id == id) { path = e.resourcePath; break; }
        if (path == null) foreach (var w in CompanionPartyPersistence.WaitlistPaths) if (w != null && w.id == id) { path = w.resourcePath; break; }
        var so = path != null ? CompanionPartyPersistence.LoadCompanion(path) : null;
        return so != null && so.IsImmune(type);
    }

    public static void AddCompanion(int id, StatusEffectType type, int turns, int every = 1)
    {
        if (id == 0 || turns <= 0 || !Rules.ContainsKey(type)) return;
        if (CompanionImmune(id, type)) return; // [2026-10-23] 면역 (슬라임 = 독)
        Dictionary<StatusEffectType, CompStat> d;
        if (!_comp.TryGetValue(id, out d)) { d = new Dictionary<StatusEffectType, CompStat>(); _comp[id] = d; }
        d[type] = new CompStat { left = turns, walked = 0, every = Mathf.Max(0, every) };
        OnChanged?.Invoke();
    }

    public static void RemoveCompanion(int id, StatusEffectType type)
    {
        Dictionary<StatusEffectType, CompStat> d;
        if (_comp.TryGetValue(id, out d) && d.Remove(type)) { if (d.Count == 0) _comp.Remove(id); OnChanged?.Invoke(); }
    }

    public static void ClearCompanion(int id)
    {
        if (_comp.Remove(id)) OnChanged?.Invoke();
    }

    /// <summary>동료 카드 아이콘용.</summary>
    public static List<KeyValuePair<StatusEffectType, Sprite>> CompanionIcons(int id)
    {
        var list = new List<KeyValuePair<StatusEffectType, Sprite>>();
        Dictionary<StatusEffectType, CompStat> d;
        if (!_comp.TryGetValue(id, out d)) return list;
        foreach (var type in d.Keys)
        {
            var so = BattleAssetFor(type);
            list.Add(new KeyValuePair<StatusEffectType, Sprite>(type, so != null ? (so.flatIcon != null ? so.flatIcon : so.itemIcon) : null));
        }
        return list;
    }

    /// <summary>전투 시작: 이 동료에게 남은 맵 상태이상을 전투 상태이상으로 다시 건다. 걸린 이름 목록.</summary>
    public static List<string> ApplyToBattleCompanion(PlayerStats ally)
    {
        var applied = new List<string>();
        Dictionary<StatusEffectType, CompStat> d;
        if (ally == null || ally.companionId == 0 || !_comp.TryGetValue(ally.companionId, out d)) return applied;
        foreach (var kv in d)
        {
            var so = BattleAssetFor(kv.Key);
            if (so != null && ally.ApplyStatusEffect(so, Mathf.Max(1, kv.Value.left))) applied.Add(Rules[kv.Key].name);
        }
        return applied;
    }

    /// <summary>전투 끝: 동료의 전투 상태이상이 진짜 — 이전 맵 기록을 지우고 남은 턴으로 다시 적는다. 쓰러졌으면 모두 지운다.</summary>
    public static void CarryFromBattleCompanion(PlayerStats ally)
    {
        if (ally == null || ally.companionId == 0) return;
        _comp.Remove(ally.companionId);
        if (ally.currentHP > 0 && ally.activeStatusEffects != null)
            foreach (var type in CarriedTypes)
            {
                var se = ally.activeStatusEffects.Find(e => e != null && e.data != null && e.data.effectType == type);
                if (se != null && se.remainingTurns > 0) AddCompanion(ally.companionId, type, se.remainingTurns, 1);
            }
        OnChanged?.Invoke();
    }

    /// <summary>한 걸음: 동료 상태이상 진행 + 피해. 없어진 동료(내보냄·대기열로 감) 기록은 정리.</summary>
    private static string TickCompanions()
    {
        if (_comp.Count == 0) return null;
        string msg = null;
        foreach (int id in new List<int>(_comp.Keys))
        {
            CompanionPartyPersistence.Entry entry = null;
            foreach (var e in CompanionPartyPersistence.ActiveRoster) if (e != null && e.id == id) { entry = e; break; }
            var so = entry != null ? CompanionPartyPersistence.LoadCompanion(entry.resourcePath) : null;
            if (entry == null || so == null || entry.currentHP <= 0) { _comp.Remove(id); continue; }
            var d = _comp[id];
            foreach (var type in new List<StatusEffectType>(d.Keys))
            {
                Rule r = Rules[type];
                CompStat st = d[type];
                st.walked++;
                int every = st.every > 0 ? st.every : r.every;
                if (every > 0 && r.percent > 0f && st.walked % every == 0 && entry.currentHP > 0)
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(so.HP * r.percent));
                    int before = entry.currentHP;
                    entry.currentHP = Mathf.Max(0, before - dmg);
                    if (before > entry.currentHP)
                        msg = (msg == null ? "" : msg + "  ") + $"{so.MonsterName} <color={r.color}>{r.name} -{before - entry.currentHP}</color>" + (entry.currentHP <= 0 ? " <color=#FF4A4A>쓰러졌다!</color>" : "");
                }
                if (--st.left <= 0) d.Remove(type);
            }
            if (d.Count == 0 || entry.currentHP <= 0) _comp.Remove(id);
        }
        return msg;
    }

    /// <summary>
    /// 한 걸음(또는 제자리 한 턴) 진행. 피해를 입었으면 알림 문구를, 아니면 null.
    /// </summary>
    public static string TickStep(PlayerStats hero)
    {
        string compMsg = TickCompanions(); // [2026-10-21] 동료 상태이상
        if (_left.Count == 0 || hero == null)
        {
            if (compMsg != null) OnChanged?.Invoke();
            return compMsg;
        }
        string msg = compMsg;
        int total = 0;
        var keys = new List<StatusEffectType>(_left.Keys);
        foreach (var type in keys)
        {
            Rule r = Rules[type];
            if (_left[type] == UntilNextBattle) continue;
            _walked[type] = (_walked.TryGetValue(type, out int w) ? w : 0) + 1;
            int every = _everyOverride.TryGetValue(type, out int ov) ? ov : r.every;
            if (every > 0 && r.percent > 0f && _walked[type] % every == 0 && hero.currentHP > 0)
            {
                // [2026-10-08] 함정 상태이상으로도 죽는다 (HP 0 → MapManager.CheckHeroDeath 가 게임 오버)
                int dmg = Mathf.Max(1, Mathf.RoundToInt(hero.maxHP * r.percent));
                int before = hero.currentHP;
                hero.currentHP = Mathf.Max(0, before - dmg);
                int lost = before - hero.currentHP;
                if (lost > 0) { total += lost; msg = (msg == null ? "" : msg + "  ") + $"<color={r.color}>{r.name} -{lost}</color>"; }
            }
            if (--_left[type] <= 0)
            {
                _left.Remove(type);
                _walked.Remove(type);
                _everyOverride.Remove(type);
                msg = (msg == null ? "" : msg + "  ") + $"<color=#AAAAAA>{r.name} wears off.</color>";
            }
        }
        if (msg != null) OnChanged?.Invoke();
        return msg;
    }

    /// <summary>HUD 표시용 한 줄 (예: "Poison 21 · Blind 30"). 없으면 "".</summary>
    public static string Summary()
    {
        if (_left.Count == 0) return "";
        var parts = new List<string>();
        foreach (var kv in _left)
        {
            Rule r = Rules[kv.Key];
            parts.Add(kv.Value == UntilNextBattle ? $"<color={r.color}>{r.name}</color>" : $"<color={r.color}>{r.name} {kv.Value}</color>");
        }
        return string.Join("  ·  ", parts);
    }

    /// <summary>
    /// 전투 시작: 걸려 있는 던전 상태이상을 전투 상태이상으로도 건다. 충격(Stun)은 1턴 기절 후 던전 쪽에서 사라진다.
    /// 걸린 것들의 이름 목록을 돌려준다 (전투 로그용).
    /// </summary>
    public static List<string> ApplyToBattle(PlayerStats hero)
    {
        var applied = new List<string>();
        if (hero == null || _left.Count == 0) return applied;
        foreach (var type in new List<StatusEffectType>(_left.Keys))
        {
            if (!BattleAssets.TryGetValue(type, out string path)) continue;
            var so = Resources.Load<StatusEffectSO>(path);
            if (so == null) { Debug.LogWarning($"[DungeonFieldStatus] 'Resources/{path}' 없음"); continue; }
            // 함정이 건 짧은 상태이상(출혈 3턴 등)은 남은 턴 그대로 전투에도
            int turns = type == StatusEffectType.Stun ? 1
                      : _everyOverride.ContainsKey(type) ? Mathf.Max(1, _left[type])
                      : Mathf.Max(1, so.physicalDuration);
            bool ok = hero.ApplyStatusEffect(so, turns);
            if (type == StatusEffectType.Stun)
            {
                // 첫 라운드만 기절 (기본은 '걸린 라운드는 안 줄어듦'이라 2라운드가 되므로 바로 줄어들게)
                var inst = hero.activeStatusEffects.Find(se => se.data != null && se.data.effectType == StatusEffectType.Stun);
                if (inst != null) inst.appliedThisTurn = false;
                Remove(type);
            }
            if (ok) applied.Add(Rules[type].name);
        }
        return applied;
    }

    // ── 저장 ──
    // companion 0 = 주인공, 그 외 = 동료 고유 번호 [2026-10-21] (예전 저장엔 없음 → 0 = 주인공)
    [Serializable] public class SaveEntry { public int type, left, walked, every, companion; } // every 0 = 기본 규칙

    public static List<SaveEntry> ToSave()
    {
        var list = new List<SaveEntry>();
        foreach (var kv in _left)
            list.Add(new SaveEntry { type = (int)kv.Key, left = kv.Value, walked = _walked.TryGetValue(kv.Key, out int w) ? w : 0,
                                     every = _everyOverride.TryGetValue(kv.Key, out int ev) ? ev : 0 });
        foreach (var c in _comp)
            foreach (var kv in c.Value)
                list.Add(new SaveEntry { type = (int)kv.Key, left = kv.Value.left, walked = kv.Value.walked, every = kv.Value.every, companion = c.Key });
        return list;
    }

    public static void FromSave(List<SaveEntry> list)
    {
        _left.Clear();
        _walked.Clear();
        _everyOverride.Clear();
        _comp.Clear();
        if (list != null)
            foreach (var e in list)
            {
                var t = (StatusEffectType)e.type;
                if (!Rules.ContainsKey(t)) continue;
                if (e.companion != 0)
                {
                    Dictionary<StatusEffectType, CompStat> d;
                    if (!_comp.TryGetValue(e.companion, out d)) { d = new Dictionary<StatusEffectType, CompStat>(); _comp[e.companion] = d; }
                    d[t] = new CompStat { left = e.left, walked = e.walked, every = e.every };
                    continue;
                }
                _left[t] = e.left;
                _walked[t] = e.walked;
                if (e.every > 0) _everyOverride[t] = e.every;
            }
        OnChanged?.Invoke();
    }
}
