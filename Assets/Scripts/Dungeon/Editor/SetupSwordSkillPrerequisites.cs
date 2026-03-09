using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// Sword Lore 스킬 SO들의 선행 스킬 정보를 자동으로 설정하는 에디터 도구
/// </summary>
public class SetupSwordSkillPrerequisites : EditorWindow
{
    [MenuItem("Tools/Skill Tree/Setup Sword Skill Prerequisites (SO)")]
    public static void SetupPrerequisites()
    {
        Debug.Log("[SetupSwordSkillPrerequisites] Sword 스킬 SO 선행 스킬 설정 시작...");
        
        // 1. 모든 Sword 스킬 SO 로드
        Dictionary<string, SkillData> skillMap = LoadAllSwordSkills();
        
        if (skillMap.Count == 0)
        {
            EditorUtility.DisplayDialog("오류", "Sword 스킬 SO를 찾을 수 없습니다!\n\nAssets/Resources/Skills/Sword_Lore/ 폴더를 확인하세요.", "확인");
            return;
        }
        
        Debug.Log($"[SetupSwordSkillPrerequisites] {skillMap.Count}개의 스킬 SO 로드 완료");
        
        // 2. 각 스킬 찾기
        SkillData basicSwordsmanship = FindSkill(skillMap, "basicswordsmanship");
        SkillData slash = FindSkill(skillMap, "slash");
        SkillData mandritto = FindSkill(skillMap, "mandritto");
        SkillData sharpEdge = FindSkill(skillMap, "sharpedge");
        SkillData combatBreathing = FindSkill(skillMap, "combatbreathing");
        SkillData strongSlash = FindSkill(skillMap, "strongslash");
        
        int successCount = 0;
        
        // 3. 선행 스킬 관계 설정
        
        // Basic Swordsmanship: 선행 스킬 없음 (루트)
        if (basicSwordsmanship != null)
        {
            SetPrerequisites(basicSwordsmanship, new List<SkillData>());
            Debug.Log($"[SetupSwordSkillPrerequisites] ✅ {basicSwordsmanship.skillName}: 선행 스킬 없음 (루트)");
            successCount++;
        }
        
        // Slash: Basic Swordsmanship 필요
        if (slash != null && basicSwordsmanship != null)
        {
            SetPrerequisites(slash, new List<SkillData> { basicSwordsmanship });
            Debug.Log($"[SetupSwordSkillPrerequisites] ✅ {slash.skillName}: {basicSwordsmanship.skillName} 필요");
            successCount++;
        }
        
        // Mandritto: Basic Swordsmanship 필요
        if (mandritto != null && basicSwordsmanship != null)
        {
            SetPrerequisites(mandritto, new List<SkillData> { basicSwordsmanship });
            Debug.Log($"[SetupSwordSkillPrerequisites] ✅ {mandritto.skillName}: {basicSwordsmanship.skillName} 필요");
            successCount++;
        }
        
        // Sharp Edge: Basic Swordsmanship 필요
        if (sharpEdge != null && basicSwordsmanship != null)
        {
            SetPrerequisites(sharpEdge, new List<SkillData> { basicSwordsmanship });
            Debug.Log($"[SetupSwordSkillPrerequisites] ✅ {sharpEdge.skillName}: {basicSwordsmanship.skillName} 필요");
            successCount++;
        }
        
        // Combat Breathing: Sharp Edge 필요 (2단계!)
        if (combatBreathing != null && sharpEdge != null)
        {
            SetPrerequisites(combatBreathing, new List<SkillData> { sharpEdge });
            Debug.Log($"[SetupSwordSkillPrerequisites] ✅ {combatBreathing.skillName}: {sharpEdge.skillName} 필요 (2단계)");
            successCount++;
        }
        
        // Strong Slash: Slash 필요 (2단계!)
        if (strongSlash != null && slash != null)
        {
            SetPrerequisites(strongSlash, new List<SkillData> { slash });
            Debug.Log($"[SetupSwordSkillPrerequisites] ✅ {strongSlash.skillName}: {slash.skillName} 필요 (2단계)");
            successCount++;
        }
        
        // 변경사항 저장
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog(
            "설정 완료!", 
            $"Sword 스킬 SO 선행 스킬 설정 완료!\n\n✓ {successCount}개 스킬 설정 완료\n\n스킬 트리 구조:\n" +
            "Basic Swordsmanship (루트)\n" +
            "├─ Slash\n" +
            "│  └─ Strong Slash\n" +
            "├─ Mandritto\n" +
            "└─ Sharp Edge\n" +
            "   └─ Combat Breathing\n\n" +
            "각 스킬 SO의 Inspector를 확인해보세요!", 
            "확인"
        );
    }
    
    /// <summary>
    /// 모든 Sword 스킬 SO 로드
    /// </summary>
    private static Dictionary<string, SkillData> LoadAllSwordSkills()
    {
        Dictionary<string, SkillData> skillMap = new Dictionary<string, SkillData>();
        
        // Resources/Skills/Sword_Lore 폴더에서 모든 SkillData 에셋 찾기
        string[] paths = new string[] 
        { 
            "Assets/Resources/Skills/Sword_Lore",
            "Assets/Scripts/Battle/Data/Skills/Sword_Lore"
        };
        
        foreach (string searchPath in paths)
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillData", new[] { searchPath });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                
                if (skill != null)
                {
                    // 스킬 이름으로 매핑 (공백 제거, 소문자)
                    string key = skill.skillName.Replace(" ", "").ToLower();
                    if (!skillMap.ContainsKey(key))
                    {
                        skillMap[key] = skill;
                        Debug.Log($"[SetupSwordSkillPrerequisites] 스킬 로드: {skill.skillName} ({path})");
                    }
                }
            }
        }
        
        return skillMap;
    }
    
    /// <summary>
    /// 스킬 찾기
    /// </summary>
    private static SkillData FindSkill(Dictionary<string, SkillData> skillMap, string key)
    {
        if (skillMap.ContainsKey(key))
        {
            return skillMap[key];
        }
        
        Debug.LogWarning($"[SetupSwordSkillPrerequisites] ⚠️ '{key}' 스킬을 찾을 수 없습니다!");
        return null;
    }
    
    /// <summary>
    /// 선행 스킬 설정
    /// </summary>
    private static void SetPrerequisites(SkillData skill, List<SkillData> prerequisites)
    {
        if (skill == null) return;
        
        SerializedObject so = new SerializedObject(skill);
        
        // prerequisiteSkills 설정
        SerializedProperty prereqProp = so.FindProperty("prerequisiteSkills");
        if (prereqProp != null)
        {
            prereqProp.ClearArray();
            prereqProp.arraySize = prerequisites.Count;
            
            for (int i = 0; i < prerequisites.Count; i++)
            {
                prereqProp.GetArrayElementAtIndex(i).objectReferenceValue = prerequisites[i];
            }
        }
        
        // requiredLorePoints 설정 (기본 1)
        SerializedProperty lpProp = so.FindProperty("requiredLorePoints");
        if (lpProp != null && lpProp.intValue == 0)
        {
            lpProp.intValue = 1;
        }
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(skill);
    }
    
    /// <summary>
    /// 모든 스킬 SO의 선행 스킬 정보 출력
    /// </summary>
    [MenuItem("Tools/Skill Tree/Show Sword Skill Prerequisites")]
    public static void ShowPrerequisites()
    {
        Dictionary<string, SkillData> skillMap = LoadAllSwordSkills();
        
        if (skillMap.Count == 0)
        {
            Debug.LogWarning("[SetupSwordSkillPrerequisites] 스킬 SO를 찾을 수 없습니다!");
            return;
        }
        
        Debug.Log("=== Sword 스킬 선행 스킬 정보 ===");
        
        foreach (var kvp in skillMap)
        {
            SkillData skill = kvp.Value;
            string prereqInfo = "없음 (루트)";
            
            if (skill.prerequisiteSkills != null && skill.prerequisiteSkills.Count > 0)
            {
                List<string> prereqNames = new List<string>();
                foreach (var prereq in skill.prerequisiteSkills)
                {
                    if (prereq != null)
                    {
                        prereqNames.Add(prereq.skillName);
                    }
                }
                prereqInfo = string.Join(", ", prereqNames);
            }
            
            Debug.Log($"[{skill.skillName}] LP: {skill.requiredLorePoints}, 선행 스킬: {prereqInfo}");
        }
    }
}













