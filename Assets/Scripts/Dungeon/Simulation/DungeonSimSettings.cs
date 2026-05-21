using System.Collections.Generic;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>층 진입(또는 다음 층으로 넘어가기) 전 AI가 참고하는 권장 레벨.</summary>
    [System.Serializable]
    public class DungeonSimFloorLevelGate
    {
        [Tooltip("이 층에 ‘처음 들어갈’ 때 요구되는 최소 레벨(다음 층이 이 번호면, 직전 층에서 파밍).")]
        [Min(1)] public int floor = 1;

        [Min(1)] public int minLevelToEnter = 1;

        [Tooltip("기획 참고용 — 진행 판정에는 사용하지 않습니다.")]
        [Min(1)] public int maxRecommendedLevel = 99;
    }

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

        [Header("전투 — 동료화(시뮬)")]
        [Tooltip("승리 시 전투당 1회만, 해당 전투 적 목록에서 무작위 1체를 골라 MonsterSO.CompanionChance·CompanionSO로 동료 합류를 시뮬합니다. 성공 시 CompanionSO 스탯으로 BattleSimUnit이 파티에 추가됩니다(몬스터 동료 최대 3, 아군 슬롯 총 최대 4).")]
        public bool simCompanionRollAfterVictoryEnabled = true;

        [Tooltip("켜면 승리·레벨업 처리 직후, 몬스터 동료의 MonsterLevel이 플레이어보다 simCompanionReplaceMinLevelGap 이상 낮으면 파티에서 제거하고, 같은 전투 적 목록에서 동료화 가능 몬스터로 교체 영입을 시도합니다(CompanionChance 없음). 아군 4·몬스터 동료 3 상한은 유지.")]
        public bool simCompanionAutoReplaceWhenLevelLagEnabled = true;

        [Tooltip("플레이어 레벨 − 동료 MonsterLevel ≥ 이 값이면 교체 대상(기본 2 = 플레이어보다 2 이상 낮으면 교체).")]
        [Min(2)] public int simCompanionReplaceMinLevelGap = 2;

        [Header("전투 — 적 파티 규모(시뮬)")]
        [Tooltip("켜면 인카운터마다 적을 항상 4명으로 맞춥니다. 부족하면 해당 층 풀에서 가중 추첨으로 보충합니다.")]
        public bool simAlwaysFourEnemyUnits = true;

        [Header("던전 이동 중 회복 (시뮬)")]
        [Tooltip("이동 칸 처리 중에도 확률/주기로 회복 루프를 돌립니다.")]
        public bool dungeonStepHealEnabled = true;

        [Tooltip("이동 1칸마다 이 확률로 회복 시도(0이면 무시). 주기 조건과 OR.")]
        [Range(0f, 1f)] public float stepHealRollChance = 0.08f;

        [Tooltip("N칸마다 1회 회복 시도. 0이면 주기만 끔.")]
        [Min(0)] public int stepHealPeriodicN = 6;

        [Header("EXP 곡선 (PlayerExpProgression과 동일)")]
        [Tooltip("구버전 에셋 직렬화 호환용 — 무시됩니다. 레벨업 EXP는 PlayerExpProgression 표를 사용합니다.")]
        [HideInInspector]
        [Min(1)] public int expPerLevelMultiplier = 100;

        [Tooltip("시작 레벨")]
        [Min(1)] public int startingLevel = 1;

        [Tooltip("시작 EXP")]
        [Min(0)] public int startingExp = 0;

        [Tooltip("던전 시뮬 전용 — 승리 시 획득 EXP(몬스터 ExpReward 합)에 곱합니다. 기본 1 = 인게임과 동일(몬스터 보상 그대로). 0 이하이면 1로 처리합니다.")]
        [Min(0f)] public float simExpRewardMultiplier = 1f;

        [Header("레벨업 (던전 시뮬) — 고정치 + 종의 기억")]
        [Tooltip("CharacterClass 미지정 시에만 사용 — 레벨당 maxHP 기본 증가. 지정 시에는 직업 hpPerLevel이 대신 쓰입니다.")]
        [Min(0)] public int levelUpHpGain = 8;

        [Tooltip("CharacterClass 미지정 시에만 사용 — 레벨당 maxMP 기본 증가.")]
        [Min(0)] public int levelUpMpGain = 3;

        [Tooltip("던전 시뮬 ApplyLevelUp에서 스탯 고정 증가에는 사용하지 않음(스탯은 랜덤 +2만).")]
        [Min(0)] public int levelUpAtkGain = 1;

        [Tooltip("던전 시뮬 ApplyLevelUp에서 스탯 고정 증가에는 사용하지 않음(스탯은 랜덤 +2만).")]
        [Min(0)] public int levelUpDefGain = 1;

        [Tooltip("던전 시뮬 ApplyLevelUp에서 스탯 고정 증가에는 사용하지 않음(스탯은 랜덤 +2만).")]
        [Min(0)] public int levelUpMagGain = 0;

        [Tooltip("던전 시뮬 ApplyLevelUp에서 스탯 고정 증가에는 사용하지 않음(스탯은 랜덤 +2만).")]
        [Min(0)] public int levelUpAgiGain = 0;

        [Tooltip("던전 시뮬 ApplyLevelUp에서 스탯 고정 증가에는 사용하지 않음(스탯은 랜덤 +2만).")]
        [Min(0)] public int levelUpLukGain = 0;

        [Header("HP 회복 임계 (던전 시뮬)")]
        [Tooltip("전원이 이 HP 비율 이상이 될 때까지 소모품을 반복 사용(유지 목표, 예: 0.7 = 70% 미만이면 계속).")]
        [Range(0.01f, 1f)] public float healTargetHpRatio = 0.70f;

        [Tooltip("포션 1회 적용 대상: 현재 HP/MaxHP가 이 값 미만인 유닛.")]
        [Range(0.01f, 1f)] public float potionHealHpRatio = 0.70f;

        [Tooltip("일반(비긴급) 시 약초 대상: 이 비율 미만인 유닛만 약초(저HP 구간 보존).")]
        [Range(0.01f, 1f)] public float herbUseHpRatio = 0.40f;

        [Tooltip("긴급 회복: 누군가 이 비율 미만이면 포션→잔→약초 순(약초 우선순위 역전).")]
        [Range(0.01f, 1f)] public float emergencyHealHpRatio = 0.25f;

        [Tooltip("시작 일반 HP 포션 개수")]
        [Min(0)] public int startingHpPotionCount = 5;

        [Tooltip("일반 HP 포션 1회 회복량 (절대값)")]
        [Min(0)] public int hpPotionHealAmount = 30;

        [Tooltip("레거시 — healTargetHpRatio가 0에 가까울 때만 대체로 사용됩니다.")]
        [Range(0f, 1f)] public float potionUseHpThreshold = 0.70f;

        [Header("Medicinal Herb (약초) — 시뮬 전용")]
        [Tooltip("비우면 아래 Fallback 범위로 회복량을 계산합니다.")]
        public MedicinalHerbSimData medicinalHerbData;

        [Tooltip("medicinalHerbData가 없을 때 최소 HP 회복")]
        [Min(0)] public int medicinalHerbHealMinFallback = 32;

        [Tooltip("medicinalHerbData가 없을 때 최대 HP 회복(포함)")]
        [Min(0)] public int medicinalHerbHealMaxFallback = 35;

        [Tooltip("약초를 이 층(포함)부터")]
        [Min(1)] public int medicinalHerbGrantFloorsMin = 1;

        [Tooltip("약초를 이 층(포함)까지 진입 시 지급")]
        [Min(1)] public int medicinalHerbGrantFloorsMax = 5;

        [Tooltip("지급 층 범위 진입 시 추가 약초 — 최소(포함). Max와 같으면 고정.")]
        [Min(0)] public int medicinalHerbGrantCountMin = 0;

        [Tooltip("지급 층 범위 진입 시 추가 약초 — 최대(포함). Min~Max 균등 랜덤.")]
        [Min(0)] public int medicinalHerbGrantCountMax = 2;

        [Header("1층 마을 (시뮬 전용) — 경제 없음")]
        [Tooltip("켜면 매 회차 1층 진입 시 아군 HP·MP 최대로, HP포션·새벽의 잔을 시작 스택으로 맞춥니다. 약초는 아래 스택이 1 이상일 때만 덮어쓰며, 기본은 층 진입 지급(약초 범위)만 사용합니다.")]
        public bool floor1TownFullRestoreEnabled = true;

        [Tooltip("0이면 마을이 약초 개수를 건드리지 않습니다(층 진입 0~N 지급만). 1 이상이면 마을 적용 시마다 이 개수로 덮어씁니다.")]
        [Min(0)] public int floor1TownMedicinalHerbStack = 0;

        [Tooltip("1층 + 1층 마을 켜짐일 때만 — 전투 승리 직후 마을과 동일 풀 HP/MP·소모품 재충전(귀환 노가다 시뮬).")]
        public bool floor1TownAfterVictoryEnabled = true;

        [Header("도망 (Flee) 정책 — BattleSimulator의 fleeWhenHpDisadvantaged와 함께 작동")]
        [Tooltip("한 층에서 허용되는 최대 도망 횟수. 이 횟수에 도달하면 이후 전투에서는 HP 격차 도망이 차단됩니다.")]
        [Min(0)] public int maxFleesPerFloor = 2;

        [Header("AI 진행 (던전 시뮬)")]
        [Tooltip("켜면: 전투 전 HP·마을 귀환 판정, 다음 층 권장 레벨 미달 시 같은 층 추가 패스(파밍)를 시도합니다.")]
        public bool aiProgressionEnabled = false;

        [Tooltip("전투 직전: 아군 현재 HP 합이 (적 ATK 최대값 × 이 턴 수) 미만이면 마을 풀충전을 반복한 뒤 재판정. 여전히 미만이면 전투 진행.")]
        [Min(1)] public int aiSurvivalEnemyTurnCount = 3;

        [Tooltip("한 인카운터에서 마을 귀환을 반복할 최대 횟수(무한 루프 방지).")]
        [Min(0)] public int aiMaxTownRetreatsPerEncounter = 6;

        [Tooltip("다음 층 진입 최소 레벨 미달 시, 같은 층을 다시 도는 최대 횟수(한 횟수 = RunSingleFloor 1회, 층 진입 지급 생략).")]
        [Min(0)] public int aiMaxFarmingPassesBeforeNextFloor = 50;

        [Tooltip("다음 층 요구 레벨(수동·풀 병합)을 만족한 뒤에도, 그보다 이 레벨만큼 더 올릴 때까지 같은 층 파밍을 시도합니다. 0 = 요구치만 맞추면 즉시 다음 층(기본). EXP가 적으면 aiMaxFarmingPasses에 걸려 중간에 올라가지 못할 수 있습니다.")]
        [Min(0)] public int aiExtraLevelsBeyondNextFloorGate = 0;

        [Tooltip("false면 전투 전 HP·마을 판정을 끕니다(권장 레벨 파밍만 유지).")]
        public bool aiTownRetreatBeforeUnsafeBattle = true;

        [Tooltip("켜면 다음 층 진입에 필요한 레벨 = max(floorLevelGates, 몬스터 풀 스탯으로 추정한 권장 레벨). 끄면 게이트 리스트만 사용(비어 있으면 1).")]
        public bool aiMergeMonsterPoolRecommendedLevel = true;

        [Header("AI — 다음 층 레벨 (턴 승리 기준, 옵션)")]
        [Tooltip("켜면: 다음 층 몬스터 풀 대상으로 ‘N턴 이내 승리’ 프로브로 최소 플레이어 레벨을 산출해 병합합니다. 생존·장비 관찰용으로는 끄는 것을 권장합니다.")]
        public bool aiTurnWinGateEnabled = false;

        [Tooltip("true면 몬스터 풀 스탯 추정(aiMergeMonsterPoolRecommendedLevel)으로 나온 권장 레벨은 무시하고, 수동 게이트와 턴 승리 기준만 사용합니다.")]
        public bool aiTurnWinGateReplacesPoolRecommendation = true;

        [Tooltip("이 턴 수 이하로 전투가 끝난 승리만 ‘통과’로 카운트합니다(프로브 판정).")]
        [Min(1)] public int aiTurnWinGateMaxTurns = 3;

        [Tooltip("후보 레벨 L마다 같은 층 적을 몇 번 샘플링할지(가중 랜덤 1마리 전투).")]
        [Min(1)] public int aiTurnWinGateProbesPerLevel = 8;

        [Tooltip("프로브 중 이 비율 이상이 (승리 ∧ 턴≤제한)이면 해당 L을 충분하다고 봅니다.")]
        [Range(0.5f, 1f)] public float aiTurnWinGateRequiredSuccessRatio = 0.875f;

        [Tooltip("턴 게이트 탐색 시 최대 플레이어 레벨(상한에 걸리면 이 값을 그대로 반환).")]
        [Min(1)] public int aiTurnWinGateMaxSearchLevel = 60;

        [Header("AI — 10층 전체 클리어 최소 레벨(프로브)")]
        [Tooltip("켜면: **마지막 층(floorCount) 진입** 직전 파밍 목표에 ‘L레벨 파티로 1~10층 전체를 무사히 클리어할 확률’ 프로브 결과를 합산합니다. 프로브 도중에는 수동·풀 게이트만 사용합니다(순환 참조 방지).")]
        public bool aiFloor10ClearMinLevelGateEnabled = true;

        [Tooltip("후보 레벨 L마다 전체 던전(층 1~floorCount) 시뮬을 몇 번 돌릴지.")]
        [Min(1)] public int aiFloor10ClearProbesPerLevel = 6;

        [Tooltip("프로브 중 이 비율 이상이 ‘전 층 클리어·사망 없음’이면 해당 L을 충분하다고 봅니다.")]
        [Range(0.5f, 1f)] public float aiFloor10ClearRequiredSuccessRatio = 0.67f;

        [Tooltip("10층 클리어 최소 레벨 탐색 상한.")]
        [Min(1)] public int aiFloor10ClearMaxSearchLevel = 60;

        [Tooltip("층별 ‘이 층에 들어가려면’ 최소 레벨. 비어 있거나 해당 층 행이 없으면 1(제한 없음)으로 간주합니다.")]
        public List<DungeonSimFloorLevelGate> floorLevelGates = new List<DungeonSimFloorLevelGate>();

        [Header("새벽의 잔 (Dawn Chalice) 정책")]
        [Tooltip("시작 충전 수 (3 = 한 회차에 3번 사용 가능)")]
        [Min(0)] public int startingDawnChaliceCharges = 3;

        [Tooltip("새벽의 잔 사용 시 최대 HP 대비 회복 비율 (0.6 = 60%)")]
        [Range(0f, 1f)] public float dawnChaliceHpHealPercent = 0.6f;

        [Tooltip("새벽의 잔 사용 시 최대 MP 대비 회복 비율")]
        [Range(0f, 1f)] public float dawnChaliceMpHealPercent = 0.6f;

        [Tooltip("HP 비율이 이 값 미만일 때 새벽의 잔 발동 후보(시뮬 기본 0.6). useDawnChaliceOnlyWhenPotionEmpty가 켜져 있으면 포션 0개일 때만 검사합니다.")]
        [Range(0f, 1f)] public float dawnChaliceHpThreshold = 0.6f;

        [Tooltip("true면 일반 포션이 0개일 때만 새벽의 잔 사용. false면 HP 임계 도달 시 일반 포션과 무관하게 사용.")]
        public bool useDawnChaliceOnlyWhenPotionEmpty = false;

        [Header("출력")]
        [Tooltip("CSV 저장 경로 (프로젝트 루트 기준 상대). {TIMESTAMP}는 실행 시각으로 치환.")]
        public string csvRelativePath = "DungeonSimLogs/DungeonSim_{TIMESTAMP}.csv";

        [Tooltip("요약 텍스트 저장 경로 (프로젝트 루트 기준 상대). Assets/ 하위에 두면 Unity Project 창에서 클릭해 바로 볼 수 있습니다.")]
        public string summaryRelativePath = "Assets/Data/Simulation/_LastDungeonSimSummary.txt";

        [Tooltip("요약에 Crude 장비 AI의 점수 공식 설명 + 이번 실행에서 첫/마지막 마을 선택 시 1줄 점수 내역을 붙입니다(인터뷰 대신 숫자 설명).")]
        public bool aiEquipAppendExplainSampleToSummary = false;

        [Header("안전 가드")]
        [Tooltip("한 층에서 발생할 수 있는 전투 수 상한 (무한 루프 방지)")]
        [Min(1)] public int maxBattlesPerFloor = 50;

        /// <summary>유지 목표 HP 비율(미만이면 회복 루프).</summary>
        public float GetHealMaintainRatio()
        {
            if (healTargetHpRatio > 0.001f) return Mathf.Clamp(healTargetHpRatio, 0.01f, 0.999f);
            return Mathf.Clamp(potionUseHpThreshold > 0.001f ? potionUseHpThreshold : 0.7f, 0.01f, 0.999f);
        }

        /// <summary>포션을 쓸 최소 ‘위험’ 비율(이 비율 미만인 유닛에게).</summary>
        public float GetPotionHealCutoff()
        {
            if (potionHealHpRatio > 0.001f) return Mathf.Clamp(potionHealHpRatio, 0.01f, 0.999f);
            return GetHealMaintainRatio();
        }

        /// <summary>일반 시 약초를 쓸 HP 비율(미만).</summary>
        public float GetHerbHealCutoff()
        {
            if (herbUseHpRatio > 0.001f) return Mathf.Clamp(herbUseHpRatio, 0.01f, 0.999f);
            return 0.4f;
        }

        /// <summary>긴급 회복 HP 비율(미만).</summary>
        public float GetEmergencyHealCutoff()
        {
            if (emergencyHealHpRatio > 0.001f) return Mathf.Clamp(emergencyHealHpRatio, 0.01f, 0.999f);
            return 0.25f;
        }

        public int GetExpToNextLevel(int currentLevel) =>
            PlayerExpProgression.GetExpToNextLevel(currentLevel);

        /// <summary>해당 층에 처음 진입하기 위한 최소 레벨. 정의 없으면 1.</summary>
        public int GetMinLevelToEnterFloor(int floor)
        {
            if (floor < 1) return 1;
            if (floorLevelGates == null || floorLevelGates.Count == 0) return 1;
            int best = 1;
            bool found = false;
            for (int i = 0; i < floorLevelGates.Count; i++)
            {
                var g = floorLevelGates[i];
                if (g == null || g.floor != floor) continue;
                found = true;
                best = Mathf.Max(best, Mathf.Max(1, g.minLevelToEnter));
            }
            return found ? best : 1;
        }

        /// <summary>
        /// AI 층 진입 판정용 — <see cref="GetMinLevelToEnterFloor"/>와
        /// (옵션) <see cref="DungeonSimMonsterPool.GetRecommendedMinLevelToEnterFloor"/> 중 큰 값.
        /// </summary>
        public int GetEffectiveMinLevelToEnterFloor(int floor, DungeonSimMonsterPool pool)
        {
            int fromGates = GetMinLevelToEnterFloor(floor);
            if (!aiMergeMonsterPoolRecommendedLevel || pool == null) return fromGates;
            int fromPool = pool.GetRecommendedMinLevelToEnterFloor(floor);
            return Mathf.Max(fromGates, fromPool);
        }

        /// <summary>해당 층에 대한 수동 최소 레벨 행이 <see cref="floorLevelGates"/>에 정의돼 있는지.</summary>
        public bool HasMinLevelGateRowForFloor(int floor)
        {
            if (floorLevelGates == null || floorLevelGates.Count == 0) return false;
            for (int i = 0; i < floorLevelGates.Count; i++)
            {
                var g = floorLevelGates[i];
                if (g != null && g.floor == floor) return true;
            }
            return false;
        }

        /// <summary>층 진입 약초 지급량 — Min~Max(포함) 균등 랜덤. System.Random용.</summary>
        public int RollMedicinalHerbGrantCount(System.Random rng)
        {
            if (rng == null) return 0;
            int lo = Mathf.Min(medicinalHerbGrantCountMin, medicinalHerbGrantCountMax);
            int hi = Mathf.Max(medicinalHerbGrantCountMin, medicinalHerbGrantCountMax);
            return rng.Next(lo, hi + 1);
        }

        private void OnValidate()
        {
            simCompanionReplaceMinLevelGap = Mathf.Max(2, simCompanionReplaceMinLevelGap);
            aiTurnWinGateRequiredSuccessRatio = Mathf.Clamp(aiTurnWinGateRequiredSuccessRatio, 0.5f, 1f);
            aiFloor10ClearRequiredSuccessRatio = Mathf.Clamp(aiFloor10ClearRequiredSuccessRatio, 0.5f, 1f);
            if (medicinalHerbGrantCountMax < medicinalHerbGrantCountMin)
                medicinalHerbGrantCountMax = medicinalHerbGrantCountMin;
            if (floorLevelGates == null) return;
            for (int i = 0; i < floorLevelGates.Count; i++)
            {
                var g = floorLevelGates[i];
                if (g == null) continue;
                g.floor = Mathf.Max(1, g.floor);
                g.minLevelToEnter = Mathf.Max(1, g.minLevelToEnter);
                g.maxRecommendedLevel = Mathf.Max(g.minLevelToEnter, g.maxRecommendedLevel);
            }
        }
    }
}
