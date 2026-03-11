#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// 직업별 Oath & Binding SO를 한 번에 생성/업데이트하는 에디터 도구
/// - 메뉴: Tools/Game Setup/Oath & Binding/*
/// </summary>
public static class OathBindingSetupTool
{
    private const string BaseFolder = "Assets/Scripts/Battle/Data/Oath&Path/Oath&Binding";

    [MenuItem("Tools/Game Setup/Oath & Binding/워리어 서약 생성")]
    public static void CreateWarriorOaths()
    {
        EnsureBaseFolder();
        CreateIronDiscipline();
        CreateWarriorsWill();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Warrior Oath & Binding SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Oath & Binding/몽크 서약 생성")]
    public static void CreateMonkOaths()
    {
        EnsureBaseFolder();
        CreateUnarmedProwess();
        CreateInnerFlow();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Monk Oath & Binding SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Oath & Binding/마법사 서약 생성")]
    public static void CreateMageOaths()
    {
        EnsureBaseFolder();
        CreateArcaneOverload();
        CreateCurseMastery();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Mage Oath & Binding SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Oath & Binding/시프 서약 생성")]
    public static void CreateThiefOaths()
    {
        EnsureBaseFolder();
        CreateShadowVeil();
        CreateGhostStep();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Thief Oath & Binding SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Oath & Binding/수녀 서약 생성")]
    public static void CreateSisterOaths()
    {
        EnsureBaseFolder();
        CreateSacredVow();
        CreateDarkRevelation();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Sister Oath & Binding SO 생성/업데이트 완료");
    }

    [MenuItem("Tools/Game Setup/Oath & Binding/모든 직업 서약 생성")]
    public static void CreateAllOaths()
    {
        CreateWarriorOaths();
        CreateMonkOaths();
        CreateMageOaths();
        CreateThiefOaths();
        CreateSisterOaths();
        Debug.Log("✅ 모든 직업 Oath & Binding SO 생성/업데이트 완료");
    }

    private static void EnsureBaseFolder()
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
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data/Oath&Path", "Oath&Binding");
    }

    private static OathBindingData CreateOrLoad(string fileName)
    {
        string path = $"{BaseFolder}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<OathBindingData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<OathBindingData>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // Warrior
    private static void CreateIronDiscipline()
    {
        var oath = CreateOrLoad("Warrior_IronDiscipline");
        oath.oathID = "Warrior_IronDiscipline";
        oath.oathName = "Iron Discipline";
        oath.jobClass = JobClassType.Warrior;
        oath.bindingStrength = OathBindingStrength.Medium;
        oath.oathDescription =
            "모든 무기 공격력 보정, 전체 무기 명중 +3%.\n" +
            "전투 시작 시 장착 무기에 따라 STR 또는 DEF +7.";
        oath.bindingDescription =
            "마법 스킬 사용 시 이 서약 효과 50% 감소 (2턴).";
        EditorUtility.SetDirty(oath);
    }

    private static void CreateWarriorsWill()
    {
        var oath = CreateOrLoad("Warrior_WarriorsWill");
        oath.oathID = "Warrior_WarriorsWill";
        oath.oathName = "Warrior's Will";
        oath.jobClass = JobClassType.Warrior;
        oath.bindingStrength = OathBindingStrength.Weak;
        oath.oathDescription =
            "전투 시작 시 STR +5, DEF +5.\n" +
            "HP 소모 스킬 소모량 -15%.\n" +
            "아군 사망 시 공격력 +4% 누적 (최대 3회).\n" +
            "HP 30% 이하 시 피해 +10%, 받는 피해 -5%.";
        oath.bindingDescription =
            "HP 회복 시 아군 사망 누적 스택 초기화.";
        EditorUtility.SetDirty(oath);
    }

    // Monk
    private static void CreateUnarmedProwess()
    {
        var oath = CreateOrLoad("Monk_UnarmedProwess");
        oath.oathID = "Monk_UnarmedProwess";
        oath.oathName = "Unarmed Prowess";
        oath.jobClass = JobClassType.Monk;
        oath.bindingStrength = OathBindingStrength.Strong;
        oath.oathDescription =
            "STR +10.\n" +
            "최종 물리 공격력 +20%.";
        oath.bindingDescription =
            "무기 장착 시 이 서약 효과 전부 소멸.";
        EditorUtility.SetDirty(oath);
    }

    private static void CreateInnerFlow()
    {
        var oath = CreateOrLoad("Monk_InnerFlow");
        oath.oathID = "Monk_InnerFlow";
        oath.oathName = "Inner Flow";
        oath.jobClass = JobClassType.Monk;
        oath.bindingStrength = OathBindingStrength.Strong;
        oath.oathDescription =
            "회피 성공 시 MP +3% 회복.\n" +
            "MP 70% 이상 시 스킬 데미지 +10%.\n" +
            "HP 10% 이하 1회 한정 다음 턴 회피율 +30%.";
        oath.bindingDescription =
            "방어구 장착 시 회피율 -15%, MP 회복량 절반 감소.";
        EditorUtility.SetDirty(oath);
    }

    // Mage
    private static void CreateArcaneOverload()
    {
        var oath = CreateOrLoad("Mage_ArcaneOverload");
        oath.oathID = "Mage_ArcaneOverload";
        oath.oathName = "Arcane Overload";
        oath.jobClass = JobClassType.Mage;
        oath.bindingStrength = OathBindingStrength.Medium;
        oath.oathDescription =
            "마력 10포인트당 스킬 데미지 +1%.\n" +
            "MP 30% 이하 시 마법 데미지 +15%, 역효과 확률 2배.";
        oath.bindingDescription =
            "물리 스킬 사용 시 해당 턴 서약 효과 소멸.";
        EditorUtility.SetDirty(oath);
    }

    private static void CreateCurseMastery()
    {
        var oath = CreateOrLoad("Mage_CurseMastery");
        oath.oathID = "Mage_CurseMastery";
        oath.oathName = "Curse Mastery";
        oath.jobClass = JobClassType.Mage;
        oath.bindingStrength = OathBindingStrength.Weak;
        oath.oathDescription =
            "부여 저주 지속시간 +1턴.\n" +
            "자신 저주 지속시간 -1턴.\n" +
            "저주 부여 성공 시 MP 3% 회복.";
        oath.bindingDescription =
            "저주 해제 스킬 사용 시 서약 효과 2턴 비활성화.";
        EditorUtility.SetDirty(oath);
    }

    // Thief
    private static void CreateShadowVeil()
    {
        var oath = CreateOrLoad("Thief_ShadowVeil");
        oath.oathID = "Thief_ShadowVeil";
        oath.oathName = "Shadow Veil";
        oath.jobClass = JobClassType.Thief;
        oath.bindingStrength = OathBindingStrength.Medium;
        oath.oathDescription =
            "후열 위치 시 타겟팅 확률 -20%.\n" +
            "훔치기 실패 패널티 확률 -15%.\n" +
            "함정 감지율 +10%.";
        oath.bindingDescription =
            "전열 위치 시 이 서약 효과 전부 소멸.";
        EditorUtility.SetDirty(oath);
    }

    private static void CreateGhostStep()
    {
        var oath = CreateOrLoad("Thief_GhostStep");
        oath.oathID = "Thief_GhostStep";
        oath.oathName = "Ghost Step";
        oath.jobClass = JobClassType.Thief;
        oath.bindingStrength = OathBindingStrength.Weak;
        oath.oathDescription =
            "함정 감지율 +25%.\n" +
            "도주 성공률 +20%.\n" +
            "선제공격 당할 확률 -30%.";
        oath.bindingDescription =
            "전열에서 전투 시작 시 서약 효과 50% 감소.";
        EditorUtility.SetDirty(oath);
    }

    // Sister
    private static void CreateSacredVow()
    {
        var oath = CreateOrLoad("Sister_SacredVow");
        oath.oathID = "Sister_SacredVow";
        oath.oathName = "Sacred Vow";
        oath.jobClass = JobClassType.Sister;
        oath.bindingStrength = OathBindingStrength.Strong;
        oath.oathDescription =
            "힐 스킬 효과량 +15%.\n" +
            "저주 해제 성공 시 대상 HP 5% 추가 회복.\n" +
            "파티원 저주 상태일 때 마력 +10%.";
        oath.bindingDescription =
            "Dark Revelation과 동시 보유 불가.";
        EditorUtility.SetDirty(oath);
    }

    private static void CreateDarkRevelation()
    {
        var oath = CreateOrLoad("Sister_DarkRevelation");
        oath.oathID = "Sister_DarkRevelation";
        oath.oathName = "Dark Revelation";
        oath.jobClass = JobClassType.Sister;
        oath.bindingStrength = OathBindingStrength.Strong;
        oath.oathDescription =
            "힐 스킬 효과량 -20%.\n" +
            "공격 마법 데미지 +20%.\n" +
            "저주 부여 확률 +15%.";
        oath.bindingDescription =
            "Sacred Vow와 동시 보유 불가.\n" +
            "Healing Light, Sanctuary 사용 불가.\n" +
            "대신 Dark Offering 데미지 +10%.";
        EditorUtility.SetDirty(oath);
    }
}
#endif

