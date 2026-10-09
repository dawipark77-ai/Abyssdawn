using Abyssdawn;

/// <summary>
/// 이번 인카운터에 나올 몬스터를 던전에서 미리 정해 둔다.
/// 전환 연출(몬스터 실루엣)이 "실제로 나올 몬스터"를 보여주고, 전투 씬은 같은 몬스터로 시작하게 하기 위함.
/// static 이라 씬이 바뀌어도 유지된다. BattleManager 가 전투 시작 때 한 번 꺼내 쓰고 지운다.
/// </summary>
public static class EncounterPlan
{
    private static MonsterSO[] _pending;

    public static bool HasPending => _pending != null && _pending.Length > 0;

    public static void Set(MonsterSO[] monsters)
    {
        _pending = monsters;
    }

    /// <summary>[2026-10-09] 습격 (야영 중 등): 다음 전투 첫 라운드는 적만 행동한다. 전투 시작 때 한 번 꺼내 쓰고 지운다.</summary>
    public static bool ambush;

    public static bool TakeAmbush()
    {
        bool a = ambush;
        ambush = false;
        return a;
    }

    /// <summary>미리 정한 몬스터를 꺼낸다 (한 번만). 없으면 null → 전투 씬이 평소처럼 직접 뽑는다.</summary>
    public static MonsterSO[] Take()
    {
        MonsterSO[] m = _pending;
        _pending = null;
        return m != null && m.Length > 0 ? m : null;
    }
}
