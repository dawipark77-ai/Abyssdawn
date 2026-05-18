using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬 전용 — Sword Lore T1 스킬 SO 5종. (Resources 밖 경로이므로 에디터에선 AssetDatabase로 로드)
    /// </summary>
    public static class DungeonSimSwordLoreT1SkillRegistry
    {
        public const string AssetPathBasicSwordsmanship =
            "Assets/Scripts/Battle/Data/Skills/Sword_Lore/T1_BasicSwordsmanship.asset";
        public const string AssetPathMandritto =
            "Assets/Scripts/Battle/Data/Skills/Sword_Lore/T1_Mandritto.asset";
        public const string AssetPathSharpEdge =
            "Assets/Scripts/Battle/Data/Skills/Sword_Lore/T1_SharpEdge.asset";
        public const string AssetPathSlash =
            "Assets/Scripts/Battle/Data/Skills/Sword_Lore/T1_Slash.asset";
        public const string AssetPathStrongSlash =
            "Assets/Scripts/Battle/Data/Skills/Sword_Lore/T1_Strong Slash.asset";

        private static readonly string[] OrderedEditorPaths =
        {
            AssetPathBasicSwordsmanship,
            AssetPathMandritto,
            AssetPathSharpEdge,
            AssetPathSlash,
            AssetPathStrongSlash
        };

        private static SkillData[] _cachedOrdered;

        /// <summary>표시·습득 순서: Basic → Mandritto → SharpEdge → Slash → Strong Slash.</summary>
        public static SkillData[] GetOrderedT1Skills()
        {
            if (_cachedOrdered != null)
                return _cachedOrdered;

            var list = new List<SkillData>(OrderedEditorPaths.Length);
#if UNITY_EDITOR
            foreach (var p in OrderedEditorPaths)
            {
                var s = AssetDatabase.LoadAssetAtPath<SkillData>(p);
                if (s != null)
                    list.Add(s);
            }
#else
            // 빌드 런타임: Resources 미배치 시 비움. 필요 시 동일 GUID를 Resources 하위로 복제해 Load로 확장 가능.
#endif
            _cachedOrdered = list.ToArray();
            return _cachedOrdered;
        }

#if UNITY_EDITOR
        [MenuItem("Abyssdawn/Simulation/Clear Sword T1 Skill Registry Cache")]
        private static void ClearCacheMenu()
        {
            _cachedOrdered = null;
            Debug.Log("[DungeonSimSwordLoreT1SkillRegistry] Cache cleared.");
        }
#endif
    }
}
