using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    [System.Serializable]
    public class SkillEffect
    {
        public EffectType effectType = EffectType.None;
        public RecoveryTarget recoveryTarget = RecoveryTarget.HP;
        public float effectAmount = 0f;
        [Tooltip("대상에게 걸리는 저주 데이터")]
        public CurseData curseData;
        [Range(0f, 100f)]
        public float curseChance = 0f;
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
        [Tooltip("자신에게 걸리는 저주 데이터")]
        public CurseData selfCurseData;
        [Range(0f, 100f)]
        public float selfCurseChance = 0f;

        [Header("Effects")]
        public List<SkillEffect> effects = new List<SkillEffect>();

        [FormerlySerializedAs("curseData"), HideInInspector, SerializeField]
        private CurseData legacyCurseData;
        [FormerlySerializedAs("curseChance"), HideInInspector, SerializeField]
        private float legacyCurseChance = 0f;
        [FormerlySerializedAs("effectType"), HideInInspector, SerializeField]
        private EffectType legacyEffectType = EffectType.None;
        [FormerlySerializedAs("recoveryTarget"), HideInInspector, SerializeField]
        private RecoveryTarget legacyRecoveryTarget = RecoveryTarget.HP;
        [FormerlySerializedAs("effectAmount"), HideInInspector, SerializeField]
        private float legacyEffectAmount = 0f;

        [Header("Passive")]
        public TriggerCondition triggerCondition = TriggerCondition.None;

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

        public CurseData curseData
        {
            get => PrimaryEffect != null ? PrimaryEffect.curseData : null;
            set
            {
                EnsureEffectsSlot();
                effects[0].curseData = value;
            }
        }

        public float curseChance
        {
            get => PrimaryEffect != null ? PrimaryEffect.curseChance : 0f;
            set
            {
                EnsureEffectsSlot();
                effects[0].curseChance = value;
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
                legacyCurseData != null ||
                legacyCurseChance > 0f;

            if (!hasLegacyData) return;

            effects.Add(new SkillEffect
            {
                effectType = legacyEffectType,
                recoveryTarget = legacyRecoveryTarget,
                effectAmount = legacyEffectAmount,
                curseData = legacyCurseData,
                curseChance = legacyCurseChance
            });
        }
    }
}
