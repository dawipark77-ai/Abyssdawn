using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬레이터 (Phase 1).
    /// BattleSimulator(헤드리스 1전투 엔진)를 재사용해 N회 던전 진행을 반복하고
    /// 한 층당 한 행 CSV + 요약 텍스트를 출력합니다.
    /// </summary>
    [AddComponentMenu("Abyssdawn/Dungeon Simulation (Phase 1)")]
    public class DungeonSimulator : MonoBehaviour
    {
        [Header("입력 SO")]
        [Tooltip("던전 정책 (층수·step·encounter·EXP·potion)")]
        [SerializeField] private DungeonSimSettings settings;

        [Tooltip("아군 편성 — 기존 Battle Sim Ally Roster 재사용")]
        [SerializeField] private BattleSimAllyRoster allyRoster;

        [Tooltip("층별 몬스터 풀 (수동 지정)")]
        [SerializeField] private DungeonSimMonsterPool monsterPool;

        [Header("실행")]
        [Tooltip("BattleSimulator 컴포넌트 — 1전투 실행 엔진. 비우면 같은 GameObject에서 찾습니다.")]
        [SerializeField] private BattleSimulator battleSimulator;

        public bool IsReadyToRun()
        {
            return settings != null && allyRoster != null && monsterPool != null && ResolveBattleSimulator() != null;
        }

        private BattleSimulator ResolveBattleSimulator()
        {
            if (battleSimulator != null) return battleSimulator;
            var found = GetComponent<BattleSimulator>();
            if (found != null) battleSimulator = found;
            return battleSimulator;
        }

        /// <summary>에디터 버튼에서 호출됩니다.</summary>
        public void RunDungeonSimulation()
        {
            if (settings == null) { Debug.LogError("[DungeonSim] Settings 미할당"); return; }
            if (allyRoster == null) { Debug.LogError("[DungeonSim] AllyRoster 미할당"); return; }
            if (monsterPool == null) { Debug.LogError("[DungeonSim] MonsterPool 미할당"); return; }
            var bm = ResolveBattleSimulator();
            if (bm == null) { Debug.LogError("[DungeonSim] BattleSimulator 미할당 — 같은 GameObject에 BattleSimulator를 추가하거나 필드를 채우세요."); return; }

            int iterations = Mathf.Max(1, settings.iterations);
            int baseSeed = settings.baseSeed;
            var allRecords = new List<DungeonSimRecord>(iterations * settings.floorCount);

            int totalDeaths = 0;
            int totalClearedAllFloors = 0;
            long totalBattles = 0;
            long totalPotions = 0;
            int[] floorReachedCount = new int[settings.floorCount + 1];

            for (int i = 0; i < iterations; i++)
            {
                int seed = baseSeed + i;
                var rng = new System.Random(seed);
                var player = BuildPlayerFromRoster(allyRoster, settings);
                bool died = false;
                int floorsCleared = 0;

                for (int floor = 1; floor <= settings.floorCount; floor++)
                {
                    if (!player.AnyAlive())
                    {
                        died = true;
                        break;
                    }

                    var rec = RunSingleFloor(i + 1, seed, floor, player, rng, bm);
                    allRecords.Add(rec);
                    totalBattles += rec.Battles;
                    totalPotions += rec.PotionUseCount;

                    if (rec.DeathFlag)
                    {
                        died = true;
                        break;
                    }
                    floorsCleared++;
                }

                floorReachedCount[Mathf.Clamp(floorsCleared, 0, settings.floorCount)]++;
                if (died) totalDeaths++;
                if (floorsCleared >= settings.floorCount && !died) totalClearedAllFloors++;
            }

            string csvPath = ResolveAbs(settings.csvRelativePath, true);
            string summaryPath = ResolveAbs(settings.summaryRelativePath, false);

            try
            {
                WriteCsv(csvPath, allRecords);
                Debug.Log($"[DungeonSim] CSV written: {csvPath}");
            }
            catch (Exception e) { Debug.LogWarning($"[DungeonSim] CSV write failed: {e.Message}"); }

            string summary = BuildSummary(iterations, totalDeaths, totalClearedAllFloors, totalBattles, totalPotions, floorReachedCount);
            Debug.Log(summary);
            try
            {
                File.WriteAllText(summaryPath, summary, Encoding.UTF8);
                Debug.Log($"[DungeonSim] Summary written: {summaryPath}");
            }
            catch (Exception e) { Debug.LogWarning($"[DungeonSim] Summary write failed: {e.Message}"); }
        }

        // ---------------------------------------------------------------
        // 한 층 실행
        // ---------------------------------------------------------------
        private DungeonSimRecord RunSingleFloor(int runId, int seed, int floor, DungeonSimPlayer player, System.Random rng, BattleSimulator bm)
        {
            var rec = new DungeonSimRecord
            {
                RunId = runId,
                Seed = seed,
                Floor = floor,
                FloorType = "NoPool",
                LevelBefore = player.Level,
                HpBefore = player.GetTotalCurrentHP(),
                MpBefore = player.GetTotalCurrentMP()
            };

            var entry = monsterPool.GetEntryForFloor(floor);
            if (entry == null)
            {
                rec.Notes = "no monster pool entry for this floor";
                rec.LevelAfter = player.Level;
                rec.HpAfter = player.GetTotalCurrentHP();
                rec.MpAfter = player.GetTotalCurrentMP();
                rec.ClearFlag = true;
                rec.NextFloorFlag = true;
                return rec;
            }

            rec.FloorType = entry.kind.ToString();

            int steps = rng.Next(settings.stepsPerFloorMin, settings.stepsPerFloorMax + 1);
            int cooldown = 0;
            int battles = 0;
            int encounters = 0;
            int xpGained = 0;
            int goldGained = 0;
            int potionUseCount = 0;
            int skillUseCount = 0;
            int recoverySkillUseCount = 0;
            int dmgDealt = 0;
            int dmgTaken = 0;
            int turnsSum = 0;
            int levelUpsThisFloor = 0;

            int stepsTakenActual = 0;
            for (int s = 0; s < steps; s++)
            {
                stepsTakenActual++;

                if (cooldown > 0)
                {
                    cooldown--;
                    continue;
                }

                bool forceEncounter = entry.kind == DungeonSimMonsterPool.FloorKind.Boss && encounters == 0;
                bool encounterRoll = rng.NextDouble() < settings.encounterChance;
                if (!forceEncounter && !encounterRoll) continue;

                if (battles >= settings.maxBattlesPerFloor)
                {
                    rec.Notes = "maxBattlesPerFloor reached";
                    break;
                }

                encounters++;
                potionUseCount += MaybeAutoUseHpPotion(player);

                var enemyParty = monsterPool.BuildEnemyParty(entry, rng);
                if (enemyParty.Count == 0) continue;
                var enemyUnits = BuildEnemyUnitsFromMonsterSOs(enemyParty);
                if (enemyUnits.Count == 0) continue;

                battles++;
                var outcome = bm.RunOneBattleForDungeon(player.Units, enemyUnits, rng);
                turnsSum += outcome.Turns;
                dmgDealt += outcome.TotalDamageDealtToEnemies;
                dmgTaken += outcome.TotalDamageTakenByAllies;
                skillUseCount += outcome.SkillUseCount;
                recoverySkillUseCount += outcome.RecoverySkillUseCount;

                if (outcome.AllyWin)
                {
                    int xpThisBattle = 0;
                    int goldThisBattle = 0;
                    foreach (var m in enemyParty)
                    {
                        if (m == null) continue;
                        xpThisBattle += m.ExpReward;
                        goldThisBattle += m.GoldReward;
                    }
                    xpGained += xpThisBattle;
                    goldGained += goldThisBattle;
                    player.Exp += xpThisBattle;
                    player.Gold += goldThisBattle;

                    while (player.Exp >= settings.GetExpToNextLevel(player.Level))
                    {
                        player.Exp -= settings.GetExpToNextLevel(player.Level);
                        ApplyLevelUp(player);
                        levelUpsThisFloor++;
                    }

                    cooldown = settings.postBattleCooldownSteps;
                }
                else
                {
                    rec.Notes = string.IsNullOrEmpty(rec.Notes) ? "party wiped" : rec.Notes + "; party wiped";
                    break;
                }
            }

            rec.StepsMoved = stepsTakenActual;
            rec.Encounters = encounters;
            rec.Battles = battles;
            rec.PotionUseCount = potionUseCount;
            rec.SkillUseCount = skillUseCount;
            rec.RecoverySkillUseCount = recoverySkillUseCount;
            rec.TotalDamageDealt = dmgDealt;
            rec.TotalDamageTaken = dmgTaken;
            rec.TotalBattleTurns = turnsSum;
            rec.XpGained = xpGained;
            rec.GoldGained = goldGained;
            rec.LevelAfter = player.Level;
            rec.HpAfter = player.GetTotalCurrentHP();
            rec.MpAfter = player.GetTotalCurrentMP();
            rec.DeathFlag = !player.AnyAlive();
            rec.ClearFlag = !rec.DeathFlag;
            rec.NextFloorFlag = rec.ClearFlag;
            if (levelUpsThisFloor > 0)
                rec.Notes = string.IsNullOrEmpty(rec.Notes) ? $"+{levelUpsThisFloor} levelup" : rec.Notes + $"; +{levelUpsThisFloor} levelup";

            return rec;
        }

        /// <summary>
        /// Phase 1 단순 레벨업 — Settings의 평균 성장치를 모든 살아있는 유닛에 적용.
        /// PlayerStats.LevelUp의 직업 룰렛/종의기억 룰렛은 Phase 2에서 이식할 예정.
        /// </summary>
        private void ApplyLevelUp(DungeonSimPlayer player)
        {
            player.Level++;
            player.LevelUpsTotal++;
            foreach (var u in player.Units)
            {
                if (u == null) continue;
                u.MaxHP += settings.levelUpHpGain;
                u.MaxMP += settings.levelUpMpGain;
                u.Attack += settings.levelUpAtkGain;
                u.Defense += settings.levelUpDefGain;
                u.Magic += settings.levelUpMagGain;
                u.Agility += settings.levelUpAgiGain;
                u.Luck += settings.levelUpLukGain;
                u.CurrentHP = u.MaxHP;
                u.CurrentMP = u.MaxMP;
            }
        }

        // ---------------------------------------------------------------
        // 포션 자동 사용 (HP 임계 미달 시 살아있는 유닛 1명에게 사용)
        // ---------------------------------------------------------------
        private int MaybeAutoUseHpPotion(DungeonSimPlayer player)
        {
            int used = 0;
            if (player.HpPotionCount <= 0) return 0;

            foreach (var u in player.Units)
            {
                if (!u.IsAlive) continue;
                if (u.MaxHP <= 0) continue;
                float ratio = (float)u.CurrentHP / Mathf.Max(1, u.MaxHP);
                if (ratio <= settings.potionUseHpThreshold && player.HpPotionCount > 0)
                {
                    u.CurrentHP = Mathf.Min(u.MaxHP, u.CurrentHP + settings.hpPotionHealAmount);
                    player.HpPotionCount--;
                    player.HpPotionsUsedTotal++;
                    used++;
                }
            }
            return used;
        }

        // ---------------------------------------------------------------
        // 플레이어 빌드
        // ---------------------------------------------------------------
        private static DungeonSimPlayer BuildPlayerFromRoster(BattleSimAllyRoster roster, DungeonSimSettings st)
        {
            var p = new DungeonSimPlayer
            {
                Name = "Party",
                Level = Mathf.Max(1, st.startingLevel),
                Exp = Mathf.Max(0, st.startingExp),
                Gold = 0,
                HpPotionCount = Mathf.Max(0, st.startingHpPotionCount)
            };

            var ordered = roster.GetOrderedAllies();
            for (int i = 0; i < ordered.Length; i++)
            {
                var so = ordered[i];
                if (so == null) continue;
                int slotNum = i + 1;
                var slot = (BattleSlot)slotNum;
                p.Units.Add(new BattleSimUnit
                {
                    Team = BattleSimTeam.Ally,
                    DisplayName = so.allyDisplayName,
                    Slot = slot,
                    MaxHP = so.maxHP,
                    CurrentHP = so.maxHP,
                    MaxMP = so.maxMP,
                    CurrentMP = so.maxMP,
                    Attack = so.attack,
                    Defense = so.defense,
                    Magic = so.magic,
                    Agility = so.agility,
                    Luck = so.luck,
                    SimAiPattern = so.simAiPattern,
                    SimSkills = new List<SkillData>(so.simSkills ?? new List<SkillData>())
                });
            }
            return p;
        }

        // ---------------------------------------------------------------
        // 적 유닛 빌드 (MonsterSO → BattleSimUnit). 슬롯은 1번부터 순서대로 배정.
        // ---------------------------------------------------------------
        private static List<BattleSimUnit> BuildEnemyUnitsFromMonsterSOs(List<MonsterSO> party)
        {
            var list = new List<BattleSimUnit>();
            for (int i = 0; i < party.Count && i < 4; i++)
            {
                var m = party[i];
                if (m == null) continue;
                int slotNum = i + 1;
                var slot = (BattleSlot)slotNum;
                var unit = new BattleSimUnit
                {
                    Team = BattleSimTeam.Enemy,
                    DisplayName = m.MonsterName,
                    Slot = slot,
                    MaxHP = m.HP,
                    CurrentHP = m.HP,
                    MaxMP = m.MP,
                    CurrentMP = m.MP,
                    Attack = m.ATK,
                    Defense = m.DEF,
                    Magic = m.MAG,
                    Agility = m.AGI,
                    Luck = m.LUK,
                    SimAiPattern = m.AIPattern,
                    SimSkills = new List<SkillData>()
                };
                if (m.ActiveSkills != null)
                {
                    foreach (var s in m.ActiveSkills)
                        if (s != null) unit.SimSkills.Add(s);
                }
                list.Add(unit);
            }
            return list;
        }

        // ---------------------------------------------------------------
        // CSV / Summary 출력
        // ---------------------------------------------------------------
        private static void WriteCsv(string absPath, List<DungeonSimRecord> records)
        {
            var dir = Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine(DungeonSimRecord.CsvHeader);
            foreach (var r in records)
                sb.AppendLine(r.ToCsvLine());

            File.WriteAllText(absPath, sb.ToString(), Encoding.UTF8);
        }

        private string BuildSummary(int iterations, int deaths, int allFloorsCleared, long battles, long potions, int[] reached)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Dungeon Simulation (Phase 1) ===");
            sb.AppendLine($"리포트 생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Iterations: {iterations}, Floors: {settings.floorCount}, BaseSeed: {settings.baseSeed}");
            sb.AppendLine($"Steps/Floor: {settings.stepsPerFloorMin}~{settings.stepsPerFloorMax}, EncounterChance: {settings.encounterChance:P0}, Cooldown: {settings.postBattleCooldownSteps}");
            sb.AppendLine();
            sb.AppendLine($"전체 클리어(모든 층): {allFloorsCleared}/{iterations} ({100f * allFloorsCleared / iterations:F1}%)");
            sb.AppendLine($"사망 회차: {deaths}/{iterations} ({100f * deaths / iterations:F1}%)");
            sb.AppendLine($"평균 전투 수/회차: {(iterations > 0 ? (double)battles / iterations : 0):F2}");
            sb.AppendLine($"평균 포션 사용/회차: {(iterations > 0 ? (double)potions / iterations : 0):F2}");
            sb.AppendLine();
            sb.AppendLine("--- 도달 층 분포 (해당 회차가 마지막으로 클리어한 층 수) ---");
            for (int i = 0; i <= settings.floorCount; i++)
            {
                if (reached[i] == 0) continue;
                sb.AppendLine($"  {i}층 도달: {reached[i]} ({100f * reached[i] / iterations:F1}%)");
            }
            sb.AppendLine();
            sb.AppendLine("CSV 컬럼: run_id,seed,floor,floor_type,steps_moved,encounters,battles,hp_before,hp_after,mp_before,mp_after,xp_gained,gold_gained,level_before,level_after,potion_use_count,skill_use_count,recovery_skill_use_count,total_damage_dealt,total_damage_taken,total_battle_turns,death_flag,clear_flag,next_floor_flag,notes");
            sb.AppendLine();
            sb.AppendLine("[Phase 1 한계] 레벨업 적용 로직은 본 컴포넌트에 아직 들어있지 않습니다 — DungeonSimPlayer.Exp만 누적되고 Level은 startingLevel을 유지합니다. Phase 2에서 PlayerStats.LevelUp 모듈을 시뮬에 이식하여 baseHP/MP/스탯 성장을 반영할 예정입니다.");

            return sb.ToString();
        }

        private static string ResolveAbs(string relative, bool isCsv)
        {
            string filled = relative ?? "";
            if (filled.Contains("{TIMESTAMP}"))
                filled = filled.Replace("{TIMESTAMP}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string abs = Path.Combine(Application.dataPath, "..", filled).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(abs);
        }
    }
}
