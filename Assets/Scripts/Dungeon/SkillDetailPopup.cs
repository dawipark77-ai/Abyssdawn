using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 스킬 상세 정보 팝업 컨트롤러
/// </summary>
public class SkillDetailPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("팝업을 닫을 배경 이미지")]
    public Image backgroundImage;
    
    [Tooltip("닫기 버튼")]
    public Button closeButton;
    
    [Header("Skill Info Display")]
    [Tooltip("스킬 아이콘 이미지")]
    public Image iconImage;
    
    [Tooltip("스킬 이름 텍스트")]
    public TextMeshProUGUI nameText;
    
    [Tooltip("스킬 설명 텍스트")]
    public TextMeshProUGUI descriptionText;
    
    [Tooltip("코스트 텍스트")]
    public TextMeshProUGUI costText;
    
    [Header("Optional: ESC Key")]
    [Tooltip("ESC 키로 팝업 닫기")]
    public bool enableEscapeKey = true;
    
    private SkillData currentSkillData;
    
    private void Awake()
    {
        // 배경 이미지에 Button 컴포넌트 추가 (클릭으로 닫기)
        if (backgroundImage != null)
        {
            Button bgButton = backgroundImage.GetComponent<Button>();
            if (bgButton == null)
            {
                bgButton = backgroundImage.gameObject.AddComponent<Button>();
            }
            bgButton.onClick.AddListener(ClosePopup);
        }
        
        // 닫기 버튼 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }
        
        // 초기 상태: 비활성화
        gameObject.SetActive(false);
    }
    
    private void Update()
    {
        // ESC 키로 닫기
        if (enableEscapeKey && Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameObject.activeSelf)
            {
                ClosePopup();
            }
        }
    }
    
    /// <summary>
    /// 스킬 정보를 표시하며 팝업 열기
    /// </summary>
    public void ShowSkillDetail(SkillData skillData)
    {
        if (skillData == null)
        {
            Debug.LogWarning("[SkillDetailPopup] SkillData가 null입니다!");
            return;
        }
        
        currentSkillData = skillData;
        
        // 스킬 정보 표시
        UpdateSkillInfo();
        
        // 팝업 열기
        gameObject.SetActive(true);
        
        Debug.Log($"[SkillDetailPopup] {skillData.skillName} 정보 표시");
    }
    
    /// <summary>
    /// 스킬 정보 UI 업데이트
    /// </summary>
    private void UpdateSkillInfo()
    {
        if (currentSkillData == null) return;
        
        // 아이콘 설정
        if (iconImage != null && currentSkillData.skillIcon != null)
        {
            iconImage.sprite = currentSkillData.skillIcon;
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
                costInfo = "Cost: None";
            }
            else
            {
                costInfo = "Cost: " + costInfo;
            }
            
            costText.text = costInfo;
        }
    }
    
    /// <summary>
    /// 팝업 닫기
    /// </summary>
    public void ClosePopup()
    {
        gameObject.SetActive(false);
        Debug.Log("[SkillDetailPopup] 팝업 닫힘");
    }
    
    /// <summary>
    /// 팝업이 열려있는지 확인
    /// </summary>
    public bool IsOpen()
    {
        return gameObject.activeSelf;
    }
    
    private void OnDestroy()
    {
        // 이벤트 해제
        if (backgroundImage != null)
        {
            Button bgButton = backgroundImage.GetComponent<Button>();
            if (bgButton != null)
            {
                bgButton.onClick.RemoveListener(ClosePopup);
            }
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }
    }
}

