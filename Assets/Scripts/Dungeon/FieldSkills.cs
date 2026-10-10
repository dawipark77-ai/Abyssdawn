using UnityEngine;

/// <summary>
/// 탐험 스킬 (UTILITY → Exploration 트리). SkillData.fieldSkill = true — 전투 스킬 칸을 쓰지 않고 배우기만 하면 항상 적용.
/// 효과는 이름으로 코드에 연결되어 있다 (에셋의 effects 목록은 비어 있음):
///  - Trap Detection      : 함정 자동 감지 확률 +15%p (누구나 가진 감지 — MapManager Trap Sense), 서치 반경 +1, 숨은 함정을 밟기 직전 50% 멈춤
///  - Trap Disarm         : 알려진 함정에 들어서려 하면 해제 시도 (55% + 민첩 0.5%/1, 최대 95%). 성공 = 함정 부품, 실패 = 발동(피해 절반)
///  - Survivalist         : 빛 지속 +50%, 회복 아이템 효과 +20%, 서치할 때 10% 확률로 약초
///  - Danger Sense        : 위험도 % 표시, 습격 직전 경고 → 그 전투 첫 턴 회피 +20%
///  - Resource Management : 상점 사는 값 -10%, 파는 값 +10%, 전투 골드 +15%
/// </summary>
public static class FieldSkills
{
    public const string TrapDetection = "Trap Detection";
    public const string TrapDisarm = "Trap Disarm";
    public const string Survivalist = "Survivalist";
    public const string DangerSense = "Danger Sense";
    public const string ResourceManagement = "Resource Management";
    // [2026-10-11] 원정술 = 야전술(Fieldcraft) + 보급술(Quartermastery). 모두 상시 스킬
    public const string SecureRetreat = "Secure Retreat";
    public const string AmplePack = "Ample Pack";
    public const string LootSalvage = "Loot Salvage";
    public const string FrugalUse = "Frugal Use";
    public const string RegularCustomer = "Regular Customer";

    // 수치 (한 곳에서 조정)
    public const float TrapDetectPassiveChance = 0.15f;   // [2026-10-09] 함정 자동 감지 확률 +25%p → +15%p
    public const int TrapDetectSearchBonus = 1;            // 서치 반경 +1
    public const float TrapDetectStopChance = 0.5f;       // 숨은 함정 직전 멈춤
    public const float DisarmBaseChance = 0.55f;
    public const float DisarmNoSkillChance = 0.30f;      // [2026-10-08] 스킬 없이 함정을 눌러 해제할 때
    public const float DisarmPerAgi = 0.005f;
    public const float DisarmMaxChance = 0.95f;
    public const float DisarmFailDamageMult = 0.5f;
    public const float SurvivalistLightMult = 1.5f;      // [2026-10-11] 보급술 문서에 없어 사용하지 않음 (빛 보너스 제거)
    public const float SurvivalistHerbChance = 0.05f;     // [2026-10-11] 10% → 5% (등급 무관)
    /// <summary>[2026-10-11] 생존 전문가 1~5등급: 회복 아이템 효과 +5/9/13/16/20%.</summary>
    public static readonly float[] SurvivalistPotency = { 0.05f, 0.09f, 0.13f, 0.16f, 0.20f };
    public const int AmplePackBonus = 1;                  // 넉넉한 배낭: 소비 아이템 최대 보유 +1 (새벽의 잔 제외)
    public const float LootChestExtraChance = 0.25f;      // 전리품 수습: 상자에서 소비 아이템 1개 더
    public const float LootVictoryPotionChance = 0.05f;   // 전리품 수습: 전투 승리 시 HP 포션
    public const float FrugalChance = 0.15f;              // 아껴 쓰기: 회복·해독 아이템이 소모되지 않을 확률
    /// <summary>단골 손님 1~5등급: 여관 할인율.</summary>
    public static readonly float[] RegularInnDiscount = { 0.10f, 0.15f, 0.25f, 0.35f, 0.50f };
    /// <summary>단골 손님 1~5등급: 여관에서 쉬고 나가면 다음 층 첫 3번의 전투 동안 최대 HP·MP +%.</summary>
    public static readonly float[] RegularInnVigor = { 0.03f, 0.05f, 0.06f, 0.08f, 0.10f };
    public const int RegularInnVigorBattles = 3;
    public const float SecureRetreatFleeBonus = 0.15f;    // 퇴로 확보: 계단을 찾은 층에서 도주 첫 시도 +15%p
    public const float SecureRetreatFailDamageMult = 0.7f; // 퇴로 확보: 도주에 실패한 라운드에 받는 피해 -30%
    public const float DangerSenseEvasion = 0.20f;
    public const float ResourceGoldMult = 1.15f;

    /// <summary>주인공(동료 제외)의 PlayerStatData.</summary>
    public static PlayerStatData HeroData()
    {
        foreach (var p in Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            if (p != null && !p.IsRecruitedCompanion && p.statData != null) return p.statData;
        return null;
    }

    public static bool Has(string skillName)
    {
        var d = HeroData();
        return d != null && d.HasFieldSkill(skillName);
    }

    /// <summary>[2026-10-09] 탐험 스킬 등급 (안 배움 0).</summary>
    public static int Rank(string skillName)
    {
        var d = HeroData();
        return d != null ? d.FieldSkillRank(skillName) : 0;
    }

    // ─────────────────────────────────────────
    // [2026-10-09] 야영 (Make Camp) — 던전 지도에서 쓰는 액티브, 1~5등급 (같은 칸에서 다시 찍음). 선행: 생존 전문가
    //   층마다 1번 · 마을 층(B1·B5) 불가. 쉬면 HP·MP 회복 + 등급에 따라 던전 상태이상 해제 · 빛.
    //   위험: 그때의 위험도만큼 습격 확률(최대 40%) → 습격당하면 적 선공. 대가: 야영 후 위험도 +30%.
    //   Lv1 30% · 해제 없음
    //   Lv2 40% · 출혈
    //   Lv3 50% · 출혈·독 · 빛 +30걸음
    //   Lv4 60% · 출혈·독·화상·실명 · 빛 +30걸음 · 습격 확률 ×0.75
    //   Lv5 70% · 모든 던전 상태이상 · 빛 +30걸음 · 습격 확률 ×0.5 · 위험도 +15%만
    // ─────────────────────────────────────────
    public const string MakeCamp = "Make Camp";
    public const int MakeCampMaxRank = 5;
    public static readonly float[] CampRestore = { 0.30f, 0.40f, 0.50f, 0.60f, 0.70f };
    public static readonly float[] CampAmbushMult = { 1f, 1f, 1f, 0.75f, 0.5f };
    public static readonly float[] CampDangerAfter = { 0.30f, 0.30f, 0.30f, 0.30f, 0.15f };
    public const float CampAmbushMax = 0.40f;
    public const int CampLightSteps = 30;          // Lv3 이상
    public const int CampLightFromRank = 3;

    /// <summary>이 등급에서 야영으로 풀리는 던전 상태이상.</summary>
    public static AbyssdawnBattle.StatusEffectType[] CampCures(int rank)
    {
        switch (rank)
        {
            case 1: return new AbyssdawnBattle.StatusEffectType[0];
            case 2: return new[] { AbyssdawnBattle.StatusEffectType.Bleed };
            case 3: return new[] { AbyssdawnBattle.StatusEffectType.Bleed, AbyssdawnBattle.StatusEffectType.Poison };
            case 4: return new[] { AbyssdawnBattle.StatusEffectType.Bleed, AbyssdawnBattle.StatusEffectType.Poison, AbyssdawnBattle.StatusEffectType.Ignite, AbyssdawnBattle.StatusEffectType.Blind };
            default: return new[] { AbyssdawnBattle.StatusEffectType.Bleed, AbyssdawnBattle.StatusEffectType.Poison, AbyssdawnBattle.StatusEffectType.Ignite, AbyssdawnBattle.StatusEffectType.Blind, AbyssdawnBattle.StatusEffectType.Stun };
        }
    }
}
