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

    /// <summary>
    /// 에디터에서 값이 변경될 때마다 자동으로 종의 특성 활성화 체크
    /// </summary>
    private void OnValidate()
    {
        CheckAndActivateTrait();
    }

    /// <summary>
    /// 종의 기억 3개를 체크하고 조건 충족 시 특성 자동 활성화
    /// </summary>
    public void CheckAndActivateTrait()
    {
        // 3개 슬롯이 모두 비어있으면 특성 비활성화
        if (memorySlot1 == null && memorySlot2 == null && memorySlot3 == null)
        {
            activeTrait = null;
            return;
        }

        // 3개가 모두 채워져야 함
        if (memorySlot1 == null || memorySlot2 == null || memorySlot3 == null)
        {
            activeTrait = null;
            return;
        }

        // 모두 같은 종족인지 체크
        SpeciesType species = memorySlot1.species;
        
        if (memorySlot2.species != species || memorySlot3.species != species)
        {
            // 종족이 다르면 특성 비활성화
            activeTrait = null;
            Debug.Log($"[종의 특성] 종족이 일치하지 않습니다. Slot1:{memorySlot1.species}, Slot2:{memorySlot2.species}, Slot3:{memorySlot3.species}");
            return;
        }

        // 3개가 모두 같은 종족이면 해당 특성 활성화
        TraitsOfSpeciesData targetTrait = GetTraitBySpecies(species);
        
        if (targetTrait != null)
        {
            activeTrait = targetTrait;
            Debug.Log($"[종의 특성] ✅ {species} 특성 활성화! - {targetTrait.traitNameEnglish}");
        }
        else
        {
            activeTrait = null;
            Debug.LogWarning($"[종의 특성] ⚠️ {species} 종족의 특성 SO가 설정되지 않았습니다! Inspector에서 {species}Trait을 할당해주세요.");
        }
    }

    /// <summary>
    /// 종족에 맞는 특성 SO를 자동으로 로드하여 반환
    /// Resources/Traits/ 폴더에서 자동 검색
    /// </summary>
    private TraitsOfSpeciesData GetTraitBySpecies(SpeciesType species)
    {
        string traitPath = "";
        
        switch (species)
        {
            case SpeciesType.Human:
                traitPath = "Traits/Human_LastStand";
                break;
            case SpeciesType.Elf:
                traitPath = "Traits/Elf_Trait";
                break;
            case SpeciesType.Orc:
                traitPath = "Traits/Orc_Trait";
                break;
            case SpeciesType.Halfling:
                traitPath = "Traits/Halfling_Trait";
                break;
            case SpeciesType.Dwarf:
                traitPath = "Traits/Dwarf_Trait";
                break;
            default:
                return null;
        }

        TraitsOfSpeciesData trait = Resources.Load<TraitsOfSpeciesData>(traitPath);
        
        if (trait == null)
        {
            Debug.LogWarning($"[종의 특성] ⚠️ {species} 특성을 찾을 수 없습니다! Resources/{traitPath}.asset 파일을 생성해주세요.");
        }
        
        return trait;
    }

    /// <summary>
    /// 현재 장착된 종의 기억 정보 출력 (디버그용)
    /// </summary>
    public void PrintMemoryStatus()
    {
        Debug.Log("=== 종의 기억 상태 ===");
        Debug.Log($"Slot 1: {(memorySlot1 != null ? $"{memorySlot1.memoryName} ({memorySlot1.species})" : "비어있음")}");
        Debug.Log($"Slot 2: {(memorySlot2 != null ? $"{memorySlot2.memoryName} ({memorySlot2.species})" : "비어있음")}");
        Debug.Log($"Slot 3: {(memorySlot3 != null ? $"{memorySlot3.memoryName} ({memorySlot3.species})" : "비어있음")}");
        Debug.Log($"활성화된 특성: {(activeTrait != null ? activeTrait.traitNameEnglish : "없음")}");
    }
}