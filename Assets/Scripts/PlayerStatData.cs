using UnityEngine;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// 플레이어의 런타임 상태 데이터를 저장하는 ScriptableObject
/// 중요: 이 에셋은 게임 실행 중 실시간으로 업데이트됩니다!
/// HP/MP가 변경될 때마다 PlayerStats의 setter에서 이 에셋에 자동 저장됩니다.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerStatData", menuName = "MyRPG/PlayerData Asset", order = 1)]
public class PlayerStatData : ScriptableObject
{
    [Header("런타임 데이터 (실시간 저장)")]
    [Tooltip("현재 HP - 실시간으로 저장됨")]
    public int currentHP;

    [Tooltip("현재 MP - 실시간으로 저장됨")]
    public int currentMP;

    [Tooltip("레벨 - 실시간으로 저장됨")]
    public int level = 1;

    [Tooltip("경험치 - 실시간으로 저장됨")]
    public int exp = 0;

    [Header("탈부착형 캐릭터 시스템")]
    [Tooltip("현재 장착된 직업 - 에디터에서 변경하면 배틀/맵 씬 모두 자동 반영됨")]
    public CharacterClass currentJob;

    [Tooltip("현재 장착된 스킬 리스트 - 스탯(직업)과 기술(스킬)이 분리됨")]
    public List<AbyssdawnBattle.SkillData> equippedSkills = new List<AbyssdawnBattle.SkillData>();

    [Tooltip("현재 장착된 패시브 리스트 - SkillData(Passive) 에셋을 사용")]
    public List<AbyssdawnBattle.SkillData> equippedPassives = new List<AbyssdawnBattle.SkillData>();

    [Header("스킬 트리 시스템")]
    [Tooltip("배운 스킬 목록 (스킬 트리에서 배운 모든 스킬)")]
    public List<AbyssdawnBattle.SkillData> learnedSkills = new List<AbyssdawnBattle.SkillData>();

    [Tooltip("사용 가능한 스킬 포인트")]
    public int skillPoints = 0;

    [Header("장비 시스템")]
    [Tooltip("오른손 장비 (한손 무기 또는 양손 무기)")]
    public AbyssdawnBattle.EquipmentData rightHand;

    [Tooltip("왼손 장비 (한손 무기 또는 방패)")]
    public AbyssdawnBattle.EquipmentData leftHand;

    [Tooltip("몸통 장비 (갑옷)")]
    public AbyssdawnBattle.EquipmentData body;

    [Tooltip("장신구 1")]
    public AbyssdawnBattle.EquipmentData accessory1;

    [Tooltip("장신구 2")]
    public AbyssdawnBattle.EquipmentData accessory2;

    [Header("종의 기억 (Memory of Species)")]
    [Tooltip("종의 기억 슬롯 1 - 3개 장착 시 특성 발동")]
    public MemoryOfSpeciesData memorySlot1;

    [Tooltip("종의 기억 슬롯 2 - 3개 장착 시 특성 발동")]
    public MemoryOfSpeciesData memorySlot2;

    [Tooltip("종의 기억 슬롯 3 - 3개 장착 시 특성 발동")]
    public MemoryOfSpeciesData memorySlot3;

    [Header("종의 특성 (Traits of Species)")]
    [Tooltip("활성화된 종의 특성 - 종의 기억 3개 장착 시 자동 활성화")]
    public TraitsOfSpeciesData activeTrait;
}