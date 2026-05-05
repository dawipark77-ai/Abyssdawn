using UnityEngine;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using AbyssdawnBattle;

public class PlayerStats : MonoBehaviour
{
    [Header("1. 저장 금고 (에셋 파일 연결)")]
    [Tooltip("PlayerStatData 에셋을 연결하세요. HP/MP가 실시간으로 이 에셋에 저장됩니다.")]
    public PlayerStatData statData;

    // --- [레벨업/자유 분배용 내부 캐시] ---
    private int _fallbackAllocatedAttack;
    private int _fallbackAllocatedDefense;
    private int _fallbackAllocatedMagic;
    private int _fallbackAllocatedAgility;
    private int _fallbackAllocatedLuck;

    // [FIX] 런타임 fallback 변수 (statData가 없을 때만 사용)
    private int _fallbackCurrentHP;
    private int _fallbackCurrentMP;
    private int _fallbackLevel = 1;
    private int _fallbackExp = 0;
    private bool _isInitialized = false; // 초기화 완료 플래그
    private static bool _isFirstLaunch = true; // 앱 첫 실행 여부

    // [LastStand] 전투당 1회 사망 회피 (Human 종의 특성)
    private bool _lastStandUsed = false;

    // [NEW] 이벤트 시스템 - HP/MP/스탯이 변경될 때마다 발동
    public static event Action OnStatusChanged;

    [Header("2. 캐릭터 정보")]
    public string playerName = "Hero";

    // GameManager 호환용: 현재 직업 에셋의 이름을 반환
    public string jobClass => (characterClass != null) ? characterClass.className : "None";

    [Header("3. 태초의 기본 수치 (고정값)")]
    public int baseHP
    {
        get => statData != null ? statData.baseHP : 20;
        set { if (statData != null) statData.baseHP = value; }
    }
    public int baseMP
    {
        get => statData != null ? statData.baseMP : 0;
        set { if (statData != null) statData.baseMP = value; }
    }
    public int baseAttack
    {
        get => statData != null ? statData.baseAttack : 5;
        set { if (statData != null) statData.baseAttack = value; }
    }
    public int baseDefense
    {
        get => statData != null ? statData.baseDefense : 5;
        set { if (statData != null) statData.baseDefense = value; }
    }
    public int baseMagic
    {
        get => statData != null ? statData.baseMagic : 5;
        set { if (statData != null) statData.baseMagic = value; }
    }
    public int baseAgility
    {
        get => statData != null ? statData.baseAgility : 5;
        set { if (statData != null) statData.baseAgility = value; }
    }
    public int baseLuck
    {
        get => statData != null ? statData.baseLuck : 3;
        set { if (statData != null) statData.baseLuck = value; }
    }

    [Header("4. 장착된 직업 데이터 (런타임 - 읽기 전용)")]
    [Tooltip("이 필드는 Awake()에서 statData.currentJob을 읽어 자동 설정됩니다. 에디터에서 직접 수정하지 마세요!")]
    public CharacterClass characterClass;

    // 자유 분배로 증가한 기본 스탯(직업/장비/패시브 적용 전 순수 플레이어 투자치)
    public int AllocatedAttack   => (statData != null) ? statData.allocatedAttack   : _fallbackAllocatedAttack;
    public int AllocatedDefense  => (statData != null) ? statData.allocatedDefense  : _fallbackAllocatedDefense;
    public int AllocatedMagic    => (statData != null) ? statData.allocatedMagic    : _fallbackAllocatedMagic;
    public int AllocatedAgility  => (statData != null) ? statData.allocatedAgility  : _fallbackAllocatedAgility;
    public int AllocatedLuck     => (statData != null) ? statData.allocatedLuck     : _fallbackAllocatedLuck;

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

            // 3. + 기타
            int passiveBonus = GetPassiveHPBonus();
            int equipmentBonus = GetEquipmentHPBonus();
            int traitBonus = GetTraitBonus(PassiveBonusStat.HP);

            return baseValue + classBonus + passiveBonus + equipmentBonus + traitBonus;
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

            // 3. + 기타
            int passiveBonus = GetPassiveMPBonus();
            int equipmentBonus = GetEquipmentMPBonus(baseValue + classBonus + passiveBonus);
            int traitBonus = GetTraitBonus(PassiveBonusStat.MP);

            return baseValue + classBonus + passiveBonus + equipmentBonus + traitBonus;
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

    // --- [런타임 데이터 프로퍼티 - SO 실시간 저장] ---
    // HP/MP가 변경될 때마다 SO 에셋에 즉시 저장됩니다
    public int currentHP
    {
        get => (statData != null) ? statData.currentHP : _fallbackCurrentHP;
        set
        {
            int oldValue = (statData != null) ? statData.currentHP : _fallbackCurrentHP;
            int newValue = Mathf.Clamp(value, 0, maxHP);

            // [FIX] 실시간 저장: SO 에셋에 즉시 기록
            if (statData != null)
            {
                statData.currentHP = newValue;
#if UNITY_EDITOR
                EditorUtility.SetDirty(statData);
#endif
            }

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
        get => (statData != null) ? statData.currentMP : _fallbackCurrentMP;
        set
        {
            int oldValue = (statData != null) ? statData.currentMP : _fallbackCurrentMP;
            int newValue = Mathf.Clamp(value, 0, maxMP);
            Debug.Log($"[MP_TRACE] MP set to {newValue} by {gameObject.name}");

            if (statData != null)
            {
                statData.currentMP = newValue;
#if UNITY_EDITOR
                EditorUtility.SetDirty(statData);
#endif
            }

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
        get => (statData != null) ? statData.level : _fallbackLevel;
        set
        {
            if (statData != null)
                statData.level = value;
            else
                _fallbackLevel = value;

            // [NEW] 레벨 변경 시 이벤트 발동
            OnStatusChanged?.Invoke();
        }
    }
    public int exp
    {
        get => (statData != null) ? statData.exp : _fallbackExp;
        set
        {
            if (statData != null)
                statData.exp = value;
            else
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

        // 종의 기억 세트 효과 런타임 체크 (OnValidate는 에디터 전용이므로 여기서도 실행)
        statData.CheckAndActivateTrait();

        // [NEW] 씬 간 자동 동기화: HeroData의 currentJob을 읽어 characterClass에 할당
        if (statData.currentJob != null)
        {
            characterClass = statData.currentJob;
            Debug.Log($"[PlayerStats] ✓ HeroData에서 직업 로드: {characterClass.className}");
            Debug.Log($"[PlayerStats]   - Attack: {characterClass.attackBonus:+#;-#;0}, Defense: {characterClass.defenseBonus:+#;-#;0}, Magic: {characterClass.magicBonus:+#;-#;0}");
            Debug.Log($"[PlayerStats]   - HP/MP 직업 가산: HP +{characterClass.hpBonus}, MP +{characterClass.mpBonus}");
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

        // [FIX] 조건부 초기화: 에셋에 저장된 HP가 0이거나 비정상일 때만 maxHP로 초기화
        int currentHPFromAsset = statData.currentHP;
        int currentMPFromAsset = statData.currentMP;

        if (_isFirstLaunch)
        {
            Debug.Log("[PlayerStats] 첫 실행 감지 → HP/MP 풀 초기화");
            currentHP = maxHP;
            currentMP = maxMP;
            _isFirstLaunch = false;
        }
        // HP 조건: 유효 범위면 유지, 그렇지 않으면 새 게임으로 초기화
        else if (currentHPFromAsset > 0 && currentHPFromAsset <= maxHP)
        {
            Debug.Log($"[PlayerStats] SO에서 로드한 HP 유지: {currentHPFromAsset}/{maxHP}");
        }
        else
        {
            Debug.Log($"[PlayerStats] SO의 HP가 비정상({currentHPFromAsset}) → 새 게임으로 간주, maxHP({maxHP})로 초기화");
            currentHP = maxHP;
        }

        // MP 조건: 유효 범위면 유지, 그렇지 않으면 새 게임으로 초기화
        if (currentMPFromAsset >= 0 && currentMPFromAsset <= maxMP)
        {
            Debug.Log($"[PlayerStats] SO에서 로드한 MP 유지: {currentMPFromAsset}/{maxMP}");
        }
        else
        {
            Debug.Log($"[PlayerStats] SO의 MP가 비정상({currentMPFromAsset}) → 새 게임으로 간주, maxMP({maxMP})로 초기화");
            currentMP = maxMP;
        }

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
        exp += amount;
        while (exp >= maxExp) LevelUp();
    }

    private void LevelUp()
    {
        level++;
        exp -= maxExp;
        maxExp = level * 100;
        ApplyClassRandomGrowth();
        ApplyMemoryRandomGrowth();
        ApplyHpMpGrowth();
        GrantFreeStatPoint();
        currentHP = maxHP;
        currentMP = maxMP;
        OnStatusChanged?.Invoke();
    }

    /// <summary>
    /// 레벨업 시 직업 성장치에 따라 기본 스탯이 랜덤 상승하는 로직
    /// </summary>
    private void ApplyClassRandomGrowth()
    {
        if (characterClass == null) return;

        // 성장 가중치 설정 (0 이하인 값은 자동으로 무시)
        float atkW = Mathf.Max(0f, characterClass.attackGrowthPerLevel);
        float defW = Mathf.Max(0f, characterClass.defenseGrowthPerLevel);
        float magW = Mathf.Max(0f, characterClass.magicGrowthPerLevel);
        float agiW = Mathf.Max(0f, characterClass.agilityGrowthPerLevel);
        float lukW = Mathf.Max(0f, characterClass.luckGrowthPerLevel);

        float total = atkW + defW + magW + agiW + lukW;
        if (total <= 0f)
        {
            // 성장치가 모두 0이면 균등 분배
            atkW = defW = magW = agiW = lukW = 1f;
            total = 5f;
        }

        float pick = UnityEngine.Random.Range(0f, total);

        if (pick < atkW)
        {
            AddAllocatedStat(StatType.Attack, 1);
        }
        else if (pick < atkW + defW)
        {
            AddAllocatedStat(StatType.Defense, 1);
        }
        else if (pick < atkW + defW + magW)
        {
            AddAllocatedStat(StatType.Magic, 1);
        }
        else if (pick < atkW + defW + magW + agiW)
        {
            AddAllocatedStat(StatType.Agility, 1);
        }
        else
        {
            AddAllocatedStat(StatType.Luck, 1);
        }
    }

    /// <summary>
    /// 레벨업 시 장착된 종의 기억(MemoryOfSpecies) 성장치에 따라
    /// 추가로 기본 스탯이 랜덤 상승하는 로직
    /// - 직업 성장과는 완전히 별도로 한 번 더 돌립니다.
    /// </summary>
    private void ApplyMemoryRandomGrowth()
    {
        if (statData == null) return;

        var memories = new System.Collections.Generic.List<AbyssdawnBattle.MemoryOfSpeciesData>();
        if (statData.memorySlot1 != null) memories.Add(statData.memorySlot1);
        if (statData.memorySlot2 != null) memories.Add(statData.memorySlot2);
        if (statData.memorySlot3 != null) memories.Add(statData.memorySlot3);

        if (memories.Count == 0) return;

        float atkW = 0f;
        float defW = 0f;
        float magW = 0f;
        float agiW = 0f;
        float lukW = 0f;

        foreach (var mem in memories)
        {
            if (mem == null) continue;
            atkW += Mathf.Max(0f, mem.attackGrowthPerLevel);
            defW += Mathf.Max(0f, mem.defenseGrowthPerLevel);
            magW += Mathf.Max(0f, mem.magicGrowthPerLevel);
            agiW += Mathf.Max(0f, mem.agilityGrowthPerLevel);
            lukW += Mathf.Max(0f, mem.luckGrowthPerLevel);
        }

        float total = atkW + defW + magW + agiW + lukW;
        if (total <= 0f)
        {
            // 성장치가 전부 0이면 종의 기억에서는 레벨업 보정 없음
            return;
        }

        float pick = UnityEngine.Random.Range(0f, total);

        if (pick < atkW)
        {
            AddAllocatedStat(StatType.Attack, 1);
        }
        else if (pick < atkW + defW)
        {
            AddAllocatedStat(StatType.Defense, 1);
        }
        else if (pick < atkW + defW + magW)
        {
            AddAllocatedStat(StatType.Magic, 1);
        }
        else if (pick < atkW + defW + magW + agiW)
        {
            AddAllocatedStat(StatType.Agility, 1);
        }
        else
        {
            AddAllocatedStat(StatType.Luck, 1);
        }
    }

    /// <summary>
    /// 레벨업 시 HP/MP는 항상 상승하도록 처리.
    /// - 직업의 hpPerLevel/mpPerLevel을 기본으로 사용하고
    /// - 장착된 종의 기억의 hpGrowthPerLevel/mpGrowthPerLevel을 추가로 더해
    ///   약간의 랜덤 오차를 준 뒤 baseHP/baseMP를 증가시킵니다.
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
            if (statData.memorySlot1 != null) memories.Add(statData.memorySlot1);
            if (statData.memorySlot2 != null) memories.Add(statData.memorySlot2);
            if (statData.memorySlot3 != null) memories.Add(statData.memorySlot3);

            foreach (var mem in memories)
            {
                if (mem == null) continue;
                memoryHpGrowth += Mathf.Max(0f, mem.hpGrowthPerLevel);
                memoryMpGrowth += Mathf.Max(0f, mem.mpGrowthPerLevel);
            }
        }

        float expectedHpGain = classHpGain + memoryHpGrowth;
        float expectedMpGain = classMpGain + memoryMpGrowth;

        // 약간의 오차(-1 ~ +1)를 더하되, 최소 1 이상은 항상 오른다.
        int finalHpGain = Mathf.Max(1, Mathf.RoundToInt(expectedHpGain + UnityEngine.Random.Range(-1f, 1f)));
        int finalMpGain = Mathf.Max(1, Mathf.RoundToInt(expectedMpGain + UnityEngine.Random.Range(-1f, 1f)));

        baseHP += finalHpGain;
        baseMP += finalMpGain;
    }

    /// <summary>
    /// 레벨업 시 플레이어가 자유롭게 분배할 수 있는 포인트 1점 지급
    /// </summary>
    private void GrantFreeStatPoint()
    {
        if (statData != null)
        {
            statData.freeStatPoints++;
        }
    }

    /// <summary>
    /// 외부(UI 등)에서 호출하는 자유 분배용 API
    /// </summary>
    public void AllocateFreePoint(StatType statType)
    {
        if (statData != null)
        {
            if (statData.freeStatPoints <= 0) return;
            statData.freeStatPoints--;
        }

        AddAllocatedStat(statType, 1);
        OnStatusChanged?.Invoke();
    }

    private void AddAllocatedStat(StatType statType, int amount)
    {
        if (amount == 0) return;

        switch (statType)
        {
            case StatType.Attack:
                if (statData != null) statData.allocatedAttack += amount;
                else _fallbackAllocatedAttack += amount;
                break;
            case StatType.Defense:
                if (statData != null) statData.allocatedDefense += amount;
                else _fallbackAllocatedDefense += amount;
                break;
            case StatType.Magic:
                if (statData != null) statData.allocatedMagic += amount;
                else _fallbackAllocatedMagic += amount;
                break;
            case StatType.Agility:
                if (statData != null) statData.allocatedAgility += amount;
                else _fallbackAllocatedAgility += amount;
                break;
            case StatType.Luck:
                if (statData != null) statData.allocatedLuck += amount;
                else _fallbackAllocatedLuck += amount;
                break;
        }
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




