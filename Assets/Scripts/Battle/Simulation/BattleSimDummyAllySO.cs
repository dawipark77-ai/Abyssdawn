using System.Collections.Generic;
using AbyssdawnBattle;
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

        [Header("직업 (시뮬 레벨업 — HP/MP 성장)")]
        [Tooltip("비우면 던전 시뮬 Settings의 levelUpHpGain / levelUpMpGain으로 대체. 지정 시 레벨당 hpPerLevel·mpPerLevel이 PlayerStats.ApplyHpMpGrowth와 동일하게 적용됩니다.")]
        public CharacterClass characterClass;

        [Header("종의 기억 (시뮬 레벨업)")]
        [Tooltip("PlayerStats와 동일 — 3슬롯 합산 성장치로 레벨업 시 스탯 1회 가중 랜덤, HP/MP 성장치 추가")]
        public MemoryOfSpeciesData memorySlot1;
        [Tooltip("종의 기억 슬롯 2")]
        public MemoryOfSpeciesData memorySlot2;
        [Tooltip("종의 기억 슬롯 3")]
        public MemoryOfSpeciesData memorySlot3;

        [Header("시뮬 스킬 조합 (슬롯당 1행 — SkillData 에셋 드래그)")]
        [Tooltip("리스트 크기는 +로 늘리고, 각 칸에 Resources 등의 SkillData를 드래그합니다. (런타임 시뮬 연동은 추후)")]
        public List<SkillData> simSkills = new List<SkillData>();
    }
}
