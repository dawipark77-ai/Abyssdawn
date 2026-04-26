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
        [SerializeField] private string reportRelativePath = "Assets/Data/Simulation/_LastBattleSimReport.txt";

        [Header("행동 AI (시뮬)")]
        [Tooltip("아이템 행동 — 아직 미구현이면 끄세요.")]
        [SerializeField] private bool allowItemActions;
        [Tooltip("도망 행동 — 아직 미구현이면 끄세요.")]
        [SerializeField] private bool allowFleeActions;
        [SerializeField] private float simItemPickChance = 0.02f;
        [SerializeField] private float simFleePickChance = 0.03f;

        public bool HasRostersAssigned() => allyRoster != null && enemyRoster != null;

        /// <summary>에디터 커스텀 인스펙터에서 호출합니다.</summary>
        public void RunSimulation()
        {
            if (allyRoster == null || enemyRoster == null)
            {
                Debug.LogError("[BattleSimulator] Ally 또는 Enemy Roster가 비어 있습니다.");
                return;
            }
            if (iterations <= 0)
            {
                Debug.LogError("[BattleSimulator] iterations는 1 이상이어야 합니다.");
                return;
            }

            var acc = new SlotBattleAccumulator();
            int allyWins = 0;
            int enemyWins = 0;
            long turnSum = 0;

            for (int i = 0; i < iterations; i++)
            {
                var rng = new System.Random(baseSeed + i);
                int turns = RunSingleBattle(rng, criticalChanceBase, acc, allowItemActions, allowFleeActions, simItemPickChance, simFleePickChance, out bool alliesVictory);
                turnSum += turns;
                if (alliesVictory) allyWins++;
                else enemyWins++;
            }

            float avgTurns = iterations > 0 ? (float)turnSum / iterations : 0f;
            var sb = new StringBuilder();
            sb.AppendLine($"=== Battle Simulation ({iterations} runs, seed base {baseSeed}) ===");
            sb.AppendLine($"Ally wins: {allyWins} ({100f * allyWins / iterations:F1}%)");
            sb.AppendLine($"Enemy wins: {enemyWins} ({100f * enemyWins / iterations:F1}%)");
            sb.AppendLine($"Avg turns: {avgTurns:F2} (max cap {maxTurnsPerBattle})");
            sb.AppendLine();
            sb.AppendLine("--- 구현 검증 (시뮬 전용 공식) ---");
            sb.AppendLine("명중: Base(스킬 accuracy 또는 1) × AGI보정(clamp(0.8+(Att-Def)*0.02,0.6,1.2)) × SlotHit × Equip(1) → clamp 0.45~0.95.");
            sb.AppendLine("딜: max(ATK×0.3, floor(ATK×1.6 − DEF×0.6)) × 피격슬롯(전열1~2:×0.90, 후열3~4:×1.10) × Crit(1.5 또는 1) × Equip(1).");
            sb.AppendLine();
            acc.AppendSlotReport(sb, iterations);
            sb.AppendLine();
            sb.AppendLine($"규칙: 양측 슬롯 순 행동. 방어=해당 가드 구간 동안 받는 피해×{BattleSimCombatMath.SimGuardIncomingDamageMultiplier:F2}({(1f - BattleSimCombatMath.SimGuardIncomingDamageMultiplier) * 100f:F0}% 경감), 아군가드=같은 라운드 적 페이즈, 적가드=다음 라운드 아군 페이즈, 중첩 없음.");
            sb.AppendLine("타겟: AIPattern별 — Aggressive=전열 가중 랜덤, Defensive=균등 랜덤, Support=후열 우선.");
            sb.AppendLine($"아이템 행동: {(allowItemActions ? "허용" : "비활성")}, 도망: {(allowFleeActions ? "허용" : "비활성")}.");

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
            bool allowItem,
            bool allowFlee,
            float itemChance,
            float fleeChance,
            out bool alliesVictory)
        {
            var allies = BuildAlliesFromRoster(allyRoster);
            var enemies = BuildEnemiesFromRoster(enemyRoster);
            var run = new SingleRunSlotTracker();

            for (int round = 0; round < maxTurnsPerBattle; round++)
            {
                if (!HasAlive(allies))
                {
                    alliesVictory = false;
                    ClearAllSimGuardFlags(allies, enemies);
                    run.CommitTo(acc, allies, enemies);
                    return round;
                }
                if (!HasAlive(enemies))
                {
                    alliesVictory = true;
                    ClearAllSimGuardFlags(allies, enemies);
                    run.CommitTo(acc, allies, enemies);
                    return round;
                }

                ExecuteSideTurn(allies, enemies, rng, critChanceBase, acc, targetIsAlly: false, allowItem, allowFlee, itemChance, fleeChance);
                ClearEnemyNextAllyPhaseGuards(enemies);
                if (!HasAlive(enemies))
                {
                    alliesVictory = true;
                    run.BumpPartialRoundAllyOnly(allies);
                    ClearAllSimGuardFlags(allies, enemies);
                    run.CommitTo(acc, allies, enemies);
                    return round + 1;
                }

                ExecuteSideTurn(enemies, allies, rng, critChanceBase, acc, targetIsAlly: true, allowItem, allowFlee, itemChance, fleeChance);
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

        private static void ExecuteSideTurn(
            List<BattleSimUnit> attackers,
            List<BattleSimUnit> defenders,
            System.Random rng,
            float critChanceBase,
            SlotBattleAccumulator acc,
            bool targetIsAlly,
            bool allowItem,
            bool allowFlee,
            float itemChance,
            float fleeChance)
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
                int applied = ApplyPhysicalDamageWithSimGuard(target, dmg, targetIsAlly);
                if (targetIsAlly)
                    acc.AllyDefenderDamageTaken[si] += applied;
                else
                    acc.EnemyDefenderDamageTaken[si] += applied;
            }
        }

        /// <param name="damageIsEnemyVsAlly"><c>ExecuteSideTurn</c>의 targetIsAlly와 동일(적이 아군을 칠 때 true).</param>
        private static int ApplyPhysicalDamageWithSimGuard(BattleSimUnit target, int damageAfterFormula, bool damageIsEnemyVsAlly)
        {
            int dmg = damageAfterFormula;
            if (damageIsEnemyVsAlly && target.SimGuardEnemyPhase)
                dmg = Mathf.FloorToInt(dmg * BattleSimCombatMath.SimGuardIncomingDamageMultiplier);
            else if (!damageIsEnemyVsAlly && target.SimGuardNextAllyPhase)
                dmg = Mathf.FloorToInt(dmg * BattleSimCombatMath.SimGuardIncomingDamageMultiplier);
            int before = target.CurrentHP;
            target.ApplyDamage(dmg);
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
                    SimAiPattern = so.simAiPattern
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
                    SimAiPattern = so.aiPattern
                });
            }
            return list;
        }
    }
}
