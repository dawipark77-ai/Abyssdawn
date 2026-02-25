using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패널 배경 클릭 시 패널을 닫는 범용 스크립트
/// LoreTree_Panel, SkillDetail_Popup 등 모든 팝업에 사용 가능
/// </summary>
[RequireComponent(typeof(Button), typeof(Image))]
public class PanelBackgroundCloser : MonoBehaviour
{
    [Header("닫을 패널 선택")]
    [Tooltip("닫을 패널 GameObject (비어있으면 자동으로 부모를 찾음)")]
    public GameObject panelToClose;
    
    [Header("닫기 방법 선택")]
    [Tooltip("특정 컨트롤러를 통해 닫기 (SkillDetailPopup, LoreButtonController 등)")]
    public MonoBehaviour closerScript;
    
    private Button button;
    private Image image;
    
    private void Awake()
    {
        Debug.Log($"[PanelBackgroundCloser] ⚡ Awake - {gameObject.name}");
        
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        
        // Raycast Target 활성화 (클릭 감지를 위해)
        if (image != null)
        {
            image.raycastTarget = true;
            Debug.Log($"[PanelBackgroundCloser] Raycast Target 활성화: {image.raycastTarget}");
        }
        
        // 패널이 지정되지 않았으면 부모를 사용
        if (panelToClose == null)
        {
            panelToClose = transform.parent?.gameObject;
            if (panelToClose != null)
            {
                Debug.Log($"[PanelBackgroundCloser] 패널 자동 검색: {panelToClose.name}");
            }
        }
        
        // 버튼 클릭 이벤트 연결
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ClosePanel);
            Debug.Log($"[PanelBackgroundCloser] ✅ 배경 클릭 이벤트 연결 완료");
        }
    }
    
    private void ClosePanel()
    {
        Debug.Log($"[PanelBackgroundCloser] 🔘🔘🔘 배경 클릭 감지! (Time: {Time.time})");
        
        // 방법 1: SkillDetailPopup 스크립트로 닫기
        if (closerScript is SkillDetailPopup)
        {
            Debug.Log("[PanelBackgroundCloser] SkillDetailPopup.ClosePopup() 호출");
            ((SkillDetailPopup)closerScript).ClosePopup();
            return;
        }
        
        // 방법 2: LoreButtonController로 닫기
        if (closerScript is LoreButtonController)
        {
            Debug.Log("[PanelBackgroundCloser] LoreButtonController.CloseLorePanel() 호출");
            ((LoreButtonController)closerScript).CloseLorePanel();
            return;
        }
        
        // 방법 3: 자동으로 컨트롤러 찾기
        if (closerScript == null)
        {
            // SkillDetailPopup 찾기
            SkillDetailPopup skillPopup = FindObjectOfType<SkillDetailPopup>();
            if (skillPopup != null && skillPopup.IsPanelOpen())
            {
                Debug.Log("[PanelBackgroundCloser] SkillDetailPopup 자동 검색하여 닫기");
                skillPopup.ClosePopup();
                return;
            }
            
            // LoreButtonController 찾기
            LoreButtonController loreController = FindObjectOfType<LoreButtonController>();
            if (loreController != null)
            {
                Debug.Log("[PanelBackgroundCloser] LoreButtonController 자동 검색하여 닫기");
                loreController.CloseLorePanel();
                return;
            }
        }
        
        // 방법 4: 직접 GameObject 닫기
        if (panelToClose != null)
        {
            Debug.Log($"[PanelBackgroundCloser] GameObject 직접 닫기: {panelToClose.name}");
            panelToClose.SetActive(false);
        }
        else
        {
            Debug.LogError("[PanelBackgroundCloser] ❌ 닫을 패널을 찾을 수 없습니다!");
        }
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ClosePanel);
        }
    }
}



