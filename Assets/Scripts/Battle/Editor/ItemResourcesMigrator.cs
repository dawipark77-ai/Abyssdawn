#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AbyssdawnBattle;

/// <summary>
/// Tools > Abyssdawn > Migrate Items to Resources
/// SO 파일을 Resources 서브폴더로 이동합니다.
///   장비  : Assets/Scripts/Battle/Data/Equipments  → Assets/Resources/Equipments
///   소비  : Assets/Scripts/Battle/Data/Items        → Assets/Resources/Items
/// </summary>
public static class ItemResourcesMigrator
{
    private const string SRC_EQUIPMENT  = "Assets/Scripts/Battle/Data/Equipments";
    private const string SRC_CONSUMABLE = "Assets/Scripts/Battle/Data/Items";
    private const string DST_EQUIPMENT  = "Assets/Resources/Item_Equipments/Equipments";
    private const string DST_CONSUMABLE = "Assets/Resources/Item_Equipments/Items";

    [MenuItem("Tools/Abyssdawn/Migrate Items to Resources", priority = 212)]
    public static void Migrate()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Item_Equipments");
        EnsureFolder("Assets/Resources/Item_Equipments", "Equipments");
        EnsureFolder("Assets/Resources/Item_Equipments", "Items");

        int eq  = MoveAssets<EquipmentData>   (SRC_EQUIPMENT,  DST_EQUIPMENT,  "t:EquipmentData");
        int con = MoveAssets<ConsumableItemSO> (SRC_CONSUMABLE, DST_CONSUMABLE, "t:ConsumableItemSO");

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Migrate Items to Resources",
            $"이동 완료!\n\n" +
            $"장비 SO        : {eq}개\n" +
            $"소비 아이템 SO : {con}개\n\n" +
            $"이동 경로\n" +
            $"  장비  → {DST_EQUIPMENT}\n" +
            $"  소비  → {DST_CONSUMABLE}\n\n" +
            "이미 이동된 파일은 건너뜁니다.",
            "확인");
    }

    private static int MoveAssets<T>(string srcFolder, string dstFolder, string typeFilter)
        where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(typeFilter, new[] { srcFolder });
        var seen = new System.Collections.Generic.HashSet<string>();
        int count = 0;

        foreach (string guid in guids)
        {
            if (!seen.Add(guid)) continue;

            string srcPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(srcPath)) continue;

            // 이미 대상 폴더에 있으면 건너뜀
            if (srcPath.StartsWith(dstFolder + "/")) continue;

            string fileName = System.IO.Path.GetFileName(srcPath);
            string dstPath  = dstFolder + "/" + fileName;

            // 목적지에 같은 이름이 있으면 건너뜀
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(dstPath)))
            {
                Debug.LogWarning($"[Migrator] 건너뜀 (이미 존재): {dstPath}");
                continue;
            }

            string result = AssetDatabase.MoveAsset(srcPath, dstPath);
            if (string.IsNullOrEmpty(result))
            {
                Debug.Log($"[Migrator] 이동 완료: {srcPath}  →  {dstPath}");
                count++;
            }
            else
            {
                Debug.LogError($"[Migrator] 이동 실패: {srcPath}  ({result})");
            }
        }

        return count;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, child);
            Debug.Log($"[Migrator] 폴더 생성: {full}");
        }
    }
}
#endif
