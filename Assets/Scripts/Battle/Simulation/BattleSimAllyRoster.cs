using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 전투 시뮬레이션용 아군(더미 동료) 편성. 슬롯 1~4에 <see cref="BattleSimDummyAllySO"/>를 묶습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleSimAllyRoster", menuName = "Abyssdawn/Simulation/Battle Sim Ally Roster", order = 12)]
    public class BattleSimAllyRoster : ScriptableObject
    {
        [Header("슬롯 1~4 (BattleSimDummyAllySO)")]
        [Tooltip("전투 슬롯 1 (맨 앞)")]
        public BattleSimDummyAllySO allySlot1;

        [Tooltip("슬롯 2")]
        public BattleSimDummyAllySO allySlot2;

        [Tooltip("슬롯 3")]
        public BattleSimDummyAllySO allySlot3;

        [Tooltip("슬롯 4 (맨 뒤)")]
        public BattleSimDummyAllySO allySlot4;

        /// <summary>시뮬 Setup 등에서 순서대로 읽기 (null은 호출 측에서 스킵)</summary>
        public BattleSimDummyAllySO[] GetOrderedAllies()
        {
            return new[] { allySlot1, allySlot2, allySlot3, allySlot4 };
        }
    }
}
