using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 랜덤 인카운터 + 위험도 게이지 (세계수의 미궁식).
/// 걸을 때마다 위험도가 조금씩(랜덤) 차오르고, 차오를수록 전투 확률이 오른다. 가득 차면 반드시 전투.
/// 화면의 게이지 색(초록 → 노랑 → 빨강)으로 "곧 싸우게 된다"는 긴장감을 준다.
/// 평균 전투 간격은 encounterChance 로 조절한다 (0.09 ≈ 평균 10~11걸음).
/// </summary>
public class DungeonEncounter : MonoBehaviour
{
    public static DungeonEncounter Instance { get; private set; }

    /// <summary>위험도(0~1)가 바뀔 때마다 알림 (HUD 게이지용).</summary>
    public static event Action<float> OnDangerChanged;
    public static float Danger => DungeonPersistentData.danger;

    [Header("Encounter Settings")]
    [Range(0f, 1f)]
    public float encounterChance = 0.15f;

    [Tooltip("전투 복귀 후 인카운터가 발생하지 않는 이동 횟수")]
    public int postBattleCooldownSteps = 3;

    [Header("위험도 게이지")]
    [Tooltip("한 걸음에 차오르는 위험도 = encounterChance × 이 값 × (0.5~1.5 랜덤). 1이면 평균 1/encounterChance 걸음에 가득 참")]
    public float dangerFillRate = 0.9f;
    [Tooltip("게이지가 가득 차기 전 조기 전투 확률 = encounterChance × 이 값 × 위험도")]
    public float earlyEncounterFactor = 0.5f;

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

        if (encounterChance <= 0f) return;

        float danger = Mathf.Clamp01(Danger + UnityEngine.Random.Range(0.5f, 1.5f) * encounterChance * dangerFillRate);
        SetDanger(danger);

        float chance = danger >= 1f ? 1f : encounterChance * earlyEncounterFactor * danger;
        float roll = UnityEngine.Random.value;
        Debug.Log("[DungeonEncounter] Checking encounter at " + pos + ". Danger: " + danger.ToString("F2") + ", Roll: " + roll.ToString("F2") + ", Chance: " + chance.ToString("F3"));
        if (roll < chance)
        {
            StartEncounter();
        }
    }

    /// <summary>경보 함정 등으로 즉시 전투.</summary>
    public void ForceEncounter()
    {
        StartEncounter();
    }

    public static void SetDanger(float value)
    {
        DungeonPersistentData.danger = Mathf.Clamp01(value);
        OnDangerChanged?.Invoke(DungeonPersistentData.danger);
    }

    void StartEncounter()
    {
        Debug.Log("[DungeonEncounter] >>> STARTING ENCOUNTER! <<<");
        SetDanger(0f);

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
