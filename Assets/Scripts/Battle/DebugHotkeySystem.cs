using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Abyssdawn;

/// <summary>
/// ?붾쾭洹??ロ궎 ?쒖뒪?? F1~F10?쇰줈 寃뚯엫 ?곹깭 ?꾪솚, ???뚰솚, ?뚯뒪??湲곕뒫 ?ㅽ뻾
/// </summary>
public class DebugHotkeySystem : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;
    public PlayerStats playerStats;
    public List<EnemyStats> enemyPrefabs = new List<EnemyStats>(); // ?몄뒪?숉꽣?먯꽌 ?좊떦?????꾨━?밸뱾

    [Header("-- Test Companion --")]
    [Tooltip("Test companion MonsterSO. Press F11 to add to current party.")]
    public MonsterSO testCompanion;

    [Header("Spawn Settings")]
    public Transform spawnCenter; // ???앹꽦 以묒떖 ?꾩튂
    public float spawnOffset = 2f; // ??媛꾧꺽
    
    [Header("Debug Settings")]
    public bool enableDebugMode = true; // ?붾쾭洹?紐⑤뱶 耳쒓린/?꾧린
    
    private List<EnemyStats> spawnedEnemies = new List<EnemyStats>();
    
    // 寃뚯엫 ?곹깭 ?닿굅??
    public enum GameState
    {
        Exploration,  // ?먯깋 紐⑤뱶
        Battle,       // ?꾪닾 紐⑤뱶
        Menu,         // 硫붾돱(?ㅽ뀒?댄꽣???λ퉬/?ㅽ궗 李?
        Paused        // ?쇱떆?뺤?
    }
    
    private GameState currentState = GameState.Exploration;
    
    void Awake()
    {
        if (battleManager == null)
        {
            battleManager = Object.FindFirstObjectByType<BattleManager>();
        }
    }

    void Update()
    {
        if (!enableDebugMode) return;
        
        // ?ロ궎 泥섎━
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SwitchToExplorationMode();
        }
        else if (Input.GetKeyDown(KeyCode.F2))
        {
            StartBattle();
        }
        else if (Input.GetKeyDown(KeyCode.F3))
        {
            SpawnRandomEnemy();
        }
        else if (Input.GetKeyDown(KeyCode.F4))
        {
            SpawnMultipleEnemies();
        }
        else if (Input.GetKeyDown(KeyCode.F5))
        {
            RestartBattleWithRandomEnemy();
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
              && Input.GetKeyDown(KeyCode.F11))
            LogAllCombatantStats();
        else if (Input.GetKeyDown(KeyCode.F11))
            AddTestCompanion();
        else if (Input.GetKeyDown(KeyCode.F6))
        {
            HealPlayer();
        }
        else if (Input.GetKeyDown(KeyCode.F7))
        {
            DamagePlayer(10);
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F8))
        {
            ResetBattle();
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F9))
        {
            TogglePause();
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F10))
        {
            LogCurrentStatus();
        }
        
        // 異붽?: ?レ옄?ㅻ줈 ?뱀젙 ???뚰솚 (1~9)
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) && enemyPrefabs.Count >= i)
            {
                SpawnEnemyByIndex(i - 1);
            }
        }
    }
    
    // F1: ?먯깋 紐⑤뱶濡??꾪솚
    private void SwitchToExplorationMode()
    {
        currentState = GameState.Exploration;
        if (battleManager != null)
        {
            battleManager.actionPanel.SetActive(false);
            battleManager.skillPanel.SetActive(false);
            battleManager.battleEnded = true;
        }
        Debug.Log("[DEBUG] Switched to Exploration Mode");
    }
    
    // F2: ?꾪닾 ?쒖옉
    private void StartBattle()
    {
        currentState = GameState.Battle;
        if (battleManager != null)
        {
            battleManager.battleEnded = false;
            battleManager.StartBattle();
        }
        Debug.Log("[DEBUG] Battle Started");
    }
    
    // F3: ?쒕뜡 ??1留덈━ ?뚰솚
    private void SpawnRandomEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("[DEBUG] No enemy prefabs assigned!");
            return;
        }
        
        EnemyStats randomEnemy = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
        SpawnEnemyAtPosition(randomEnemy, GetSpawnPosition(0));
        Debug.Log($"[DEBUG] Spawned random enemy: {randomEnemy.enemyName}");
    }
    
    // F4: ?쒕뜡 ??1~4留덈━ ?뚰솚 (以묒븰 ?移?諛곗튂)
    private void SpawnMultipleEnemies()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("[DEBUG] No enemy prefabs assigned!");
            return;
        }
        
        ClearAllEnemies(); // 湲곗〈 ???쒓굅
        
        int enemyCount = UnityEngine.Random.Range(1, 5); // 1~4留덈━
        Vector3 center = spawnCenter != null ? spawnCenter.position : Vector3.zero;
        
        for (int i = 0; i < enemyCount; i++)
        {
            EnemyStats randomEnemy = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
            Vector3 pos = GetSymmetricPosition(center, enemyCount, i);
            SpawnEnemyAtPosition(randomEnemy, pos);
        }
        
        Debug.Log($"[DEBUG] Spawned {enemyCount} enemies (symmetric around center)");
    }
    
    // F5: 紐⑤뱺 ???쒓굅
    private void ClearAllEnemies()
    {
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }
        spawnedEnemies.Clear();
        Debug.Log("[DEBUG] Cleared all enemies");
    }
    
    // F6: ?뚮젅?댁뼱 ?뚮났
    private void HealPlayer()
    {
        if (playerStats != null)
        {
            playerStats.Heal(999); // ?꾩쟾 ?뚮났
            Debug.Log($"[DEBUG] Player healed. HP: {playerStats.currentHP}/{playerStats.maxHP}");
        }
    }
    
    // F7: ?뚮젅?댁뼱 ?곕?吏
    private void DamagePlayer(int damage)
    {
        if (playerStats != null)
        {
            playerStats.TakeDamage(damage);
            Debug.Log($"[DEBUG] Player took {damage} damage. HP: {playerStats.currentHP}/{playerStats.maxHP}");
        }
    }
    
    // F8: ?꾪닾 由ъ뀑
    private void ResetBattle()
    {
        ClearAllEnemies();
        if (battleManager != null)
        {
            battleManager.battleEnded = false;
            battleManager.playerTurn = true;
        }
        if (playerStats != null)
        {
            playerStats.currentHP = playerStats.maxHP;
            playerStats.currentMP = playerStats.maxMP;
        }
        Debug.Log("[DEBUG] Battle reset");
    }

    // F5: ?꾪닾 ?꾩껜 由ъ뀑 ???덈줈?????쒕뜡 ?뚰솚
    private void RestartBattleWithRandomEnemy()
    {
        Debug.Log("[DEBUG] Restarting battle (F5)");

        // 湲곕낯 由ъ뀑 濡쒖쭅 ?섑뻾
        ResetBattle();

        if (battleManager == null)
        {
            battleManager = Object.FindFirstObjectByType<BattleManager>();
        }

        // BattleManager媛 ?덉쑝硫????꾪닾 ?쒖옉 (EnemyDatabase瑜??듯빐 ?쒕뜡 ???뚰솚)
        if (battleManager != null)
        {
            battleManager.StopAllCoroutines();
            battleManager.StartBattle();
        }
        else
        {
            // BattleManager媛 ?놁쑝硫??붾쾭洹몄슜 ?꾨━?뱀뿉???쒕뜡 ???뚰솚
            ClearAllEnemies();
            SpawnRandomEnemy();
        }
    }
    
    // F9: ?쇱떆?뺤? ?좉?
    private void TogglePause()
    {
        if (Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            currentState = GameState.Paused;
            Debug.Log("[DEBUG] Game Paused");
        }
        else
        {
            Time.timeScale = 1f;
            Debug.Log("[DEBUG] Game Resumed");
        }
    }
    
    // F10: ?꾩옱 ?곹깭 濡쒓렇
    private void LogCurrentStatus()
    {
        Debug.Log("=== DEBUG STATUS ===");
        Debug.Log($"Game State: {currentState}");
        if (playerStats != null)
        {
            Debug.Log($"Player HP: {playerStats.currentHP}/{playerStats.maxHP}, MP: {playerStats.currentMP}/{playerStats.maxMP}");
            Debug.Log($"Player ATK: {playerStats.attack}, DEF: {playerStats.defense}, MAG: {playerStats.magic}");
        }
        Debug.Log($"Spawned Enemies: {spawnedEnemies.Count}");
        if (battleManager != null)
        {
            Debug.Log($"Battle Ended: {battleManager.battleEnded}, Player Turn: {battleManager.playerTurn}");
        }
    }
    
    // ?レ옄?? ?뱀젙 ???뚰솚 (?몃뜳??
    private void SpawnEnemyByIndex(int index)
    {
        if (index < 0 || index >= enemyPrefabs.Count)
        {
            Debug.LogWarning($"[DEBUG] Invalid enemy index: {index}");
            return;
        }
        
        EnemyStats enemy = enemyPrefabs[index];
        Vector3 pos = GetSpawnPosition(spawnedEnemies.Count);
        SpawnEnemyAtPosition(enemy, pos);
        Debug.Log($"[DEBUG] Spawned enemy by index {index}: {enemy.enemyName}");
    }
    
    // ???뚰솚 ?ы띁
    private void SpawnEnemyAtPosition(EnemyStats enemyPrefab, Vector3 position)
    {
        GameObject enemyObj = Instantiate(enemyPrefab.gameObject, position, Quaternion.identity);
        EnemyStats enemy = enemyObj.GetComponent<EnemyStats>();
        if (enemy != null)
        {
            spawnedEnemies.Add(enemy);
            
            // BattleManager??泥?踰덉㎏ ???곌껐 (?ㅼ쨷 ??吏???꾧퉴吏 ?꾩떆)
            if (battleManager != null && spawnedEnemies.Count == 1)
            {
                battleManager.enemy = enemy;
            }
        }
    }
    
    // 以묒븰 ?移??꾩튂 怨꾩궛 (?쒓?泥섎읆 以묒븰 湲곗? 醫뚯슦 ?쇱묠)
    private Vector3 GetSymmetricPosition(Vector3 center, int totalCount, int index)
    {
        float offset = 0f;
        
        // 以묒븰 湲곗? ?移??ㅽ봽??怨꾩궛
        if (totalCount == 1)
        {
            offset = 0f;
        }
        else if (totalCount == 2)
        {
            offset = (index == 0) ? -spawnOffset * 0.5f : spawnOffset * 0.5f;
        }
        else if (totalCount == 3)
        {
            if (index == 0) offset = -spawnOffset;
            else if (index == 1) offset = 0f;
            else offset = spawnOffset;
        }
        else // 4留덈━
        {
            if (index == 0) offset = -spawnOffset * 1.5f;
            else if (index == 1) offset = -spawnOffset * 0.5f;
            else if (index == 2) offset = spawnOffset * 0.5f;
            else offset = spawnOffset * 1.5f;
        }
        
        // 移대찓??湲곗? right 踰≫꽣 ?ъ슜 (1?몄묶 湲곗?)
        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        return center + right * offset;
    }
    
    // ?⑥씪 ?꾩튂 (湲곕낯)
    private Vector3 GetSpawnPosition(int index)
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : Vector3.zero;
        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        return center + right * (index * spawnOffset);
    }

    // F11: Add testCompanion to the active companion party
    private void AddTestCompanion()
    {
        if (testCompanion == null)
        {
            Debug.Log("[Debug] testCompanion이 설정되지 않았습니다.");
            return;
        }
        bool added = CompanionPartyPersistence.TryAddActive(
            testCompanion, testCompanion.HP, testCompanion.MP);
        Debug.Log(added
            ? $"[Debug] {testCompanion.MonsterName} 파티 추가 완료."
            : $"[Debug] {testCompanion.MonsterName} 추가 실패 (파티/대기열 꽉 참).");
    }

    // F12: 전체 전투원(아군+적)의 최종(StatModifier 반영) 스탯을 콘솔에 출력
    private void LogAllCombatantStats()
    {
        battleManager = Object.FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            Debug.Log("[StatLog] BattleManager 미연결");
            return;
        }

        Debug.Log("========== [StatLog] 전체 전투원 스탯 ==========");

        // 아군
        foreach (var member in battleManager.GetActivePartyMembers())
        {
            if (member == null || member.currentHP <= 0) continue;

            float atkFinal  = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack,         member.Attack);
            float defFinal  = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Defense,        member.Defense);
            float magFinal  = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Magic,          member.Magic);
            float spdFinal  = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Speed,          member.Agility);
            float evasion   = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Evasion,        0f);
            float accuracy  = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Accuracy,       1f);
            float critChance= battleManager.criticalChance + member.Luck
                            + member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance,     0f);
            float critDmg   = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage,     1.5f);
            float healRcv   = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.HealingReceived,1f);
            float stResist  = member.ApplyStatModifiers(AbyssdawnBattle.ModStatType.StatusResist,   1f);

            Debug.Log($"[아군] {member.playerName} HP:{member.currentHP}/{member.maxHP}\n" +
                      $"  [기본] ATK:{member.Attack}  DEF:{member.Defense}  " +
                      $"MAG:{member.Magic}  SPD:{member.Agility}  LUK:{member.Luck}\n" +
                      $"  [최종] ATK:{atkFinal:F1}  DEF:{defFinal:F1}  " +
                      $"MAG:{magFinal:F1}  SPD:{spdFinal:F1}  LUK:{member.Luck}\n" +
                      $"  명중배율:{accuracy:F2}  회피:{evasion:F2}  " +
                      $"크리확률:{critChance:F1}%  크리데미지:x{critDmg:F2}\n" +
                      $"  회복효율:x{healRcv:F2}  상태이상저항:x{stResist:F2}");

            Debug.Log($"  [활성 모디파이어] {member.activeStatModifiers.Count}개: " +
                      string.Join(", ", member.activeStatModifiers.Select(m =>
                          $"{m.modifier.statType} {m.modifier.modType} {m.modifier.value:F2} (남은턴:{m.remainingTurns})")));
        }

        // 적
        foreach (var enemy in battleManager.GetActiveEnemies())
        {
            if (enemy == null || enemy.IsDead()) continue;

            float atkFinal  = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack,          enemy.attack);
            float defFinal  = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Defense,         enemy.defense);
            float magFinal  = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Magic,           enemy.magic);
            float spdFinal  = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Speed,           enemy.Agility);
            float evasion   = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Evasion,         0f);
            float accuracy  = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Accuracy,        1f);
            float critChance= battleManager.criticalChance + enemy.luck
                            + enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance,      0f);
            float critDmg   = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage,      1.5f);
            float healRcv   = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.HealingReceived, 1f);
            float stResist  = enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.StatusResist,    1f);

            Debug.Log($"[적] {enemy.enemyName} HP:{enemy.currentHP}/{enemy.maxHP}\n" +
                      $"  [기본] ATK:{enemy.attack}  DEF:{enemy.defense}  " +
                      $"MAG:{enemy.magic}  SPD:{enemy.Agility}  LUK:{enemy.luck}\n" +
                      $"  [최종] ATK:{atkFinal:F1}  DEF:{defFinal:F1}  " +
                      $"MAG:{magFinal:F1}  SPD:{spdFinal:F1}  LUK:{enemy.luck}\n" +
                      $"  명중배율:{accuracy:F2}  회피:{evasion:F2}  " +
                      $"크리확률:{critChance:F1}%  크리데미지:x{critDmg:F2}\n" +
                      $"  회복효율:x{healRcv:F2}  상태이상저항:x{stResist:F2}");

            Debug.Log($"  [활성 모디파이어] {enemy.activeStatModifiers.Count}개: " +
                      string.Join(", ", enemy.activeStatModifiers.Select(m =>
                          $"{m.modifier.statType} {m.modifier.modType} {m.modifier.value:F2} (남은턴:{m.remainingTurns})")));
        }

        Debug.Log("=================================================");
    }

}


