using UnityEngine;

public static class DungeonPersistentData
{
    public static bool hasSavedState = false;
    public static int currentSeed = -1;
    public static int currentFloor = 1; // 1층부터 시작 (B1)
    public static Vector2Int lastPlayerGridPos;
    public static DungeonDirection lastPlayerFacing;
    public static System.Collections.Generic.HashSet<Vector2Int> revealedTiles = new System.Collections.Generic.HashSet<Vector2Int>();

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
        revealedTiles.Clear();
        
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
