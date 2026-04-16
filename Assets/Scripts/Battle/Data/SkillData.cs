using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    public enum BackflowType
    {
        None,
        HPLoss,
        MPLoss,
        StatusEffect,
        Stun,
        HPAndMP
    }

    /// <summary>
    /// 타겟 종류 (적, 아군, 자신, 모두)
    /// </summary>
    public enum TargetFaction
    {
        Enemy,      // 적
        Ally,       // 아군
        Self,       // 자신
        All         // 모두 (적+아군)
    }

    /// <summary>
    /// 타겟팅 모드 (단일, 다중, 전열, 후열, 전체 등)
    /// </summary>
    public enum TargetMode
    {
        Single,         // 단일 타겟 (슬롯 1개 선택)
        Multiple,       // 다중 타겟 (특정 슬롯들 지정)
        FrontRow,       // 전열 전체 (슬롯 1, 2)
        BackRow,        // 후열 전체 (슬롯 3, 4)
        All             // 전체 (모든 슬롯)
    }

    /// <summary>
    /// 전열/후열 타입 (슬롯 인덱스로부터 계산됨)
    /// </summary>
    public enum RowType
    {
        Front,  // 전열 (슬롯 1, 2)
        Back    // 후열 (슬롯 3, 4)
    }

    /// <summary>
    /// 슬롯 번호 (1-4, 인덱스 기반)
    /// </summary>
    public enum BattleSlot
    {
        None = 0,
        Slot1 = 1,
        Slot2 = 2,
        Slot3 = 3,
        Slot4 = 4
    }

    /// <summary>
    /// 슬롯 마스크 (Flags enum) - 스킬 조건 표현용
    /// 예: SlotMask.Front | SlotMask.Slot3 = 전열 + 3번 슬롯
    /// </summary>
    [System.Flags]
    public enum SlotMask
    {
        None = 0,
        Slot1 = 1 << 0,      // 1
        Slot2 = 1 << 1,      // 2
        Slot3 = 1 << 2,      // 4
        Slot4 = 1 << 3,      // 8
        Front = Slot1 | Slot2,   // 전열 (1, 2)
        Back = Slot3 | Slot4,    // 후열 (3, 4)
        Any = Front | Back      // 전체 (1, 2, 3, 4)
    }

    /// <summary>
    /// 슬롯/전열 관련 유틸리티 헬퍼
    /// </summary>
    public static class SlotHelper
    {
        /// <summary>
        /// 슬롯 인덱스(1-4)로부터 전열/후열 타입 반환
        /// </summary>
        public static RowType GetRow(int slotIndex)
        {
            return slotIndex <= 2 ? RowType.Front : RowType.Back;
        }

        /// <summary>
        /// BattleSlot enum으로부터 전열/후열 타입 반환
        /// </summary>
        public static RowType GetRow(BattleSlot slot)
        {
            int index = (int)slot;
            return index <= 2 ? RowType.Front : RowType.Back;
        }

        /// <summary>
        /// 슬롯 인덱스(1-4)가 전열인지 확인
        /// </summary>
        public static bool IsFrontRow(int slotIndex)
        {
            return slotIndex <= 2;
        }

        /// <summary>
        /// BattleSlot이 전열인지 확인
        /// </summary>
        public static bool IsFrontRow(BattleSlot slot)
        {
            return slot == BattleSlot.Slot1 || slot == BattleSlot.Slot2;
        }

        /// <summary>
        /// 슬롯 인덱스(1-4)가 후열인지 확인
        /// </summary>
        public static bool IsBackRow(int slotIndex)
        {
            return slotIndex > 2;
        }

        /// <summary>
        /// BattleSlot이 후열인지 확인
        /// </summary>
        public static bool IsBackRow(BattleSlot slot)
        {
            return slot == BattleSlot.Slot3 || slot == BattleSlot.Slot4;
        }

        /// <summary>
        /// BattleSlot을 SlotMask로 변환
        /// </summary>
        public static SlotMask ToSlotMask(BattleSlot slot)
        {
            return (SlotMask)(1 << ((int)slot - 1));
        }

        /// <summary>
        /// BattleSlot 리스트를 SlotMask로 변환
        /// </summary>
        public static SlotMask ToSlotMask(List<BattleSlot> slots)
        {
            SlotMask mask = SlotMask.None;
            foreach (var slot in slots)
            {
                if (slot != BattleSlot.None)
                    mask |= ToSlotMask(slot);
            }
            return mask;
        }

        /// <summary>
        /// SlotMask에 해당 슬롯이 포함되어 있는지 확인
        /// </summary>
        public static bool ContainsSlot(SlotMask mask, BattleSlot slot)
        {
            SlotMask slotMask = ToSlotMask(slot);
            return (mask & slotMask) != 0;
        }
    }

    /// <summary>
    /// 스킬 타겟팅 설정 (SlotMask 기반)
    /// </summary>
    [System.Serializable]
    public class SkillTargeting
    {
        [Header("타겟 종류")]
        [Tooltip("적, 아군, 자신, 모두 중 선택")]
        public TargetFaction targetFaction = TargetFaction.Enemy;

        [Header("스킬 사용 가능 위치 (SlotMask)")]
        [Tooltip("이 스킬을 사용할 수 있는 슬롯 위치\n" +
                 "예: Front = 전열(1,2)에서만 사용 가능\n" +
                 "예: Slot1 = 1번 슬롯에서만 사용 가능\n" +
                 "예: Any = 어디서든 사용 가능")]
        public SlotMask allowedCasterSlots = SlotMask.Any;

        [Header("타겟 슬롯 (SlotMask)")]
        [Tooltip("타겟팅할 슬롯 위치\n" +
                 "예: Front = 전열(1,2) 타겟\n" +
                 "예: Slot1 | Slot2 = 1번과 2번 슬롯 타겟\n" +
                 "예: Slot3 = 3번 슬롯만 타겟\n" +
                 "예: Any = 전체 타겟")]
        public SlotMask allowedTargetSlots = SlotMask.Slot1;

        [Header("레거시 호환 (Multiple 모드용)")]
        [Tooltip("레거시 호환을 위한 슬롯 리스트 (자동 변환됨)")]
        [System.Obsolete("Use allowedTargetSlots instead")]
        public List<BattleSlot> specificSlots = new List<BattleSlot>();

        /// <summary>
        /// 실제 타겟팅할 슬롯 리스트를 반환 (SlotMask 기반)
        /// </summary>
        public List<BattleSlot> GetTargetSlots()
        {
            List<BattleSlot> slots = new List<BattleSlot>();

            if ((allowedTargetSlots & SlotMask.Slot1) != 0)
                slots.Add(BattleSlot.Slot1);
            if ((allowedTargetSlots & SlotMask.Slot2) != 0)
                slots.Add(BattleSlot.Slot2);
            if ((allowedTargetSlots & SlotMask.Slot3) != 0)
                slots.Add(BattleSlot.Slot3);
            if ((allowedTargetSlots & SlotMask.Slot4) != 0)
                slots.Add(BattleSlot.Slot4);

            return slots;
        }

        /// <summary>
        /// 특정 슬롯에서 이 스킬을 사용할 수 있는지 확인
        /// </summary>
        public bool CanCastFrom(BattleSlot casterSlot)
        {
            SlotMask casterMask = SlotHelper.ToSlotMask(casterSlot);
            return (allowedCasterSlots & casterMask) != 0;
        }

        /// <summary>
        /// 특정 슬롯이 타겟 범위에 포함되는지 확인
        /// </summary>
        public bool CanTarget(BattleSlot targetSlot)
        {
            SlotMask targetMask = SlotHelper.ToSlotMask(targetSlot);
            return (allowedTargetSlots & targetMask) != 0;
        }
    }

    [System.Serializable]
    public class SkillEffect
    {
        public EffectType effectType = EffectType.None;
        public RecoveryTarget recoveryTarget = RecoveryTarget.HP;
        public float effectAmount = 0f;
        [Tooltip("대상에게 걸리는 상태이상 데이터")]
        public StatusEffectSO statusEffect;
        [Range(0f, 100f)]
        public float statusEffectChance = 0f;
    }

    [CreateAssetMenu(fileName = "Skill_", menuName = "Abyssdawn/Skill Data", order = 1)]
    public class SkillData : ScriptableObject
    {
        [Header("Basic Info")]
        public string skillID;
        public string skillName;
        public Sprite skillIcon;
        [TextArea(2, 4)]
        public string description;

        [Header("Type")]
        public UsageType usageType = UsageType.Active;
        public DamageType damageType = DamageType.Physical;
        [FormerlySerializedAs("scaleStat")]
        public ScaleStat scalingStat = ScaleStat.Attack;

        [Tooltip("Required weapon category to use this skill.\nNone = works with any weapon (common/universal skills).\nSword/Dagger/Bow etc. = only usable when that weapon type is equipped.")]
        public WeaponCategory weaponCategory = WeaponCategory.None;

        [Header("Targeting")]
        [Tooltip("스킬의 타겟팅 설정\n" +
                 "타겟 종류: 적/아군/자신/모두\n" +
                 "타겟팅 모드: 단일/다중/전열/후열/전체\n" +
                 "예: Mandritto = 적, 전열 (Slot1, Slot2)\n" +
                 "예: Heal = 아군, 단일\n" +
                 "예: AoE = 적, 전체")]
        public SkillTargeting targeting = new SkillTargeting();

        [Header("Cost")]
        [Range(0f, 100f)]
        public float hpCostPercent = 0f;
        public int mpCost = 0;

        [Header("Power")]
        public float minMult = 1.0f;
        public float maxMult = 1.0f;
        public int hitCount = 1;
        [Range(0f, 1f)]
        [Tooltip("기본 명중률 (0.0 ~ 1.0)")]
        public float accuracy = 0.95f;

        [Header("Risk")]
        [Range(0f, 100f)]
        public float selfDmgPercent = 0f;
        [Range(0f, 100f)]
        public float selfDmgChance = 0f;
        [Tooltip("자신에게 걸리는 상태이상 데이터 (역효과)")]
        [FormerlySerializedAs("selfCurseData")]
        public StatusEffectSO selfStatusEffect;
        [Range(0f, 100f)]
        public float selfStatusEffectChance = 0f;

        [Header("Effects")]
        public List<SkillEffect> effects = new List<SkillEffect>();

        [HideInInspector, SerializeField]
        private float legacyCurseChance = 0f;
        [FormerlySerializedAs("effectType"), HideInInspector, SerializeField]
        private EffectType legacyEffectType = EffectType.None;
        [FormerlySerializedAs("recoveryTarget"), HideInInspector, SerializeField]
        private RecoveryTarget legacyRecoveryTarget = RecoveryTarget.HP;
        [FormerlySerializedAs("effectAmount"), HideInInspector, SerializeField]
        private float legacyEffectAmount = 0f;

        [Header("Passive")]
        public TriggerCondition triggerCondition = TriggerCondition.None;

        [Header("Dunbreak (던브레이크)")]
        [Tooltip("던브레이크 발동 확률 (0.0 ~ 100.0%)")]
        [Range(0f, 100f)]
        public float dunbreakChance = 0f;
        
        [Tooltip("던브레이크 효과 설명 (게임 내 표시용)")]
        [TextArea(1, 3)]
        public string dunbreakDescription = "";

        [Header("━━━━━━━━━━ 상태이상 (저주) ━━━━━━━━━━")]
        [FormerlySerializedAs("curseData")]
        public StatusEffectSO curseEffect;
        [Range(0f, 1f)]
        public float curseApplyChance;

        [Header("━━━━━━━━━━ 역류 (Backflow) ━━━━━━━━━━")]
        [Range(0f, 1f)]
        public float backflowChance;
        public BackflowType backflowType;
        public StatusEffectSO backflowStatusEffect;

        [Tooltip("패시브 스킬 전용: 역류 억제율 (0.0 ~ 1.0). 장착 시 시전자의 역류 확률을 이 값만큼 감소.")]
        [Range(0f, 1f)]
        public float backflowSuppression;

        [Header("━━━━━━━━━━ 패시브 랭크 ━━━━━━━━━━")]
        public int maxRank;
        public float[] bonusPerRank;
        public float[] backflowPerRank;

        [Header("━━━━━━━━━━ 특수 조건 ━━━━━━━━━━")]
        public bool requireTargetIgnited;
        public bool selfIgnitedBonus;

        [Header("━━━━━━━━━━ 확장 전투 필드 ━━━━━━━━━━")]
        [Tooltip("스킬 고유 방어구 파괴 비율 (예: 8 = 적 현재 방어력의 8%)")]
        [Range(0f, 100f)]
        public float armorBreakPercent = 0f;

        [Tooltip("직전에 사용되어야 하는 스킬 ID")]
        public string requiresPreviousSkillId = "";

        [Tooltip("직전 스킬과 같은 대상을 요구하는지 여부")]
        public bool requiresSameTarget = false;

        [Tooltip("선제 행동 여부")]
        public bool preemptive = false;

        [Tooltip("사용 후 이번 턴 추가 행동 없이 종료하는지 여부")]
        public bool endsTurnImmediately = false;

        [Tooltip("추가 크리티컬 확률 보너스 (예: 5 = +5%)")]
        [Range(0f, 100f)]
        public float critBonusPercent = 0f;

        [Tooltip("파티 전체 물리 공격력 버프 비율 (예: 5 = +5%)")]
        [Range(0f, 100f)]
        public float partyPhysicalBuffPercent = 0f;

        [Tooltip("파티 전체 물리 공격력 버프 지속 턴")]
        public int partyPhysicalBuffTurns = 0;

        [Tooltip("자신에게 부여되는 민첩 패널티 값")]
        public float selfAgilityPenalty = 0f;

        [Tooltip("자신 민첩 패널티 지속 턴")]
        public int selfAgilityPenaltyTurns = 0;

        [Tooltip("방어력 감소 구간별 출혈 확률 보너스 배열. 20/40/60/80/100% 순서로 입력.")]
        public float[] bleedChanceByArmorBroken = new float[0];

        [Header("Skill Tree")]
        [Tooltip("이 스킬을 배우기 위해 필요한 선행 스킬들")]
        public List<SkillData> prerequisiteSkills = new List<SkillData>();

        [Tooltip("이 스킬을 배우는 데 필요한 LP(Lore Point)")]
        public int requiredLorePoints = 1;

        // Legacy compatibility properties (for BattleManager refactoring transition)
        public string skillType => damageType.ToString();
        public float minMultiplier => minMult;
        public float maxMultiplier => maxMult;
        public string scalingStatName => scalingStat.ToString();
        public bool isRecovery => HasEffectType(EffectType.Recovery);
        public bool isDefensive => HasEffectType(EffectType.BuffDefense);
        public float effectValue => effectAmount;
        public float selfDamagePercent => selfDmgPercent;
        public float selfDamageChance => selfDmgChance;
        public Sprite icon => skillIcon;

        // Helper properties
        public bool IsActive => usageType == UsageType.Active;
        public bool IsPassive => usageType == UsageType.Passive;
        public bool HasCost => hpCostPercent > 0 || mpCost > 0;
        public bool IsDamaging => HasEffectType(EffectType.Damage) || minMult > 0;

        public IReadOnlyList<SkillEffect> Effects => effects;
        public SkillEffect PrimaryEffect => (effects != null && effects.Count > 0) ? effects[0] : null;

        public EffectType effectType
        {
            get => PrimaryEffect != null ? PrimaryEffect.effectType : EffectType.None;
            set
            {
                EnsureEffectsSlot();
                effects[0].effectType = value;
            }
        }

        public RecoveryTarget recoveryTarget
        {
            get => PrimaryEffect != null ? PrimaryEffect.recoveryTarget : RecoveryTarget.HP;
            set
            {
                EnsureEffectsSlot();
                effects[0].recoveryTarget = value;
            }
        }

        public float effectAmount
        {
            get => PrimaryEffect != null ? PrimaryEffect.effectAmount : 0f;
            set
            {
                EnsureEffectsSlot();
                effects[0].effectAmount = value;
            }
        }

        public StatusEffectSO statusEffect
        {
            get => PrimaryEffect != null ? PrimaryEffect.statusEffect : null;
            set
            {
                EnsureEffectsSlot();
                effects[0].statusEffect = value;
            }
        }

        public float statusEffectChance
        {
            get => PrimaryEffect != null ? PrimaryEffect.statusEffectChance : 0f;
            set
            {
                EnsureEffectsSlot();
                effects[0].statusEffectChance = value;
            }
        }

        private void OnValidate()
        {
            MigrateLegacyEffects();
        }

        private void EnsureEffectsSlot()
        {
            if (effects == null) effects = new List<SkillEffect>();
            if (effects.Count == 0) effects.Add(new SkillEffect());
        }

        private bool HasEffectType(EffectType type)
        {
            if (effects == null) return false;
            foreach (var effect in effects)
            {
                if (effect != null && effect.effectType == type) return true;
            }
            return false;
        }

        private void MigrateLegacyEffects()
        {
            if (effects == null) effects = new List<SkillEffect>();
            if (effects.Count > 0) return;

            bool hasLegacyData =
                legacyEffectType != EffectType.None ||
                legacyEffectAmount > 0f ||
                legacyCurseChance > 0f;

            if (!hasLegacyData) return;

            effects.Add(new SkillEffect
            {
                effectType = legacyEffectType,
                recoveryTarget = legacyRecoveryTarget,
                effectAmount = legacyEffectAmount,
                statusEffectChance = legacyCurseChance
            });
        }
    }
}
