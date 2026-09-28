using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 마을 입구 타일 ↔ 마을 씬 전환.
/// 마을은 아직 미구현이므로, 마을 층(층 설정표 Town)에는 일반 층 + "마을 입구" 타일(◆)이 생성되고
/// 그 칸을 밟으면 마을 씬으로 넘어간다. 마을 씬이 빌드 설정에 없으면 경고만 남기고 이동하지 않는다.
///
/// 던전 상태(층·시드·위치·드러난 지도)는 전투 인카운터와 같은 방식으로 저장되므로,
/// 마을에서 돌아오면 같은 층의 같은 자리(마을 입구 위)에서 이어진다.
/// </summary>
public static class DungeonTownGate
{
    /// <summary>마을 씬으로 이동. 이동을 시작했으면 true, 마을 씬이 없어 이동하지 않았으면 false.</summary>
    public static bool Enter(string townSceneName, DungeonGridPlayer player)
    {
        if (string.IsNullOrEmpty(townSceneName) || !Application.CanStreamedLevelBeLoaded(townSceneName))
        {
            Debug.LogWarning($"[DungeonTownGate] 마을 입구에 도착했지만 마을 씬 '{townSceneName}'이(가) 빌드 설정에 없어 이동하지 않습니다. " +
                             "(마을 씬을 만든 뒤 File > Build Profiles 에 추가하면 자동으로 연결됩니다)");
            return false;
        }

        if (player != null)
        {
            DungeonPersistentData.lastPlayerGridPos = player.gridPos;
            DungeonPersistentData.lastPlayerFacing = player.facing;
            DungeonPersistentData.hasSavedState = true;

            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats == null) stats = Object.FindFirstObjectByType<PlayerStats>();
            if (stats != null) GameManager.EnsureInstance().SaveFromPlayer(stats);
        }

        DungeonPersistentData.returnSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[DungeonTownGate] B{DungeonPersistentData.currentFloor} 마을 입구 → '{townSceneName}' (복귀 씬: {DungeonPersistentData.returnSceneName})");
        SceneManager.LoadScene(townSceneName);
        return true;
    }

    /// <summary>마을 → 던전 복귀. 떠났던 층·위치에서 이어진다.</summary>
    public static void ReturnToDungeon(string fallbackDungeonScene)
    {
        string scene = string.IsNullOrEmpty(DungeonPersistentData.returnSceneName)
            ? fallbackDungeonScene
            : DungeonPersistentData.returnSceneName;
        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogError("[DungeonTownGate] 돌아갈 던전 씬을 알 수 없습니다.");
            return;
        }
        SceneManager.LoadScene(scene);
    }
}
