using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SkillDetail_Popup을 자동으로 설정해주는 에디터 도구
/// </summary>
public class SkillDetailPopupSetup : EditorWindow
{
    [MenuItem("Tools/Skill Tree/Setup Skill Detail Popup")]
    public static void SetupSkillDetailPopup()
    {
        Debug.Log("[SkillDetailPopupSetup] SkillDetail_Popup 자동 설정 시작...");
        
        // Canvas 찾기
        Canvas canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "Canvas를 찾을 수 없습니다!", "확인");
            return;
        }
        
        // SkillDetail_Popup 찾기
        GameObject popup = FindGameObjectByName(canvas.transform, "SkillDetail_Popup");
        
        if (popup == null)
        {
            // 다른 이름으로도 검색
            popup = FindGameObjectByName(canvas.transform, "SkillDetailPopup");
            if (popup == null)
            {
                popup = FindGameObjectByName(canvas.transform, "Skill_Detail_Popup");
            }
        }
        
        if (popup == null)
        {
            EditorUtility.DisplayDialog("오류", "SkillDetail_Popup을 찾을 수 없습니다!\n\n하이라키에서 'SkillDetail_Popup' 또는 'SkillDetailPopup'이라는 이름의 GameObject를 찾아주세요.", "확인");
            return;
        }
        
        Debug.Log($"[SkillDetailPopupSetup] 팝업 발견: {popup.name}");
        
        // SkillDetailPopup 컴포넌트 추가/가져오기
        SkillDetailPopup popupController = popup.GetComponent<SkillDetailPopup>();
        if (popupController == null)
        {
            popupController = popup.AddComponent<SkillDetailPopup>();
            Debug.Log("[SkillDetailPopupSetup] SkillDetailPopup 컴포넌트 추가됨");
        }
        else
        {
            Debug.Log("[SkillDetailPopupSetup] SkillDetailPopup 컴포넌트 이미 존재함");
        }
        
        SerializedObject so = new SerializedObject(popupController);
        
        // Background_Image 찾기
        Transform bgTransform = FindTransformByName(popup.transform, "Background_Image");
        if (bgTransform == null) bgTransform = FindTransformByName(popup.transform, "Background");
        if (bgTransform == null) bgTransform = FindTransformByName(popup.transform, "BG");
        
        if (bgTransform != null)
        {
            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage != null)
            {
                so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
                Debug.Log($"[SkillDetailPopupSetup] Background 이미지 연결: {bgTransform.name}");
            }
        }
        
        // Close_Button 찾기
        Transform closeBtnTransform = FindTransformByName(popup.transform, "Close_Button");
        if (closeBtnTransform == null) closeBtnTransform = FindTransformByName(popup.transform, "CloseButton");
        if (closeBtnTransform == null) closeBtnTransform = FindTransformByName(popup.transform, "Close");
        if (closeBtnTransform == null) closeBtnTransform = FindTransformByName(popup.transform, "X");
        
        if (closeBtnTransform != null)
        {
            Button closeBtn = closeBtnTransform.GetComponent<Button>();
            if (closeBtn != null)
            {
                so.FindProperty("closeButton").objectReferenceValue = closeBtn;
                Debug.Log($"[SkillDetailPopupSetup] Close 버튼 연결: {closeBtnTransform.name}");
            }
        }
        
        // Icon_Image 찾기
        Transform iconTransform = FindTransformByName(popup.transform, "Icon_Image");
        if (iconTransform == null) iconTransform = FindTransformByName(popup.transform, "Icon");
        if (iconTransform == null) iconTransform = FindTransformByName(popup.transform, "SkillIcon");
        
        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                so.FindProperty("iconImage").objectReferenceValue = iconImage;
                Debug.Log($"[SkillDetailPopupSetup] Icon 이미지 연결: {iconTransform.name}");
            }
        }
        
        // Name_Text 찾기
        Transform nameTransform = FindTransformByName(popup.transform, "Name_Text");
        if (nameTransform == null) nameTransform = FindTransformByName(popup.transform, "SkillName");
        if (nameTransform == null) nameTransform = FindTransformByName(popup.transform, "Title");
        
        if (nameTransform != null)
        {
            TextMeshProUGUI nameText = nameTransform.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                so.FindProperty("nameText").objectReferenceValue = nameText;
                Debug.Log($"[SkillDetailPopupSetup] Name 텍스트 연결: {nameTransform.name}");
            }
        }
        
        // Description_Text 찾기
        Transform descTransform = FindTransformByName(popup.transform, "Description_Text");
        if (descTransform == null) descTransform = FindTransformByName(popup.transform, "Description");
        if (descTransform == null) descTransform = FindTransformByName(popup.transform, "Details");
        
        if (descTransform != null)
        {
            TextMeshProUGUI descText = descTransform.GetComponent<TextMeshProUGUI>();
            if (descText != null)
            {
                so.FindProperty("descriptionText").objectReferenceValue = descText;
                Debug.Log($"[SkillDetailPopupSetup] Description 텍스트 연결: {descTransform.name}");
            }
        }
        
        // Cost_Text 찾기
        Transform costTransform = FindTransformByName(popup.transform, "Cost_Text");
        if (costTransform == null) costTransform = FindTransformByName(popup.transform, "Cost");
        
        if (costTransform != null)
        {
            TextMeshProUGUI costText = costTransform.GetComponent<TextMeshProUGUI>();
            if (costText != null)
            {
                so.FindProperty("costText").objectReferenceValue = costText;
                Debug.Log($"[SkillDetailPopupSetup] Cost 텍스트 연결: {costTransform.name}");
            }
        }
        
        so.ApplyModifiedProperties();
        
        // 초기 상태: 비활성화
        popup.SetActive(false);
        
        // 변경사항 저장
        EditorUtility.SetDirty(popupController);
        
        EditorUtility.DisplayDialog(
            "설정 완료!", 
            "SkillDetail_Popup이 성공적으로 설정되었습니다!\n\n✓ 스킬 노드 클릭 → 상세 정보 팝업 표시\n✓ 배경 클릭 또는 ESC 키로 닫기", 
            "확인"
        );
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
}



