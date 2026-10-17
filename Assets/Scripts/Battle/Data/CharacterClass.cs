using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 직업(클래스) 데이터 ScriptableObject.
/// 플레이어 Inspector 기본 수치(baseHP/MP, base 공격 등)에 아래 보정값을 <b>정수로 더함</b>.
/// </summary>
[CreateAssetMenu(fileName = "New Character Class", menuName = "Game/Character Class")]
public class CharacterClass : ScriptableObject
{
    [Header("기본 정보")]
    public string className = "Unknown";
    public string description = "";
    public Sprite classIcon;

    // [2026-10-14] 직업 특성 (Class Trait) — 그 직업이면 늘 켜져 있는 고유 효과
    [Header("직업 특성 (Class Trait)")]
    public string traitName = "";
    public string traitNameKR = "";
    [TextArea(2, 4)] public string traitDescription = "";
    [TextArea(2, 4)] public string traitDescriptionKR = "";
    [Tooltip("무기를 들고 하는 물리 공격(기본 공격·물리 스킬) 명중률에 더함. 0.10 = +10%p")]
    [Range(0f, 0.5f)] public float weaponAccuracyBonus = 0f;

    // [2026-10-14] 직업 특성 숫자 (도적 · 몽크)
    [Header("직업 특성 — 도적 · 몽크")]
    [Tooltip("치명타 확률 %p (도적 +5)")]
    public float critChanceBonus = 0f;
    [Tooltip("첫 턴 민첩 배율 보너스 (도적 0.15 = +15%)")]
    public float firstTurnSpeedBonus = 0f;
    [Tooltip("중갑을 입으면 회피 감소 (도적 0.10 = −10%p)")]
    public float heavyArmourEvasionPenalty = 0f;
    [Tooltip("맨손일 때 공격 % (몽크 0.10 = +10%)")]
    public float unarmedDamageBonus = 0f;
    [Tooltip("양손 무기를 들면 명중 감소 (몽크 0.10 = −10%p)")]
    public float twoHandedAccuracyPenalty = 0f;
    [Tooltip("HP 를 쓰는 물리 스킬을 같은 % 의 MP 로 낸다 (몽크)")]
    public bool physicalCostUsesMP = false;

    // [2026-10-14] 새 게임에서 이 직업을 고르면 처음부터 가지고 시작하는 장비 (무기 → 오른손, 갑옷 → 몸통, 장신구 → 장신구1)
    [Header("기초 장비 (Starting Equipment)")]
    public System.Collections.Generic.List<AbyssdawnBattle.EquipmentData> startingEquipment = new System.Collections.Generic.List<AbyssdawnBattle.EquipmentData>();

    // [2026-10-18] 새 게임에서 이 직업으로 시작하면 받는 소모품 (아이템 + 개수)
    [System.Serializable]
    public class StartingItem
    {
        public AbyssdawnBattle.ConsumableItemSO item;
        [Min(1)] public int count = 1;
    }
    [Header("시작 소모품 (Starting Consumables)")]
    public System.Collections.Generic.List<StartingItem> startingConsumables = new System.Collections.Generic.List<StartingItem>();

    // [2026-10-14] 서약 후보 — 새 게임에서 이 중 1개를 고른다 (서약마다 제약이 붙음)
    [Header("서약 (Oath & Binding)")]
    public System.Collections.Generic.List<AbyssdawnBattle.OathBindingData> oaths = new System.Collections.Generic.List<AbyssdawnBattle.OathBindingData>();

    // [2026-10-14] 직업 패스 — LP 로 배우는 직업 전용 스킬 (Class 패널 PATH 탭)
    [Header("직업 패스 (Path)")]
    public System.Collections.Generic.List<AbyssdawnBattle.PathSkillData> pathSkills = new System.Collections.Generic.List<AbyssdawnBattle.PathSkillData>();

    [Header("=== 스탯 배율 (Multiplier, 1.0 = 100%) ===")]
    [Tooltip("HP 배율 - 예: 1.30 = 130% (30% 증가)")]
    [Range(0.1f, 3.0f)]
    public float hpMultiplier = 1.0f;

    [Tooltip("MP 배율")]
    [Range(0.1f, 3.0f)]
    public float mpMultiplier = 1.0f;

    [Tooltip("공격력 배율")]
    [Range(0.1f, 3.0f)]
    public float attackMultiplier = 1.0f;

    [Tooltip("방어력 배율")]
    [Range(0.1f, 3.0f)]
    public float defenseMultiplier = 1.0f;

    [Tooltip("마력 배율")]
    [Range(0.1f, 3.0f)]
    public float magicMultiplier = 1.0f;

    [Tooltip("민첩 배율")]
    [Range(0.1f, 3.0f)]
    public float agilityMultiplier = 1.0f;

    [Tooltip("행운 배율")]
    [Range(0.1f, 3.0f)]
    public float luckMultiplier = 1.0f;

    [Header("스탯 보정 (Hero·PlayerStats 기본 수치에 더함)")]
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int magicBonus = 0;
    public int agilityBonus = 0;
    public int luckBonus = 0;

    [Tooltip("Base HP(플레이어 기본 HP)에 더합니다. 예: Base 20, 여기 10 → 직업 반영 최대 HP는 baseHP+10 (+패시브·장비 등 별도). 레벨업으로 늘어난 baseHP에도 동일하게 더해집니다.")]
    [FormerlySerializedAs("hpFlatBonus")]
    public int hpBonus = 0;

    [Tooltip("Base MP에 더합니다.")]
    [FormerlySerializedAs("mpFlatBonus")]
    public int mpBonus = 0;

    [Header("레벨업 보너스 (선택)")]
    [Tooltip("레벨당 baseHP에 더해지는 증가량(직업). 레벨업 시 baseHP에 가산.")]
    public int hpPerLevel = 0;

    [Tooltip("레벨당 baseMP에 더해지는 증가량(직업).")]
    public int mpPerLevel = 0;

    [Header("레벨업 스탯 성장치 (레거시 가중치 — 미사용 시 0)")]
    [Tooltip("구버전: 가중치 랜덤 1스탯. 신규: levelUp*ChancePercent 사용 시 무시됩니다.")]
    public float attackGrowthPerLevel = 0f;
    public float defenseGrowthPerLevel = 0f;
    public float magicGrowthPerLevel = 0f;
    public float agilityGrowthPerLevel = 0f;
    public float luckGrowthPerLevel = 0f;

    /// <summary>5스탯 레벨업 독립 판정 — 최종 확률 상한/하한(종의 기억 보정 합산 후 클램프).</summary>
    public const float StatLevelUpChanceMinPercent = 5f;
    public const float StatLevelUpChanceMaxPercent = 70f;

    [Header("레벨업 — 5스탯 독립 성공 확률 기본 (%)")]
    [Tooltip("각 스탯마다 별도 주사위. 최종 = 이 값 + 종의 기억 보정 합 → 5~70% 클램프. 전부 0이면 레거시 attackGrowthPerLevel 가중치로 1회 추첨.")]
    [Range(0f, 100f)] public float levelUpAttackChancePercent = 0f;
    [Range(0f, 100f)] public float levelUpDefenseChancePercent = 0f;
    [Range(0f, 100f)] public float levelUpMagicChancePercent = 0f;
    [Range(0f, 100f)] public float levelUpAgilityChancePercent = 0f;
    [Range(0f, 100f)] public float levelUpLuckChancePercent = 0f;

    /// <summary>DEF·MAG 1포인트 상승 시 추가 MaxHP / MaxMP (레벨업 랜덤·자유분배 공통).</summary>
    public const int HpBonusPerDefensePointGained = 3;
    public const int MpBonusPerMagicPointGained = 3;

    public static float ClampStatLevelUpChancePercent(float rawPercent) =>
        Mathf.Clamp(rawPercent, StatLevelUpChanceMinPercent, StatLevelUpChanceMaxPercent);

    /// <summary>신규 독립 확률 테이블을 쓰는지(하나라도 0 초과면 사용).</summary>
    public bool UsesIndependentStatLevelUpChances =>
        levelUpAttackChancePercent > 0.001f ||
        levelUpDefenseChancePercent > 0.001f ||
        levelUpMagicChancePercent > 0.001f ||
        levelUpAgilityChancePercent > 0.001f ||
        levelUpLuckChancePercent > 0.001f;

    public int GetFinalAttack(int baseAttack) => baseAttack + attackBonus;
    public int GetFinalDefense(int baseDefense) => baseDefense + defenseBonus;
    public int GetFinalMagic(int baseMagic) => baseMagic + magicBonus;
    public int GetFinalAgility(int baseAgility) => baseAgility + agilityBonus;
    public int GetFinalLuck(int baseLuck) => baseLuck + luckBonus;

    /// <summary>직업 가산만: runtime baseHP + 직업 hpBonus. 패시브/장비는 PlayerStats에서 추가.</summary>
    public int GetFinalMaxHP(int baseHP) => baseHP + hpBonus;

    /// <summary>runtime baseMP + 직업 mpBonus.</summary>
    public int GetFinalMaxMP(int baseMP) => baseMP + mpBonus;

    public override string ToString()
    {
        return $"[{className}] ATK:{attackBonus:+#;-#;0} DEF:{defenseBonus:+#;-#;0} MAG:{magicBonus:+#;-#;0} " +
               $"AGI:{agilityBonus:+#;-#;0} LUK:{luckBonus:+#;-#;0} HP:{hpBonus:+#;-#;0} MP:{mpBonus:+#;-#;0}";
    }
}
