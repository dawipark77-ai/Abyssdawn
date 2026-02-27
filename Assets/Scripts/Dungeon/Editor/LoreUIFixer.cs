using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Lore UI 계층 구조를 수정하는 에디터 도구
/// </summary>
public class LoreUIFixer : EditorWindow
{
    [MenuItem("Tools/Lore System/Fix UI Layer Order")]
    public static void FixUILayerOrder()
    {
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다!", "확인");
            return;
        }
        
        GameObject loreButton = FindGameObjectByName(canvas.transform, "Lore");
        GameObject loreTreePanel = FindGameObjectByName(canvas.transform, "LoreTree_Panel");
        
        if (loreButton == null || loreTreePanel == null)
        {
            EditorUtility.DisplayDialog("오류", "Lore 버튼 또는 LoreTree_Panel을 찾을 수 없습니다!", "확인");
            return;
        }
        
        // Lore 버튼을 LoreTree_Panel보다 뒤에 배치 (먼저 그려지도록)
        int panelIndex = loreTreePanel.transform.GetSiblingIndex();
        loreButton.transform.SetSiblingIndex(panelIndex - 1);
        
        Debug.Log($"[LoreUIFixer] Lore 버튼을 패널 앞으로 이동했습니다. (Index: {loreButton.transform.GetSiblingIndex()})");
        
        EditorUtility.DisplayDialog("완료!", "UI 계층 순서가 수정되었습니다!\n\n하지만 패널이 열렸을 때 Lore 버튼이 여전히 가려질 수 있습니다.\n\n대신 패널 내부에 Close 버튼을 사용하거나, 패널 배경을 클릭하면 닫히도록 하는 것을 권장합니다.", "확인");
    }
    
    [MenuItem("Tools/Lore System/Add Panel Background Button")]
    public static void AddPanelBackgroundButton()
    {
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다!", "확인");
            return;
        }
        
        GameObject loreTreePanel = FindGameObjectByName(canvas.transform, "LoreTree_Panel");
        if (loreTreePanel == null)
        {
            EditorUtility.DisplayDialog("오류", "LoreTree_Panel을 찾을 수 없습니다!", "확인");
            return;
        }
        
        // Background라는 이름의 자식 오브젝트 찾기
        Transform background = loreTreePanel.transform.Find("Background");
        
        if (background == null)
        {
            // Background가 없으면 생성
            GameObject bgObject = new GameObject("Background");
            bgObject.transform.SetParent(loreTreePanel.transform, false);
            bgObject.transform.SetAsFirstSibling(); // 맨 뒤에 배치
            
            // RectTransform 설정
            RectTransform bgRect = bgObject.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // Image 추가 (반투명 검정)
            Image bgImage = bgObject.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);
            
            background = bgObject.transform;
            Debug.Log("[LoreUIFixer] Background 오브젝트 생성됨");
        }
        
        // Button 컴포넌트 추가/가져오기
        Button bgButton = background.GetComponent<Button>();
        if (bgButton == null)
        {
            bgButton = background.gameObject.AddComponent<Button>();
            Debug.Log("[LoreUIFixer] Background에 Button 추가됨");
        }
        
        // LoreButtonController 찾기
        GameObject loreButton = FindGameObjectByName(canvas.transform, "Lore");
        if (loreButton != null)
        {
            LoreButtonController controller = loreButton.GetComponent<LoreButtonController>();
            if (controller != null)
            {
                // 배경 버튼 클릭 시 패널 닫기
                bgButton.onClick.RemoveAllListeners();
                bgButton.onClick.AddListener(() => controller.CloseLorePanel());
                
                Debug.Log("[LoreUIFixer] Background 버튼 클릭 이벤트 연결됨");
                
                EditorUtility.DisplayDialog("완료!", "패널 배경을 클릭하면 패널이 닫힙니다!\n\n배경의 투명도는 Background 오브젝트의 Image 컴포넌트에서 조정할 수 있습니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("경고", "LoreButtonController를 찾을 수 없습니다!\n\n먼저 'Setup Lore Button'을 실행하세요.", "확인");
            }
        }
    }
    
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
}






