using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Class 패널 안의 탭 버튼들을 클릭하면 해당하는 패널을 열고 닫는 컨트롤러
/// Tab_OathBinding → Oath&Binding 패널
/// Tab_ClassPath → Class_Path 패널
/// </summary>
public class ClassTabController : MonoBehaviour
{
    [System.Serializable]
    public class TabPanelMapping
    {
        [Tooltip("탭 버튼 (예: Tab_OathBinding, Tab_ClassPath)")]
        public Button tabButton;
        
        [Tooltip("해당하는 패널 (예: Oath&Binding, Class_Path)")]
        public GameObject targetPanel;
        
        [Tooltip("탭 이름 (자동 매칭용, 비워두면 버튼 GameObject 이름 사용)")]
        public string tabName;
    }
    
    [Header("탭-패널 매핑")]
    [Tooltip("탭 버튼과 패널 매핑 리스트")]
    public TabPanelMapping[] tabMappings = new TabPanelMapping[2];
    
    [Header("설정")]
    [Tooltip("한 번에 하나의 패널만 열리도록 할지 여부")]
    public bool singlePanelMode = true;
    
    [Tooltip("Class 패널 부모 오브젝트 (자동 검색용)")]
    public Transform classPanelParent;
    
    private GameObject currentOpenPanel = null;
    
    private void Awake()
    {
        // Class 패널 부모 자동 찾기
        if (classPanelParent == null)
        {
            classPanelParent = transform.parent;
            if (classPanelParent == null)
            {
                GameObject classObj = GameObject.Find("Class");
                if (classObj != null)
                {
                    classPanelParent = classObj.transform;
                }
            }
        }
        
        // 매핑이 비어있거나 기본값이면 자동으로 찾기 시도
        if (tabMappings == null || tabMappings.Length == 0 || 
            (tabMappings[0].tabButton == null && tabMappings[1].tabButton == null))
        {
            AutoMapTabsToPanels();
        }
        
        // 각 탭 버튼에 클릭 이벤트 연결
        SetupTabListeners();
    }
    
    private void Start()
    {
        // 초기 상태: 첫 번째 탭의 패널을 열거나, 모두 닫기
        if (tabMappings != null && tabMappings.Length > 0 && tabMappings[0].targetPanel != null)
        {
            // 기본적으로 첫 번째 탭(OathBinding)을 열기
            OpenTabPanel(tabMappings[0].targetPanel);
        }
        else
        {
            // 모든 패널 닫기
            CloseAllPanels();
        }
    }
    
    /// <summary>
    /// 탭 버튼과 패널을 자동으로 매핑
    /// </summary>
    private void AutoMapTabsToPanels()
    {
        if (classPanelParent == null) return;
        
        // TabButtons 찾기
        Transform tabButtonsParent = classPanelParent.Find("TabButtons");
        if (tabButtonsParent == null)
        {
            Debug.LogWarning("[ClassTabController] TabButtons를 찾을 수 없습니다.");
            return;
        }
        
        // Oath&Binding 패널 찾기
        Transform oathBindingPanel = classPanelParent.Find("Oath&Binding");
        if (oathBindingPanel == null)
        {
            oathBindingPanel = classPanelParent.Find("Oath & Binding");
        }
        
        // Class_Path 패널 찾기
        Transform classPathPanel = classPanelParent.Find("Class_Path");
        if (classPathPanel == null)
        {
            classPathPanel = classPanelParent.Find("ClassPath");
        }
        
        // 탭 버튼 찾기
        Transform tabOathBinding = tabButtonsParent.Find("Tab_OathBinding");
        Transform tabClassPath = tabButtonsParent.Find("Tab_ClassPath");
        
        // 매핑 배열 초기화
        tabMappings = new TabPanelMapping[2];
        
        // 첫 번째 탭: OathBinding
        if (tabOathBinding != null && oathBindingPanel != null)
        {
            tabMappings[0] = new TabPanelMapping
            {
                tabButton = tabOathBinding.GetComponent<Button>(),
                targetPanel = oathBindingPanel.gameObject,
                tabName = "OathBinding"
            };
            Debug.Log($"[ClassTabController] ✅ Tab_OathBinding → Oath&Binding 매핑 완료");
        }
        
        // 두 번째 탭: ClassPath
        if (tabClassPath != null && classPathPanel != null)
        {
            tabMappings[1] = new TabPanelMapping
            {
                tabButton = tabClassPath.GetComponent<Button>(),
                targetPanel = classPathPanel.gameObject,
                tabName = "ClassPath"
            };
            Debug.Log($"[ClassTabController] ✅ Tab_ClassPath → Class_Path 매핑 완료");
        }
    }
    
    /// <summary>
    /// 모든 탭 버튼에 클릭 이벤트 연결
    /// </summary>
    private void SetupTabListeners()
    {
        if (tabMappings == null) return;
        
        foreach (var mapping in tabMappings)
        {
            if (mapping.tabButton == null)
            {
                Debug.LogWarning("[ClassTabController] 탭 버튼이 null인 매핑이 있습니다.");
                continue;
            }
            
            if (mapping.targetPanel == null)
            {
                Debug.LogWarning($"[ClassTabController] '{mapping.tabButton.name}' 탭에 해당하는 패널이 null입니다.");
                continue;
            }
            
            // 기존 리스너 제거
            mapping.tabButton.onClick.RemoveAllListeners();
            
            // 새 리스너 추가 (클로저로 패널 캡처)
            GameObject targetPanel = mapping.targetPanel;
            mapping.tabButton.onClick.AddListener(() => OnTabButtonClicked(targetPanel));
            
            Debug.Log($"[ClassTabController] ✅ 탭 버튼 이벤트 연결: {mapping.tabButton.name} → {targetPanel.name}");
        }
    }
    
    /// <summary>
    /// 탭 버튼 클릭 시 호출
    /// </summary>
    private void OnTabButtonClicked(GameObject targetPanel)
    {
        if (targetPanel == null)
        {
            Debug.LogError("[ClassTabController] 타겟 패널이 null입니다!");
            return;
        }
        
        if (singlePanelMode)
        {
            // 항상 하나만 열리도록: 다른 패널은 전부 닫고 이 패널만 연다
            CloseAllPanels();
        }

        targetPanel.SetActive(true);
        currentOpenPanel = targetPanel;
        Debug.Log($"[ClassTabController] 탭 선택 → 패널 열기: {targetPanel.name}");
    }
    
    /// <summary>
    /// 특정 탭의 패널 열기 (외부에서 호출 가능)
    /// </summary>
    public void OpenTabPanel(GameObject panelToOpen)
    {
        if (panelToOpen == null) return;
        
        if (singlePanelMode)
        {
            CloseAllPanels();
        }
        
        panelToOpen.SetActive(true);
        currentOpenPanel = panelToOpen;
        Debug.Log($"[ClassTabController] 외부 호출로 패널 열기: {panelToOpen.name}");
    }

        /// <summary>
        /// Class 패널이 다시 열릴 때, 최소 하나의 탭 패널이 열려 있도록 보장
        /// </summary>
        public void EnsureDefaultPanel()
        {
            if (tabMappings == null || tabMappings.Length == 0) return;

            // 이미 열려 있는 패널이 있으면 그대로 사용
            foreach (var mapping in tabMappings)
            {
                if (mapping != null && mapping.targetPanel != null && mapping.targetPanel.activeSelf)
                {
                    currentOpenPanel = mapping.targetPanel;
                    return;
                }
            }

            // 아무 것도 안 열려 있으면 첫 번째 유효한 패널을 연다
            foreach (var mapping in tabMappings)
            {
                if (mapping != null && mapping.targetPanel != null)
                {
                    OpenTabPanel(mapping.targetPanel);
                    return;
                }
            }
        }
    
    /// <summary>
    /// 모든 패널 닫기
    /// </summary>
    public void CloseAllPanels()
    {
        if (tabMappings == null) return;
        
        foreach (var mapping in tabMappings)
        {
            if (mapping.targetPanel != null)
            {
                mapping.targetPanel.SetActive(false);
            }
        }
        
        currentOpenPanel = null;
        Debug.Log("[ClassTabController] 모든 패널 닫기 완료");
    }
    
    private void OnDestroy()
    {
        // 모든 버튼 이벤트 해제
        if (tabMappings != null)
        {
            foreach (var mapping in tabMappings)
            {
                if (mapping.tabButton != null)
                {
                    mapping.tabButton.onClick.RemoveAllListeners();
                }
            }
        }
    }
}

