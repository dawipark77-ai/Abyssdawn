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
        public int Magic;
        public int Agility;
        public int Luck;

        /// <summary>시뮬 AI — <see cref="MonsterSO.AIPattern"/> 또는 더미 아군 SO.</summary>
        public AIPattern SimAiPattern = AIPattern.Aggressive;

        /// <summary>아군이 방어 선택 → 같은 라운드 <b>적 페이즈</b>에서 받는 모든 피해 × 0.6 (40% 경감).</summary>
        public bool SimGuardEnemyPhase;

        /// <summary>적이 방어 선택 → <b>다음 라운드 아군 페이즈</b>에서 받는 모든 피해 × 0.6. 중첩 없음.</summary>
        public bool SimGuardNextAllyPhase;

        public bool IsAlive => CurrentHP > 0;

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
        }
    }
}
