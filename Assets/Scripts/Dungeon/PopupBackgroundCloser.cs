using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팝업 배경을 클릭하면 팝업을 닫는 간단한 스크립트
/// Background_Image GameObject에 붙여서 사용
/// </summary>
[RequireComponent(typeof(Button))]
public class PopupBackgroundCloser : MonoBehaviour
{
    [Header("References")]
    [Tooltip("닫을 팝업 (SkillDetailPopup GameObject)")]
    public GameObject popupToClose;
    
    [Tooltip("또는 SkillDetailPopup 스크립트 직접 연결")]
    public SkillDetailPopup popupScript;
    
    private Button button;
    
    private void Awake()
    {
        Debug.Log($"[PopupBackgroundCloser] ⚡ Awake - {gameObject.name}");
        
        button = GetComponent<Button>();
        
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnBackgroundClicked);
            Debug.Log($"[PopupBackgroundCloser] ✅ 배경 클릭 이벤트 연결 완료 - {gameObject.name}");
        }
        else
        {
            Debug.LogError($"[PopupBackgroundCloser] ❌ Button 컴포넌트를 찾을 수 없습니다!");
        }
    }
    
    private void OnBackgroundClicked()
    {
        Debug.Log($"[PopupBackgroundCloser] 🔘🔘🔘 배경 클릭 감지! (Time: {Time.time})");
        
        // 방법 1: SkillDetailPopup 스크립트로 닫기
        if (popupScript != null)
        {
            Debug.Log("[PopupBackgroundCloser] 방법 1: SkillDetailPopup.ClosePopup() 호출");
            popupScript.ClosePopup();
            return;
        }
        
        // 방법 2: GameObject.SetActive로 직접 닫기
        if (popupToClose != null)
        {
            Debug.Log("[PopupBackgroundCloser] 방법 2: GameObject.SetActive(false) 호출");
            popupToClose.SetActive(false);
            return;
        }
        
        // 방법 3: 자동으로 SkillDetailPopup 찾기
        SkillDetailPopup popup = FindObjectOfType<SkillDetailPopup>();
        if (popup != null)
        {
            Debug.Log("[PopupBackgroundCloser] 방법 3: SkillDetailPopup 자동 검색하여 닫기");
            popup.ClosePopup();
            return;
        }
        
        Debug.LogError("[PopupBackgroundCloser] ❌ 팝업을 닫을 수 없습니다! Inspector에서 연결해주세요.");
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnBackgroundClicked);
        }
    }
}






