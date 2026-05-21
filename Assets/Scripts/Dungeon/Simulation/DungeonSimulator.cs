using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        /// <summary>한 번의 RunDungeonSimulation 안에서 층별 턴-승리 기준 최소 레벨을 재계산하지 않도록 캐시합니다.</summary>
        private readonly Dictionary<int, int> _aiTurnWinMinLevelCache = new Dictionary<int, int>();

        /// <summary>전체 10층 클리어 최소 레벨 프로브 중 — 수동·풀 게이트만 사용(10층 게이트·턴 게이트 제외).</summary>
        private bool _aiGateProbeSimpleOnly;

        private const int SimDungeonMaxAllySlots = 4;
        private const int SimDungeonMaxMonsterCompanions = 3;

        private bool _floor10ClearMinCached;
        private int _floor10ClearMinLevel;

        private bool _sessionLoggingActive;
        private int _sessionBattleSeq;
        private List<DungeonSimBattleLogRow> _logBattles;
        private List<DungeonSimPartySlotLogRow> _logPartySlots;
        private List<DungeonSimRecruitLogRow> _logRecruits;

        private sealed class SimEquipHistograms
        {
            public readonly Dictionary<string, int> AiPick = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Death = new Dictionary<string, int>();
            /// <summary>동료 합류 성공 시 몬스터 표시명별 누적(전투당 최대 1회).</summary>
            public readonly Dictionary<string, int> CompanionJoinByName = new Dictionary<string, int>();
            /// <summary>전체 층 클리어(사망 없음) 회차 종료 시점의 동료 파이프 시그니처별 누적.</summary>
            public readonly Dictionary<string, int> FullClearCompanionSig = new Dictionary<string, int>();
        }

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

            _aiTurnWinMinLevelCache.Clear();
            _floor10ClearMinCached = false;
            _floor10ClearMinLevel = 0;
            DungeonSimAiEquipment.ResetExplainSamples();

            int iterations = Mathf.Max(1, settings.iterations);
            int baseSeed = settings.baseSeed;
            var allFloors = new List<DungeonSimFloorLogRow>(iterations * settings.floorCount * 2);
            var allRuns = new List<DungeonSimRunLogRow>(iterations);

            _sessionLoggingActive = true;
            _sessionBattleSeq = 0;
            _logBattles = new List<DungeonSimBattleLogRow>(iterations * settings.floorCount * 6);
            _logPartySlots = new List<DungeonSimPartySlotLogRow>(iterations * settings.floorCount * 16);
            _logRecruits = new List<DungeonSimRecruitLogRow>(512);

            int totalDeaths = 0;
            int totalClearedAllFloors = 0;
            long totalBattles = 0;
            long totalBattleWins = 0;
            long totalBattleFlees = 0;
            long totalPotions = 0;
            long totalDawnChalice = 0;
            long totalMedicinalHerbs = 0;
            int[] floorReachedCount = new int[settings.floorCount + 1];
            var equipHist = new SimEquipHistograms();

            for (int i = 0; i < iterations; i++)
            {
                int seed = baseSeed + i;
                var rng = new System.Random(seed);
                var player = BuildPlayerFromRoster(allyRoster, settings);
                bool died = false;
                int floorsCleared = 0;
                var runFloors = new List<DungeonSimFloorLogRow>(settings.floorCount * 2);

                for (int floor = 1; floor <= settings.floorCount; floor++)
                {
                    if (!player.AnyAlive())
                    {
                        died = true;
                        break;
                    }

                    var fr = RunSingleFloor(i + 1, seed, floor, player, rng, bm, equipHist, skipFloorEntryEffects: false);
                    allFloors.Add(fr);
                    runFloors.Add(fr);
                    totalBattles += fr.Battles;
                    totalBattleWins += fr.BattlesWon;
                    totalBattleFlees += fr.BattlesFled;
                    totalPotions += fr.PotionUseDelta;
                    totalDawnChalice += fr.ChaliceUseDelta;
                    totalMedicinalHerbs += fr.HerbUseDelta;

                    if (fr.DeathFlag != 0)
                    {
                        died = true;
                        break;
                    }

                    if (settings.aiProgressionEnabled && floor < settings.floorCount)
                    {
                        int needLv = GetAiNextFloorRequiredLevel(floor + 1);
                        int targetLv = needLv + Mathf.Max(0, settings.aiExtraLevelsBeyondNextFloorGate);
                        int farmPasses = 0;
                        while (player.AnyAlive()
                               && player.Level < targetLv
                               && farmPasses < settings.aiMaxFarmingPassesBeforeNextFloor)
                        {
                            var farmFr = RunSingleFloor(i + 1, seed, floor, player, rng, bm, equipHist, skipFloorEntryEffects: true);
                            allFloors.Add(farmFr);
                            runFloors.Add(farmFr);
                            totalBattles += farmFr.Battles;
                            totalBattleWins += farmFr.BattlesWon;
                            totalBattleFlees += farmFr.BattlesFled;
                            totalPotions += farmFr.PotionUseDelta;
                            totalDawnChalice += farmFr.ChaliceUseDelta;
                            totalMedicinalHerbs += farmFr.HerbUseDelta;
                            farmPasses++;
                            if (farmFr.DeathFlag != 0)
                            {
                                died = true;
                                break;
                            }
                        }
                    }

                    if (died) break;
                    floorsCleared++;
                }

                floorReachedCount[Mathf.Clamp(floorsCleared, 0, settings.floorCount)]++;
                if (died)
                {
                    totalDeaths++;
                    string sig = DungeonSimAiEquipment.GetPartyEquipSignature(player);
                    if (!string.IsNullOrEmpty(sig))
                    {
                        if (!equipHist.Death.TryGetValue(sig, out int dc))
                            dc = 0;
                        equipHist.Death[sig] = dc + 1;
                    }
                }
                if (floorsCleared >= settings.floorCount && !died)
                {
                    totalClearedAllFloors++;
                    string cSig = player.FormatCompanionJoinHistorySig();
                    if (!equipHist.FullClearCompanionSig.TryGetValue(cSig, out int fc))
                        fc = 0;
                    equipHist.FullClearCompanionSig[cSig] = fc + 1;
                }

                allRuns.Add(BuildRunLogRow(i + 1, seed, runFloors, player, died, floorsCleared, equipHist));
            }

            _sessionLoggingActive = false;

            string csvPath = ResolveAbs(settings.csvRelativePath, true);
            string summaryPath = ResolveAbs(settings.summaryRelativePath, false);

            bool wroteToAssets = false;
            try
            {
                WriteStructuredCsvs(csvPath, allRuns, allFloors, _logBattles, _logPartySlots, _logRecruits);
                Debug.Log($"[DungeonSim] CSV written (runs/floors/battles/party/recruits): {csvPath}");
                if (IsInsideAssets(settings.csvRelativePath)) wroteToAssets = true;
            }
            catch (Exception e) { Debug.LogWarning($"[DungeonSim] CSV write failed: {e.Message}"); }

            string summary = BuildSummary(iterations, totalDeaths, totalClearedAllFloors, totalBattles, totalBattleWins, totalBattleFlees, totalMedicinalHerbs, totalPotions, totalDawnChalice, floorReachedCount, allFloors, allRuns, equipHist);
            Debug.Log(summary);
            try
            {
                var dir = Path.GetDirectoryName(summaryPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(summaryPath, summary, Encoding.UTF8);
                Debug.Log($"[DungeonSim] Summary written: {summaryPath}");
                if (IsInsideAssets(settings.summaryRelativePath)) wroteToAssets = true;
            }
            catch (Exception e) { Debug.LogWarning($"[DungeonSim] Summary write failed: {e.Message}"); }

#if UNITY_EDITOR
            if (wroteToAssets)
                AssetDatabase.Refresh();
#endif
        }

        private static bool IsInsideAssets(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return false;
            string norm = relativePath.Replace('\\', '/').TrimStart('/');
            return norm.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetPartyHpPct(DungeonSimPlayer p)
        {
            if (p == null) return 1f;
            int mx = p.GetTotalMaxHP();
            if (mx <= 0) return 1f;
            return Mathf.Clamp01(p.GetTotalCurrentHP() / (float)mx);
        }

        private static float GetPartyMpPct(DungeonSimPlayer p)
        {
            if (p == null) return 1f;
            int mx = p.GetTotalMaxMP();
            if (mx <= 0) return 1f;
            return Mathf.Clamp01(p.GetTotalCurrentMP() / (float)mx);
        }

        private static int CountMonsterCompanions(DungeonSimPlayer p)
        {
            if (p?.Units == null) return 0;
            int c = 0;
            foreach (var u in p.Units)
                if (u != null && u.SimIsMonsterCompanion) c++;
            return c;
        }

        private static int CountRosterAllies(DungeonSimPlayer p)
        {
            if (p?.Units == null) return 0;
            int c = 0;
            foreach (var u in p.Units)
                if (u != null && !u.SimIsMonsterCompanion) c++;
            return c;
        }

        private static string BuildEnemyPartySig(List<MonsterSO> party)
        {
            if (party == null || party.Count == 0) return "";
            var parts = new List<string>(party.Count);
            foreach (var m in party)
            {
                if (m == null) parts.Add("?");
                else parts.Add($"{m.MonsterName}L{m.MonsterLevel}");
            }
            return string.Join("|", parts);
        }

        private static string SlotRowTag(int slotNum) => slotNum <= 2 ? "front" : "back";

        private void LogPartySlotRowsForBattle(int battleSeq, int runId, int seed, int floor, DungeonSimPlayer player)
        {
            if (!_sessionLoggingActive || _logPartySlots == null || player?.Units == null) return;
            foreach (var u in player.Units)
            {
                if (u == null) continue;
                int sn = (int)u.Slot;
                if (sn < 1 || sn > 4) continue;
                _logPartySlots.Add(new DungeonSimPartySlotLogRow
                {
                    BattleSeq = battleSeq,
                    RunId = runId,
                    Seed = seed,
                    Floor = floor,
                    Slot = sn,
                    RowTag = SlotRowTag(sn),
                    UnitKind = u.SimIsMonsterCompanion ? "monster_companion" : "roster",
                    DisplayName = u.DisplayName ?? "",
                    ClassName = u.SimCharacterClass != null ? u.SimCharacterClass.name : "",
                    PartyLevelField = player.Level,
                    MonsterLevelField = u.SimIsMonsterCompanion ? u.SimCompanionMonsterLevel : 0,
                    Atk = u.Attack,
                    Def = u.Defense,
                    Mag = u.Magic,
                    Agi = u.Agility,
                    Luk = u.Luck,
                    Hp = u.CurrentHP,
                    MaxHp = u.MaxHP,
                    Mp = u.CurrentMP,
                    MaxMp = u.MaxMP
                });
            }
        }

        private void AppendBattleLogRow(
            int battleSeq,
            int runId,
            int seed,
            int floor,
            int encounterIdx,
            List<MonsterSO> enemyParty,
            BattleSimulator.DungeonBattleResult o)
        {
            if (!_sessionLoggingActive || _logBattles == null || o == null) return;

            _logBattles.Add(new DungeonSimBattleLogRow
            {
                BattleSeq = battleSeq,
                RunId = runId,
                Seed = seed,
                Floor = floor,
                EncounterIndexInFloor = encounterIdx,
                EnemyPartySig = BuildEnemyPartySig(enemyParty),
                ResultWin = o.AllyWin ? 1 : 0,
                ResultFlee = o.AllyEscaped ? 1 : 0,
                ResultLoss = (!o.AllyWin && !o.AllyEscaped) ? 1 : 0,
                Turns = o.Turns,
                DamageDealtToEnemies = o.TotalDamageDealtToEnemies,
                DamageTakenByAllies = o.TotalDamageTakenByAllies,
                SkillActivationBreakdown = o.SkillActivationBreakdown ?? "",
                BattleHerbUses = o.BattleMedicinalHerbUses,
                BattlePotionUses = o.BattleHpPotionUses,
                BattleChaliceUses = o.BattleDawnChaliceUses,
                AllySlot1HpStart = o.AllyHpStartBySlot[1],
                AllySlot1HpEnd = o.AllyHpEndBySlot[1],
                AllySlot2HpStart = o.AllyHpStartBySlot[2],
                AllySlot2HpEnd = o.AllyHpEndBySlot[2],
                AllySlot3HpStart = o.AllyHpStartBySlot[3],
                AllySlot3HpEnd = o.AllyHpEndBySlot[3],
                AllySlot4HpStart = o.AllyHpStartBySlot[4],
                AllySlot4HpEnd = o.AllyHpEndBySlot[4],
                EnemySlot1HpStart = o.EnemyHpStartBySlot[1],
                EnemySlot1HpEnd = o.EnemyHpEndBySlot[1],
                EnemySlot2HpStart = o.EnemyHpStartBySlot[2],
                EnemySlot2HpEnd = o.EnemyHpEndBySlot[2],
                EnemySlot3HpStart = o.EnemyHpStartBySlot[3],
                EnemySlot3HpEnd = o.EnemyHpEndBySlot[3],
                EnemySlot4HpStart = o.EnemyHpStartBySlot[4],
                EnemySlot4HpEnd = o.EnemyHpEndBySlot[4],
                A1Dealt = o.AllyAttackerDamageDealt[1],
                A2Dealt = o.AllyAttackerDamageDealt[2],
                A3Dealt = o.AllyAttackerDamageDealt[3],
                A4Dealt = o.AllyAttackerDamageDealt[4],
                A1Taken = o.AllyDefenderDamageTaken[1],
                A2Taken = o.AllyDefenderDamageTaken[2],
                A3Taken = o.AllyDefenderDamageTaken[3],
                A4Taken = o.AllyDefenderDamageTaken[4],
                A1OffAtt = o.AllyAttackerOffenseAttempts[1],
                A2OffAtt = o.AllyAttackerOffenseAttempts[2],
                A3OffAtt = o.AllyAttackerOffenseAttempts[3],
                A4OffAtt = o.AllyAttackerOffenseAttempts[4],
                A1OffHit = o.AllyAttackerOffenseHits[1],
                A2OffHit = o.AllyAttackerOffenseHits[2],
                A3OffHit = o.AllyAttackerOffenseHits[3],
                A4OffHit = o.AllyAttackerOffenseHits[4],
                A1Crit = o.AllyAttackerCrits[1],
                A2Crit = o.AllyAttackerCrits[2],
                A3Crit = o.AllyAttackerCrits[3],
                A4Crit = o.AllyAttackerCrits[4],
                A1SurvTurns = o.AllySlotSurvivalTurns[1],
                A2SurvTurns = o.AllySlotSurvivalTurns[2],
                A3SurvTurns = o.AllySlotSurvivalTurns[3],
                A4SurvTurns = o.AllySlotSurvivalTurns[4],
                A1Defend = o.AllyDefendActions[1],
                A2Defend = o.AllyDefendActions[2],
                A3Defend = o.AllyDefendActions[3],
                A4Defend = o.AllyDefendActions[4],
                A1Skill = o.AllySkillCasts[1],
                A2Skill = o.AllySkillCasts[2],
                A3Skill = o.AllySkillCasts[3],
                A4Skill = o.AllySkillCasts[4]
            });
        }

        private static void WriteStructuredCsvs(
            string userCsvPath,
            List<DungeonSimRunLogRow> runs,
            List<DungeonSimFloorLogRow> floors,
            List<DungeonSimBattleLogRow> battles,
            List<DungeonSimPartySlotLogRow> party,
            List<DungeonSimRecruitLogRow> recruits)
        {
            string dir = Path.GetDirectoryName(userCsvPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string fileName = Path.GetFileNameWithoutExtension(userCsvPath);
            if (string.IsNullOrEmpty(fileName))
                fileName = "DungeonSim";
            string stem = string.IsNullOrEmpty(dir) ? fileName : Path.Combine(dir, fileName);

            void WriteFile<T>(string suffix, IEnumerable<T> rows, string header, Func<T, string> line)
            {
                var sb = new StringBuilder();
                sb.AppendLine(header);
                if (rows != null)
                {
                    foreach (var r in rows)
                        sb.AppendLine(line(r));
                }
                File.WriteAllText(stem + suffix, sb.ToString(), Encoding.UTF8);
            }

            WriteFile("_runs.csv", runs ?? new List<DungeonSimRunLogRow>(), DungeonSimRunLogRow.Header, r => r.ToLine());
            WriteFile("_floors.csv", floors ?? new List<DungeonSimFloorLogRow>(), DungeonSimFloorLogRow.Header, r => r.ToLine());
            WriteFile("_battles.csv", battles ?? new List<DungeonSimBattleLogRow>(), DungeonSimBattleLogRow.Header, r => r.ToLine());
            WriteFile("_party.csv", party ?? new List<DungeonSimPartySlotLogRow>(), DungeonSimPartySlotLogRow.Header, r => r.ToLine());
            WriteFile("_recruits.csv", recruits ?? new List<DungeonSimRecruitLogRow>(), DungeonSimRecruitLogRow.Header, r => r.ToLine());
        }

        private DungeonSimRunLogRow BuildRunLogRow(
            int runId,
            int seed,
            List<DungeonSimFloorLogRow> runFloors,
            DungeonSimPlayer player,
            bool died,
            int floorsCleared,
            SimEquipHistograms equipHist)
        {
            int targetFloors = settings != null ? settings.floorCount : 0;
            int finalFloor = 0;
            if (runFloors != null && runFloors.Count > 0)
                finalFloor = runFloors[runFloors.Count - 1].Floor;
            else
                finalFloor = Mathf.Clamp(floorsCleared, 0, targetFloors);

            int battlesTotal = 0, wins = 0, losses = 0, flees = 0;
            long xp = 0, dmgD = 0, dmgT = 0;
            int herb = 0, pot = 0, chal = 0;
            if (runFloors != null)
            {
                for (int i = 0; i < runFloors.Count; i++)
                {
                    var f = runFloors[i];
                    battlesTotal += f.Battles;
                    wins += f.BattlesWon;
                    losses += f.BattlesLost;
                    flees += f.BattlesFled;
                    xp += f.XpGained;
                    dmgD += f.DamageDealtFloor;
                    dmgT += f.DamageTakenFloor;
                    herb += f.HerbUseDelta;
                    pot += f.PotionUseDelta;
                    chal += f.ChaliceUseDelta;
                }
            }

            string deathCause = "none";
            string deathEquip = "";
            if (died && runFloors != null && runFloors.Count > 0)
            {
                var last = runFloors[runFloors.Count - 1];
                deathEquip = last.DeathAiEquipSignature ?? "";
                if (!string.IsNullOrEmpty(last.Notes) && last.Notes.IndexOf("party wiped", StringComparison.OrdinalIgnoreCase) >= 0)
                    deathCause = "battle";
                else
                    deathCause = "other";
            }

            return new DungeonSimRunLogRow
            {
                RunId = runId,
                Seed = seed,
                TargetFloors = targetFloors,
                FinalFloorReached = finalFloor,
                DeathFlag = died ? 1 : 0,
                DeathCause = deathCause,
                FinalPartyLevel = player != null ? player.Level : 0,
                FinalCompanionCumulative = player != null ? player.FormatCompanionJoinHistorySig() : "-",
                BattlesTotal = battlesTotal,
                BattlesWon = wins,
                BattlesLost = losses,
                BattlesFled = flees,
                HerbUses = herb,
                PotionUses = pot,
                ChaliceUses = chal,
                XpGainedRun = xp,
                DamageDealtRun = dmgD,
                DamageTakenRun = dmgT,
                CompanionCountEnd = CountMonsterCompanions(player),
                RosterAllyCountEnd = CountRosterAllies(player),
                DeathAiEquipSignature = deathEquip
            };
        }

        // ---------------------------------------------------------------
        // 한 층 실행
        // ---------------------------------------------------------------
        private DungeonSimFloorLogRow RunSingleFloor(int runId, int seed, int floor, DungeonSimPlayer player, System.Random rng, BattleSimulator bm, SimEquipHistograms equipHist, bool skipFloorEntryEffects)
        {
            string notes = "";

            var fr = new DungeonSimFloorLogRow
            {
                RunId = runId,
                Seed = seed,
                Floor = floor,
                FloorType = "NoPool",
                LevelOnEnter = player.Level,
                PartyHpPctEnter = GetPartyHpPct(player),
                PartyMpPctEnter = GetPartyMpPct(player),
                HerbOnEnter = player.MedicinalHerbCount,
                PotionOnEnter = player.HpPotionCount,
                ChaliceOnEnter = player.DawnChaliceCharges,
                CompanionCountEnter = CountMonsterCompanions(player),
                RosterAllyCountEnter = CountRosterAllies(player),
                TotalHpEnter = player.GetTotalCurrentHP(),
                TotalMpEnter = player.GetTotalCurrentMP()
            };

            var entry = monsterPool.GetEntryForFloor(floor);
            if (entry == null)
            {
                notes = "no monster pool entry for this floor";
                fr.Notes = notes;
                fr.LevelOnExit = player.Level;
                fr.PartyHpPctExit = GetPartyHpPct(player);
                fr.PartyMpPctExit = GetPartyMpPct(player);
                fr.HerbOnExit = player.MedicinalHerbCount;
                fr.PotionOnExit = player.HpPotionCount;
                fr.ChaliceOnExit = player.DawnChaliceCharges;
                fr.CompanionCountExit = CountMonsterCompanions(player);
                fr.RosterAllyCountExit = CountRosterAllies(player);
                fr.TotalHpExit = player.GetTotalCurrentHP();
                fr.TotalMpExit = player.GetTotalCurrentMP();
                fr.HerbUseDelta = 0;
                fr.PotionUseDelta = 0;
                fr.ChaliceUseDelta = 0;
                fr.ClearFlag = 1;
                fr.DeathFlag = 0;
                fr.Floor1TownUsesCumulative = player.Floor1TownUsesTotal;
                fr.SkillActivationBreakdown = "";
                fr.CompanionsJoinedCumulative = player.FormatCompanionJoinHistorySig();
                fr.LastBattleEnemyCount = 0;
                fr.CompanionJoinsThisFloor = 0;
                fr.SumEnemyMonstersInBattlesThisFloor = 0;
                fr.MaxEnemyMonstersSingleBattleThisFloor = 0;
                return fr;
            }

            fr.FloorType = entry.kind.ToString();

            if (skipFloorEntryEffects)
                notes = string.IsNullOrEmpty(notes) ? "ai_farm" : notes + "; ai_farm";

            int fMin = Mathf.Min(settings.medicinalHerbGrantFloorsMin, settings.medicinalHerbGrantFloorsMax);
            int fMax = Mathf.Max(settings.medicinalHerbGrantFloorsMin, settings.medicinalHerbGrantFloorsMax);
            if (!skipFloorEntryEffects)
            {
                if (floor == 1 && settings.floor1TownFullRestoreEnabled)
                {
                    ApplyFloor1TownFullRestore(player, settings, rng, monsterPool, floor, equipHist?.AiPick);
                    fr.PartyHpPctEnter = GetPartyHpPct(player);
                    fr.PartyMpPctEnter = GetPartyMpPct(player);
                    fr.TotalHpEnter = player.GetTotalCurrentHP();
                    fr.TotalMpEnter = player.GetTotalCurrentMP();
                    notes = string.IsNullOrEmpty(notes) ? "floor1_town" : notes + "; floor1_town";
                }
                if (floor >= fMin && floor <= fMax)
                    player.MedicinalHerbCount += Mathf.Max(0, settings.RollMedicinalHerbGrantCount(rng));
            }

            int steps = rng.Next(settings.stepsPerFloorMin, settings.stepsPerFloorMax + 1);
            int cooldown = 0;
            int battles = 0;
            int battleWins = 0;
            int battlesLostThisFloor = 0;
            int encounters = 0;
            int xpGained = 0;
            int goldGained = 0;
            int potionUseCount = 0;
            int dawnChaliceUseCount = 0;
            int medicinalHerbUseCount = 0;
            int skillUseCount = 0;
            int recoverySkillUseCount = 0;
            var floorSkillActivation = new Dictionary<string, long>();
            int dmgDealt = 0;
            int dmgTaken = 0;
            int turnsSum = 0;
            int winTurnsSum = 0;
            int levelUpsThisFloor = 0;
            int fleesThisFloor = 0;
            int lastBattleEnemyCount = 0;
            int sumEnemyMonstersInBattlesThisFloor = 0;
            int maxEnemyMonstersSingleBattleThisFloor = 0;
            int companionJoinsThisFloor = 0;

            int stepsTakenActual = 0;
            for (int s = 0; s < steps; s++)
            {
                stepsTakenActual++;

                if (settings.dungeonStepHealEnabled)
                {
                    bool periodic = settings.stepHealPeriodicN > 0 && stepsTakenActual % settings.stepHealPeriodicN == 0;
                    bool roll = settings.stepHealRollChance > 0f && rng.NextDouble() < settings.stepHealRollChance;
                    if (periodic || roll)
                    {
                        TryHealPartyPriorityHerbPotionChalice(player, settings, rng, out int hs, out int ps, out int cs);
                        medicinalHerbUseCount += hs;
                        potionUseCount += ps;
                        dawnChaliceUseCount += cs;
                    }
                }

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
                    notes = string.IsNullOrEmpty(notes) ? "maxBattlesPerFloor reached" : notes + "; maxBattlesPerFloor reached";
                    break;
                }

                int hPre, pPre, cPre;
                TryHealPartyPriorityHerbPotionChalice(player, settings, rng, out hPre, out pPre, out cPre);
                medicinalHerbUseCount += hPre;
                potionUseCount += pPre;
                dawnChaliceUseCount += cPre;

                var enemyParty = monsterPool.BuildEnemyParty(entry, rng);
                ApplyDungeonSimEnemyPartySize(enemyParty, entry, monsterPool, rng, settings);
                if (enemyParty.Count == 0) continue;
                var enemyUnits = BuildEnemyUnitsFromMonsterSOs(enemyParty);
                if (enemyUnits.Count == 0) continue;

                if (settings.aiProgressionEnabled && settings.aiTownRetreatBeforeUnsafeBattle && settings.floor1TownFullRestoreEnabled)
                {
                    int maxStrike = GetMaxMonsterStrikeFromParty(enemyParty);
                    int turnsProbe = Mathf.Max(1, settings.aiSurvivalEnemyTurnCount);
                    for (int tr = 0; tr < settings.aiMaxTownRetreatsPerEncounter; tr++)
                    {
                        if (maxStrike <= 0) break;
                        long needHp = (long)maxStrike * turnsProbe;
                        if (player.GetTotalCurrentHP() >= needHp) break;
                        ApplyFloor1TownFullRestore(player, settings, rng, monsterPool, floor, equipHist?.AiPick);
                        if (player.GetTotalCurrentHP() >= needHp) break;
                    }
                }

                encounters++;
                lastBattleEnemyCount = enemyUnits.Count;
                sumEnemyMonstersInBattlesThisFloor += enemyUnits.Count;
                if (enemyUnits.Count > maxEnemyMonstersSingleBattleThisFloor)
                    maxEnemyMonstersSingleBattleThisFloor = enemyUnits.Count;

                battles++;
                bool allowFleeForThisBattle = fleesThisFloor < Mathf.Max(0, settings.maxFleesPerFloor);

                int thisBattleSeq = 0;
                if (_sessionLoggingActive)
                {
                    _sessionBattleSeq++;
                    thisBattleSeq = _sessionBattleSeq;
                    LogPartySlotRowsForBattle(thisBattleSeq, runId, seed, floor, player);
                }

                var outcome = bm.RunOneBattleForDungeon(player.Units, enemyUnits, rng, allowFleeForThisBattle, player, settings);

                if (_sessionLoggingActive && thisBattleSeq > 0)
                    AppendBattleLogRow(thisBattleSeq, runId, seed, floor, encounters, enemyParty, outcome);

                turnsSum += outcome.Turns;
                dmgDealt += outcome.TotalDamageDealtToEnemies;
                dmgTaken += outcome.TotalDamageTakenByAllies;
                skillUseCount += outcome.SkillUseCount;
                recoverySkillUseCount += outcome.RecoverySkillUseCount;
                SimSkillActivationLog.MergeEncoded(floorSkillActivation, outcome.SkillActivationBreakdown);
                potionUseCount += outcome.BattleHpPotionUses;
                dawnChaliceUseCount += outcome.BattleDawnChaliceUses;
                medicinalHerbUseCount += outcome.BattleMedicinalHerbUses;

                if (outcome.AllyWin)
                {
                    battleWins++;
                    winTurnsSum += outcome.Turns;
                    int xpThisBattle = 0;
                    int goldThisBattle = 0;
                    foreach (var m in enemyParty)
                    {
                        if (m == null) continue;
                        xpThisBattle += m.ExpReward;
                        goldThisBattle += m.GoldReward;
                    }
                    float xpMult = settings.simExpRewardMultiplier <= 0f ? 1f : settings.simExpRewardMultiplier;
                    int xpAward = Mathf.Max(0, Mathf.RoundToInt(xpThisBattle * xpMult));
                    if (xpThisBattle > 0 && xpAward < 1)
                        xpAward = 1;
                    xpGained += xpAward;
                    goldGained += goldThisBattle;
                    player.Exp += xpAward;
                    player.Gold += goldThisBattle;

                    while (player.Exp >= settings.GetExpToNextLevel(player.Level))
                    {
                        player.Exp -= settings.GetExpToNextLevel(player.Level);
                        ApplyLevelUp(player, settings, rng);
                        levelUpsThisFloor++;
                    }

                    ReplaceUnderleveledMonsterCompanionsAfterVictory(player, enemyParty, rng, settings, ref notes);

                    TryRollCompanionAfterVictory(
                        player, enemyParty, rng, settings, equipHist,
                        ref companionJoinsThisFloor, ref notes,
                        _sessionLoggingActive ? _logRecruits : null,
                        thisBattleSeq, runId, seed, floor);

                    if (floor == 1 && settings.floor1TownFullRestoreEnabled && settings.floor1TownAfterVictoryEnabled)
                        ApplyFloor1TownFullRestore(player, settings, rng, monsterPool, floor, equipHist?.AiPick);

                    cooldown = settings.postBattleCooldownSteps;
                }
                else if (outcome.AllyEscaped)
                {
                    fleesThisFloor++;
                    cooldown = settings.postBattleCooldownSteps;
                }
                else
                {
                    battlesLostThisFloor++;
                    notes = string.IsNullOrEmpty(notes) ? "party wiped" : notes + "; party wiped";
                    break;
                }

                int hPost, pPost, cPost;
                TryHealPartyPriorityHerbPotionChalice(player, settings, rng, out hPost, out pPost, out cPost);
                medicinalHerbUseCount += hPost;
                potionUseCount += pPost;
                dawnChaliceUseCount += cPost;
            }

            fr.StepsMoved = stepsTakenActual;
            fr.Encounters = encounters;
            fr.Battles = battles;
            fr.BattlesWon = battleWins;
            fr.BattlesFled = fleesThisFloor;
            fr.BattlesLost = battlesLostThisFloor;
            fr.HerbUseDelta = medicinalHerbUseCount;
            fr.PotionUseDelta = potionUseCount;
            fr.ChaliceUseDelta = dawnChaliceUseCount;
            fr.HerbOnExit = player.MedicinalHerbCount;
            fr.PotionOnExit = player.HpPotionCount;
            fr.ChaliceOnExit = player.DawnChaliceCharges;
            fr.SkillActivationBreakdown = SimSkillActivationLog.ToEncoded(floorSkillActivation);
            fr.DamageDealtFloor = dmgDealt;
            fr.DamageTakenFloor = dmgTaken;
            fr.TotalBattleTurns = turnsSum;
            fr.WinBattleTurnsSum = winTurnsSum;
            fr.XpGained = xpGained;
            fr.GoldGained = goldGained;
            fr.LevelOnExit = player.Level;
            fr.PartyHpPctExit = GetPartyHpPct(player);
            fr.PartyMpPctExit = GetPartyMpPct(player);
            fr.TotalHpExit = player.GetTotalCurrentHP();
            fr.TotalMpExit = player.GetTotalCurrentMP();
            fr.DeathFlag = !player.AnyAlive() ? 1 : 0;
            fr.ClearFlag = player.AnyAlive() ? 1 : 0;
            fr.Floor1TownUsesCumulative = player.Floor1TownUsesTotal;
            fr.LastBattleEnemyCount = lastBattleEnemyCount;
            fr.SumEnemyMonstersInBattlesThisFloor = sumEnemyMonstersInBattlesThisFloor;
            fr.MaxEnemyMonstersSingleBattleThisFloor = maxEnemyMonstersSingleBattleThisFloor;
            fr.CompanionJoinsThisFloor = companionJoinsThisFloor;
            fr.CompanionsJoinedCumulative = player.FormatCompanionJoinHistorySig();
            if (fr.DeathFlag != 0)
                fr.DeathAiEquipSignature = DungeonSimAiEquipment.GetPartyEquipSignature(player);
            if (levelUpsThisFloor > 0)
                notes = string.IsNullOrEmpty(notes) ? $"+{levelUpsThisFloor} levelup" : notes + $"; +{levelUpsThisFloor} levelup";
            if (fleesThisFloor > 0)
                notes = string.IsNullOrEmpty(notes) ? $"flee×{fleesThisFloor}" : notes + $"; flee×{fleesThisFloor}";
            fr.CompanionCountExit = CountMonsterCompanions(player);
            fr.RosterAllyCountExit = CountRosterAllies(player);
            fr.Notes = notes;

            return fr;
        }

        /// <summary>
        /// AI 파밍 목표 레벨 — 수동 게이트, (옵션) 풀 스탯 추정, (옵션) 턴 승리 프로브, (옵션) 10층 전체 클리어 최소 레벨 프로브.
        /// </summary>
        private int GetAiNextFloorRequiredLevel(int nextFloor)
        {
            if (_aiGateProbeSimpleOnly)
                return settings.GetEffectiveMinLevelToEnterFloor(nextFloor, monsterPool);

            int manual = settings.GetMinLevelToEnterFloor(nextFloor);
            int poolRec = (!settings.aiMergeMonsterPoolRecommendedLevel || monsterPool == null)
                ? 1
                : monsterPool.GetRecommendedMinLevelToEnterFloor(nextFloor);

            int effectiveNoTurn = settings.aiMergeMonsterPoolRecommendedLevel && monsterPool != null
                ? Mathf.Max(manual, poolRec)
                : manual;

            if (!settings.aiTurnWinGateEnabled)
            {
                int lv = effectiveNoTurn;
                if (settings.aiFloor10ClearMinLevelGateEnabled && nextFloor == settings.floorCount)
                    lv = Mathf.Max(lv, GetCachedFloor10ClearMinLevel());
                return lv;
            }

            if (!_aiTurnWinMinLevelCache.TryGetValue(nextFloor, out int turnRec))
            {
                turnRec = ComputeMinPlayerLevelForNextFloorTurnWin(nextFloor);
                _aiTurnWinMinLevelCache[nextFloor] = turnRec;
            }

            int lv2 = settings.aiTurnWinGateReplacesPoolRecommendation
                ? Mathf.Max(manual, turnRec)
                : Mathf.Max(manual, poolRec, turnRec);

            if (settings.aiFloor10ClearMinLevelGateEnabled && nextFloor == settings.floorCount)
                lv2 = Mathf.Max(lv2, GetCachedFloor10ClearMinLevel());
            return lv2;
        }

        private int GetCachedFloor10ClearMinLevel()
        {
            if (_floor10ClearMinCached)
                return _floor10ClearMinLevel;
            _floor10ClearMinLevel = ComputeMinPlayerLevelForFullDungeonClear();
            _floor10ClearMinCached = true;
            return _floor10ClearMinLevel;
        }

        /// <summary>
        /// 최소 L — L레벨 파티로 1~floorCount층을 연속 클리어(사망 없음)할 확률이 기준 이상인 가장 낮은 L.
        /// 프로브 구간에는 <see cref="_aiGateProbeSimpleOnly"/>로 10층 게이트·턴 게이트를 끕니다.
        /// </summary>
        private int ComputeMinPlayerLevelForFullDungeonClear()
        {
            if (monsterPool == null || allyRoster == null || settings == null) return 1;
            var bm = ResolveBattleSimulator();
            if (bm == null) return 1;

            int probes = Mathf.Max(1, settings.aiFloor10ClearProbesPerLevel);
            float ratio = Mathf.Clamp(settings.aiFloor10ClearRequiredSuccessRatio, 0.5f, 1f);
            int needOk = Mathf.CeilToInt(probes * ratio);
            int maxL = Mathf.Max(1, settings.aiFloor10ClearMaxSearchLevel);

            bool prev = _aiGateProbeSimpleOnly;
            _aiGateProbeSimpleOnly = true;
            try
            {
                for (int L = 1; L <= maxL; L++)
                {
                    int ok = 0;
                    for (int p = 0; p < probes; p++)
                    {
                        int growthSeed = unchecked(settings.baseSeed ^ (L * unchecked((int)0x85EBCA6B)) ^ (p * unchecked((int)0xC2B2AE35)));
                        int runSeed = unchecked(growthSeed + 1747632519 + p * 49999);
                        if (SimulateFullDungeonRunClearAllFloors(L, growthSeed, runSeed, bm))
                            ok++;
                        if (ok >= needOk)
                            return L;
                    }
                }

                return maxL;
            }
            finally
            {
                _aiGateProbeSimpleOnly = prev;
            }
        }

        /// <summary>한 회차 분량 — 전 층 무사 통과 시 true.</summary>
        private bool SimulateFullDungeonRunClearAllFloors(int playerLevel, int levelGrowthSeed, int runSeed, BattleSimulator bm)
        {
            var player = CreateSimPlayerAtLevel(allyRoster, settings, playerLevel, levelGrowthSeed);
            if (player == null || !player.AnyAlive()) return false;
            var rng = new System.Random(runSeed);
            for (int floor = 1; floor <= settings.floorCount; floor++)
            {
                if (!player.AnyAlive()) return false;
                var fr = RunSingleFloor(-1, runSeed, floor, player, rng, bm, null, skipFloorEntryEffects: false);
                if (fr.DeathFlag != 0) return false;

                if (settings.aiProgressionEnabled && floor < settings.floorCount)
                {
                    int needLv = GetAiNextFloorRequiredLevel(floor + 1);
                    int targetLv = needLv + Mathf.Max(0, settings.aiExtraLevelsBeyondNextFloorGate);
                    int farmPasses = 0;
                    while (player.AnyAlive()
                           && player.Level < targetLv
                           && farmPasses < settings.aiMaxFarmingPassesBeforeNextFloor)
                    {
                        var farmFr = RunSingleFloor(-1, runSeed, floor, player, rng, bm, null, skipFloorEntryEffects: true);
                        farmPasses++;
                        if (farmFr.DeathFlag != 0) return false;
                    }
                }
            }

            return player.AnyAlive();
        }

        /// <summary>
        /// <see cref="DungeonSimSettings.aiTurnWinGateMaxTurns"/>턴 이내 승리하는 프로브 비율이 기준을 넘는 최소 플레이어 레벨(탐색 상한까지).
        /// </summary>
        private int ComputeMinPlayerLevelForNextFloorTurnWin(int nextFloor)
        {
            if (monsterPool == null || allyRoster == null || settings == null) return 1;
            var bm = ResolveBattleSimulator();
            if (bm == null) return 1;

            var entry = monsterPool.GetEntryForFloor(nextFloor);
            if (entry == null) return 1;

            int turnCap = Mathf.Max(1, settings.aiTurnWinGateMaxTurns);
            int probes = Mathf.Max(1, settings.aiTurnWinGateProbesPerLevel);
            float ratio = Mathf.Clamp(settings.aiTurnWinGateRequiredSuccessRatio, 0.5f, 1f);
            int needOk = Mathf.CeilToInt(probes * ratio);
            int maxL = Mathf.Max(1, settings.aiTurnWinGateMaxSearchLevel);
            int growthSeedBase = settings.baseSeed ^ (nextFloor * unchecked((int)0x9E3779B9));

            for (int L = 1; L <= maxL; L++)
            {
                int growthSeed = unchecked(growthSeedBase + L * 7919);
                var template = CreateSimPlayerAtLevel(allyRoster, settings, L, growthSeed);
                if (template == null || !template.AnyAlive()) continue;

                int ok = 0;
                for (int b = 0; b < probes; b++)
                {
                    int brng = unchecked(growthSeed + b * 130027 + nextFloor * 17);
                    var battleRng = new System.Random(brng);
                    var enemyParty = monsterPool.BuildEnemyParty(entry, battleRng);
                    ApplyDungeonSimEnemyPartySize(enemyParty, entry, monsterPool, battleRng, settings);
                    if (enemyParty == null || enemyParty.Count == 0) continue;

                    var allies = CloneBattleSimUnits(template.Units);
                    var enemies = BuildEnemyUnitsFromMonsterSOs(enemyParty);
                    if (enemies == null || enemies.Count == 0) continue;

                    var outcome = bm.RunOneBattleForDungeon(allies, enemies, battleRng, false, null, null);
                    if (outcome.AllyWin && outcome.Turns <= turnCap) ok++;
                }

                if (ok >= needOk) return L;
            }

            return maxL;
        }

        private static List<BattleSimUnit> CloneBattleSimUnits(List<BattleSimUnit> src)
        {
            var list = new List<BattleSimUnit>(src != null ? src.Count : 0);
            if (src == null) return list;
            for (int i = 0; i < src.Count; i++)
            {
                var u = src[i];
                if (u == null) continue;
                var c = new BattleSimUnit
                {
                    Team = u.Team,
                    DisplayName = u.DisplayName,
                    Slot = u.Slot,
                    MaxHP = u.MaxHP,
                    CurrentHP = u.CurrentHP,
                    MaxMP = u.MaxMP,
                    CurrentMP = u.CurrentMP,
                    Attack = u.Attack,
                    Defense = u.Defense,
                    Magic = u.Magic,
                    Agility = u.Agility,
                    Luck = u.Luck,
                    SimAiPattern = u.SimAiPattern,
                    MemorySlot1 = u.MemorySlot1,
                    MemorySlot2 = u.MemorySlot2,
                    MemorySlot3 = u.MemorySlot3,
                    SimCharacterClass = u.SimCharacterClass,
                    SimStatLayerEquipmentEnabled = u.SimStatLayerEquipmentEnabled,
                    SimDungeonPartyLevel = u.SimDungeonPartyLevel,
                    SimIsMonsterCompanion = u.SimIsMonsterCompanion,
                    SimCompanionMonsterLevel = u.SimCompanionMonsterLevel,
                    IntrinsicMaxHP = u.IntrinsicMaxHP,
                    IntrinsicMaxMP = u.IntrinsicMaxMP,
                    IntrinsicAttack = u.IntrinsicAttack,
                    IntrinsicDefense = u.IntrinsicDefense,
                    IntrinsicMagic = u.IntrinsicMagic,
                    IntrinsicAgility = u.IntrinsicAgility,
                    IntrinsicLuck = u.IntrinsicLuck,
                    SimEquipRightHand = u.SimEquipRightHand,
                    SimEquipLeftHand = u.SimEquipLeftHand,
                    SimEquipBody = u.SimEquipBody,
                    SimEquipAccessory1 = u.SimEquipAccessory1,
                    SimEquipAccessory2 = u.SimEquipAccessory2
                };
                if (u.SimSkills != null)
                {
                    for (int si = 0; si < u.SimSkills.Count; si++)
                    {
                        var sk = u.SimSkills[si];
                        if (sk != null) c.SimSkills.Add(sk);
                    }
                }
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 파티 레벨 L일 때 Sword Lore T1 5종을 순환하며 누적 습득(L=1이면 1번째, L=3이면 1~3번째).
        /// </summary>
        private static void SyncDungeonSwordLoreT1SkillsToPartyLevel(DungeonSimPlayer player)
        {
            if (player?.Units == null) return;
            var chain = DungeonSimSwordLoreT1SkillRegistry.GetOrderedT1Skills();
            int L = Mathf.Max(1, player.Level);
            if (chain == null || chain.Length == 0)
            {
                foreach (var u in player.Units)
                {
                    if (u == null) continue;
                    if (u.SimIsMonsterCompanion)
                    {
                        u.SimDungeonPartyLevel = L;
                        continue;
                    }
                    if (u.SimStatLayerEquipmentEnabled)
                        u.SimDungeonPartyLevel = L;
                }

                return;
            }

            foreach (var u in player.Units)
            {
                if (u == null || u.SimIsMonsterCompanion || !u.SimStatLayerEquipmentEnabled) continue;
                u.SimDungeonPartyLevel = L;
                if (u.SimSkills == null) u.SimSkills = new List<SkillData>();
                for (int li = 1; li <= L; li++)
                {
                    int idx = (li - 1) % chain.Length;
                    var sk = chain[idx];
                    if (sk == null) continue;
                    if (!u.SimSkills.Contains(sk))
                        u.SimSkills.Add(sk);
                }
            }
        }

        /// <summary>
        /// 던전 시뮬 레벨업.
        /// <para>HP/MP — 직업 hpPerLevel/mpPerLevel(없으면 Settings) + 종의 기억 HP/Lv·MP/Lv 합, 반올림(노이즈 없음).</para>
        /// <para>스탯 — 직업+종의 기억 가중치로 <b>1회</b> 추첨해 인트린식 +1, DEF/MAG면 MaxHP/MaxMP +3, 이어서 시뮬 자유 1스탯(균등 랜덤, DEF/MAG 시 HP/MP 보너스 동일).</para>
        /// </summary>
        private static void ApplyLevelUp(DungeonSimPlayer player, DungeonSimSettings settings, System.Random rng)
        {
            if (player == null || settings == null || rng == null) return;
            player.Level++;
            player.LevelUpsTotal++;
            foreach (var u in player.Units)
            {
                if (u == null || !u.IsAlive || u.SimIsMonsterCompanion) continue;

                AccumulateMemoryGrowthWeights(u,
                    out float memAtkW, out float memDefW, out float memMagW, out float memAgiW, out float memLukW,
                    out float hpGrowthSum, out float mpGrowthSum);

                int classHpGain = u.SimCharacterClass != null
                    ? Mathf.Max(0, u.SimCharacterClass.hpPerLevel)
                    : settings.levelUpHpGain;
                int classMpGain = u.SimCharacterClass != null
                    ? Mathf.Max(0, u.SimCharacterClass.mpPerLevel)
                    : settings.levelUpMpGain;
                int finalHpGain = Mathf.Max(0, Mathf.RoundToInt(classHpGain + hpGrowthSum));
                int finalMpGain = Mathf.Max(0, Mathf.RoundToInt(classMpGain + mpGrowthSum));

                u.IntrinsicMaxHP += finalHpGain;
                u.IntrinsicMaxMP += finalMpGain;

                AccumulateMemoryChanceBonuses(u,
                    out float bAtk, out float bDef, out float bMag, out float bAgi, out float bLuk);

                var c = u.SimCharacterClass;
                float wa, wd, wm, wag, wl;
                if (c != null && c.UsesIndependentStatLevelUpChances)
                {
                    wa = CharacterClass.ClampStatLevelUpChancePercent(c.levelUpAttackChancePercent + bAtk);
                    wd = CharacterClass.ClampStatLevelUpChancePercent(c.levelUpDefenseChancePercent + bDef);
                    wm = CharacterClass.ClampStatLevelUpChancePercent(c.levelUpMagicChancePercent + bMag);
                    wag = CharacterClass.ClampStatLevelUpChancePercent(c.levelUpAgilityChancePercent + bAgi);
                    wl = CharacterClass.ClampStatLevelUpChancePercent(c.levelUpLuckChancePercent + bLuk);
                }
                else
                {
                    if (c != null)
                    {
                        wa = Mathf.Max(0f, c.attackGrowthPerLevel);
                        wd = Mathf.Max(0f, c.defenseGrowthPerLevel);
                        wm = Mathf.Max(0f, c.magicGrowthPerLevel);
                        wag = Mathf.Max(0f, c.agilityGrowthPerLevel);
                        wl = Mathf.Max(0f, c.luckGrowthPerLevel);
                    }
                    else
                    {
                        wa = wd = wm = wag = wl = 0f;
                    }

                    wa += memAtkW;
                    wd += memDefW;
                    wm += memMagW;
                    wag += memAgiW;
                    wl += memLukW;
                    if (wa + wd + wm + wag + wl <= 0f)
                        wa = wd = wm = wag = wl = 1f;
                }

                SimWeightedPickOneIntrinsicStat(u, wa, wd, wm, wag, wl, rng, out int defGained, out int magGained);
                u.IntrinsicMaxHP += defGained * CharacterClass.HpBonusPerDefensePointGained;
                u.IntrinsicMaxMP += magGained * CharacterClass.MpBonusPerMagicPointGained;
                SimGrantRandomFreeStatPoint(u, rng);
            }

            SyncDungeonSwordLoreT1SkillsToPartyLevel(player);
            foreach (var u in player.Units)
            {
                if (u == null) continue;
                if (u.SimIsMonsterCompanion)
                {
                    u.SimDungeonPartyLevel = Mathf.Max(1, player.Level);
                    continue;
                }
                if (!u.IsAlive) continue;
                u.RecomputeSimDerivedStatsFromIntrinsicsAndEquipment();
                u.CurrentHP = u.MaxHP;
                u.CurrentMP = u.MaxMP;
            }
        }

        /// <summary>
        /// 로스터 베이스(Lv1)에서 던전 시뮬과 동일한 <see cref="ApplyLevelUp"/> 규칙을 반복 적용해 <paramref name="targetLevel"/>에 맞춥니다.
        /// <paramref name="levelGrowthSeed"/>가 같으면 레벨업 랜덤 시퀀스도 동일합니다.
        /// </summary>
        public static DungeonSimPlayer CreateSimPlayerAtLevel(
            BattleSimAllyRoster roster,
            DungeonSimSettings st,
            int targetLevel,
            int levelGrowthSeed)
        {
            if (roster == null || st == null) return null;
            int L = Mathf.Max(1, targetLevel);
            var rng = new System.Random(levelGrowthSeed == int.MinValue ? 1 : levelGrowthSeed);
            var p = BuildPlayerFromRoster(roster, st, forceBaselineLevel1: true);
            while (p.Level < L)
                ApplyLevelUp(p, st, rng);
            p.Exp = 0;
            return p;
        }

        private static void SimWeightedPickOneIntrinsicStat(
            BattleSimUnit u,
            float wa, float wd, float wm, float wag, float wl,
            System.Random rng,
            out int defenseGained,
            out int magicGained)
        {
            defenseGained = 0;
            magicGained = 0;
            if (u == null || rng == null) return;

            float t = wa + wd + wm + wag + wl;
            if (t <= 0f)
            {
                wa = wd = wm = wag = wl = 1f;
                t = 5f;
            }

            float pick = (float)(rng.NextDouble() * t);
            if (pick < wa) u.IntrinsicAttack++;
            else if (pick < wa + wd) { u.IntrinsicDefense++; defenseGained = 1; }
            else if (pick < wa + wd + wm) { u.IntrinsicMagic++; magicGained = 1; }
            else if (pick < wa + wd + wm + wag) u.IntrinsicAgility++;
            else u.IntrinsicLuck++;
        }

        private static void SimGrantRandomFreeStatPoint(BattleSimUnit u, System.Random rng)
        {
            if (u == null || rng == null) return;
            switch (rng.Next(0, 5))
            {
                case 0: u.IntrinsicAttack++; break;
                case 1:
                    u.IntrinsicDefense++;
                    u.IntrinsicMaxHP += CharacterClass.HpBonusPerDefensePointGained;
                    break;
                case 2:
                    u.IntrinsicMagic++;
                    u.IntrinsicMaxMP += CharacterClass.MpBonusPerMagicPointGained;
                    break;
                case 3: u.IntrinsicAgility++; break;
                default: u.IntrinsicLuck++; break;
            }
        }

        private static void AccumulateMemoryChanceBonuses(
            BattleSimUnit u,
            out float atkB, out float defB, out float magB, out float agiB, out float lukB)
        {
            float a = 0f, d = 0f, m1 = 0f, ag = 0f, l = 0f;
            AddMemChanceBonus(u.MemorySlot1, ref a, ref d, ref m1, ref ag, ref l);
            AddMemChanceBonus(u.MemorySlot2, ref a, ref d, ref m1, ref ag, ref l);
            AddMemChanceBonus(u.MemorySlot3, ref a, ref d, ref m1, ref ag, ref l);
            atkB = a; defB = d; magB = m1; agiB = ag; lukB = l;
        }

        private static void AddMemChanceBonus(
            MemoryOfSpeciesData m,
            ref float atkB, ref float defB, ref float magB, ref float agiB, ref float lukB)
        {
            if (m == null) return;
            atkB += m.attackLevelUpChanceBonus;
            defB += m.defenseLevelUpChanceBonus;
            magB += m.magicLevelUpChanceBonus;
            agiB += m.agilityLevelUpChanceBonus;
            lukB += m.luckLevelUpChanceBonus;
        }

        private static void AccumulateMemoryGrowthWeights(
            BattleSimUnit u,
            out float atkW, out float defW, out float magW, out float agiW, out float lukW,
            out float hpGrowthSum, out float mpGrowthSum)
        {
            float a = 0f, d = 0f, m1 = 0f, ag = 0f, l = 0f, hpS = 0f, mpS = 0f;
            AddMem(u.MemorySlot1, ref a, ref d, ref m1, ref ag, ref l, ref hpS, ref mpS);
            AddMem(u.MemorySlot2, ref a, ref d, ref m1, ref ag, ref l, ref hpS, ref mpS);
            AddMem(u.MemorySlot3, ref a, ref d, ref m1, ref ag, ref l, ref hpS, ref mpS);
            atkW = a; defW = d; magW = m1; agiW = ag; lukW = l;
            hpGrowthSum = hpS; mpGrowthSum = mpS;
        }

        private static void AddMem(
            MemoryOfSpeciesData m,
            ref float atkW, ref float defW, ref float magW, ref float agiW, ref float lukW,
            ref float hpGrowthSum, ref float mpGrowthSum)
        {
            if (m == null) return;
            atkW += Mathf.Max(0f, m.attackGrowthPerLevel);
            defW += Mathf.Max(0f, m.defenseGrowthPerLevel);
            magW += Mathf.Max(0f, m.magicGrowthPerLevel);
            agiW += Mathf.Max(0f, m.agilityGrowthPerLevel);
            lukW += Mathf.Max(0f, m.luckGrowthPerLevel);
            hpGrowthSum += Mathf.Max(0f, m.hpGrowthPerLevel);
            mpGrowthSum += Mathf.Max(0f, m.mpGrowthPerLevel);
        }

        /// <summary>생성 시 SO 고정 스탯 + 종의 기억의 고정 보정(%, 절대) 합산.</summary>
        private static void ComputeInitialStatsWithMemoryFlat(BattleSimDummyAllySO so, out int hp, out int mp, out int atk, out int def, out int mag, out int agi, out int luk)
        {
            int h = so.maxHP, mP = so.maxMP, a = so.attack, d = so.defense, mg = so.magic, ag = so.agility, lk = so.luck;
            int baseHpRef = Mathf.Max(1, so.maxHP);
            int baseMpRef = Mathf.Max(1, so.maxMP);
            ApplyMemFlat(so.memorySlot1, baseHpRef, baseMpRef, ref h, ref mP, ref a, ref d, ref mg, ref ag, ref lk);
            ApplyMemFlat(so.memorySlot2, baseHpRef, baseMpRef, ref h, ref mP, ref a, ref d, ref mg, ref ag, ref lk);
            ApplyMemFlat(so.memorySlot3, baseHpRef, baseMpRef, ref h, ref mP, ref a, ref d, ref mg, ref ag, ref lk);
            hp = h; mp = mP; atk = a; def = d; mag = mg; agi = ag; luk = lk;
        }

        private static void ApplyMemFlat(
            MemoryOfSpeciesData m,
            int baseHpRef,
            int baseMpRef,
            ref int hp, ref int mp, ref int atk, ref int def, ref int mag, ref int agi, ref int luk)
        {
            if (m == null) return;
            hp += Mathf.RoundToInt(baseHpRef * m.hpBonusPercent / 100f) + m.hpBonus;
            mp += Mathf.RoundToInt(baseMpRef * m.mpBonusPercent / 100f);
            atk += m.attackBonus;
            def += m.defenseBonus;
            mag += m.magicBonus;
            agi += m.agilityBonus;
            luk += m.luckBonus;
        }

        // ---------------------------------------------------------------
        // 1층 마을 — 시뮬 전용 (경제 없음: 풀 HP/MP + 소모품 스택 고정)
        // ---------------------------------------------------------------
        public static void ApplyFloor1TownFullRestore(
            DungeonSimPlayer player,
            DungeonSimSettings settings,
            System.Random rng,
            DungeonSimMonsterPool monsterPool,
            int currentFloorForEquipAi,
            Dictionary<string, int> aiEquipPickHistogram)
        {
            if (player?.Units == null || settings == null) return;

            player.HpPotionCount = Mathf.Max(0, settings.startingHpPotionCount);
            player.DawnChaliceCharges = Mathf.Max(0, settings.startingDawnChaliceCharges);
            if (settings.floor1TownMedicinalHerbStack > 0)
                player.MedicinalHerbCount = Mathf.Max(0, settings.floor1TownMedicinalHerbStack);

            if (rng != null && monsterPool != null)
                DungeonSimAiEquipment.TryPickAndApply(player, monsterPool, currentFloorForEquipAi, settings.floorCount, rng, aiEquipPickHistogram);

            foreach (var u in player.Units)
            {
                if (u == null || !u.IsAlive) continue;
                if (u.MaxHP > 0) u.CurrentHP = u.MaxHP;
                if (u.MaxMP > 0) u.CurrentMP = u.MaxMP;
            }

            player.Floor1TownUsesTotal++;
        }

        // ---------------------------------------------------------------
        // 약초 / 포션 / 새벽의 잔 — 시뮬 전용 (BattleSimulator에서도 호출)
        // 유지 목표(healTargetHpRatio)까지 자원이 허용하는 한 반복.
        // 일반: 약초(herbUseHpRatio 미만) → 포션 → 포션 소진 시 잔.
        // 긴급(emergencyHealHpRatio 미만): 포션 → 잔 → 약초.
        // ---------------------------------------------------------------
        public static void GetMedicinalHerbHealRange(DungeonSimSettings settings, out int minHp, out int maxHp)
        {
            minHp = 32;
            maxHp = 35;
            if (settings == null) return;
            if (settings.medicinalHerbData != null)
            {
                minHp = Mathf.Max(0, settings.medicinalHerbData.healHpMin);
                maxHp = Mathf.Max(minHp, settings.medicinalHerbData.healHpMax);
            }
            else
            {
                minHp = Mathf.Max(0, settings.medicinalHerbHealMinFallback);
                maxHp = Mathf.Max(minHp, settings.medicinalHerbHealMaxFallback);
            }
        }

        /// <summary>살아 있는 아군 중 누군가의 HP/MaxHP가 <paramref name="ratioThreshold"/> 미만이면 true.</summary>
        public static bool AnyAllyHpRatioStrictlyBelow(DungeonSimPlayer player, float ratioThreshold)
        {
            if (player?.Units == null) return false;
            foreach (var u in player.Units)
            {
                if (u == null || !u.IsAlive || u.MaxHP <= 0) continue;
                if ((float)u.CurrentHP / u.MaxHP < ratioThreshold) return true;
            }
            return false;
        }

        /// <summary>eligibleBelowRatio 미만인 유닛 중 HP%가 가장 낮은 1명에게 약초 1개.</summary>
        private static bool TryConsumeSingleMedicinalHerbOnWorstBelow(
            DungeonSimPlayer player,
            DungeonSimSettings settings,
            System.Random rng,
            float eligibleBelowRatio)
        {
            if (player == null || settings == null || rng == null) return false;
            if (player.MedicinalHerbCount <= 0) return false;
            BattleSimUnit pick = null;
            float worstRatio = 999f;
            foreach (var u in player.Units)
            {
                if (u == null || !u.IsAlive || u.MaxHP <= 0) continue;
                float r = (float)u.CurrentHP / u.MaxHP;
                if (r >= eligibleBelowRatio) continue;
                if (r < worstRatio)
                {
                    worstRatio = r;
                    pick = u;
                }
            }
            if (pick == null) return false;
            GetMedicinalHerbHealRange(settings, out int hMin, out int hMax);
            int amt = rng.Next(hMin, hMax + 1);
            pick.CurrentHP = Mathf.Min(pick.MaxHP, pick.CurrentHP + amt);
            player.MedicinalHerbCount--;
            player.MedicinalHerbsUsedTotal++;
            return true;
        }

        /// <summary>포션 1웨이브 — 비율 미만인 살아 있는 유닛 각각에 포션 1개씩(가능한 만큼).</summary>
        public static int TryConsumeHpPotionForDungeonSim(DungeonSimPlayer player, DungeonSimSettings settings)
        {
            if (player == null || settings == null) return 0;
            int used = 0;
            if (player.HpPotionCount <= 0) return 0;
            float cutoff = settings.GetPotionHealCutoff();

            foreach (var u in player.Units)
            {
                if (!u.IsAlive) continue;
                if (u.MaxHP <= 0) continue;
                float ratio = (float)u.CurrentHP / Mathf.Max(1, u.MaxHP);
                if (ratio < cutoff && player.HpPotionCount > 0)
                {
                    u.CurrentHP = Mathf.Min(u.MaxHP, u.CurrentHP + settings.hpPotionHealAmount);
                    player.HpPotionCount--;
                    player.HpPotionsUsedTotal++;
                    used++;
                }
            }
            return used;
        }

        public static void TryHealPartyPriorityHerbPotionChalice(
            DungeonSimPlayer player,
            DungeonSimSettings settings,
            System.Random rng,
            out int herbsUsed,
            out int potionsUsed,
            out int chalicesUsed)
        {
            herbsUsed = potionsUsed = chalicesUsed = 0;
            if (player == null || settings == null || rng == null) return;
            float maintain = settings.GetHealMaintainRatio();
            float herbCut = settings.GetHerbHealCutoff();
            float emerg = settings.GetEmergencyHealCutoff();

            if (!AnyAllyHpRatioStrictlyBelow(player, maintain))
                return;

            bool progressed = true;
            while (progressed && AnyAllyHpRatioStrictlyBelow(player, maintain))
            {
                progressed = false;
                bool crisis = AnyAllyHpRatioStrictlyBelow(player, emerg);

                if (crisis)
                {
                    int p = TryConsumeHpPotionForDungeonSim(player, settings);
                    if (p > 0)
                    {
                        potionsUsed += p;
                        progressed = true;
                        continue;
                    }

                    if (player.HpPotionCount <= 0 && player.DawnChaliceCharges > 0)
                    {
                        int ch = TryConsumeDawnChaliceForDungeonSim(player, settings, hpThresholdOverride: maintain, bypassPotionEmptyGate: true);
                        if (ch > 0)
                        {
                            chalicesUsed += ch;
                            progressed = true;
                            continue;
                        }
                    }

                    if (player.MedicinalHerbCount > 0 && TryConsumeSingleMedicinalHerbOnWorstBelow(player, settings, rng, maintain))
                    {
                        herbsUsed++;
                        progressed = true;
                        continue;
                    }
                }
                else
                {
                    if (player.MedicinalHerbCount > 0 && AnyAllyHpRatioStrictlyBelow(player, herbCut)
                        && TryConsumeSingleMedicinalHerbOnWorstBelow(player, settings, rng, herbCut))
                    {
                        herbsUsed++;
                        progressed = true;
                        continue;
                    }

                    int p2 = TryConsumeHpPotionForDungeonSim(player, settings);
                    if (p2 > 0)
                    {
                        potionsUsed += p2;
                        progressed = true;
                        continue;
                    }

                    if (player.HpPotionCount <= 0 && player.DawnChaliceCharges > 0 && AnyAllyHpRatioStrictlyBelow(player, maintain))
                    {
                        int ch2 = TryConsumeDawnChaliceForDungeonSim(player, settings, hpThresholdOverride: maintain, bypassPotionEmptyGate: true);
                        if (ch2 > 0)
                        {
                            chalicesUsed += ch2;
                            progressed = true;
                            continue;
                        }
                    }
                }
            }
        }

        public static int TryConsumeDawnChaliceForDungeonSim(
            DungeonSimPlayer player,
            DungeonSimSettings settings,
            float? hpThresholdOverride = null,
            bool bypassPotionEmptyGate = false)
        {
            if (player == null || settings == null) return 0;
            if (player.DawnChaliceCharges <= 0) return 0;
            if (!bypassPotionEmptyGate && settings.useDawnChaliceOnlyWhenPotionEmpty && player.HpPotionCount > 0) return 0;

            float th = hpThresholdOverride ?? settings.dawnChaliceHpThreshold;

            bool needsHeal = false;
            foreach (var u in player.Units)
            {
                if (!u.IsAlive) continue;
                if (u.MaxHP <= 0) continue;
                float ratio = (float)u.CurrentHP / Mathf.Max(1, u.MaxHP);
                if (ratio < th)
                {
                    needsHeal = true;
                    break;
                }
            }
            if (!needsHeal) return 0;

            int hpPct = Mathf.Clamp(Mathf.RoundToInt(settings.dawnChaliceHpHealPercent * 100f), 0, 100);
            int mpPct = Mathf.Clamp(Mathf.RoundToInt(settings.dawnChaliceMpHealPercent * 100f), 0, 100);

            foreach (var u in player.Units)
            {
                if (!u.IsAlive) continue;
                if (u.MaxHP > 0)
                {
                    int healHP = Mathf.Max(1, (u.MaxHP * hpPct) / 100);
                    u.CurrentHP = Mathf.Min(u.MaxHP, u.CurrentHP + healHP);
                }
                if (u.MaxMP > 0)
                {
                    int healMP = Mathf.Max(1, (u.MaxMP * mpPct) / 100);
                    u.CurrentMP = Mathf.Min(u.MaxMP, u.CurrentMP + healMP);
                }
            }

            player.DawnChaliceCharges--;
            player.DawnChaliceUsedTotal++;
            return 1;
        }

        // ---------------------------------------------------------------
        // 플레이어 빌드
        // ---------------------------------------------------------------
        /// <param name="forceBaselineLevel1">true면 Lv1·EXP0으로만 빌드(보스 레벨 탐침 등). false면 Settings의 startingLevel/startingExp 사용.</param>
        private static DungeonSimPlayer BuildPlayerFromRoster(BattleSimAllyRoster roster, DungeonSimSettings st, bool forceBaselineLevel1 = false)
        {
            int startLv = forceBaselineLevel1 ? 1 : Mathf.Max(1, st.startingLevel);
            var p = new DungeonSimPlayer
            {
                Name = "Party",
                Level = startLv,
                Exp = forceBaselineLevel1 ? 0 : Mathf.Max(0, st.startingExp),
                Gold = 0,
                HpPotionCount = Mathf.Max(0, st.startingHpPotionCount),
                DawnChaliceCharges = Mathf.Max(0, st.startingDawnChaliceCharges)
            };

            var ordered = roster.GetOrderedAllies();
            for (int i = 0; i < ordered.Length; i++)
            {
                var so = ordered[i];
                if (so == null) continue;
                int slotNum = i + 1;
                var slot = (BattleSlot)slotNum;
                ComputeInitialStatsWithMemoryFlat(so, out int hp, out int mp, out int atk, out int def, out int mag, out int agi, out int luk);
                var u = new BattleSimUnit
                {
                    Team = BattleSimTeam.Ally,
                    DisplayName = so.allyDisplayName,
                    Slot = slot,
                    SimStatLayerEquipmentEnabled = true,
                    IntrinsicMaxHP = hp,
                    IntrinsicMaxMP = mp,
                    IntrinsicAttack = atk,
                    IntrinsicDefense = def,
                    IntrinsicMagic = mag,
                    IntrinsicAgility = agi,
                    IntrinsicLuck = luk,
                    SimAiPattern = so.simAiPattern,
                    SimSkills = new List<SkillData>(so.simSkills ?? new List<SkillData>()),
                    MemorySlot1 = so.memorySlot1,
                    MemorySlot2 = so.memorySlot2,
                    MemorySlot3 = so.memorySlot3,
                    SimCharacterClass = so.characterClass
                };
                p.Units.Add(u);
            }

            SyncDungeonSwordLoreT1SkillsToPartyLevel(p);
            foreach (var u in p.Units)
            {
                if (u != null && u.SimStatLayerEquipmentEnabled)
                    u.RecomputeSimDerivedStatsFromIntrinsicsAndEquipment();
            }

            return p;
        }

        /// <summary>인카운터 직전 생존 판정용 — 적 파티 중 max(ATK, MAG) 최댓값.</summary>
        private static int GetMaxMonsterStrikeFromParty(List<MonsterSO> party)
        {
            int maxStrike = 0;
            if (party == null) return 0;
            for (int i = 0; i < party.Count; i++)
            {
                var m = party[i];
                if (m == null) continue;
                maxStrike = Mathf.Max(maxStrike, Mathf.Max(m.ATK, m.MAG));
            }
            return maxStrike;
        }

        /// <summary>
        /// 승리·레벨업 처리 후 — 몬스터 동료의 <see cref="BattleSimUnit.SimCompanionMonsterLevel"/>이 플레이어보다
        /// <see cref="DungeonSimSettings.simCompanionReplaceMinLevelGap"/> 이상 낮으면 제거하고, 같은 전투 적 목록에서 교체 영입(CompanionChance 없음).
        /// 아군 4인·몬스터 동료 3인 상한은 <see cref="TryAddMonsterCompanionToParty"/>로 유지.
        /// </summary>
        private static void ReplaceUnderleveledMonsterCompanionsAfterVictory(
            DungeonSimPlayer player,
            List<MonsterSO> enemyParty,
            System.Random rng,
            DungeonSimSettings settings,
            ref string floorNotes)
        {
            if (settings == null || !settings.simCompanionAutoReplaceWhenLevelLagEnabled) return;
            if (player?.Units == null || enemyParty == null || rng == null) return;

            int pLv = Mathf.Max(1, player.Level);
            int gap = Mathf.Max(2, settings.simCompanionReplaceMinLevelGap);

            var toRemove = new List<BattleSimUnit>();
            for (int i = 0; i < player.Units.Count; i++)
            {
                var u = player.Units[i];
                if (u == null || !u.SimIsMonsterCompanion) continue;
                if (u.SimCompanionMonsterLevel <= 0) continue;
                if (pLv - u.SimCompanionMonsterLevel >= gap)
                    toRemove.Add(u);
            }
            if (toRemove.Count == 0) return;

            foreach (var u in toRemove)
            {
                player.Units.Remove(u);
                string tag = string.IsNullOrEmpty(u.DisplayName) ? "?" : u.DisplayName.Replace(';', ',');
                floorNotes = AppendSemiColonNote(floorNotes, "companion_cull_level:" + tag);
            }

            for (int k = 0; k < toRemove.Count; k++)
            {
                if (!CanPartyAcceptAnotherMonsterCompanion(player)) break;
                if (!TryRecruitReplacementCompanionFromEnemyParty(player, enemyParty, pLv, rng, ref floorNotes))
                    floorNotes = AppendSemiColonNote(floorNotes, "companion_replace_fail:no_eligible");
            }
        }

        private static bool CanPartyAcceptAnotherMonsterCompanion(DungeonSimPlayer player)
        {
            if (player?.Units == null) return false;
            if (player.Units.Count >= SimDungeonMaxAllySlots) return false;
            int n = 0;
            for (int i = 0; i < player.Units.Count; i++)
            {
                var u = player.Units[i];
                if (u != null && u.SimIsMonsterCompanion) n++;
            }
            return n < SimDungeonMaxMonsterCompanions;
        }

        /// <summary>동료화 가능 몬스터만. 우선 플레이어−1 이상, 다음 −2 이상, 마지막으로 아무 동료화 가능체.</summary>
        private static MonsterSO PickReplacementMonsterFromEnemyParty(List<MonsterSO> enemyParty, int playerLevel, System.Random rng)
        {
            if (enemyParty == null || rng == null) return null;
            int p = Mathf.Max(1, playerLevel);
            var tier1 = new List<MonsterSO>();
            var tier2 = new List<MonsterSO>();
            var tier3 = new List<MonsterSO>();
            for (int i = 0; i < enemyParty.Count; i++)
            {
                var m = enemyParty[i];
                if (m == null || !m.CanBecomCompanion) continue;
                int ml = m.MonsterLevel;
                if (ml >= p - 1) tier1.Add(m);
                else if (ml >= p - 2) tier2.Add(m);
                else tier3.Add(m);
            }
            MonsterSO Pick(List<MonsterSO> pool) =>
                pool != null && pool.Count > 0 ? pool[rng.Next(pool.Count)] : null;
            return Pick(tier1) ?? Pick(tier2) ?? Pick(tier3);
        }

        private static bool TryRecruitReplacementCompanionFromEnemyParty(
            DungeonSimPlayer player,
            List<MonsterSO> enemyParty,
            int playerLevel,
            System.Random rng,
            ref string floorNotes)
        {
            if (!CanPartyAcceptAnotherMonsterCompanion(player)) return false;
            var m = PickReplacementMonsterFromEnemyParty(enemyParty, playerLevel, rng);
            if (m == null) return false;
            var cd = m.CompanionData;
            if (cd == null) return false;
            if (!TryAddMonsterCompanionToParty(player, m, cd, ref floorNotes))
                return false;
            string nm = string.IsNullOrWhiteSpace(cd.CompanionName)
                ? (string.IsNullOrWhiteSpace(m.MonsterName) ? m.name : m.MonsterName)
                : cd.CompanionName;
            floorNotes = AppendSemiColonNote(floorNotes, "companion_replace_join:" + (string.IsNullOrEmpty(nm) ? "?" : nm.Replace(';', ',')));
            return true;
        }

        /// <summary>전투 승리 직후 — 해당 전투 적 목록에서 균등 1체를 골라 <see cref="MonsterSO.CompanionChance"/> 1회만 굴립니다. 성공 시 <see cref="CompanionSO"/> 기반 유닛을 파티에 추가합니다(몬스터 동료 최대 3, 아군 슬롯 최대 4).</summary>
        private static void TryRollCompanionAfterVictory(
            DungeonSimPlayer player,
            List<MonsterSO> enemyParty,
            System.Random rng,
            DungeonSimSettings settings,
            SimEquipHistograms equipHist,
            ref int companionJoinsThisFloor,
            ref string floorNotes,
            List<DungeonSimRecruitLogRow> recruitLog,
            int battleSeq,
            int runId,
            int seed,
            int floor)
        {
            void LogRow(string mName, int mLv, string chanceStr, string rollStr, int success, string failReason, string joinedName, int joinedSlot)
            {
                if (recruitLog == null) return;
                recruitLog.Add(new DungeonSimRecruitLogRow
                {
                    BattleSeq = battleSeq,
                    RunId = runId,
                    Seed = seed,
                    Floor = floor,
                    MonsterName = mName ?? "",
                    MonsterLevel = mLv,
                    CompanionChanceStr = chanceStr ?? "",
                    RollU01Str = rollStr ?? "",
                    Success = success,
                    FailReason = failReason ?? "",
                    JoinedDisplayName = joinedName ?? "",
                    JoinedSlot = joinedSlot
                });
            }

            if (settings == null || !settings.simCompanionRollAfterVictoryEnabled) return;
            if (player?.Units == null || enemyParty == null || enemyParty.Count == 0 || rng == null) return;

            int idx = rng.Next(enemyParty.Count);
            var m = enemyParty[idx];
            if (m == null)
            {
                LogRow("?", 0, "", "", 0, "null_monster_pick", "", 0);
                return;
            }

            string mName = string.IsNullOrEmpty(m.MonsterName) ? m.name : m.MonsterName;
            int mLv = m.MonsterLevel;

            if (!m.CanBecomCompanion)
            {
                LogRow(mName, mLv, "", "", 0, "not_companion_eligible", "", 0);
                return;
            }

            double roll = rng.NextDouble();
            float ch = m.CompanionChance;
            string rollStr = roll.ToString("0.######", CultureInfo.InvariantCulture);
            string chStr = ch.ToString("0.######", CultureInfo.InvariantCulture);
            if (roll >= ch)
            {
                LogRow(mName, mLv, chStr, rollStr, 0, "probability", "", 0);
                return;
            }

            if (!CanPartyAcceptAnotherMonsterCompanion(player))
            {
                LogRow(mName, mLv, chStr, rollStr, 0,
                    player.Units.Count >= SimDungeonMaxAllySlots ? "party_full" : "max_monster_companions", "", 0);
                floorNotes = AppendSemiColonNote(floorNotes,
                    player.Units.Count >= SimDungeonMaxAllySlots
                        ? "companion_join_skip:party_full"
                        : "companion_join_skip:max_monster_companions");
                return;
            }

            var cd = m.CompanionData;
            if (cd == null)
            {
                LogRow(mName, mLv, chStr, rollStr, 0, "no_companion_so", "", 0);
                return;
            }

            if (!TryAddMonsterCompanionToParty(player, m, cd, ref floorNotes))
            {
                LogRow(mName, mLv, chStr, rollStr, 0, "add_failed", "", 0);
                return;
            }

            int joinedSlot = (int)player.Units[player.Units.Count - 1].Slot;

            string historyName = string.IsNullOrWhiteSpace(cd.CompanionName)
                ? (string.IsNullOrWhiteSpace(m.MonsterName) ? m.name : m.MonsterName)
                : cd.CompanionName;
            player.SimCompanionJoinsTotal++;
            player.LastCompanionJoinedName = historyName;
            player.SimCompanionJoinHistory.Add(historyName);
            companionJoinsThisFloor++;
            floorNotes = AppendSemiColonNote(floorNotes, "companion_join:" + historyName);
            LogRow(mName, mLv, chStr, rollStr, 1, "", historyName, joinedSlot);
            if (equipHist != null)
            {
                if (!equipHist.CompanionJoinByName.TryGetValue(historyName, out int c))
                    c = 0;
                equipHist.CompanionJoinByName[historyName] = c + 1;
            }
        }

        private static string AppendSemiColonNote(string existing, string add)
        {
            if (string.IsNullOrEmpty(add)) return existing ?? "";
            if (string.IsNullOrEmpty(existing)) return add;
            return existing + ";" + add;
        }

        /// <summary>MonsterSO 리스트 → 전투 시뮬 적 유닛(슬롯 1부터).</summary>
        public static List<BattleSimUnit> BuildEnemyUnitsFromMonsterSOs(List<MonsterSO> party)
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

        /// <summary>
        /// <see cref="DungeonSimSettings.simAlwaysFourEnemyUnits"/>가 켜져 있으면 적 파티를 4마리로 맞춥니다(던전 루프·보스 프로브 등 공통).
        /// </summary>
        public static void ApplyDungeonSimEnemyPartySize(
            List<MonsterSO> party,
            DungeonSimMonsterPool.FloorEntry entry,
            DungeonSimMonsterPool pool,
            System.Random rng,
            DungeonSimSettings settings)
        {
            if (party == null || entry == null || pool == null || rng == null || settings == null) return;
            if (!settings.simAlwaysFourEnemyUnits) return;
            pool.NormalizeEnemyPartyMonsterCount(party, entry, rng, SimDungeonMaxAllySlots);
        }

        private static bool TryPickEmptyAllySlotForCompanion(BattleSimUnit[] occupiedSlots1To4, SlotMask allowed, out BattleSlot slot)
        {
            slot = BattleSlot.None;
            for (int n = 1; n <= SimDungeonMaxAllySlots; n++)
            {
                SlotMask bit = (SlotMask)(1 << (n - 1));
                if ((allowed & bit) == 0) continue;
                if (occupiedSlots1To4[n] != null) continue;
                slot = (BattleSlot)n;
                return true;
            }
            for (int n = 1; n <= SimDungeonMaxAllySlots; n++)
            {
                if (occupiedSlots1To4[n] != null) continue;
                slot = (BattleSlot)n;
                return true;
            }
            return false;
        }

        private static bool TryAddMonsterCompanionToParty(
            DungeonSimPlayer player,
            MonsterSO sourceMonster,
            CompanionSO comp,
            ref string floorNotes)
        {
            var occ = new BattleSimUnit[SimDungeonMaxAllySlots + 1];
            foreach (var u in player.Units)
            {
                if (u == null) continue;
                int sn = (int)u.Slot;
                if (sn >= 1 && sn <= SimDungeonMaxAllySlots) occ[sn] = u;
            }
            if (!TryPickEmptyAllySlotForCompanion(occ, comp.AllowedSlots, out BattleSlot slot))
            {
                floorNotes = AppendSemiColonNote(floorNotes, "companion_join_fail:no_slot");
                return false;
            }

            var unit = new BattleSimUnit
            {
                Team = BattleSimTeam.Ally,
                DisplayName = string.IsNullOrWhiteSpace(comp.CompanionName)
                    ? (string.IsNullOrWhiteSpace(sourceMonster.MonsterName) ? sourceMonster.name : sourceMonster.MonsterName)
                    : comp.CompanionName,
                Slot = slot,
                SimIsMonsterCompanion = true,
                SimStatLayerEquipmentEnabled = false,
                SimAiPattern = sourceMonster.AIPattern,
                MaxHP = Mathf.Max(1, comp.HP),
                CurrentHP = Mathf.Max(1, comp.HP),
                MaxMP = Mathf.Max(0, comp.MP),
                CurrentMP = Mathf.Max(0, comp.MP),
                Attack = Mathf.Max(0, comp.ATK),
                Defense = Mathf.Max(0, comp.DEF),
                Magic = Mathf.Max(0, comp.MAG),
                Agility = Mathf.Max(0, comp.AGI),
                Luck = Mathf.Max(0, comp.LUK),
                SimDungeonPartyLevel = Mathf.Max(1, player.Level),
                SimCompanionMonsterLevel = Mathf.Max(0, sourceMonster.MonsterLevel),
                SimSkills = new List<SkillData>()
            };
            if (comp.ActiveSkills != null)
            {
                foreach (var sk in comp.ActiveSkills)
                    if (sk != null) unit.SimSkills.Add(sk);
            }
            player.Units.Add(unit);
            return true;
        }

        // ---------------------------------------------------------------
        // CSV / Summary 출력
        // ---------------------------------------------------------------
        private string BuildSummary(int iterations, int deaths, int allFloorsCleared, long battles, long battleWins, long battleFlees, long medicinalHerbs, long potions, long dawnChalice, int[] reached, List<DungeonSimFloorLogRow> floorRows, List<DungeonSimRunLogRow> runRows, SimEquipHistograms equipHist)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Dungeon Simulation (Phase 1) ===");
            sb.AppendLine($"리포트 생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Iterations: {iterations}, Floors: {settings.floorCount}, BaseSeed: {settings.baseSeed}");
            sb.AppendLine($"Steps/Floor: {settings.stepsPerFloorMin}~{settings.stepsPerFloorMax}, EncounterChance: {settings.encounterChance:P0}, Cooldown: {settings.postBattleCooldownSteps}");
            sb.AppendLine($"시작 자원 — HP포션: {settings.startingHpPotionCount}개, 새벽의 잔: {settings.startingDawnChaliceCharges}회 (HP {settings.dawnChaliceHpHealPercent:P0} / MP {settings.dawnChaliceMpHealPercent:P0} 회복)");
            sb.AppendLine($"시뮬 EXP 배율: ×{settings.simExpRewardMultiplier:0.##} (승리 시 몬스터 ExpReward 합에 적용)");
            sb.AppendLine($"약초·포션·잔 — 유지 HP<{settings.GetHealMaintainRatio():P0}, 포션<{settings.GetPotionHealCutoff():P0}, 약초대상<{settings.GetHerbHealCutoff():P0}, 긴급<{settings.GetEmergencyHealCutoff():P0}, 잔판정<{settings.dawnChaliceHpThreshold:P0}; 이동 중 회복: {(settings.dungeonStepHealEnabled ? $"ON p={settings.stepHealRollChance:P0} / N={settings.stepHealPeriodicN}" : "OFF")}");
            {
                int hLo = Mathf.Min(settings.medicinalHerbGrantCountMin, settings.medicinalHerbGrantCountMax);
                int hHi = Mathf.Max(settings.medicinalHerbGrantCountMin, settings.medicinalHerbGrantCountMax);
                sb.AppendLine($"층 진입 약초: {settings.medicinalHerbGrantFloorsMin}~{settings.medicinalHerbGrantFloorsMax}층마다 진입 시 {hLo}~{hHi}개(균등 랜덤) 추가. 1층도 동일 범위에 포함되면 1층 진입 시에도 지급.");
            }
            if (settings.floor1TownFullRestoreEnabled)
            {
                string herbTown = settings.floor1TownMedicinalHerbStack > 0
                    ? $"약초는 마을 시마다 {settings.floor1TownMedicinalHerbStack}개로 덮어씀."
                    : "약초는 마을로 리필하지 않음(층 진입 지급만).";
                sb.AppendLine($"1층 마을(시뮬) — 1층 진입 시 HP·MP 풀, 포션 {settings.startingHpPotionCount}·잔 {settings.startingDawnChaliceCharges}으로 맞춤(경제 없음). {herbTown}" +
                              (settings.floor1TownAfterVictoryEnabled ? " 승리마다 동일 충전." : ""));
            }
            string enemyPartyLine = settings.simAlwaysFourEnemyUnits
                ? "적 파티(시뮬): 인카운터당 적 4명 고정(simAlwaysFourEnemyUnits). 부족 시 해당 층 풀 가중 추첨으로 보충. 각 슬롯은 가중치로 몬스터를 독립 추첨(동일 개체 중복 가능)."
                : "적 파티(시뮬): 인카운터당 적 수 = DungeonSimMonsterPool 해당 층의 partySizeMin~partySizeMax(1~4, 균등). 각 슬롯은 가중치로 몬스터를 독립 추첨(동일 개체 중복 가능).";
            sb.AppendLine(enemyPartyLine + " 아군은 로스터 + 몬스터 동료(CompanionSO)로 슬롯 최대 4, 몬스터 동료 최대 3.");
            if (floorRows != null)
            {
                long cj = 0;
                for (int ri = 0; ri < floorRows.Count; ri++)
                    cj += floorRows[ri].CompanionJoinsThisFloor;
                int rowN = floorRows.Count;
                double perFloorRow = rowN > 0 ? cj / (double)rowN : 0d;
                sb.AppendLine(
                    $"동료화(시뮬): CSV 층 행 {rowN}건에 기록된 합류 성공 합계 {cj}회(행당 평균 {perFloorRow:F3}회). " +
                    "※ 이 수치는 ‘회차당’이 아니라 모든 층·모든 회차의 층 행을 더한 값입니다. 회차당 분포는 아래 ‘동료 합류 성공 횟수’ 블록을 보세요. " +
                    $"승리 후 전투당 1회 무작위 적 1체에 MonsterSO.CompanionChance·CompanionSO. 성공 시 파티에 유닛 추가. simCompanionRollAfterVictoryEnabled={settings.simCompanionRollAfterVictoryEnabled}.");
                if (equipHist != null && equipHist.CompanionJoinByName != null && equipHist.CompanionJoinByName.Count > 0)
                {
                    var pairs = new List<KeyValuePair<string, int>>(equipHist.CompanionJoinByName);
                    pairs.Sort((a, b) =>
                    {
                        int cmp = b.Value.CompareTo(a.Value);
                        return cmp != 0 ? cmp : string.CompareOrdinal(a.Key, b.Key);
                    });
                    sb.Append("  동료 몬스터 합류 누적(이름별, CompanionChance 성공만): ");
                    for (int hi = 0; hi < pairs.Count; hi++)
                    {
                        if (hi > 0) sb.Append(" / ");
                        sb.Append(pairs[hi].Key).Append('=').Append(pairs[hi].Value);
                    }
                    sb.AppendLine();
                }
                AppendMostCommonEndOfRunCompanionCombos(sb, runRows, topN: 30);
                AppendCompanionJoinPerRunDepthStats(sb, runRows, iterations);
            }
            if (settings.aiProgressionEnabled)
            {
                sb.AppendLine("AI 진행(시뮬): ON — 전투 전 아군 현재 HP 합 < 적 max(ATK,MAG)×" + settings.aiSurvivalEnemyTurnCount + "이면 마을 풀충전(최대 " + settings.aiMaxTownRetreatsPerEncounter + "회/인카운터) 후 재판정, 불가면 전투.");
                sb.AppendLine("  다음 층 진입 최소 레벨 미달 시 같은 층 추가 패스(최대 " + settings.aiMaxFarmingPassesBeforeNextFloor + "회, CSV notes: ai_farm). 요구+추가: +" + settings.aiExtraLevelsBeyondNextFloorGate + "Lv.");
                if (settings.aiTurnWinGateEnabled)
                {
                    sb.AppendLine($"  다음 층 요구 레벨 — 턴 승리 기준(옵션): LvL 파티(CreateSimPlayerAtLevel)로 해당 층 풀 적 1마리 전투를 {settings.aiTurnWinGateProbesPerLevel}회 샘플링, (승리 ∧ 턴≤{settings.aiTurnWinGateMaxTurns}) 비율 ≥ {settings.aiTurnWinGateRequiredSuccessRatio:P0}인 가장 낮은 L.");
                    if (settings.aiTurnWinGateReplacesPoolRecommendation)
                        sb.AppendLine("  병합(턴 ON): max(수동 게이트, 턴 기준) — 풀 스탯 추정은 이 모드에서 생략.");
                    else
                        sb.AppendLine("  병합(턴 ON): max(수동, 풀 스탯 추정, 턴 기준).");
                }
                else if (settings.aiFloor10ClearMinLevelGateEnabled)
                {
                    sb.AppendLine($"  다음 층 요구 레벨 — **{settings.floorCount}층 진입** 시 추가: 전체 {settings.floorCount}층을 사망 없이 통과할 확률이 ≥{settings.aiFloor10ClearRequiredSuccessRatio:P0}인 최소 L(프로브 {settings.aiFloor10ClearProbesPerLevel}회/레벨, 상한 L{settings.aiFloor10ClearMaxSearchLevel}). 턴 승리 게이트는 OFF.");
                    if (settings.aiMergeMonsterPoolRecommendedLevel && monsterPool != null)
                        sb.AppendLine("  병합: max(수동, 풀 추정, 10층전체클리어프로브) — 턴 기준 미사용.");
                    else
                        sb.AppendLine("  병합: max(수동, 10층전체클리어프로브) — 풀 병합·턴 기준 미사용.");
                }
                else
                {
                    sb.AppendLine("  다음 층 요구 레벨 — 턴 승리·10층 전체 프로브 모두 OFF: 수동 게이트 + (옵션)풀 추정만.");
                    if (settings.aiMergeMonsterPoolRecommendedLevel && monsterPool != null)
                        sb.AppendLine("  병합: max(수동, 풀 스탯 추정).");
                    else
                        sb.AppendLine("  병합: 수동 게이트만(풀 병합 OFF).");
                }
                if (settings.floorLevelGates != null && settings.floorLevelGates.Count > 0)
                {
                    var gs = new System.Text.StringBuilder();
                    for (int gi = 0; gi < settings.floorLevelGates.Count; gi++)
                    {
                        var g = settings.floorLevelGates[gi];
                        if (g == null) continue;
                        if (gs.Length > 0) gs.Append(", ");
                        gs.Append($"{g.floor}층≥Lv{g.minLevelToEnter}");
                    }
                    if (gs.Length > 0)
                        sb.AppendLine("  수동 층별 최소 진입 레벨: " + gs);
                }
                if (monsterPool != null)
                {
                    var es = new System.Text.StringBuilder();
                    int shown = 0;
                    for (int f = 2; f <= settings.floorCount && shown < 12; f++)
                    {
                        int eff = GetAiNextFloorRequiredLevel(f);
                        int gateOnly = settings.GetMinLevelToEnterFloor(f);
                        int poolOnly = monsterPool.GetRecommendedMinLevelToEnterFloor(f);
                        int turnOnly = settings.aiTurnWinGateEnabled
                            ? (_aiTurnWinMinLevelCache.TryGetValue(f, out int t) ? t : ComputeMinPlayerLevelForNextFloorTurnWin(f))
                            : -1;
                        int floor10Only = settings.aiFloor10ClearMinLevelGateEnabled && f == settings.floorCount
                            ? (_floor10ClearMinCached ? _floor10ClearMinLevel : GetCachedFloor10ClearMinLevel())
                            : -1;
                        if (es.Length > 0) es.Append(", ");
                        es.Append($"{f}층→Lv{eff}");
                        if (turnOnly >= 0)
                        {
                            es.Append($"(수동{gateOnly}/풀추정{poolOnly}/턴기준{turnOnly})");
                        }
                        else if (floor10Only >= 0)
                        {
                            es.Append($"(수동{gateOnly}/풀추정{poolOnly}/10층전체{floor10Only})");
                        }
                        else if (eff != gateOnly || eff != poolOnly)
                        {
                            string manual = settings.HasMinLevelGateRowForFloor(f)
                                ? $"수동Lv{gateOnly}"
                                : "수동없음";
                            es.Append($"({manual}/풀추정{poolOnly})");
                        }
                        shown++;
                    }
                    if (es.Length > 0)
                        sb.AppendLine("  층별 유효 진입 레벨(샘플): " + es);
                }
            }
            AppendDungeonSimLevelStatTable(sb);
            sb.AppendLine();
            sb.AppendLine($"전체 클리어(모든 층): {allFloorsCleared}/{iterations} ({100f * allFloorsCleared / iterations:F1}%)");
            AppendFullClearCompanionSummary(sb, allFloorsCleared, equipHist, floorRows, settings.floorCount);
            sb.AppendLine($"사망 회차: {deaths}/{iterations} ({100f * deaths / iterations:F1}%)");
            if (battles > 0)
                sb.AppendLine($"전투 승률(전체): {battleWins}/{battles} ({100.0 * battleWins / battles:F1}%)");
            else
                sb.AppendLine("전투 승률(전체): N/A (전투 0회)");
            if (floorRows != null && battles > 0)
            {
                long sumEnemySlots = 0;
                int globalMaxSingle = 0;
                for (int ri = 0; ri < floorRows.Count; ri++)
                {
                    sumEnemySlots += floorRows[ri].SumEnemyMonstersInBattlesThisFloor;
                    int mx = floorRows[ri].MaxEnemyMonstersSingleBattleThisFloor;
                    if (mx > globalMaxSingle) globalMaxSingle = mx;
                }

                sb.AppendLine(
                    $"적 몬스터 등장 합계(실제 전투에 들어간 적 유닛 수, 전 판·전 회차 합산): {sumEnemySlots}마리 — " +
                    $"DungeonSimMonsterPool 층별 partySizeMin~Max(각 1~4)로 한 판의 마리수를 정한 뒤, 그만큼 가중치 추첨으로 채웁니다. " +
                    $"전체 {battles}판 기준 평균 {sumEnemySlots / (double)battles:F2}마리/판, 이번 실행에서 한 판 최대 {globalMaxSingle}마리.");
            }
            {
                long winTurnSum = 0;
                long winBattleCount = 0;
                if (floorRows != null)
                {
                    for (int ri = 0; ri < floorRows.Count; ri++)
                    {
                        var r = floorRows[ri];
                        if (r.BattlesWon <= 0) continue;
                        winTurnSum += r.WinBattleTurnsSum;
                        winBattleCount += r.BattlesWon;
                    }
                }
                if (winBattleCount > 0)
                    sb.AppendLine($"승리 전투당 평균 소모 턴(층행·전투 가중): {winTurnSum / (double)winBattleCount:F2} (승리 {winBattleCount}회, 턴 합 {winTurnSum})");
                else
                    sb.AppendLine("승리 전투당 평균 소모 턴: N/A (승리 전투 없음)");
            }
            sb.AppendLine($"평균 전투 수/회차: {(iterations > 0 ? (double)battles / iterations : 0):F2}");
            sb.AppendLine($"평균 도망 수/회차: {(iterations > 0 ? (double)battleFlees / iterations : 0):F2}  (HP 격차 ≥ {(int)(0.7f * 100)}%p, 층당 최대 {settings.maxFleesPerFloor}회)");
            sb.AppendLine($"평균 약초 사용/회차: {(iterations > 0 ? (double)medicinalHerbs / iterations : 0):F2}");
            sb.AppendLine($"평균 포션 사용/회차: {(iterations > 0 ? (double)potions / iterations : 0):F2}");
            sb.AppendLine($"평균 새벽의 잔 사용/회차: {(iterations > 0 ? (double)dawnChalice / iterations : 0):F2}");
            sb.AppendLine();
            sb.AppendLine("--- 도달 층 분포 (클리어한 층 수 i → 표시는 ‘그다음 층’: i=0이면 1층에서 종료) ---");
            for (int i = 0; i <= settings.floorCount; i++)
            {
                if (reached[i] == 0) continue;
                string label = i >= settings.floorCount
                    ? $"{settings.floorCount}층 전체 클리어"
                    : $"{i + 1}층 도달";
                sb.AppendLine($"  {label}: {reached[i]} ({100f * reached[i] / iterations:F1}%)");
            }
            sb.AppendLine();
            AppendEquipHistogramSections(sb, equipHist, settings);
            AppendCompanionHistogramSections(sb, equipHist, floorRows, settings.floorCount);
            AppendGlobalSkillActivationSummary(sb, floorRows);
            AppendDiagnostics(sb, floorRows);
            sb.AppendLine();
            sb.AppendLine("CSV: 동일 경로 stem에 _runs.csv, _floors.csv, _battles.csv, _party.csv, _recruits.csv 가 생성됩니다.");
            sb.AppendLine();
            sb.AppendLine("[Phase 1] 레벨업: HP/MP = 직업 hpPerLevel/mpPerLevel(없으면 Settings) + 종의 기억 HP/Lv·MP/Lv 합(반올림). 스탯 = 직업+기억 가중 1회 추첨(+1) + DEF/MAG면 MaxHP/MaxMP +3 + 시뮬 자유 1스탯(균등). 다음 레벨 EXP는 PlayerExpProgression 표(인게임과 동일).");

            return sb.ToString();
        }

        private static void AppendEquipHistogramSections(StringBuilder sb, SimEquipHistograms equipHist, DungeonSimSettings simSettings)
        {
            if (equipHist == null) return;
            DungeonSimAiEquipment.EnsureCrudePoolLoaded();
            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("=== 던전 시뮬 — Crude 장비(AI) ===");
            sb.AppendLine($"Resources.LoadAll 경로: \"{DungeonSimAiEquipment.CrudeResourcesPath}\" (장비 타입·방패=Hand+blockData 기준 분류)");
            sb.AppendLine("규칙: 양손 1칸만 / 한손×2 또는 한손+방패(총 2칸) / 방어구 0~1 / 악세 0~2. 마을(1층 입장·승리 후·AI 풀충전)마다 현재 층·다음 층 위협치로 점수 최대 조합 선택.");
            if (simSettings != null && simSettings.aiEquipAppendExplainSampleToSummary)
            {
                sb.AppendLine();
                sb.AppendLine("── 장비 점수(설명) — 규칙 기반, ‘인터뷰’ 대신 아래 수식·샘플로 검증 ──");
                sb.AppendLine(DungeonSimAiEquipment.DescribeScoringModelForSummary());
                if (DungeonSimAiEquipment.TryGetExplainSamples(out string first, out string last))
                {
                    sb.AppendLine("이번 실행 샘플(전 회차·전 마을 방문 합산 중 첫 1회 / 마지막 1회):");
                    sb.AppendLine("  첫 선택: " + (string.IsNullOrEmpty(first) ? "(마을 장비 선택 없음)" : first));
                    sb.AppendLine("  마지막 선택: " + (string.IsNullOrEmpty(last) ? "(마을 장비 선택 없음)" : last));
                }
                else
                {
                    sb.AppendLine("이번 실행 샘플: (마을 장비 선택 호출 없음)");
                }
            }
            sb.AppendLine();

            void AppendSortedDict(string title, Dictionary<string, int> dict)
            {
                sb.AppendLine(title);
                if (dict == null || dict.Count == 0)
                {
                    sb.AppendLine("  (없음)");
                    sb.AppendLine();
                    return;
                }
                var list = new List<KeyValuePair<string, int>>(dict);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < list.Count; i++)
                    sb.AppendLine($"  {list[i].Value,5}회 | {list[i].Key}");
                sb.AppendLine();
            }

            AppendSortedDict("① 사망 시 착용했던 장비(회차 종료 시 사망 1회당 집계)", equipHist.Death);
            AppendSortedDict("② 마을 방문 시 AI가 선택한 장비(선택 1회당 집계)", equipHist.AiPick);
        }

        /// <summary>전체 클리어 직후 요약에 붙입니다. 클리어 0회일 때는 CSV에서 조합을 찾는 법을 안내합니다.</summary>
        private static void AppendFullClearCompanionSummary(StringBuilder sb, int totalFullClears, SimEquipHistograms equipHist, List<DungeonSimFloorLogRow> floorRows, int floorCount)
        {
            sb.AppendLine("--- 전체 클리어 시 동료 조합(누적, 몬스터 표시명을 | 로 연결) ---");
            if (totalFullClears <= 0)
            {
                sb.AppendLine($"  이번 실행에서는 전체 클리어가 없어 조합 목록이 비어 있습니다.");
                sb.AppendLine($"  CSV에서 확인: 같은 run_id 안에서 floor={floorCount} 이고 death_flag=0 인 행의 companions_joined_cumulative 가 그 회차의 최종 동료 누적입니다.");
                sb.AppendLine();
                return;
            }

            if (equipHist != null && equipHist.FullClearCompanionSig != null && equipHist.FullClearCompanionSig.Count > 0)
            {
                var agg = new List<KeyValuePair<string, int>>(equipHist.FullClearCompanionSig);
                agg.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.AppendLine($"  조합별 클리어 횟수(동일 시그니처 합산, 총 {totalFullClears}회):");
                for (int i = 0; i < agg.Count; i++)
                {
                    string label = string.IsNullOrEmpty(agg[i].Key) || agg[i].Key == "-"
                        ? "(동료 합류 없음)"
                        : agg[i].Key;
                    sb.AppendLine($"    {agg[i].Value,5}회 | {label}");
                }
            }

            if (floorRows != null && floorRows.Count > 0 && floorCount > 0)
            {
                var lastClearRowByRun = new Dictionary<int, DungeonSimFloorLogRow>();
                for (int ri = 0; ri < floorRows.Count; ri++)
                {
                    var r = floorRows[ri];
                    if (r.Floor != floorCount || r.DeathFlag != 0) continue;
                    lastClearRowByRun[r.RunId] = r;
                }

                const int maxPerRunLines = 100;
                sb.AppendLine($"  회차별 상세(run_id, seed, 최종 조합) — 총 {lastClearRowByRun.Count}회차(최대 {maxPerRunLines}행만 출력):");
                var runIds = new List<int>(lastClearRowByRun.Keys);
                runIds.Sort();
                int cap = Mathf.Min(runIds.Count, maxPerRunLines);
                for (int k = 0; k < cap; k++)
                {
                    var r = lastClearRowByRun[runIds[k]];
                    string combo = string.IsNullOrEmpty(r.CompanionsJoinedCumulative) || r.CompanionsJoinedCumulative == "-"
                        ? "(동료 합류 없음)"
                        : r.CompanionsJoinedCumulative;
                    sb.AppendLine($"    run {r.RunId,4} | seed {r.Seed} | {combo}");
                }

                if (runIds.Count > maxPerRunLines)
                    sb.AppendLine($"    … 외 {runIds.Count - maxPerRunLines}회차 생략 — 전체는 CSV에서 floor={floorCount}, death_flag=0 으로 필터하세요.");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// 각 run_id의 runs CSV 행에 기록된 <see cref="DungeonSimRunLogRow.FinalCompanionCumulative"/>를 집계합니다.
        /// 전체 클리어 여부와 무관하게, 회차 종료 시점까지 누적된 동료 표시명 조합의 빈도를 봅니다.
        /// </summary>
        private static void AppendMostCommonEndOfRunCompanionCombos(StringBuilder sb, List<DungeonSimRunLogRow> runs, int topN)
        {
            if (runs == null || runs.Count == 0) return;
            topN = Mathf.Max(5, topN);

            var counts = new Dictionary<string, int>();
            for (int i = 0; i < runs.Count; i++)
            {
                string sig = runs[i].FinalCompanionCumulative;
                if (string.IsNullOrWhiteSpace(sig) || sig == "-")
                    sig = "(동료 합류 없음)";
                if (!counts.TryGetValue(sig, out int c))
                    c = 0;
                counts[sig] = c + 1;
            }

            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            sb.AppendLine("--- 회차 종료 시점 동료 누적 조합(빈도순, *_runs.csv 의 final_companion_cumulative) ---");
            int show = Mathf.Min(topN, list.Count);
            for (int i = 0; i < show; i++)
                sb.AppendLine($"  {list[i].Value,5}회 / {list.Count}종 중 | {list[i].Key}");
            if (list.Count > topN)
                sb.AppendLine($"  … 고유 조합 {list.Count}종 중 상위 {topN}개만 표시");
            sb.AppendLine();
        }

        /// <summary><see cref="DungeonSimRecord.CompanionsJoinedCumulative"/> 문자열의 합류 성공 횟수(| 구분, 빈 토큰 무시).</summary>
        private static int CountPipeSeparatedCompanionNames(string cumulative)
        {
            if (string.IsNullOrWhiteSpace(cumulative) || cumulative == "-") return 0;
            var parts = cumulative.Split('|');
            int n = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i])) n++;
            }

            return n;
        }

        /// <summary>
        /// 회차마다 runs 행의 누적 동료 합류 횟수로, 이번 시뮬에서의 최댓값·평균을 요약합니다.
        /// </summary>
        private static void AppendCompanionJoinPerRunDepthStats(StringBuilder sb, List<DungeonSimRunLogRow> runs, int iterations)
        {
            if (runs == null || runs.Count == 0) return;

            int maxJoins = 0;
            long sumJoins = 0;
            DungeonSimRunLogRow maxRun = null;
            foreach (var run in runs)
            {
                int c = CountPipeSeparatedCompanionNames(run.FinalCompanionCumulative);
                sumJoins += c;
                if (c > maxJoins)
                {
                    maxJoins = c;
                    maxRun = run;
                }
            }

            int nRuns = runs.Count;
            double avg = nRuns > 0 ? sumJoins / (double)nRuns : 0d;

            sb.AppendLine("--- 동료 합류 성공 횟수(회차 누적, 전투 파티 반영) ---");
            sb.AppendLine($"  이번 실행 회차 수: {nRuns} (설정 iterations={iterations}).");
            sb.AppendLine($"  한 회차가 끝날 때까지 누적된 합류 성공 최대: {maxJoins}회(몬스터 동료 상한 3).");
            if (maxJoins > 0 && nRuns > 0 && maxRun != null)
            {
                string tail = string.IsNullOrEmpty(maxRun.FinalCompanionCumulative) || maxRun.FinalCompanionCumulative == "-"
                    ? "-"
                    : maxRun.FinalCompanionCumulative;
                if (tail.Length > 100)
                    tail = tail.Substring(0, 97) + "...";
                sb.AppendLine($"    (해당 예시) run {maxRun.RunId} | seed {maxRun.Seed} | …{tail}");
            }

            sb.AppendLine($"  회차당 평균 합류 성공 횟수: {avg:F2}회.");
            sb.AppendLine();
        }

        private static void AppendCompanionHistogramSections(StringBuilder sb, SimEquipHistograms equipHist, List<DungeonSimFloorLogRow> floorRows, int floorCount)
        {
            if (equipHist == null) return;
            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("=== 던전 시뮬 — 동료화 로그(합류 이력) ===");
            sb.AppendLine("CSV(*_floors.csv): companions_joined_cumulative = 해당 층 종료 시점까지 합류 성공한 몬스터 표시명(순서, | 구분). notes에 companion_join:이름(해당 층) 병기.");

            void AppendSortedDict(string title, Dictionary<string, int> dict)
            {
                sb.AppendLine(title);
                if (dict == null || dict.Count == 0)
                {
                    sb.AppendLine("  (없음)");
                    sb.AppendLine();
                    return;
                }

                var list = new List<KeyValuePair<string, int>>(dict);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < list.Count; i++)
                    sb.AppendLine($"  {list[i].Value,5}회 | {list[i].Key}");
                sb.AppendLine();
            }

            AppendSortedDict("① 동료 합류 성공(몬스터 표시명별, 승리 후 시도 1회 중 성공)", equipHist.CompanionJoinByName);
            AppendSortedDict("② 전체 클리어(사망 없음) 회차 종료 시 동료 조합(누적 파이프 시그니처)", equipHist.FullClearCompanionSig);

            if (floorRows != null && floorRows.Count > 0 && floorCount > 0)
            {
                var lastClearRowByRun = new Dictionary<int, DungeonSimFloorLogRow>();
                for (int ri = 0; ri < floorRows.Count; ri++)
                {
                    var r = floorRows[ri];
                    if (r.Floor != floorCount || r.DeathFlag != 0) continue;
                    lastClearRowByRun[r.RunId] = r;
                }

                sb.AppendLine("③ 전체 클리어 회차 목록(run_id → seed → 최종 조합) — 요약 상단 블록과 동일 규칙(최대 50행)");
                if (lastClearRowByRun.Count == 0)
                {
                    sb.AppendLine("  (해당 없음)");
                }
                else
                {
                    var runIds = new List<int>(lastClearRowByRun.Keys);
                    runIds.Sort();
                    int cap = Mathf.Min(runIds.Count, 50);
                    for (int k = 0; k < cap; k++)
                    {
                        var r = lastClearRowByRun[runIds[k]];
                        string combo = string.IsNullOrEmpty(r.CompanionsJoinedCumulative) || r.CompanionsJoinedCumulative == "-"
                            ? "(동료 합류 없음)"
                            : r.CompanionsJoinedCumulative;
                        sb.AppendLine($"  run {r.RunId,4} | seed {r.Seed} | {combo}");
                    }

                    if (runIds.Count > 50)
                        sb.AppendLine($"  … 외 {runIds.Count - 50}행 생략(요약 상단 또는 CSV)");
                }

                sb.AppendLine();
            }
        }

        /// <summary>모든 층 CSV 행의 <see cref="DungeonSimFloorLogRow.SkillActivationBreakdown"/>을 합산해 요약에 붙입니다.</summary>
        private static void AppendGlobalSkillActivationSummary(StringBuilder sb, List<DungeonSimFloorLogRow> floorRows)
        {
            if (floorRows == null || floorRows.Count == 0) return;
            var g = new Dictionary<string, long>();
            for (int i = 0; i < floorRows.Count; i++)
                SimSkillActivationLog.MergeEncoded(g, floorRows[i].SkillActivationBreakdown);
            sb.AppendLine("─────────────────────────────────────────");
            SimSkillActivationLog.AppendSortedHumanReport(
                sb,
                g,
                "=== 전체 시뮬 — 스킬 액티브 시전 누적(모든 층 CSV 행 합산, skillID 우선) ===",
                maxLines: 50);
        }

        /// <summary>
        /// 요약 텍스트에 던전 시뮬 <see cref="ApplyLevelUp"/> 규칙으로 올린 레벨별 파티 스탯 표를 붙입니다.
        /// 성장 RNG는 <see cref="DungeonSimSettings.baseSeed"/> 한 가지로 고정해, Lv1→LvN이 한 경로로 쌓인 스냅샷입니다.
        /// </summary>
        private void AppendDungeonSimLevelStatTable(StringBuilder sb)
        {
            if (allyRoster == null || settings == null) return;

            int growthSeed = settings.baseSeed == int.MinValue ? 1 : settings.baseSeed;
            int capSearch = Mathf.Max(settings.aiTurnWinGateMaxSearchLevel, settings.aiFloor10ClearMaxSearchLevel);
            int upper = Mathf.Max(50, capSearch);
            int maxLv = Mathf.Clamp(
                Mathf.Max(12, Mathf.Max(settings.floorCount + 8, capSearch)),
                12, upper);

            sb.AppendLine("=== 던전 시뮬 — 레벨별 파티 스탯 (ApplyLevelUp, 성장 시드=baseSeed 고정) ===");
            sb.AppendLine("※ 로스터(아군 SO)만 — 던전 중 합류한 몬스터 동료(CompanionSO)는 이 표에 포함하지 않습니다. 장비(Crude AI 마을 착용)도 미반영. Sword Lore T1·기본 검술 보너스는 포함. 레벨마다 CreateSimPlayerAtLevel 스냅샷.");
            sb.AppendLine();
            sb.AppendLine("Lv | ΣMaxHP ΣMaxMP | ΣATK ΣDEF ΣMAG ΣAGI ΣLUK | 슬롯별(이름:ATK/DEF/MAG/AGI/LUK MaxHP)");
            for (int L = 1; L <= maxLv; L++)
            {
                var p = CreateSimPlayerAtLevel(allyRoster, settings, L, growthSeed);
                if (p == null || !p.AnyAlive())
                {
                    sb.AppendLine($"  {L,2} | (파티 생성 실패)");
                    continue;
                }

                int sHp = 0, sMp = 0, sAtk = 0, sDef = 0, sMag = 0, sAgi = 0, sLuk = 0;
                foreach (var u in p.Units)
                {
                    if (u == null || !u.IsAlive) continue;
                    sHp += u.MaxHP;
                    sMp += u.MaxMP;
                    sAtk += u.Attack;
                    sDef += u.Defense;
                    sMag += u.Magic;
                    sAgi += u.Agility;
                    sLuk += u.Luck;
                }

                var slotSb = new StringBuilder();
                for (int i = 0; i < p.Units.Count; i++)
                {
                    var u = p.Units[i];
                    if (u == null || !u.IsAlive) continue;
                    string nm = string.IsNullOrEmpty(u.DisplayName) ? $"S{i + 1}" : u.DisplayName;
                    if (nm.Length > 10) nm = nm.Substring(0, 10);
                    if (slotSb.Length > 0) slotSb.Append(" | ");
                    slotSb.Append($"{nm}:{u.Attack}/{u.Defense}/{u.Magic}/{u.Agility}/{u.Luck} HP{u.MaxHP}");
                }

                sb.AppendLine(
                    $"  {L,2} | {sHp,5} {sMp,5} | {sAtk,4} {sDef,4} {sMag,4} {sAgi,4} {sLuk,4} | {slotSb}");
            }
        }

        // ---------------------------------------------------------------
        // 진단 — 어디서 막히는가
        //   ① 사망 층 분포 (death_flag=1)
        //   ② 층별 진단표 (도달 / 클리어 / 사망 / 평균 전투·승률·HP·포션·잔·레벨)
        //   ③ 회복 자원 진단 (포션이 바닥난 회차의 평균 층, 새벽의 잔 사용률 등)
        //   ④ 레벨업 진단 (사망 시 평균 레벨, 첫 레벨업 평균 층)
        //   ⑤ 사망 원인 분류 (1전투 즉사 / 회복 자원 보유한 채 죽음 / 자원 고갈 사망)
        // ---------------------------------------------------------------
        private void AppendDiagnostics(StringBuilder sb, List<DungeonSimFloorLogRow> floorRows)
        {
            if (floorRows == null || floorRows.Count == 0) return;

            int floorCap = Mathf.Max(1, settings.floorCount);

            // 한 회차의 마지막 행만 모음 — CSV에 기록된 순서대로, 같은 run_id는 항상 덮어써서 마지막 행이 남게 함
            // (AI 파밍으로 같은 층 번호가 연속될 때 r.Floor > prev.Floor 비교만 하면 이전 클리어 행이 남는 버그가 있음)
            var lastRowByRun = new Dictionary<int, DungeonSimFloorLogRow>();
            foreach (var r in floorRows)
                lastRowByRun[r.RunId] = r;

            // 층별 누적기
            int[] reachedCnt = new int[floorCap + 2];
            int[] clearedCnt = new int[floorCap + 2];
            int[] wipedCnt = new int[floorCap + 2];
            long[] battlesSum = new long[floorCap + 2];
            long[] winsSum = new long[floorCap + 2];
            long[] fledSum = new long[floorCap + 2];
            long[] hpBeforeSum = new long[floorCap + 2];
            long[] hpAfterSum = new long[floorCap + 2];
            long[] potionUseSum = new long[floorCap + 2];
            long[] herbUseSum = new long[floorCap + 2];
            long[] chaliceUseSum = new long[floorCap + 2];
            long[] potionAfterSum = new long[floorCap + 2];
            long[] herbAfterSum = new long[floorCap + 2];
            long[] chaliceAfterSum = new long[floorCap + 2];
            long[] xpSum = new long[floorCap + 2];
            long[] levelBeforeSum = new long[floorCap + 2];
            long[] levelAfterSum = new long[floorCap + 2];
            long[] stepsSum = new long[floorCap + 2];
            long[] damageTakenSum = new long[floorCap + 2];
            long[] winTurnsSumByFloor = new long[floorCap + 2];

            foreach (var r in floorRows)
            {
                int f = Mathf.Clamp(r.Floor, 1, floorCap);
                reachedCnt[f]++;
                if (r.ClearFlag != 0) clearedCnt[f]++;
                if (r.DeathFlag != 0) wipedCnt[f]++;
                battlesSum[f] += r.Battles;
                winsSum[f] += r.BattlesWon;
                fledSum[f] += r.BattlesFled;
                hpBeforeSum[f] += r.TotalHpEnter;
                hpAfterSum[f] += r.TotalHpExit;
                potionUseSum[f] += r.PotionUseDelta;
                herbUseSum[f] += r.HerbUseDelta;
                chaliceUseSum[f] += r.ChaliceUseDelta;
                potionAfterSum[f] += r.PotionOnExit;
                herbAfterSum[f] += r.HerbOnExit;
                chaliceAfterSum[f] += r.ChaliceOnExit;
                xpSum[f] += r.XpGained;
                levelBeforeSum[f] += r.LevelOnEnter;
                levelAfterSum[f] += r.LevelOnExit;
                stepsSum[f] += r.StepsMoved;
                damageTakenSum[f] += r.DamageTakenFloor;
                winTurnsSumByFloor[f] += r.WinBattleTurnsSum;
            }

            int totalRuns = lastRowByRun.Count;

            long nFullClear = 0, nDeathEnd = 0, nOtherEnd = 0;
            long sumLvClear = 0, sumHerbClear = 0, sumPotClear = 0, sumChalClear = 0, sumTownClear = 0;
            long sumLvDeath = 0, sumHerbDeath = 0, sumPotDeath = 0, sumChalDeath = 0, sumTownDeath = 0;
            foreach (var kv in lastRowByRun)
            {
                var r = kv.Value;
                bool fullClear = r.DeathFlag == 0 && r.Floor >= floorCap;
                if (fullClear)
                {
                    nFullClear++;
                    sumLvClear += r.LevelOnExit;
                    sumHerbClear += r.HerbOnExit;
                    sumPotClear += r.PotionOnExit;
                    sumChalClear += r.ChaliceOnExit;
                    sumTownClear += r.Floor1TownUsesCumulative;
                }
                else if (r.DeathFlag != 0)
                {
                    nDeathEnd++;
                    sumLvDeath += r.LevelOnExit;
                    sumHerbDeath += r.HerbOnExit;
                    sumPotDeath += r.PotionOnExit;
                    sumChalDeath += r.ChaliceOnExit;
                    sumTownDeath += r.Floor1TownUsesCumulative;
                }
                else
                    nOtherEnd++;
            }

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("=== 회차 종료 시점 (회차당 마지막 층 CSV 행 기준) ===");
            sb.AppendLine($"  전체 회차: {totalRuns}");
            if (nFullClear > 0)
            {
                sb.AppendLine(
                    $"  {floorCap}층 전체 클리어(사망 없음): {nFullClear}회 — 평균 레벨 {sumLvClear / (double)nFullClear:F2}, " +
                    $"남은 약초 {sumHerbClear / (double)nFullClear:F2}, 포션 {sumPotClear / (double)nFullClear:F2}, 새벽의 잔 {sumChalClear / (double)nFullClear:F2}, " +
                    $"1층 마을 누적 {sumTownClear / (double)nFullClear:F2}회");
            }
            if (nDeathEnd > 0)
            {
                sb.AppendLine(
                    $"  사망으로 종료: {nDeathEnd}회 — 평균 레벨 {sumLvDeath / (double)nDeathEnd:F2}, " +
                    $"남은 약초 {sumHerbDeath / (double)nDeathEnd:F2}, 포션 {sumPotDeath / (double)nDeathEnd:F2}, 새벽의 잔 {sumChalDeath / (double)nDeathEnd:F2}, " +
                    $"1층 마을 누적 {sumTownDeath / (double)nDeathEnd:F2}회");
            }
            if (nOtherEnd > 0)
                sb.AppendLine($"  기타 종료(사망 아님·{floorCap}층 미만 등): {nOtherEnd}회 — 시뮬 예외 시 CSV로 확인.");

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("=== 진단 — 어디서 막히는가 ===");

            // ① 사망 층 분포
            int[] deathFloor = new int[floorCap + 2];
            int wipedTotal = 0;
            foreach (var kv in lastRowByRun)
            {
                var r = kv.Value;
                if (r.DeathFlag != 0)
                {
                    int f = Mathf.Clamp(r.Floor, 1, floorCap);
                    deathFloor[f]++;
                    wipedTotal++;
                }
            }
            sb.AppendLine();
            sb.AppendLine("① 사망 층 분포 (회차의 마지막 층이 death=1인 케이스)");
            if (wipedTotal == 0)
            {
                sb.AppendLine("  (사망 회차 없음)");
            }
            else
            {
                int firstWallFloor = -1;
                for (int f = 1; f <= floorCap; f++)
                {
                    if (deathFloor[f] == 0) continue;
                    if (firstWallFloor < 0) firstWallFloor = f;
                    sb.AppendLine($"  {f}층 사망: {deathFloor[f]} ({100.0 * deathFloor[f] / totalRuns:F1}%)");
                }
                if (firstWallFloor > 0)
                {
                    sb.AppendLine($"  ▶ 번호상 가장 이른 마지막 사망 층: {firstWallFloor}층 (사망이 1회 이상 기록된 층 중 최소 번호 — 통과 병목과는 다를 수 있음).");
                    int peakF = -1;
                    int peakCnt = 0;
                    for (int f = 1; f <= floorCap; f++)
                    {
                        if (deathFloor[f] > peakCnt)
                        {
                            peakCnt = deathFloor[f];
                            peakF = f;
                        }
                    }
                    if (peakCnt > 0 && peakF > 0)
                        sb.AppendLine($"  ▶ 사망 종료가 가장 많이 집계된 층: {peakF}층 ({peakCnt}회, 전체 사망 대비 {100.0 * peakCnt / wipedTotal:F1}%).");
                }
            }

            // ② 층별 진단표
            sb.AppendLine();
            sb.AppendLine("② 층별 진단표 (도달 회차 기준 평균)");
            sb.AppendLine("  층 |  도달 | 클리어%  | 사망% | 전투 | 승률 | 도망 | 승리평균턴 | HP진입 → HP종료(손실) | 약초사용/남음 | 포션사용/남음 | 잔사용/남음 | 레벨진입 → 종료 | XP");
            for (int f = 1; f <= floorCap; f++)
            {
                int reachedF = reachedCnt[f];
                if (reachedF == 0) continue;
                double clearPct = 100.0 * clearedCnt[f] / reachedF;
                double diePct = 100.0 * wipedCnt[f] / reachedF;
                double avgBattles = (double)battlesSum[f] / reachedF;
                double avgWinRate = battlesSum[f] > 0 ? 100.0 * winsSum[f] / battlesSum[f] : 0;
                double avgFlee = (double)fledSum[f] / reachedF;
                double avgWinTurns = winsSum[f] > 0 ? (double)winTurnsSumByFloor[f] / winsSum[f] : 0;
                double avgHpBefore = (double)hpBeforeSum[f] / reachedF;
                double avgHpAfter = (double)hpAfterSum[f] / reachedF;
                double avgHerbUse = (double)herbUseSum[f] / reachedF;
                double avgHerbAfter = (double)herbAfterSum[f] / reachedF;
                double avgPotionUse = (double)potionUseSum[f] / reachedF;
                double avgPotionAfter = (double)potionAfterSum[f] / reachedF;
                double avgChaliceUse = (double)chaliceUseSum[f] / reachedF;
                double avgChaliceAfter = (double)chaliceAfterSum[f] / reachedF;
                double avgLvBefore = (double)levelBeforeSum[f] / reachedF;
                double avgLvAfter = (double)levelAfterSum[f] / reachedF;
                double avgXp = (double)xpSum[f] / reachedF;
                sb.AppendLine(
                    $"  {f,2} | {reachedF,5} | {clearPct,6:F1}% | {diePct,5:F1}% | {avgBattles,4:F2} | {avgWinRate,4:F1}% | {avgFlee,4:F2} | {avgWinTurns,5:F2} | " +
                    $"{avgHpBefore,5:F1} → {avgHpAfter,5:F1} ({avgHpBefore - avgHpAfter,5:F1}) | " +
                    $"{avgHerbUse,4:F2}/{avgHerbAfter,4:F2} | {avgPotionUse,4:F2}/{avgPotionAfter,4:F2} | {avgChaliceUse,4:F2}/{avgChaliceAfter,4:F2} | " +
                    $"{avgLvBefore,4:F2} → {avgLvAfter,4:F2} | {avgXp,5:F1}");
            }

            // ③ 회복 자원 진단
            sb.AppendLine();
            sb.AppendLine("③ 회복 자원 진단");
            int runsPotionExhausted = 0;
            int runsChaliceExhausted = 0;
            long sumPotionExhaustionFloor = 0;
            long sumChaliceExhaustionFloor = 0;
            var perRunRecords = new Dictionary<int, List<DungeonSimFloorLogRow>>();
            foreach (var r in floorRows)
            {
                if (!perRunRecords.TryGetValue(r.RunId, out var list))
                {
                    list = new List<DungeonSimFloorLogRow>();
                    perRunRecords[r.RunId] = list;
                }
                list.Add(r);
            }
            foreach (var kv in perRunRecords)
            {
                var list = kv.Value;
                list.Sort((a, b) => a.Floor.CompareTo(b.Floor));
                int firstPotionEmpty = -1;
                int firstChaliceEmpty = -1;
                foreach (var r in list)
                {
                    if (firstPotionEmpty < 0 && r.PotionOnExit == 0) firstPotionEmpty = r.Floor;
                    if (firstChaliceEmpty < 0 && r.ChaliceOnExit == 0 && settings.startingDawnChaliceCharges > 0)
                        firstChaliceEmpty = r.Floor;
                }
                if (firstPotionEmpty > 0) { runsPotionExhausted++; sumPotionExhaustionFloor += firstPotionEmpty; }
                if (firstChaliceEmpty > 0) { runsChaliceExhausted++; sumChaliceExhaustionFloor += firstChaliceEmpty; }
            }
            if (settings.startingHpPotionCount > 0)
            {
                double avgFloor = runsPotionExhausted > 0 ? (double)sumPotionExhaustionFloor / runsPotionExhausted : 0;
                sb.AppendLine($"  포션이 0이 된 회차: {runsPotionExhausted}/{totalRuns} ({100.0 * runsPotionExhausted / totalRuns:F1}%), 평균 {avgFloor:F1}층에서 소진.");
                if (runsPotionExhausted == 0)
                    sb.AppendLine($"  ▶ 포션을 다 쓰지 못한 채 끝났습니다 — 회복 트리거(HP<{settings.GetHealMaintainRatio():P0} 유지 목표)에 걸리기 전에 죽거나, 승/도망 없이 끝남.");
            }
            if (settings.startingDawnChaliceCharges > 0)
            {
                double avgFloor = runsChaliceExhausted > 0 ? (double)sumChaliceExhaustionFloor / runsChaliceExhausted : 0;
                sb.AppendLine($"  새벽의 잔이 0이 된 회차: {runsChaliceExhausted}/{totalRuns} ({100.0 * runsChaliceExhausted / totalRuns:F1}%), 평균 {avgFloor:F1}층에서 소진.");
                if (runsChaliceExhausted == 0)
                    sb.AppendLine($"  ▶ 잔이 거의 안 쓰임 — 임계({settings.dawnChaliceHpThreshold:P0}) 도달 전 사망, 또는 포션 우선 정책(useDawnChaliceOnlyWhenPotionEmpty)에 막힘.");
            }

            // ④ 레벨업 진단
            sb.AppendLine();
            sb.AppendLine("④ 레벨업 진단");
            long sumLevelAtDeath = 0;
            int countDeathLevel = 0;
            int firstLevelUpFloorSum = 0;
            int countFirstLevelUp = 0;
            foreach (var kv in perRunRecords)
            {
                var list = kv.Value;
                bool died = false;
                int deathLevel = 1;
                int firstLevelUpFloor = -1;
                foreach (var r in list)
                {
                    if (r.LevelOnExit > r.LevelOnEnter && firstLevelUpFloor < 0)
                        firstLevelUpFloor = r.Floor;
                    if (r.DeathFlag != 0) { died = true; deathLevel = r.LevelOnExit; }
                }
                if (died) { sumLevelAtDeath += deathLevel; countDeathLevel++; }
                if (firstLevelUpFloor > 0) { firstLevelUpFloorSum += firstLevelUpFloor; countFirstLevelUp++; }
            }
            double avgDeathLv = countDeathLevel > 0 ? (double)sumLevelAtDeath / countDeathLevel : 0;
            double avgFirstLevelUpFloor = countFirstLevelUp > 0 ? (double)firstLevelUpFloorSum / countFirstLevelUp : 0;
            sb.AppendLine($"  평균 사망 시 레벨: {avgDeathLv:F2}  (시작 레벨: {settings.startingLevel})");
            if (countFirstLevelUp > 0)
                sb.AppendLine($"  첫 레벨업 평균 발생 층: {avgFirstLevelUpFloor:F2}층 ({countFirstLevelUp}/{totalRuns} 회차에서 발생)");
            else
                sb.AppendLine("  첫 레벨업 발생 회차 없음 — XP가 누적되지 않거나 1전투 즉사로 보상을 못 받음.");

            // ⑤ 사망 원인 분류 (마지막 행 기준)
            sb.AppendLine();
            sb.AppendLine("⑤ 사망 원인 분류 (사망 회차의 마지막 행 기준)");
            int sudden = 0;     // 1전투 만에 죽음 (배틀=1)
            int afterMany = 0;  // 그 층에서 여러 전투 치르고 사망
            int withPotion = 0; // 사망 시 포션 남음
            int noPotion = 0;   // 사망 시 포션 0
            int withChalice = 0;
            int noChalice = 0;
            int deathLastVs1 = 0, deathLastVs2 = 0, deathLastVs3Plus = 0, deathLastVsUnknown = 0;
            foreach (var kv in lastRowByRun)
            {
                var r = kv.Value;
                if (r.DeathFlag == 0) continue;
                if (r.Battles <= 1) sudden++;
                else afterMany++;
                if (r.PotionOnExit > 0) withPotion++; else noPotion++;
                if (r.ChaliceOnExit > 0) withChalice++; else noChalice++;
                int ec = r.LastBattleEnemyCount;
                if (ec <= 0) deathLastVsUnknown++;
                else if (ec == 1) deathLastVs1++;
                else if (ec == 2) deathLastVs2++;
                else deathLastVs3Plus++;
            }
            if (wipedTotal == 0)
            {
                sb.AppendLine("  (사망 회차 없음 — 분류 생략)");
            }
            else
            {
                sb.AppendLine($"  1전투 즉사 (그 층 첫 전투에서 패배): {sudden} ({100.0 * sudden / wipedTotal:F1}%)");
                sb.AppendLine($"  소모전 끝 사망 (그 층에서 여러 번 싸운 뒤): {afterMany} ({100.0 * afterMany / wipedTotal:F1}%)");
                if (settings.startingHpPotionCount > 0)
                {
                    sb.AppendLine($"  포션 남은 채 사망: {withPotion} ({100.0 * withPotion / wipedTotal:F1}%) — 회복 자원이 살릴 수 있는 속도가 아님");
                    sb.AppendLine($"  포션 다 쓴 뒤 사망: {noPotion} ({100.0 * noPotion / wipedTotal:F1}%) — 포션 자원 고갈(잔은 별도 줄)");
                }
                else
                {
                    sb.AppendLine($"  HP포션: 시뮬 시작 {settings.startingHpPotionCount}개 — 사망 시점 포션 잔량 분류는 적용되지 않음(항상 0).");
                }
                if (settings.startingDawnChaliceCharges > 0)
                {
                    sb.AppendLine($"  잔 남은 채 사망: {withChalice} ({100.0 * withChalice / wipedTotal:F1}%) — 잔 발동 조건 미달");
                    sb.AppendLine($"  잔 다 쓴 뒤 사망: {noChalice} ({100.0 * noChalice / wipedTotal:F1}%)");
                }
                sb.AppendLine($"  마지막 전투 적 수 (그 층에서 패배한 판 기준): 1마리 {deathLastVs1} ({100.0 * deathLastVs1 / wipedTotal:F1}%), 2마리 {deathLastVs2} ({100.0 * deathLastVs2 / wipedTotal:F1}%), 3마리 이상 {deathLastVs3Plus} ({100.0 * deathLastVs3Plus / wipedTotal:F1}%), 기록 없음(0) {deathLastVsUnknown} ({100.0 * deathLastVsUnknown / wipedTotal:F1}%)");
            }

            // ⑥ 핵심 결론 한 줄
            sb.AppendLine();
            sb.AppendLine("⑥ 한 줄 결론");
            if (wipedTotal == 0)
            {
                sb.AppendLine("  사망이 없습니다 — 현재 구성으로는 충분히 진행 가능. 더 어려운 풀을 시험해 보십시오.");
            }
            else
            {
                int firstWallFloor = 1;
                for (int f = 1; f <= floorCap; f++) { if (deathFloor[f] > 0) { firstWallFloor = f; break; } }
                if (settings.startingHpPotionCount <= 0)
                {
                    if (noChalice > withChalice)
                        sb.AppendLine($"  ▶ 주된 사망 양상: HP포션 없이(새벽의 잔만). 잔 충전을 소진한 뒤에도 버티지 못한 비중이 큼(잔 소진 후 사망 {noChalice}/{wipedTotal}).");
                    else if (withChalice > noChalice)
                        sb.AppendLine($"  ▶ 주된 사망 양상: HP포션 없이(새벽의 잔만). 잔이 남아도 연속 전투·턴 내 손실로 사망한 비중이 큼(잔 잔여 채 사망 {withChalice}/{wipedTotal}).");
                    else
                        sb.AppendLine($"  ▶ 주된 사망 양상: HP포션 없이(새벽의 잔만). {firstWallFloor}층 전후로 소모전·즉사가 섞임.");
                }
                else if (sudden > afterMany && withPotion > noPotion)
                    sb.AppendLine($"  ▶ 주된 사망 양상: {firstWallFloor}층에서 1전투 즉사 + 포션이 남음. 회복 자원 부족이 아니라 단위 전투력(공·방·HP·AGI)이 부족합니다.");
                else if (noPotion > withPotion)
                    sb.AppendLine($"  ▶ 주된 사망 양상: 포션을 모두 소진한 뒤에도 버티지 못함 — 회복량/시작 개수가 부족하거나 적이 너무 강함.");
                else
                    sb.AppendLine($"  ▶ 주된 사망 양상: {firstWallFloor}층의 소모전 패배. 평균 전투수와 손실량이 누적되어 죽음.");
            }
            sb.AppendLine("─────────────────────────────────────────");
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
