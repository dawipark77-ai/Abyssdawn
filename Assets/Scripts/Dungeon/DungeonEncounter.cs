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

        float before = Danger;
        float danger = Mathf.Clamp01(Danger + UnityEngine.Random.Range(0.5f, 1.5f) * encounterChance * dangerFillRate);
        SetDanger(danger);
        WarnIfSensed(before, danger);

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

    /// <summary>그물 함정 등: 위험도를 한 번에 올린다. 가득 차면 잠깐 뒤 전투.</summary>
    public void AddDanger(float amount)
    {
        float before = Danger;
        SetDanger(Danger + amount);
        WarnIfSensed(before, Danger);
        if (Danger >= 1f) StartCoroutine(EncounterAfter(0.8f));
    }

    private System.Collections.IEnumerator EncounterAfter(float seconds)
    {
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
        yield return new WaitForSeconds(seconds);
        if (player != null) player.UnlockInput(this);
        StartEncounter();
    }

    // [2026-10-07] 탐험 스킬 '위험 예지': 위험도가 80% 를 넘는 순간 경고. 전투에서는 첫 턴 회피 +20% (BattleManager)
    public const float DangerSenseWarnAt = 0.8f;

    private static void WarnIfSensed(float before, float after)
    {
        if (before >= DangerSenseWarnAt || after < DangerSenseWarnAt) return;
        if (!FieldSkills.Has(FieldSkills.DangerSense)) return;
        var hud = DungeonHud.Instance;
        if (hud != null) hud.Toast("<color=#FFB060>You sense something stalking you...</color>");
    }

    void StartEncounter()
    {
        if (EncounterTransition.IsPlaying) return; // 전환 연출 중 중복 발동 방지
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

        // 이번에 나올 몬스터를 미리 정해 둔다 (전환 연출의 실루엣 = 실제로 나올 몬스터, 전투 씬이 같은 몬스터로 시작)
        Abyssdawn.MonsterSO[] monsters = BattleManager.LoadMonsterSOsForFloor(DungeonPersistentData.currentFloor);
        EncounterPlan.Set(monsters);

        Debug.Log("[DungeonEncounter] Loading battle scene with transition: " + battleSceneName);
        EncounterTransition.EnterBattle(battleSceneName, monsters);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 테스트용: Ctrl+E = 즉시 인카운터 (전환 연출 확인용)
    void Update()
    {
        if (EncounterTransition.IsPlaying) return;
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("[DungeonEncounter] (테스트) Ctrl+E 강제 인카운터");
            StartEncounter();
        }
    }
#endif
}
