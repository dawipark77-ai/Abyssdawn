using UnityEngine;

public static class DungeonPersistentData
{
    public static bool hasSavedState = false;
    public static int currentSeed = -1;
    public static int currentFloor = 1; // 1층부터 시작 (B1)
    public static Vector2Int lastPlayerGridPos;
    public static DungeonDirection lastPlayerFacing;
    // 현재 층의 드러난 지도. 층을 옮기면 그 층 기록(floors)의 revealed 로 바뀐다.
    public static System.Collections.Generic.HashSet<Vector2Int> revealedTiles = new System.Collections.Generic.HashSet<Vector2Int>();
    // 마을 입구로 나갔을 때 돌아올 던전 씬 (DungeonTownGate)
    public static string returnSceneName;

    // 층별 탐험 기록 (시드·지도·보물상자·함정·샘). 위층으로 돌아가도 그대로 복원된다.
    public static readonly System.Collections.Generic.Dictionary<int, DungeonFloorState> floors =
        new System.Collections.Generic.Dictionary<int, DungeonFloorState>();

    // 위험도 게이지 (0~1, 세계수의 미궁식). 가득 차면 반드시 전투. 전투가 시작되면 0.
    public static float danger;
    // [2026-10-11] 보급술 '단골 손님': 여관 → 다음 층 첫 3번 전투 최대 HP·MP +%
    public static bool innVigorPending;
    public static int innVigorBattlesLeft;
    public static float innVigorPercent;

    // 플레이어가 들고 있는 빛이 남은 걸음 수 (0 이면 빛 없음). 시야 +2 (MapManager.SightRadiusAt)
    public static int playerLightSteps;

    /// <summary>floor 층의 기록. 없으면 새 시드로 만든다.</summary>
    public static DungeonFloorState GetOrCreateFloor(int floor)
    {
        DungeonFloorState state;
        if (!floors.TryGetValue(floor, out state))
        {
            state = new DungeonFloorState(floor, Random.Range(int.MinValue, int.MaxValue));
            floors[floor] = state;
        }
        return state;
    }

    // Player Stats Persistence
    public static bool hasPlayerStats = false;
    public static int heroHP;
    public static int heroMaxHP;
    public static int heroMP;
    public static int heroMaxMP;
    public static bool heroIgnited;
    public static int heroIgniteTurns;

    public static void ClearState()
    {
        hasSavedState = false;
        currentSeed = -1;
        currentFloor = 1;
        revealedTiles = new System.Collections.Generic.HashSet<Vector2Int>();
        returnSceneName = null;
        floors.Clear();
        danger = 0f;
        playerLightSteps = 0;
        DungeonFieldStatus.Clear(); // 함정 상태이상
        BossEncounter.defeatedFloors.Clear(); // [2026-10-09] 층 보스
        innVigorPending = false; innVigorBattlesLeft = 0; innVigorPercent = 0f; PlayerStats.InnVigorPercent = 0f;

        hasPlayerStats = false;
        heroHP = 0;
        heroMaxHP = 0;
        heroMP = 0;
        heroMaxMP = 0;
        heroIgnited = false;
        heroIgniteTurns = 0;
    }

    public static void SavePlayerState(PlayerStats player)
    {
        if (player == null) return;
        
        hasPlayerStats = true;
        heroHP = player.currentHP;
        heroMaxHP = player.maxHP;
        heroMP = player.currentMP;
        heroMaxMP = player.maxMP;
        heroIgnited = player.isIgnited;
        heroIgniteTurns = player.igniteTurnsRemaining;
        
        Debug.Log($"[DungeonPersistentData] Saved Player State: HP {heroHP}/{heroMaxHP}, MP {heroMP}/{heroMaxMP}, Ignited: {heroIgnited}");
    }

    public static void LoadPlayerState(PlayerStats player)
    {
        if (player == null || !hasPlayerStats) return;

        player.currentHP = heroHP;
        player.currentMP = heroMP;
        // 상태이상 복원(Ignite 등)은 SO 참조가 필요하므로 GameManager가 담당합니다.
        // 호출 후 GameManager.Instance.RestoreIgniteFromDungeon(player) 를 함께 호출하세요.

        Debug.Log($"[DungeonPersistentData] Loaded Player State: HP {heroHP}/{heroMaxHP}, MP {heroMP}/{heroMaxMP}, Ignited(turns): {heroIgniteTurns}");
    }

    /// <summary>
    /// 저장된 Ignite 잔여 턴 수를 반환합니다.
    /// GameManager.RestoreIgniteFromDungeon()에서 사용합니다.
    /// </summary>
    public static int GetIgniteTurns() => heroIgniteTurns;
}
