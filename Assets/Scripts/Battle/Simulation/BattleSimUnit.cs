using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    public enum BattleSimTeam
    {
        Ally,
        Enemy
    }

    /// <summary>
    /// 시뮬레이션 전용 유닛 인스턴스 (씬 오브젝트 없음).
    /// </summary>
    public class BattleSimUnit
    {
        public BattleSimTeam Team;
        public string DisplayName;
        public BattleSlot Slot;

        public int MaxHP;
        public int CurrentHP;
        public int MaxMP;
        public int CurrentMP;
        public int Attack;
        public int Defense;
        /// <summary>MAG(마법) — 시뮬 전용.</summary>
        public int Magic;
        public int Agility;
        public int Luck;

        /// <summary>시뮬 AI — <see cref="MonsterSO.AIPattern"/> 또는 더미 아군 SO.</summary>
        public AIPattern SimAiPattern = AIPattern.Aggressive;

        /// <summary>아군이 방어 선택 → 같은 라운드 <b>적 페이즈</b>에서 받는 모든 피해 × 0.6 (40% 경감).</summary>
        public bool SimGuardEnemyPhase;

        /// <summary>적이 방어 선택 → <b>다음 라운드 아군 페이즈</b>에서 받는 모든 피해 × 0.6. 중첩 없음.</summary>
        public bool SimGuardNextAllyPhase;

        /// <summary>시뮬 전용 스킬 목록 (로스터 SO에서 복사).</summary>
        public List<SkillData> SimSkills = new List<SkillData>();

        /// <summary>던전/배틀 시뮬 — 더미 아군 SO의 종의 기억 3칸 (레벨업 룰렛·성장치 합산에 사용).</summary>
        public MemoryOfSpeciesData MemorySlot1;
        public MemoryOfSpeciesData MemorySlot2;
        public MemoryOfSpeciesData MemorySlot3;

        /// <summary>던전 시뮬 레벨업 시 HP/MP — <see cref="CharacterClass.hpPerLevel"/> / <see cref="CharacterClass.mpPerLevel"/>.</summary>
        public CharacterClass SimCharacterClass;

        public bool IsAlive => CurrentHP > 0;

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
        }
    }
}
