using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 전투 시뮬레이션용 적 편성(슬롯 1~4 = 전열 맨앞 ~ 후열 맨뒤).
    /// Project 창에서 이 에셋을 선택하면 Inspector에 네 마리가 표시됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleSimEnemyRoster", menuName = "Abyssdawn/Simulation/Battle Sim Enemy Roster", order = 10)]
    public class BattleSimEnemyRoster : ScriptableObject
    {
        [Header("슬롯 1~4 (BattleSimEnemyUnitSO)")]
        [Tooltip("전투 슬롯 1 (맨 앞)")]
        public BattleSimEnemyUnitSO enemySlot1;

        [Tooltip("슬롯 2")]
        public BattleSimEnemyUnitSO enemySlot2;

        [Tooltip("슬롯 3")]
        public BattleSimEnemyUnitSO enemySlot3;

        [Tooltip("슬롯 4 (맨 뒤)")]
        public BattleSimEnemyUnitSO enemySlot4;

        /// <summary>시뮬 Setup 등에서 순서대로 읽기 (null 슬롯은 건너뛰도록 호출 측에서 처리)</summary>
        public BattleSimEnemyUnitSO[] GetOrderedEnemyUnits()
        {
            return new[] { enemySlot1, enemySlot2, enemySlot3, enemySlot4 };
        }
    }
}
