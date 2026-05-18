using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 플레이어 Lv1~10 EXP 누적/구간 표 (기획 고정). Lv10 이후는 마지막 구간(2100) 대비 ~1.31배씩 외삽.
    /// <para>누적: Lv2=100, Lv3=300, Lv4=600, Lv5=1050, Lv6=1700, Lv7=2600, Lv8=3800, Lv9=5400, Lv10=7500.</para>
    /// </summary>
    public static class PlayerExpProgression
    {
        /// <summary>인덱스 L = 현재 레벨, 값 = L→L+1 필요 EXP. 인덱스 0은 미사용.</summary>
        private static readonly int[] ExpToNextLevels1Through9 =
        {
            0,
            100,
            200,
            300,
            450,
            650,
            900,
            1200,
            1600,
            2100
        };

        const float PostTableGrowthRatio = 1.31f;

        /// <summary>레벨 <paramref name="currentLevel"/>에서 다음 레벨까지 필요한 EXP.</summary>
        public static int GetExpToNextLevel(int currentLevel)
        {
            int L = Mathf.Max(1, currentLevel);
            if (L <= 9)
                return Mathf.Max(1, ExpToNextLevels1Through9[L]);

            float step = 2100f;
            for (int i = 0; i < L - 9; i++)
                step *= PostTableGrowthRatio;
            return Mathf.Max(1, Mathf.RoundToInt(step));
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
