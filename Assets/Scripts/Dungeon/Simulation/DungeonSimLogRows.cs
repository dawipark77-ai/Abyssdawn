using System.Globalization;
using System.Text;

namespace Abyssdawn
{
    /// <summary>던전 시뮬 구조화 CSV — 공용 이스케이프.</summary>
    public static class DungeonSimLogCsv
    {
        public static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool needsQuote = s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0;
            if (!needsQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }

    /// <summary>1행 = 1회차 종료 시점 요약.</summary>
    public sealed class DungeonSimRunLogRow
    {
        public int RunId;
        public int Seed;
        public int TargetFloors;
        public int FinalFloorReached;
        public int DeathFlag;
        /// <summary>battle | resource_exhaustion | other | none</summary>
        public string DeathCause;
        public int FinalPartyLevel;
        public string FinalCompanionCumulative;
        public int BattlesTotal;
        public int BattlesWon;
        public int BattlesLost;
        public int BattlesFled;
        public int HerbUses;
        public int PotionUses;
        public int ChaliceUses;
        public long XpGainedRun;
        public long DamageDealtRun;
        public long DamageTakenRun;
        public int CompanionCountEnd;
        public int RosterAllyCountEnd;
        /// <summary>사망 회차에서 마지막으로 기록된 AI 장비 시그니처(없으면 빈 문자열).</summary>
        public string DeathAiEquipSignature;

        public static string Header =>
            "run_id,seed,target_floors,final_floor_reached,death_flag,death_cause," +
            "final_party_level,final_companion_cumulative," +
            "battles_total,battles_won,battles_lost,battles_fled," +
            "herb_uses,potion_uses,chalice_uses," +
            "xp_gained_run,damage_dealt_run,damage_taken_run," +
            "companion_count_end,roster_ally_count_end,death_ai_equip";

        public string ToLine()
        {
            var sb = new StringBuilder(256);
            sb.Append(RunId).Append(',');
            sb.Append(Seed).Append(',');
            sb.Append(TargetFloors).Append(',');
            sb.Append(FinalFloorReached).Append(',');
            sb.Append(DeathFlag).Append(',');
            sb.Append(DungeonSimLogCsv.Esc(DeathCause ?? "")).Append(',');
            sb.Append(FinalPartyLevel).Append(',');
            sb.Append(DungeonSimLogCsv.Esc(string.IsNullOrEmpty(FinalCompanionCumulative) ? "-" : FinalCompanionCumulative)).Append(',');
            sb.Append(BattlesTotal).Append(',');
            sb.Append(BattlesWon).Append(',');
            sb.Append(BattlesLost).Append(',');
            sb.Append(BattlesFled).Append(',');
            sb.Append(HerbUses).Append(',');
            sb.Append(PotionUses).Append(',');
            sb.Append(ChaliceUses).Append(',');
            sb.Append(XpGainedRun).Append(',');
            sb.Append(DamageDealtRun).Append(',');
            sb.Append(DamageTakenRun).Append(',');
            sb.Append(CompanionCountEnd).Append(',');
            sb.Append(RosterAllyCountEnd).Append(',');
            sb.Append(DungeonSimLogCsv.Esc(DeathAiEquipSignature ?? ""));
            return sb.ToString();
        }
    }

    /// <summary>1행 = 1회차·1층.</summary>
    public sealed class DungeonSimFloorLogRow
    {
        public int RunId;
        public int Seed;
        public int Floor;
        public string FloorType;
        public int LevelOnEnter;
        public float PartyHpPctEnter;
        public float PartyMpPctEnter;
        public int HerbOnEnter;
        public int PotionOnEnter;
        public int ChaliceOnEnter;
        public int CompanionCountEnter;
        public int RosterAllyCountEnter;
        public int TotalHpEnter;
        public int TotalMpEnter;

        public int StepsMoved;
        public int Encounters;
        public int Battles;
        public int BattlesWon;
        public int BattlesFled;
        public int BattlesLost;
        public int ClearFlag;
        public int DeathFlag;

        public int LevelOnExit;
        public float PartyHpPctExit;
        public float PartyMpPctExit;
        public int HerbOnExit;
        public int PotionOnExit;
        public int ChaliceOnExit;
        public int CompanionCountExit;
        public int RosterAllyCountExit;
        public int TotalHpExit;
        public int TotalMpExit;

        public int HerbUseDelta;
        public int PotionUseDelta;
        public int ChaliceUseDelta;

        public long XpGained;
        public int GoldGained;
        public long DamageDealtFloor;
        public long DamageTakenFloor;
        public int TotalBattleTurns;
        public int WinBattleTurnsSum;
        public string SkillActivationBreakdown;
        public string Notes;

        public int CompanionJoinsThisFloor;
        public string CompanionsJoinedCumulative;
        public int SumEnemyMonstersInBattlesThisFloor;
        public int MaxEnemyMonstersSingleBattleThisFloor;
        public string DeathAiEquipSignature;
        public int Floor1TownUsesCumulative;
        public int LastBattleEnemyCount;

        public static string Header =>
            "run_id,seed,floor,floor_type," +
            "level_enter,party_hp_pct_enter,party_mp_pct_enter,herb_enter,potion_enter,chalice_enter,companion_count_enter,roster_ally_count_enter,total_hp_enter,total_mp_enter," +
            "steps_moved,encounters,battles,battles_won,battles_fled,battles_lost,clear_flag,death_flag," +
            "level_exit,party_hp_pct_exit,party_mp_pct_exit,herb_exit,potion_exit,chalice_exit,companion_count_exit,roster_ally_count_exit,total_hp_exit,total_mp_exit," +
            "herb_use_delta,potion_use_delta,chalice_use_delta," +
            "xp_gained,gold_gained,damage_dealt_floor,damage_taken_floor,total_battle_turns,win_turns_sum,avg_turns_per_win," +
            "skill_activation_breakdown,companion_joins_this_floor,companions_joined_cumulative,sum_enemy_monsters_in_battles,max_enemy_monsters_single_battle,death_ai_equip,floor1_town_uses_cumulative,last_battle_enemy_count,notes";

        public string ToLine()
        {
            float avgWin = BattlesWon > 0 ? WinBattleTurnsSum / (float)BattlesWon : 0f;
            var sb = new StringBuilder(384);
            sb.Append(RunId).Append(',').Append(Seed).Append(',').Append(Floor).Append(',')
                .Append(DungeonSimLogCsv.Esc(FloorType ?? "")).Append(',');
            sb.Append(LevelOnEnter).Append(',')
                .Append(PartyHpPctEnter.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                .Append(PartyMpPctEnter.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(HerbOnEnter).Append(',').Append(PotionOnEnter).Append(',').Append(ChaliceOnEnter).Append(',')
                .Append(CompanionCountEnter).Append(',').Append(RosterAllyCountEnter).Append(',')
                .Append(TotalHpEnter).Append(',').Append(TotalMpEnter).Append(',');
            sb.Append(StepsMoved).Append(',').Append(Encounters).Append(',').Append(Battles).Append(',')
                .Append(BattlesWon).Append(',').Append(BattlesFled).Append(',').Append(BattlesLost).Append(',')
                .Append(ClearFlag).Append(',').Append(DeathFlag).Append(',');
            sb.Append(LevelOnExit).Append(',')
                .Append(PartyHpPctExit.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
                .Append(PartyMpPctExit.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(HerbOnExit).Append(',').Append(PotionOnExit).Append(',').Append(ChaliceOnExit).Append(',')
                .Append(CompanionCountExit).Append(',').Append(RosterAllyCountExit).Append(',')
                .Append(TotalHpExit).Append(',').Append(TotalMpExit).Append(',');
            sb.Append(HerbUseDelta).Append(',').Append(PotionUseDelta).Append(',').Append(ChaliceUseDelta).Append(',');
            sb.Append(XpGained).Append(',').Append(GoldGained).Append(',')
                .Append(DamageDealtFloor).Append(',').Append(DamageTakenFloor).Append(',')
                .Append(TotalBattleTurns).Append(',').Append(WinBattleTurnsSum).Append(',')
                .Append(avgWin.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(DungeonSimLogCsv.Esc(SkillActivationBreakdown ?? "")).Append(',')
                .Append(CompanionJoinsThisFloor).Append(',')
                .Append(DungeonSimLogCsv.Esc(string.IsNullOrEmpty(CompanionsJoinedCumulative) ? "-" : CompanionsJoinedCumulative)).Append(',')
                .Append(SumEnemyMonstersInBattlesThisFloor).Append(',')
                .Append(MaxEnemyMonstersSingleBattleThisFloor).Append(',')
                .Append(DungeonSimLogCsv.Esc(DeathAiEquipSignature ?? "")).Append(',')
                .Append(Floor1TownUsesCumulative).Append(',')
                .Append(LastBattleEnemyCount).Append(',')
                .Append(DungeonSimLogCsv.Esc(Notes ?? ""));
            return sb.ToString();
        }
    }

    /// <summary>1행 = 1전투.</summary>
    public sealed class DungeonSimBattleLogRow
    {
        public int BattleSeq;
        public int RunId;
        public int Seed;
        public int Floor;
        public int EncounterIndexInFloor;
        public string EnemyPartySig;
        public int ResultWin;
        public int ResultFlee;
        public int ResultLoss;
        public int Turns;
        public long DamageDealtToEnemies;
        public long DamageTakenByAllies;
        public string SkillActivationBreakdown;
        public int BattleHerbUses;
        public int BattlePotionUses;
        public int BattleChaliceUses;

        public int AllySlot1HpStart, AllySlot1HpEnd, AllySlot2HpStart, AllySlot2HpEnd;
        public int AllySlot3HpStart, AllySlot3HpEnd, AllySlot4HpStart, AllySlot4HpEnd;
        public int EnemySlot1HpStart, EnemySlot1HpEnd, EnemySlot2HpStart, EnemySlot2HpEnd;
        public int EnemySlot3HpStart, EnemySlot3HpEnd, EnemySlot4HpStart, EnemySlot4HpEnd;

        public long A1Dealt, A2Dealt, A3Dealt, A4Dealt;
        public long A1Taken, A2Taken, A3Taken, A4Taken;
        public long A1OffAtt, A2OffAtt, A3OffAtt, A4OffAtt;
        public long A1OffHit, A2OffHit, A3OffHit, A4OffHit;
        public long A1Crit, A2Crit, A3Crit, A4Crit;
        public long A1SurvTurns, A2SurvTurns, A3SurvTurns, A4SurvTurns;
        public int A1Defend, A2Defend, A3Defend, A4Defend;
        public int A1Skill, A2Skill, A3Skill, A4Skill;

        public static string Header =>
            "battle_seq,run_id,seed,floor,encounter_idx,enemy_party_sig," +
            "result_win,result_flee,result_loss,turns,dmg_dealt_to_enemies,dmg_taken_by_allies," +
            "skill_activation_breakdown,battle_herb_uses,battle_potion_uses,battle_chalice_uses," +
            "a1_hp0,a1_hp1,a2_hp0,a2_hp1,a3_hp0,a3_hp1,a4_hp0,a4_hp1," +
            "e1_hp0,e1_hp1,e2_hp0,e2_hp1,e3_hp0,e3_hp1,e4_hp0,e4_hp1," +
            "a1_dealt,a2_dealt,a3_dealt,a4_dealt,a1_taken,a2_taken,a3_taken,a4_taken," +
            "a1_off_att,a2_off_att,a3_off_att,a4_off_att,a1_off_hit,a2_off_hit,a3_off_hit,a4_off_hit," +
            "a1_crit,a2_crit,a3_crit,a4_crit,a1_surv_turns,a2_surv_turns,a3_surv_turns,a4_surv_turns," +
            "a1_defend,a2_defend,a3_defend,a4_defend,a1_skill,a2_skill,a3_skill,a4_skill";

        public string ToLine()
        {
            var sb = new StringBuilder(512);
            sb.Append(BattleSeq).Append(',').Append(RunId).Append(',').Append(Seed).Append(',').Append(Floor).Append(',')
                .Append(EncounterIndexInFloor).Append(',').Append(DungeonSimLogCsv.Esc(EnemyPartySig ?? "")).Append(',');
            sb.Append(ResultWin).Append(',').Append(ResultFlee).Append(',').Append(ResultLoss).Append(',')
                .Append(Turns).Append(',').Append(DamageDealtToEnemies).Append(',').Append(DamageTakenByAllies).Append(',');
            sb.Append(DungeonSimLogCsv.Esc(SkillActivationBreakdown ?? "")).Append(',')
                .Append(BattleHerbUses).Append(',').Append(BattlePotionUses).Append(',').Append(BattleChaliceUses).Append(',');
            sb.Append(AllySlot1HpStart).Append(',').Append(AllySlot1HpEnd).Append(',')
                .Append(AllySlot2HpStart).Append(',').Append(AllySlot2HpEnd).Append(',')
                .Append(AllySlot3HpStart).Append(',').Append(AllySlot3HpEnd).Append(',')
                .Append(AllySlot4HpStart).Append(',').Append(AllySlot4HpEnd).Append(',');
            sb.Append(EnemySlot1HpStart).Append(',').Append(EnemySlot1HpEnd).Append(',')
                .Append(EnemySlot2HpStart).Append(',').Append(EnemySlot2HpEnd).Append(',')
                .Append(EnemySlot3HpStart).Append(',').Append(EnemySlot3HpEnd).Append(',')
                .Append(EnemySlot4HpStart).Append(',').Append(EnemySlot4HpEnd).Append(',');
            sb.Append(A1Dealt).Append(',').Append(A2Dealt).Append(',').Append(A3Dealt).Append(',').Append(A4Dealt).Append(',');
            sb.Append(A1Taken).Append(',').Append(A2Taken).Append(',').Append(A3Taken).Append(',').Append(A4Taken).Append(',');
            sb.Append(A1OffAtt).Append(',').Append(A2OffAtt).Append(',').Append(A3OffAtt).Append(',').Append(A4OffAtt).Append(',');
            sb.Append(A1OffHit).Append(',').Append(A2OffHit).Append(',').Append(A3OffHit).Append(',').Append(A4OffHit).Append(',');
            sb.Append(A1Crit).Append(',').Append(A2Crit).Append(',').Append(A3Crit).Append(',').Append(A4Crit).Append(',');
            sb.Append(A1SurvTurns).Append(',').Append(A2SurvTurns).Append(',').Append(A3SurvTurns).Append(',').Append(A4SurvTurns).Append(',');
            sb.Append(A1Defend).Append(',').Append(A2Defend).Append(',').Append(A3Defend).Append(',').Append(A4Defend).Append(',');
            sb.Append(A1Skill).Append(',').Append(A2Skill).Append(',').Append(A3Skill).Append(',').Append(A4Skill);
            return sb.ToString();
        }
    }

    /// <summary>전투 직전 파티 스냅샷 — 1행 = 1슬롯.</summary>
    public sealed class DungeonSimPartySlotLogRow
    {
        public int BattleSeq;
        public int RunId;
        public int Seed;
        public int Floor;
        public int Slot;
        public string RowTag;
        public string UnitKind;
        public string DisplayName;
        public string ClassName;
        public int PartyLevelField;
        public int MonsterLevelField;
        public int Atk, Def, Mag, Agi, Luk;
        public int Hp, MaxHp, Mp, MaxMp;

        public static string Header =>
            "battle_seq,run_id,seed,floor,slot,row_tag,unit_kind,display_name,class_name," +
            "party_level_field,monster_level_field,atk,def,mag,agi,luk,hp,max_hp,mp,max_mp";

        public string ToLine()
        {
            return BattleSeq + "," + RunId + "," + Seed + "," + Floor + "," + Slot + "," +
                   DungeonSimLogCsv.Esc(RowTag ?? "") + "," +
                   DungeonSimLogCsv.Esc(UnitKind ?? "") + "," +
                   DungeonSimLogCsv.Esc(DisplayName ?? "") + "," +
                   DungeonSimLogCsv.Esc(ClassName ?? "") + "," +
                   PartyLevelField + "," + MonsterLevelField + "," +
                   Atk + "," + Def + "," + Mag + "," + Agi + "," + Luk + "," +
                   Hp + "," + MaxHp + "," + Mp + "," + MaxMp;
        }
    }

    /// <summary>동료 영입 시도(CompanionChance) 1행.</summary>
    public sealed class DungeonSimRecruitLogRow
    {
        public int BattleSeq;
        public int RunId;
        public int Seed;
        public int Floor;
        public string MonsterName;
        public int MonsterLevel;
        public string CompanionChanceStr;
        public string RollU01Str;
        public int Success;
        public string FailReason;
        public string JoinedDisplayName;
        public int JoinedSlot;

        public static string Header =>
            "battle_seq,run_id,seed,floor,monster_name,monster_level,companion_chance,roll_u01,success,fail_reason,joined_display_name,joined_slot";

        public string ToLine()
        {
            return BattleSeq + "," + RunId + "," + Seed + "," + Floor + "," +
                   DungeonSimLogCsv.Esc(MonsterName ?? "") + "," + MonsterLevel + "," +
                   DungeonSimLogCsv.Esc(CompanionChanceStr ?? "") + "," +
                   DungeonSimLogCsv.Esc(RollU01Str ?? "") + "," + Success + "," +
                   DungeonSimLogCsv.Esc(FailReason ?? "") + "," +
                   DungeonSimLogCsv.Esc(JoinedDisplayName ?? "") + "," + JoinedSlot;
        }
    }
}
