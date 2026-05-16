using System.Collections.Generic;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬레이터 — 층별 몬스터 등장 풀(수동 지정).
    /// 한 항목 = 한 층의 등장 정책. 등장 풀이 없는 층은 인카운터를 건너뜁니다.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonSimMonsterPool",
        menuName = "Abyssdawn/Simulation/Dungeon Sim Monster Pool", order = 31)]
    public class DungeonSimMonsterPool : ScriptableObject
    {
        /// <summary>층 종류 — CSV의 floor_type 컬럼과 1:1 매핑.</summary>
        public enum FloorKind
        {
            Normal,
            Elite,
            Boss
        }

        /// <summary>층별 등장 항목 한 줄.</summary>
        [System.Serializable]
        public class FloorEntry
        {
            [Tooltip("적용 층 번호 (1-base)")]
            [Min(1)] public int floor = 1;

            [Tooltip("층 종류 (CSV 분류용)")]
            public FloorKind kind = FloorKind.Normal;

            [Tooltip("등장 몬스터 목록 (가중치 랜덤)")]
            public List<MonsterPick> monsters = new List<MonsterPick>();

            [Tooltip("적 파티 최소 마릿수 (1~4)")]
            [Range(1, 4)] public int partySizeMin = 1;

            [Tooltip("적 파티 최대 마릿수 (1~4). Boss 층은 1로 두는 것을 권장합니다.")]
            [Range(1, 4)] public int partySizeMax = 2;
        }

        [System.Serializable]
        public class MonsterPick
        {
            public MonsterSO monster;

            [Tooltip("이 층 안에서의 상대적 등장 가중치 (0 이하 → 등장 안 함)")]
            [Min(0f)] public float weight = 1f;
        }

        [Header("층별 등장 풀")]
        public List<FloorEntry> entries = new List<FloorEntry>();

        /// <summary>지정 층에 해당하는 항목을 반환합니다(없으면 null).</summary>
        public FloorEntry GetEntryForFloor(int floor)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].floor == floor)
                    return entries[i];
            }
            return null;
        }

        /// <summary>가중치 기반으로 1마리 뽑기.</summary>
        public MonsterSO PickWeighted(FloorEntry entry, System.Random rng)
        {
            if (entry == null || entry.monsters == null || entry.monsters.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < entry.monsters.Count; i++)
            {
                var m = entry.monsters[i];
                if (m == null || m.monster == null) continue;
                if (m.weight <= 0f) continue;
                totalWeight += m.weight;
            }
            if (totalWeight <= 0f) return null;

            float pick = (float)rng.NextDouble() * totalWeight;
            float acc = 0f;
            for (int i = 0; i < entry.monsters.Count; i++)
            {
                var m = entry.monsters[i];
                if (m == null || m.monster == null) continue;
                if (m.weight <= 0f) continue;
                acc += m.weight;
                if (pick <= acc) return m.monster;
            }
            return null;
        }

        /// <summary>지정 층 entry로부터 적 파티(여러 마리) 빌드.</summary>
        /// <param name="dungeonFloor">시뮬 던전 층(1-base). <paramref name="randomOneOrTwoEnemyPartyFromFloor"/>와 함께 쓰입니다.</param>
        /// <param name="randomOneOrTwoEnemyPartyFromFloor">이 값 이상 층이면서 <see cref="FloorKind.Boss"/>가 아니면 적 1~2마리 균등 랜덤(0이면 비활성).</param>
        public List<MonsterSO> BuildEnemyParty(FloorEntry entry, System.Random rng, int dungeonFloor = 0, int randomOneOrTwoEnemyPartyFromFloor = 0)
        {
            var list = new List<MonsterSO>();
            if (entry == null) return list;

            int sizeMin = Mathf.Clamp(entry.partySizeMin, 1, 4);
            int sizeMax = Mathf.Clamp(entry.partySizeMax, sizeMin, 4);

            if (randomOneOrTwoEnemyPartyFromFloor > 0
                && dungeonFloor >= randomOneOrTwoEnemyPartyFromFloor
                && entry.kind != FloorKind.Boss)
            {
                sizeMin = 1;
                sizeMax = 2;
            }

            int size = rng.Next(sizeMin, sizeMax + 1);

            for (int i = 0; i < size; i++)
            {
                var picked = PickWeighted(entry, rng);
                if (picked != null) list.Add(picked);
            }
            return list;
        }
    }
}
