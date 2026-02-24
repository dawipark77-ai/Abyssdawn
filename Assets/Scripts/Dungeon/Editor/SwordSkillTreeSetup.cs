using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// Sword Lore 스킬 트리를 자동으로 설정해주는 에디터 도구
/// </summary>
public class SwordSkillTreeSetup : EditorWindow
{
    [MenuItem("Tools/Skill Tree/Setup Sword Lore Tree")]
    public static void SetupSwordLoreTree()
    {
        Debug.Log("[SwordSkillTreeSetup] Sword Lore 스킬 트리 자동 설정 시작...");
        
        // 1. SwordLore 오브젝트 찾기
        GameObject swordLore = GameObject.Find("SwordLore");
        if (swordLore == null)
        {
            // Sword Lore 또는 Sword_Lore로도 검색
            swordLore = GameObject.Find("Sword Lore");
            if (swordLore == null)
            {
                swordLore = GameObject.Find("Sword_Lore");
            }
        }
        
        if (swordLore == null)
        {
            EditorUtility.DisplayDialog("오류", "SwordLore 오브젝트를 찾을 수 없습니다!\n\n하이라키에서 'SwordLore' 또는 'Sword Lore'를 찾아주세요.", "확인");
            return;
        }
        
        Debug.Log($"[SwordSkillTreeSetup] SwordLore 발견: {GetHierarchyPath(swordLore.transform)}");
        
        // 2. SwordSkillTreeManager 추가/가져오기
        SwordSkillTreeManager manager = swordLore.GetComponent<SwordSkillTreeManager>();
        if (manager == null)
        {
            manager = swordLore.AddComponent<SwordSkillTreeManager>();
            Debug.Log("[SwordSkillTreeSetup] SwordSkillTreeManager 추가됨");
        }
        
        // 3. PlayerStatData 로드
        PlayerStatData playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
        if (playerStatData != null)
        {
            SerializedObject managerSO = new SerializedObject(manager);
            managerSO.FindProperty("playerStatData").objectReferenceValue = playerStatData;
            managerSO.ApplyModifiedProperties();
            Debug.Log("[SwordSkillTreeSetup] PlayerStatData 연결 완료");
        }
        else
        {
            Debug.LogWarning("[SwordSkillTreeSetup] PlayerStatData를 찾을 수 없습니다.");
        }
        
        // 4. 스킬 SO 파일들 로드
        Dictionary<string, SkillData> skillDataMap = LoadAllSwordSkills();
        
        if (skillDataMap.Count == 0)
        {
            EditorUtility.DisplayDialog("오류", "스킬 SO 파일을 찾을 수 없습니다!\n\nAssets/Scripts/Battle/Data/Skills/Sword_Lore/ 폴더를 확인해주세요.", "확인");
            return;
        }
        
        Debug.Log($"[SwordSkillTreeSetup] {skillDataMap.Count}개의 스킬 SO 로드 완료");
        
        // 5. 스킬 노드들 찾기 및 설정
        int setupCount = SetupSkillNodes(swordLore.transform, skillDataMap);
        
        // 6. 선행 스킬 관계 설정
        SetupPrerequisites(swordLore.transform);
        
        // 변경사항 저장
        EditorUtility.SetDirty(manager);
        
        EditorUtility.DisplayDialog(
            "설정 완료!", 
            $"Sword Lore 스킬 트리 설정 완료!\n\n✓ {setupCount}개의 스킬 노드 연결\n✓ 선행 스킬 관계 설정\n✓ PlayerStatData 연결\n\n게임을 실행하여 테스트해보세요!", 
            "확인"
        );
    }
    
    /// <summary>
    /// 모든 Sword 스킬 SO 로드
    /// </summary>
    private static Dictionary<string, SkillData> LoadAllSwordSkills()
    {
        Dictionary<string, SkillData> skillMap = new Dictionary<string, SkillData>();
        
        // Sword_Lore 폴더에서 모든 SkillData 에셋 찾기
        string[] guids = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Scripts/Battle/Data/Skills/Sword_Lore" });
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            
            if (skill != null)
            {
                // 스킬 이름으로 매핑 (공백 제거)
                string key = skill.skillName.Replace(" ", "").ToLower();
                skillMap[key] = skill;
                Debug.Log($"[SwordSkillTreeSetup] 스킬 로드: {skill.skillName} ({skill.skillID})");
            }
        }
        
        return skillMap;
    }
    
    /// <summary>
    /// 스킬 노드들 설정
    /// </summary>
    private static int SetupSkillNodes(Transform parent, Dictionary<string, SkillData> skillDataMap)
    {
        int setupCount = 0;
        
        // 노드 이름과 스킬 매핑
        Dictionary<string, string> nodeNameToSkillKey = new Dictionary<string, string>
        {
            { "Basic Swordsmanship", "basicswordsmanship" },
            { "BasicSwordsmanship", "basicswordsmanship" },
            { "Basic_Swordsmanship", "basicswordsmanship" },
            
            { "Slash", "slash" },
            
            { "Mandritto", "mandritto" },
            
            { "Sharp Edge", "sharpedge" },
            { "SharpEdge", "sharpedge" },
            { "Sharp_Edge", "sharpedge" },
            
            { "Combat Breathing", "combatbreathing" },
            { "CombatBreathing", "combatbreathing" },
            { "Combat_Breathing", "combatbreathing" },
            
            { "Strong Slash", "strongslash" },
            { "StrongSlash", "strongslash" },
            { "Strong_Slash", "strongslash" }
        };
        
        // 모든 자식 오브젝트 검색
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            string nodeName = child.name;
            
            // 노드 이름으로 스킬 찾기
            if (nodeNameToSkillKey.ContainsKey(nodeName))
            {
                string skillKey = nodeNameToSkillKey[nodeName];
                
                if (skillDataMap.ContainsKey(skillKey))
                {
                    SkillData skill = skillDataMap[skillKey];
                    
                    // SkillTreeNode 추가/가져오기
                    SkillTreeNode node = child.GetComponent<SkillTreeNode>();
                    if (node == null)
                    {
                        node = child.gameObject.AddComponent<SkillTreeNode>();
                        Debug.Log($"[SwordSkillTreeSetup] SkillTreeNode 추가: {nodeName}");
                    }
                    
                    // 스킬 데이터 연결
                    SerializedObject nodeSO = new SerializedObject(node);
                    nodeSO.FindProperty("skillData").objectReferenceValue = skill;
                    nodeSO.FindProperty("requiredSkillPoints").intValue = 1;
                    
                    // UI 참조 자동 설정
                    Button button = child.GetComponent<Button>();
                    if (button != null)
                    {
                        nodeSO.FindProperty("nodeButton").objectReferenceValue = button;
                    }
                    
                    // Image 컴포넌트 찾기 (자신 또는 자식에서)
                    Image icon = child.GetComponent<Image>();
                    if (icon == null)
                    {
                        // 자식에서 찾기 (첫 번째 Image)
                        icon = child.GetComponentInChildren<Image>(true);
                    }
                    if (icon != null)
                    {
                        nodeSO.FindProperty("skillIcon").objectReferenceValue = icon;
                        Debug.Log($"[SwordSkillTreeSetup] Image 연결: {icon.gameObject.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[SwordSkillTreeSetup] {nodeName}에서 Image를 찾을 수 없습니다!");
                    }
                    
                    TextMeshProUGUI text = child.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (text != null)
                    {
                        nodeSO.FindProperty("skillNameText").objectReferenceValue = text;
                    }
                    
                    nodeSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(node);
                    
                    setupCount++;
                    Debug.Log($"[SwordSkillTreeSetup] ✅ {nodeName} → {skill.skillName} 연결 완료");
                }
            }
        }
        
        return setupCount;
    }
    
    /// <summary>
    /// 선행 스킬 관계 설정
    /// </summary>
    private static void SetupPrerequisites(Transform parent)
    {
        // 노드들 찾기
        SkillTreeNode basicSwordsmanship = FindNodeByName(parent, "Basic Swordsmanship", "BasicSwordsmanship", "Basic_Swordsmanship");
        SkillTreeNode slash = FindNodeByName(parent, "Slash");
        SkillTreeNode mandritto = FindNodeByName(parent, "Mandritto");
        SkillTreeNode sharpEdge = FindNodeByName(parent, "Sharp Edge", "SharpEdge", "Sharp_Edge");
        SkillTreeNode combatBreathing = FindNodeByName(parent, "Combat Breathing", "CombatBreathing", "Combat_Breathing");
        SkillTreeNode strongSlash = FindNodeByName(parent, "Strong Slash", "StrongSlash", "Strong_Slash");
        
        // 선행 스킬 설정
        // 기본 검술: 선행 스킬 없음 (루트)
        if (basicSwordsmanship != null)
        {
            SerializedObject so = new SerializedObject(basicSwordsmanship);
            so.FindProperty("prerequisiteNodes").ClearArray();
            so.ApplyModifiedProperties();
            Debug.Log("[SwordSkillTreeSetup] 기본 검술: 선행 스킬 없음 (루트)");
        }
        
        // 나머지 스킬들: 기본 검술이 선행 스킬
        SkillTreeNode[] tier1Skills = { slash, mandritto, sharpEdge, combatBreathing, strongSlash };
        
        foreach (var skill in tier1Skills)
        {
            if (skill != null && basicSwordsmanship != null)
            {
                SerializedObject so = new SerializedObject(skill);
                SerializedProperty prereqProp = so.FindProperty("prerequisiteNodes");
                prereqProp.ClearArray();
                prereqProp.arraySize = 1;
                prereqProp.GetArrayElementAtIndex(0).objectReferenceValue = basicSwordsmanship;
                so.ApplyModifiedProperties();
                
                Debug.Log($"[SwordSkillTreeSetup] {skill.name}: 선행 스킬 = 기본 검술");
            }
        }
    }
    
    /// <summary>
    /// 이름으로 SkillTreeNode 찾기 (여러 이름 시도)
    /// </summary>
    private static SkillTreeNode FindNodeByName(Transform parent, params string[] names)
    {
        foreach (string name in names)
        {
            Transform found = FindTransformByName(parent, name);
            if (found != null)
            {
                SkillTreeNode node = found.GetComponent<SkillTreeNode>();
                if (node != null)
                {
                    return node;
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// 이름으로 Transform 찾기 (재귀)
    /// </summary>
    private static Transform FindTransformByName(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        
        foreach (Transform child in parent)
        {
            Transform found = FindTransformByName(child, name);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    /// <summary>
    /// 하이라키 경로 가져오기
    /// </summary>
    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
    
    [MenuItem("Tools/Skill Tree/Find Sword Lore Nodes")]
    public static void FindSwordLoreNodes()
    {
        GameObject swordLore = GameObject.Find("SwordLore");
        if (swordLore == null)
        {
            swordLore = GameObject.Find("Sword Lore");
        }
        if (swordLore == null)
        {
            swordLore = GameObject.Find("Sword_Lore");
        }
        
        if (swordLore == null)
        {
            Debug.LogError("[SwordSkillTreeSetup] SwordLore 오브젝트를 찾을 수 없습니다!");
            return;
        }
        
        Debug.Log("=== Sword Lore 노드 검색 결과 ===");
        Debug.Log($"SwordLore: {GetHierarchyPath(swordLore.transform)}");
        
        // 모든 자식 오브젝트 출력
        foreach (Transform child in swordLore.GetComponentsInChildren<Transform>(true))
        {
            if (child != swordLore.transform)
            {
                Debug.Log($"- {child.name} ({GetHierarchyPath(child)})");
            }
        }
    }
    
    [MenuItem("Tools/Skill Tree/Refresh All Skill Icons")]
    public static void RefreshAllSkillIcons()
    {
        // SwordLore 오브젝트 찾기
        GameObject swordLore = GameObject.Find("SwordLore");
        if (swordLore == null)
        {
            swordLore = GameObject.Find("Sword Lore");
        }
        if (swordLore == null)
        {
            swordLore = GameObject.Find("Sword_Lore");
        }
        
        if (swordLore == null)
        {
            EditorUtility.DisplayDialog("오류", "SwordLore 오브젝트를 찾을 수 없습니다!", "확인");
            return;
        }
        
        // SwordSkillTreeManager 찾기
        SwordSkillTreeManager manager = swordLore.GetComponent<SwordSkillTreeManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("오류", "SwordSkillTreeManager가 없습니다!\n\n먼저 'Setup Sword Lore Tree'를 실행하세요.", "확인");
            return;
        }
        
        // 모든 아이콘 새로고침
        manager.RefreshAllIcons();
        
        EditorUtility.DisplayDialog("완료!", "모든 스킬 아이콘이 새로고침되었습니다!", "확인");
    }
}

