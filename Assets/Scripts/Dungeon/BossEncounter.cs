using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-10-09] 층 보스. B10 마지막 계단(내려가는 계단)을 밟으면 스켈레톤 나이트 1마리와 싸운다.
/// 이기면 그 층 보스는 끝 — 그다음부터 계단으로 그냥 내려간다. 보스전에서는 도망칠 수 없고 보스도 도망치지 않는다.
/// </summary>
public static class BossEncounter
{
    public const int BossFloor = 10;
    public const string BossName = "Skeleton Knight";
    private const string BossResource = "Monsters/SkeletonKnight";

    /// <summary>보스를 쓰러뜨린 층 (저장됨 — SaveSystem).</summary>
    public static readonly HashSet<int> defeatedFloors = new HashSet<int>();

    public static bool HasBoss(int floor) => floor == BossFloor && !defeatedFloors.Contains(floor);

    public static Abyssdawn.MonsterSO LoadBoss()
    {
        var so = Resources.Load<Abyssdawn.MonsterSO>(BossResource);
        if (so == null) Debug.LogError($"[BossEncounter] 보스 MonsterSO 를 찾지 못했습니다: Resources/{BossResource}");
        return so;
    }

    public static bool IsBoss(Abyssdawn.MonsterSO so) => so != null && so.MonsterName == BossName;

    public static bool IsBoss(EnemyStats e) => e != null && IsBoss(e.sourceMonster);
}
