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
        // (구) statData 단일 소스 가드 제거 — staticPartyData에서 씬 전환 후 복원

        if (staticPartyData.TryGetValue(player.playerName, out PartyMemberData data))
        {
            Debug.Log($"[GM:DIAG] Loaded from dict | HP={data.currentHP}, MP={data.currentMP}, EXP={data.exp}, Lv={data.level}");

            player.level = data.level;
            // jobClass, maxHP, maxMP, attack, defense, magic, agility, luck are now read-only properties
            // They are calculated from CharacterClass SO, so we don't restore them
            player.exp = data.exp;
            player.maxExp = data.maxExp;
            player.currentHP = Mathf.Clamp(data.currentHP, 0, player.maxHP);
            player.currentMP = Mathf.Clamp(data.currentMP, 0, player.maxMP);
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
