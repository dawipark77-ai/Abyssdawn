using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using Abyssdawn;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AbyssdawnBattle;

public class PlayerStats : MonoBehaviour
{
    [Header("1. 저장 금고 (에셋 파일 연결)")]
    [Tooltip("PlayerStatData 에셋을 연결하세요. HP/MP가 실시간으로 이 에셋에 저장됩니다.")]
    public PlayerStatData statData;

    // --- [런타임 상태 데이터] ---
    // [2026-05-07] PlayerStatData(SO)에서 분리되어 컴포넌트에서 직접 보유.
    // 인스펙터 노출은 디버깅 편의용 — Play 모드 종료 시 자동 리셋(SerializedField 직렬화는 GameObject 인스턴스에 한정).
    [SerializeField] private int _fallbackAllocatedAttack;
    [SerializeField] private int _fallbackAllocatedDefense;
    [SerializeField] private int _fallbackAllocatedMagic;
    [SerializeField] private int _fallbackAllocatedAgility;
    [SerializeField] private int _fallbackAllocatedLuck;

    [SerializeField] private int _fallbackCurrentHP;
    [SerializeField] private int _fallbackCurrentMP;
    [SerializeField] private int _fallbackLevel = 1;
    [SerializeField] private int _fallbackExp = 0;
    [SerializeField] private int _fallbackFreeStatPoints = 0;
    [SerializeField] private int _fallbackSkillPoints = 0;

    // [2026-05-24] Base 스탯 7종을 SO에서 분리. SO(PlayerStatData)는 초기 시드값
    // 템플릿으로만 사용. 런타임 변동은 모두 _fallbackBase*에만 기록되어
    // Play 모드 종료 후 .asset 누적 오염을 차단.
    [SerializeField] private int _fallbackBaseHP;
    [SerializeField] private int _fallbackBaseMP;
    [SerializeField] private int _fallbackBaseAttack;
    [SerializeField] private int _fallbackBaseDefense;
    [SerializeField] private int _fallbackBaseMagic;
    [SerializeField] private int _fallbackBaseAgility;
    [SerializeField] private int _fallbackBaseLuck;
    // SO에서 한 번이라도 시드됐는지 표시 (false면 다음 호출 시 SO에서 로드)
    private bool _baseStatsSeeded = false;
    private bool _isInitialized = false; // 초기화 완료 플래그
    private static bool _isFirstLaunch = true; // 앱 첫 실행 여부

    // [LastStand] 전투당 1회 사망 회피 (Human 종의 특성)
    private bool _lastStandUsed = false;

    // [NEW] 이벤트 시스템 - HP/MP/스탯이 변경될 때마다 발동
    public static event Action OnStatusChanged;

    [Header("2. 캐릭터 정보")]
    public string playerName = "Hero";

    /// <summary>영입 동료일 때 원본 CompanionSO. 프리셋 Warrior/Rogue/Wizard는 null.</summary>
    [HideInInspector] public Abyssdawn.CompanionSO companionSource;

    public bool IsRecruitedCompanion => companionSource != null;

    // GameManager 호환용: 현재 직업 에셋의 이름을 반환
    public string jobClass => (characterClass != null) ? characterClass.className : "None";

    [Header("3. 태초의 기본 수치 (런타임 보유 — SO는 초기 시드만 제공)")]
    // [2026-05-24] Base 스탯은 _fallbackBase* 필드에 저장. SO는 read-only 템플릿.
    // 시드 미완료 시 SeedBaseStatsFromSO()가 lazy load.
    public int baseHP
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseHP; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseHP = value; }
    }
    public int baseMP
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseMP; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseMP = value; }
    }
    public int baseAttack
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseAttack; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseAttack = value; }
    }
    public int baseDefense
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseDefense; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseDefense = value; }
    }
    public int baseMagic
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseMagic; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseMagic = value; }
    }
    public int baseAgility
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseAgility; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseAgility = value; }
    }
    public int baseLuck
    {
        get { EnsureBaseStatsSeeded(); return _fallbackBaseLuck; }
        set { EnsureBaseStatsSeeded(); _fallbackBaseLuck = value; }
    }

    /// <summary>
    /// SO에서 base 스탯 7개를 _fallback* 필드로 1회 복사. 이후 모든 변동은 _fallback*에만.
    /// statData == null이면 코드 디폴트(20/0/5/5/5/5/3)로 시드.
    /// </summary>
    private void EnsureBaseStatsSeeded()
    {
        if (_baseStatsSeeded) return;
        if (statData != null)
        {
            _fallbackBaseHP      = statData.baseHP;
            _fallbackBaseMP      = statData.baseMP;
            _fallbackBaseAttack  = statData.baseAttack;
            _fallbackBaseDefense = statData.baseDefense;
            _fallbackBaseMagic   = statData.baseMagic;
            _fallbackBaseAgility = statData.baseAgility;
            _fallbackBaseLuck    = statData.baseLuck;
        }
        else
        {
            _fallbackBaseHP      = 20;
            _fallbackBaseMP      = 0;
            _fallbackBaseAttack  = 5;
            _fallbackBaseDefense = 5;
            _fallbackBaseMagic   = 5;
            _fallbackBaseAgility = 5;
            _fallbackBaseLuck    = 3;
        }
        _baseStatsSeeded = true;
    }

    [Header("4. 장착된 직업 데이터 (런타임 - 읽기 전용)")]
    [Tooltip("이 필드는 Awake()에서 statData.currentJob을 읽어 자동 설정됩니다. 에디터에서 직접 수정하지 마세요!")]
    public CharacterClass characterClass;

    // 자유 분배로 증가한 기본 스탯(직업/장비/패시브 적용 전 순수 플레이어 투자치)
    // [2026-05-07] PlayerStatData(SO)에서 분리됨 — 컴포넌트의 _fallback* 필드가 primary
    public int AllocatedAttack   => _fallbackAllocatedAttack;
    public int AllocatedDefense  => _fallbackAllocatedDefense;
    public int AllocatedMagic    => _fallbackAllocatedMagic;
    public int AllocatedAgility  => _fallbackAllocatedAgility;
    public int AllocatedLuck     => _fallbackAllocatedLuck;

    // 자유 분배에 사용할 남은 스탯 포인트
    public int FreeStatPoints
    {
        get => _fallbackFreeStatPoints;
        set { _fallbackFreeStatPoints = Mathf.Max(0, value); OnStatusChanged?.Invoke(); }
    }

    // 스킬 트리에 사용할 스킬 포인트(LP) — Lore Points
    // [2026-05-07] PlayerStatData(SO)에서 분리됨 — SwordSkillTreeManager/NewSkillTreeUI에서 사용
    public int skillPoints
    {
        get => _fallbackSkillPoints;
        set { _fallbackSkillPoints = Mathf.Max(0, value); OnStatusChanged?.Invoke(); }
    }

    // --- [실시간 조립 계산식 - 직업 배율 → 직업 가산 → 패시브/장비/특성] ---
    // MaxHP = (baseHP × hpMultiplier) + hpBonus + 패시브/장비/특성
    public int maxHP
    {
        get
        {
            // 1. Base × Multiplier
            float multiplier = characterClass != null ? characterClass.hpMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(baseHP * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.hpBonus : 0;

            int memoryFlatHp = GetMemorySpeciesHpFlatBonus();
            int memoryHpFromPercent = Mathf.RoundToInt(baseHP * GetMemorySpeciesHpBonusPercentSum() / 100f);

            // 3. + 기타
            int passiveBonus = GetPassiveHPBonus();
            int equipmentBonus = GetEquipmentHPBonus();
            int traitBonus = GetTraitBonus(PassiveBonusStat.HP);

            return baseValue + classBonus + memoryFlatHp + memoryHpFromPercent + passiveBonus + equipmentBonus + traitBonus;
        }
    }

    public int maxMP
    {
        get
        {
            // 1. Base × Multiplier
            float multiplier = characterClass != null ? characterClass.mpMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(baseMP * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.mpBonus : 0;

            int memoryMpFromPercent = Mathf.RoundToInt(baseMP * GetMemorySpeciesMpBonusPercentSum() / 100f);

            // 3. + 기타
            int passiveBonus = GetPassiveMPBonus();
            int equipmentBonus = GetEquipmentMPBonus(baseValue + classBonus + passiveBonus);
            int traitBonus = GetTraitBonus(PassiveBonusStat.MP);

            return baseValue + classBonus + memoryMpFromPercent + passiveBonus + equipmentBonus + traitBonus;
        }
    }

    // 대문자 버전 (BattleManager 등 최신 스크립트용)
    public int Attack
    {
        get
        {
            // 1. (Base + Allocated) × Multiplier
            int pureBase = baseAttack + AllocatedAttack;
            float multiplier = characterClass != null ? characterClass.attackMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(pureBase * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.attackBonus : 0;

            // 3. + 기타
            return baseValue + classBonus + GetPassiveAttackBonus() + GetEquipmentAttackBonus() + GetTraitBonus(PassiveBonusStat.Attack);
        }
    }

    public int Defense
    {
        get
        {
            // 1. (Base + Allocated) × Multiplier
            int pureBase = baseDefense + AllocatedDefense;
            float multiplier = characterClass != null ? characterClass.defenseMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(pureBase * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.defenseBonus : 0;

            // 3. + 기타
            return baseValue + classBonus + GetPassiveDefenseBonus() + GetEquipmentDefenseBonus() + GetTraitBonus(PassiveBonusStat.Defense);
        }
    }

    public int Magic
    {
        get
        {
            // 1. (Base + Allocated) × Multiplier
            int pureBase = baseMagic + AllocatedMagic;
            float multiplier = characterClass != null ? characterClass.magicMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(pureBase * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.magicBonus : 0;

            // 3. + 기타
            return baseValue + classBonus + GetPassiveMagicBonus() + GetEquipmentMagicBonus() + GetTraitBonus(PassiveBonusStat.Magic);
        }
    }

    public int Agility
    {
        get
        {
            // 1. (Base + Allocated) × Multiplier
            int pureBase = baseAgility + AllocatedAgility;
            float multiplier = characterClass != null ? characterClass.agilityMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(pureBase * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.agilityBonus : 0;

            // 3. + 기타
            return baseValue + classBonus + GetPassiveAgilityBonus() + GetEquipmentAgilityBonus() + GetTraitBonus(PassiveBonusStat.Agility);
        }
    }

    public int Luck
    {
        get
        {
            // 1. (Base + Allocated) × Multiplier
            int pureBase = baseLuck + AllocatedLuck;
            float multiplier = characterClass != null ? characterClass.luckMultiplier : 1.0f;
            int baseValue = Mathf.RoundToInt(pureBase * multiplier);

            // 2. + Class Bonus
            int classBonus = characterClass != null ? characterClass.luckBonus : 0;

            // 3. + 기타
            return baseValue + classBonus + GetPassiveLuckBonus() + GetEquipmentLuckBonus() + GetTraitBonus(PassiveBonusStat.Luck);
        }
    }

    [Header("5. 전투 포지션 (전열 / 후열)")]
    [Tooltip("현재 슬롯 위치 (BattleLine에서 자동 설정됨). 슬롯 1,2 = 전열, 슬롯 3,4 = 후열.")]
    public BattleSlot currentSlot = BattleSlot.Slot1;

    [HideInInspector]
    public bool isFrontRow = true; // 레거시 호환 - IsFrontRow 프로퍼티는 currentSlot 기반으로 계산됩니다.

    public bool IsFrontRow => SlotHelper.IsFrontRow(currentSlot);
    public bool IsBackRow => SlotHelper.IsBackRow(currentSlot);

    // 소문자 버전 (GameManager 등 기존 레거시 스크립트 호환용)
    public int attack  => Attack;
    public int defense => Defense;
    public int magic   => Magic;
    public int agility => Agility;
    public int luck    => Luck;

    public float GetScaleValue(ScaleStat stat)
    {
        switch (stat)
        {
            case ScaleStat.Attack:
                return Attack;
            case ScaleStat.Defense:
                return Defense;
            case ScaleStat.Magic:
                return Magic;
            case ScaleStat.Agility:
                return Agility;
            case ScaleStat.Luck:
                return Luck;
            case ScaleStat.CurrentHPPercent:
                return maxHP > 0 ? Mathf.Clamp01((float)currentHP / maxHP) : 1f;
            case ScaleStat.CurrentMPPercent:
                return maxMP > 0 ? Mathf.Clamp01((float)currentMP / maxMP) : 1f;
            case ScaleStat.None:
            default:
                return 1f;
        }
    }

    // --- [런타임 데이터 프로퍼티] ---
    // [2026-05-07] PlayerStatData(SO)에서 분리됨 — 컴포넌트 인스턴스가 진실의 단일 소스.
    // Play 모드 종료 시 자동 리셋되어 .asset 누적 문제 해결.
    public int currentHP
    {
        get => _fallbackCurrentHP;
        set
        {
            int oldValue = _fallbackCurrentHP;
            int newValue = Mathf.Clamp(value, 0, maxHP);

            _fallbackCurrentHP = newValue;

            // [DEBUG] HP 변경 로그
            if (oldValue != newValue)
            {
                Debug.Log($"[PlayerStats] {playerName} currentHP 변경: {oldValue} -> {newValue} (maxHP: {maxHP})");

                // [DEBUG] 이벤트 구독자 수 확인
                if (OnStatusChanged != null)
                {
                    int subscriberCount = OnStatusChanged.GetInvocationList().Length;
                    Debug.Log($"[PlayerStats] OnStatusChanged 이벤트 발동! (구독자 수: {subscriberCount})");
                }
                else
                {
                    Debug.LogWarning($"[PlayerStats] OnStatusChanged 이벤트에 구독자가 없습니다!");
                }
            }

            // [NEW] HP 변경 시 이벤트 발동
            OnStatusChanged?.Invoke();
        }
    }
    public int currentMP
    {
        get => _fallbackCurrentMP;
        set
        {
            int oldValue = _fallbackCurrentMP;
            int newValue = Mathf.Clamp(value, 0, maxMP);
            Debug.Log($"[MP_TRACE] MP set to {newValue} by {gameObject.name}");

            _fallbackCurrentMP = newValue;

            // [DEBUG] MP 변경 로그
            if (oldValue != newValue)
            {
                Debug.Log($"[PlayerStats] {playerName} currentMP 변경: {oldValue} -> {newValue} (maxMP: {maxMP})");
            }

            // [NEW] MP 변경 시 이벤트 발동
            OnStatusChanged?.Invoke();
        }
    }
    public int level
    {
        get => _fallbackLevel;
        set
        {
            _fallbackLevel = value;

            // [NEW] 레벨 변경 시 이벤트 발동
            OnStatusChanged?.Invoke();
        }
    }
    public int exp
    {
        get => _fallbackExp;
        set
        {
            _fallbackExp = value;

            // [NEW] 경험치 변경 시 이벤트 발동
            OnStatusChanged?.Invoke();
        }
    }

    // --- [패시브 보너스 계산 메서드] ---
    private enum PassiveBonusStat { HP, MP, Attack, Defense, Magic, Agility, Luck }

    private int GetPassiveBonus(PassiveBonusStat stat)
    {
        if (statData == null || statData.equippedPassives == null) return 0;
        int total = 0;

        Debug.Log($"[GetPassiveBonus] Calculating {stat} bonus from {statData.equippedPassives.Count} passives");

        foreach (var passive in statData.equippedPassives)
        {
            if (passive == null || !passive.IsPassive) continue;

            Debug.Log($"  - Checking passive: {passive.skillName}");

            bool usedEffect = false;
            if (passive.Effects != null && passive.Effects.Count > 0)
            {
                Debug.Log($"    Effects count: {passive.Effects.Count}");
                foreach (var effect in passive.Effects)
                {
                    if (effect == null) continue;

                    int amount = Mathf.RoundToInt(effect.effectAmount);
                    Debug.Log($"    Effect: type={effect.effectType}, amount={amount}");
                    
                    switch (stat)
                    {
                        case PassiveBonusStat.HP:
                            if (effect.effectType == EffectType.Recovery &&
                                (effect.recoveryTarget == RecoveryTarget.HP || effect.recoveryTarget == RecoveryTarget.Both))
                            {
                                total += amount;
                                usedEffect = true;
                            }
                            break;
                        case PassiveBonusStat.MP:
                            if (effect.effectType == EffectType.Recovery &&
                                (effect.recoveryTarget == RecoveryTarget.MP || effect.recoveryTarget == RecoveryTarget.Both))
                            {
                                total += amount;
                                usedEffect = true;
                            }
                            break;
                        case PassiveBonusStat.Attack:
                            if (effect.effectType == EffectType.BuffAttack ||
                                effect.effectType == EffectType.PassiveAttack)
                            {
                                total += amount;
                                usedEffect = true;
                                Debug.Log($"      → Attack +{amount} (from effect)");
                            }
                            break;
                        case PassiveBonusStat.Defense:
                            if (effect.effectType == EffectType.BuffDefense ||
                                effect.effectType == EffectType.PassiveDefense)
                            {
                                total += amount;
                                usedEffect = true;
                            }
                            break;
                        case PassiveBonusStat.Magic:
                            if (effect.effectType == EffectType.PassiveMagic)
                            {
                                total += amount;
                                usedEffect = true;
                            }
                            break;
                        case PassiveBonusStat.Agility:
                            if (effect.effectType == EffectType.PassiveAgility)
                            {
                                total += amount;
                                usedEffect = true;
                            }
                            break;
                        case PassiveBonusStat.Luck:
                            if (effect.effectType == EffectType.PassiveLuck)
                            {
                                total += amount;
                                usedEffect = true;
                            }
                            break;
                    }
                }
            }

            // Fallback 로직 제거: 모든 패시브는 명시적인 effectType을 사용해야 함
            // Shield Wall 같은 조건부 효과는 stat bonus를 주지 않음
        }

        Debug.Log($"[GetPassiveBonus] Total {stat} bonus: +{total}");
        return total;
    }

    private int GetPassiveHPBonus() => GetPassiveBonus(PassiveBonusStat.HP);
    private int GetPassiveMPBonus() => GetPassiveBonus(PassiveBonusStat.MP);
    private int GetPassiveAttackBonus()
    {
        int bonus = GetPassiveBonus(PassiveBonusStat.Attack);

        // 기본 검술(Basic Swordsmanship) 전용 추가 보정
        bonus += GetBasicSwordsmanshipAttackBonus();

        Debug.LogWarning($"★★★ FINAL PASSIVE ATTACK BONUS: +{bonus} ★★★");
        return bonus;
    }
    private int GetPassiveDefenseBonus() => GetPassiveBonus(PassiveBonusStat.Defense);
    private int GetPassiveMagicBonus() => GetPassiveBonus(PassiveBonusStat.Magic);
    private int GetPassiveAgilityBonus() => GetPassiveBonus(PassiveBonusStat.Agility);
    private int GetPassiveLuckBonus() => GetPassiveBonus(PassiveBonusStat.Luck);

    /// <summary>
    /// 패시브 스킬로부터 명중률 보정치를 가져옵니다 (0.0 ~ 1.0 범위).
    /// </summary>
    public float GetPassiveAccuracyBonus()
    {
        if (statData == null || statData.equippedPassives == null) return 0f;
        float total = 0f;

        foreach (var passive in statData.equippedPassives)
        {
            if (passive == null || !passive.IsPassive) continue;

            if (passive.Effects != null && passive.Effects.Count > 0)
            {
                foreach (var effect in passive.Effects)
                {
                    if (effect == null) continue;
                    if (effect.effectType == EffectType.PassiveAccuracy)
                    {
                        // effectAmount를 명중률 보정치로 사용 (예: 0.1 = 10% 증가)
                        total += effect.effectAmount;
                    }
                }
            }
        }

        // 기본 검술(Basic Swordsmanship) 명중률 +5%
        if (HasEquippedPassiveByName("Basic Swordsmanship"))
        {
            total += 0.05f;
        }

        return total;
    }

    /// <summary>
    /// 현재 장착된 패시브 목록에서 특정 이름의 패시브를 가지고 있는지 확인
    /// (SO ID 시스템 도입 전까지 임시로 skillName 문자열을 사용)
    /// </summary>
    private bool HasEquippedPassiveByName(string skillName)
    {
        if (statData == null || statData.equippedPassives == null) return false;
        foreach (var passive in statData.equippedPassives)
        {
            if (passive == null) continue;
            if (!passive.IsPassive) continue;
            if (passive.skillName == skillName) return true;
        }
        return false;
    }

    /// <summary>
    /// 기본 검술 패시브로 인한 추가 공격력 보정
    /// - 검 장착 시 기본 공격력 +2
    /// - 레벨당 공격력 +0.25 (소수점은 내림 처리)
    /// </summary>
    private int GetBasicSwordsmanshipAttackBonus()
    {
        if (!HasEquippedPassiveByName("Basic Swordsmanship")) return 0;

        // TODO: "검 장착 여부" 체크는 장비 타입 시스템 도입 시 EquipmentManager 기반으로 교체
        bool hasSwordEquipped = true;

        if (!hasSwordEquipped) return 0;

        int bonus = 2;
        bonus += Mathf.FloorToInt(level * 0.25f);
        Debug.Log($"[BasicSwordsmanship] Attack bonus: +{bonus} (level {level})");
        return bonus;
    }

    /// <summary>
    /// 장비로부터 HP 보정치를 가져옵니다.
    /// </summary>
    private int GetTraitBonus(PassiveBonusStat stat)
    {
        if (statData == null || statData.activeTrait == null) return 0;
        var t = statData.activeTrait;
        switch (stat)
        {
            case PassiveBonusStat.HP:      return t.hpBonus;
            case PassiveBonusStat.Attack:  return t.attackBonus;
            case PassiveBonusStat.Defense: return t.defenseBonus;
            case PassiveBonusStat.Magic:   return t.magicBonus;
            case PassiveBonusStat.Agility: return t.agilityBonus;
            case PassiveBonusStat.Luck:    return t.luckBonus;
            default: return 0;
        }
    }

    private List<EquipmentData> GetEquippedItemsList()
    {
        EquipmentManager equipmentManager = GetComponent<EquipmentManager>();
        if (equipmentManager != null)
            return equipmentManager.GetEquippedItems();

        if (statData != null)
        {
            var list = new List<EquipmentData>();
            if (statData.rightHand != null)  list.Add(statData.rightHand);
            if (statData.leftHand != null)   list.Add(statData.leftHand);
            if (statData.body != null)       list.Add(statData.body);
            if (statData.accessory1 != null) list.Add(statData.accessory1);
            if (statData.accessory2 != null) list.Add(statData.accessory2);
            return list;
        }

        return new List<EquipmentData>();
    }

    public int GetEquipmentHPBonus()
    {
        int total = 0;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.hpBonus;
        return total;
    }

    /// <summary>
    /// 장비로부터 MP 보정치를 가져옵니다.
    /// baseForPercent: mpBonusPercent 계산의 기준이 되는 MP값 (기본 + 패시브). 0이면 퍼센트 계산 생략.
    /// </summary>
    public int GetEquipmentMPBonus(int baseForPercent = 0)
    {
        int total = 0;
        float totalPercent = 0f;
        foreach (var item in GetEquippedItemsList())
        {
            if (item != null)
            {
                total += item.mpBonus;
                totalPercent += item.mpBonusPercent;
            }
        }
        if (baseForPercent > 0 && totalPercent > 0f)
            total += Mathf.RoundToInt(baseForPercent * totalPercent);
        return total;
    }

    /// <summary>
    /// 장비로부터 공격력 보정치를 가져옵니다.
    /// </summary>
    public int GetEquipmentAttackBonus()
    {
        int total = 0;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.attackBonus;
        return total;
    }

    /// <summary>
    /// 장비로부터 방어력 보정치를 가져옵니다.
    /// </summary>
    public int GetEquipmentDefenseBonus()
    {
        int total = 0;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.defenseBonus;
        return total;
    }

    /// <summary>
    /// 장비로부터 마법력 보정치를 가져옵니다.
    /// </summary>
    public int GetEquipmentMagicBonus()
    {
        int total = 0;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.magicBonus;
        return total;
    }

    /// <summary>
    /// 장비로부터 민첩 보정치를 가져옵니다.
    /// </summary>
    public int GetEquipmentAgilityBonus()
    {
        int total = 0;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.agiBonus;
        return total;
    }

    /// <summary>
    /// 장비로부터 행운 보정치를 가져옵니다.
    /// </summary>
    public int GetEquipmentLuckBonus()
    {
        int total = 0;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.luckBonus;
        return total;
    }

    private void AppendMemoriesToList(System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData> list)
    {
        if (statData == null || list == null) return;
        if (statData.memorySlot1 != null) list.Add(statData.memorySlot1);
        if (statData.memorySlot2 != null) list.Add(statData.memorySlot2);
        if (statData.memorySlot3 != null) list.Add(statData.memorySlot3);
    }

    private int GetMemorySpeciesHpFlatBonus()
    {
        if (statData == null) return 0;
        var list = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>(3);
        AppendMemoriesToList(list);
        int sum = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) sum += list[i].hpBonus;
        return sum;
    }

    private float GetMemorySpeciesHpBonusPercentSum()
    {
        if (statData == null) return 0f;
        var list = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>(3);
        AppendMemoriesToList(list);
        float sum = 0f;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) sum += list[i].hpBonusPercent;
        return sum;
    }

    private float GetMemorySpeciesMpBonusPercentSum()
    {
        if (statData == null) return 0f;
        var list = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>(3);
        AppendMemoriesToList(list);
        float sum = 0f;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) sum += list[i].mpBonusPercent;
        return sum;
    }

    /// <summary>
    /// 장비로부터 명중률 보정치를 가져옵니다 (0.0 ~ 1.0 범위).
    /// </summary>
    public float GetEquipmentAccuracyBonus()
    {
        EquipmentManager equipmentManager = GetComponent<EquipmentManager>();
        if (equipmentManager == null) return 0f;

        float total = 0f;
        var equippedItems = equipmentManager.GetEquippedItems();
        foreach (var item in equippedItems)
        {
            if (item != null)
            {
                total += item.accuracyBonus;
            }
        }
        return total;
    }

    /// <summary>
    /// 마법 데미지 증폭 배율 (장비 magicAmplify 곱셈 합산).
    /// 1.0이 기본값. 장비 여러 개가 곱해진다.
    /// </summary>
    public float MagicAmplify
    {
        get
        {
            float result = 1f;
            foreach (var item in GetEquippedItemsList())
                if (item != null) result *= item.magicAmplify;
            return result;
        }
    }

    /// <summary>
    /// 총 역류 억제율 (장비 + 패시브 합산, 최대 0.8).
    /// </summary>
    public float TotalBackflowSuppression
    {
        get
        {
            float total = GetEquipmentBackflowSuppression() + GetPassiveBackflowSuppression();
            return Mathf.Clamp(total, 0f, 0.8f);
        }
    }

    private float GetEquipmentBackflowSuppression()
    {
        float total = 0f;
        foreach (var item in GetEquippedItemsList())
            if (item != null) total += item.backflowSuppression;
        return total;
    }

    private float GetPassiveBackflowSuppression()
    {
        if (statData == null || statData.equippedPassives == null) return 0f;
        float total = 0f;
        // equippedPassives는 SkillData 타입 (패시브 스킬) — backflowSuppression 필드 합산
        foreach (var passive in statData.equippedPassives)
            if (passive != null) total += passive.backflowSuppression;
        return total;
    }

    /// <summary>
    /// 스탯 변경 이벤트를 발동합니다. (외부에서 호출 가능)
    /// 장비 장착/해제 등으로 스탯이 변경되었을 때 UI를 업데이트하기 위해 사용합니다.
    /// </summary>
    public void NotifyStatusChanged()
    {
        OnStatusChanged?.Invoke();
    }

    // --- [UI용 보너스 계산 메서드] ---
    // HP/MP 보너스 (최종값 - 기본값)
    public int GetHPBonus() => maxHP - baseHP;
    public int GetMPBonus() => maxMP - baseMP;

    // 스탯 보너스 (직업 보정치 + 패시브 보너스 + 장비 보너스)
    public int GetAttackBonus()
    {
        int jobBonus = (characterClass != null) ? characterClass.attackBonus : 0;
        return jobBonus + AllocatedAttack + GetPassiveAttackBonus() + GetEquipmentAttackBonus();
    }

    public int GetDefenseBonus()
    {
        int jobBonus = (characterClass != null) ? characterClass.defenseBonus : 0;
        return jobBonus + AllocatedDefense + GetPassiveDefenseBonus() + GetEquipmentDefenseBonus();
    }

    public int GetMagicBonus()
    {
        int jobBonus = (characterClass != null) ? characterClass.magicBonus : 0;
        return jobBonus + AllocatedMagic + GetPassiveMagicBonus() + GetEquipmentMagicBonus();
    }

    public int GetAgilityBonus()
    {
        int jobBonus = (characterClass != null) ? characterClass.agilityBonus : 0;
        return jobBonus + AllocatedAgility + GetPassiveAgilityBonus() + GetEquipmentAgilityBonus();
    }

    public int GetLuckBonus()
    {
        int jobBonus = (characterClass != null) ? characterClass.luckBonus : 0;
        return jobBonus + AllocatedLuck + GetPassiveLuckBonus() + GetEquipmentLuckBonus();
    }

    [Header("5. 레벨 시스템")]
    [Tooltip("현재 레벨에서 다음 레벨까지 필요한 EXP — PlayerExpProgression 표와 동기화됩니다.")]
    public int maxExp = 100;

    [Header("6. 배틀 상태 (휘발성)")]
    public bool isDefending = false;
    public float defenceReduction = 0.4f;
    public float defenseBuffAmount = 0f;

    [Header("7. Status Effect System (상태이상 시스템)")]
    [Tooltip("현재 걸린 상태이상 리스트 (런타임에서 자동 관리)")]
    public List<StatusEffectInstance> activeStatusEffects = new List<StatusEffectInstance>();

    // Legacy compatibility
    public bool isIgnited => HasStatusEffect(StatusEffectType.Ignite);
    public int igniteTurnsRemaining => GetStatusEffectRemainingTurns(StatusEffectType.Ignite);

    void Awake()
    {
        // [DEBUG] 인스턴스 ID 출력 (어떤 PlayerStats가 초기화되는지 확인)
        Debug.Log($"[PlayerStats] Awake() 호출: GameObject={gameObject.name}, InstanceID={GetInstanceID()}, playerName={playerName}");
        Debug.Log($"[PlayerStats:DIAG] Awake | InstanceID={GetInstanceID()} | Scene='{gameObject.scene.name}' | GO='{gameObject.name}' | playerName='{playerName}' | _isInitialized={_isInitialized}");

        // [SAFETY] statData 즉시 진단 (this 컨텍스트로 Unity Console에서 GameObject 강조)
        if (statData == null)
        {
            Debug.LogError($"[PlayerStats] {gameObject.name}: statData가 할당되지 않았습니다! HeroData.asset을 연결하세요!", this);
        }

        // [FIX] 이미 초기화되었으면 다시 초기화하지 않음 (중복 방지)
        if (_isInitialized)
        {
            Debug.Log($"[PlayerStats] {playerName} 이미 초기화되어 있습니다. 스킵합니다. (InstanceID={GetInstanceID()})");
            return;
        }

        Debug.Log($"[PlayerStats] {playerName} 초기화 시작... (InstanceID={GetInstanceID()})");

        // [CRITICAL] statData 연결 확인
        if (statData == null)
        {
            Debug.LogError($"[PlayerStats] {playerName}의 statData가 할당되지 않았습니다! HeroData.asset을 연결하세요!");
            _fallbackCurrentHP = baseHP;
            _fallbackCurrentMP = baseMP;
            _fallbackLevel = 1;
            _fallbackExp = 0;
            return;
        }

        Debug.Log($"[PlayerStats] ✓ statData 에셋 연결됨: {statData.name}");

        // [2026-05-20] 빈 playerName이면 GameManager.SaveFromPlayer/ApplyToPlayer가 전부 스킵되어
        // 던전에서 쌓은 EXP·레벨이 전투 씬(별도 PlayerStats 인스턴스)으로 넘어가지 않음 → 전투는 항상 "새 캐릭터"처럼 동작.
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Hero";
            Debug.LogWarning("[PlayerStats] playerName이 비어 있어 'Hero'로 설정했습니다. (GameManager 파티 키·씬 간 동기화에 필요)", this);
        }

        // 종의 기억 세트 효과 런타임 체크 (OnValidate는 에디터 전용이므로 여기도 실행)
        statData.CheckAndActivateTrait();

        // [NEW] 씬 간 자동 동기화: HeroData의 currentJob을 읽어 characterClass에 할당
        if (statData.currentJob != null)
        {
            characterClass = statData.currentJob;
            Debug.Log($"[PlayerStats] ✓ HeroData에서 직업 로드: {characterClass.className}");
            Debug.Log($"[PlayerStats]   - Attack: {characterClass.attackBonus:+#;-#;0}, Defense: {characterClass.defenseBonus:+#;-#;0}, Magic: {characterClass.magicBonus:+#;-#;0}");
            Debug.Log($"[PlayerStats]   - HP/MP 직업 가산: HP +{characterClass.hpBonus}, MP +{characterClass.mpBonus}");
        }
        else if (companionSource != null)
        {
            // [2026-05-24] 영입 동료는 직업 없이 CompanionSO 고정 스탯이 진실의 단일 소스.
            // 자동 Warrior 할당이 동료 HP/스탯을 오염시키는 것을 차단.
            // characterClass는 null 유지 → maxHP 계산식에서 hpMultiplier=1.0, classBonus=0 적용 → baseHP가 그대로 최종값.
            Debug.Log($"[PlayerStats] '{playerName}' 영입 동료 — 자동 직업 할당 스킵 (CompanionSO 고정 스탯 유지)");
        }
        else
        {
            Debug.LogWarning($"[PlayerStats] HeroData에 직업이 설정되지 않았습니다! 기본 Warrior 직업을 설정합니다.");
            SetClass("Warrior");

            if (characterClass != null)
            {
                Debug.Log($"[PlayerStats] ✓ {playerName}에 Warrior 직업이 자동 설정되었습니다.");
            }
            else
            {
                Debug.LogError($"[PlayerStats] ✗ Warrior 직업 설정 실패! CharacterClassDatabase.asset 파일이 Resources 폴더에 있는지 확인하세요.");
            }
        }

        // [DEBUG] 스탯 계산 확인
        Debug.Log($"[PlayerStats] 최종 스탯 계산:");
        Debug.Log($"[PlayerStats]   - MaxHP: {maxHP} = BaseHP {baseHP} + JobHP {characterClass?.hpBonus ?? 0} + Passive/Equip/Trait");
        Debug.Log($"[PlayerStats]   - MaxMP: {maxMP} = BaseMP {baseMP} + JobMP {characterClass?.mpBonus ?? 0} + Passive/Equip");
        Debug.Log($"[PlayerStats]   - Attack: {Attack} = Base({baseAttack}) + Job({characterClass?.attackBonus ?? 0}) + Passive({GetPassiveAttackBonus()})");

        // [FIX] 조건부 초기화: 컴포넌트 보유 HP가 0이거나 비정상일 때만 maxHP로 초기화
        // [2026-05-07] PlayerStatData 분리 — 컴포넌트의 _fallbackCurrentHP가 진실
        // [2026-05-11 옵션 B] GameManager.staticPartyData 우선 복원 — 씬 전환 영속화 인프라 연결
        int currentHPFromAsset = _fallbackCurrentHP;
        int currentMPFromAsset = _fallbackCurrentMP;

        bool restoredFromGameManager = false;
        var gmInst = GameManager.Instance;
        Debug.Log($"[PlayerStats:DIAG] Awake GM check | GM={(gmInst != null ? "exists" : "NULL")} | playerName='{playerName}'");

        if (gmInst != null && !string.IsNullOrEmpty(playerName))
        {
            bool _diagHasKey = gmInst.partyData.ContainsKey(playerName);
            string _diagKeysStr = gmInst.partyData.Count > 0
                ? string.Join(",", gmInst.partyData.Keys)
                : "(empty)";
            Debug.Log($"[PlayerStats:DIAG] Awake GM dict | HasKey('{playerName}')={_diagHasKey} | Dict count={gmInst.partyData.Count} | Keys=[{_diagKeysStr}]");
        }

        if (gmInst != null && !string.IsNullOrEmpty(playerName) && gmInst.partyData.ContainsKey(playerName))
        {
            Debug.Log($"[PlayerStats:DIAG] Awake Before restore | HP={currentHP}, MP={currentMP}, EXP={exp}, Lv={level}");
            gmInst.ApplyToPlayer(this);
            restoredFromGameManager = true;
            Debug.Log($"[PlayerStats:DIAG] Awake After restore | HP={currentHP}, MP={currentMP}, EXP={exp}, Lv={level}");
            Debug.Log($"[PlayerStats] GameManager에서 상태 복원: HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}, EXP {exp}, Lv {level}");
        }

        if (!restoredFromGameManager)
        {
            if (_isFirstLaunch)
            {
                Debug.Log("[PlayerStats] 첫 실행 감지 → HP/MP 풀 초기화");
                currentHP = maxHP;
                currentMP = maxMP;
                _isFirstLaunch = false;
            }
            else
            {
                // HP 조건: 유효 범위면 유지, 그렇지 않으면 새 게임으로 초기화
                if (currentHPFromAsset > 0 && currentHPFromAsset <= maxHP)
                {
                    Debug.Log($"[PlayerStats] 컴포넌트 HP 유지: {currentHPFromAsset}/{maxHP}");
                }
                else
                {
                    Debug.Log($"[PlayerStats] 컴포넌트 HP 비정상({currentHPFromAsset}) → maxHP({maxHP})로 초기화");
                    currentHP = maxHP;
                }

                // MP 조건: HP와 동일 기준(> 0)으로 통일 — SerializeField 기본값 0 → maxMP 복원
                if (currentMPFromAsset > 0 && currentMPFromAsset <= maxMP)
                {
                    Debug.Log($"[PlayerStats] 컴포넌트 MP 유지: {currentMPFromAsset}/{maxMP}");
                }
                else
                {
                    Debug.Log($"[PlayerStats] 컴포넌트 MP 비정상({currentMPFromAsset}) → maxMP({maxMP})로 초기화");
                    currentMP = maxMP;
                }
            }

            // GameManager에 첫 등록 — 다음 씬 전환부터 영속화 인프라 동작
            if (gmInst != null && !string.IsNullOrEmpty(playerName))
            {
                gmInst.SaveFromPlayer(this);
                Debug.Log($"[PlayerStats] GameManager에 초기 스냅샷 저장: HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}");
            }
        }

        SyncMaxExpToProgressionCurve();

        _isInitialized = true; // 초기화 완료 플래그

        Debug.Log($"[PlayerStats] ===== {playerName} 초기화 완료! =====");
        Debug.Log($"[PlayerStats] InstanceID: {GetInstanceID()}");
        Debug.Log($"[PlayerStats] 직업: {characterClass?.className ?? "None"}");
        Debug.Log($"[PlayerStats] HP: {currentHP}/{maxHP}");
        Debug.Log($"[PlayerStats] MP: {currentMP}/{maxMP}");
        Debug.Log($"[PlayerStats] Attack: {Attack} (base: {baseAttack} + bonus: {GetAttackBonus()})");
        Debug.Log($"[PlayerStats] ==============================");
    }

    void Start()
    {
        // [FIX] 초기화 확인만 수행 (값 수정 안 함)
        Debug.Log($"[PlayerStats] Start() - {playerName} 상태 확인 (InstanceID={GetInstanceID()}):");
        Debug.Log($"[PlayerStats] Start() - HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}");

        // [FIX] 이벤트 발동 (UI 갱신)
        OnStatusChanged?.Invoke();
    }

    // [2026-05-11 옵션 B 마무리] 씬 전환 후에도 GameManager.staticPartyData에서 복원
    // — Awake가 _isInitialized 가드로 스킵되거나 DontDestroyOnLoad라서 재진입 안 되는 경우 대비
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoadedRestore;
        TryRestoreFromGameManager("OnEnable");
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedRestore;
    }

    private void OnSceneLoadedRestore(Scene scene, LoadSceneMode mode)
    {
        TryRestoreFromGameManager($"OnSceneLoaded({scene.name})");
    }

    private void TryRestoreFromGameManager(string context)
    {
        Debug.Log($"[PlayerStats:DIAG] TryRestore({context}) | playerName='{playerName}' | InstanceID={GetInstanceID()}");

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning($"[PlayerStats:DIAG] {context} ABORT: playerName empty");
            return;
        }
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning($"[PlayerStats:DIAG] {context} ABORT: GameManager null");
            return;
        }
        if (!gm.partyData.ContainsKey(playerName))
        {
            string _diagKeysStr = gm.partyData.Count > 0 ? string.Join(",", gm.partyData.Keys) : "(empty)";
            Debug.LogWarning($"[PlayerStats:DIAG] {context} ABORT: key '{playerName}' not in dict. Count={gm.partyData.Count}, Keys=[{_diagKeysStr}]");
            return;
        }

        Debug.Log($"[PlayerStats:DIAG] {context} Before restore | HP={currentHP}, MP={currentMP}, EXP={exp}, Lv={level}");
        gm.ApplyToPlayer(this);
        SyncMaxExpToProgressionCurve();
        Debug.Log($"[PlayerStats:DIAG] {context} After restore | HP={currentHP}, MP={currentMP}, EXP={exp}/{maxExp}, Lv={level}");
        Debug.Log($"[PlayerStats] {context}: GameManager 복원 — {playerName} HP {currentHP}/{maxHP}, MP {currentMP}/{maxMP}, EXP {exp}, Lv {level}");
        OnStatusChanged?.Invoke();
    }

    // --- [직업 관련 함수] ---

    /// <summary>
    /// 직업을 이름으로 설정 (CharacterClassDatabase 사용)
    /// </summary>
    public void SetClass(string className)
    {
        // Null check for database
        if (CharacterClassDatabase.Instance == null)
        {
            Debug.LogError($"[PlayerStats] CharacterClassDatabase를 로드할 수 없습니다! Resources/CharacterClassDatabase.asset 파일을 확인하세요.");
            return;
        }

        CharacterClass newClass = CharacterClassDatabase.Instance.GetClassByName(className);

        if (newClass != null)
        {
            // [FIX] 직업 변경 전 HP/MP 비율 계산
            // currentHP가 0이거나 maxHP가 0이면 100%로 간주 (초기 설정)
            float hpRatio = (maxHP > 0 && currentHP > 0) ? (float)currentHP / maxHP : 1.0f;
            float mpRatio = (maxMP > 0 && currentMP >= 0) ? (float)currentMP / maxMP : 1.0f;

            Debug.Log($"[PlayerStats] 직업 변경 전 - HP: {currentHP}/{maxHP} ({hpRatio:P0}), MP: {currentMP}/{maxMP} ({mpRatio:P0})");

            // [NEW] 직업 변경 - characterClass와 statData 모두 업데이트
            characterClass = newClass;

            if (statData != null)
            {
                statData.currentJob = newClass;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(statData);
                UnityEditor.AssetDatabase.SaveAssets();
#endif
                Debug.Log($"[PlayerStats] ✓ HeroData에 직업 저장: {newClass.className}");
            }

            // [FIX] 변경된 maxHP/maxMP에 동일한 비율 적용
            currentHP = Mathf.RoundToInt(maxHP * hpRatio);
            currentMP = Mathf.RoundToInt(maxMP * mpRatio);

            // 최소 1, 최대치 초과 방지
            currentHP = Mathf.Clamp(currentHP, 1, maxHP);
            currentMP = Mathf.Clamp(currentMP, 0, maxMP);

            Debug.Log($"[PlayerStats] {playerName}의 직업이 {className}으로 변경되었습니다.");
            Debug.Log($"[PlayerStats]   - HP: {currentHP}/{maxHP}, MP: {currentMP}/{maxMP}");
            Debug.Log($"[PlayerStats]   - Attack: {Attack}, Defense: {Defense}, Magic: {Magic}");
        }
        else
        {
            Debug.LogWarning($"[PlayerStats] 직업 '{className}'을(를) 찾을 수 없습니다.");
        }
    }

    // --- [전투 관련 함수] ---

    public void Defend()
    {
        isDefending = true;
        Debug.Log($"{playerName} 방어 자세 취함!");
    }

    public int TakeDamage(int damage)
    {
        float totalReduction = 0f;

        // 방어 커맨드 / 방어 버프
        if (isDefending)
        {
            totalReduction += defenceReduction;
        }
        if (defenseBuffAmount > 0)
        {
            totalReduction += (defenseBuffAmount / 100f);
        }

        // 패시브에 의한 추가 피해 감소 (예: 기본 검술 전열 -5%)
        totalReduction += GetAdditionalDamageReductionFromPassives();

        // 과도한 감소 방지
        totalReduction = Mathf.Clamp(totalReduction, 0f, 0.9f);

        int finalDamage = Mathf.Max(1, Mathf.FloorToInt(damage * (1f - totalReduction)));

        // 한 턴짜리 방어 상태/버프는 소모
        isDefending = false;
        defenseBuffAmount = 0f;

        int newHP = currentHP - finalDamage;

        // [LastStand] HP가 0 이하로 떨어질 때, Human 종의 특성 발동 체크
        if (newHP <= 0 && !_lastStandUsed && HasSpecialEffect("LastStand"))
        {
            // Luck 기반 생존 확률: 30% 기본 + Luck당 2% (최대 80%)
            float survivalChance = Mathf.Clamp(30f + Luck * 2f, 30f, 80f);
            if (UnityEngine.Random.Range(0f, 100f) < survivalChance)
            {
                _lastStandUsed = true;
                currentHP = 1;
                Debug.Log($"[LastStand] {playerName} Last Stand triggered! Survived with 1 HP. (chance {survivalChance:F0}%)");
                NotifyStatusChanged();
                return finalDamage;
            }
            else
            {
                Debug.Log($"[LastStand] {playerName} Last Stand failed. (chance {survivalChance:F0}%)");
            }
        }

        currentHP = Mathf.Clamp(newHP, 0, maxHP);
        if (currentHP <= 0) Die();
        return finalDamage;
    }

    /// <summary>
    /// 종의 특성 specialEffectTags에 해당 태그가 있는지 확인
    /// </summary>
    public bool HasSpecialEffect(string tag)
    {
        if (statData == null || statData.activeTrait == null) return false;
        var tags = statData.activeTrait.specialEffectTags;
        if (tags == null) return false;
        foreach (var t in tags)
            if (t == tag) return true;
        return false;
    }

    /// <summary>
    /// 전투 시작 시 Last Stand 플래그 초기화
    /// </summary>
    public void ResetLastStand()
    {
        _lastStandUsed = false;
    }

    /// <summary>
    /// 패시브 스킬에서 오는 추가 피해 감소율 계산
    /// </summary>
    private float GetAdditionalDamageReductionFromPassives()
    {
        float reduction = 0f;

        // 기본 검술: 전열일 때 받는 데미지 -5%
        if (HasEquippedPassiveByName("Basic Swordsmanship") && IsFrontRow)
        {
            reduction += 0.05f;
        }

        return reduction;
    }

    public void Heal(int amount) { currentHP = Mathf.Min(currentHP + amount, maxHP); }
    public void UseMP(int amount) { currentMP = Mathf.Clamp(currentMP - amount, 0, maxMP); }
    void Die() { Debug.Log($"{playerName} 사망"); }
    
    // --- [Status Effect System Methods] ---

    /// <summary>
    /// 상태이상을 적용합니다. Luck 기반 저항 확률 체크 포함.
    /// SO 기본 physicalDuration을 사용합니다.
    /// </summary>
    public bool ApplyStatusEffect(StatusEffectSO effect)
        => ApplyStatusEffect(effect, effect != null ? effect.physicalDuration : 0);

    /// <summary>
    /// 상태이상을 적용합니다. 저장된 상태 복원 등 커스텀 턴 수가 필요할 때 사용합니다.
    /// </summary>
    public bool ApplyStatusEffect(StatusEffectSO effect, int customDuration)
    {
        if (effect == null || customDuration <= 0) return false;

        // Luck 기반 저항 (최대 25%)
        float resistChance = Mathf.Clamp(Luck * 0.2f, 0f, 25f);
        if (resistChance > 0f && UnityEngine.Random.Range(0f, 100f) < resistChance)
        {
            Debug.Log($"[StatusEffect] {playerName}가 Luck 보너스로 {effect.effectType}을 저항했습니다.");
            return false;
        }

        StatusEffectInstance existing = activeStatusEffects.Find(e => e.data.effectType == effect.effectType);
        if (existing != null)
        {
            existing.remainingTurns = Mathf.Max(existing.remainingTurns, customDuration);
            Debug.Log($"[StatusEffect] {playerName}의 {effect.effectType} 지속 갱신: {existing.remainingTurns}턴");
        }
        else
        {
            var instance = new StatusEffectInstance(effect);
            instance.remainingTurns = customDuration;
            activeStatusEffects.Add(instance);
            Debug.Log($"[StatusEffect] {playerName}에게 {effect.effectType} 적용! ({customDuration}턴)");
        }

        OnStatusChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 특정 타입의 상태이상을 제거합니다.
    /// </summary>
    public void RemoveStatusEffect(StatusEffectType type)
    {
        StatusEffectInstance se = activeStatusEffects.Find(e => e.data.effectType == type);
        if (se != null)
        {
            activeStatusEffects.Remove(se);
            Debug.Log($"[StatusEffect] {playerName}의 {type} 해제됨");
            OnStatusChanged?.Invoke();
        }
    }

    /// <summary>
    /// 모든 상태이상을 제거합니다.
    /// </summary>
    public void RemoveAllStatusEffects()
    {
        activeStatusEffects.Clear();
        Debug.Log($"[StatusEffect] {playerName}의 모든 상태이상 해제됨");
        OnStatusChanged?.Invoke();
    }

    /// <summary>
    /// 특정 타입의 상태이상이 걸려있는지 확인
    /// </summary>
    public bool HasStatusEffect(StatusEffectType type)
        => activeStatusEffects.Exists(e => e.data.effectType == type);

    /// <summary>
    /// 특정 타입의 상태이상 남은 턴 수 반환
    /// </summary>
    public int GetStatusEffectRemainingTurns(StatusEffectType type)
    {
        StatusEffectInstance se = activeStatusEffects.Find(e => e.data.effectType == type);
        return se != null ? se.remainingTurns : 0;
    }

    /// <summary>
    /// 턴 종료 시 호출: 상태이상 DoT 처리 및 턴 감소
    /// </summary>
    public void ProcessStatusEffectsEndOfTurn()
    {
        if (activeStatusEffects.Count == 0 || currentHP <= 0) return;

        Debug.Log($"[StatusEffect] {playerName}의 상태이상 처리 시작 (수: {activeStatusEffects.Count})");

        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffectInstance se = activeStatusEffects[i];

            // 적용된 턴에는 DoT/차감 없이 플래그만 해제
            if (se.appliedThisTurn)
            {
                se.appliedThisTurn = false;
                Debug.Log($"[StatusEffect] {playerName}의 {se.data.effectType} 적용된 턴 — DoT/차감 건너뜀.");
                continue;
            }

            if (se.data.physicalDamagePerTurn > 0f)
            {
                int dotDamage = Mathf.Max(1, Mathf.FloorToInt(maxHP * se.data.physicalDamagePerTurn));
                currentHP = Mathf.Max(0, currentHP - dotDamage);
                Debug.Log($"[StatusEffect] {playerName}이(가) {se.data.effectType}로 {dotDamage} DoT 피해. (남은 HP: {currentHP})");
            }

            se.remainingTurns--;
            if (se.remainingTurns <= 0)
            {
                Debug.Log($"[StatusEffect] {playerName}의 {se.data.effectType} 효과 종료");
                activeStatusEffects.RemoveAt(i);
            }
        }

        if (currentHP <= 0) Die();
        OnStatusChanged?.Invoke();
    }

    /// <summary>공격력 감소 디버프 합산</summary>
    public float GetStatusEffectAttackDebuff()
    {
        float total = 0f;
        foreach (var se in activeStatusEffects)
            total += se.data.attackDebuff;
        return Mathf.Min(total, 100f);
    }

    /// <summary>방어력 감소 디버프 합산</summary>
    public float GetStatusEffectDefenseDebuff()
    {
        float total = 0f;
        foreach (var se in activeStatusEffects)
            total += se.data.defenseDebuff;
        return Mathf.Min(total, 100f);
    }

    /// <summary>행동 불가 상태 확인 (Stun)</summary>
    public bool IsStunned() => activeStatusEffects.Exists(se => se.data.preventAction);

    /// <summary>스킬 사용 불가 상태 확인 (Silence)</summary>
    public bool IsSilenced() => activeStatusEffects.Exists(se => se.data.preventSkillUse);

    public void AddExp(int amount)
    {
        Debug.Log($"[PlayerStats:DIAG] AddExp called | amount={amount} | Before: EXP={exp}, Lv={level}, maxExp={maxExp} | playerName='{playerName}' | InstanceID={GetInstanceID()}");
        exp += amount;
        while (exp >= maxExp) LevelUp();
        Debug.Log($"[PlayerStats:DIAG] AddExp result | After: EXP={exp}, Lv={level}, maxExp={maxExp} | playerName='{playerName}' | InstanceID={GetInstanceID()}");
    }

    private void SyncMaxExpToProgressionCurve()
    {
        maxExp = Mathf.Max(1, PlayerExpProgression.GetExpToNextLevel(level));
    }

    // [LevelUp 2026-05-13] 변화량 추적 필드 — Apply*() 내부에서 누적, LevelUp 끝에 메시지 생성에 사용.
    private int _lvUpHpGain;
    private int _lvUpMpGain;
    private int _lvUpAtkGain;
    private int _lvUpDefGain;
    private int _lvUpMagGain;
    private int _lvUpAgiGain;
    private int _lvUpLukGain;

    private void LevelUp()
    {
        level++;
        exp -= maxExp;
        maxExp = Mathf.Max(1, PlayerExpProgression.GetExpToNextLevel(level));

        _lvUpHpGain = 0;
        _lvUpMpGain = 0;
        _lvUpAtkGain = 0;
        _lvUpDefGain = 0;
        _lvUpMagGain = 0;
        _lvUpAgiGain = 0;
        _lvUpLukGain = 0;

        ApplyHpMpGrowth();
        ApplyCombinedClassMemoryRandomStatGrowth();
        ApplyHpMpBonusFromDefenseMagicGainedThisLevel();
        GrantFreeStatPoint();
        GrantSkillPoint();  // [2026-05-24] 매 레벨업마다 LP +1 자동 지급
        currentHP = maxHP;
        currentMP = maxMP;
        OnStatusChanged?.Invoke();

        EmitLevelUpBattleLog();
        LogLevelUpStatsSnapshot();
    }

    /// <summary>
    /// 레벨업 직후 전투 스탯 스냅샷(장비·패시브·직업 반영 후). 콘솔 필터: <c>LevelUpStats</c>.
    /// </summary>
    private void LogLevelUpStatsSnapshot()
    {
        string n = string.IsNullOrEmpty(playerName) ? "Hero" : playerName;
        Debug.Log(
            $"[LevelUpStats] {n} | Lv{level} | HP {currentHP}/{maxHP} | MP {currentMP}/{maxMP} | " +
            $"ATK {Attack} | DEF {Defense} | MAG {Magic} | AGI {Agility} | LUK {Luck} | FreePts {FreeStatPoints}");
    }

    /// <summary>
    /// 레벨업 결과를 드퀘식 컬러 태그 배틀 로그로 BattleManager에 전송.
    /// BattleManager가 없을 경우(던전 씬 등)에는 Debug.Log로만 출력.
    /// </summary>
    private void EmitLevelUpBattleLog()
    {
        const string colorName   = "#FFD700";
        const string colorHp     = "#FF6B6B";
        const string colorMp     = "#6B9EFF";
        const string colorAtk    = "#FFA500";
        const string colorDef    = "#90EE90";
        const string colorMag    = "#C77DFF";
        const string colorAgi    = "#00CED1";
        const string colorLuk    = "#F9F871";
        const string colorPoint  = "#FFD700";

        var nameStr = string.IsNullOrEmpty(playerName) ? "Hero" : playerName;

        var lines = new System.Collections.Generic.List<string>();

        // ① 레벨업 (Level up)
        lines.Add($"<color={colorName}>{nameStr}</color> leveled up to <b>Lv {level}</b>!");

        // ② 자유 스탯 포인트 +1 획득 (Free Stat Point gained)
        int totalFreePts = _fallbackFreeStatPoints;
        lines.Add($"<color={colorPoint}>Free Stat Point +1!</color> (Total: {totalFreePts})");

        // ③ HP/MP 상승분 (변동 있을 때만)
        var hpMpGains = new System.Collections.Generic.List<string>();
        if (_lvUpHpGain > 0) hpMpGains.Add($"HP <color={colorHp}>+{_lvUpHpGain}</color>");
        if (_lvUpMpGain > 0) hpMpGains.Add($"MP <color={colorMp}>+{_lvUpMpGain}</color>");
        if (hpMpGains.Count > 0) lines.Add(string.Join(", ", hpMpGains));

        // ④ 랜덤으로 오른 스탯 (변동 있을 때만)
        var randomGains = new System.Collections.Generic.List<string>();
        if (_lvUpAtkGain > 0) randomGains.Add($"STR <color={colorAtk}>+{_lvUpAtkGain}</color>");
        if (_lvUpDefGain > 0) randomGains.Add($"DEF <color={colorDef}>+{_lvUpDefGain}</color>");
        if (_lvUpMagGain > 0) randomGains.Add($"MAG <color={colorMag}>+{_lvUpMagGain}</color>");
        if (_lvUpAgiGain > 0) randomGains.Add($"AGI <color={colorAgi}>+{_lvUpAgiGain}</color>");
        if (_lvUpLukGain > 0) randomGains.Add($"LUK <color={colorLuk}>+{_lvUpLukGain}</color>");
        if (randomGains.Count > 0) lines.Add(string.Join(", ", randomGains));

        // ⑤ 스킬 포인트(SP) +1 획득 — GrantSkillPoint가 LevelUp마다 호출되므로 항상 표시
        // 메시지에서는 풀네임 "Skill Point" 사용 (Free Stat Point와 패턴 일치). UI 헤더/팝업의 "SP" 약어와 별개.
        int totalSP = _fallbackSkillPoints;
        lines.Add($"<color={colorPoint}>Skill Point +1!</color> (Total: {totalSP})");

        // 시퀀스 출력 — 전투 씬이면 BattleManager의 타이핑 코루틴 사용,
        // 던전 씬 등 BattleManager가 없으면 Debug.Log 한 번에.
        var bm = FindFirstObjectByType<BattleManager>();
        if (bm != null && bm.messageText != null)
        {
            bm.PlayMessageSequence(lines);
        }
        else
        {
            foreach (var l in lines) Debug.Log($"[LevelUp] {l}");
        }
    }

    /// <summary>
    /// 레벨업 시 직업 + 장착 종의 기억을 <b>한 번의 가중치 추첨</b>으로 합쳐 스탯 +1.
    /// 신규 직업(독립 확률 %)은 (직업 기본 + 기억 %p)를 5~70% 클램프한 뒤 비중으로 추첨, 레거시는 attackGrowth 등 합산 가중치.
    /// </summary>
    private void ApplyCombinedClassMemoryRandomStatGrowth()
    {
        if (characterClass == null) return;

        float wAtk, wDef, wMag, wAgi, wLuk;

        if (characterClass.UsesIndependentStatLevelUpChances)
        {
            GetMemoryLevelUpChanceBonusSums(out float aM, out float dM, out float mM, out float agM, out float lM);
            wAtk = CharacterClass.ClampStatLevelUpChancePercent(characterClass.levelUpAttackChancePercent + aM);
            wDef = CharacterClass.ClampStatLevelUpChancePercent(characterClass.levelUpDefenseChancePercent + dM);
            wMag = CharacterClass.ClampStatLevelUpChancePercent(characterClass.levelUpMagicChancePercent + mM);
            wAgi = CharacterClass.ClampStatLevelUpChancePercent(characterClass.levelUpAgilityChancePercent + agM);
            wLuk = CharacterClass.ClampStatLevelUpChancePercent(characterClass.levelUpLuckChancePercent + lM);
        }
        else
        {
            wAtk = Mathf.Max(0f, characterClass.attackGrowthPerLevel);
            wDef = Mathf.Max(0f, characterClass.defenseGrowthPerLevel);
            wMag = Mathf.Max(0f, characterClass.magicGrowthPerLevel);
            wAgi = Mathf.Max(0f, characterClass.agilityGrowthPerLevel);
            wLuk = Mathf.Max(0f, characterClass.luckGrowthPerLevel);

            if (statData != null)
            {
                var memories = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>();
                AppendMemoriesToList(memories);
                foreach (var mem in memories)
                {
                    if (mem == null) continue;
                    wAtk += Mathf.Max(0f, mem.attackGrowthPerLevel);
                    wDef += Mathf.Max(0f, mem.defenseGrowthPerLevel);
                    wMag += Mathf.Max(0f, mem.magicGrowthPerLevel);
                    wAgi += Mathf.Max(0f, mem.agilityGrowthPerLevel);
                    wLuk += Mathf.Max(0f, mem.luckGrowthPerLevel);
                }
            }

            float sumLegacy = wAtk + wDef + wMag + wAgi + wLuk;
            if (sumLegacy <= 0f)
                wAtk = wDef = wMag = wAgi = wLuk = 1f;
        }

        float total = wAtk + wDef + wMag + wAgi + wLuk;
        if (total <= 0f)
            return;

        float pick = UnityEngine.Random.Range(0f, total);
        StatType chosen;
        if (pick < wAtk) chosen = StatType.Attack;
        else if (pick < wAtk + wDef) chosen = StatType.Defense;
        else if (pick < wAtk + wDef + wMag) chosen = StatType.Magic;
        else if (pick < wAtk + wDef + wMag + wAgi) chosen = StatType.Agility;
        else chosen = StatType.Luck;

        AddAllocatedStat(chosen, 1);
        TrackStatGain(chosen, 1);
    }

    private void GetMemoryLevelUpChanceBonusSums(out float atk, out float def, out float mag, out float agi, out float luk)
    {
        atk = def = mag = agi = luk = 0f;
        if (statData == null) return;
        var memories = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>();
        AppendMemoriesToList(memories);
        foreach (var mem in memories)
        {
            if (mem == null) continue;
            atk += mem.attackLevelUpChanceBonus;
            def += mem.defenseLevelUpChanceBonus;
            mag += mem.magicLevelUpChanceBonus;
            agi += mem.agilityLevelUpChanceBonus;
            luk += mem.luckLevelUpChanceBonus;
        }
    }

    /// <summary>
    /// 레벨업 시 HP/MP — 직업 hpPerLevel/mpPerLevel + 종의 기억 hpGrowthPerLevel/mpGrowthPerLevel 합을 반올림(확정, 노이즈 없음).
    /// </summary>
    private void ApplyHpMpGrowth()
    {
        int classHpGain = 0;
        int classMpGain = 0;

        if (characterClass != null)
        {
            classHpGain = Mathf.Max(0, characterClass.hpPerLevel);
            classMpGain = Mathf.Max(0, characterClass.mpPerLevel);
        }

        float memoryHpGrowth = 0f;
        float memoryMpGrowth = 0f;

        if (statData != null)
        {
            var memories = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>();
            AppendMemoriesToList(memories);

            foreach (var mem in memories)
            {
                if (mem == null) continue;
                memoryHpGrowth += mem.hpGrowthPerLevel;
                memoryMpGrowth += mem.mpGrowthPerLevel;
            }
        }

        int finalHpGain = Mathf.Max(0, Mathf.RoundToInt(classHpGain + memoryHpGrowth));
        int finalMpGain = Mathf.Max(0, Mathf.RoundToInt(classMpGain + memoryMpGrowth));

        baseHP += finalHpGain;
        baseMP += finalMpGain;

        _lvUpHpGain += finalHpGain;
        _lvUpMpGain += finalMpGain;
    }

    /// <summary>이번 레벨업에서 자동으로 오른 DEF/MAG 1당 MaxHP·MaxMP +3.</summary>
    private void ApplyHpMpBonusFromDefenseMagicGainedThisLevel()
    {
        int bonusHp = _lvUpDefGain * CharacterClass.HpBonusPerDefensePointGained;
        int bonusMp = _lvUpMagGain * CharacterClass.MpBonusPerMagicPointGained;
        if (bonusHp == 0 && bonusMp == 0) return;
        baseHP += bonusHp;
        baseMP += bonusMp;
        _lvUpHpGain += bonusHp;
        _lvUpMpGain += bonusMp;
    }

    /// <summary>
    /// 레벨업 변화량 누적 — 능력치별로 가산.
    /// </summary>
    private void TrackStatGain(StatType statType, int amount)
    {
        switch (statType)
        {
            case StatType.Attack:  _lvUpAtkGain += amount; break;
            case StatType.Defense: _lvUpDefGain += amount; break;
            case StatType.Magic:   _lvUpMagGain += amount; break;
            case StatType.Agility: _lvUpAgiGain += amount; break;
            case StatType.Luck:    _lvUpLukGain += amount; break;
        }
    }

    /// <summary>
    /// 레벨업 시 플레이어가 자유롭게 분배할 수 있는 포인트 1점 지급
    /// </summary>
    private void GrantFreeStatPoint()
    {
        _fallbackFreeStatPoints++;
    }

    /// <summary>
    /// 레벨업 시 스킬 트리에 사용할 스킬 포인트(LP) 1점 지급.
    /// GrantFreeStatPoint와 동일한 패턴 — _fallbackSkillPoints에 직접 +1.
    /// (skillPoints 프로퍼티 setter는 OnStatusChanged를 발동하지만, LevelUp 끝에서 한 번에 발동되므로 직접 증분으로 충분)
    /// </summary>
    private void GrantSkillPoint()
    {
        _fallbackSkillPoints++;
        Debug.Log($"[Learn-DIAG] GrantSkillPoint 호출됨 — '{playerName}' (InstanceID={GetInstanceID()}) skillPoints → {_fallbackSkillPoints}");
    }

    /// <summary>
    /// 외부(UI 등)에서 호출하는 자유 분배용 API
    /// </summary>
    public void AllocateFreePoint(StatType statType)
    {
        if (_fallbackFreeStatPoints <= 0) return;
        _fallbackFreeStatPoints--;

        AddAllocatedStat(statType, 1);
        if (statType == StatType.Defense)
        {
            baseHP += CharacterClass.HpBonusPerDefensePointGained;
            _fallbackCurrentHP = Mathf.Min(_fallbackCurrentHP + CharacterClass.HpBonusPerDefensePointGained, maxHP);
        }
        else if (statType == StatType.Magic)
        {
            baseMP += CharacterClass.MpBonusPerMagicPointGained;
            _fallbackCurrentMP = Mathf.Min(_fallbackCurrentMP + CharacterClass.MpBonusPerMagicPointGained, maxMP);
        }
        OnStatusChanged?.Invoke();
    }

    private void AddAllocatedStat(StatType statType, int amount)
    {
        if (amount == 0) return;

        switch (statType)
        {
            case StatType.Attack:  _fallbackAllocatedAttack  += amount; break;
            case StatType.Defense: _fallbackAllocatedDefense += amount; break;
            case StatType.Magic:   _fallbackAllocatedMagic   += amount; break;
            case StatType.Agility: _fallbackAllocatedAgility += amount; break;
            case StatType.Luck:    _fallbackAllocatedLuck    += amount; break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // [PlusButton API] StatusPanel의 + 버튼이 호출하는 단축 메서드.
    // 자유 분배 포인트(_fallbackFreeStatPoints)가 1 이상일 때만 적용된다.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>HP +1 (baseHP 직접 증가). 자유 포인트 1 소모.</summary>
    public void AddHP()
    {
        if (_fallbackFreeStatPoints <= 0) return;
        _fallbackFreeStatPoints--;
        baseHP += 1;
        _fallbackCurrentHP = Mathf.Min(_fallbackCurrentHP + 1, maxHP);
        OnStatusChanged?.Invoke();
    }

    /// <summary>MP +1 (baseMP 직접 증가). 자유 포인트 1 소모.</summary>
    public void AddMP()
    {
        if (_fallbackFreeStatPoints <= 0) return;
        _fallbackFreeStatPoints--;
        baseMP += 1;
        _fallbackCurrentMP = Mathf.Min(_fallbackCurrentMP + 1, maxMP);
        OnStatusChanged?.Invoke();
    }

    /// <summary>STR(공격력) +1.</summary>
    public void AddSTR() => AllocateFreePoint(StatType.Attack);

    /// <summary>DEF(방어력) +1. CharacterClass 설정에 따라 HP 보너스도 함께 부여될 수 있다.</summary>
    public void AddDEF() => AllocateFreePoint(StatType.Defense);

    /// <summary>MAG(마법력) +1. CharacterClass 설정에 따라 MP 보너스도 함께 부여될 수 있다.</summary>
    public void AddMAG() => AllocateFreePoint(StatType.Magic);

    /// <summary>AGI(민첩성) +1.</summary>
    public void AddAGI() => AllocateFreePoint(StatType.Agility);

    /// <summary>LUK(행운) +1.</summary>
    public void AddLUK() => AllocateFreePoint(StatType.Luck);

    // ─────────────────────────────────────────────────────────────────────
    // [영속화 복원 API] GameManager.ApplyToPlayer 전용.
    // 씬 전환 후 PartyMemberData 스냅샷에서 자유 분배 포인트·분배 누적치·
    // 스킬 포인트(LP)를 한 번에 복원한다. AllocateFreePoint/GrantFreeStatPoint를
    // 거치지 않고 _fallback* 필드에 직접 기록(이벤트도 한 번만 발동).
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// [영속화 복원 API — Base 스탯] GameManager.ApplyToPlayer 전용.
    /// PartyMemberData 스냅샷에서 base 스탯 7종을 _fallbackBase*에 직접 기록.
    /// SO는 건드리지 않음. 시드 플래그를 true로 설정하여 lazy 시드 차단.
    /// </summary>
    public void RestoreBaseStats(
        int bHP, int bMP, int bAttack, int bDefense, int bMagic, int bAgility, int bLuck)
    {
        _fallbackBaseHP      = bHP;
        _fallbackBaseMP      = bMP;
        _fallbackBaseAttack  = bAttack;
        _fallbackBaseDefense = bDefense;
        _fallbackBaseMagic   = bMagic;
        _fallbackBaseAgility = bAgility;
        _fallbackBaseLuck    = bLuck;
        _baseStatsSeeded = true; // 복원 후엔 SO 재시드 차단
        OnStatusChanged?.Invoke();
        Debug.Log($"[PlayerStats] RestoreBaseStats: HP={bHP}, MP={bMP}, ATK={bAttack}, " +
                  $"DEF={bDefense}, MAG={bMagic}, AGI={bAgility}, LUK={bLuck}");
    }

    public void RestoreAllocations(
        int freeStatPts,
        int allocAttack,
        int allocDefense,
        int allocMagic,
        int allocAgility,
        int allocLuck,
        int skillPts)
    {
        _fallbackFreeStatPoints   = Mathf.Max(0, freeStatPts);
        _fallbackAllocatedAttack  = Mathf.Max(0, allocAttack);
        _fallbackAllocatedDefense = Mathf.Max(0, allocDefense);
        _fallbackAllocatedMagic   = Mathf.Max(0, allocMagic);
        _fallbackAllocatedAgility = Mathf.Max(0, allocAgility);
        _fallbackAllocatedLuck    = Mathf.Max(0, allocLuck);
        _fallbackSkillPoints      = Mathf.Max(0, skillPts);
        OnStatusChanged?.Invoke();
        Debug.Log($"[PlayerStats] RestoreAllocations: Free={_fallbackFreeStatPoints}, " +
                  $"AllocATK={_fallbackAllocatedAttack}, AllocDEF={_fallbackAllocatedDefense}, " +
                  $"AllocMAG={_fallbackAllocatedMagic}, AllocAGI={_fallbackAllocatedAgility}, " +
                  $"AllocLUK={_fallbackAllocatedLuck}, LP={_fallbackSkillPoints}");
    }
}

/// <summary>
/// 자유 분배 / 직업 성장에 사용하는 기본 스탯 타입
/// </summary>
public enum StatType
{
    Attack,
    Defense,
    Magic,
    Agility,
    Luck
}




