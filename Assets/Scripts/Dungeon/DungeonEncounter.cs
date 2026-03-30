using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonEncounter : MonoBehaviour
{
    public static DungeonEncounter Instance { get; private set; }

    [Header("Encounter Settings")]
    [Range(0f, 1f)]
    public float encounterChance = 0.15f;

    [Tooltip("전투 복귀 후 인카운터가 발생하지 않는 이동 횟수")]
    public int postBattleCooldownSteps = 3;

    public string battleSceneName = "Abyysborn_Battle 01";
    public static string lastDungeonScene;

    private int _stepsSinceReturn = 0;
    public static bool justReturnedFromBattle = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 전투 복귀 시 쿨다운 시작
        if (justReturnedFromBattle)
        {
            _stepsSinceReturn = 0;
            justReturnedFromBattle = false;
            Debug.Log($"[DungeonEncounter] 전투 복귀 — {postBattleCooldownSteps}칸 인카운터 쿨다운 시작");
        }
        else
        {
            // 새 세션이면 쿨다운 없음
            _stepsSinceReturn = postBattleCooldownSteps;
        }
    }

    public void CheckEncounter(Vector2Int pos)
    {
        // 쿨다운 중이면 인카운터 스킵
        if (_stepsSinceReturn < postBattleCooldownSteps)
        {
            _stepsSinceReturn++;
            Debug.Log($"[DungeonEncounter] 쿨다운 중 ({_stepsSinceReturn}/{postBattleCooldownSteps}) — 인카운터 스킵");
            return;
        }

        float roll = UnityEngine.Random.value;
        Debug.Log("[DungeonEncounter] Checking encounter at " + pos + ". Roll: " + roll.ToString("F2") + ", Chance: " + encounterChance.ToString("F2"));
        if (roll < encounterChance)
        {
            StartEncounter();
        }
    }

    void StartEncounter()
    {
        Debug.Log("[DungeonEncounter] >>> STARTING ENCOUNTER! <<<");

        lastDungeonScene = SceneManager.GetActiveScene().name;

        DungeonGridPlayer dPlayer = FindFirstObjectByType<DungeonGridPlayer>();
        if (dPlayer != null)
        {
            DungeonPersistentData.lastPlayerGridPos = dPlayer.gridPos;
            DungeonPersistentData.lastPlayerFacing = dPlayer.facing;
            DungeonPersistentData.hasSavedState = true;
            Debug.Log("[DungeonEncounter] Saving grid state: " + dPlayer.gridPos + ", facing " + dPlayer.facing);
        }

        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            var gm = GameManager.EnsureInstance();
            gm.SaveFromPlayer(stats);
            Debug.Log("[DungeonEncounter] Saved " + stats.playerName + " stats to GM. HP: " + stats.currentHP + "/" + stats.maxHP);
        }

        Debug.Log("[DungeonEncounter] Loading battle scene: " + battleSceneName);
        SceneManager.LoadScene(battleSceneName);
    }
}
