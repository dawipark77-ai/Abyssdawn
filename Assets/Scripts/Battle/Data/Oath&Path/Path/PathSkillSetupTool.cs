#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// 직업별 Path 스킬 SO를 생성/업데이트하는 에디터 도구
/// - 각 직업별 폴더( Warrior / Monk / Mage / Thief / Sister ) 안에 개별 SO 생성
/// </summary>
public static class PathSkillSetupTool
{
    private const string BaseFolder = "Assets/Scripts/Battle/Data/Oath&Path/Path";

    [MenuItem("Tools/Game Setup/Path Skills/워리어 Path 1티어 생성")]
    public static void CreateWarriorTier1()
    {
        EnsureFolders();
        CreateWarriorBloodPrice();
        CreateWarriorWarCry();
        CreateWarriorBerserkersEdge();
        CreateWarriorWeaponOath();
        CreateWarriorIronMomentum();
        SaveAndRefresh();
        Debug.Log("✅ Warrior Path 1티어 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Path Skills/몽크 Path 1티어 생성")]
    public static void CreateMonkTier1()
    {
        EnsureFolders();
        CreateMonkTranscendence();
        CreateMonkInnerBreath();
        CreateMonkFocusedMind();
        CreateMonkHarmonicStrike();
        CreateMonkVoidStep();
        SaveAndRefresh();
        Debug.Log("✅ Monk Path 1티어 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Path Skills/마법사 Path 1티어 생성")]
    public static void CreateMageTier1()
    {
        EnsureFolders();
        CreateMageManaSurge();
        CreateMageSpellRebound();
        CreateMageManaVeil();
        CreateMageChainCurse();
        CreateMageOvercharge();
        SaveAndRefresh();
        Debug.Log("✅ Mage Path 1티어 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Path Skills/시프 Path 1티어 생성")]
    public static void CreateThiefTier1()
    {
        EnsureFolders();
        CreateThiefPickpocket();
        CreateThiefVenomCoat();
        CreateThiefSmokeBomb();
        CreateThiefShadowSwap();
        CreateThiefBlindside();
        SaveAndRefresh();
        Debug.Log("✅ Thief Path 1티어 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Path Skills/수녀 Path 1티어 생성")]
    public static void CreateSisterTier1()
    {
        EnsureFolders();
        CreateSisterHealingLight();
        CreateSisterSanctuary();
        CreateSisterSoulDrain();
        CreateSisterDarkOffering();
        CreateSisterTwistedPrayer();
        SaveAndRefresh();
        Debug.Log("✅ Sister Path 1티어 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Path Skills/모든 직업 Path 1티어 생성")]
    public static void CreateAllTier1()
    {
        CreateWarriorTier1();
        CreateMonkTier1();
        CreateMageTier1();
        CreateThiefTier1();
        CreateSisterTier1();
        Debug.Log("✅ 모든 직업 Path 1티어 스킬 SO 생성/업데이트 완료");
    }

    // --- 공용 유틸 ---

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle"))
            AssetDatabase.CreateFolder("Assets/Scripts", "Battle");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle/Data"))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle/Data/Oath&Path"))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data", "Oath&Path");
        if (!AssetDatabase.IsValidFolder(BaseFolder))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data/Oath&Path", "Path");

        EnsureJobFolder("Warrior");
        EnsureJobFolder("Monk");
        EnsureJobFolder("Mage");
        EnsureJobFolder("Thief");
        EnsureJobFolder("Sister");
    }

    private static void EnsureJobFolder(string job)
    {
        string path = $"{BaseFolder}/{job}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(BaseFolder, job);
        }
    }

    private static PathSkillData CreateOrLoad(string jobFolder, string fileName)
    {
        string path = $"{BaseFolder}/{jobFolder}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<PathSkillData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<PathSkillData>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // --- Warrior Tier 1 ---

    private static void CreateWarriorBloodPrice()
    {
        var s = CreateOrLoad("Warrior", "Warrior_T1_BloodPrice");
        s.skillID = "Warrior_T1_BloodPrice";
        s.skillName = "Blood Price";
        s.jobClass = JobClassType.Warrior;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.costType = PathSkillCostType.None;
        s.costValue = 0f;
        s.effectDescription =
            "HP 소모 스킬 사용 시, 소모량만큼 다음 공격 데미지 +1% 누적 (최대 +15%).\n" +
            "전투 종료 시 누적 효과 초기화.";
        s.drawbackDescription = "HP를 소모하는 스킬을 자주 사용할수록 리스크가 커진다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateWarriorWarCry()
    {
        var s = CreateOrLoad("Warrior", "Warrior_T1_WarCry");
        s.skillID = "Warrior_T1_WarCry";
        s.skillName = "War Cry";
        s.jobClass = JobClassType.Warrior;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "2턴 동안 파티 전체 공격력 +8%.\n" +
            "공포/정신 계열 저주 저항 +20%.";
        s.drawbackDescription = "시전자 HP 5%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateWarriorBerserkersEdge()
    {
        var s = CreateOrLoad("Warrior", "Warrior_T1_BerserkersEdge");
        s.skillID = "Warrior_T1_BerserkersEdge";
        s.skillName = "Berserker's Edge";
        s.jobClass = JobClassType.Warrior;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.costType = PathSkillCostType.None;
        s.effectDescription =
            "HP 40% 이하일 때 공격력 +15%, 받는 피해 +10%.\n" +
            "생존보다 폭발적인 화력을 중시하는 스타일.";
        s.drawbackDescription = "위험 구간에 머물수록 생존 리스크가 커진다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateWarriorWeaponOath()
    {
        var s = CreateOrLoad("Warrior", "Warrior_T1_WeaponOath");
        s.skillID = "Warrior_T1_WeaponOath";
        s.skillName = "Weapon Oath";
        s.jobClass = JobClassType.Warrior;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "2턴 동안 현재 장착 무기 공격력 +20%, 크리티컬 확률 +10%.";
        s.drawbackDescription = "시전자 HP 4%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateWarriorIronMomentum()
    {
        var s = CreateOrLoad("Warrior", "Warrior_T1_IronMomentum");
        s.skillID = "Warrior_T1_IronMomentum";
        s.skillName = "Iron Momentum";
        s.jobClass = JobClassType.Warrior;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.costType = PathSkillCostType.None;
        s.effectDescription =
            "같은 대상을 연속 공격할 때마다 데미지 +8% 누적 (최대 +24%).\n" +
            "대상이 바뀌거나 전투가 끝나면 누적이 초기화된다.";
        s.drawbackDescription = "집중 공격이 강점인 대신, 타겟을 자주 바꾸면 효율이 떨어진다.";
        EditorUtility.SetDirty(s);
    }

    // --- Monk Tier 1 ---

    private static void CreateMonkTranscendence()
    {
        var s = CreateOrLoad("Monk", "Monk_T1_Transcendence");
        s.skillID = "Monk_T1_Transcendence";
        s.skillName = "Transcendence";
        s.jobClass = JobClassType.Monk;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "회피 성공할 때마다 다음 공격 데미지 +8% 누적 (최대 +24%).\n" +
            "피격 시 누적이 모두 초기화된다.";
        s.drawbackDescription = "맞기 시작하면 누적 보너스를 유지하기 어렵다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMonkInnerBreath()
    {
        var s = CreateOrLoad("Monk", "Monk_T1_InnerBreath");
        s.skillID = "Monk_T1_InnerBreath";
        s.skillName = "Inner Breath";
        s.jobClass = JobClassType.Monk;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 6f;
        s.effectDescription =
            "즉시 HP Max의 8% 회복.\n" +
            "2턴 동안 저주 저항 +20%.";
        s.drawbackDescription = "MP 6%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMonkFocusedMind()
    {
        var s = CreateOrLoad("Monk", "Monk_T1_FocusedMind");
        s.skillID = "Monk_T1_FocusedMind";
        s.skillName = "Focused Mind";
        s.jobClass = JobClassType.Monk;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "턴 시작 시 MP가 50% 이상이면 AGI +8%, 크리티컬 확률 +5%.";
        s.drawbackDescription = "MP를 절반 이상 유지해야 효과를 받을 수 있다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMonkHarmonicStrike()
    {
        var s = CreateOrLoad("Monk", "Monk_T1_HarmonicStrike");
        s.skillID = "Monk_T1_HarmonicStrike";
        s.skillName = "Harmonic Strike";
        s.jobClass = JobClassType.Monk;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "공격력 × 1.2 배율로 단일 적 공격.\n" +
            "명중 시 자신 HP Max의 3%를 회복.";
        s.drawbackDescription = "MP 5%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMonkVoidStep()
    {
        var s = CreateOrLoad("Monk", "Monk_T1_VoidStep");
        s.skillID = "Monk_T1_VoidStep";
        s.skillName = "Void Step";
        s.jobClass = JobClassType.Monk;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "이번 턴 단일 적의 공격 1회를 완전 회피.\n" +
            "회피에 성공하면 전열↔후열을 자유롭게 이동.";
        s.drawbackDescription = "MP 4%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    // --- Mage Tier 1 ---

    private static void CreateMageManaSurge()
    {
        var s = CreateOrLoad("Mage", "Mage_T1_ManaSurge");
        s.skillID = "Mage_T1_ManaSurge";
        s.skillName = "Mana Surge";
        s.jobClass = JobClassType.Mage;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "MP 80% 이상일 때 다음 마법 데미지 +20%.\n" +
            "발동 후 MP 10% 추가 소모.";
        s.drawbackDescription = "강한 한 방 대신 MP를 많이 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMageSpellRebound()
    {
        var s = CreateOrLoad("Mage", "Mage_T1_SpellRebound");
        s.skillID = "Mage_T1_SpellRebound";
        s.skillName = "Spell Rebound";
        s.jobClass = JobClassType.Mage;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "역효과 발동 시 2턴 동안 마력 +10%.";
        s.drawbackDescription = "역효과 발생을 전제로 한 위험한 빌드에 어울린다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMageManaVeil()
    {
        var s = CreateOrLoad("Mage", "Mage_T1_ManaVeil");
        s.skillID = "Mage_T1_ManaVeil";
        s.skillName = "Mana Veil";
        s.jobClass = JobClassType.Mage;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 8f;
        s.effectDescription =
            "2턴 동안 받는 마법 피해 -20%, 물리 피해 -10%.";
        s.drawbackDescription = "MP 8%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMageChainCurse()
    {
        var s = CreateOrLoad("Mage", "Mage_T1_ChainCurse");
        s.skillID = "Mage_T1_ChainCurse";
        s.skillName = "Chain Curse";
        s.jobClass = JobClassType.Mage;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 7f;
        s.effectDescription =
            "단일 적에게 저주를 부여할 때,\n" +
            "인접한 적에게 50% 확률로 동일 저주가 전염.";
        s.drawbackDescription = "MP 7%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateMageOvercharge()
    {
        var s = CreateOrLoad("Mage", "Mage_T1_Overcharge");
        s.skillID = "Mage_T1_Overcharge";
        s.skillName = "Overcharge";
        s.jobClass = JobClassType.Mage;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "즉시 MP 20% 회복.\n" +
            "다음 턴 역효과 확률 +15%.";
        s.drawbackDescription = "HP 5%를 희생하고 역효과 리스크를 높인다.";
        EditorUtility.SetDirty(s);
    }

    // --- Thief Tier 1 (재설계 버전) ---

    private static void CreateThiefPickpocket()
    {
        var s = CreateOrLoad("Thief", "Thief_T1_Pickpocket");
        s.skillID = "Thief_T1_Pickpocket";
        s.skillName = "Pickpocket";
        s.jobClass = JobClassType.Thief;
        s.tier = 1;
        s.type = PathSkillType.ActiveUtility;
        s.costType = PathSkillCostType.ActionOnly;
        s.effectDescription =
            "전투 중 적 1명 대상.\n" +
            "골드 또는 아이템 1개를 훔치기 시도.\n" +
            "성공 시 획득, 실패 시 해당 적 공격력 +10% (1턴).";
        s.drawbackDescription =
            "데미지가 없고 이 턴 공격을 포기한다.\n" +
            "실패 패널티로 적의 공격력이 상승한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateThiefVenomCoat()
    {
        var s = CreateOrLoad("Thief", "Thief_T1_VenomCoat");
        s.skillID = "Thief_T1_VenomCoat";
        s.skillName = "Venom Coat";
        s.jobClass = JobClassType.Thief;
        s.tier = 1;
        s.type = PathSkillType.ActiveUtility;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 3f;
        s.effectDescription =
            "다음 공격에 독을 부여.\n" +
            "3턴 동안 매 턴 대상 Max HP 2% 피해.\n" +
            "독이 중첩되면 지속시간 +1턴 (최대 5턴).";
        s.drawbackDescription =
            "독 부여에 실패하면 HP만 소모된다.\n" +
            "무기 트리에 없는 시프 전용 독 기술.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateThiefSmokeBomb()
    {
        var s = CreateOrLoad("Thief", "Thief_T1_SmokeBomb");
        s.skillID = "Thief_T1_SmokeBomb";
        s.skillName = "Smoke Bomb";
        s.jobClass = JobClassType.Thief;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.ActionOnly;
        s.effectDescription =
            "파티 전체 이번 턴 회피율 +20%.\n" +
            "도주 시 성공률 +40%.\n" +
            "적 전체 명중률 -15% (1턴).";
        s.drawbackDescription =
            "이 턴 공격을 하지 않는다.\n" +
            "연막 후 후열 이동이 강제되어 전열 유지가 불가능하다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateThiefShadowSwap()
    {
        var s = CreateOrLoad("Thief", "Thief_T1_ShadowSwap");
        s.skillID = "Thief_T1_ShadowSwap";
        s.skillName = "Shadow Swap";
        s.jobClass = JobClassType.Thief;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "아군 1명과 즉시 포지션 교체.\n" +
            "교체된 아군은 이번 턴 회피율 +10%.\n" +
            "교체 직후 자신은 기본 공격 1회 발동 (×0.8 배율).";
        s.drawbackDescription =
            "교체 대상 아군이 전열에 있어야 자신이 후열로 이동할 수 있고,\n" +
            "반대 상황도 동일한 제약을 받는다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateThiefBlindside()
    {
        var s = CreateOrLoad("Thief", "Thief_T1_Blindside");
        s.skillID = "Thief_T1_Blindside";
        s.skillName = "Blindside";
        s.jobClass = JobClassType.Thief;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "후열에서만 사용 가능.\n" +
            "전열 또는 후열 적 단일 대상 공격.\n" +
            "기본 배율 ×1.4, 대상이 이미 행동을 마친 적이라면 ×1.8로 상승.\n" +
            "명중 시 대상 방어력 -10% (2턴).";
        s.drawbackDescription =
            "전열에서는 사용할 수 없고,\n" +
            "적이 아직 행동 전이면 배율이 낮다.";
        EditorUtility.SetDirty(s);
    }

    // --- Sister Path Tier 구조 (성녀 / 타락 계열, 2-1-2) ---

    private static void CreateSisterHealingLight()
    {
        var s = CreateOrLoad("Sister", "Sister_T1_HealingLight");
        s.skillID = "Sister_T1_HealingLight";
        s.skillName = "Healing Light";
        s.jobClass = JobClassType.Sister;
        s.tier = 1;
        s.type = PathSkillType.ActiveHeal;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 6f;
        s.effectDescription =
            "단일 대상 HP Max의 15% 회복.\n" +
            "대상에게 걸린 저주 1개 해제.";
        s.drawbackDescription = "MP 6%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSisterSanctuary()
    {
        var s = CreateOrLoad("Sister", "Sister_T1_Sanctuary");
        s.skillID = "Sister_T1_Sanctuary";
        s.skillName = "Sanctuary";
        s.jobClass = JobClassType.Sister;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 8f;
        s.effectDescription =
            "2턴 동안 파티 전체 저주 저항 +25%, 받는 피해 -5%.";
        s.drawbackDescription = "MP 8%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSisterSoulDrain()
    {
        var s = CreateOrLoad("Sister", "Sister_T1_SoulDrain");
        s.skillID = "Sister_T1_SoulDrain";
        s.skillName = "Soul Drain";
        s.jobClass = JobClassType.Sister;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "단일 적에게 마력 기반 데미지를 준다.\n" +
            "가한 데미지의 30%만큼 자신의 HP를 회복.";
        s.drawbackDescription = "MP 5%를 소모한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSisterDarkOffering()
    {
        var s = CreateOrLoad("Sister", "Sister_T1_DarkOffering");
        s.skillID = "Sister_T1_DarkOffering";
        s.skillName = "Dark Offering";
        s.jobClass = JobClassType.Sister;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.MPPercent;
        s.costValue = 7f;
        s.effectDescription =
            "자신 HP 10%를 희생.\n" +
            "단일 적에게 마력 기반 데미지 ×2.0.\n" +
            "추가로 저주를 60% 확률로 부여.";
        s.drawbackDescription =
            "HP 희생 + MP 7% 소모.\n" +
            "강한 일격이지만 방어적으로 취약해진다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSisterTwistedPrayer()
    {
        var s = CreateOrLoad("Sister", "Sister_T1_TwistedPrayer");
        s.skillID = "Sister_T1_TwistedPrayer";
        s.skillName = "Twisted Prayer";
        s.jobClass = JobClassType.Sister;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "자신에게 저주가 걸려 있을 때 공격 데미지 +20%, 힐량 -10%.";
        s.drawbackDescription =
            "저주 상태를 유지할수록 화력은 강해지지만,\n" +
            "회복 능력은 떨어진다.";
        EditorUtility.SetDirty(s);
    }
}
#endif

