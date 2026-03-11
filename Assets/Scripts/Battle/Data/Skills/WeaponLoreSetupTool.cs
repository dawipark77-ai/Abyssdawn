#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// 무기별 Lore 스킬 SO(Dagger_Lore, Katana_Lore 등)를 생성/업데이트하는 에디터 도구
/// - 각 무기 트리 폴더 안에 개별 WeaponLoreSkillData 에셋 생성
/// - WeaponCategory를 통해 "어떤 무기 스킬인지"를 명시적으로 구분
/// </summary>
public static class WeaponLoreSetupTool
{
    private const string BaseFolder = "Assets/Scripts/Battle/Data/Skills";

    [MenuItem("Tools/Game Setup/Weapon Lore/단검(Dagger) 트리 생성")]
    public static void CreateDaggerLore()
    {
        EnsureFolders();
        CreateDagger_FlickeringBlade();
        CreateDagger_VitalStrike();
        CreateDagger_ShadowSlash();
        CreateDagger_FlurryStab();
        CreateDagger_BladeDance();
        CreateDagger_RendWound();
        SaveAndRefresh();
        Debug.Log("✅ Dagger Lore 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Weapon Lore/도(Katana) 트리 생성")]
    public static void CreateKatanaLore()
    {
        EnsureFolders();
        CreateKatana_Mastery();
        CreateKatana_IaiStrike();
        CreateKatana_FlowingCut();
        CreateKatana_CrimsonStance();
        CreateKatana_CleavingDraw();
        SaveAndRefresh();
        Debug.Log("✅ Katana Lore 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Weapon Lore/듀얼(Dual) 트리 생성")]
    public static void CreateDualLore()
    {
        EnsureFolders();
        CreateDual_CrossSlash();
        CreateDual_BloodFeast();
        CreateDual_TwinFlow();
        CreateDual_OffRhythmStrike();
        CreateDual_FrenziedDance();
        CreateDual_CrossResonance();
        SaveAndRefresh();
        Debug.Log("✅ Dual Lore 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Weapon Lore/창(Spear) 트리 생성")]
    public static void CreateSpearLore()
    {
        EnsureFolders();
        CreateSpear_Mastery();
        CreateSpear_LinearThrust();
        CreateSpear_Impale();
        CreateSpear_ThreateningReach();
        CreateSpear_RushingThrust();
        CreateSpear_PreemptiveThrust();
        SaveAndRefresh();
        Debug.Log("✅ Spear Lore 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Weapon Lore/폴암(Polearm) 트리 생성")]
    public static void CreatePolearmLore()
    {
        EnsureFolders();
        CreatePolearm_Mastery();
        CreatePolearm_RearArcSlash();
        CreatePolearm_FrontArcSlash();
        CreatePolearm_SweepingStrike();
        CreatePolearm_PolearmDominance();
        CreatePolearm_RearSuppression();
        SaveAndRefresh();
        Debug.Log("✅ Polearm Lore 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Weapon Lore/활(Bow) 트리 생성")]
    public static void CreateBowLore()
    {
        EnsureFolders();
        CreateBow_Mastery();
        CreateBow_RapidShot();
        CreateBow_AimedShot();
        CreateBow_FocusFire();
        CreateBow_ScatterShot();
        CreateBow_SuppressingShot();
        SaveAndRefresh();
        Debug.Log("✅ Bow Lore 스킬 SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Weapon Lore/모든 무기 Lore 생성")]
    public static void CreateAllWeaponLores()
    {
        CreateDaggerLore();
        CreateKatanaLore();
        CreateDualLore();
        CreateSpearLore();
        CreatePolearmLore();
        CreateBowLore();
        Debug.Log("✅ 모든 무기 Lore 스킬 SO 생성/업데이트 완료");
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
        if (!AssetDatabase.IsValidFolder(BaseFolder))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data", "Skills");

        EnsureLoreFolder("Dagger_Lore");
        EnsureLoreFolder("Katana_Lore");
        EnsureLoreFolder("Dual_Lore");
        EnsureLoreFolder("Spear_Lore");
        EnsureLoreFolder("Polearm_Lore");
        EnsureLoreFolder("Bow_Lore");
    }

    private static void EnsureLoreFolder(string loreFolder)
    {
        string path = $"{BaseFolder}/{loreFolder}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(BaseFolder, loreFolder);
        }
    }

    private static WeaponLoreSkillData CreateOrLoad(string loreFolder, string fileName)
    {
        string path = $"{BaseFolder}/{loreFolder}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<WeaponLoreSkillData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<WeaponLoreSkillData>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void SaveAndRefresh()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // --- 단검 트리 (Dagger Lore) ---

    private static void CreateDagger_FlickeringBlade()
    {
        var s = CreateOrLoad("Dagger_Lore", "Dagger_FlickeringBlade");
        s.skillID = "Dagger_FlickeringBlade";
        s.skillName = "신속한 손놀림 (Flickering Blade)";
        s.weaponCategory = WeaponCategory.Dagger;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.costType = PathSkillCostType.None;
        s.effectDescription =
            "단검 장착 시 턴 순서 +5.\n" +
            "듀얼 장착 시 첫 타 명중하면 두 번째 타격 명중률 +15%.";
        s.drawbackDescription =
            "단검 외 무기로 변경 시 1턴 동안 공격력 -10%.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDagger_VitalStrike()
    {
        var s = CreateOrLoad("Dagger_Lore", "Dagger_VitalStrike");
        s.skillID = "Dagger_VitalStrike";
        s.skillName = "급소 파고들기 (Vital Strike)";
        s.weaponCategory = WeaponCategory.Dagger;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "후열에서 전열을 공격할 때 데미지 +10%.\n" +
            "적 HP가 75% 이상일 때 크리티컬 확률 +15%.";
        s.drawbackDescription = "조건 자체가 대가이므로 추가 패널티 없음.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDagger_ShadowSlash()
    {
        var s = CreateOrLoad("Dagger_Lore", "Dagger_ShadowSlash");
        s.skillID = "Dagger_ShadowSlash";
        s.skillName = "그림자 베기 (Shadow Slash)";
        s.weaponCategory = WeaponCategory.Dagger;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "배율 ×1.3의 단일 공격.\n" +
            "명중 시 적 방어력 -8% (2턴).\n" +
            "듀얼 장착 시 즉시 두 번째 단검 추가 타격 ×0.5 발동.";
        s.drawbackDescription =
            "전열에서 사용할 경우 전체 데미지 -15%.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDagger_FlurryStab()
    {
        var s = CreateOrLoad("Dagger_Lore", "Dagger_FlurryStab");
        s.skillID = "Dagger_FlurryStab";
        s.skillName = "연속 찌르기 (Flurry Stab)";
        s.weaponCategory = WeaponCategory.Dagger;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "3타 연속 공격, 각 타격 배율 ×0.5.\n" +
            "각 타격은 독립 크리티컬 판정.\n" +
            "3타 모두 명중 시 마지막 타격에 ×1.5 추가 배율 적용.";
        s.drawbackDescription =
            "사용 후 1턴 동안 방어력 -10%.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDagger_BladeDance()
    {
        var s = CreateOrLoad("Dagger_Lore", "Dagger_BladeDance");
        s.skillID = "Dagger_BladeDance";
        s.skillName = "칼날 춤 (Blade Dance)";
        s.weaponCategory = WeaponCategory.Dagger;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 3f;
        s.effectDescription =
            "이번 턴 첫 번째 근접 공격에 대해 회피율 +25%.\n" +
            "회피에 성공하면 즉시 반격 ×0.7 발동.";
        s.drawbackDescription =
            "원거리 공격에는 효과가 없으며,\n" +
            "회피에 실패하면 반격도 발생하지 않는다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDagger_RendWound()
    {
        var s = CreateOrLoad("Dagger_Lore", "Dagger_RendWound");
        s.skillID = "Dagger_RendWound";
        s.skillName = "상처 벌리기 (Rend Wound)";
        s.weaponCategory = WeaponCategory.Dagger;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 3f;
        s.effectDescription =
            "출혈 상태의 적에게만 사용 가능.\n" +
            "배율 ×0.3의 추가 공격.\n" +
            "기존 출혈 지속 턴 +1, 출혈 데미지 +0.5% 추가.";
        s.drawbackDescription =
            "출혈이 걸려 있지 않은 적에게는 사용 불가.\n" +
            "먼저 출혈을 부여해야 진가를 발휘한다.";
        EditorUtility.SetDirty(s);
    }

    // --- 도 트리 (Katana Lore) ---

    private static void CreateKatana_Mastery()
    {
        var s = CreateOrLoad("Katana_Lore", "Katana_Mastery");
        s.skillID = "Katana_Mastery";
        s.skillName = "도 숙련 (Katana Mastery)";
        s.weaponCategory = WeaponCategory.Katana;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "도 장착 시 민첩 +4, 공격력 +2.\n" +
            "기본 공격 명중 시 10% 확률로 출혈 부여 (Max HP 3%/턴, 4턴).\n" +
            "레벨당 공격력 +1 (최대 +5).";
        s.drawbackDescription =
            "방패 장착 불가.\n" +
            "갑옷 착용 시 민첩 보정 -2.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateKatana_IaiStrike()
    {
        var s = CreateOrLoad("Katana_Lore", "Katana_IaiStrike");
        s.skillID = "Katana_IaiStrike";
        s.skillName = "거합 (Iai Strike)";
        s.weaponCategory = WeaponCategory.Katana;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "민첩 판정과 무관하게 이번 턴 가장 먼저 행동.\n" +
            "배율 ×1.6.\n" +
            "명중 시 출혈 부여 (Max HP 3%/턴, 4턴).";
        s.drawbackDescription =
            "같은 전투에서 2회 연속 사용할 경우 배율이 ×0.8로 감쇄.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateKatana_FlowingCut()
    {
        var s = CreateOrLoad("Katana_Lore", "Katana_FlowingCut");
        s.skillID = "Katana_FlowingCut";
        s.skillName = "유수 베기 (Flowing Cut)";
        s.weaponCategory = WeaponCategory.Katana;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 3f;
        s.effectDescription =
            "첫 타 배율 ×1.1.\n" +
            "명중 시 즉시 추가 베기 ×0.7 발동.\n" +
            "추가 베기는 출혈 중인 적에게 ×1.2 배율 보너스.";
        s.drawbackDescription =
            "첫 타가 명중했을 때만 추가 베기가 발동.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateKatana_CrimsonStance()
    {
        var s = CreateOrLoad("Katana_Lore", "Katana_CrimsonStance");
        s.skillID = "Katana_CrimsonStance";
        s.skillName = "선혈 자세 (Crimson Stance)";
        s.weaponCategory = WeaponCategory.Katana;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "5턴 동안 모든 공격의 출혈 부여 확률 +25%.\n" +
            "출혈 중인 적 공격 시 데미지 +15%.\n" +
            "출혈 적 처치 시 민첩 +5 (2턴).";
        s.drawbackDescription =
            "방어력 -10%.\n" +
            "출혈이 없는 적에게는 데미지 보너스 없음.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateKatana_CleavingDraw()
    {
        var s = CreateOrLoad("Katana_Lore", "Katana_CleavingDraw");
        s.skillID = "Katana_CleavingDraw";
        s.skillName = "일도양단 (Cleaving Draw)";
        s.weaponCategory = WeaponCategory.Katana;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 7f;
        s.effectDescription =
            "배율 ×2.0, 방어 관통 +20%.\n" +
            "명중 시 출혈 부여 (Max HP 3%/턴, 4턴).\n" +
            "대상이 이미 출혈 중이면 배율 ×2.5로 상승하고, 출혈 턴 수가 리셋.";
        s.drawbackDescription =
            "사용 후 1턴 동안 민첩 -8.\n" +
            "빗나가면 큰 손해.";
        EditorUtility.SetDirty(s);
    }

    // --- 듀얼 트리 (Dual Lore) ---

    private static void CreateDual_CrossSlash()
    {
        var s = CreateOrLoad("Dual_Lore", "Dual_CrossSlash");
        s.skillID = "Dual_CrossSlash";
        s.skillName = "교차 베기 (Cross Slash)";
        s.weaponCategory = WeaponCategory.Dual;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "좌우 각 ×0.6, 총 2타 공격.\n" +
            "2타 모두 명중 시 다음 턴 듀얼 공격력 +10%.";
        s.drawbackDescription =
            "한손 무기 단일 장착 시 사용 불가.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDual_BloodFeast()
    {
        var s = CreateOrLoad("Dual_Lore", "Dual_BloodFeast");
        s.skillID = "Dual_BloodFeast";
        s.skillName = "피의 축제 (Blood Feast)";
        s.weaponCategory = WeaponCategory.Dual;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 6f;
        s.effectDescription =
            "단일 적에게 배율 ×1.4 데미지.\n" +
            "대상을 처치하면 인접 적에게 자동 연쇄 공격, 코스트 0, 배율 ×1.0으로 초기화.\n" +
            "조건 충족 시 최대 4연속 처치 가능.";
        s.drawbackDescription =
            "처치에 실패하면 연쇄 공격이 발생하지 않는다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDual_TwinFlow()
    {
        var s = CreateOrLoad("Dual_Lore", "Dual_TwinFlow");
        s.skillID = "Dual_TwinFlow";
        s.skillName = "쌍검류 (Twin Flow)";
        s.weaponCategory = WeaponCategory.Dual;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "듀얼 장착 중 기본 공격이 자동으로 2타가 된다 (각 ×0.35, 총 ×0.7).\n" +
            "첫 타 크리티컬 시 두 번째 타격의 크리티컬 확률 +20%.";
        s.drawbackDescription =
            "방패 장착 불가.\n" +
            "기본 공격 총합이 한손 단일보다 낮아 방어력이 높은 적에게는 불리.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDual_OffRhythmStrike()
    {
        var s = CreateOrLoad("Dual_Lore", "Dual_OffRhythmStrike");
        s.skillID = "Dual_OffRhythmStrike";
        s.skillName = "엇박 공격 (Off-Rhythm Strike)";
        s.weaponCategory = WeaponCategory.Dual;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 3f;
        s.effectDescription =
            "이번 타격에 한해 적의 블록/회피 판정 -20%.\n" +
            "배율 ×1.1.";
        s.drawbackDescription =
            "블록/회피가 없는 적에게는 그저 ×1.1 배율의 약한 공격에 불과하다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDual_FrenziedDance()
    {
        var s = CreateOrLoad("Dual_Lore", "Dual_FrenziedDance");
        s.skillID = "Dual_FrenziedDance";
        s.skillName = "광란의 춤 (Frenzied Dance)";
        s.weaponCategory = WeaponCategory.Dual;
        s.tier = 1;
        s.type = PathSkillType.ActiveSupport;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "4턴 동안 듀얼 기본 공격 타격 수 +1 (총 3타, 각 ×0.35).\n" +
            "피격 시 즉시 반격 ×0.4 자동 발동.";
        s.drawbackDescription =
            "방어력 -10%.\n" +
            "단일 무기 장착 시 효과가 없다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateDual_CrossResonance()
    {
        var s = CreateOrLoad("Dual_Lore", "Dual_CrossResonance");
        s.skillID = "Dual_CrossResonance";
        s.skillName = "이종 공명 (Cross Resonance)";
        s.weaponCategory = WeaponCategory.Dual;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "서로 다른 종류의 한손 무기를 듀얼 장착할 때만 발동.\n" +
            "각 무기 트리 패시브 중 1개씩 선택하여 효과 +20% 강화.";
        s.drawbackDescription =
            "같은 종 듀얼에서는 발동하지 않으며,\n" +
            "강화할 패시브 선택은 전투 전에 고정된다.";
        EditorUtility.SetDirty(s);
    }

    // --- 창 트리 (Spear Lore) ---

    private static void CreateSpear_Mastery()
    {
        var s = CreateOrLoad("Spear_Lore", "Spear_Mastery");
        s.skillID = "Spear_Mastery";
        s.skillName = "창 숙련 (Spear Mastery)";
        s.weaponCategory = WeaponCategory.Spear;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "공격력 +3, 명중 +5%.\n" +
            "후열에서 전열을 공격 가능 (명중 -10%).\n" +
            "전열에서 후열을 공격 가능 (명중 -15%).";
        s.drawbackDescription =
            "포지션 유연성 대신 명중 패널티를 감수해야 한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSpear_LinearThrust()
    {
        var s = CreateOrLoad("Spear_Lore", "Spear_LinearThrust");
        s.skillID = "Spear_LinearThrust";
        s.skillName = "일직선 찌르기 (Linear Thrust)";
        s.weaponCategory = WeaponCategory.Spear;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "배율 ×1.3.\n" +
            "방어 관통 +20%.\n" +
            "명중 시 적 포지션을 1칸 후퇴시킨다.";
        s.drawbackDescription =
            "직선 상 단일 타겟만 공격 가능.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSpear_Impale()
    {
        var s = CreateOrLoad("Spear_Lore", "Spear_Impale");
        s.skillID = "Spear_Impale";
        s.skillName = "꿰뚫기 (Impale)";
        s.weaponCategory = WeaponCategory.Spear;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 6f;
        s.effectDescription =
            "배율 ×1.5, 방어 완전 무시.\n" +
            "명중 시 적을 2턴 동안 포지션 고정.\n" +
            "전열 적 공격 시 후열까지 관통하여 50% 위력 추가 타격.";
        s.drawbackDescription =
            "HP 소모가 크고 빗나가면 큰 손해.\n" +
            "후열 관통은 후열 4번 기준 명중률로 판정.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSpear_ThreateningReach()
    {
        var s = CreateOrLoad("Spear_Lore", "Spear_ThreateningReach");
        s.skillID = "Spear_ThreateningReach";
        s.skillName = "견제 자세 (Threatening Reach)";
        s.weaponCategory = WeaponCategory.Spear;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "창 장착 중 적이 자신에게 근접 공격을 시도하면 20% 확률로 선제 반격 ×0.6.\n" +
            "전열에 있을 때 확률 +10%.";
        s.drawbackDescription =
            "반격 배율이 낮아 억제력이 핵심이며, 순수 딜은 높지 않다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSpear_RushingThrust()
    {
        var s = CreateOrLoad("Spear_Lore", "Spear_RushingThrust");
        s.skillID = "Spear_RushingThrust";
        s.skillName = "돌격 찌르기 (Rushing Thrust)";
        s.weaponCategory = WeaponCategory.Spear;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "배율 ×1.4.\n" +
            "후열에서 사용할 경우 명중 패널티 제거.\n" +
            "명중 시 자신을 전열로 전진시키며,\n" +
            "크리티컬 시 적 민첩 -10% (2턴).";
        s.drawbackDescription =
            "전열로 전진하면서 위험에 노출된다.\n" +
            "후퇴 스킬이 없으면 유지가 힘들 수 있다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateSpear_PreemptiveThrust()
    {
        var s = CreateOrLoad("Spear_Lore", "Spear_PreemptiveThrust");
        s.skillID = "Spear_PreemptiveThrust";
        s.skillName = "선제 견제 (Preemptive Thrust)";
        s.weaponCategory = WeaponCategory.Spear;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "전열 적 중 누군가 공격 행동을 선언했을 때만 발동.\n" +
            "해당 적의 행동을 취소시키고, 선제 찌르기 ×0.8을 가한다.";
        s.drawbackDescription =
            "전열 적의 행동에만 반응 가능.\n" +
            "조건을 만족하지 못하면 발동하지 않고 HP도 소모되지 않는다.";
        EditorUtility.SetDirty(s);
    }

    // --- 폴암 트리 (Polearm Lore) ---

    private static void CreatePolearm_Mastery()
    {
        var s = CreateOrLoad("Polearm_Lore", "Polearm_Mastery");
        s.skillID = "Polearm_Mastery";
        s.skillName = "폴암 숙련 (Polearm Mastery)";
        s.weaponCategory = WeaponCategory.Polearm;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "공격력 +3.\n" +
            "전열 적 2명을 동시에 공격하는 스킬 사용 가능.\n" +
            "후열에서 전열 공격 시 명중 -10%.\n" +
            "레벨당 공격력 +1 (최대 +5).";
        s.drawbackDescription =
            "양손 무기라 방패/듀얼 장착이 불가능.";
        EditorUtility.SetDirty(s);
    }

    private static void CreatePolearm_RearArcSlash()
    {
        var s = CreateOrLoad("Polearm_Lore", "Polearm_RearArcSlash");
        s.skillID = "Polearm_RearArcSlash";
        s.skillName = "후열 횡베기 (Rear Arc Slash)";
        s.weaponCategory = WeaponCategory.Polearm;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "후열에서 전열 1~2번 슬롯을 동시에 공격.\n" +
            "각 타격 배율 ×0.85.\n" +
            "명중한 적마다 민첩 -5 (1턴).";
        s.drawbackDescription =
            "후열 공격 명중 패널티가 적용되며,\n" +
            "슬롯 4 기준으로는 명중률이 낮다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreatePolearm_FrontArcSlash()
    {
        var s = CreateOrLoad("Polearm_Lore", "Polearm_FrontArcSlash");
        s.skillID = "Polearm_FrontArcSlash";
        s.skillName = "전열 횡베기 (Front Arc Slash)";
        s.weaponCategory = WeaponCategory.Polearm;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "전열에서 전열 1~2번을 동시에 공격 (각 ×0.9).\n" +
            "후열 3번 슬롯까지 50% 위력의 추가 타격.";
        s.drawbackDescription =
            "전열 노출 상태에서 사용해야 하므로 위험 부담이 크다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreatePolearm_SweepingStrike()
    {
        var s = CreateOrLoad("Polearm_Lore", "Polearm_SweepingStrike");
        s.skillID = "Polearm_SweepingStrike";
        s.skillName = "회전 베기 (Sweeping Strike)";
        s.weaponCategory = WeaponCategory.Polearm;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 7f;
        s.effectDescription =
            "전열 전체 + 후열 전체를 동시에 공격.\n" +
            "각 타격 배율 ×0.6, 각 슬롯 개별 명중 판정.\n" +
            "명중한 모든 적에게 민첩 -3 (1턴).";
        s.drawbackDescription =
            "배율이 낮고 HP 소모가 크며, 주 목적은 민첩 디버프.";
        EditorUtility.SetDirty(s);
    }

    private static void CreatePolearm_PolearmDominance()
    {
        var s = CreateOrLoad("Polearm_Lore", "Polearm_Dominance");
        s.skillID = "Polearm_Dominance";
        s.skillName = "폴암의 위압 (Polearm Dominance)";
        s.weaponCategory = WeaponCategory.Polearm;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "2명 이상을 동시에 타격할 때마다 다음 단일 공격 +10% 누적 (최대 3중첩).\n" +
            "단일 공격 사용 시 누적이 소모된다.";
        s.drawbackDescription =
            "단일 공격만 사용하면 중첩을 쌓을 수 없다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreatePolearm_RearSuppression()
    {
        var s = CreateOrLoad("Polearm_Lore", "Polearm_RearSuppression");
        s.skillID = "Polearm_RearSuppression";
        s.skillName = "후열 압제 (Rear Suppression)";
        s.weaponCategory = WeaponCategory.Polearm;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "후열 적 중 누군가 공격 행동을 선언했을 때만 발동.\n" +
            "해당 적의 행동을 취소하고, 후열 횡베기 ×0.6으로 공격.";
        s.drawbackDescription =
            "후열 적의 행동에만 반응 가능.\n" +
            "조건을 만족하지 못하면 발동하지 않고 HP도 소모되지 않는다.\n" +
            "이 턴에는 추가 행동을 할 수 없다.";
        EditorUtility.SetDirty(s);
    }

    // --- 활 트리 (Bow Lore) ---

    private static void CreateBow_Mastery()
    {
        var s = CreateOrLoad("Bow_Lore", "Bow_Mastery");
        s.skillID = "Bow_Mastery";
        s.skillName = "활 숙련 (Bow Mastery)";
        s.weaponCategory = WeaponCategory.Bow;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "민첩 +3, 명중 +5%.\n" +
            "크리티컬 발생 시 다음 공격의 크리티컬 확률 +10% 누적 (최대 3중첩).\n" +
            "후열 고정이지만 전열 공격 시 명중 패널티 없음.";
        s.drawbackDescription =
            "근접 적이 후열까지 침투하면 공격력 -20%.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateBow_RapidShot()
    {
        var s = CreateOrLoad("Bow_Lore", "Bow_RapidShot");
        s.skillID = "Bow_RapidShot";
        s.skillName = "속사 (Rapid Shot)";
        s.weaponCategory = WeaponCategory.Bow;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 3f;
        s.effectDescription =
            "2회 연속 발사, 각 타격 배율 ×0.7.\n" +
            "각 타격은 독립 크리티컬 판정.\n" +
            "2발 모두 명중 시 크리티컬 누적 +1 추가.";
        s.drawbackDescription =
            "배율이 낮아 방어력이 높은 적에게는 비효율적.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateBow_AimedShot()
    {
        var s = CreateOrLoad("Bow_Lore", "Bow_AimedShot");
        s.skillID = "Bow_AimedShot";
        s.skillName = "조준 (Aimed Shot)";
        s.weaponCategory = WeaponCategory.Bow;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "배율 ×1.5, 명중 +20%.\n" +
            "크리티컬 시 적 민첩 -10% (2턴).\n" +
            "후열 4번 슬롯 적에 대한 명중 패널티 절반 감소.";
        s.drawbackDescription =
            "단발이라 속사에 비해 크리 누적 속도가 느리다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateBow_FocusFire()
    {
        var s = CreateOrLoad("Bow_Lore", "Bow_FocusFire");
        s.skillID = "Bow_FocusFire";
        s.skillName = "집중 사격 (Focus Fire)";
        s.weaponCategory = WeaponCategory.Bow;
        s.tier = 1;
        s.type = PathSkillType.Passive;
        s.effectDescription =
            "같은 적을 3턴 연속 공격하면 명중 +15%, 크리티컬 +10%가 고정 부여.\n" +
            "대상을 변경하면 누적이 초기화된다.";
        s.drawbackDescription =
            "단일 타겟에만 집중해야 하며,\n" +
            "파티가 다른 적을 공격해도 스스로는 동일 타겟을 유지해야 한다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateBow_ScatterShot()
    {
        var s = CreateOrLoad("Bow_Lore", "Bow_ScatterShot");
        s.skillID = "Bow_ScatterShot";
        s.skillName = "산탄 사격 (Scatter Shot)";
        s.weaponCategory = WeaponCategory.Bow;
        s.tier = 1;
        s.type = PathSkillType.ActiveAttack;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 5f;
        s.effectDescription =
            "적 2명을 동시에 공격, 각 배율 ×0.8.\n" +
            "전열/후열 무관하게 타겟 선택.\n" +
            "각 타격은 독립 크리티컬 판정.\n" +
            "2명 모두 명중 시 크리티컬 누적 +1.";
        s.drawbackDescription =
            "쇠뇌는 이 스킬을 사용할 수 없다.";
        EditorUtility.SetDirty(s);
    }

    private static void CreateBow_SuppressingShot()
    {
        var s = CreateOrLoad("Bow_Lore", "Bow_SuppressingShot");
        s.skillID = "Bow_SuppressingShot";
        s.skillName = "견제 사격 (Suppressing Shot)";
        s.weaponCategory = WeaponCategory.Bow;
        s.tier = 1;
        s.type = PathSkillType.ActiveManeuver;
        s.costType = PathSkillCostType.HPPercent;
        s.costValue = 4f;
        s.effectDescription =
            "전열/후열 관계 없이 단일 적을 지정.\n" +
            "해당 적이 행동하기 전에 선제 발동하여 배율 ×0.6 데미지.\n" +
            "행동을 크게 억제하는 견제용 스킬.";
        s.drawbackDescription =
            "데미지가 낮고, 이 턴에는 추가 행동을 할 수 없다.";
        EditorUtility.SetDirty(s);
    }
}
#endif

