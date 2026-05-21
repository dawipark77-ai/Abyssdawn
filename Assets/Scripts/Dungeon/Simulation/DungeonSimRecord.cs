using System.Globalization;
using System.Text;

namespace Abyssdawn
{
    /// <summary>
    /// CSV 한 행 — 한 회차의 한 층 진행 요약 (첨부 회화 명세 기반, Phase 1 핵심 16지표 + 메타).
    /// 컬럼 추가 시 <see cref="CsvHeader"/>와 <see cref="ToCsvLine"/>를 동시에 수정하세요.
    /// </summary>
    public struct DungeonSimRecord
    {
        public int RunId;
        public int Seed;
        public int Floor;
        public string FloorType;     // Normal / Elite / Boss / NoPool

        public int StepsMoved;
        public int Encounters;
        public int Battles;
        /// <summary>해당 층에서 아군 승리로 끝난 전투 수 (패배 시 마지막 판은 미포함).</summary>
        public int BattlesWon;
        /// <summary>해당 층에서 HP 격차로 도망(보상 없음, 생존 유지)친 전투 수.</summary>
        public int BattlesFled;

        public int HpBefore;
        public int HpAfter;
        public int MpBefore;
        public int MpAfter;

        public int XpGained;
        public int GoldGained;
        public int LevelBefore;
        public int LevelAfter;

        public int MedicinalHerbUseCount;
        public int PotionUseCount;
        public int DawnChaliceUseCount;
        /// <summary>이 층을 마친 직후의 약초 잔량.</summary>
        public int MedicinalHerbRemainAfter;
        /// <summary>이 층을 마친 직후의 일반 HP 포션 잔량.</summary>
        public int PotionRemainAfter;
        /// <summary>이 층을 마친 직후의 새벽의 잔 충전 잔량.</summary>
        public int DawnChaliceRemainAfter;
        public int SkillUseCount;
        public int RecoverySkillUseCount;
        /// <summary>이 층에서 액티브 스킬 시전 누적(전투 합). 형식: <c>skillID=n|...</c> — CSV 한 칸.</summary>
        public string SkillActivationBreakdown;
        public int TotalDamageDealt;
        public int TotalDamageTaken;
        public int TotalBattleTurns;
        /// <summary>승리한 전투만 — 소모된 턴 수 합(평균은 BattlesWon으로 나눔).</summary>
        public int WinBattleTurnsSum;

        public bool DeathFlag;
        public bool ClearFlag;
        public bool NextFloorFlag;

        /// <summary>이 층을 마친 직후까지의 1층 마을(풀 회복·충전) 누적 호출 횟수.</summary>
        public int Floor1TownUsesCumulative;

        /// <summary>이 층에서 마지막으로 치른 전투의 적 수(해당 층 전투 없음 0). 사망 행이면 패배한 그 전투 기준).</summary>
        public int LastBattleEnemyCount;

        /// <summary>이 층에서 치른 모든 전투에 등장한 적 몬스터 수의 합(판마다 enemy 파티 마리수 누적).</summary>
        public int SumEnemyMonstersInBattlesThisFloor;

        /// <summary>이 층에서 한 전투당 적이 가장 많았을 때의 마리수(최대 4 등 풀 상한).</summary>
        public int MaxEnemyMonstersSingleBattleThisFloor;

        /// <summary>이 층에서 전투 승리 후 동료 합류 시뮬에 성공한 횟수(전투당 최대 1회 시도·최대 1성공).</summary>
        public int CompanionJoinsThisFloor;

        /// <summary>이 층 종료 시점까지 누적된 동료 합류 표시명(가입 순, <c>|</c> 구분). 없으면 "-".</summary>
        public string CompanionsJoinedCumulative;

        public string Notes;

        /// <summary>사망 행(death_flag=1)일 때 마지막 AI 장비 시그니처.</summary>
        public string DeathAiEquipSignature;

        public static string CsvHeader =>
            "run_id,seed,floor,floor_type," +
            "steps_moved,encounters,battles,battles_won,battles_fled," +
            "hp_before,hp_after,mp_before,mp_after," +
            "xp_gained,gold_gained,level_before,level_after," +
            "medicinal_herb_use_count,potion_use_count,dawn_chalice_use_count,medicinal_herb_remain_after,potion_remain_after,chalice_remain_after,skill_use_count,recovery_skill_use_count," +
            "total_damage_dealt,total_damage_taken,total_battle_turns,win_turns_sum,avg_turns_per_win," +
            "death_flag,clear_flag,next_floor_flag,floor1_town_uses_cumulative,last_battle_enemy_count,sum_enemy_monsters_in_battles,max_enemy_monsters_single_battle,death_ai_equip,skill_activation_breakdown,companion_joins,companions_joined_cumulative,notes";

        public string ToCsvLine()
        {
            var sb = new StringBuilder(256);
            sb.Append(RunId).Append(',');
            sb.Append(Seed).Append(',');
            sb.Append(Floor).Append(',');
            sb.Append(EscapeCsv(FloorType)).Append(',');
            sb.Append(StepsMoved).Append(',');
            sb.Append(Encounters).Append(',');
            sb.Append(Battles).Append(',');
            sb.Append(BattlesWon).Append(',');
            sb.Append(BattlesFled).Append(',');
            sb.Append(HpBefore).Append(',');
            sb.Append(HpAfter).Append(',');
            sb.Append(MpBefore).Append(',');
            sb.Append(MpAfter).Append(',');
            sb.Append(XpGained).Append(',');
            sb.Append(GoldGained).Append(',');
            sb.Append(LevelBefore).Append(',');
            sb.Append(LevelAfter).Append(',');
            sb.Append(MedicinalHerbUseCount).Append(',');
            sb.Append(PotionUseCount).Append(',');
            sb.Append(DawnChaliceUseCount).Append(',');
            sb.Append(MedicinalHerbRemainAfter).Append(',');
            sb.Append(PotionRemainAfter).Append(',');
            sb.Append(DawnChaliceRemainAfter).Append(',');
            sb.Append(SkillUseCount).Append(',');
            sb.Append(RecoverySkillUseCount).Append(',');
            sb.Append(TotalDamageDealt).Append(',');
            sb.Append(TotalDamageTaken).Append(',');
            sb.Append(TotalBattleTurns).Append(',');
            sb.Append(WinBattleTurnsSum).Append(',');
            float avgWin = BattlesWon > 0 ? WinBattleTurnsSum / (float)BattlesWon : 0f;
            sb.Append(avgWin.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(DeathFlag ? 1 : 0).Append(',');
            sb.Append(ClearFlag ? 1 : 0).Append(',');
            sb.Append(NextFloorFlag ? 1 : 0).Append(',');
            sb.Append(Floor1TownUsesCumulative).Append(',');
            sb.Append(LastBattleEnemyCount).Append(',');
            sb.Append(SumEnemyMonstersInBattlesThisFloor).Append(',');
            sb.Append(MaxEnemyMonstersSingleBattleThisFloor).Append(',');
            sb.Append(EscapeCsv(DeathAiEquipSignature)).Append(',');
            sb.Append(EscapeCsv(SkillActivationBreakdown ?? "")).Append(',');
            sb.Append(CompanionJoinsThisFloor).Append(',');
            sb.Append(EscapeCsv(string.IsNullOrEmpty(CompanionsJoinedCumulative) ? "-" : CompanionsJoinedCumulative)).Append(',');
            sb.Append(EscapeCsv(Notes));
            return sb.ToString();
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool needsQuote = s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0;
            if (!needsQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
