using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoreTree_Panel 내부의 닫기 버튼 등을 관리하는 컨트롤러
/// </summary>
public class LoreTreePanelController : MonoBehaviour
{
    [Header("Close Button")]
    [Tooltip("패널을 닫을 버튼 (X 버튼, Close 버튼 등)")]
    public Button closeButton;
    
    [Header("Optional: ESC Key")]
    [Tooltip("ESC 키로 패널 닫기 활성화")]
    public bool enableEscapeKey = true;
    
    private LoreButtonController loreButtonController;
    
    private void Awake()
    {
        // LoreButtonController 찾기
        loreButtonController = FindObjectOfType<LoreButtonController>();
        
        if (loreButtonController == null)
        {
            Debug.LogWarning("[LoreTreePanelController] LoreButtonController를 찾을 수 없습니다.");
        }
        
        // Close 버튼이 할당되지 않았으면 자동으로 찾기
        if (closeButton == null)
        {
            // "Close", "X", "CloseButton" 등의 이름으로 찾기
            Transform closeTransform = transform.Find("CloseButton");
            if (closeTransform == null) closeTransform = transform.Find("Close");
            if (closeTransform == null) closeTransform = transform.Find("X");
            if (closeTransform == null) closeTransform = transform.Find("Btn_Close");
            
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
                Debug.Log($"[LoreTreePanelController] Close 버튼 자동 검색 성공: {closeTransform.name}");
            }
        }
        
        // Close 버튼 클릭 이벤트 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            Debug.Log("[LoreTreePanelController] Close 버튼 클릭 이벤트 연결 완료");
        }
    }
    
    private void Update()
    {
        // ESC 키로 패널 닫기
        if (enableEscapeKey && Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameObject.activeSelf)
            {
                OnCloseButtonClicked();
            }
        }
    }
    
    /// <summary>
    /// Close 버튼 클릭 시 호출
    /// </summary>
    private void OnCloseButtonClicked()
    {
        Debug.Log("[LoreTreePanelController] Close 버튼 클릭");
        
        if (loreButtonController != null)
        {
            loreButtonController.CloseLorePanel();
        }
        else
        {
            // LoreButtonController가 없으면 직접 패널 닫기
            gameObject.SetActive(false);
            Debug.LogWarning("[LoreTreePanelController] LoreButtonController가 없어서 직접 패널을 닫습니다.");
        }
    }
    
    /// <summary>
    /// 외부에서 호출 가능한 닫기 메서드
    /// </summary>
    public void ClosePanel()
    {
        OnCloseButtonClicked();
    }
    
    private void OnDestroy()
    {
        // Close 버튼 이벤트 해제
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }
    }
}







