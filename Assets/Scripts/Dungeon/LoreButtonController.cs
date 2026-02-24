using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lore 버튼을 클릭하면 LoreTree_Panel을 열고 닫는 컨트롤러
/// </summary>
public class LoreButtonController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Lore 버튼 (이 스크립트가 붙은 게임오브젝트의 Button 컴포넌트를 자동으로 사용)")]
    public Button loreButton;
    
    [Tooltip("열고 닫을 LoreTree 패널")]
    public GameObject loreTreePanel;
    
    [Header("Settings")]
    [Tooltip("패널을 열 때 다른 UI를 숨길지 여부")]
    public bool hideOtherUIOnOpen = false;
    
    [Tooltip("숨길 UI 오브젝트들 (hideOtherUIOnOpen이 true일 때)")]
    public GameObject[] uiObjectsToHide;
    
    private bool isPanelOpen = false;
    
    private void Awake()
    {
        Debug.Log($"[LoreButtonController] ⚡ Awake 시작 - GameObject: {gameObject.name}, Active: {gameObject.activeInHierarchy}, Enabled: {enabled}");
        
        // 버튼이 할당되지 않았으면 자동으로 찾기
        if (loreButton == null)
        {
            loreButton = GetComponent<Button>();
            Debug.Log($"[LoreButtonController] Button 자동 검색: {(loreButton != null ? "성공" : "실패")}");
        }
        else
        {
            Debug.Log($"[LoreButtonController] Button 이미 할당됨: {loreButton.gameObject.name}");
        }
        
        // 버튼 클릭 이벤트 연결
        if (loreButton != null)
        {
            // 기존 리스너 제거 (중복 방지)
            loreButton.onClick.RemoveListener(OnLoreButtonClicked);
            
            // 새로운 리스너 추가
            loreButton.onClick.AddListener(OnLoreButtonClicked);
            
            Debug.Log($"[LoreButtonController] ✅ Lore 버튼 클릭 이벤트 연결 완료! (리스너 수: {loreButton.onClick.GetPersistentEventCount()})");
            Debug.Log($"[LoreButtonController] 버튼 Interactable: {loreButton.interactable}, Navigation: {loreButton.navigation.mode}");
            
            // 즉시 테스트용 로그
            Debug.Log($"[LoreButtonController] 🎯 테스트: 지금 이 버튼을 클릭하면 로그가 나타나야 합니다!");
        }
        else
        {
            Debug.LogError("[LoreButtonController] ❌ Lore 버튼을 찾을 수 없습니다!");
        }
    }
    
    // 버튼이 실제로 클릭 가능한지 확인하는 메서드 추가
    private void OnEnable()
    {
        Debug.Log($"[LoreButtonController] OnEnable 호출됨 - {gameObject.name}");
    }
    
    private void Start()
    {
        Debug.Log($"[LoreButtonController] Start 호출됨");
        
        // 패널이 할당되지 않았으면 자동으로 찾기
        if (loreTreePanel == null)
        {
            loreTreePanel = GameObject.Find("LoreTree_Panel");
            
            if (loreTreePanel != null)
            {
                Debug.Log($"[LoreButtonController] LoreTree_Panel 자동 검색 성공: {loreTreePanel.name}");
            }
            else
            {
                Debug.LogWarning("[LoreButtonController] LoreTree_Panel을 찾을 수 없습니다. Inspector에서 수동으로 할당해주세요.");
            }
        }
        else
        {
            Debug.Log($"[LoreButtonController] LoreTree_Panel 이미 할당됨: {loreTreePanel.name}");
        }
        
        // 초기 상태: 패널 닫기
        if (loreTreePanel != null)
        {
            loreTreePanel.SetActive(false);
            isPanelOpen = false;
            Debug.Log("[LoreButtonController] 초기 상태: 패널 닫힘");
        }
    }
    
    private void Update()
    {
        // 패널의 실제 상태와 isPanelOpen 변수 동기화
        if (loreTreePanel != null)
        {
            isPanelOpen = loreTreePanel.activeSelf;
        }
    }
    
    /// <summary>
    /// Lore 버튼 클릭 시 호출
    /// </summary>
    private void OnLoreButtonClicked()
    {
        Debug.Log($"[LoreButtonController] 🔘 버튼 클릭 감지! Time: {Time.time}");
        
        if (loreTreePanel == null)
        {
            Debug.LogError("[LoreButtonController] ❌ LoreTree_Panel이 할당되지 않았습니다!");
            return;
        }
        
        // 토글: 실제 패널의 activeSelf 상태를 체크
        bool isCurrentlyOpen = loreTreePanel.activeSelf;
        
        Debug.Log($"[LoreButtonController] 현재 패널 상태: {(isCurrentlyOpen ? "열림" : "닫힘")}");
        
        if (isCurrentlyOpen)
        {
            Debug.Log("[LoreButtonController] → 패널 닫기 시도");
            CloseLorePanel();
        }
        else
        {
            Debug.Log("[LoreButtonController] → 패널 열기 시도");
            OpenLorePanel();
        }
    }
    
    /// <summary>
    /// LoreTree 패널 열기
    /// </summary>
    public void OpenLorePanel()
    {
        if (loreTreePanel == null)
        {
            Debug.LogError("[LoreButtonController] LoreTree_Panel이 null입니다!");
            return;
        }
        
        Debug.Log($"[LoreButtonController] ✅ LoreTree 패널 열기 - 이전 상태: {loreTreePanel.activeSelf}");
        
        // 다른 UI 숨기기 (옵션)
        if (hideOtherUIOnOpen && uiObjectsToHide != null)
        {
            foreach (GameObject ui in uiObjectsToHide)
            {
                if (ui != null)
                {
                    ui.SetActive(false);
                }
            }
        }
        
        // 패널 열기
        loreTreePanel.SetActive(true);
        isPanelOpen = true;
        
        Debug.Log($"[LoreButtonController] 패널 열림 완료 - 현재 상태: {loreTreePanel.activeSelf}");
        
        // 커서 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// LoreTree 패널 닫기
    /// </summary>
    public void CloseLorePanel()
    {
        if (loreTreePanel == null)
        {
            Debug.LogError("[LoreButtonController] LoreTree_Panel이 null입니다!");
            return;
        }
        
        Debug.Log($"[LoreButtonController] ❌ LoreTree 패널 닫기 - 이전 상태: {loreTreePanel.activeSelf}");
        
        // 패널 닫기
        loreTreePanel.SetActive(false);
        isPanelOpen = false;
        
        Debug.Log($"[LoreButtonController] 패널 닫힘 완료 - 현재 상태: {loreTreePanel.activeSelf}");
        
        // 다른 UI 다시 표시 (옵션)
        if (hideOtherUIOnOpen && uiObjectsToHide != null)
        {
            foreach (GameObject ui in uiObjectsToHide)
            {
                if (ui != null)
                {
                    ui.SetActive(true);
                }
            }
        }
    }
    
    /// <summary>
    /// 외부에서 패널 상태 확인
    /// </summary>
    public bool IsPanelOpen()
    {
        if (loreTreePanel != null)
        {
            return loreTreePanel.activeSelf;
        }
        return isPanelOpen;
    }
    
    private void OnDestroy()
    {
        // 버튼 이벤트 해제
        if (loreButton != null)
        {
            loreButton.onClick.RemoveListener(OnLoreButtonClicked);
        }
    }
}


