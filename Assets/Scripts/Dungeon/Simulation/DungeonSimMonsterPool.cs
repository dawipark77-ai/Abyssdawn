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

            [Tooltip("미사용 — 던전 시뮬 Phase1은 인카운터마다 항상 적 1마리(1대1)만 스폰합니다.")]
            [Range(1, 4)] public int partySizeMin = 1;

            [Tooltip("미사용 — 던전 시뮬 Phase1은 인카운터마다 항상 적 1마리(1대1)만 스폰합니다.")]
            [Range(1, 4)] public int partySizeMax = 1;
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

        /// <summary>인카운터당 적 1마리(1대1 시뮬) — <see cref="PickWeighted"/>.</summary>
        public List<MonsterSO> BuildEnemyParty(FloorEntry entry, System.Random rng)
        {
            var list = new List<MonsterSO>();
            if (entry == null) return list;
            var picked = PickWeighted(entry, rng);
            if (picked != null) list.Add(picked);
            return list;
        }

        /// <summary>
        /// 해당 층 풀에 등록된 몬스터 스탯으로 ‘이 층에 들어가기 전’ 권장 최소 레벨을 추정합니다.
        /// <see cref="DungeonSimSettings.GetEffectiveMinLevelToEnterFloor"/>에서 수동 게이트와 병합합니다.
        /// </summary>
        public int GetRecommendedMinLevelToEnterFloor(int floor)
        {
            var entry = GetEntryForFloor(floor);
            if (entry == null || entry.monsters == null || entry.monsters.Count == 0) return 1;

            int best = 1;
            for (int i = 0; i < entry.monsters.Count; i++)
            {
                var pick = entry.monsters[i];
                if (pick == null || pick.monster == null || pick.weight <= 0f) continue;
                best = Mathf.Max(best, EstimateRecommendedLevelForMonster(pick.monster));
            }

            float kindMul = entry.kind == FloorKind.Boss ? 1.35f
                : entry.kind == FloorKind.Elite ? 1.15f
                : 1f;
            return Mathf.Max(1, Mathf.CeilToInt(best * kindMul));
        }

        /// <summary>단일 몬스터 기준 권장 플레이어 레벨(>=1).</summary>
        public static int EstimateRecommendedLevelForMonster(MonsterSO m)
        {
            if (m == null) return 1;
            float strike = Mathf.Max(m.ATK, m.MAG);
            float bulk = (m.HP + m.DEF) / 12f;
            float threat = strike + bulk * 0.35f;
            int fromStats = Mathf.Max(1, Mathf.CeilToInt(threat / 9f));
            return Mathf.Max(fromStats, m.MonsterLevel);
        }
    }
}
