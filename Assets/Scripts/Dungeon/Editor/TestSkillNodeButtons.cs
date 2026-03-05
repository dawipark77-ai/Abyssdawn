using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// 스킬 노드 버튼이 제대로 설정되어 있는지 검사하는 도구
/// </summary>
public class TestSkillNodeButtons : EditorWindow
{
    [MenuItem("Tools/Test Skill Node Buttons")]
    public static void ShowWindow()
    {
        GetWindow<TestSkillNodeButtons>("Test Skill Node Buttons");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("모든 스킬 노드 버튼의 상태를 검사합니다.", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Check All Skill Nodes", GUILayout.Height(40)))
        {
            CheckAllNodes();
        }
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Reconnect All Button Events", GUILayout.Height(30)))
        {
            ReconnectAllButtonEvents();
        }
    }

    private void CheckAllNodes()
    {
        Debug.Log("=== 스킬 노드 버튼 검사 시작 ===");
        
        SkillTreeNode[] nodes = FindObjectsOfType<SkillTreeNode>(true);
        
        if (nodes.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "씬에서 SkillTreeNode를 찾을 수 없습니다!", "OK");
            return;
        }
        
        foreach (var node in nodes)
        {
            Debug.Log($"\n--- {node.gameObject.name} 검사 중... ---");
            
            // 1. GameObject 활성 상태 확인
            Debug.Log($"  GameObject Active: {node.gameObject.activeSelf}");
            Debug.Log($"  GameObject ActiveInHierarchy: {node.gameObject.activeInHierarchy}");
            
            // 2. SkillTreeNode 컴포넌트 활성 상태
            Debug.Log($"  SkillTreeNode Enabled: {node.enabled}");
            
            // 3. Button 컴포넌트 확인
            SerializedObject so = new SerializedObject(node);
            SerializedProperty buttonProp = so.FindProperty("nodeButton");
            
            if (buttonProp != null && buttonProp.objectReferenceValue != null)
            {
                Button btn = buttonProp.objectReferenceValue as Button;
                Debug.Log($"  ✅ Button 연결: {btn.gameObject.name}");
                Debug.Log($"     - Interactable: {btn.interactable}");
                Debug.Log($"     - Navigation: {btn.navigation.mode}");
                Debug.Log($"     - Persistent Event Count: {btn.onClick.GetPersistentEventCount()}");
                Debug.Log($"     - Button GameObject Active: {btn.gameObject.activeSelf}");
            }
            else
            {
                Debug.LogError($"  ❌ Button이 연결되지 않음!");
            }
            
            // 4. SkillData 확인
            SerializedProperty dataProp = so.FindProperty("skillData");
            if (dataProp != null && dataProp.objectReferenceValue != null)
            {
                Debug.Log($"  ✅ SkillData 연결: {dataProp.objectReferenceValue.name}");
            }
            else
            {
                Debug.LogError($"  ❌ SkillData가 연결되지 않음!");
            }
            
            // 5. SkillDetailPopup 확인
            SerializedProperty popupProp = so.FindProperty("skillDetailPopup");
            if (popupProp != null && popupProp.objectReferenceValue != null)
            {
                Debug.Log($"  ✅ SkillDetailPopup 연결: {popupProp.objectReferenceValue.name}");
            }
            else
            {
                Debug.LogError($"  ❌ SkillDetailPopup이 연결되지 않음!");
            }
        }
        
        Debug.Log("\n=== 검사 완료 ===");
        
        EditorUtility.DisplayDialog(
            "검사 완료",
            $"총 {nodes.Length}개의 스킬 노드를 검사했습니다.\n\n" +
            "Console 창에서 자세한 결과를 확인하세요.",
            "OK"
        );
    }

    private void ReconnectAllButtonEvents()
    {
        Debug.Log("=== 버튼 이벤트 재연결 시작 ===");
        
        SkillTreeNode[] nodes = FindObjectsOfType<SkillTreeNode>(true);
        
        if (nodes.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "씬에서 SkillTreeNode를 찾을 수 없습니다!", "OK");
            return;
        }
        
        int reconnectedCount = 0;
        
        foreach (var node in nodes)
        {
            SerializedObject so = new SerializedObject(node);
            SerializedProperty buttonProp = so.FindProperty("nodeButton");
            
            if (buttonProp != null && buttonProp.objectReferenceValue != null)
            {
                Button btn = buttonProp.objectReferenceValue as Button;
                
                // Persistent Event를 수동으로 추가
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    btn.onClick,
                    new UnityEngine.Events.UnityAction(() => {
                        Debug.Log($"Button clicked via Editor: {node.gameObject.name}");
                    })
                );
                
                Debug.Log($"✅ {node.gameObject.name}: 버튼 이벤트 재연결 완료");
                reconnectedCount++;
            }
        }
        
        Debug.Log($"=== 재연결 완료: {reconnectedCount}개 ===");
        
        EditorUtility.DisplayDialog(
            "재연결 완료",
            $"{reconnectedCount}개의 버튼 이벤트를 재연결했습니다.",
            "OK"
        );
    }
}










