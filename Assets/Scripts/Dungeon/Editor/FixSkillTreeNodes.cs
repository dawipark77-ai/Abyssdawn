using UnityEngine;
using UnityEditor;

/// <summary>
/// 스킬 트리 노드의 Missing Script 제거 및 팝업 연결 도구
/// </summary>
public class FixSkillTreeNodes : EditorWindow
{
    [MenuItem("Tools/Fix Skill Tree Nodes")]
    public static void ShowWindow()
    {
        GetWindow<FixSkillTreeNodes>("Fix Skill Tree Nodes");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("스킬 트리 노드의 Missing Script를 제거하고 SkillDetailPopup을 자동으로 연결합니다.", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Fix All Skill Tree Nodes", GUILayout.Height(40)))
        {
            FixAllNodes();
        }
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Remove Missing Scripts Only", GUILayout.Height(30)))
        {
            RemoveMissingScriptsOnly();
        }
    }

    private void FixAllNodes()
    {
        // SkillDetailPopup 찾기
        SkillDetailPopup popup = FindObjectOfType<SkillDetailPopup>(true);
        
        if (popup == null)
        {
            EditorUtility.DisplayDialog("Error", "씬에서 SkillDetailPopup을 찾을 수 없습니다!", "OK");
            return;
        }
        
        // 모든 SkillTreeNode 찾기
        SkillTreeNode[] nodes = FindObjectsOfType<SkillTreeNode>(true);
        
        if (nodes.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "씬에서 SkillTreeNode를 찾을 수 없습니다!", "OK");
            return;
        }
        
        int fixedCount = 0;
        int removedCount = 0;
        
        foreach (var node in nodes)
        {
            // Missing Script 제거
            int removed = RemoveMissingScripts(node.gameObject);
            removedCount += removed;
            
            // SkillDetailPopup 연결
            SerializedObject so = new SerializedObject(node);
            SerializedProperty popupProp = so.FindProperty("skillDetailPopup");
            
            if (popupProp != null && popupProp.objectReferenceValue == null)
            {
                popupProp.objectReferenceValue = popup;
                so.ApplyModifiedProperties();
                fixedCount++;
                Debug.Log($"[FixSkillTreeNodes] ✅ {node.gameObject.name}: SkillDetailPopup 연결 완료");
            }
        }
        
        EditorUtility.DisplayDialog(
            "완료!",
            $"총 {nodes.Length}개의 노드를 확인했습니다.\n\n" +
            $"• Missing Script 제거: {removedCount}개\n" +
            $"• SkillDetailPopup 연결: {fixedCount}개",
            "OK"
        );
        
        Debug.Log($"[FixSkillTreeNodes] 🎉 작업 완료! Missing Script: {removedCount}개 제거, 팝업 연결: {fixedCount}개");
    }

    private void RemoveMissingScriptsOnly()
    {
        // 모든 SkillTreeNode 찾기
        SkillTreeNode[] nodes = FindObjectsOfType<SkillTreeNode>(true);
        
        if (nodes.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "씬에서 SkillTreeNode를 찾을 수 없습니다!", "OK");
            return;
        }
        
        int totalRemoved = 0;
        
        foreach (var node in nodes)
        {
            int removed = RemoveMissingScripts(node.gameObject);
            totalRemoved += removed;
        }
        
        EditorUtility.DisplayDialog(
            "완료!",
            $"총 {totalRemoved}개의 Missing Script를 제거했습니다.",
            "OK"
        );
        
        Debug.Log($"[FixSkillTreeNodes] 🎉 Missing Script {totalRemoved}개 제거 완료!");
    }

    /// <summary>
    /// GameObject에서 Missing Script 제거
    /// </summary>
    private int RemoveMissingScripts(GameObject go)
    {
        int removedCount = 0;
        var components = go.GetComponents<Component>();
        
        SerializedObject so = new SerializedObject(go);
        var prop = so.FindProperty("m_Component");
        
        for (int i = components.Length - 1; i >= 0; i--)
        {
            if (components[i] == null)
            {
                prop.DeleteArrayElementAtIndex(i);
                removedCount++;
                Debug.Log($"[FixSkillTreeNodes] ❌ {go.name}: Missing Script 제거");
            }
        }
        
        so.ApplyModifiedProperties();
        
        return removedCount;
    }
}















