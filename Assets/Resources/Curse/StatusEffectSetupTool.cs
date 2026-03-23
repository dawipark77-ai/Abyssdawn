#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;

/// <summary>
/// 기본 상태이상 SO(스턴, 출혈1/출혈2, 독)를 생성/업데이트하는 에디터 도구
/// </summary>
public static class StatusEffectSetupTool
{
    private const string BaseFolder = "Assets/Scripts/Battle/Data/Curse";

    [MenuItem("Tools/Game Setup/Status Effects/기본 상태이상 생성")]
    public static void CreateBaseStatusEffects()
    {
        EnsureFolder();
        CreateStun();
        CreateBleed1_Dagger();
        CreateBleed2_Katana();
        CreatePoison();
        CreateIgnite();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ 스턴 / 출혈1 / 출혈2 / 독 / 점화 StatusEffect SO 생성/업데이트 완료");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle"))
            AssetDatabase.CreateFolder("Assets/Scripts", "Battle");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Battle/Data"))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle", "Data");
        if (!AssetDatabase.IsValidFolder(BaseFolder))
            AssetDatabase.CreateFolder("Assets/Scripts/Battle/Data", "Curse");
    }

    private static StatusEffectSO CreateOrLoad(string fileName)
    {
        string path = $"{BaseFolder}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<StatusEffectSO>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // --- Stun ---

    private static void CreateStun()
    {
        var s = CreateOrLoad("StatusEffect_Stun");
        s.effectType = StatusEffectType.Stun;
        s.variantId = "Stun";
        s.physicalDuration = 1;
        s.magicalDuration = 1;              // 기본 1턴 스턴
        s.physicalDamagePerTurn = 0; // 스턴은 자체 피해 없음
        s.physicalApplyChance = 0.2f; // 예시: 물리 스킬 기본 20% (개별 스킬에서 보정 가능)
        s.magicalDamagePerTurn = 0;
        s.magicalApplyChance = 0.2f;  // 예시: 마법 스킬 기본 20%
        s.selfApplyChance = 0f;
        EditorUtility.SetDirty(s);
    }

    // --- Bleed1: 단검 출혈 (Max HP 1.5% / 턴, 2턴) ---

    private static void CreateBleed1_Dagger()
    {
        var s = CreateOrLoad("StatusEffect_Bleed1_Dagger");
        s.effectType = StatusEffectType.Bleed;
        s.variantId = "Bleed1_Dagger";
        s.physicalDuration = 2;
        s.magicalDuration = 2;                 // 2턴
        s.physicalDamagePerTurn = 0.015f; // MaxHP 1.5% / 턴
        s.physicalApplyChance = 0.35f;    // 예시: 물리 공격 기본 35% (스킬/무기에서 조정)
        s.magicalDamagePerTurn = 0.015f;  // 마법으로도 같은 수치 사용 가능
        s.magicalApplyChance = 0.35f;
        s.selfApplyChance = 0f;
        EditorUtility.SetDirty(s);
    }

    // --- Bleed2: 카타나 출혈 (Max HP 4% / 턴, 3턴) ---

    private static void CreateBleed2_Katana()
    {
        var s = CreateOrLoad("StatusEffect_Bleed2_Katana");
        s.effectType = StatusEffectType.Bleed;
        s.variantId = "Bleed2_Katana";
        s.physicalDuration = 3;
        s.magicalDuration = 3;                 // 3턴
        s.physicalDamagePerTurn = 0.04f; // MaxHP 4% / 턴
        s.physicalApplyChance = 0.3f;    // 예시 확률 (스킬별로 따로 보정 가능)
        s.magicalDamagePerTurn = 0.04f;
        s.magicalApplyChance = 0.3f;
        s.selfApplyChance = 0f;
        EditorUtility.SetDirty(s);
    }

    // --- Poison: 독 (Max HP 2% / 턴, 10턴 기본) ---

    private static void CreatePoison()
    {
        var s = CreateOrLoad("StatusEffect_Poison");
        s.effectType = StatusEffectType.Poison;
        s.variantId = "Poison";
        s.physicalDuration = 10;
        s.magicalDuration = 10;                 // 기본 10턴
        s.physicalDamagePerTurn = 0.02f; // MaxHP 2% / 턴
        s.physicalApplyChance = 0.4f;    // 예시: 무기 독 부여 40% (듀얼 시 ×0.35 적용 예정)
        s.magicalDamagePerTurn = 0.02f;
        s.magicalApplyChance = 0.5f;     // 예시: 마법 독은 조금 더 높게
        s.selfApplyChance = 0f;         // 필요 시 독 역효과에 사용
        EditorUtility.SetDirty(s);
    }

    // --- Ignite: 점화 (불 도트) ---
    // 무기용(물리): Max HP 3% / 턴, 2턴 (총 6%)
    // 마법용(마법): Max HP 5% / 턴, 2턴 (총 10%)
    private static void CreateIgnite()
    {
        var s = CreateOrLoad("StatusEffect_Ignite");
        s.effectType = StatusEffectType.Ignite;
        s.variantId = "Ignite";
        // 무기 점화: 2턴, 마법 점화: 3턴
        s.physicalDuration = 2;
        s.magicalDuration = 3;

        // 무기 점화(물리 기준)
        s.physicalDamagePerTurn = 0.03f; // MaxHP 3% / 턴
        s.physicalApplyChance = 0.25f;   // 기본 25% (무기/스킬에서 추가 보정 가능)

        // 마법 점화(스킬 기준)
        s.magicalDamagePerTurn = 0.05f;  // MaxHP 5% / 턴
        s.magicalApplyChance = 0.3f;     // 기본 30%

        // 역효과 등 시전자에게 점화를 거는 경우에만 사용 (기본은 0)
        s.selfApplyChance = 0f;

        EditorUtility.SetDirty(s);
    }
}
#endif

