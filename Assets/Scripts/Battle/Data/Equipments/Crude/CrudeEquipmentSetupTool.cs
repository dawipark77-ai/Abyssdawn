#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// 조잡한(Crude) 장비 시리즈 무기/방패 SO를 생성/업데이트하는 에디터 도구
/// </summary>
public static class CrudeEquipmentSetupTool
{
    private const string BaseFolder = "Assets/Scripts/Battle/Data/Equipments/Crude";

    [MenuItem("Tools/Game Setup/Equipments/조잡한 장비 생성")]
    public static void CreateAllCrudeEquipments()
    {
        EnsureFolder();
        CreateCrudeDagger();
        CreateCrudeKatana();
        CreateCrudeGreatsword();
        CreateCrudeAxe();
        CreateCrudeHammer();
        CreateCrudeSpear();
        CreateCrudePolearm();
        CreateCrudeBow();
        CreateCrudeCrossbow();
        CreateCrudeWand();
        CreateCrudeStaff();

        CreateCrudeBuckler();
        CreateCrudeShield();
        CreateCrudeGreatshield();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ 조잡한 장비 시리즈 생성/업데이트 완료");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle"))
            AssetDatabase.CreateFolder("Assets/Scripts", "Battle");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle/Data"))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle/Data/Equipments"))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data", "Equipments");
        if (!AssetDatabase.IsValidFolder(BaseFolder))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data/Equipments", "Crude");
    }

    private static EquipmentData CreateOrLoad(string fileName)
    {
        string path = $"{BaseFolder}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<EquipmentData>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // ───────────── 무기 ─────────────

    private static void CreateCrudeDagger()
    {
        var e = CreateOrLoad("Crude_Dagger");
        e.equipmentName = "조잡한 단검";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 1;
        e.agiBonus = 1;
        e.defenseBonus = 0;
        e.magicBonus = 0;
        e.luckBonus = 0;
        e.accuracyBonus = 0f;
        e.hpBonus = 0;
        e.mpBonus = 0;
        e.description = "조잡하게 만든 단검. 공격 +1, 민첩 +1.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeKatana()
    {
        var e = CreateOrLoad("Crude_Katana");
        e.equipmentName = "조잡한 도";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 1;
        e.agiBonus = 1;
        e.defenseBonus = 0;
        e.magicBonus = 0;
        e.luckBonus = 0;
        e.accuracyBonus = 0f;
        e.description = "조잡하게 벼린 도. 공격 +1, 민첩 +1.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeGreatsword()
    {
        var e = CreateOrLoad("Crude_Greatsword");
        e.equipmentName = "조잡한 대검";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 2;
        e.description = "무겁고 거친 대검. 공격 +2.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeAxe()
    {
        var e = CreateOrLoad("Crude_Axe");
        e.equipmentName = "조잡한 도끼";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 2;
        e.armorBreakCoefficient = 0.02f;
        e.weaponEffect = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Scripts/Battle/Data/EquipmentsSkills/ArmorBreak.asset");
        e.description = "대충 갈아 만든 도끼. 공격 +2, 방어구 파괴.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeHammer()
    {
        var e = CreateOrLoad("Crude_Hammer");
        e.equipmentName = "조잡한 망치";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 2;
        e.description = "균형이 맞지 않는 망치. 공격 +2.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeSpear()
    {
        var e = CreateOrLoad("Crude_Spear");
        e.equipmentName = "조잡한 창";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 1;
        e.accuracyBonus = 0.02f; // +2%
        e.description = "대충 깎아 만든 창. 공격 +1, 명중 +2%.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudePolearm()
    {
        var e = CreateOrLoad("Crude_Polearm");
        e.equipmentName = "조잡한 폴암";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 1;
        e.accuracyBonus = 0.01f; // +1%
        e.description = "불균형한 폴암. 공격 +1, 명중 +1%.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeBow()
    {
        var e = CreateOrLoad("Crude_Bow");
        e.equipmentName = "조잡한 활";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 1;
        e.agiBonus = 1;
        e.description = "휘어진 활. 공격 +1, 민첩 +1.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeCrossbow()
    {
        var e = CreateOrLoad("Crude_Crossbow");
        e.equipmentName = "조잡한 쇠뇌";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 2;
        e.description = "조악한 쇠뇌. 공격 +2.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeWand()
    {
        var e = CreateOrLoad("Crude_Wand");
        e.equipmentName = "조잡한 한손 완드";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 0;
        e.magicBonus = 1;
        e.mpBonusPercent = 0.05f;
        e.description = "대충 깎아 만든 마법봉. 마력 +1, 최대 MP +5%.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeStaff()
    {
        var e = CreateOrLoad("Crude_Staff");
        e.equipmentName = "조잡한 양손 지팡이";
        e.equipmentType = EquipmentType.TwoHanded;
        e.isTwoHanded = true;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.attackBonus = 0;
        e.magicBonus = 2;
        e.mpBonusPercent = 0.08f;
        e.description = "불균형한 양손 지팡이. 마력 +2, 최대 MP +8%.";
        EditorUtility.SetDirty(e);
    }

    // ───────────── 방패 ─────────────

    private static void CreateCrudeBuckler()
    {
        var e = CreateOrLoad("Crude_Buckler");
        e.equipmentName = "조잡한 버클러";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.defenseBonus = 0;
        // 블록/패링 수치는 별도 시스템에서 처리할 예정, 설명에만 기재
        e.description = "얇은 버클러. 방어 +0, 블록 +3%, 패링 반격 +20%.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeShield()
    {
        var e = CreateOrLoad("Crude_Shield");
        e.equipmentName = "조잡한 방패";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.defenseBonus = 1;
        e.description = "조잡한 목제 방패. 방어 +1, 블록 +5%.";
        EditorUtility.SetDirty(e);
    }

    private static void CreateCrudeGreatshield()
    {
        var e = CreateOrLoad("Crude_Greatshield");
        e.equipmentName = "조잡한 대방패";
        e.equipmentType = EquipmentType.Hand;
        e.isTwoHanded = false;
        e.armourCategory = ArmourCategory.None;
        e.armourType = ArmourType.None;
        e.defenseBonus = 2;
        e.agiBonus = -2;
        e.description = "무겁고 두꺼운 대방패. 방어 +2, 블록 +8%, 민첩 -2.";
        EditorUtility.SetDirty(e);
    }
}
#endif

