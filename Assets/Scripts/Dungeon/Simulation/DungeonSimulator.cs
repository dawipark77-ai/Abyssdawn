using System;
using System.Collections.Generic;
using System.IO;
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
            long totalBattleWins = 0;
            long totalBattleFlees = 0;
            long totalPotions = 0;
            long totalDawnChalice = 0;
            long totalMedicinalHerbs = 0;
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

                    var rec = RunSingleFloor(i + 1, seed, floor, player, rng, bm, skipFloorEntryEffects: false);
                    allRecords.Add(rec);
                    totalBattles += rec.Battles;
                    totalBattleWins += rec.BattlesWon;
                    totalBattleFlees += rec.BattlesFled;
                    totalPotions += rec.PotionUseCount;
                    totalDawnChalice += rec.DawnChaliceUseCount;
                    totalMedicinalHerbs += rec.MedicinalHerbUseCount;

                    if (rec.DeathFlag)
                    {
                        died = true;
                        break;
                    }

                    if (settings.aiProgressionEnabled && floor < settings.floorCount)
                    {
                        int needLv = settings.GetMinLevelToEnterFloor(floor + 1);
                        int farmPasses = 0;
                        while (player.AnyAlive()
                               && player.Level < needLv
                               && farmPasses < settings.aiMaxFarmingPassesBeforeNextFloor)
                        {
                            var farmRec = RunSingleFloor(i + 1, seed, floor, player, rng, bm, skipFloorEntryEffects: true);
                            allRecords.Add(farmRec);
                            totalBattles += farmRec.Battles;
                            totalBattleWins += farmRec.BattlesWon;
                            totalBattleFlees += farmRec.BattlesFled;
                            totalPotions += farmRec.PotionUseCount;
                            totalDawnChalice += farmRec.DawnChaliceUseCount;
                            totalMedicinalHerbs += farmRec.MedicinalHerbUseCount;
                            farmPasses++;
                            if (farmRec.DeathFlag)
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
                if (died) totalDeaths++;
                if (floorsCleared >= settings.floorCount && !died) totalClearedAllFloors++;
            }

            string csvPath = ResolveAbs(settings.csvRelativePath, true);
            string summaryPath = ResolveAbs(settings.summaryRelativePath, false);

            bool wroteToAssets = false;
            try
            {
                WriteCsv(csvPath, allRecords);
                Debug.Log($"[DungeonSim] CSV written: {csvPath}");
                if (IsInsideAssets(settings.csvRelativePath)) wroteToAssets = true;
            }
            catch (Exception e) { Debug.LogWarning($"[DungeonSim] CSV write failed: {e.Message}"); }

            string summary = BuildSummary(iterations, totalDeaths, totalClearedAllFloors, totalBattles, totalBattleWins, totalBattleFlees, totalMedicinalHerbs, totalPotions, totalDawnChalice, floorReachedCount, allRecords);
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

        // ---------------------------------------------------------------
        // 한 층 실행
        // ---------------------------------------------------------------
        private DungeonSimRecord RunSingleFloor(int runId, int seed, int floor, DungeonSimPlayer player, System.Random rng, BattleSimulator bm, bool skipFloorEntryEffects)
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
                rec.Floor1TownUsesCumulative = player.Floor1TownUsesTotal;
                rec.LastBattleEnemyCount = 0;
                return rec;
            }

            rec.FloorType = entry.kind.ToString();

            if (skipFloorEntryEffects)
                rec.Notes = string.IsNullOrEmpty(rec.Notes) ? "ai_farm" : rec.Notes + "; ai_farm";

            int fMin = Mathf.Min(settings.medicinalHerbGrantFloorsMin, settings.medicinalHerbGrantFloorsMax);
            int fMax = Mathf.Max(settings.medicinalHerbGrantFloorsMin, settings.medicinalHerbGrantFloorsMax);
            if (!skipFloorEntryEffects)
            {
                if (floor == 1 && settings.floor1TownFullRestoreEnabled)
                {
                    ApplyFloor1TownFullRestore(player, settings);
                    rec.HpBefore = player.GetTotalCurrentHP();
                    rec.MpBefore = player.GetTotalCurrentMP();
                    rec.Notes = string.IsNullOrEmpty(rec.Notes) ? "floor1_town" : rec.Notes + "; floor1_town";
                }
                else if (floor >= fMin && floor <= fMax)
                    player.MedicinalHerbCount += Mathf.Max(0, settings.RollMedicinalHerbGrantCount(rng));
            }

            int steps = rng.Next(settings.stepsPerFloorMin, settings.stepsPerFloorMax + 1);
            int cooldown = 0;
            int battles = 0;
            int battleWins = 0;
            int encounters = 0;
            int xpGained = 0;
            int goldGained = 0;
            int potionUseCount = 0;
            int dawnChaliceUseCount = 0;
            int medicinalHerbUseCount = 0;
            int skillUseCount = 0;
            int recoverySkillUseCount = 0;
            int dmgDealt = 0;
            int dmgTaken = 0;
            int turnsSum = 0;
            int levelUpsThisFloor = 0;
            int fleesThisFloor = 0;
            int lastBattleEnemyCount = 0;

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
                    rec.Notes = "maxBattlesPerFloor reached";
                    break;
                }

                encounters++;
                int hPre, pPre, cPre;
                TryHealPartyPriorityHerbPotionChalice(player, settings, rng, out hPre, out pPre, out cPre);
                medicinalHerbUseCount += hPre;
                potionUseCount += pPre;
                dawnChaliceUseCount += cPre;

                var enemyParty = monsterPool.BuildEnemyParty(entry, rng, floor, settings.randomOneOrTwoEnemyPartyFromFloor);
                if (enemyParty.Count == 0) continue;
                var enemyUnits = BuildEnemyUnitsFromMonsterSOs(enemyParty);
                if (enemyUnits.Count == 0) continue;

                if (settings.aiProgressionEnabled && settings.aiTownRetreatBeforeUnsafeBattle && settings.floor1TownFullRestoreEnabled)
                {
                    int maxAtk = GetMaxMonsterAtkFromParty(enemyParty);
                    int turns = Mathf.Max(1, settings.aiSurvivalEnemyTurnCount);
                    for (int tr = 0; tr < settings.aiMaxTownRetreatsPerEncounter; tr++)
                    {
                        if (maxAtk <= 0) break;
                        long needHp = (long)maxAtk * turns;
                        if (player.GetTotalCurrentHP() >= needHp) break;
                        ApplyFloor1TownFullRestore(player, settings);
                        if (player.GetTotalCurrentHP() >= needHp) break;
                    }
                }

                lastBattleEnemyCount = enemyUnits.Count;

                battles++;
                bool allowFleeForThisBattle = fleesThisFloor < Mathf.Max(0, settings.maxFleesPerFloor);
                var outcome = bm.RunOneBattleForDungeon(player.Units, enemyUnits, rng, allowFleeForThisBattle, player, settings);
                turnsSum += outcome.Turns;
                dmgDealt += outcome.TotalDamageDealtToEnemies;
                dmgTaken += outcome.TotalDamageTakenByAllies;
                skillUseCount += outcome.SkillUseCount;
                recoverySkillUseCount += outcome.RecoverySkillUseCount;
                potionUseCount += outcome.BattleHpPotionUses;
                dawnChaliceUseCount += outcome.BattleDawnChaliceUses;
                medicinalHerbUseCount += outcome.BattleMedicinalHerbUses;

                if (outcome.AllyWin)
                {
                    battleWins++;
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
                        ApplyLevelUp(player, rng);
                        levelUpsThisFloor++;
                    }

                    if (floor == 1 && settings.floor1TownFullRestoreEnabled && settings.floor1TownAfterVictoryEnabled)
                        ApplyFloor1TownFullRestore(player, settings);

                    cooldown = settings.postBattleCooldownSteps;
                }
                else if (outcome.AllyEscaped)
                {
                    fleesThisFloor++;
                    cooldown = settings.postBattleCooldownSteps;
                }
                else
                {
                    rec.Notes = string.IsNullOrEmpty(rec.Notes) ? "party wiped" : rec.Notes + "; party wiped";
                    break;
                }

                // 전투 직전에는 대개 만피라 임계에 안 걸림. 승리·도망 후 HP가 깎인 상태에서 회복(약초→포션→잔).
                int hPost, pPost, cPost;
                TryHealPartyPriorityHerbPotionChalice(player, settings, rng, out hPost, out pPost, out cPost);
                medicinalHerbUseCount += hPost;
                potionUseCount += pPost;
                dawnChaliceUseCount += cPost;
            }

            rec.StepsMoved = stepsTakenActual;
            rec.Encounters = encounters;
            rec.Battles = battles;
            rec.BattlesWon = battleWins;
            rec.BattlesFled = fleesThisFloor;
            rec.PotionUseCount = potionUseCount;
            rec.DawnChaliceUseCount = dawnChaliceUseCount;
            rec.MedicinalHerbUseCount = medicinalHerbUseCount;
            rec.PotionRemainAfter = player.HpPotionCount;
            rec.DawnChaliceRemainAfter = player.DawnChaliceCharges;
            rec.MedicinalHerbRemainAfter = player.MedicinalHerbCount;
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
            rec.Floor1TownUsesCumulative = player.Floor1TownUsesTotal;
            rec.LastBattleEnemyCount = lastBattleEnemyCount;
            if (levelUpsThisFloor > 0)
                rec.Notes = string.IsNullOrEmpty(rec.Notes) ? $"+{levelUpsThisFloor} levelup" : rec.Notes + $"; +{levelUpsThisFloor} levelup";
            if (fleesThisFloor > 0)
                rec.Notes = string.IsNullOrEmpty(rec.Notes) ? $"flee×{fleesThisFloor}" : rec.Notes + $"; flee×{fleesThisFloor}";

            return rec;
        }

        /// <summary>
        /// 던전 시뮬 레벨업.
        /// <para>HP/MP — <see cref="CharacterClass.hpPerLevel"/>·<see cref="CharacterClass.mpPerLevel"/>
        /// (없으면 Settings 대체) + 종의 기억 성장치 합 + ±1 노이즈(<see cref="PlayerStats.ApplyHpMpGrowth"/> 동일).</para>
        /// <para>스탯 — 시뮬 전용으로 <b>레벨업당 2 포인트 랜덤</b>:
        ///   ① 직업 가중 랜덤 +1 (가중치 0/직업 없음이면 5스탯 균등),
        ///   ② 종의 기억 가중 랜덤 +1 (가중치 0/슬롯 없음이면 5스탯 균등 — 실전의 “선택 1포인트”를 시뮬에서 랜덤 대체).</para>
        /// <para>Settings.levelUp*Gain는 위 랜덤과 별개로 추가되는 고정 보너스(기본값 0 권장).</para>
        /// </summary>
        private void ApplyLevelUp(DungeonSimPlayer player, System.Random rng)
        {
            player.Level++;
            player.LevelUpsTotal++;
            foreach (var u in player.Units)
            {
                if (u == null || !u.IsAlive) continue;

                AccumulateMemoryGrowthWeights(u,
                    out float memAtkW, out float memDefW, out float memMagW, out float memAgiW, out float memLukW,
                    out float hpGrowthSum, out float mpGrowthSum);

                float hpNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float mpNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                int classHpGain = u.SimCharacterClass != null
                    ? Mathf.Max(0, u.SimCharacterClass.hpPerLevel)
                    : settings.levelUpHpGain;
                int classMpGain = u.SimCharacterClass != null
                    ? Mathf.Max(0, u.SimCharacterClass.mpPerLevel)
                    : settings.levelUpMpGain;
                int finalHpGain = Mathf.Max(1, Mathf.RoundToInt(classHpGain + hpGrowthSum + hpNoise));
                int finalMpGain = Mathf.Max(1, Mathf.RoundToInt(classMpGain + mpGrowthSum + mpNoise));

                u.MaxHP += finalHpGain;
                u.MaxMP += finalMpGain;
                u.Attack += settings.levelUpAtkGain;
                u.Defense += settings.levelUpDefGain;
                u.Magic += settings.levelUpMagGain;
                u.Agility += settings.levelUpAgiGain;
                u.Luck += settings.levelUpLukGain;

                ApplyClassRandomStatPlusOne(u, rng);
                ApplyMemoryRandomStatPlusOneOrUniform(u, memAtkW, memDefW, memMagW, memAgiW, memLukW, rng);

                u.CurrentHP = u.MaxHP;
                u.CurrentMP = u.MaxMP;
            }
        }

        /// <summary>
        /// 시뮬 “2포인트 랜덤” 중 ①: 직업 가중 랜덤 +1.
        /// <see cref="CharacterClass.attackGrowthPerLevel"/> ~ <see cref="CharacterClass.luckGrowthPerLevel"/> 합을
        /// 가중치로 사용. 모두 0이거나 직업이 없으면 5스탯 균등 랜덤.
        /// </summary>
        private static void ApplyClassRandomStatPlusOne(BattleSimUnit u, System.Random rng)
        {
            float a, d, m, ag, l;
            var c = u.SimCharacterClass;
            if (c != null)
            {
                a = Mathf.Max(0f, c.attackGrowthPerLevel);
                d = Mathf.Max(0f, c.defenseGrowthPerLevel);
                m = Mathf.Max(0f, c.magicGrowthPerLevel);
                ag = Mathf.Max(0f, c.agilityGrowthPerLevel);
                l = Mathf.Max(0f, c.luckGrowthPerLevel);
            }
            else
            {
                a = d = m = ag = l = 0f;
            }
            if (a + d + m + ag + l <= 0f)
            {
                a = d = m = ag = l = 1f; // 균등 폴백
            }
            PickAndBumpStat(u, a, d, m, ag, l, rng);
        }

        /// <summary>
        /// 시뮬 “2포인트 랜덤” 중 ②: 종의 기억 가중 랜덤 +1.
        /// 가중치가 0이거나 슬롯이 비어 있으면 5스탯 균등 랜덤(실전의 “선택 1포인트”를 시뮬에서 랜덤 대체).
        /// </summary>
        private static void ApplyMemoryRandomStatPlusOneOrUniform(
            BattleSimUnit u,
            float atkW, float defW, float magW, float agiW, float lukW,
            System.Random rng)
        {
            if (atkW + defW + magW + agiW + lukW <= 0f)
            {
                atkW = defW = magW = agiW = lukW = 1f; // 균등 폴백
            }
            PickAndBumpStat(u, atkW, defW, magW, agiW, lukW, rng);
        }

        private static void PickAndBumpStat(BattleSimUnit u, float a, float d, float m, float ag, float l, System.Random rng)
        {
            float total = a + d + m + ag + l;
            if (total <= 0f) return;
            float pick = (float)(rng.NextDouble() * total);
            if (pick < a) u.Attack++;
            else if (pick < a + d) u.Defense++;
            else if (pick < a + d + m) u.Magic++;
            else if (pick < a + d + m + ag) u.Agility++;
            else u.Luck++;
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
            ApplyMemFlat(so.memorySlot1, baseHpRef, ref h, ref a, ref d, ref mg, ref ag, ref lk);
            ApplyMemFlat(so.memorySlot2, baseHpRef, ref h, ref a, ref d, ref mg, ref ag, ref lk);
            ApplyMemFlat(so.memorySlot3, baseHpRef, ref h, ref a, ref d, ref mg, ref ag, ref lk);
            hp = h; mp = mP; atk = a; def = d; mag = mg; agi = ag; luk = lk;
        }

        private static void ApplyMemFlat(
            MemoryOfSpeciesData m,
            int baseHpRef,
            ref int hp, ref int atk, ref int def, ref int mag, ref int agi, ref int luk)
        {
            if (m == null) return;
            hp += Mathf.RoundToInt(baseHpRef * m.hpBonusPercent / 100f) + m.hpBonus;
            atk += m.attackBonus;
            def += m.defenseBonus;
            mag += m.magicBonus;
            agi += m.agilityBonus;
            luk += m.luckBonus;
        }

        // ---------------------------------------------------------------
        // 1층 마을 — 시뮬 전용 (경제 없음: 풀 HP/MP + 소모품 스택 고정)
        // ---------------------------------------------------------------
        public static void ApplyFloor1TownFullRestore(DungeonSimPlayer player, DungeonSimSettings settings)
        {
            if (player?.Units == null || settings == null) return;
            foreach (var u in player.Units)
            {
                if (u == null || !u.IsAlive) continue;
                if (u.MaxHP > 0) u.CurrentHP = u.MaxHP;
                if (u.MaxMP > 0) u.CurrentMP = u.MaxMP;
            }
            player.HpPotionCount = Mathf.Max(0, settings.startingHpPotionCount);
            player.DawnChaliceCharges = Mathf.Max(0, settings.startingDawnChaliceCharges);
            player.MedicinalHerbCount = Mathf.Max(0, settings.floor1TownMedicinalHerbStack);
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
        private static DungeonSimPlayer BuildPlayerFromRoster(BattleSimAllyRoster roster, DungeonSimSettings st)
        {
            var p = new DungeonSimPlayer
            {
                Name = "Party",
                Level = Mathf.Max(1, st.startingLevel),
                Exp = Mathf.Max(0, st.startingExp),
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
                p.Units.Add(new BattleSimUnit
                {
                    Team = BattleSimTeam.Ally,
                    DisplayName = so.allyDisplayName,
                    Slot = slot,
                    MaxHP = hp,
                    CurrentHP = hp,
                    MaxMP = mp,
                    CurrentMP = mp,
                    Attack = atk,
                    Defense = def,
                    Magic = mag,
                    Agility = agi,
                    Luck = luk,
                    SimAiPattern = so.simAiPattern,
                    SimSkills = new List<SkillData>(so.simSkills ?? new List<SkillData>()),
                    MemorySlot1 = so.memorySlot1,
                    MemorySlot2 = so.memorySlot2,
                    MemorySlot3 = so.memorySlot3,
                    SimCharacterClass = so.characterClass
                });
            }
            return p;
        }

        /// <summary>인카운터 직전 생존 판정용 — 적 파티 중 최대 ATK.</summary>
        private static int GetMaxMonsterAtkFromParty(List<MonsterSO> party)
        {
            int maxAtk = 0;
            if (party == null) return 0;
            for (int i = 0; i < party.Count; i++)
            {
                var m = party[i];
                if (m == null) continue;
                maxAtk = Mathf.Max(maxAtk, m.ATK);
            }
            return maxAtk;
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

        private string BuildSummary(int iterations, int deaths, int allFloorsCleared, long battles, long battleWins, long battleFlees, long medicinalHerbs, long potions, long dawnChalice, int[] reached, List<DungeonSimRecord> records)
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
                sb.AppendLine($"층 진입 약초: {settings.medicinalHerbGrantFloorsMin}~{settings.medicinalHerbGrantFloorsMax}층마다 {hLo}~{hHi}개(균등 랜덤). 1층 마을 적용 시 1층 진입 지급은 생략.");
            }
            if (settings.floor1TownFullRestoreEnabled)
            {
                int gLo = Mathf.Min(settings.medicinalHerbGrantCountMin, settings.medicinalHerbGrantCountMax);
                int gHi = Mathf.Max(settings.medicinalHerbGrantCountMin, settings.medicinalHerbGrantCountMax);
                sb.AppendLine($"1층 마을(시뮬) — 1층 진입 시 HP·MP 풀, 포션 {settings.startingHpPotionCount}·잔 {settings.startingDawnChaliceCharges}·약초 {settings.floor1TownMedicinalHerbStack}으로 맞춤(경제 없음). 이 층의 약초 진입 지급({gLo}~{gHi} 랜덤)은 생략." +
                              (settings.floor1TownAfterVictoryEnabled ? " 승리마다 동일 충전." : ""));
            }
            if (settings.randomOneOrTwoEnemyPartyFromFloor > 0)
                sb.AppendLine($"적 파티(시뮬): {settings.randomOneOrTwoEnemyPartyFromFloor}층 이상·비보스 인카운터는 적 1~2마리 균등 랜덤.");
            if (settings.aiProgressionEnabled)
            {
                sb.AppendLine("AI 진행(시뮬): ON — 전투 전 HP < 적최대ATK×" + settings.aiSurvivalEnemyTurnCount + "이면 마을 풀충전(최대 " + settings.aiMaxTownRetreatsPerEncounter + "회/인카운터) 후 재판정, 불가면 전투.");
                sb.AppendLine("  다음 층 진입 최소 레벨 미달 시 같은 층 추가 패스(최대 " + settings.aiMaxFarmingPassesBeforeNextFloor + "회, CSV notes: ai_farm).");
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
                        sb.AppendLine("  층별 최소 진입 레벨: " + gs);
                }
            }
            sb.AppendLine();
            sb.AppendLine($"전체 클리어(모든 층): {allFloorsCleared}/{iterations} ({100f * allFloorsCleared / iterations:F1}%)");
            sb.AppendLine($"사망 회차: {deaths}/{iterations} ({100f * deaths / iterations:F1}%)");
            if (battles > 0)
                sb.AppendLine($"전투 승률(전체): {battleWins}/{battles} ({100.0 * battleWins / battles:F1}%)");
            else
                sb.AppendLine("전투 승률(전체): N/A (전투 0회)");
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
            AppendDiagnostics(sb, records);
            sb.AppendLine();
            sb.AppendLine("CSV 컬럼: " + DungeonSimRecord.CsvHeader);
            sb.AppendLine();
            sb.AppendLine("[Phase 1] 레벨업: HP/MP = CharacterClass hpPerLevel/mpPerLevel(없으면 Settings) + 종의 기억 성장치 합 + ±1. 스탯 = 직업 가중 랜덤 +1 (없으면 균등) + 종의 기억 가중 랜덤 +1 (없으면 균등). Settings.levelUp*Gain은 추가 고정.");

            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // 진단 — 어디서 막히는가
        //   ① 사망 층 분포 (death_flag=1)
        //   ② 층별 진단표 (도달 / 클리어 / 사망 / 평균 전투·승률·HP·포션·잔·레벨)
        //   ③ 회복 자원 진단 (포션이 바닥난 회차의 평균 층, 새벽의 잔 사용률 등)
        //   ④ 레벨업 진단 (사망 시 평균 레벨, 첫 레벨업 평균 층)
        //   ⑤ 사망 원인 분류 (1전투 즉사 / 회복 자원 보유한 채 죽음 / 자원 고갈 사망)
        // ---------------------------------------------------------------
        private void AppendDiagnostics(StringBuilder sb, List<DungeonSimRecord> records)
        {
            if (records == null || records.Count == 0) return;

            int floors = Mathf.Max(1, settings.floorCount);

            // 한 회차의 마지막 행만 모음(사망/완주 모두 포함, 사망행이라면 그게 마지막)
            var lastRowByRun = new Dictionary<int, DungeonSimRecord>();
            foreach (var r in records)
            {
                if (!lastRowByRun.TryGetValue(r.RunId, out var prev) || r.Floor > prev.Floor)
                    lastRowByRun[r.RunId] = r;
            }

            // 층별 누적기
            int[] reachedCnt = new int[floors + 2];
            int[] clearedCnt = new int[floors + 2];
            int[] wipedCnt = new int[floors + 2];
            long[] battlesSum = new long[floors + 2];
            long[] winsSum = new long[floors + 2];
            long[] fledSum = new long[floors + 2];
            long[] hpBeforeSum = new long[floors + 2];
            long[] hpAfterSum = new long[floors + 2];
            long[] potionUseSum = new long[floors + 2];
            long[] herbUseSum = new long[floors + 2];
            long[] chaliceUseSum = new long[floors + 2];
            long[] potionAfterSum = new long[floors + 2];
            long[] herbAfterSum = new long[floors + 2];
            long[] chaliceAfterSum = new long[floors + 2];
            long[] xpSum = new long[floors + 2];
            long[] levelBeforeSum = new long[floors + 2];
            long[] levelAfterSum = new long[floors + 2];
            long[] stepsSum = new long[floors + 2];
            long[] damageTakenSum = new long[floors + 2];

            foreach (var r in records)
            {
                int f = Mathf.Clamp(r.Floor, 1, floors);
                reachedCnt[f]++;
                if (r.ClearFlag) clearedCnt[f]++;
                if (r.DeathFlag) wipedCnt[f]++;
                battlesSum[f] += r.Battles;
                winsSum[f] += r.BattlesWon;
                fledSum[f] += r.BattlesFled;
                hpBeforeSum[f] += r.HpBefore;
                hpAfterSum[f] += r.HpAfter;
                potionUseSum[f] += r.PotionUseCount;
                herbUseSum[f] += r.MedicinalHerbUseCount;
                chaliceUseSum[f] += r.DawnChaliceUseCount;
                potionAfterSum[f] += r.PotionRemainAfter;
                herbAfterSum[f] += r.MedicinalHerbRemainAfter;
                chaliceAfterSum[f] += r.DawnChaliceRemainAfter;
                xpSum[f] += r.XpGained;
                levelBeforeSum[f] += r.LevelBefore;
                levelAfterSum[f] += r.LevelAfter;
                stepsSum[f] += r.StepsMoved;
                damageTakenSum[f] += r.TotalDamageTaken;
            }

            int totalRuns = lastRowByRun.Count;

            long nFullClear = 0, nDeathEnd = 0, nOtherEnd = 0;
            long sumLvClear = 0, sumHerbClear = 0, sumPotClear = 0, sumChalClear = 0, sumTownClear = 0;
            long sumLvDeath = 0, sumHerbDeath = 0, sumPotDeath = 0, sumChalDeath = 0, sumTownDeath = 0;
            foreach (var kv in lastRowByRun)
            {
                var r = kv.Value;
                bool fullClear = !r.DeathFlag && r.Floor >= floors;
                if (fullClear)
                {
                    nFullClear++;
                    sumLvClear += r.LevelAfter;
                    sumHerbClear += r.MedicinalHerbRemainAfter;
                    sumPotClear += r.PotionRemainAfter;
                    sumChalClear += r.DawnChaliceRemainAfter;
                    sumTownClear += r.Floor1TownUsesCumulative;
                }
                else if (r.DeathFlag)
                {
                    nDeathEnd++;
                    sumLvDeath += r.LevelAfter;
                    sumHerbDeath += r.MedicinalHerbRemainAfter;
                    sumPotDeath += r.PotionRemainAfter;
                    sumChalDeath += r.DawnChaliceRemainAfter;
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
                    $"  {floors}층 전체 클리어(사망 없음): {nFullClear}회 — 평균 레벨 {sumLvClear / (double)nFullClear:F2}, " +
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
                sb.AppendLine($"  기타 종료(사망 아님·{floors}층 미만 등): {nOtherEnd}회 — 시뮬 예외 시 CSV로 확인.");

            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine("=== 진단 — 어디서 막히는가 ===");

            // ① 사망 층 분포
            int[] deathFloor = new int[floors + 2];
            int wipedTotal = 0;
            foreach (var kv in lastRowByRun)
            {
                var r = kv.Value;
                if (r.DeathFlag)
                {
                    int f = Mathf.Clamp(r.Floor, 1, floors);
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
                for (int f = 1; f <= floors; f++)
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
                    for (int f = 1; f <= floors; f++)
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
            sb.AppendLine("  층 |  도달 | 클리어%  | 사망% | 전투 | 승률 | 도망 | HP진입 → HP종료(손실) | 약초사용/남음 | 포션사용/남음 | 잔사용/남음 | 레벨진입 → 종료 | XP");
            for (int f = 1; f <= floors; f++)
            {
                int reachedF = reachedCnt[f];
                if (reachedF == 0) continue;
                double clearPct = 100.0 * clearedCnt[f] / reachedF;
                double diePct = 100.0 * wipedCnt[f] / reachedF;
                double avgBattles = (double)battlesSum[f] / reachedF;
                double avgWinRate = battlesSum[f] > 0 ? 100.0 * winsSum[f] / battlesSum[f] : 0;
                double avgFlee = (double)fledSum[f] / reachedF;
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
                    $"  {f,2} | {reachedF,5} | {clearPct,6:F1}% | {diePct,5:F1}% | {avgBattles,4:F2} | {avgWinRate,4:F1}% | {avgFlee,4:F2} | " +
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
            var perRunRecords = new Dictionary<int, List<DungeonSimRecord>>();
            foreach (var r in records)
            {
                if (!perRunRecords.TryGetValue(r.RunId, out var list))
                {
                    list = new List<DungeonSimRecord>();
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
                    if (firstPotionEmpty < 0 && r.PotionRemainAfter == 0) firstPotionEmpty = r.Floor;
                    if (firstChaliceEmpty < 0 && r.DawnChaliceRemainAfter == 0 && settings.startingDawnChaliceCharges > 0)
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
                    if (r.LevelAfter > r.LevelBefore && firstLevelUpFloor < 0)
                        firstLevelUpFloor = r.Floor;
                    if (r.DeathFlag) { died = true; deathLevel = r.LevelAfter; }
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
                if (!r.DeathFlag) continue;
                if (r.Battles <= 1) sudden++;
                else afterMany++;
                if (r.PotionRemainAfter > 0) withPotion++; else noPotion++;
                if (r.DawnChaliceRemainAfter > 0) withChalice++; else noChalice++;
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
                for (int f = 1; f <= floors; f++) { if (deathFloor[f] > 0) { firstWallFloor = f; break; } }
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
