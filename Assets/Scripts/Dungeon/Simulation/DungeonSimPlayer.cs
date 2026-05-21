using System.Collections.Generic;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬용 플레이어/파티 상태 누적기.
    /// BattleSimUnit를 base로 두고 한 회차(run) 동안 EXP/Gold/Potion/Level이 누적됩니다.
    /// 한 전투마다 BattleSimulator에 BattleSimUnit 리스트를 그대로 넘기고,
    /// 전투 종료 후 RestoreUnitState() 호출은 하지 않습니다(HP/MP가 자연 누적).
    /// </summary>
    public class DungeonSimPlayer
    {
        public string Name;
        public int Level;
        public int Exp;
        public int Gold;

        public int HpPotionCount;
        public int HpPotionsUsedTotal;

        /// <summary>던전 시뮬 전용 — Medicinal Herb(약초). 층 진입 시 지급·전투 전후 소모.</summary>
        public int MedicinalHerbCount;
        public int MedicinalHerbsUsedTotal;

        // 새벽의 잔 — HP/MP %회복 + (Phase 2에서 상태이상 회복) 충전식 아이템
        public int DawnChaliceCharges;
        public int DawnChaliceUsedTotal;

        public int LevelUpsTotal;

        /// <summary>1층 마을(풀 HP/MP·소모품 충전) 호출 누적 — 회차 전체.</summary>
        public int Floor1TownUsesTotal;

        /// <summary>마지막 마을 방문 시 AI가 고른 장비 시그니처(보고용).</summary>
        public string LastAiEquipSignature = "";

        /// <summary>마지막 마을 방문 시 장비 점수 1줄(옵션 로그·요약용).</summary>
        public string LastAiEquipScoreExplain = "";

        /// <summary>이 회차에서 AI 장비 재선택 횟수(마을 방문마다 +1).</summary>
        public int AiEquipPickCount;

        /// <summary>이 회차에서 시뮬 동료 합류에 성공한 횟수(성공 시 <see cref="BattleSimUnit"/> 파티에 반영).</summary>
        public int SimCompanionJoinsTotal;

        /// <summary>마지막으로 동료화에 성공한 몬스터 표시명(보고용).</summary>
        public string LastCompanionJoinedName = "";

        /// <summary>이번 회차에서 동료 합류에 성공한 순서대로의 표시명(중복 허용).</summary>
        public readonly List<string> SimCompanionJoinHistory = new List<string>();

        /// <summary>CSV·요약용 — 합류 이력을 <c>|</c>로 이은 문자열. 없으면 "-".</summary>
        public string FormatCompanionJoinHistorySig()
        {
            if (SimCompanionJoinHistory == null || SimCompanionJoinHistory.Count == 0) return "-";
            return string.Join("|", SimCompanionJoinHistory);
        }

        /// <summary>BattleSimulator에 그대로 전달되는 전투 유닛(슬롯 1~4).</summary>
        public readonly List<BattleSimUnit> Units = new List<BattleSimUnit>();

        public int GetTotalCurrentHP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.CurrentHP);
            return sum;
        }

        public int GetTotalMaxHP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.MaxHP);
            return sum;
        }

        public int GetTotalCurrentMP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.CurrentMP);
            return sum;
        }

        public int GetTotalMaxMP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.MaxMP);
            return sum;
        }

        public bool AnyAlive()
        {
            foreach (var u in Units) if (u.IsAlive) return true;
            return false;
        }
    }
}
