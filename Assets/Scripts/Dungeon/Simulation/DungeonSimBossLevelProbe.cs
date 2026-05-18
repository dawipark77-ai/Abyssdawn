using System.IO;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 최종 층 보스(예: 3층 고블린) 대비 — 시뮬 <see cref="DungeonSimulator.ApplyLevelUp"/> 규칙으로 올린 레벨별
    /// 1vs1 반복 전투 승률을 측정합니다. 던전 전체가 아니라 “이 레벨이면 몇 % 이기나”만 봅니다.
    /// </summary>
    [AddComponentMenu("Abyssdawn/Dungeon Sim Boss Level Probe")]
    public class DungeonSimBossLevelProbe : MonoBehaviour
    {
        [Header("입력 (DungeonSimulator와 동일 구성)")]
        [SerializeField] private DungeonSimSettings settings;
        [SerializeField] private BattleSimAllyRoster allyRoster;
        [SerializeField] private DungeonSimMonsterPool monsterPool;
        [SerializeField] private BattleSimulator battleSimulator;

        [Header("보스 층")]
        [Tooltip("MonsterPool entries의 floor 번호 (예: 3층 고블린)")]
        [Min(1)] public int bossFloor = 3;

        [Header("스윕 범위")]
        [Min(1)] public int levelMin = 1;
        [Min(1)] public int levelMax = 25;
        [Min(1)] public int battlesPerLevel = 300;
        public int baseSeed = 77777;

        [Tooltip("이 승률(포함) 이상인 가장 낮은 레벨을 요약에 강조")]
        [Range(0f, 1f)] public float highlightWinRateAtOrAbove = 0.5f;

        [Header("전투 조건")]
        [Tooltip("켜면 던전 시뮬과 같이 전투 중 포션·잔·약초 사용. 끄면 맨몸에 가깝게 순수 전투만.")]
        public bool useDungeonConsumablesInProbe = false;

        public bool allowFleeInProbe = false;

        [Header("출력")]
        [Tooltip("프로젝트 루트 기준 상대 경로. 비우면 파일 저장 안 함.")]
        public string probeReportRelativePath = "Assets/Data/Simulation/_LastBossLevelProbe.txt";

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

        /// <summary>에디터 버튼에서 호출합니다.</summary>
        public void RunBossLevelProbe()
        {
            if (!IsReadyToRun())
            {
                Debug.LogError("[BossLevelProbe] Settings / AllyRoster / MonsterPool / BattleSimulator 를 채우세요.");
                return;
            }

            var bm = ResolveBattleSimulator();
            var entry = monsterPool.GetEntryForFloor(bossFloor);
            if (entry == null)
            {
                Debug.LogError($"[BossLevelProbe] MonsterPool에 floor {bossFloor} 항목이 없습니다.");
                return;
            }

            int lo = Mathf.Min(levelMin, levelMax);
            int hi = Mathf.Max(levelMin, levelMax);
            int bpl = Mathf.Max(1, battlesPerLevel);

            var sb = new StringBuilder();
            sb.AppendLine("=== Boss Level Probe (Dungeon Sim) ===");
            sb.AppendLine($"시각: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"보스 층: {bossFloor}, floor_type={entry.kind}, battles/level={bpl}, seed={baseSeed}");
            sb.AppendLine($"레벨 성장: CreateSimPlayerAtLevel → DungeonSimulator.ApplyLevelUp (Settings 동일)");
            sb.AppendLine($"소모품 mid-battle: {(useDungeonConsumablesInProbe ? "ON" : "OFF")}, 도망 허용: {allowFleeInProbe}");
            sb.AppendLine();

            int firstHighlight = -1;
            for (int L = lo; L <= hi; L++)
            {
                int growthSeed = unchecked(baseSeed + L * 7919);
                int wins = 0;
                for (int i = 0; i < bpl; i++)
                {
                    var rng = new System.Random(unchecked(baseSeed + L * 130051 + i * 17));
                    var player = DungeonSimulator.CreateSimPlayerAtLevel(allyRoster, settings, L, growthSeed);
                    if (player == null || !player.AnyAlive()) continue;

                    var enemyParty = monsterPool.BuildEnemyParty(entry, rng);
                    if (enemyParty == null || enemyParty.Count == 0) continue;
                    var enemies = DungeonSimulator.BuildEnemyUnitsFromMonsterSOs(enemyParty);
                    if (enemies == null || enemies.Count == 0) continue;

                    DungeonSimPlayer cx = useDungeonConsumablesInProbe ? player : null;
                    DungeonSimSettings cs = useDungeonConsumablesInProbe ? settings : null;
                    var outcome = bm.RunOneBattleForDungeon(player.Units, enemies, rng, allowFleeInProbe, cx, cs);
                    if (outcome.AllyWin) wins++;
                }

                double p = 100.0 * wins / bpl;
                sb.AppendLine($"Lv{L,3}: 승 {wins,4}/{bpl} ({p:F1}%)");
                if (firstHighlight < 0 && wins >= bpl * highlightWinRateAtOrAbove)
                    firstHighlight = L;
            }

            sb.AppendLine();
            if (firstHighlight > 0)
                sb.AppendLine(
                    $"▶ 승률 {highlightWinRateAtOrAbove:P0} 이상인 가장 낮은 레벨: Lv{firstHighlight} (동일 레벨업 RNG 시드: baseSeed+Lv*7919)");
            else
                sb.AppendLine($"▶ Lv{lo}~{hi} 구간에서 승률 {highlightWinRateAtOrAbove:P0} 이상인 레벨 없음 — 상한 확대·전투 수·보스 스탯을 확인하세요.");

            string report = sb.ToString();
            Debug.Log(report);

            if (!string.IsNullOrEmpty(probeReportRelativePath))
            {
                string abs = ResolveProjectRelativeAbs(probeReportRelativePath);
                try
                {
                    var dir = Path.GetDirectoryName(abs);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(abs, report, Encoding.UTF8);
                    Debug.Log($"[BossLevelProbe] Report written: {abs}");
#if UNITY_EDITOR
                    if (abs.Replace('\\', '/').Contains("/Assets/"))
                        UnityEditor.AssetDatabase.Refresh();
#endif
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BossLevelProbe] Write failed: {e.Message}");
                }
            }
        }

        private static string ResolveProjectRelativeAbs(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            string norm = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", norm));
        }
    }
}
