using UnityEngine;

/// <summary>
/// [2026-10-14] 성향(성격) 효과 모음 — 새 게임에서 고른 성향 1개가 탐험·전투에 주는 보정.
/// 설계 원칙: 완벽한 성격은 없다 — 성향마다 얻는 것과 잃는 것이 짝 (마스터 설계 문서 11. 성격 시스템).
/// 지금 성향은 SaveSystem.Disposition (DispositionData 에셋 이름 "Disposition_XXX") 에 저장된다.
/// 수치는 초안 — 여기 상수만 바꾸면 된다. 효과는 주인공에게만 (동료 몬스터 제외).
///
///  1 탐험가 Explorer        : 시야 +1칸, 탐색 범위 +1칸            / 조우 +15%
///  2 호기심 Curious         : 상자 좋은 물건(지식의 서·장비) +20%    / 함정 피해 +30%
///  3 구두쇠 Miser           : 상점 구매가 −15%                     / 회복 아이템 효과 −20%
///  4 탐욕 Greedy            : 몬스터 골드 +25%                     / 위험도(ENTROPY) 상승 +20%
///  5 완벽주의자 Perfectionist: 명중 +5%, 회피 +5%                  / 빗나가면 다음 턴 공격 −10%, 함정에 걸리면 그 층 동안 명중 −10%
///  6 겁쟁이 Coward          : 도망 성공률 +25%                     / 전투 첫 턴 공격 −15%
///  7 무모함 Reckless        : 치명타율 +8%                         / 받는 피해 +10%
///  8 신중함 Cautious        : 함정 감지 +25%, 함정 피해 −50%        / 전투 첫 턴 민첩 −20%
///  9 냉혈 ColdBlooded       : 적을 쓰러뜨리면 다음 공격 +10%         / 동료 영입 확률 −50%
/// 10 고독한 늑대 LoneWolf    : 동료 없이 싸우면 모든 능력 +10%        / 동료가 있으면 받는 회복 −15%
/// </summary>
public static class Disposition
{
    public const string Explorer = "Explorer", Curious = "Curious", Miser = "Miser", Greedy = "Greedy", Perfectionist = "Perfectionist",
                        Coward = "Coward", Reckless = "Reckless", Cautious = "Cautious", ColdBlooded = "ColdBlooded", LoneWolf = "LoneWolf";

    /// <summary>지금 성향 ID (없으면 null).</summary>
    public static string Current
    {
        get
        {
            string n = SaveSystem.Disposition;
            if (string.IsNullOrEmpty(n)) return null;
            return n.StartsWith("Disposition_") ? n.Substring("Disposition_".Length) : n;
        }
    }

    public static bool Is(string id) => Current == id;

    // ── 탐험 ──
    public static int SightBonus => Is(Explorer) ? 1 : 0;
    public static int SearchBonus => Is(Explorer) ? 1 : 0;
    public static float EncounterMult => Is(Explorer) ? 1.15f : 1f;
    public static float DangerFillMult => Is(Greedy) ? 1.20f : 1f;
    public static float ChestGoodMult => Is(Curious) ? 1.20f : 1f;
    public static float TrapDamageMult => Is(Curious) ? 1.30f : Is(Cautious) ? 0.50f : 1f;
    public static float TrapSenseBonus => Is(Cautious) ? 0.25f : 0f;
    public static float ShopBuyMult => Is(Miser) ? 0.85f : 1f;
    public static float HealItemMult => Is(Miser) ? 0.80f : 1f;

    // ── 전투 ──
    public static float GoldMult => Is(Greedy) ? 1.25f : 1f;
    public static float AccuracyBonus => Is(Perfectionist) ? 0.05f : 0f;
    public static float EvasionBonus => Is(Perfectionist) ? 0.05f : 0f;
    public const float PerfectionistMissAttack = 0.90f;      // 빗나간 다음 턴 공격 ×0.9
    public const float PerfectionistTrapAccuracy = 0.10f;    // 함정에 걸린 층에서 명중 −10%p
    public static float FleeBonus => Is(Coward) ? 0.25f : 0f;
    public const float CowardFirstTurnAttack = 0.85f;
    public static float CritBonus => Is(Reckless) ? 8f : 0f; // %p
    public static float DamageTakenMult => Is(Reckless) ? 1.10f : 1f;
    public const float CautiousFirstTurnSpeed = 0.80f;
    public const float ColdBloodedKillBonus = 0.10f;
    public static float RecruitMult => Is(ColdBlooded) ? 0.5f : 1f;
    public const float LoneWolfAloneMult = 1.10f;
    public const float LoneWolfHealWithAllies = 0.85f;

    /// <summary>완벽주의자: 함정에 걸린 층 (그 층에 있는 동안 명중 −10%p). 새 게임·불러오기 때 지움.</summary>
    public static int PerfectionistTrapFloor = -1;
    public static float TrapAccuracyPenalty =>
        Is(Perfectionist) && PerfectionistTrapFloor > 0 && PerfectionistTrapFloor == DungeonPersistentData.currentFloor ? PerfectionistTrapAccuracy : 0f;

    /// <summary>주인공(동료 몬스터 아님)인가.</summary>
    public static bool IsHero(PlayerStats p) => p != null && !p.IsRecruitedCompanion;
}
