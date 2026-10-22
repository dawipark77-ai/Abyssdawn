using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [2026-10-21] Resources/SaveAssetRegistry.asset 을 채운다 — 저장 파일이 이름으로 찾는 에셋 종류 전부.
/// 메뉴로 직접 돌릴 수도 있고, .asset 이 추가·삭제·이동되면 자동으로 다시 채운다.
/// </summary>
public class SaveAssetRegistryBuilder : AssetPostprocessor
{
    private const string AssetPath = "Assets/Resources/SaveAssetRegistry.asset";
    private static readonly string[] TypeFilters =
    {
        "t:MemoryOfSpeciesData", "t:SkillData", "t:PathSkillData", "t:OathBindingData",
        "t:CharacterClass", "t:EquipmentData", "t:DispositionData",
    };
    private static bool _queued;

    [MenuItem("Tools/Abyssdawn/Refresh Save Asset Registry")]
    public static void Refresh()
    {
        var reg = AssetDatabase.LoadAssetAtPath<SaveAssetRegistry>(AssetPath);
        if (reg == null)
        {
            reg = ScriptableObject.CreateInstance<SaveAssetRegistry>();
            AssetDatabase.CreateAsset(reg, AssetPath);
        }
        var list = new List<ScriptableObject>();
        var seen = new HashSet<string>();
        foreach (var filter in TypeFilters)
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".asset") || path.Contains("/_Recovery/") || !seen.Add(guid)) continue;
                var so = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
                if (so != null) list.Add(so);
            }
        list.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
        bool same = reg.assets.Count == list.Count;
        for (int i = 0; same && i < list.Count; i++) same = reg.assets[i] == list[i];
        if (same) return;
        reg.assets = list;
        EditorUtility.SetDirty(reg);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SaveAssetRegistry] 저장용 에셋 {list.Count}개 등록");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (_queued) return;
        bool relevant = false;
        foreach (var p in imported) if (p.EndsWith(".asset") && p != AssetPath) { relevant = true; break; }
        if (!relevant) foreach (var p in deleted) if (p.EndsWith(".asset")) { relevant = true; break; }
        if (!relevant) foreach (var p in moved) if (p.EndsWith(".asset")) { relevant = true; break; }
        if (!relevant) return;
        _queued = true;
        EditorApplication.delayCall += () => { _queued = false; if (!EditorApplication.isPlayingOrWillChangePlaymode) Refresh(); };
    }
}
