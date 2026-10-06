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
/// 걷는 중 피해로는 쓰러지지 않는다 (HP 최소 1) — 대신 HP 1 로 다음 전투에 들어가게 된다.
/// 전투가 시작되면 걸려 있는 것을 전투 상태이상으로도 건다 (BattleManager.ApplyDungeonCarryOver).
/// 해제: 해독제·붕대·냉각제·정화수 (ConsumableEffectApplier), 숨고르기(출혈), 여관.
/// </summary>
public static class DungeonFieldStatus
{
    public const int UntilNextBattle = -1;

    private class Rule { public int steps, every; public float percent; public string name, color; }

    private static readonly Dictionary<StatusEffectType, Rule> Rules = new Dictionary<StatusEffectType, Rule>
    {
        { StatusEffectType.Poison, new Rule { steps = 30, every = 3, percent = 0.03f, name = "Poison", color = "#7CFC7C" } },
        { StatusEffectType.Bleed,  new Rule { steps = 24, every = 4, percent = 0.04f, name = "Bleeding", color = "#FF5A5A" } },
        { StatusEffectType.Ignite, new Rule { steps = 16, every = 2, percent = 0.03f, name = "Burn", color = "#FF9A3C" } },
        { StatusEffectType.Blind,  new Rule { steps = 40, every = 0, percent = 0f, name = "Blind", color = "#B0B0FF" } },
        { StatusEffectType.Stun,   new Rule { steps = UntilNextBattle, every = 0, percent = 0f, name = "Concussed", color = "#FFD700" } },
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

    public static event Action OnChanged;

    public static bool Has(StatusEffectType type) => _left.ContainsKey(type);
    public static bool Any => _left.Count > 0;

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
        OnChanged?.Invoke();
    }

    public static void Remove(StatusEffectType type)
    {
        if (_left.Remove(type)) { _walked.Remove(type); OnChanged?.Invoke(); }
    }

    public static void Clear()
    {
        if (_left.Count == 0) return;
        _left.Clear();
        _walked.Clear();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 한 걸음(또는 제자리 한 턴) 진행. 피해를 입었으면 알림 문구를, 아니면 null.
    /// </summary>
    public static string TickStep(PlayerStats hero)
    {
        if (_left.Count == 0 || hero == null) return null;
        string msg = null;
        int total = 0;
        var keys = new List<StatusEffectType>(_left.Keys);
        foreach (var type in keys)
        {
            Rule r = Rules[type];
            if (_left[type] == UntilNextBattle) continue;
            _walked[type] = (_walked.TryGetValue(type, out int w) ? w : 0) + 1;
            if (r.every > 0 && r.percent > 0f && _walked[type] % r.every == 0 && hero.currentHP > 1)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(hero.maxHP * r.percent));
                int before = hero.currentHP;
                hero.currentHP = Mathf.Max(1, before - dmg);
                int lost = before - hero.currentHP;
                if (lost > 0) { total += lost; msg = (msg == null ? "" : msg + "  ") + $"<color={r.color}>{r.name} -{lost}</color>"; }
            }
            if (--_left[type] <= 0)
            {
                _left.Remove(type);
                _walked.Remove(type);
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
            bool ok = hero.ApplyStatusEffect(so, type == StatusEffectType.Stun ? 1 : Mathf.Max(1, so.physicalDuration));
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
    [Serializable] public class SaveEntry { public int type, left, walked; }

    public static List<SaveEntry> ToSave()
    {
        var list = new List<SaveEntry>();
        foreach (var kv in _left)
            list.Add(new SaveEntry { type = (int)kv.Key, left = kv.Value, walked = _walked.TryGetValue(kv.Key, out int w) ? w : 0 });
        return list;
    }

    public static void FromSave(List<SaveEntry> list)
    {
        _left.Clear();
        _walked.Clear();
        if (list != null)
            foreach (var e in list)
            {
                var t = (StatusEffectType)e.type;
                if (!Rules.ContainsKey(t)) continue;
                _left[t] = e.left;
                _walked[t] = e.walked;
            }
        OnChanged?.Invoke();
    }
}
