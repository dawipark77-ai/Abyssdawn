using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Lore 버튼과 LoreTree_Panel을 자동으로 연결해주는 에디터 도구
/// </summary>
public class LoreSystemSetup : EditorWindow
{
    [MenuItem("Tools/Lore System/Setup Lore Button")]
    public static void SetupLoreButton()
    {
        Debug.Log("[LoreSystemSetup] Lore 버튼 자동 설정 시작...");
        
        // Canvas 찾기
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다!", "확인");
            return;
        }
        
        // Lore 버튼 찾기
        GameObject loreButton = FindGameObjectByName(canvas.transform, "Lore");
        if (loreButton == null)
        {
            EditorUtility.DisplayDialog("오류", "Lore 버튼을 찾을 수 없습니다!\n\n하이라키에서 'Lore'라는 이름의 GameObject를 찾아주세요.", "확인");
            return;
        }
        
        // LoreTree_Panel 찾기
        GameObject loreTreePanel = FindGameObjectByName(canvas.transform, "LoreTree_Panel");
        if (loreTreePanel == null)
        {
            EditorUtility.DisplayDialog("오류", "LoreTree_Panel을 찾을 수 없습니다!\n\n하이라키에서 'LoreTree_Panel'이라는 이름의 GameObject를 찾아주세요.", "확인");
            return;
        }
        
        // 1. Lore 버튼에 LoreButtonController 추가/가져오기
        LoreButtonController loreController = loreButton.GetComponent<LoreButtonController>();
        if (loreController == null)
        {
            loreController = loreButton.AddComponent<LoreButtonController>();
            Debug.Log("[LoreSystemSetup] LoreButtonController 추가됨");
        }
        else
        {
            Debug.Log("[LoreSystemSetup] LoreButtonController 이미 존재함");
        }
        
        // 2. LoreButtonController 설정
        SerializedObject so = new SerializedObject(loreController);
        so.FindProperty("loreButton").objectReferenceValue = loreButton.GetComponent<Button>();
        so.FindProperty("loreTreePanel").objectReferenceValue = loreTreePanel;
        so.ApplyModifiedProperties();
        
        Debug.Log("[LoreSystemSetup] LoreButtonController 연결 완료");
        
        // 3. LoreTree_Panel에 LoreTreePanelController 추가/가져오기
        LoreTreePanelController panelController = loreTreePanel.GetComponent<LoreTreePanelController>();
        if (panelController == null)
        {
            panelController = loreTreePanel.AddComponent<LoreTreePanelController>();
            Debug.Log("[LoreSystemSetup] LoreTreePanelController 추가됨");
        }
        else
        {
            Debug.Log("[LoreSystemSetup] LoreTreePanelController 이미 존재함");
        }
        
        // 4. Close 버튼 찾기 (선택적)
        GameObject closeButton = FindGameObjectByName(loreTreePanel.transform, "CloseButton");
        if (closeButton == null) closeButton = FindGameObjectByName(loreTreePanel.transform, "Close");
        if (closeButton == null) closeButton = FindGameObjectByName(loreTreePanel.transform, "X");
        if (closeButton == null) closeButton = FindGameObjectByName(loreTreePanel.transform, "Btn_Close");
        
        if (closeButton != null && closeButton.GetComponent<Button>() != null)
        {
            SerializedObject panelSO = new SerializedObject(panelController);
            panelSO.FindProperty("closeButton").objectReferenceValue = closeButton.GetComponent<Button>();
            panelSO.ApplyModifiedProperties();
            Debug.Log($"[LoreSystemSetup] Close 버튼 연결 완료: {closeButton.name}");
        }
        else
        {
            Debug.LogWarning("[LoreSystemSetup] Close 버튼을 찾을 수 없습니다. 수동으로 연결해주세요.");
        }
        
        // 5. LoreTree_Panel 초기 상태를 비활성화로 설정
        loreTreePanel.SetActive(false);
        
        // 변경사항 저장
        EditorUtility.SetDirty(loreController);
        EditorUtility.SetDirty(panelController);
        EditorUtility.SetDirty(loreTreePanel);
        
        EditorUtility.DisplayDialog(
            "설정 완료!", 
            "Lore 버튼과 LoreTree_Panel이 성공적으로 연결되었습니다!\n\n✓ Lore 버튼 클릭 → LoreTree_Panel 열림\n✓ 다시 클릭 → 패널 닫힘\n✓ ESC 키로도 닫을 수 있습니다", 
            "확인"
        );
    }
    
    /// <summary>
    /// 이름으로 GameObject 찾기 (재귀적으로 모든 자식 검색)
    /// </summary>
    private static GameObject FindGameObjectByName(Transform parent, string name)
    {
        if (parent.name == name)
            return parent.gameObject;
        
        foreach (Transform child in parent)
        {
            GameObject found = FindGameObjectByName(child, name);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    [MenuItem("Tools/Lore System/Find Lore Objects")]
    public static void FindLoreObjects()
    {
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LoreSystemSetup] Canvas를 찾을 수 없습니다!");
            return;
        }
        
        GameObject loreButton = FindGameObjectByName(canvas.transform, "Lore");
        GameObject loreTreePanel = FindGameObjectByName(canvas.transform, "LoreTree_Panel");
        
        Debug.Log("=== Lore System 오브젝트 검색 결과 ===");
        Debug.Log($"Canvas: {(canvas != null ? "✓ 발견" : "✗ 없음")}");
        Debug.Log($"Lore 버튼: {(loreButton != null ? $"✓ 발견 - {GetHierarchyPath(loreButton.transform)}" : "✗ 없음")}");
        Debug.Log($"LoreTree_Panel: {(loreTreePanel != null ? $"✓ 발견 - {GetHierarchyPath(loreTreePanel.transform)}" : "✗ 없음")}");
        
        // LoreButtonController 확인
        if (loreButton != null)
        {
            LoreButtonController controller = loreButton.GetComponent<LoreButtonController>();
            Debug.Log($"LoreButtonController: {(controller != null ? "✓ 있음" : "✗ 없음")}");
            
            if (controller != null)
            {
                Debug.Log($"  - loreButton 연결: {(controller.loreButton != null ? "✓" : "✗")}");
                Debug.Log($"  - loreTreePanel 연결: {(controller.loreTreePanel != null ? "✓" : "✗")}");
            }
            
            Selection.activeGameObject = loreButton;
        }
    }
    
    [MenuItem("Tools/Lore System/Validate Lore Setup")]
    public static void ValidateLoreSetup()
    {
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("검증 실패", "❌ Canvas를 찾을 수 없습니다!", "확인");
            return;
        }
        
        GameObject loreButton = FindGameObjectByName(canvas.transform, "Lore");
        GameObject loreTreePanel = FindGameObjectByName(canvas.transform, "LoreTree_Panel");
        
        string report = "=== Lore System 검증 결과 ===\n\n";
        bool allGood = true;
        int warningCount = 0;
        
        // 1. Lore 버튼 확인
        if (loreButton == null)
        {
            report += "❌ Lore 버튼을 찾을 수 없습니다!\n";
            allGood = false;
        }
        else
        {
            report += $"✅ Lore 버튼 발견: {loreButton.name}\n";
            
            // Button 컴포넌트 확인
            Button btn = loreButton.GetComponent<Button>();
            if (btn == null)
            {
                report += "   ❌ Button 컴포넌트가 없습니다!\n";
                allGood = false;
            }
            else
            {
                report += "   ✅ Button 컴포넌트 있음\n";
            }
            
            // LoreButtonController 확인
            LoreButtonController controller = loreButton.GetComponent<LoreButtonController>();
            if (controller == null)
            {
                report += "   ❌ LoreButtonController가 없습니다!\n";
                allGood = false;
            }
            else
            {
                report += "   ✅ LoreButtonController 있음\n";
                
                if (controller.loreButton == null)
                {
                    report += "      ⚠️ loreButton 필드가 비어있습니다 (자동 검색됨)\n";
                    warningCount++;
                }
                else
                {
                    report += "      ✅ loreButton 필드 연결됨\n";
                    
                    // 버튼이 자기 자신을 참조하는지 확인
                    if (controller.loreButton.gameObject != loreButton)
                    {
                        report += $"      ⚠️ loreButton이 자기 자신이 아닌 다른 버튼을 참조: {controller.loreButton.gameObject.name}\n";
                        warningCount++;
                    }
                    
                    // 버튼 Interactable 확인
                    if (!controller.loreButton.interactable)
                    {
                        report += "      ⚠️ loreButton이 비활성화되어 있습니다! (Interactable = false)\n";
                        warningCount++;
                    }
                    else
                    {
                        report += "      ✅ loreButton Interactable = true\n";
                    }
                }
                
                if (controller.loreTreePanel == null)
                {
                    report += "      ❌ loreTreePanel 필드가 비어있습니다!\n";
                    allGood = false;
                }
                else
                {
                    report += "      ✅ loreTreePanel 필드 연결됨\n";
                    
                    // 패널이 올바른 오브젝트를 참조하는지 확인
                    if (controller.loreTreePanel != loreTreePanel)
                    {
                        report += $"      ⚠️ loreTreePanel이 다른 오브젝트를 참조: {controller.loreTreePanel.name}\n";
                        warningCount++;
                    }
                }
            }
        }
        
        // 2. LoreTree_Panel 확인
        report += "\n";
        if (loreTreePanel == null)
        {
            report += "❌ LoreTree_Panel을 찾을 수 없습니다!\n";
            allGood = false;
        }
        else
        {
            report += $"✅ LoreTree_Panel 발견: {loreTreePanel.name}\n";
            report += $"   현재 상태: {(loreTreePanel.activeSelf ? "활성화됨 (게임 시작 시 닫히도록 설정해야 함)" : "비활성화됨 ✅")}\n";
        }
        
        Debug.Log(report);
        
        if (allGood && warningCount == 0)
        {
            EditorUtility.DisplayDialog("검증 성공!", "✅ Lore 시스템이 완벽하게 설정되었습니다!\n\n게임을 실행하여 테스트해보세요.\n\n게임 실행 시 콘솔에서 다음 메시지를 확인하세요:\n- [LoreButtonController] ✅ Lore 버튼 클릭 이벤트 연결 완료!\n- [LoreButtonController] 🔘 버튼 클릭 감지!", "확인");
        }
        else if (allGood)
        {
            EditorUtility.DisplayDialog("검증 완료 (경고 있음)", $"⚠️ Lore 시스템이 작동은 하지만 {warningCount}개의 경고가 있습니다.\n\n콘솔 창에서 자세한 내용을 확인하세요.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("검증 실패", "❌ Lore 시스템 설정에 문제가 있습니다.\n\n콘솔 창에서 자세한 내용을 확인하세요.\n\n'Tools > Lore System > Setup Lore Button'을 실행하여 자동 설정을 시도해보세요.", "확인");
        }
    }
    
    [MenuItem("Tools/Lore System/Test Button Click")]
    public static void TestButtonClick()
    {
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다!", "확인");
            return;
        }
        
        GameObject loreButton = FindGameObjectByName(canvas.transform, "Lore");
        if (loreButton == null)
        {
            EditorUtility.DisplayDialog("오류", "Lore 버튼을 찾을 수 없습니다!", "확인");
            return;
        }
        
        LoreButtonController controller = loreButton.GetComponent<LoreButtonController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog("오류", "LoreButtonController가 없습니다!", "확인");
            return;
        }
        
        // 버튼 클릭 테스트 (에디터 모드에서는 작동하지 않을 수 있음)
        Button btn = loreButton.GetComponent<Button>();
        if (btn != null)
        {
            Debug.Log("[LoreSystemSetup] 버튼 클릭 테스트 시작...");
            btn.onClick.Invoke();
            Debug.Log("[LoreSystemSetup] 버튼 클릭 테스트 완료!");
            
            EditorUtility.DisplayDialog("테스트 완료", "버튼 클릭 이벤트가 실행되었습니다.\n\n콘솔 창에서 로그를 확인하세요.\n\n주의: 에디터 모드에서는 일부 기능이 작동하지 않을 수 있습니다. 게임을 실행하여 테스트하세요.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("오류", "Button 컴포넌트를 찾을 수 없습니다!", "확인");
        }
    }
    
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
}


