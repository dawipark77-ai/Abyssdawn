using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 플레이어 Lv1~10 EXP 누적/구간 표. Lv10 이후는 마지막 구간(2000) 대비 ~1.31배씩 외삽.
    /// [2026-10-12] 드퀘식 곡선으로 초반을 낮춤 (첫 레벨업 약 3전투, Lv10 은 10층 근처 약 48전투 — 추정).
    ///   예전 100/200/300/450/650/900/1200/1600/2100 → 70/150/260/400/600/850/1150/1500/2000
    ///   몬스터 EXP 는 레벨 차이 보정(스톤샤드식, <see cref="LevelDiffMultiplier"/>)을 받는다.
    /// <para>누적: Lv2=70, Lv3=220, Lv4=480, Lv5=880, Lv6=1480, Lv7=2330, Lv8=3480, Lv9=4980, Lv10=6980.</para>
    /// </summary>
    public static class PlayerExpProgression
    {
        /// <summary>인덱스 L = 현재 레벨, 값 = L→L+1 필요 EXP. 인덱스 0은 미사용.</summary>
        private static readonly int[] ExpToNextLevels1Through9 =
        {
            0,
            70,
            150,
            260,
            400,
            600,
            850,
            1150,
            1500,
            2000
        };

        const float PostTableGrowthRatio = 1.31f;

        /// <summary>레벨 <paramref name="currentLevel"/>에서 다음 레벨까지 필요한 EXP.</summary>
        public static int GetExpToNextLevel(int currentLevel)
        {
            int L = Mathf.Max(1, currentLevel);
            if (L <= 9)
                return Mathf.Max(1, ExpToNextLevels1Through9[L]);

            float step = 2000f;
            for (int i = 0; i < L - 9; i++)
                step *= PostTableGrowthRatio;
            return Mathf.Max(1, Mathf.RoundToInt(step));
        }

        /// <summary>
        /// [2026-10-12] 스톤샤드식 레벨 차이 보정: (몬스터 레벨 − 내 레벨)에 따라 받는 EXP 배율.
        /// 강한 적은 더 주고(+3 이상 ×1.5), 약한 적은 덜 준다(−4 이하 ×0.3) — 노가다는 되지만 깊이 내려갈수록 이득.
        /// </summary>
        public static float LevelDiffMultiplier(int monsterLevel, int playerLevel)
        {
            int d = monsterLevel - playerLevel;
            if (d >= 3) return 1.5f;
            if (d == 2) return 1.3f;
            if (d == 1) return 1.15f;
            if (d == 0) return 1f;
            if (d == -1) return 0.85f;
            if (d == -2) return 0.7f;
            if (d == -3) return 0.5f;
            return 0.3f;
        }

        /// <summary>해당 레벨 구간 시작 시점의 누적 EXP (Lv1 시작 = 0).</summary>
        public static int GetCumulativeExpAtLevelStart(int level)
        {
            int L = Mathf.Max(1, level);
            int sum = 0;
            for (int cur = 1; cur < L; cur++)
                sum += GetExpToNextLevel(cur);
            return sum;
        }
    }
}
