using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 헤드리스 전투 시뮬 전용 적 단위 데이터. <see cref="MonsterSO"/>와 별도로 스탯만 빠르게 조정합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleSimEnemyUnit", menuName = "Abyssdawn/Simulation/Battle Sim Enemy Unit", order = 13)]
    public class BattleSimEnemyUnitSO : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("리포트·로그용")]
        public string displayName = "Sim Enemy";

        [Header("기본 스탯 (시뮬만)")]
        public int maxHP = 130;
        public int maxMP;
        public int attack = 17;
        public int defense = 13;
        [Tooltip("MAG — 마법 스탯")]
        public int magic;
        public int agility = 9;
        public int luck = 4;

        [Header("시뮬 AI")]
        public AIPattern aiPattern = AIPattern.Aggressive;

        [Header("시뮬 스킬 조합 (슬롯당 1행 — SkillData 에셋 드래그)")]
        [Tooltip("리스트 크기는 +로 늘리고, 각 칸에 Resources 등의 SkillData를 드래그합니다. (런타임 시뮬 연동은 추후)")]
        public List<SkillData> simSkills = new List<SkillData>();
    }
}
