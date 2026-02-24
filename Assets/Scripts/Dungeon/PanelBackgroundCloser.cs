using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패널 배경 클릭 시 패널을 닫는 간단한 스크립트
/// </summary>
[RequireComponent(typeof(Button))]
public class PanelBackgroundCloser : MonoBehaviour
{
    [Tooltip("닫을 패널 (자동으로 부모를 찾음)")]
    public GameObject panelToClose;
    
    private Button button;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        
        // 패널이 지정되지 않았으면 부모를 사용
        if (panelToClose == null)
        {
            panelToClose = transform.parent?.gameObject;
        }
        
        // 버튼 클릭 이벤트 연결
        if (button != null)
        {
            button.onClick.AddListener(ClosePanel);
        }
    }
    
    private void ClosePanel()
    {
        if (panelToClose != null)
        {
            Debug.Log($"[PanelBackgroundCloser] 배경 클릭 - {panelToClose.name} 닫기");
            
            // LoreButtonController를 통해 닫기
            LoreButtonController loreController = FindObjectOfType<LoreButtonController>();
            if (loreController != null)
            {
                loreController.CloseLorePanel();
            }
            else
            {
                // 직접 닫기
                panelToClose.SetActive(false);
            }
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

