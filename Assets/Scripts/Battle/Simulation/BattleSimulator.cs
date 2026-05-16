using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// <see cref="BattleSimAllyRoster"/> + <see cref="BattleSimEnemyRoster"/>를 읽어 헤드리스 자동 전투를 반복합니다.
    /// (구버전 <c>BalanceSimulator</c>와 다릅니다 — Roster SO 필드가 있는 쪽이 본 컴포넌트입니다.)
    /// </summary>
    [AddComponentMenu("Abyssdawn/Battle Simulation (Roster SO)")]
    public class BattleSimulator : MonoBehaviour
    {
        [Header("편성 (SO)")]
        [SerializeField] private BattleSimAllyRoster allyRoster;
        [SerializeField] private BattleSimEnemyRoster enemyRoster;

        [Header("실행 옵션")]
        [SerializeField] private int iterations = 100;
        [SerializeField] private int baseSeed = 12345;
        [Tooltip("턴 상한 (무한 루프 방지)")]
        [SerializeField] private int maxTurnsPerBattle = 500;
        [SerializeField] private float criticalChanceBase = BattleSimCombatMath.DefaultCriticalChanceBase;

        [Header("출력")]
        [SerializeField] private bool writeReportFile = true;
        [Tooltip("프로젝트 루트(Assets 상위) 기준 상대 경로. Assets/ 아래 두면 런타임 덮어쓰기 시 Import 에러(버전 불일치)가 날 수 있습니다.")]
        [SerializeField] private string reportRelativePath = "BattleSimLogs/_LastBattleSimReport.txt";

        [Header("행동 AI (시뮬)")]
        [Tooltip("아이템 행동 — 아직 미구현이면 끄세요.")]
        [SerializeField] private bool allowItemActions;
        [Tooltip("도망 행동 — 아직 미구현이면 끄세요.")]
        [SerializeField] private bool allowFleeActions;
        [SerializeField] private float simItemPickChance = 0.02f;
        [SerializeField] private float simFleePickChance = 0.03f;

        [Tooltip("살아 있는 유닛 기준 HP 풀(현재HP 합 / MaxHP 합)이 적보다 낮으면, 아군 페이즈 시작 전에 전투를 즉시 종료합니다(도망 성공 100%, 보상 없음).")]
        [SerializeField] private bool fleeWhenHpDisadvantaged = true;

        [Tooltip("도망 발동 격차 — (적 HP 비율 - 아군 HP 비율)이 이 값 이상이어야 도망. 0.70 = 적이 70%p 이상 더 건강할 때만.")]
        [Range(0f, 1f)]
        [SerializeField] private float fleeHpGapThreshold = 0.70f;

        [Header("스킬 (시뮬)")]
        [Tooltip("사용 가능한 액티브 스킬이 있을 때, 방어가 아닌 행동에서 스킬을 고를 확률 (0~1). 나머지는 일반 공격.")]
        [Range(0f, 1f)]
        [SerializeField] private float simSkillPickChance = 0.45f;
        [Tooltip("회복 스킬 SO의 damageType이 None일 때, 리포트의 ‘회복·Physical/Magic’ 줄에만 쓰는 대체 분류입니다. 전체·None 집계는 SO 그대로입니다.")]
        [SerializeField] private DamageType simRecoveryReportFallbackWhenNone = DamageType.Magic;

        /// <summary><see cref="RunSimulation"/> 시작 시 인스펙터 값을 읽어 정적 집계에 전달합니다.</summary>
        private static DamageType s_recoveryReportFallbackWhenNone = DamageType.Magic;

        public bool HasRostersAssigned() => allyRoster != null && enemyRoster != null;

        // ========================================================
        // [DUNGEON SIM HOOK 2026-05-14] 던전 시뮬레이터용 공개 API.
        // BattleSimulator의 1전투 로직을 외부(DungeonSimulator)에서 호출할 수 있도록
        // 슬롯 누적기/스킬 통계는 내부에서 dummy로 만들고, 요약 결과만 반환합니다.
        // ========================================================

        /// <summary>던전 시뮬용 1전투 결과 요약.</summary>
        public sealed class DungeonBattleResult
        {
            public bool AllyWin;
            /// <summary>HP 불리 판정으로 전투를 이탈했을 때 true. 승리도 패배(전멸)도 아님 — 보상 없음, 아군 생존 유지.</summary>
            public bool AllyEscaped;
            public int Turns;
            public int TotalDamageDealtToEnemies;
            public int TotalDamageTakenByAllies;
            public int SkillUseCount;
            public int RecoverySkillUseCount;
            public int DeadAllies;
            public int DeadEnemies;
            /// <summary>이번 전투 도중(피해 직후) 소모된 약초 개수.</summary>
            public int BattleMedicinalHerbUses;
            /// <summary>이번 전투 도중(피해 직후) 소모된 일반 HP 포션 개수.</summary>
            public int BattleHpPotionUses;
            /// <summary>이번 전투 도중 소모된 새벽의 잔 충전 횟수(0 또는 1 이상 누적).</summary>
            public int BattleDawnChaliceUses;
        }

        /// <summary>
        /// 던전 시뮬레이터 전용 — 외부에서 빌드된 유닛 리스트로 1전투를 진행합니다.
        /// 입력 유닛은 호출자 소유(HP/MP가 누적되어야 함). 이 메서드는 IsAlive/HP/MP/Slot만 사용하고
        /// 시뮬 가드 플래그는 시작/종료 시 자동 클리어합니다.
        /// </summary>
        public DungeonBattleResult RunOneBattleForDungeon(
            List<BattleSimUnit> allies,
            List<BattleSimUnit> enemies,
            System.Random rng,
            bool allowFleeForThisBattle = true,
            DungeonSimPlayer dungeonConsumablePlayer = null,
            DungeonSimSettings dungeonConsumableSettings = null)
        {
            if (allies == null || enemies == null || rng == null)
                return new DungeonBattleResult { AllyWin = false, AllyEscaped = false, Turns = 0 };

            ClearAllSimGuardFlags(allies, enemies);

            var acc = new SlotBattleAccumulator();
            var skillStats = new SimGlobalSkillStats();
            bool alliesVictory;
            bool allyEscaped;

            DungeonMidBattleConsumableContext dungeonCx = null;
            if (dungeonConsumablePlayer != null && dungeonConsumableSettings != null)
                dungeonCx = new DungeonMidBattleConsumableContext(dungeonConsumablePlayer, dungeonConsumableSettings, rng);

            int turns = RunSingleBattleWithUnits(
                allies, enemies, rng, criticalChanceBase, acc, skillStats,
                allowItemActions, allowFleeActions,
                simItemPickChance, simFleePickChance,
                allowFleeForThisBattle,
                dungeonCx,
                out alliesVictory, out allyEscaped);

            int totalDamageEnemies = 0;
            int totalDamageAllies = 0;
            for (int s = 1; s <= 4; s++)
            {
                totalDamageEnemies += (int)acc.EnemyDefenderDamageTaken[s];
                totalDamageAllies += (int)acc.AllyDefenderDamageTaken[s];
            }

            int deadAllies = 0;
            int deadEnemies = 0;
            foreach (var u in allies) if (!u.IsAlive) deadAllies++;
            foreach (var u in enemies) if (!u.IsAlive) deadEnemies++;

            ClearAllSimGuardFlags(allies, enemies);

            return new DungeonBattleResult
            {
                AllyWin = alliesVictory,
                AllyEscaped = allyEscaped,
                Turns = turns,
                TotalDamageDealtToEnemies = totalDamageEnemies,
                TotalDamageTakenByAllies = totalDamageAllies,
                SkillUseCount = (int)skillStats.TotalSkillActivations,
                RecoverySkillUseCount = (int)skillStats.RecoveryActivations,
                DeadAllies = deadAllies,
                DeadEnemies = deadEnemies,
                BattleMedicinalHerbUses = dungeonCx != null ? dungeonCx.HerbUses : 0,
                BattleHpPotionUses = dungeonCx != null ? dungeonCx.PotionUses : 0,
                BattleDawnChaliceUses = dungeonCx != null ? dungeonCx.ChaliceUses : 0
            };
        }

        /// <summary>던전 시뮬 1전투 중 — 아군 HP가 깎인 뒤 <see cref="DungeonSimulator.TryHealPartyPriorityHerbPotionChalice"/> (유지·긴급·적극 반복).</summary>
        private sealed class DungeonMidBattleConsumableContext
        {
            public readonly DungeonSimPlayer Player;
            public readonly DungeonSimSettings Settings;
            public readonly System.Random Rng;
            public int HerbUses;
            public int PotionUses;
            public int ChaliceUses;

            public DungeonMidBattleConsumableContext(DungeonSimPlayer player, DungeonSimSettings settings, System.Random rng)
            {
                Player = player;
                Settings = settings;
                Rng = rng;
            }
        }

        private static void DungeonSimTryConsumablesAfterAllyHpReduced(
            BattleSimUnit damagedAlly,
            DungeonMidBattleConsumableContext cx)
        {
            if (cx == null || damagedAlly == null) return;
            if (damagedAlly.Team != BattleSimTeam.Ally) return;
            DungeonSimulator.TryHealPartyPriorityHerbPotionChalice(cx.Player, cx.Settings, cx.Rng, out int h, out int p, out int c);
            cx.HerbUses += h;
            cx.PotionUses += p;
            cx.ChaliceUses += c;
        }

        /// <summary>외부에서 빌드된 유닛 리스트로 1라운드 루프를 돌리는 내부 헬퍼.</summary>
        private int RunSingleBattleWithUnits(
            List<BattleSimUnit> allies,
            List<BattleSimUnit> enemies,
            System.Random rng,
            float critChanceBase,
            SlotBattleAccumulator acc,
            SimGlobalSkillStats skillStats,
            bool allowItem,
            bool allowFlee,
            float itemChance,
            float fleeChance,
            bool allowHpGapFlee,
            DungeonMidBattleConsumableContext dungeonCx,
            out bool alliesVictory,
            out bool allyEscaped)
        {
            allyEscaped = false;
            var run = new SingleRunSlotTracker();

            for (int round = 0; round < maxTurnsPerBattle; round++)
            {
                if (!HasAlive(allies)) { alliesVictory = false; ClearAllSimGuardFlags(allies, enemies); run.CommitTo(acc, allies, enemies); return round; }
                if (!HasAlive(enemies)) { alliesVictory = true; ClearAllSimGuardFlags(allies, enemies); run.CommitTo(acc, allies, enemies); return round; }

                if (fleeWhenHpDisadvantaged && allowHpGapFlee && IsAllyHpPoolDisadvantagedVsEnemy(allies, enemies, fleeHpGapThreshold))
                {
                    alliesVictory = false;
                    allyEscaped = true;
                    ClearAllSimGuardFlags(allies, enemies);
                    run.CommitTo(acc, allies, enemies);
                    return round;
                }

                ExecuteSideTurn(allies, enemies, rng, critChanceBase, acc, skillStats, targetIsAlly: false, allowItem, allowFlee, itemChance, fleeChance, simSkillPickChance, dungeonCx);
                ClearEnemyNextAllyPhaseGuards(enemies);
                if (!HasAlive(enemies))
                {
                    alliesVictory = true;
                    run.BumpPartialRoundAllyOnly(allies);
                    ClearAllSimGuardFlags(allies, enemies);
                    run.CommitTo(acc, allies, enemies);
                    return round + 1;
                }

                ExecuteSideTurn(enemies, allies, rng, critChanceBase, acc, skillStats, targetIsAlly: true, allowItem, allowFlee, itemChance, fleeChance, simSkillPickChance, dungeonCx);
                ClearAllyEnemyPhaseGuards(allies);
                run.BumpEndOfFullRound(allies, enemies);
            }

            if (!HasAlive(allies)) alliesVictory = false;
            else if (!HasAlive(enemies)) alliesVictory = true;
            else alliesVictory = GetTotalHp(allies) > GetTotalHp(enemies);
            ClearAllSimGuardFlags(allies, enemies);
            run.CommitTo(acc, allies, enemies);
            return maxTurnsPerBattle;
        }

        /// <summary>에디터 커스텀 인스펙터에서 호출합니다.</summary>
        public void RunSimulation()
        {
            if (allyRoster == null || enemyRoster == null)
            {
                Debug.LogError("[BattleSimulator] Ally 또는 Enemy Roster가 비어 있습니다.");
                return;
            }

            s_recoveryReportFallbackWhenNone = simRecoveryReportFallbackWhenNone;

            if (iterations <= 0)
            {
                Debug.LogError("[BattleSimulator] iterations는 1 이상이어야 합니다.");
                return;
            }

            var acc = new SlotBattleAccumulator();
            var skillStats = new SimGlobalSkillStats();
            int allyWins = 0;
            int enemyWins = 0;
            long turnSum = 0;

            for (int i = 0; i < iterations; i++)
            {
                var rng = new System.Random(baseSeed + i);
                int turns = RunSingleBattle(rng, criticalChanceBase, acc, skillStats, allowItemActions, allowFleeActions, simItemPickChance, simFleePickChance, out bool alliesVictory, out _);
                turnSum += turns;
                skillStats.TotalRoundsSummed += turns;
                if (alliesVictory) allyWins++;
                else enemyWins++;
            }

            float avgTurns = iterations > 0 ? (float)turnSum / iterations : 0f;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Battle Simulation ({iterations} runs, seed base {baseSeed}) ===");
            sb.AppendLine($"리포트 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (로컬)");
            sb.AppendLine("시뮬 정의 버전: sim-2026-04-27 | 일반딜 v2 + simSkills 액티브(MP·HP 코스트=실전 동일) + HP<=50%시 회복 우선 + damageType별 HP% 집계 + 스킬/회복 집계");
            sb.AppendLine($"Ally wins: {allyWins} ({100f * allyWins / iterations:F1}%)");
            sb.AppendLine($"Enemy wins: {enemyWins} ({100f * enemyWins / iterations:F1}%)");
            sb.AppendLine($"Avg turns: {avgTurns:F2} (max cap {maxTurnsPerBattle})");
            sb.AppendLine();
            sb.AppendLine("--- 구현 검증 (시뮬 전용 공식) ---");
            sb.AppendLine("명중: Base(스킬 accuracy 또는 1) × AGI보정(clamp(0.8+(Att-Def)*0.02,0.6,1.2)) × SlotHit × Equip(1) → clamp 0.45~0.95.");
            sb.AppendLine("딜(일반): max(ATK×0.3, floor(ATK×1.6 − DEF×0.6)) × 피격슬롯(전열1~2:×1.10, 후열3~4:×0.90) × Crit(1.5 또는 1) × Equip(1).");
            sb.AppendLine($"스킬: SO simSkills, MP>=mpCost, HP>floor(maxHP*hpCostPercent/100)(실전과 동일), 시전 슬롯 허용 시 {simSkillPickChance:P0} 확률로 액티브 선택 -> SkillData 명중·배율·hitCount. 시뮬 AI: CurrentHP/MaxHP<=50%이면 쓸 수 있는 회복 스킬만 후보(없으면 기존처럼 전체). 회복=Effect Recovery 고정량.");
            sb.AppendLine();
            skillStats.AppendReport(sb, iterations);
            sb.AppendLine();
            acc.AppendSlotReport(sb, iterations);
            sb.AppendLine();
            sb.AppendLine($"규칙: 양측 슬롯 순 행동. 방어=해당 가드 구간 동안 받는 피해×{BattleSimCombatMath.SimGuardIncomingDamageMultiplier:F2}({(1f - BattleSimCombatMath.SimGuardIncomingDamageMultiplier) * 100f:F0}% 경감), 아군가드=같은 라운드 적 페이즈, 적가드=다음 라운드 아군 페이즈, 중첩 없음.");
            sb.AppendLine("타겟: AIPattern별 — Aggressive=전열 가중 랜덤, Defensive=균등 랜덤, Support=후열 우선.");
            sb.AppendLine($"아이템 행동: {(allowItemActions ? "허용" : "비활성")}, 도망(행동 랜덤): {(allowFleeActions ? "허용" : "비활성")}, HP불리 시 즉시 도망: {(fleeWhenHpDisadvantaged ? "켜짐(100%)" : "끔")}.");

            string report = sb.ToString();
            Debug.Log(report);

            if (writeReportFile)
            {
                try
                {
                    string abs = Path.Combine(Application.dataPath, "..", reportRelativePath).Replace('\\', Path.DirectorySeparatorChar);
                    abs = Path.GetFullPath(abs);
                    string dir = Path.GetDirectoryName(abs);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(abs, report, Encoding.UTF8);
                    Debug.Log($"[BattleSimulator] Report written: {abs}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BattleSimulator] Report file write failed: {e.Message}");
                }
            }
        }

        private int RunSingleBattle(
            System.Random rng,
            float critChanceBase,
            SlotBattleAccumulator acc,
            SimGlobalSkillStats skillStats,
            bool allowItem,
            bool allowFlee,
            float itemChance,
            float fleeChance,
            out bool alliesVictory,
            out bool allyEscaped)
        {
            var allies = BuildAlliesFromRoster(allyRoster);
            var enemies = BuildEnemiesFromRoster(enemyRoster);
            return RunSingleBattleWithUnits(
                allies, enemies, rng, critChanceBase, acc, skillStats,
                allowItem, allowFlee, itemChance, fleeChance,
                allowHpGapFlee: true,
                dungeonCx: null,
                out alliesVictory, out allyEscaped);
        }

        private static void ExecuteSideTurn(
            List<BattleSimUnit> attackers,
            List<BattleSimUnit> defenders,
            System.Random rng,
            float critChanceBase,
            SlotBattleAccumulator acc,
            SimGlobalSkillStats skillStats,
            bool targetIsAlly,
            bool allowItem,
            bool allowFlee,
            float itemChance,
            float fleeChance,
            float simSkillPickChance,
            DungeonMidBattleConsumableContext dungeonCx)
        {
            foreach (var unit in attackers.OrderBy(u => (int)u.Slot))
            {
                if (!unit.IsAlive) continue;

                BattleSimActionType action = BattleSimAi.PickAction(unit, rng, allowItem, allowFlee, itemChance, fleeChance);
                if (action == BattleSimActionType.Item || action == BattleSimActionType.Flee)
                    action = BattleSimActionType.Attack;

                if (action == BattleSimActionType.Defend)
                {
                    if (unit.Team == BattleSimTeam.Ally)
                        unit.SimGuardEnemyPhase = true;
                    else
                        unit.SimGuardNextAllyPhase = true;
                    continue;
                }

                bool usedSkill = simSkillPickChance > 0f &&
                                 rng.NextDouble() < simSkillPickChance &&
                                 TryExecuteSimSkill(unit, attackers, defenders, rng, critChanceBase, acc, skillStats, targetIsAlly, dungeonCx);
                if (usedSkill)
                    continue;

                var targetMode = BattleSimAi.GetTargetModeForPattern(unit.SimAiPattern);
                var target = BattleSimAi.PickTarget(defenders, rng, targetMode);
                if (target == null) continue;

                int si = SlotIndex(target.Slot);
                if (si < 1 || si > 4) continue;

                if (targetIsAlly)
                    acc.AllyDefenderAttackAttempts[si]++;
                else
                    acc.EnemyDefenderAttackAttempts[si]++;

                bool hit = BattleSimCombatMath.RollHit(
                    null,
                    unit.Agility,
                    target.Agility,
                    unit.Luck,
                    0f,
                    0f,
                    target.Slot,
                    rng);

                if (!hit) continue;

                if (targetIsAlly)
                    acc.AllyDefenderHits[si]++;
                else
                    acc.EnemyDefenderHits[si]++;

                bool crit = BattleSimCombatMath.RollCritical(unit.Luck, critChanceBase, rng);
                int dmg = BattleSimCombatMath.CalculateSimMeleeDamage(unit.Attack, target.Defense, crit, target.Slot);
                int applied = ApplyPhysicalDamageWithSimGuard(target, dmg, targetIsAlly, dungeonCx);
                if (targetIsAlly)
                    acc.AllyDefenderDamageTaken[si] += applied;
                else
                    acc.EnemyDefenderDamageTaken[si] += applied;
            }
        }

        /// <param name="damageIsEnemyVsAlly"><c>ExecuteSideTurn</c>의 targetIsAlly와 동일(적이 아군을 칠 때 true).</param>
        private static int ApplyPhysicalDamageWithSimGuard(
            BattleSimUnit target,
            int damageAfterFormula,
            bool damageIsEnemyVsAlly,
            DungeonMidBattleConsumableContext dungeonCx = null)
        {
            int dmg = damageAfterFormula;
            if (damageIsEnemyVsAlly && target.SimGuardEnemyPhase)
                dmg = Mathf.FloorToInt(dmg * BattleSimCombatMath.SimGuardIncomingDamageMultiplier);
            else if (!damageIsEnemyVsAlly && target.SimGuardNextAllyPhase)
                dmg = Mathf.FloorToInt(dmg * BattleSimCombatMath.SimGuardIncomingDamageMultiplier);
            int before = target.CurrentHP;
            target.ApplyDamage(dmg);
            if (damageIsEnemyVsAlly && dungeonCx != null && before > target.CurrentHP)
                DungeonSimTryConsumablesAfterAllyHpReduced(target, dungeonCx);
            return before - target.CurrentHP;
        }

        private static void ClearAllyEnemyPhaseGuards(List<BattleSimUnit> allies)
        {
            foreach (var u in allies)
                u.SimGuardEnemyPhase = false;
        }

        private static void ClearEnemyNextAllyPhaseGuards(List<BattleSimUnit> enemies)
        {
            foreach (var u in enemies)
                u.SimGuardNextAllyPhase = false;
        }

        private static void ClearAllSimGuardFlags(List<BattleSimUnit> allies, List<BattleSimUnit> enemies)
        {
            foreach (var u in allies)
            {
                u.SimGuardEnemyPhase = false;
                u.SimGuardNextAllyPhase = false;
            }
            foreach (var u in enemies)
            {
                u.SimGuardEnemyPhase = false;
                u.SimGuardNextAllyPhase = false;
            }
        }

        private static int SlotIndex(BattleSlot s) => Mathf.Clamp((int)s, 1, 4);

        /// <summary>전체 시뮬 반복에 걸친 스킬 시전·회복 HP% 구간 집계.</summary>
        private sealed class SimGlobalSkillStats
        {
            public long TotalSkillActivations;
            public long TotalRoundsSummed;
            public long RecoveryActivations;
            public long RecoveryCastByAlly;
            public long RecoveryCastByEnemy;
            public readonly long[] RecoveryCasterHpPctBucket = new long[10];

            /// <summary><see cref="SkillData.damageType"/> 기준 전 스킬 시전(회복·딜 공통), 비용 직전 시전자 HP%.</summary>
            public long CastsDamageTypePhysical;
            public long CastsDamageTypeMagic;
            public long CastsDamageTypeNone;
            public readonly long[] HpPctBucketPhysical = new long[10];
            public readonly long[] HpPctBucketMagic = new long[10];
            public readonly long[] HpPctBucketNone = new long[10];

            /// <summary>회복 분기만(실제 회복 처리). damageType별.</summary>
            public long RecoveryBranchPhysical;
            public long RecoveryBranchMagic;
            public long RecoveryBranchNone;
            public readonly long[] RecoveryBranchHpPhysical = new long[10];
            public readonly long[] RecoveryBranchHpMagic = new long[10];
            public readonly long[] RecoveryBranchHpNone = new long[10];

            /// <summary>피해 스킬 분기만(명중·딜 루프). damageType별.</summary>
            public long DamageBranchPhysical;
            public long DamageBranchMagic;
            public long DamageBranchNone;
            public readonly long[] DamageBranchHpPhysical = new long[10];
            public readonly long[] DamageBranchHpMagic = new long[10];
            public readonly long[] DamageBranchHpNone = new long[10];

            public void AppendReport(StringBuilder sb, int iterations)
            {
                sb.AppendLine("--- 스킬 사용 집계 (전체 반복 합산) ---");
                sb.AppendLine($"액티브 스킬 시전 총횟수: {TotalSkillActivations} (방어 제외, 시뮬 스킬 롤 성공 후 실제 시전만)");
                if (iterations > 0)
                    sb.AppendLine($"판당 평균 스킬 시전: {(double)TotalSkillActivations / iterations:F3}");
                else
                    sb.AppendLine("판당 평균 스킬 시전: n/a");

                if (TotalRoundsSummed > 0)
                    sb.AppendLine($"라운드당 평균 스킬 시전: {(double)TotalSkillActivations / TotalRoundsSummed:F4} (분모=각 판의 종료 라운드 수 합, RunSingleBattle 반환값 합)");
                else
                    sb.AppendLine("라운드당 평균 스킬 시전: n/a (라운드 합 0)");

                sb.AppendLine($"회복 스킬 시전 총횟수: {RecoveryActivations} (시전자: 아군 {RecoveryCastByAlly} / 적 {RecoveryCastByEnemy})");
                sb.AppendLine("회복 시전 시 시전자 HP% (스킬 비용 MP·HP 차감 직전, 100*CurrentHP/MaxHP), 10% 구간별 횟수 및 회복 시전 대비 비율:");
                if (RecoveryActivations <= 0)
                {
                    sb.AppendLine("  (회복 스킬 시전 없음 — simSkills 미배치, 패시브만, 자원/타겟 부족, 또는 스킬 롤 미적중)");
                }
                else
                {
                    for (int b = 0; b < 10; b++)
                    {
                        long c = RecoveryCasterHpPctBucket[b];
                        double pctOfRecovery = 100.0 * c / RecoveryActivations;
                        int lo = b * 10;
                        int hi = b == 9 ? 100 : (b + 1) * 10;
                        string rangeLabel = b == 9 ? "[90%, 100%]" : $"[{lo}%, {hi}%)";
                        sb.AppendLine($"  {rangeLabel}: {c} ({pctOfRecovery:F1}% of recovery casts)");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("--- damageType별 스킬 시전 (SkillData.damageType, 비용 직전 시전자 HP%) ---");
                sb.AppendLine("※ 회복 전용 분기·피해 딜 분기 모두 포함한 ‘전체’와, 분기별 소계.");
                AppendDamageTypeHistogram(sb, "전체·Physical", CastsDamageTypePhysical, HpPctBucketPhysical);
                AppendDamageTypeHistogram(sb, "전체·Magic", CastsDamageTypeMagic, HpPctBucketMagic);
                AppendDamageTypeHistogram(sb, "전체·None", CastsDamageTypeNone, HpPctBucketNone);
                sb.AppendLine("— 회복 분기만 (ApplySimRecoveryFromSkill 경로) —");
                AppendDamageTypeHistogram(sb, "회복·Physical", RecoveryBranchPhysical, RecoveryBranchHpPhysical);
                AppendDamageTypeHistogram(sb, "회복·Magic", RecoveryBranchMagic, RecoveryBranchHpMagic);
                AppendDamageTypeHistogram(sb, "회복·None", RecoveryBranchNone, RecoveryBranchHpNone);
                sb.AppendLine("— 피해 스킬 분기만 (명중/딜 루프) —");
                AppendDamageTypeHistogram(sb, "딜·Physical", DamageBranchPhysical, DamageBranchHpPhysical);
                AppendDamageTypeHistogram(sb, "딜·Magic", DamageBranchMagic, DamageBranchHpMagic);
                AppendDamageTypeHistogram(sb, "딜·None", DamageBranchNone, DamageBranchHpNone);
                sb.AppendLine($"※ 회복 분기 표: damageType이 None인 회복 SO는 아래 줄에만 '{s_recoveryReportFallbackWhenNone}'(인스펙터 simRecoveryReportFallbackWhenNone)로 분류합니다. '전체·None'은 SO 원본 그대로입니다.");
            }

            private static void AppendDamageTypeHistogram(StringBuilder sb, string label, long total, long[] buckets)
            {
                sb.AppendLine($"{label}: 총 {total}회");
                if (total <= 0)
                {
                    sb.AppendLine("  (없음)");
                    return;
                }

                for (int b = 0; b < 10; b++)
                {
                    long c = buckets[b];
                    double pct = 100.0 * c / total;
                    int lo = b * 10;
                    int hi = b == 9 ? 100 : (b + 1) * 10;
                    string rangeLabel = b == 9 ? "[90%, 100%]" : $"[{lo}%, {hi}%)";
                    sb.AppendLine($"  {rangeLabel}: {c} ({pct:F1}% of this row)");
                }
            }
        }

        /// <summary>한 판 안에서 슬롯별 생존(라운드) 누적 후 전역 누적기에 합산.</summary>
        private sealed class SingleRunSlotTracker
        {
            private readonly int[] allySurvivalRounds = new int[5];
            private readonly int[] enemySurvivalRounds = new int[5];

            public void BumpEndOfFullRound(List<BattleSimUnit> allies, List<BattleSimUnit> enemies)
            {
                foreach (var u in allies)
                {
                    if (!u.IsAlive) continue;
                    int i = SlotIndex(u.Slot);
                    allySurvivalRounds[i]++;
                }
                foreach (var u in enemies)
                {
                    if (!u.IsAlive) continue;
                    int i = SlotIndex(u.Slot);
                    enemySurvivalRounds[i]++;
                }
            }

            public void BumpPartialRoundAllyOnly(List<BattleSimUnit> allies)
            {
                foreach (var u in allies)
                {
                    if (!u.IsAlive) continue;
                    int i = SlotIndex(u.Slot);
                    allySurvivalRounds[i]++;
                }
            }

            public void CommitTo(SlotBattleAccumulator acc, List<BattleSimUnit> allies, List<BattleSimUnit> enemies)
            {
                for (int s = 1; s <= 4; s++)
                {
                    acc.AllySlotSurvivalTurnsSum[s] += allySurvivalRounds[s];
                    acc.EnemySlotSurvivalTurnsSum[s] += enemySurvivalRounds[s];
                }

                foreach (var u in allies)
                {
                    int i = SlotIndex(u.Slot);
                    if (u.IsAlive) acc.AllySlotSurvivedBattleEnd[i]++;
                    else acc.AllySlotDiedInBattle[i]++;
                }
                foreach (var u in enemies)
                {
                    int i = SlotIndex(u.Slot);
                    if (u.IsAlive) acc.EnemySlotSurvivedBattleEnd[i]++;
                    else acc.EnemySlotDiedInBattle[i]++;
                }
            }
        }

        private sealed class SlotBattleAccumulator
        {
            public readonly long[] AllyDefenderAttackAttempts = new long[5];
            public readonly long[] AllyDefenderHits = new long[5];
            public readonly long[] AllyDefenderDamageTaken = new long[5];
            public readonly long[] AllySlotSurvivalTurnsSum = new long[5];
            public readonly int[] AllySlotDiedInBattle = new int[5];
            public readonly int[] AllySlotSurvivedBattleEnd = new int[5];

            public readonly long[] EnemyDefenderAttackAttempts = new long[5];
            public readonly long[] EnemyDefenderHits = new long[5];
            public readonly long[] EnemyDefenderDamageTaken = new long[5];
            public readonly long[] EnemySlotSurvivalTurnsSum = new long[5];
            public readonly int[] EnemySlotDiedInBattle = new int[5];
            public readonly int[] EnemySlotSurvivedBattleEnd = new int[5];

            public void AppendSlotReport(StringBuilder sb, int iterations)
            {
                sb.AppendLine("[적 슬롯이 피격자일 때 — 아군이 공격, 일반 공격 명중/딜]");
                AppendDefenderBlock(sb, iterations, "Enemy", EnemyDefenderAttackAttempts, EnemyDefenderHits, EnemyDefenderDamageTaken,
                    EnemySlotSurvivalTurnsSum, EnemySlotDiedInBattle, EnemySlotSurvivedBattleEnd);
                sb.AppendLine();
                sb.AppendLine("[아군 슬롯이 피격자일 때 — 적이 공격]");
                AppendDefenderBlock(sb, iterations, "Ally", AllyDefenderAttackAttempts, AllyDefenderHits, AllyDefenderDamageTaken,
                    AllySlotSurvivalTurnsSum, AllySlotDiedInBattle, AllySlotSurvivedBattleEnd);
            }

            private static void AppendDefenderBlock(
                StringBuilder sb,
                int iterations,
                string label,
                long[] attempts,
                long[] hits,
                long[] dmgSum,
                long[] survivalTurnsSum,
                int[] deaths,
                int[] survivedEnd)
            {
                for (int s = 1; s <= 4; s++)
                {
                    long att = attempts[s];
                    long h = hits[s];
                    float hitRate = att > 0 ? 100f * h / att : 0f;
                    float avgSurvTurns = iterations > 0 ? (float)survivalTurnsSum[s] / iterations : 0f;
                    float deathRate = iterations > 0 ? 100f * deaths[s] / iterations : 0f;
                    float aliveEndRate = iterations > 0 ? 100f * survivedEnd[s] / iterations : 0f;
                    sb.AppendLine($"  {label} Slot{s}: 명중 {h}/{att} ({hitRate:F1}%), 받은 피해량 합계={dmgSum[s]}, 맞을 때 평균 피해={(h > 0 ? dmgSum[s] / (float)h : 0f):F1}");
                    sb.AppendLine($"           평균 생존 라운드(해당 슬롯 유닛이 살아 있던 라운드 수 합 ÷ 판수)={avgSurvTurns:F2}, 종료 시 사망 비율={deathRate:F1}%, 종료 시 생존 비율={aliveEndRate:F1}%");
                }
            }
        }

        private static bool HasAlive(List<BattleSimUnit> side) => side.Any(u => u.IsAlive);

        private static int GetTotalHp(List<BattleSimUnit> side) => side.Where(u => u.IsAlive).Sum(u => u.CurrentHP);

        /// <summary>살아 있는 유닛만 대상으로 ΣCurrentHP / ΣMaxHP (0~1).</summary>
        private static float GetAliveHpPoolRatio(List<BattleSimUnit> side)
        {
            int cur = 0, maxSum = 0;
            foreach (var u in side)
            {
                if (!u.IsAlive) continue;
                cur += u.CurrentHP;
                maxSum += Mathf.Max(1, u.MaxHP);
            }
            return maxSum > 0 ? (float)cur / maxSum : 1f;
        }

        /// <summary>(적 HP 풀 비율 - 아군 HP 풀 비율) &gt;= <paramref name="gapThreshold"/> 일 때만 “불리(도망 판정)”.</summary>
        private static bool IsAllyHpPoolDisadvantagedVsEnemy(List<BattleSimUnit> allies, List<BattleSimUnit> enemies, float gapThreshold)
        {
            float ra = GetAliveHpPoolRatio(allies);
            float re = GetAliveHpPoolRatio(enemies);
            return (re - ra) >= gapThreshold - 1e-4f;
        }

        private static List<BattleSimUnit> BuildAlliesFromRoster(BattleSimAllyRoster roster)
        {
            var list = new List<BattleSimUnit>();
            var ordered = roster.GetOrderedAllies();
            for (int i = 0; i < ordered.Length; i++)
            {
                var so = ordered[i];
                if (so == null) continue;
                int slotNum = i + 1;
                var slot = (BattleSlot)slotNum;
                list.Add(new BattleSimUnit
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
                    SimSkills = CopySkillList(so.simSkills),
                    MemorySlot1 = so.memorySlot1,
                    MemorySlot2 = so.memorySlot2,
                    MemorySlot3 = so.memorySlot3,
                    SimCharacterClass = so.characterClass
                });
            }
            return list;
        }

        private static List<BattleSimUnit> BuildEnemiesFromRoster(BattleSimEnemyRoster roster)
        {
            var list = new List<BattleSimUnit>();
            var ordered = roster.GetOrderedEnemyUnits();
            for (int i = 0; i < ordered.Length; i++)
            {
                var so = ordered[i];
                if (so == null) continue;
                int slotNum = i + 1;
                var slot = (BattleSlot)slotNum;
                list.Add(new BattleSimUnit
                {
                    Team = BattleSimTeam.Enemy,
                    DisplayName = so.displayName,
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
                    SimAiPattern = so.aiPattern,
                    SimSkills = CopySkillList(so.simSkills)
                });
            }
            return list;
        }

        private static List<SkillData> CopySkillList(List<SkillData> src)
        {
            var dst = new List<SkillData>();
            if (src == null) return dst;
            foreach (var s in src)
            {
                if (s != null) dst.Add(s);
            }
            return dst;
        }

        /// <summary>실전 전투와 동일: <c>floor(maxHP × hpCostPercent/100)</c>.</summary>
        private static int GetSimSkillHpCost(BattleSimUnit unit, SkillData skill)
        {
            if (unit == null || skill == null || skill.hpCostPercent <= 0f) return 0;
            return Mathf.FloorToInt(unit.MaxHP * (skill.hpCostPercent / 100f));
        }

        /// <summary>MP 잔량 + HP가 코스트 초과분을 남길 수 있는지(실전 UsePlayerSkill과 동일).</summary>
        private static bool CanAffordSimSkillCost(BattleSimUnit unit, SkillData skill)
        {
            if (unit == null || skill == null) return false;
            int mp = Mathf.Max(0, skill.mpCost);
            if (unit.CurrentMP < mp) return false;
            int hpCost = GetSimSkillHpCost(unit, skill);
            if (hpCost > 0 && unit.CurrentHP <= hpCost) return false;
            return true;
        }

        private static void ApplySimSkillCost(BattleSimUnit unit, SkillData skill, DungeonMidBattleConsumableContext dungeonCx = null)
        {
            if (unit == null || skill == null) return;
            int mp = Mathf.Max(0, skill.mpCost);
            if (mp > 0)
                unit.CurrentMP = Mathf.Max(0, unit.CurrentMP - mp);
            int hpCost = GetSimSkillHpCost(unit, skill);
            if (hpCost > 0)
            {
                int before = unit.CurrentHP;
                unit.CurrentHP = Mathf.Max(0, unit.CurrentHP - hpCost);
                if (dungeonCx != null && unit.Team == BattleSimTeam.Ally && before > unit.CurrentHP)
                    DungeonSimTryConsumablesAfterAllyHpReduced(unit, dungeonCx);
            }
        }

        private static bool DoesSkillHaveSimEffect(SkillData s)
        {
            if (s == null) return false;
            if (s.isRecovery) return true;
            if (s.IsDamaging) return true;
            if (s.damageType == DamageType.Physical || s.damageType == DamageType.Magic) return true;
            if (s.Effects != null)
            {
                foreach (var e in s.Effects)
                {
                    if (e != null && e.effectType == EffectType.Recovery) return true;
                }
            }

            return false;
        }

        private static bool SkillHasRecoveryForSim(SkillData skill)
        {
            if (skill.isRecovery) return true;
            if (skill.Effects == null) return false;
            foreach (var e in skill.Effects)
            {
                if (e != null && e.effectType == EffectType.Recovery) return true;
            }

            return false;
        }

        private static bool TryExecuteSimSkill(
            BattleSimUnit unit,
            List<BattleSimUnit> attackers,
            List<BattleSimUnit> defenders,
            System.Random rng,
            float critChanceBase,
            SlotBattleAccumulator acc,
            SimGlobalSkillStats skillStats,
            bool targetIsAlly,
            DungeonMidBattleConsumableContext dungeonCx)
        {
            var skill = PickRandomUsableSkill(unit, attackers, defenders, rng);
            if (skill == null) return false;

            var targets = ResolveSkillTargets(skill, unit, attackers, defenders);
            if (targets.Count == 0) return false;

            bool doesDamage = skill.IsDamaging || skill.damageType == DamageType.Physical || skill.damageType == DamageType.Magic;
            if (!skill.isRecovery && !doesDamage)
                return false;

            if (!CanAffordSimSkillCost(unit, skill)) return false;

            bool recoveryPath = SkillHasRecoveryForSim(skill);
            int hpBucketBeforeCost = GetSimCasterHpPctBucket(unit);

            if (skillStats != null)
            {
                skillStats.TotalSkillActivations++;
                RecordSimSkillTypeHistograms(skillStats, skill, hpBucketBeforeCost, recoveryPath);

                if (recoveryPath)
                {
                    skillStats.RecoveryActivations++;
                    if (unit.Team == BattleSimTeam.Ally)
                        skillStats.RecoveryCastByAlly++;
                    else
                        skillStats.RecoveryCastByEnemy++;

                    skillStats.RecoveryCasterHpPctBucket[hpBucketBeforeCost]++;
                }
            }

            ApplySimSkillCost(unit, skill, dungeonCx);

            if (SkillHasRecoveryForSim(skill))
            {
                ApplySimRecoveryFromSkill(skill, targets);
                return true;
            }

            int scaled = BattleSimCombatMath.GetSimSkillScaledStat(unit, skill);
            int hitCount = Mathf.Max(1, skill.hitCount);

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                int si = SlotIndex(target.Slot);
                if (si < 1 || si > 4) continue;

                for (int h = 0; h < hitCount; h++)
                {
                    if (!target.IsAlive) break;

                    if (targetIsAlly)
                        acc.AllyDefenderAttackAttempts[si]++;
                    else
                        acc.EnemyDefenderAttackAttempts[si]++;

                    bool hit = BattleSimCombatMath.RollHit(
                        skill,
                        unit.Agility,
                        target.Agility,
                        unit.Luck,
                        0f,
                        0f,
                        target.Slot,
                        rng);

                    if (!hit) continue;

                    if (targetIsAlly)
                        acc.AllyDefenderHits[si]++;
                    else
                        acc.EnemyDefenderHits[si]++;

                    bool crit = BattleSimCombatMath.RollCritical(unit.Luck, critChanceBase, rng, skill.critBonusPercent);
                    int dmg = BattleSimCombatMath.CalculateSimSkillDamage(skill, scaled, target.Defense, crit, target.Slot, rng);
                    int applied = ApplyPhysicalDamageWithSimGuard(target, dmg, targetIsAlly, dungeonCx);
                    if (targetIsAlly)
                        acc.AllyDefenderDamageTaken[si] += applied;
                    else
                        acc.EnemyDefenderDamageTaken[si] += applied;
                }
            }

            return true;
        }

        private static SkillData PickRandomUsableSkill(
            BattleSimUnit unit,
            List<BattleSimUnit> attackers,
            List<BattleSimUnit> defenders,
            System.Random rng)
        {
            if (unit.SimSkills == null || unit.SimSkills.Count == 0) return null;

            var usable = new List<SkillData>();
            foreach (var s in unit.SimSkills)
            {
                if (s == null || !s.IsActive) continue;
                if (!CanAffordSimSkillCost(unit, s)) continue;
                if (!s.targeting.CanCastFrom(unit.Slot)) continue;

                if (!DoesSkillHaveSimEffect(s)) continue;

                if (ResolveSkillTargets(s, unit, attackers, defenders).Count == 0) continue;
                usable.Add(s);
            }

            if (usable.Count == 0) return null;

            if (SimAiPrefersRecoveryOnly(unit))
            {
                var recoveryOnly = new List<SkillData>();
                foreach (var s in usable)
                {
                    if (SkillHasRecoveryForSim(s))
                        recoveryOnly.Add(s);
                }

                if (recoveryOnly.Count > 0)
                    return recoveryOnly[rng.Next(recoveryOnly.Count)];
            }

            return usable[rng.Next(usable.Count)];
        }

        /// <summary>시뮬 AI: 체력이 절반 이하이면 회복 마법(Recovery 이펙트) 위주로 고릅니다.</summary>
        private static bool SimAiPrefersRecoveryOnly(BattleSimUnit unit)
        {
            if (unit == null || unit.MaxHP <= 0) return false;
            return unit.CurrentHP / (float)unit.MaxHP <= 0.5f;
        }

        private static int GetSimCasterHpPctBucket(BattleSimUnit unit)
        {
            if (unit == null || unit.MaxHP <= 0) return 0;
            float hpPct = 100f * (unit.CurrentHP / (float)unit.MaxHP);
            return Mathf.Clamp(Mathf.FloorToInt(hpPct / 10f), 0, 9);
        }

        private static void RecordSimSkillTypeHistograms(
            SimGlobalSkillStats st,
            SkillData skill,
            int hpBucketBeforeCost,
            bool recoveryPath)
        {
            if (st == null || skill == null) return;
            int b = Mathf.Clamp(hpBucketBeforeCost, 0, 9);
            switch (skill.damageType)
            {
                case DamageType.Physical:
                    st.CastsDamageTypePhysical++;
                    st.HpPctBucketPhysical[b]++;
                    break;
                case DamageType.Magic:
                    st.CastsDamageTypeMagic++;
                    st.HpPctBucketMagic[b]++;
                    break;
                default:
                    st.CastsDamageTypeNone++;
                    st.HpPctBucketNone[b]++;
                    break;
            }

            if (recoveryPath)
            {
                DamageType recoveryRow = skill.damageType == DamageType.None
                    ? s_recoveryReportFallbackWhenNone
                    : skill.damageType;
                switch (recoveryRow)
                {
                    case DamageType.Physical:
                        st.RecoveryBranchPhysical++;
                        st.RecoveryBranchHpPhysical[b]++;
                        break;
                    case DamageType.Magic:
                        st.RecoveryBranchMagic++;
                        st.RecoveryBranchHpMagic[b]++;
                        break;
                    default:
                        st.RecoveryBranchNone++;
                        st.RecoveryBranchHpNone[b]++;
                        break;
                }
            }
            else
            {
                switch (skill.damageType)
                {
                    case DamageType.Physical:
                        st.DamageBranchPhysical++;
                        st.DamageBranchHpPhysical[b]++;
                        break;
                    case DamageType.Magic:
                        st.DamageBranchMagic++;
                        st.DamageBranchHpMagic[b]++;
                        break;
                    default:
                        st.DamageBranchNone++;
                        st.DamageBranchHpNone[b]++;
                        break;
                }
            }
        }

        private static List<BattleSimUnit> ResolveSkillTargets(
            SkillData skill,
            BattleSimUnit caster,
            List<BattleSimUnit> attackers,
            List<BattleSimUnit> defenders)
        {
            var result = new List<BattleSimUnit>();
            var t = skill.targeting;
            if (!t.CanCastFrom(caster.Slot)) return result;

            if (t.targetFaction == TargetFaction.Self)
            {
                if (caster.IsAlive) result.Add(caster);
                return result;
            }

            foreach (var slot in t.GetTargetSlots())
            {
                BattleSimUnit u = null;
                if (t.targetFaction == TargetFaction.Enemy)
                    u = defenders.FirstOrDefault(x => x.IsAlive && x.Slot == slot);
                else if (t.targetFaction == TargetFaction.Ally)
                    u = attackers.FirstOrDefault(x => x.IsAlive && x.Slot == slot);
                else
                {
                    u = attackers.FirstOrDefault(x => x.IsAlive && x.Slot == slot);
                    if (u == null)
                        u = defenders.FirstOrDefault(x => x.IsAlive && x.Slot == slot);
                }

                if (u != null && !result.Contains(u))
                    result.Add(u);
            }

            return result;
        }

        private static void ApplySimRecoveryFromSkill(SkillData skill, List<BattleSimUnit> targets)
        {
            if (skill.Effects == null) return;

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                foreach (var eff in skill.Effects)
                {
                    if (eff == null || eff.effectType != EffectType.Recovery) continue;
                    int amt = Mathf.RoundToInt(eff.effectAmount);
                    if (amt <= 0) continue;

                    switch (eff.recoveryTarget)
                    {
                        case RecoveryTarget.HP:
                            target.CurrentHP = Mathf.Min(target.MaxHP, target.CurrentHP + amt);
                            break;
                        case RecoveryTarget.MP:
                            target.CurrentMP = Mathf.Min(target.MaxMP, target.CurrentMP + amt);
                            break;
                        case RecoveryTarget.Both:
                            target.CurrentHP = Mathf.Min(target.MaxHP, target.CurrentHP + amt);
                            target.CurrentMP = Mathf.Min(target.MaxMP, target.CurrentMP + amt);
                            break;
                    }
                }
            }
        }
    }
}
