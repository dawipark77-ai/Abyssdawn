using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 스킬 상세 정보 팝업 컨트롤러 (LoreButtonController 방식)
/// 수동으로 UI 요소들을 연결해서 사용합니다.
/// </summary>
public class SkillDetailPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("팝업 패널 GameObject (이 스크립트가 붙은 오브젝트 자체일 수도 있음)")]
    public GameObject popupPanel;
    
    [Tooltip("팝업을 닫을 배경 이미지")]
    public Image backgroundImage;
    
    [Tooltip("닫기 버튼")]
    public Button closeButton;
    
    [Tooltip("스킬 배우기 버튼")]
    public Button learnButton;
    
    [Tooltip("스킬 배우기 버튼 텍스트")]
    public TextMeshProUGUI learnButtonText;
    
    [Header("Skill Info Display")]
    [Tooltip("스킬 아이콘 이미지")]
    public Image iconImage;
    
    [Tooltip("스킬 이름 텍스트")]
    public TextMeshProUGUI nameText;
    
    [Tooltip("스킬 설명 텍스트")]
    public TextMeshProUGUI descriptionText;
    
    [Tooltip("코스트 텍스트")]
    public TextMeshProUGUI costText;
    
    [Tooltip("스킬 상태 텍스트 (Locked/Available/Learned)")]
    public TextMeshProUGUI statusText;
    
    [Tooltip("필요한 LP 텍스트")]
    public TextMeshProUGUI requiredLPText;
    
    [Header("Settings")]
    [Tooltip("ESC 키로 팝업 닫기")]
    public bool enableEscapeKey = true;
    
    private bool isPanelOpen = false;
    private SkillData currentSkillData;
    private SkillTreeNode currentNode;
    
    private void Awake()
    {
        Debug.Log($"[SkillDetailPopup] ⚡ Awake 시작 - GameObject: {gameObject.name}");
        
        // 팝업 패널이 할당되지 않았으면 자신을 패널로 사용
        if (popupPanel == null)
        {
            popupPanel = gameObject;
            Debug.Log($"[SkillDetailPopup] Popup Panel이 할당되지 않아 자신을 패널로 사용: {gameObject.name}");
        }
        
        // 배경 이미지 클릭 시 닫기
        if (backgroundImage != null)
        {
            Button bgButton = backgroundImage.GetComponent<Button>();
            if (bgButton == null)
            {
                bgButton = backgroundImage.gameObject.AddComponent<Button>();
                Debug.Log("[SkillDetailPopup] 배경 이미지에 Button 컴포넌트 추가");
            }
            
            // Raycast Target 활성화 (클릭 감지를 위해)
            backgroundImage.raycastTarget = true;
            
            bgButton.onClick.RemoveListener(ClosePopup); // 중복 방지
            bgButton.onClick.AddListener(OnBackgroundClicked);
            Debug.Log($"[SkillDetailPopup] ✅ 배경 클릭 이벤트 연결 완료 (Raycast Target: {backgroundImage.raycastTarget})");
        }
        else
        {
            Debug.LogWarning("[SkillDetailPopup] ⚠️ Background Image가 할당되지 않았습니다!");
        }
        
        // 닫기 버튼 연결
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup); // 중복 방지
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            Debug.Log("[SkillDetailPopup] ✅ 닫기 버튼 이벤트 연결 완료");
        }
        else
        {
            Debug.LogWarning("[SkillDetailPopup] ⚠️ Close Button이 할당되지 않았습니다!");
        }
        
        // Learn 버튼 연결
        if (learnButton != null)
        {
            learnButton.onClick.RemoveListener(OnLearnButtonClicked); // 중복 방지
            learnButton.onClick.AddListener(OnLearnButtonClicked);
            Debug.Log("[SkillDetailPopup] ✅ Learn 버튼 이벤트 연결 완료");
        }
        else
        {
            Debug.LogWarning("[SkillDetailPopup] ⚠️ Learn Button이 할당되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// Learn 버튼 클릭 시 호출
    /// </summary>
    private void OnLearnButtonClicked()
    {
        Debug.Log("[SkillDetailPopup] 🔘 Learn 버튼 클릭 감지!");
        
        if (currentNode == null)
        {
            Debug.LogError("[SkillDetailPopup] ❌ currentNode가 null입니다!");
            return;
        }
        
        // SwordSkillTreeManager 찾기
        SwordSkillTreeManager manager = FindObjectOfType<SwordSkillTreeManager>();
        if (manager == null)
        {
            Debug.LogError("[SkillDetailPopup] ❌ SwordSkillTreeManager를 찾을 수 없습니다!");
            return;
        }
        
        // 스킬 배우기 시도
        manager.TryLearnSkill(currentNode);
        
        // 스킬 정보 새로고침 (상태 변경 반영)
        UpdateSkillInfo();
    }
    
    /// <summary>
    /// 배경 클릭 시 호출
    /// </summary>
    private void OnBackgroundClicked()
    {
        Debug.Log("[SkillDetailPopup] 🔘 배경 클릭 감지! → 팝업 닫기");
        ClosePopup();
    }
    
    /// <summary>
    /// 닫기 버튼 클릭 시 호출
    /// </summary>
    private void OnCloseButtonClicked()
    {
        Debug.Log("[SkillDetailPopup] 🔘 닫기 버튼 클릭 감지! → 팝업 닫기");
        ClosePopup();
    }
    
    private void Start()
    {
        Debug.Log($"[SkillDetailPopup] Start 호출됨");
        
        // 초기 상태: 패널 닫기
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
            isPanelOpen = false;
            Debug.Log("[SkillDetailPopup] 초기 상태: 패널 닫힘");
        }
    }
    
    private void Update()
    {
        // 패널의 실제 상태와 isPanelOpen 변수 동기화
        if (popupPanel != null)
        {
            isPanelOpen = popupPanel.activeSelf;
        }
        
        // ESC 키로 닫기
        if (enableEscapeKey && Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPanelOpen)
            {
                ClosePopup();
            }
        }
        
        // 팝업 외부 클릭 감지
        if (isPanelOpen && Input.GetMouseButtonDown(0))
        {
            // 팝업 외부를 클릭했는지 확인
            if (!IsPointerOverPopup())
            {
                Debug.Log("[SkillDetailPopup] 🔘 팝업 외부 클릭 감지! → 팝업 닫기");
                ClosePopup();
            }
        }
    }
    
    /// <summary>
    /// 마우스 포인터가 팝업 위에 있는지 확인
    /// </summary>
    private bool IsPointerOverPopup()
    {
        if (popupPanel == null) return false;
        
        RectTransform rectTransform = popupPanel.GetComponent<RectTransform>();
        if (rectTransform == null) return false;
        
        // 마우스 위치가 팝업 RectTransform 안에 있는지 확인
        bool isOver = RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform, 
            Input.mousePosition, 
            null // Overlay Canvas는 null
        );
        
        if (!isOver)
        {
            Debug.Log($"[SkillDetailPopup] 마우스가 팝업 외부에 있음 - 마우스 위치: {Input.mousePosition}");
        }
        
        return isOver;
    }
    
    /// <summary>
    /// 스킬 정보를 표시하며 팝업 열기 (SkillData만)
    /// </summary>
    public void ShowSkillDetail(SkillData skillData)
    {
        Debug.Log($"[SkillDetailPopup] 🔘 ShowSkillDetail(SkillData) 호출됨!");
        
        if (skillData == null)
        {
            Debug.LogError("[SkillDetailPopup] ❌ SkillData가 null입니다!");
            return;
        }
        
        currentSkillData = skillData;
        currentNode = null; // 노드 정보 없음
        
        // 스킬 정보 표시
        UpdateSkillInfo();
        
        // 팝업 열기
        OpenPopup();
        
        Debug.Log($"[SkillDetailPopup] ✅ {skillData.skillName} 정보 표시 완료!");
    }
    
    /// <summary>
    /// 스킬 정보를 표시하며 팝업 열기 (SkillTreeNode 포함)
    /// </summary>
    public void ShowSkillDetail(SkillData skillData, SkillTreeNode node)
    {
        Debug.Log($"[SkillDetailPopup] 🔘 ShowSkillDetail(SkillData, SkillTreeNode) 호출됨!");
        
        if (skillData == null)
        {
            Debug.LogError("[SkillDetailPopup] ❌ SkillData가 null입니다!");
            return;
        }
        
        currentSkillData = skillData;
        currentNode = node;
        
        // 스킬 정보 표시
        UpdateSkillInfo();
        
        // 팝업 열기
        OpenPopup();
        
        Debug.Log($"[SkillDetailPopup] ✅ {skillData.skillName} 정보 표시 완료!");
    }
    
    /// <summary>
    /// 팝업 열기
    /// </summary>
    public void OpenPopup()
    {
        if (popupPanel == null)
        {
            Debug.LogError("[SkillDetailPopup] ❌ Popup Panel이 null입니다!");
            return;
        }
        
        Debug.Log($"[SkillDetailPopup] ✅ 팝업 열기 - 이전 상태: {popupPanel.activeSelf}");
        
        // 패널 열기
        popupPanel.SetActive(true);
        isPanelOpen = true;
        
        Debug.Log($"[SkillDetailPopup] 팝업 열림 완료 - 현재 상태: {popupPanel.activeSelf}");
        
        // 커서 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// 스킬 정보 UI 업데이트
    /// </summary>
    private void UpdateSkillInfo()
    {
        if (currentSkillData == null) return;
        
        // 아이콘 설정
        if (iconImage != null)
        {
            if (currentSkillData.skillIcon != null)
            {
                iconImage.sprite = currentSkillData.skillIcon;
                iconImage.color = Color.white;
                Debug.Log($"[SkillDetailPopup] 아이콘 설정: {currentSkillData.skillIcon.name}");
            }
            else
            {
                // 아이콘이 없으면 투명하게
                iconImage.sprite = null;
                iconImage.color = new Color(1, 1, 1, 0);
                Debug.LogWarning($"[SkillDetailPopup] {currentSkillData.skillName}: 아이콘이 없습니다!");
            }
        }
        
        // 이름 설정
        if (nameText != null)
        {
            nameText.text = currentSkillData.skillName;
        }
        
        // 설명 설정
        if (descriptionText != null)
        {
            descriptionText.text = currentSkillData.description;
        }
        
        // 코스트 설정
        if (costText != null)
        {
            string costInfo = "";
            
            if (currentSkillData.hpCostPercent > 0)
            {
                costInfo += $"HP: {currentSkillData.hpCostPercent}%";
            }
            
            if (currentSkillData.mpCost > 0)
            {
                if (costInfo.Length > 0) costInfo += " / ";
                costInfo += $"MP: {currentSkillData.mpCost}";
            }
            
            if (costInfo.Length == 0)
            {
                costInfo = "Passive";
            }
            else
            {
                costInfo = "Cost: " + costInfo;
            }
            
            costText.text = costInfo;
        }
        
        // 노드 상태에 따라 Learn 버튼 업데이트
        UpdateLearnButton();
    }
    
    /// <summary>
    /// Learn 버튼 상태 업데이트
    /// </summary>
    private void UpdateLearnButton()
    {
        if (learnButton == null)
        {
            Debug.LogWarning("[SkillDetailPopup] Learn Button이 null입니다!");
            return;
        }
        
        // 노드 정보가 없으면 버튼 비활성화
        if (currentNode == null)
        {
            learnButton.interactable = false;
            if (learnButtonText != null)
            {
                learnButtonText.text = "View Only";
            }
            if (statusText != null)
            {
                statusText.text = "";
            }
            if (requiredLPText != null)
            {
                requiredLPText.text = "";
            }
            Debug.Log("[SkillDetailPopup] currentNode가 null → View Only 모드");
            return;
        }
        
        // 노드 상태 확인
        SkillTreeNode.SkillState state = currentNode.GetState();
        string skillName = currentSkillData != null ? currentSkillData.skillName : "Unknown";
        
        // 필요 LP 표시
        if (requiredLPText != null)
        {
            requiredLPText.text = $"Required LP: {currentNode.requiredSkillPoints}";
        }
        
        // TreeManager에서 현재 LP 확인
        SwordSkillTreeManager manager = FindObjectOfType<SwordSkillTreeManager>();
        int availableLP = manager != null ? manager.GetAvailableSkillPoints() : 0;
        
        Debug.Log($"[SkillDetailPopup] {skillName} 상태: {state}, 필요 LP: {currentNode.requiredSkillPoints}, 보유 LP: {availableLP}");
        
        switch (state)
        {
            case SkillTreeNode.SkillState.Locked:
                learnButton.interactable = false;
                if (learnButtonText != null)
                {
                    learnButtonText.text = "Locked";
                }
                if (statusText != null)
                {
                    statusText.text = "⛔ Locked (선행 스킬 필요)";
                    statusText.color = Color.gray;
                }
                Debug.Log($"[SkillDetailPopup] {skillName}: Locked → 버튼 비활성화");
                break;
                
            case SkillTreeNode.SkillState.Available:
                bool canLearn = currentNode.CanLearn();
                learnButton.interactable = canLearn;
                
                if (learnButtonText != null)
                {
                    learnButtonText.text = canLearn ? "Learn!" : "Insufficient LP";
                }
                if (statusText != null)
                {
                    if (canLearn)
                    {
                        statusText.text = "✨ Available (배울 수 있음!)";
                        statusText.color = Color.yellow;
                    }
                    else
                    {
                        statusText.text = $"⚠️ LP 부족 (필요: {currentNode.requiredSkillPoints}, 보유: {availableLP})";
                        statusText.color = Color.red;
                    }
                }
                Debug.Log($"[SkillDetailPopup] {skillName}: Available, CanLearn: {canLearn} → 버튼 {(canLearn ? "활성화" : "비활성화")}");
                break;
                
            case SkillTreeNode.SkillState.Learned:
                learnButton.interactable = false;
                if (learnButtonText != null)
                {
                    learnButtonText.text = "Learned";
                }
                if (statusText != null)
                {
                    statusText.text = "✅ Learned (이미 배움)";
                    statusText.color = Color.green;
                }
                Debug.Log($"[SkillDetailPopup] {skillName}: Learned → 버튼 비활성화");
                break;
        }
    }
    
    /// <summary>
    /// 팝업 닫기
    /// </summary>
    public void ClosePopup()
    {
        if (popupPanel == null)
        {
            Debug.LogError("[SkillDetailPopup] ❌ Popup Panel이 null입니다!");
            return;
        }
        
        Debug.Log($"[SkillDetailPopup] ❌ 팝업 닫기 - 이전 상태: {popupPanel.activeSelf}");
        
        // UI 초기화 (다음 팝업을 위해)
        ClearUI();
        
        // 패널 닫기
        popupPanel.SetActive(false);
        isPanelOpen = false;
        currentSkillData = null;
        
        Debug.Log($"[SkillDetailPopup] 팝업 닫힘 완료 - 현재 상태: {popupPanel.activeSelf}");
    }
    
    /// <summary>
    /// UI 초기화 (이전 데이터 클리어)
    /// </summary>
    private void ClearUI()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = new Color(1, 1, 1, 0); // 투명
        }
        
        if (nameText != null)
        {
            nameText.text = "";
        }
        
        if (descriptionText != null)
        {
            descriptionText.text = "";
        }
        
        if (costText != null)
        {
            costText.text = "";
        }
    }
    
    /// <summary>
    /// 팝업이 열려있는지 확인
    /// </summary>
    public bool IsPanelOpen()
    {
        return isPanelOpen;
    }
    
    private void OnDestroy()
    {
        // 버튼 이벤트 해제
        if (backgroundImage != null)
        {
            Button bgButton = backgroundImage.GetComponent<Button>();
            if (bgButton != null)
            {
                bgButton.onClick.RemoveListener(OnBackgroundClicked);
            }
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }
        
        if (learnButton != null)
        {
            learnButton.onClick.RemoveListener(OnLearnButtonClicked);
        }
    }
}


