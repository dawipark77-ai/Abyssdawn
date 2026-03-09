using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oath & Path 버튼을 클릭하면 Class 패널을 열고 닫는 컨트롤러
/// </summary>
public class OathPathButtonController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Oath & Path 버튼 (이 스크립트가 붙은 게임오브젝트의 Button 컴포넌트를 자동으로 사용)")]
    public Button oathPathButton;
    
    [Tooltip("열고 닫을 Class 패널")]
    public GameObject classPanel;
    
    [Header("Settings")]
    [Tooltip("패널을 열 때 다른 UI를 숨길지 여부")]
    public bool hideOtherUIOnOpen = false;
    
    [Tooltip("숨길 UI 오브젝트들 (hideOtherUIOnOpen이 true일 때)")]
    public GameObject[] uiObjectsToHide;
    
    private bool isPanelOpen = false;
    
    private void Awake()
    {
        // 버튼이 할당되지 않았으면 자동으로 찾기
        if (oathPathButton == null)
        {
            oathPathButton = GetComponent<Button>();
        }
        
        // 버튼 클릭 이벤트 연결
        if (oathPathButton != null)
        {
            // 기존 리스너 제거 (중복 방지)
            oathPathButton.onClick.RemoveListener(OnOathPathButtonClicked);
            
            // 새로운 리스너 추가
            oathPathButton.onClick.AddListener(OnOathPathButtonClicked);
            
            Debug.Log($"[OathPathButtonController] ✅ Oath & Path 버튼 클릭 이벤트 연결 완료!");
        }
        else
        {
            Debug.LogError("[OathPathButtonController] ❌ Oath & Path 버튼을 찾을 수 없습니다!");
        }
    }
    
    private void Start()
    {
        // 패널이 할당되지 않았으면 자동으로 찾기
        if (classPanel == null)
        {
            classPanel = GameObject.Find("Class");
            
            if (classPanel == null)
            {
                // Canvas > Class 경로로 찾기 시도
                Transform canvas = GameObject.Find("Canvas")?.transform;
                if (canvas != null)
                {
                    Transform classTransform = canvas.Find("Class");
                    if (classTransform != null)
                    {
                        classPanel = classTransform.gameObject;
                    }
                }
            }
            
            if (classPanel != null)
            {
                Debug.Log($"[OathPathButtonController] Class 패널 자동 검색 성공: {classPanel.name}");
            }
            else
            {
                Debug.LogWarning("[OathPathButtonController] Class 패널을 찾을 수 없습니다. Inspector에서 수동으로 할당해주세요.");
            }
        }
        else
        {
            Debug.Log($"[OathPathButtonController] Class 패널 이미 할당됨: {classPanel.name}");
        }
        
        // 초기 상태: 패널 닫기
        if (classPanel != null)
        {
            classPanel.SetActive(false);
            isPanelOpen = false;
            Debug.Log("[OathPathButtonController] 초기 상태: Class 패널 닫힘");
        }
    }
    
    private void Update()
    {
        // 패널의 실제 상태와 isPanelOpen 변수 동기화
        if (classPanel != null)
        {
            isPanelOpen = classPanel.activeSelf;
        }
    }
    
    /// <summary>
    /// Oath & Path 버튼 클릭 시 호출
    /// </summary>
    private void OnOathPathButtonClicked()
    {
        Debug.Log($"[OathPathButtonController] 🔘 Oath & Path 버튼 클릭 감지!");
        
        if (classPanel == null)
        {
            Debug.LogError("[OathPathButtonController] ❌ Class 패널이 할당되지 않았습니다!");
            return;
        }
        
        // 토글: 실제 패널의 activeSelf 상태를 체크
        bool isCurrentlyOpen = classPanel.activeSelf;
        
        Debug.Log($"[OathPathButtonController] 현재 패널 상태: {(isCurrentlyOpen ? "열림" : "닫힘")}");
        
        if (isCurrentlyOpen)
        {
            Debug.Log("[OathPathButtonController] → Class 패널 닫기 시도");
            CloseClassPanel();
        }
        else
        {
            Debug.Log("[OathPathButtonController] → Class 패널 열기 시도");
            OpenClassPanel();
        }
    }
    
    /// <summary>
    /// Class 패널 열기
    /// </summary>
    public void OpenClassPanel()
    {
        if (classPanel == null)
        {
            Debug.LogError("[OathPathButtonController] Class 패널이 null입니다!");
            return;
        }
        
        Debug.Log($"[OathPathButtonController] ✅ Class 패널 열기 - 이전 상태: {classPanel.activeSelf}");
        
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
        classPanel.SetActive(true);
        isPanelOpen = true;

        // Class 패널 안의 탭 컨트롤러에게 기본 탭을 열도록 요청
        var tabController = classPanel.GetComponent<ClassTabController>();
        if (tabController == null)
        {
            tabController = classPanel.GetComponentInChildren<ClassTabController>(true);
        }
        if (tabController != null)
        {
            tabController.EnsureDefaultPanel();
        }
        
        Debug.Log($"[OathPathButtonController] Class 패널 열림 완료 - 현재 상태: {classPanel.activeSelf}");
        
        // 커서 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Class 패널 닫기
    /// </summary>
    public void CloseClassPanel()
    {
        if (classPanel == null)
        {
            Debug.LogError("[OathPathButtonController] Class 패널이 null입니다!");
            return;
        }
        
        Debug.Log($"[OathPathButtonController] ❌ Class 패널 닫기 - 이전 상태: {classPanel.activeSelf}");
        
        // 패널 닫기
        classPanel.SetActive(false);
        isPanelOpen = false;
        
        Debug.Log($"[OathPathButtonController] Class 패널 닫힘 완료 - 현재 상태: {classPanel.activeSelf}");
        
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
        if (classPanel != null)
        {
            return classPanel.activeSelf;
        }
        return isPanelOpen;
    }
    
    private void OnDestroy()
    {
        // 버튼 이벤트 해제
        if (oathPathButton != null)
        {
            oathPathButton.onClick.RemoveListener(OnOathPathButtonClicked);
        }
    }
}

