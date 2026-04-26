using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 전투 시뮬레이션용 히어로 측 더미 동료 스탯.
    /// <see cref="BattleSimEnemyRoster"/>와 같이 Data/Simulation에 두고 Inspector에서 확인합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleSimDummyAlly", menuName = "Abyssdawn/Simulation/Battle Sim Dummy Ally", order = 11)]
    public class BattleSimDummyAllySO : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("로그·리포트용 이름")]
        public string allyDisplayName = "Dummy Ally";

        [Header("기본 스탯")]
        public int maxHP = 150;
        public int maxMP = 50;
        [Tooltip("물리 공격력(힘)")]
        public int attack = 10;
        public int defense = 10;
        [Tooltip("MAG — 마법 스탯")]
        public int magic = 10;
        public int agility = 10;
        public int luck = 5;

        [Header("배치 슬롯 (시뮬에서 1~4)")]
        [Range(1, 4)]
        public int battleSlotIndex = 1;

        [Header("시뮬 AI")]
        [Tooltip("MonsterSO.AIPattern과 동일 열거 — 시뮬에서 방어 빈도·타겟 가중에 사용")]
        public AIPattern simAiPattern = AIPattern.Aggressive;
    }
}
