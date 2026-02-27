using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 스킬 노드에 Button을 자동으로 연결하는 도구
/// </summary>
public class ConnectSkillNodeButtons : EditorWindow
{
    [MenuItem("Tools/Connect Skill Node Buttons")]
    public static void ShowWindow()
    {
        GetWindow<ConnectSkillNodeButtons>("Connect Skill Node Buttons");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("모든 스킬 노드의 Button을 자동으로 찾아서 연결합니다.", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Auto Connect All Buttons", GUILayout.Height(40)))
        {
            AutoConnectButtons();
        }
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Show All Button Paths", GUILayout.Height(30)))
        {
            ShowAllButtonPaths();
        }
    }

    private void AutoConnectButtons()
    {
        Debug.Log("=== 버튼 자동 연결 시작 ===\n");
        
        // 모든 SkillTreeNode 찾기
        SkillTreeNode[] nodes = FindObjectsOfType<SkillTreeNode>(true);
        
        if (nodes.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "씬에서 SkillTreeNode를 찾을 수 없습니다!", "OK");
            return;
        }
        
        int connectedCount = 0;
        int failedCount = 0;
        
        foreach (var node in nodes)
        {
            SerializedObject so = new SerializedObject(node);
            SerializedProperty buttonProp = so.FindProperty("nodeButton");
            
            if (buttonProp == null)
            {
                Debug.LogError($"❌ {node.gameObject.name}: nodeButton 필드를 찾을 수 없습니다!");
                failedCount++;
                continue;
            }
            
            // 이미 연결되어 있으면 건너뛰기
            if (buttonProp.objectReferenceValue != null)
            {
                Debug.Log($"⏭️ {node.gameObject.name}: 이미 Button이 연결되어 있습니다 - {buttonProp.objectReferenceValue.name}");
                continue;
            }
            
            // Button 찾기 (여러 방법 시도)
            Button foundButton = FindButton(node.gameObject);
            
            if (foundButton != null)
            {
                buttonProp.objectReferenceValue = foundButton;
                so.ApplyModifiedProperties();
                Debug.Log($"✅ {node.gameObject.name}: Button 연결 성공 - {foundButton.gameObject.name}");
                connectedCount++;
            }
            else
            {
                Debug.LogError($"❌ {node.gameObject.name}: Button을 찾을 수 없습니다!");
                failedCount++;
            }
        }
        
        Debug.Log($"\n=== 연결 완료 ===");
        Debug.Log($"성공: {connectedCount}개 | 실패: {failedCount}개 | 건너뜀: {nodes.Length - connectedCount - failedCount}개");
        
        EditorUtility.DisplayDialog(
            "완료!",
            $"Button 자동 연결 완료\n\n" +
            $"• 성공: {connectedCount}개\n" +
            $"• 실패: {failedCount}개\n" +
            $"• 건너뜀: {nodes.Length - connectedCount - failedCount}개",
            "OK"
        );
    }

    /// <summary>
    /// Button을 찾는 여러 방법 시도
    /// </summary>
    private Button FindButton(GameObject root)
    {
        Button button = null;
        
        // 방법 1: 자신에게서 찾기
        button = root.GetComponent<Button>();
        if (button != null)
        {
            Debug.Log($"  → 방법 1 성공: 자신에게서 발견 - {button.gameObject.name}");
            return button;
        }
        
        // 방법 2: 직계 자식에서 찾기 (활성화된 것만)
        button = root.GetComponentInChildren<Button>(false);
        if (button != null)
        {
            Debug.Log($"  → 방법 2 성공: 활성화된 자식에서 발견 - {button.gameObject.name}");
            return button;
        }
        
        // 방법 3: 모든 자식에서 찾기 (비활성화된 것도 포함)
        button = root.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            Debug.Log($"  → 방법 3 성공: 비활성화된 자식에서도 발견 - {button.gameObject.name}");
            return button;
        }
        
        // 방법 4: 특정 이름 패턴으로 찾기
        string[] possibleNames = { "Button", root.name, root.name + " (Button)" };
        foreach (string name in possibleNames)
        {
            Transform child = root.transform.Find(name);
            if (child != null)
            {
                button = child.GetComponent<Button>();
                if (button != null)
                {
                    Debug.Log($"  → 방법 4 성공: 이름으로 발견 - {button.gameObject.name}");
                    return button;
                }
            }
        }
        
        // 방법 5: 모든 자식을 순회하며 찾기
        Button[] allButtons = root.GetComponentsInChildren<Button>(true);
        if (allButtons.Length > 0)
        {
            Debug.Log($"  → 방법 5 성공: 첫 번째 Button 사용 - {allButtons[0].gameObject.name}");
            return allButtons[0];
        }
        
        Debug.LogWarning($"  → 모든 방법 실패: Button을 찾을 수 없음");
        return null;
    }

    private void ShowAllButtonPaths()
    {
        Debug.Log("=== 모든 Button 경로 표시 ===\n");
        
        // SwordLore 아래의 모든 Button 찾기
        GameObject swordLore = GameObject.Find("SwordLore");
        if (swordLore == null)
        {
            Debug.LogError("SwordLore GameObject를 찾을 수 없습니다!");
            return;
        }
        
        Button[] allButtons = swordLore.GetComponentsInChildren<Button>(true);
        
        Debug.Log($"총 {allButtons.Length}개의 Button 발견:\n");
        
        foreach (var btn in allButtons)
        {
            string path = GetGameObjectPath(btn.gameObject);
            Debug.Log($"  • {path}");
            Debug.Log($"    - Active: {btn.gameObject.activeSelf}");
            Debug.Log($"    - Interactable: {btn.interactable}\n");
        }
        
        Debug.Log("=== 표시 완료 ===");
        
        EditorUtility.DisplayDialog("완료", $"{allButtons.Length}개의 Button 경로를 Console에 표시했습니다.", "OK");
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}





