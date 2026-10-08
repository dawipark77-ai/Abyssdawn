using UnityEngine;

/// <summary>
/// 탐험 스킬 (UTILITY → Exploration 트리). SkillData.fieldSkill = true — 전투 스킬 칸을 쓰지 않고 배우기만 하면 항상 적용.
/// 효과는 이름으로 코드에 연결되어 있다 (에셋의 effects 목록은 비어 있음):
///  - Trap Detection      : 함정 자동 감지 확률 +25%p (누구나 가진 감지 — MapManager Trap Sense), 서치 반경 +1, 숨은 함정을 밟기 직전 50% 멈춤
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

    // 수치 (한 곳에서 조정)
    public const float TrapDetectPassiveChance = 0.25f;   // [2026-10-08] 함정 자동 감지(1칸 거리) 확률 +25%p
    public const int TrapDetectSearchBonus = 1;            // 서치 반경 +1
    public const float TrapDetectStopChance = 0.5f;       // 숨은 함정 직전 멈춤
    public const float DisarmBaseChance = 0.55f;
    public const float DisarmNoSkillChance = 0.30f;      // [2026-10-08] 스킬 없이 함정을 눌러 해제할 때
    public const float DisarmPerAgi = 0.005f;
    public const float DisarmMaxChance = 0.95f;
    public const float DisarmFailDamageMult = 0.5f;
    public const float SurvivalistLightMult = 1.5f;
    public const float SurvivalistHerbChance = 0.10f;
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
}
