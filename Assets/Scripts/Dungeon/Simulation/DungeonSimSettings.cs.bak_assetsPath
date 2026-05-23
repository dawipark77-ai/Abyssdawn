using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬레이터 정책 SO.
    /// 한 회차(run) 동안 사용할 던전 진행 규칙(층수·이동 칸·인카운터·EXP·포션) 모음.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonSimSettings",
        menuName = "Abyssdawn/Simulation/Dungeon Sim Settings", order = 30)]
    public class DungeonSimSettings : ScriptableObject
    {
        [Header("층 / 반복")]
        [Tooltip("시뮬 1회차에서 도달 목표 층 수 (예: 10 = 1~10층)")]
        [Min(1)] public int floorCount = 10;

        [Tooltip("같은 조건으로 반복 실행할 회차 수")]
        [Min(1)] public int iterations = 200;

        [Tooltip("기본 랜덤 시드 — run i는 baseSeed + i를 사용")]
        public int baseSeed = 12345;

        [Header("층당 이동 칸")]
        [Tooltip("한 층에서 이동할 최소 칸 수")]
        [Min(1)] public int stepsPerFloorMin = 25;

        [Tooltip("한 층에서 이동할 최대 칸 수 (실제 던전 평균치 ≈ 25~40)")]
        [Min(1)] public int stepsPerFloorMax = 40;

        [Header("인카운터 (DungeonEncounter와 동일 정책)")]
        [Tooltip("이동 1칸마다 인카운터 발생 확률")]
        [Range(0f, 1f)] public float encounterChance = 0.15f;

        [Tooltip("전투 종료 후 인카운터가 발생하지 않는 이동 칸 수")]
        [Min(0)] public int postBattleCooldownSteps = 3;

        [Header("EXP 곡선 (실제 PlayerStats.LevelUp 공식과 동일)")]
        [Tooltip("레벨 N → N+1에 필요한 EXP = N × expPerLevelMultiplier")]
        [Min(1)] public int expPerLevelMultiplier = 100;

        [Tooltip("시작 레벨")]
        [Min(1)] public int startingLevel = 1;

        [Tooltip("시작 EXP")]
        [Min(0)] public int startingExp = 0;

        [Header("레벨업 평균 성장치 (Phase 1 단순화)")]
        [Tooltip("레벨업 시 maxHP 증가량 (모든 유닛에 동일 적용). 실전 PlayerStats.LevelUp의 룰렛은 Phase 2에서 이식.")]
        [Min(0)] public int levelUpHpGain = 8;

        [Tooltip("레벨업 시 maxMP 증가량")]
        [Min(0)] public int levelUpMpGain = 3;

        [Tooltip("레벨업 시 ATK 증가량 (평균)")]
        [Min(0)] public int levelUpAtkGain = 1;

        [Tooltip("레벨업 시 DEF 증가량 (평균)")]
        [Min(0)] public int levelUpDefGain = 1;

        [Tooltip("레벨업 시 MAG 증가량 (평균)")]
        [Min(0)] public int levelUpMagGain = 0;

        [Tooltip("레벨업 시 AGI 증가량 (평균)")]
        [Min(0)] public int levelUpAgiGain = 0;

        [Tooltip("레벨업 시 LUK 증가량 (평균)")]
        [Min(0)] public int levelUpLukGain = 0;

        [Header("포션 정책 (Phase 1 단순 버전)")]
        [Tooltip("시작 포션 개수 (HP 포션 한 종류)")]
        [Min(0)] public int startingHpPotionCount = 5;

        [Tooltip("포션 1회 회복량 (HP)")]
        [Min(0)] public int hpPotionHealAmount = 30;

        [Tooltip("HP 비율이 이 값 이하로 떨어지면 전투 전 포션 사용")]
        [Range(0f, 1f)] public float potionUseHpThreshold = 0.4f;

        [Header("출력")]
        [Tooltip("CSV 저장 경로 (프로젝트 루트 기준 상대)")]
        public string csvRelativePath = "DungeonSimLogs/DungeonSim_{TIMESTAMP}.csv";

        [Tooltip("요약 텍스트 저장 경로 (프로젝트 루트 기준 상대)")]
        public string summaryRelativePath = "DungeonSimLogs/_LastDungeonSimSummary.txt";

        [Header("안전 가드")]
        [Tooltip("한 층에서 발생할 수 있는 전투 수 상한 (무한 루프 방지)")]
        [Min(1)] public int maxBattlesPerFloor = 50;

        public int GetExpToNextLevel(int currentLevel)
        {
            return Mathf.Max(1, currentLevel) * expPerLevelMultiplier;
        }
    }
}
