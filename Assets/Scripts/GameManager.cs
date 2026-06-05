using UnityEngine;
using System;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // Static instance
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager (Auto Created)");
                    _instance = go.AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class PartyMemberData
    {
        public string characterName;
        public int level;
        public string jobClass;
        public int exp;
        public int maxExp;
        public int maxHP;
        public int currentHP;
        public int maxMP;
        public int currentMP;
        public int attack;
        public int defense;
        public int magic;
        public int agility;
        public int luck;
        public bool isIgnited;
        public int igniteTurnsRemaining;

        public int baseHP;
        public int baseMP;
        public int baseAttack;
        public int baseDefense;
        public int baseMagic;
        public int baseAgility;
        public int baseLuck;

        // [2026-05-24] 자유 분배 포인트 + 분배 누적치 + 스킬 포인트(LP) 영속화
        // 누락 시 씬 전환마다 0으로 리셋되어 PlusButton/SkillTree가 깨짐.
        public int freeStatPoints;
        public int allocatedAttack;
        public int allocatedDefense;
        public int allocatedMagic;
        public int allocatedAgility;
        public int allocatedLuck;
        public int skillPoints;

        public PartyMemberData(PlayerStats stats)
        {
            characterName = stats.playerName;
            level = stats.level;
            jobClass = stats.jobClass;
            exp = stats.exp;
            maxExp = stats.maxExp;
            maxHP = stats.maxHP;
            currentHP = stats.currentHP;
            maxMP = stats.maxMP;
            currentMP = stats.currentMP;
            attack = stats.attack;
            defense = stats.defense;
            magic = stats.magic;
            agility = stats.Agility;
            luck = stats.luck;
            isIgnited = stats.isIgnited;
            igniteTurnsRemaining = stats.igniteTurnsRemaining;

            baseHP = stats.baseHP;
            baseMP = stats.baseMP;
            baseAttack = stats.baseAttack;
            baseDefense = stats.baseDefense;
            baseMagic = stats.baseMagic;
            baseAgility = stats.baseAgility;
            baseLuck = stats.baseLuck;

            // [2026-05-24] 분배 상태 캡처
            freeStatPoints    = stats.FreeStatPoints;
            allocatedAttack   = stats.AllocatedAttack;
            allocatedDefense  = stats.AllocatedDefense;
            allocatedMagic    = stats.AllocatedMagic;
            allocatedAgility  = stats.AllocatedAgility;
            allocatedLuck     = stats.AllocatedLuck;
            skillPoints       = stats.skillPoints;
        }
    }

    // CRITICAL: Using STATIC dictionary to ensure data persists even if component is re-created or missing GUID
    // [2026-05-11] OrdinalIgnoreCase — 'hero' / 'Hero' 같은 케이스 차이로 키 분리되는 버그 방지
    public static Dictionary<string, PartyMemberData> staticPartyData
        = new Dictionary<string, PartyMemberData>(StringComparer.OrdinalIgnoreCase);

    // Backward-compatible alias for older code paths.
    public Dictionary<string, PartyMemberData> partyData => staticPartyData;

    // For Inspector debugging (Optional)
    [SerializeField]
    private List<PartyMemberData> debugPartyList = new List<PartyMemberData>();

    public bool hasPlayerSnapshot { get { return staticPartyData.Count > 0; } }

    public static GameManager EnsureInstance()
    {
        return Instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ClearAllData()
    {
        staticPartyData.Clear();
        debugPartyList.Clear();
        Debug.Log("[GameManager] All party data cleared!");
    }

    /// <summary>
    /// DungeonPersistentData에 저장된 Ignite 턴 수를 기반으로
    /// 플레이어에게 Ignite 상태이상을 복원합니다.
    /// DungeonPersistentData.LoadPlayerState 직후에 호출하세요.
    /// </summary>
    public void RestoreIgniteFromDungeon(PlayerStats player)
    {
        if (player == null) return;
        int turns = DungeonPersistentData.heroIgniteTurns;
        if (turns <= 0) return;

        var ignite = Resources.Load<AbyssdawnBattle.StatusEffectSO>("StatusEffects/Curse_Ignite");
        if (ignite != null)
        {
            player.ApplyStatusEffect(ignite, turns);
            Debug.Log($"[GameManager] 던전 상태 복원 — {player.playerName}에게 Ignite {turns}턴 적용");
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying) SyncDebugList();
    }

    void SyncDebugList()
    {
        debugPartyList.Clear();
        foreach (var data in staticPartyData.Values)
        {
            debugPartyList.Add(data);
        }
    }

    public void SaveFromPlayer(PlayerStats player)
    {
        Debug.Log($"[GM:DIAG] SaveFromPlayer called | player={(player != null ? player.gameObject.name : "NULL")} | InstanceID={(player != null ? player.GetInstanceID() : 0)} | name='{player?.playerName}'");

        if (player == null)
        {
            Debug.LogWarning("[GM:DIAG] Save ABORT: player null");
            return;
        }

        // [2026-05-25 동료 HP 뭉개짐 수정] 영입 동료는 staticPartyData 저장 제외.
        // 같은 종 동료는 playerName 키가 동일해 충돌 → 동료의 진실의 소스는 CompanionPartyPersistence.ActiveRoster.
        // SaveFromPlayer 구현부 1곳에서 막아 모든 호출처를 일괄 차단.
        if (player.IsRecruitedCompanion)
        {
            Debug.Log($"[GM-Skip] 동료 '{player.playerName}' (companionId={player.companionId}) GameManager 저장 스킵 — ActiveRoster가 소스");
            return;
        }
        // (구) statData 단일 소스 가드 제거 — 런타임은 PlayerStats, 씬 전환 시 staticPartyData로 영속화

        if (string.IsNullOrEmpty(player.playerName))
        {
            Debug.LogWarning($"[GM:DIAG] Save ABORT: playerName empty (GO='{player.gameObject.name}')");
            Debug.LogWarning("[GameManager] Cannot save player with empty name!");
            return;
        }

        bool _diagIsNew = !staticPartyData.ContainsKey(player.playerName);

        if (staticPartyData.ContainsKey(player.playerName))
        {
            staticPartyData[player.playerName] = new PartyMemberData(player);
            Debug.Log("[SERIALIZATION_FIX] Saved UPDATED: " + player.playerName + " HP: " + player.currentHP + "/" + player.maxHP);
        }
        else
        {
            staticPartyData.Add(player.playerName, new PartyMemberData(player));
            Debug.Log("[SERIALIZATION_FIX] Saved ADDED: " + player.playerName + " HP: " + player.currentHP + "/" + player.maxHP);
        }

        Debug.Log($"[GM:DIAG] {(_diagIsNew ? "ADDED" : "UPDATED")} '{player.playerName}' | HP={player.currentHP}, MP={player.currentMP}, EXP={player.exp}, Lv={player.level} | Dict count={staticPartyData.Count}");

        SyncDebugList();
    }

    public void ApplyToPlayer(PlayerStats player)
    {
        Debug.Log($"[GM:DIAG] ApplyToPlayer called | name='{player?.playerName}' | InstanceID={(player != null ? player.GetInstanceID() : 0)}");

        if (player == null)
        {
            Debug.LogWarning("[GM:DIAG] Apply ABORT: player null");
            return;
        }

        // [2026-05-25 동료 HP 뭉개짐 수정] 영입 동료는 staticPartyData 복원 제외 (playerName 키 공유 방지).
        // 동료의 진실의 소스는 CompanionPartyPersistence.ActiveRoster. ApplyToPlayer 구현부 1곳에서 일괄 차단.
        if (player.IsRecruitedCompanion)
        {
            Debug.Log($"[GM-Skip] 동료 '{player.playerName}' (companionId={player.companionId}) GameManager 복원 스킵 (ApplyToPlayer) — ActiveRoster가 소스");
            return;
        }
        // (구) statData 단일 소스 가드 제거 — staticPartyData에서 씬 전환 후 복원

        if (staticPartyData.TryGetValue(player.playerName, out PartyMemberData data))
        {
            Debug.Log($"[GM:DIAG] Loaded from dict | HP={data.currentHP}, MP={data.currentMP}, EXP={data.exp}, Lv={data.level}");

            player.level = data.level;
            // jobClass, maxHP, maxMP, attack, defense, magic, agility, luck are now read-only properties
            // They are calculated from CharacterClass SO, so we don't restore them
            player.exp = data.exp;
            player.maxExp = data.maxExp;

            // [2026-05-24] Base 스탯 7종 복원 (currentHP/MP 클램프 전에 호출 — maxHP가 base에 의존)
            // 이 호출 이전: PlayerStats가 SO에서 lazy 시드하던 값이 들어있을 수 있음.
            // 이 호출 이후: PartyMemberData에 영속화된 값이 진실의 단일 소스.
            player.RestoreBaseStats(
                data.baseHP,
                data.baseMP,
                data.baseAttack,
                data.baseDefense,
                data.baseMagic,
                data.baseAgility,
                data.baseLuck);

            player.currentHP = Mathf.Clamp(data.currentHP, 0, player.maxHP);
            player.currentMP = Mathf.Clamp(data.currentMP, 0, player.maxMP);

            // [2026-05-24] 자유 분배 포인트 + 분배 누적치 + 스킬 포인트 복원
            // currentHP/MP 복원 후 호출해야 maxHP/MP가 분배 누적치 반영된 정확한 값으로 클램프됨.
            player.RestoreAllocations(
                data.freeStatPoints,
                data.allocatedAttack,
                data.allocatedDefense,
                data.allocatedMagic,
                data.allocatedAgility,
                data.allocatedLuck,
                data.skillPoints);

            if (data.isIgnited && data.igniteTurnsRemaining > 0)
            {
                var ignite = Resources.Load<AbyssdawnBattle.StatusEffectSO>("StatusEffects/Curse_Ignite");
                if (ignite != null)
                    player.ApplyStatusEffect(ignite, data.igniteTurnsRemaining);
            }

            Debug.Log("[SERIALIZATION_FIX] Loaded: " + player.playerName + " HP: " + player.currentHP + "/" + player.maxHP);
            Debug.Log($"[GM:DIAG] Applied to player | name='{player.playerName}' | final HP={player.currentHP}/{player.maxHP}, MP={player.currentMP}/{player.maxMP}, EXP={player.exp}, Lv={player.level}");
        }
        else
        {
            string _diagKeysStr = staticPartyData.Count > 0 ? string.Join(",", staticPartyData.Keys) : "(empty)";
            Debug.LogWarning($"[GM:DIAG] Apply ABORT: key '{player.playerName}' not found. Count={staticPartyData.Count}, Keys=[{_diagKeysStr}]");
            Debug.Log("[SERIALIZATION_FIX] No data for: " + player.playerName + ". Initializing...");
            SaveFromPlayer(player);
        }
    }
}
