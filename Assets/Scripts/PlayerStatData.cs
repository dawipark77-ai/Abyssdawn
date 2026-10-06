using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// 플레이어의 정적 기본 스탯/장비/스킬 데이터를 저장하는 ScriptableObject.
///
/// [2026-05-07 리팩터] 런타임 데이터(currentHP/MP, level, exp, freeStatPoints,
/// allocated*, skillPoints)는 이 SO에서 분리되어 PlayerStats(MonoBehaviour)에서
/// 직접 보유합니다. Play 모드 종료 후에도 .asset에 데이터가 누적되던 문제 해결.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerStatData", menuName = "MyRPG/PlayerData Asset", order = 1)]
public class PlayerStatData : ScriptableObject
{
    [Header("=== 기본 스탯 (Base Stats) ===")]
    [Tooltip("레벨 1 기준 기본 HP")]
    [FormerlySerializedAs("baseHP")]
    public int baseHP = 20;

    [Tooltip("레벨 1 기준 기본 MP")]
    [FormerlySerializedAs("baseMP")]
    public int baseMP = 0;

    [Tooltip("레벨 1 기준 기본 공격력")]
    [FormerlySerializedAs("baseAttack")]
    public int baseAttack = 5;

    [Tooltip("레벨 1 기준 기본 방어력")]
    [FormerlySerializedAs("baseDefense")]
    public int baseDefense = 5;

    [Tooltip("레벨 1 기준 기본 마력")]
    [FormerlySerializedAs("baseMagic")]
    public int baseMagic = 5;

    [Tooltip("레벨 1 기준 기본 민첩")]
    [FormerlySerializedAs("baseAgility")]
    public int baseAgility = 5;

    [Tooltip("레벨 1 기준 기본 행운")]
    [FormerlySerializedAs("baseLuck")]
    public int baseLuck = 3;

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
        EnforceTwoHandedRule();
        CheckAndActivateTrait();
    }

    /// <summary>
    /// 새 탐험 (저장 없이 죽음) — 배운 스킬·장착 스킬·장착 패시브를 모두 비운다.
    /// 슬롯 개수(액티브 6, 패시브 3)는 유지하고 내용만 null. 스킬 포인트는 주인공 새로 생성 시 StartingSkillPoints 로.
    /// </summary>
    public void ResetSkillsForNewRun()
    {
        if (learnedSkills == null) learnedSkills = new List<AbyssdawnBattle.SkillData>();
        learnedSkills.Clear();
        ClearSlots(ref equippedSkills, 6);
        ClearSlots(ref equippedPassives, 3);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[PlayerStatData] 새 탐험 — 배운 스킬·장착 스킬·패시브 초기화");
    }

    /// <summary>
    /// 막 배운 스킬을 빈 슬롯에 자동 장착 (액티브 → equippedSkills 6칸, 패시브 → equippedPassives 3칸).
    /// 이미 장착돼 있거나 빈 칸이 없으면 false — 그때는 SelectedSkill 패널에서 직접 장착.
    /// </summary>
    public bool AutoEquipIfFree(AbyssdawnBattle.SkillData skill)
    {
        if (skill == null || skill.fieldSkill) return false; // 탐험 스킬은 칸을 쓰지 않는다
        bool passive = skill.IsPassive;
        if (passive) { if (equippedPassives == null) equippedPassives = new List<AbyssdawnBattle.SkillData>(); }
        else if (equippedSkills == null) equippedSkills = new List<AbyssdawnBattle.SkillData>();
        var list = passive ? equippedPassives : equippedSkills;
        int slots = passive ? 3 : 6;
        if (list.Contains(skill)) return false;
        while (list.Count < slots) list.Add(null);
        for (int i = 0; i < slots; i++)
        {
            if (list[i] != null) continue;
            list[i] = skill;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[PlayerStatData] '{skill.skillName}' 자동 장착 → {(passive ? "패시브" : "액티브")} 슬롯 {i + 1}");
            return true;
        }
        return false;
    }

    /// <summary>skill 이 들어갈 칸 목록 (액티브 6 / 패시브 3, 빈 칸은 null). 탐험 스킬이면 null.</summary>
    public List<AbyssdawnBattle.SkillData> SlotListFor(AbyssdawnBattle.SkillData skill)
    {
        if (skill == null || skill.fieldSkill) return null;
        bool passive = skill.IsPassive;
        if (passive) { if (equippedPassives == null) equippedPassives = new List<AbyssdawnBattle.SkillData>(); }
        else if (equippedSkills == null) equippedSkills = new List<AbyssdawnBattle.SkillData>();
        var list = passive ? equippedPassives : equippedSkills;
        int slots = passive ? 3 : 6;
        while (list.Count < slots) list.Add(null);
        return list;
    }

    /// <summary>index 칸에 skill 장착 (원래 있던 스킬은 칸에서 빠지고 배운 목록에는 남는다).</summary>
    public void EquipAt(AbyssdawnBattle.SkillData skill, int index)
    {
        var list = SlotListFor(skill);
        if (list == null || index < 0 || index >= list.Count) return;
        int already = list.IndexOf(skill);
        if (already >= 0) list[already] = null;
        list[index] = skill;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[PlayerStatData] '{skill.skillName}' → {(skill.IsPassive ? "패시브" : "액티브")} 슬롯 {index + 1} 교체 장착");
    }

    /// <summary>탐험 스킬(fieldSkill)을 배웠는지 — 이름으로.</summary>
    public bool HasFieldSkill(string skillName)
    {
        if (learnedSkills == null) return false;
        foreach (var s in learnedSkills) if (s != null && s.fieldSkill && s.skillName == skillName) return true;
        return false;
    }

    private static void ClearSlots(ref List<AbyssdawnBattle.SkillData> list, int minCount)
    {
        if (list == null) list = new List<AbyssdawnBattle.SkillData>();
        int count = Mathf.Max(list.Count, minCount);
        list.Clear();
        for (int i = 0; i < count; i++) list.Add(null);
    }

    /// <summary>
    /// 양손 무기 장착 규칙 강제 — 오른손이 TwoHanded면 왼손 자동 해제
    /// 인스펙터 직접 편집 시에도 작동
    /// </summary>
    private void EnforceTwoHandedRule()
    {
        // 오른손이 양손 무기면 왼손 강제 해제
        if (rightHand != null && rightHand.isTwoHanded && leftHand != null)
        {
            Debug.Log($"[PlayerStatData] Two-handed weapon '{rightHand.equipmentName}' equipped — auto-unequipping left hand '{leftHand.equipmentName}'");
            leftHand = null;
        }
        // 왼손에 양손 무기가 들어온 경우 오른손으로 이동
        if (leftHand != null && leftHand.isTwoHanded)
        {
            Debug.Log($"[PlayerStatData] Two-handed weapon '{leftHand.equipmentName}' moved to right hand, left hand cleared.");
            rightHand = leftHand;
            leftHand = null;
        }
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