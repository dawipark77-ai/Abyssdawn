#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 자동 플레이 테스트 (에디터 전용 — 빌드에는 들어가지 않음).
/// 실제 게임 코드 그대로 던전을 탐험하고 전투한다. 결과는 콘솔에 "[AutoPilot]" 로 남긴다.
///
/// 시작: 플레이 모드에서 DungeonAutoPilot.Begin(runs, speed) — Claude 가 Unity 연결로 호출.
/// 정책 (평범하게 신중한 플레이어):
///  - 층 탐험: 아직 못 본 칸이 없을 때까지 돌아다닌 뒤 계단으로 내려감 (최대 걸음 수 넘으면 바로 계단).
///    길 찾기는 실제 지형을 안다고 가정 (사람보다 약간 효율적) — 아는 함정·올라가는 계단·마을 입구는 피함.
///  - 샘: HP 70% 미만일 때만 사용. 보물: 지나가며 연다.
///  - 회복: 던전에서 HP 50% 미만이면 물약, 35% 미만이면 새벽의 잔. 전투에서 HP 35% 미만이면 물약.
///  - 전투: 공격만 (스킬 없음). 동료 영입은 거절 (1대1 유지).
///  - 레벨업 자유 스탯: STR / DEF 번갈아.
/// 죽으면 게임이 1층부터 새로 시작하므로 다음 판으로 이어진다. stopAtFloor 에 도착하면 그 판을 성공으로 끝낸다.
/// </summary>
public class DungeonAutoPilot : MonoBehaviour
{
    public static DungeonAutoPilot Instance { get; private set; }

    public int runsTotal = 3;
    public float speed = 5f;
    public int stopAtFloor = 11;          // 이 층에 도착하면 성공 (10층 클리어)
    public int maxStepsPerFloor = 900;
    public float stepInterval = 0.12f;

    private int _run = 1;
    private RunStats _stats = new RunStats();
    private readonly List<string> _summaries = new List<string>();
    private float _nextAct;
    private int _lastFloorSeen = -1;
    private bool _deathLogged;
    private bool _finished;
    private int _freeStatToggle;
    private int _stepsThisFloor;
    private bool _inBattle;

    private class RunStats
    {
        public int battles, steps, maxFloor = 1, potions, chalice, springs, chests;
        public int goblinBattles, fleeTries, fleeOk;
        public readonly Dictionary<string, int> skillUses = new Dictionary<string, int>();
        public readonly List<string> learned = new List<string>();
        public float startTime;
        public readonly StringBuilder floorLog = new StringBuilder();
    }

    // ─────────────────────────────────────────
    // 시작 / 종료
    // ─────────────────────────────────────────

    // ─── 예약 시작: Play 를 켜기 전에 SessionState 에 "판수,속도" 를 넣어 두면 Play 가 시작될 때 스스로 시작한다.
    //     (Play 진입 시 코드를 다시 불러와 외부 연결이 끊겨도 자동 플레이는 혼자 돈다. 결과는 Logs/AutoPilot.txt)
    public const string ScheduleKey = "Abyssdawn.AutoPilot.Schedule";
    public static string ResultPath => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/AutoPilot.txt"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfScheduled()
    {
        string s = UnityEditor.SessionState.GetString(ScheduleKey, "");
        if (string.IsNullOrEmpty(s)) return;
        UnityEditor.SessionState.EraseString(ScheduleKey);
        string[] p = s.Split(',');
        int runs = p.Length > 0 && int.TryParse(p[0], out int r) ? r : 3;
        float spd = p.Length > 1 && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 5f;
        int stopAt = p.Length > 2 && int.TryParse(p[2], out int st) ? st : 11;
        string bld = p.Length > 3 ? p[3] : "none";
        bool sword = p.Length > 4 && p[4] == "1";
        try { System.IO.File.AppendAllText(ResultPath, $"[AutoPilot] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} 시작 — {runs}판, 속도 x{spd}, 목표 B{stopAt - 1}, 빌드 {bld}{(sword ? ", 처음부터 조잡한 검" : "")}\n"); } catch { }
        Begin(runs, spd, stopAt, bld, sword);
    }

    private static void AppendResult(string text)
    {
        try { System.IO.File.AppendAllText(ResultPath, text + "\n"); } catch { }
    }

    public static string Begin(int runs, float speed, int stopAtFloor = 11, string build = "none", bool startWithSword = false)
    {
        if (!Application.isPlaying) return "플레이 모드가 아닙니다.";
        if (Instance == null)
        {
            var go = new GameObject("[AutoPilot]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DungeonAutoPilot>();
        }
        Instance.runsTotal = runs;
        Instance.speed = speed;
        Instance.stopAtFloor = stopAtFloor;
        Instance.build = string.IsNullOrEmpty(build) ? "none" : build;
        Instance.startWithSword = startWithSword;
        Instance._stats = new RunStats { startTime = Time.realtimeSinceStartup };
        Instance._run = 1;
        Instance._summaries.Clear();
        Instance._finished = false;
        BattleManager.AutoAdvanceMessages = true;
        Time.timeScale = speed;
        Debug.Log($"[AutoPilot] 시작 — {runs}판, 속도 x{speed}, 목표 B{stopAtFloor - 1} 클리어");
        return "started";
    }

    public static string Report()
    {
        if (Instance == null) return "autopilot not running";
        var sb = new StringBuilder();
        sb.AppendLine($"run={Instance._run}/{Instance.runsTotal} finished={Instance._finished} scene={SceneManager.GetActiveScene().name} floor={DungeonPersistentData.currentFloor}");
        var hero = FindHero();
        if (hero != null) sb.AppendLine($"hero Lv{hero.level} HP {hero.currentHP}/{hero.maxHP} EXP {hero.exp}/{hero.maxExp} ATK {hero.Attack} DEF {hero.Defense} AGI {hero.Agility} LUK {hero.Luck}");
        sb.AppendLine($"battles={Instance._stats.battles} steps={Instance._stats.steps} potions={Instance._stats.potions} chalice={Instance._stats.chalice} springs={Instance._stats.springs}");
        sb.Append(Instance._stats.floorLog);
        foreach (var s in Instance._summaries) sb.AppendLine(s);
        return sb.ToString();
    }

    public static void Stop()
    {
        BattleManager.AutoAdvanceMessages = false;
        Time.timeScale = 1f;
        if (Instance != null) Destroy(Instance.gameObject);
        Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) { BattleManager.AutoAdvanceMessages = false; Time.timeScale = 1f; }
    }

    private void EndRun(string result)
    {
        var hero = FindHero();
        var uses = new StringBuilder();
        foreach (var kv in _stats.skillUses) uses.Append($"{kv.Key} {kv.Value}, ");
        string line = $"[AutoPilot] RUN {_run} [{build}{(startWithSword ? "+sword" : "")}] END: {result} | 최고 B{_stats.maxFloor} | Lv{(hero != null ? hero.level : 0)} | 전투 {_stats.battles} (고블린 {_stats.goblinBattles}) | 도망 {_stats.fleeOk}/{_stats.fleeTries} | 걸음 {_stats.steps} | 물약 {_stats.potions} 잔 {_stats.chalice} 샘 {_stats.springs} | {Time.realtimeSinceStartup - _stats.startTime:0}s\n"
                      + $"   배운 순서: {string.Join(", ", _stats.learned)}\n   스킬 사용: {uses}\n{_stats.floorLog}";
        _summaries.Add(line);
        Debug.Log(line);
        AppendResult(line);
        _run++;
        _stats = new RunStats { startTime = Time.realtimeSinceStartup };
        _lastFloorSeen = -1; // _deathLogged 는 던전(1층)으로 돌아왔을 때 풀린다 — 같은 전투에서 두 번 세지 않게
        if (result.StartsWith("성공")) _run = runsTotal + 1; // 성공한 판은 새로 시작할 방법이 없어 여기서 끝냄
        if (_run > runsTotal)
        {
            _finished = true;
            Debug.Log("[AutoPilot] 모든 판 종료\n" + string.Join("\n", _summaries));
            AppendResult("[AutoPilot] DONE");
            BattleManager.AutoAdvanceMessages = false;
            Time.timeScale = 1f;
            enabled = false;
            UnityEditor.EditorApplication.isPlaying = false; // 끝나면 Play 를 끈다
        }
    }

    // ─────────────────────────────────────────
    // 매 프레임
    // ─────────────────────────────────────────

    private void Update()
    {
        if (_finished) return;
        if (Time.timeScale > 0.5f && !Mathf.Approximately(Time.timeScale, speed)) Time.timeScale = speed; // 히트스톱 등 이후 복구
        if (Time.unscaledTime < _nextAct) return;
        _nextAct = Time.unscaledTime + stepInterval / Mathf.Max(1f, speed);

        if (EncounterTransition.IsPlaying) return;

        var bm = FindFirstObjectByType<BattleManager>();
        if (bm != null) { TickBattle(bm); return; }

        var map = FindFirstObjectByType<MapManager>();
        if (map != null && map.FloorData != null) TickDungeon(map);
    }

    // ─────────────────────────────────────────
    // 전투
    // ─────────────────────────────────────────

    private static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

    private void TickBattle(BattleManager bm)
    {
        // 게임 오버 창 → Start Over (새 판)
        var gameOver = FindFirstObjectByType<GameOverScreen>();
        if (gameOver != null)
        {
            foreach (var b in gameOver.GetComponentsInChildren<Button>(true))
                if (b.name == "RestartButton" && b.interactable) { b.onClick.Invoke(); break; }
            return;
        }

        if (!_inBattle)
        {
            _inBattle = true; _stats.battles++;
            var es = Get<List<EnemyStats>>(bm, "activeEnemies");
            if (es != null && es.Exists(e => e != null && e.enemyName == "Goblin")) _stats.goblinBattles++;
        }

        var hero = FindHero();
        if (hero != null && hero.currentHP <= 0 && !_deathLogged)
        {
            _deathLogged = true;
            _stats.floorLog.AppendLine($"   사망: B{DungeonPersistentData.currentFloor} 전투 중 (Lv{hero.level})");
            EndRun($"사망 B{DungeonPersistentData.currentFloor}");
            return;
        }

        // 동료 영입 창 → 거절
        if (bm.customRecruitPanel != null && bm.customRecruitPanel.activeInHierarchy && bm.customRecruitNoButton != null)
        { bm.customRecruitNoButton.onClick.Invoke(); return; }
        var no = GameObject.Find("Btn_NO");
        if (no != null && no.activeInHierarchy) { no.GetComponent<Button>()?.onClick.Invoke(); return; }

        if (bm.battleEnded || !bm.playerTurn) return;
        if (Get<bool>(bm, "turnInProgress") || Get<bool>(bm, "waitingForTargetSelection")) return;
        if (Get<object>(bm, "currentPhase")?.ToString() != "Command") return;

        var actor = Get<PlayerStats>(bm, "currentControlledMember");
        var enemies = Get<List<EnemyStats>>(bm, "activeEnemies");
        if (actor == null || enemies == null) return;

        MethodInfo queue = typeof(BattleManager).GetMethod("QueueAllyCommand", NP);
        if (queue == null) return;
        LearnFromBuild(actor);

        var alive = enemies.FindAll(e => e != null && e.currentHP > 0);
        if (alive.Count == 0) return;
        float hpR = actor.maxHP > 0 ? (float)actor.currentHP / actor.maxHP : 1f;

        // HP 35% 미만이면 물약 (전투에서 쓸 수 있는 것), 없으면 숨 고르기
        if (hpR < 0.35f)
        {
            ConsumableItemSO potion = FindHealItem(battle: true, allowChalice: true);
            if (potion != null)
            {
                if (potion.isDawnChalice) _stats.chalice++; else _stats.potions++;
                queue.Invoke(bm, new object[] { actor, "item", null, null, 0, true, potion });
                return;
            }
            if (TrySkill(bm, queue, actor, "Second Wind", null)) return;
        }

        // 질 것 같으면 도망 (DQ식 확률 — 실패하면 적만 행동)
        if (ShouldFlee(actor, alive))
        {
            _stats.fleeTries++;
            bm.OnRunButton();
            if (bm.battleEnded) _stats.fleeOk++;
            return;
        }

        EnemyStats target = PickTarget(actor, alive);
        int front = alive.FindAll(e => (int)e.currentSlot <= 4 || e.currentSlot == BattleSlot.Center).Count;
        bool tough = target.currentHP > ExpectedHit(actor, target);

        // 상황별 스킬 (배운·장착한 것만, 무기·횟수·HP 조건은 TrySkill 이 확인)
        if (alive.Count >= 3 && hpR > 0.5f && TrySkill(bm, queue, actor, "Intimidating Shout", null)) return;
        if (alive.Count >= 2 && hpR > 0.5f && TrySkill(bm, queue, actor, "Counter Guard", null)) return;
        if (alive.Count >= 2 && hpR < 0.6f && TrySkill(bm, queue, actor, "Brace", null)) return;
        if (front >= 2 && hpR > 0.3f && TrySkill(bm, queue, actor, "Mandritto", target)) return;
        if (tough && hpR >= 0.5f && TrySkill(bm, queue, actor, "Strong Slash", target)) return;
        if (tough && hpR >= 0.3f && TrySkill(bm, queue, actor, "Slash", target)) return;

        queue.Invoke(bm, new object[] { actor, "attack", target, null, 0, false, null });
    }

    // ─────────────────────────────────────────
    // 빌드 (스킬 조합) — 2026-10-06
    // ─────────────────────────────────────────
    public string build = "none";        // none / sword / arts / mixed
    public bool startWithSword = false;   // 매 판 시작 시 조잡한 검 장착

    private static readonly Dictionary<string, string[]> BuildOrders = new Dictionary<string, string[]>
    {
        { "sword", new[] { "Basic Swordsmanship", "Slash", "Strong Slash", "Mandritto", "Sharp Edge" } },
        { "arts",  new[] { "Hardened Body", "Second Wind", "Brace", "Tactical Awareness", "Intimidating Shout", "Counter Guard", "Combat Breathing" } },
        { "mixed", new[] { "Hardened Body", "Basic Swordsmanship", "Second Wind", "Slash", "Brace", "Strong Slash", "Tactical Awareness", "Mandritto", "Intimidating Shout", "Counter Guard" } },
    };

    private static Dictionary<string, SkillData> _skillByName;
    private static SkillData SkillNamed(string name)
    {
        if (_skillByName == null)
        {
            _skillByName = new Dictionary<string, SkillData>();
            foreach (var dir in new[] { "Assets/Scripts/Battle/Data/Skills/Sword_Lore", "Assets/Scripts/Battle/Data/Skills/Combat Arts" })
                foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:SkillData", new[] { dir }))
                {
                    var s = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                    if (s != null && !_skillByName.ContainsKey(s.skillName)) _skillByName[s.skillName] = s;
                }
        }
        SkillData r; return _skillByName.TryGetValue(name, out r) ? r : null;
    }

    /// <summary>SP 가 있으면 빌드 순서대로 배우고 빈 칸에 장착 (스킬 트리에서 배우는 것과 같은 결과).</summary>
    private void LearnFromBuild(PlayerStats hero)
    {
        string[] order;
        if (hero == null || hero.statData == null || !BuildOrders.TryGetValue(build, out order)) return;
        while (hero.skillPoints > 0)
        {
            SkillData next = null;
            foreach (var n in order)
            {
                var s = SkillNamed(n);
                if (s != null && !hero.statData.learnedSkills.Contains(s)) { next = s; break; }
            }
            if (next == null) return;
            hero.statData.learnedSkills.Add(next);
            hero.statData.AutoEquipIfFree(next);
            hero.skillPoints = hero.skillPoints - 1;
            _stats.learned.Add(next.skillName + "@Lv" + hero.level);
        }
    }

    private bool TrySkill(BattleManager bm, MethodInfo queue, PlayerStats actor, string name, EnemyStats target)
    {
        SkillData skill = null;
        foreach (var s in actor.statData.equippedSkills) if (s != null && s.skillName == name) { skill = s; break; }
        if (skill == null || skill.IsPassive) return false;
        var limit = typeof(BattleManager).GetMethod("SkillLimitReason", NP);
        if (limit != null && limit.Invoke(bm, new object[] { actor, skill }) != null) return false;
        if (skill.weaponCategory != WeaponCategory.None)
        {
            var rh = actor.statData.rightHand;
            if (rh == null || rh.weaponCategory != skill.weaponCategory) return false;
        }
        int hpCost = skill.hpCostPercent > 0 ? Mathf.Max(1, Mathf.RoundToInt(actor.maxHP * skill.hpCostPercent / 100f)) : 0;
        if (actor.currentHP <= hpCost + 1) return false;
        bool self = skill.targeting != null && skill.targeting.targetFaction == TargetFaction.Self;
        queue.Invoke(bm, new object[] { actor, "skill", self ? null : target, skill, 0, false, null });
        int c; _stats.skillUses.TryGetValue(name, out c); _stats.skillUses[name] = c + 1;
        return true;
    }

    private static float ExpectedHit(PlayerStats hero, EnemyStats e)
    {
        return Mathf.Max(1f, (hero.Attack * 2f - e.defense) / 2f) * 0.85f;
    }

    private static EnemyStats PickTarget(PlayerStats hero, List<EnemyStats> alive)
    {
        EnemyStats best = null; float bestScore = float.MinValue;
        foreach (var e in alive)
        {
            float score = (e.currentHP <= ExpectedHit(hero, e) ? 1000f : 0f) - e.currentHP + ((int)e.currentSlot <= 2 || e.currentSlot == BattleSlot.Center ? 20f : 0f);
            if (score > bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    /// <summary>남은 적을 다 잡는 데 걸리는 턴 × 적의 턴당 예상 피해 > 남은 HP(+회복 여유) 의 90% 면 도망.</summary>
    private static bool ShouldFlee(PlayerStats hero, List<EnemyStats> alive)
    {
        float turns = 0f, incoming = 0f;
        foreach (var e in alive)
        {
            turns += e.currentHP / Mathf.Max(1f, ExpectedHit(hero, e) * 0.85f);
            incoming += Mathf.Max(1f, (e.attack * 1.25f * 2f - hero.Defense) / 2f) * 0.85f;
        }
        bool heals = FindHealItem(battle: true, allowChalice: true) != null;
        float budget = hero.currentHP + (heals ? hero.maxHP * 0.3f : 0f);
        return incoming * turns > budget * 0.9f;
    }

    // ─────────────────────────────────────────
    // 던전
    // ─────────────────────────────────────────

    private void TickDungeon(MapManager map)
    {
        _inBattle = false;
        int floor = DungeonPersistentData.currentFloor;
        var hero = FindHero();
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player == null || hero == null) return;

        // 죽어서 1층부터 다시 시작한 경우 (전투 밖에서 죽음 — 함정 등)
        if (_lastFloorSeen > 1 && floor == 1 && hero.level == 1 && !_deathLogged)
        {
            _stats.floorLog.AppendLine("   사망: 던전에서 (함정 등)");
            EndRun("사망 (던전)");
            return;
        }

        if (floor != _lastFloorSeen)
        {
            if (floor > _stats.maxFloor || _lastFloorSeen < 0)
            {
                string arrive = $"   B{floor} 도착: Lv{hero.level} HP {hero.currentHP}/{hero.maxHP} ATK {hero.Attack} DEF {hero.Defense} AGI {hero.Agility} (전투 {_stats.battles}, 걸음 {_stats.steps})";
                _stats.floorLog.AppendLine(arrive);
                AppendResult($"[RUN {_run}]" + arrive);
            }
            _stats.maxFloor = Mathf.Max(_stats.maxFloor, floor);
            _lastFloorSeen = floor;
            _stepsThisFloor = 0;
            _deathLogged = false;
            if (floor >= stopAtFloor) { EndRun($"성공 — B{stopAtFloor - 1} 클리어"); return; }
        }

        var hud = DungeonHud.Instance;
        if (hud != null && hud.IsLevelUpOpen)
        {
            int idx = (_freeStatToggle++ % 2 == 0) ? 0 : 1; // STR / DEF 번갈아
            typeof(DungeonHud).GetMethod("OnLevelUpStatChosen", NP)?.Invoke(hud, new object[] { idx });
            return;
        }
        if (hud != null && hud.IsDialogOpen)
        {
            bool yes = true;
            if (player.gridPos == map.FloorData.stairsPos) yes = WantDescend(map);
            else if (map.FloorData.GetCell(player.gridPos).feature == FloorFeature.Spring)
            {
                yes = hero.currentHP < hero.maxHP * 0.7f;
                if (yes) _stats.springs++;
            }
            typeof(DungeonHud).GetMethod("CloseDialog", NP)?.Invoke(hud, new object[] { yes });
            return;
        }
        if (player.IsInputLocked) return;
        if (DungeonPanelGroup.Instance != null && DungeonPanelGroup.Instance.AnyOpen) DungeonPanelGroup.Instance.CloseAll();

        // 빌드: SP 가 생기면 배움 / 옵션: 매 판 조잡한 검
        LearnFromBuild(hero);
        if (startWithSword && hero.statData != null && hero.statData.rightHand == null)
        {
            var sword = Resources.Load<EquipmentData>("Item_Equipments/Equipments/Crude/CrudeSword");
            var em = hero.GetComponent<EquipmentManager>();
            if (sword != null) { if (em != null) em.EquipItem(sword); else hero.statData.rightHand = sword; }
        }

        // 던전 회복
        if (hero.currentHP < hero.maxHP * 0.5f)
        {
            ConsumableItemSO item = FindHealItem(battle: false, allowChalice: hero.currentHP < hero.maxHP * 0.35f);
            if (item != null && ConsumableInventory.Instance.UseItem(item))
            {
                ConsumableEffectApplier.ApplyEffects(hero, item);
                if (item.isDawnChalice) _stats.chalice++; else _stats.potions++;
                return;
            }
        }

        Vector2Int? next = NextStep(map, player.gridPos, hero);
        if (next == null) return;
        Vector2Int d = next.Value - player.gridPos;
        if (d == new Vector2Int(0, -1)) player.MoveNorth();
        else if (d == new Vector2Int(1, 0)) player.MoveEast();
        else if (d == new Vector2Int(0, 1)) player.MoveSouth();
        else if (d == new Vector2Int(-1, 0)) player.MoveWest();
        _stats.steps++;
        _stepsThisFloor++;
    }

    private bool WantDescend(MapManager map)
    {
        return !HasUnexplored(map) || _stepsThisFloor >= maxStepsPerFloor;
    }

    private static bool HasUnexplored(MapManager map)
    {
        var data = map.FloorData;
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                var p = new Vector2Int(x, y);
                if (data.IsWalkable(p) && !map.FloorState.revealed.Contains(p)) return true;
            }
        return false;
    }

    /// <summary>다음 한 걸음: 탐험이 남았으면 가장 가까운 못 본 칸, 아니면(또는 걸음 초과) 계단. 체력이 낮으면 안 쓴 샘 우선.</summary>
    private Vector2Int? NextStep(MapManager map, Vector2Int from, PlayerStats hero)
    {
        var data = map.FloorData;
        var state = map.FloorState;
        bool wantSpring = hero.currentHP < hero.maxHP * 0.7f;

        System.Func<Vector2Int, bool> isGoal;
        if (wantSpring && HasReachableSpring(map))
            isGoal = p => data.GetCell(p).feature == FloorFeature.Spring && !state.usedSprings.Contains(p) && state.revealed.Contains(p);
        else if (_stepsThisFloor < maxStepsPerFloor && HasUnexplored(map))
            isGoal = p => !state.revealed.Contains(p);
        else
            isGoal = p => p == data.stairsPos;

        Vector2Int? step = Bfs(map, from, isGoal, wantSpring, avoidTraps: true);
        if (step == null) step = Bfs(map, from, isGoal, wantSpring, avoidTraps: false); // 아는 함정이 길을 막으면 밟고 지나감
        if (step == null && !(_stepsThisFloor >= maxStepsPerFloor)) _stepsThisFloor = maxStepsPerFloor; // 갈 곳이 없으면 계단으로
        return step;
    }

    /// <summary>너비 우선 탐색 (실제 지형 기준). 피할 칸: 아는 함정, 올라가는 계단, 마을 입구, (원하지 않는) 샘·계단.</summary>
    private static Vector2Int? Bfs(MapManager map, Vector2Int from, System.Func<Vector2Int, bool> isGoal, bool wantSpring, bool avoidTraps)
    {
        var data = map.FloorData;
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);
        prev[from] = from;
        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c != from && isGoal(c))
            {
                var step = c;
                while (prev[step] != from) step = prev[step];
                return step;
            }
            foreach (var dv in dirs)
            {
                var n = c + dv;
                if (prev.ContainsKey(n) || !data.IsWalkable(n)) continue;
                if (!isGoal(n) && Avoid(map, n, wantSpring, avoidTraps)) continue;
                prev[n] = c;
                q.Enqueue(n);
            }
        }
        return null;
    }

    private bool HasReachableSpring(MapManager map)
    {
        foreach (var s in map.FloorData.springs)
            if (!map.FloorState.usedSprings.Contains(s) && map.FloorState.revealed.Contains(s)) return true;
        return false;
    }

    private static bool Avoid(MapManager map, Vector2Int p, bool wantSpring, bool avoidTraps)
    {
        var data = map.FloorData;
        if (avoidTraps && map.FloorState.knownTraps.Contains(p)) return true;
        if (data.hasStairsUp && p == data.stairsUpPos) return true;
        if (data.hasTownGate && p == data.townGatePos) return true;
        if (p == data.stairsPos) return true; // 목표일 때만 들어감
        var f = data.GetCell(p).feature;
        if (f == FloorFeature.Spring && !wantSpring) return true;
        return false;
    }

    // ─────────────────────────────────────────
    // 도우미
    // ─────────────────────────────────────────

    private static PlayerStats FindHero()
    {
        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            if (p != null && p.companionSource == null && p.playerName != null && p.playerName.ToLower() == "hero") return p;
        return FindFirstObjectByType<PlayerStats>();
    }

    private static ConsumableItemSO FindHealItem(bool battle, bool allowChalice)
    {
        var inv = ConsumableInventory.Instance;
        if (inv == null) return null;
        ConsumableItemSO best = null;
        foreach (var s in inv.slots)
        {
            if (s?.item == null || s.quantity <= 0 || s.item.hpRecoveryPercent <= 0f) continue;
            if (battle ? !s.item.usableInBattle : !s.item.usableOnMap) continue;
            if (best == null || s.item.hpRecoveryPercent > best.hpRecoveryPercent) best = s.item;
        }
        // 새벽의 잔: 씬의 인벤토리 칸이 비어 있어도 게임(InventoryUIManager)처럼 Resources 에서 찾는다
        ConsumableItemSO chalice = inv.dawnChaliceItem != null ? inv.dawnChaliceItem
            : Resources.Load<ConsumableItemSO>("Item_Equipments/Items/Dawn_Chalice");
        if (best == null && allowChalice && chalice != null && inv.dawnChaliceCharges > 0
            && (battle ? chalice.usableInBattle : chalice.usableOnMap))
            best = chalice;
        return best;
    }

    private static T Get<T>(object o, string field)
    {
        FieldInfo f = o.GetType().GetField(field, NP | BindingFlags.Public);
        if (f == null) return default(T);
        object v = f.GetValue(o);
        return v is T t ? t : default(T);
    }
}
#endif
