using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 스킬 리스트 아이템 UI 컴포넌트
/// SkillListItem 프레팹에 붙여서 사용
/// </summary>
[RequireComponent(typeof(Button))]
public class SkillListItem : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("스킬 아이콘 이미지 (Icon_Image)")]
    public Image iconImage;
    
    [Tooltip("스킬 이름 텍스트 (SkillName)")]
    public TextMeshProUGUI skillNameText;
    
    [Header("Data")]
    [Tooltip("이 아이템이 표시하는 스킬 데이터")]
    private SkillData skillData;

    private Button button;
    private SelectedSkillPanelUI selectedSkillPanel;
    
    private void Awake()
    {
        // 자동으로 UI 요소 찾기
        if (iconImage == null)
        {
            Transform iconTransform = transform.Find("Icon_Image");
            if (iconTransform != null)
            {
                iconImage = iconTransform.GetComponent<Image>();
            }
        }
        
        if (skillNameText == null)
        {
            Transform nameTransform = transform.Find("SkillName");
            if (nameTransform != null)
            {
                skillNameText = nameTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        button = GetComponent<Button>();
    }
    
    /// <summary>
    /// 스킬 데이터 설정 및 UI 업데이트
    /// </summary>
    public void SetSkillData(SkillData skill)
    {
        skillData = skill;
        UpdateUI();
        SetupClickEvent();
    }
    
    /// <summary>
    /// UI 업데이트
    /// </summary>
    private void UpdateUI()
    {
        if (skillData == null)
        {
            Debug.LogWarning("[SkillListItem] SkillData가 null입니다!");
            return;
        }
        
        // 아이콘 설정
        if (iconImage != null)
        {
            if (skillData.skillIcon != null)
            {
                iconImage.sprite = skillData.skillIcon;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color = new Color(1, 1, 1, 0.3f); // 반투명
            }
        }
        
        // 이름 설정
        if (skillNameText != null)
        {
            skillNameText.text = skillData.skillName;
        }
    }

    /// <summary>
    /// 버튼 클릭 시 선택 패널에 통보
    /// </summary>
    private void SetupClickEvent()
    {
        if (button == null) return;

        if (selectedSkillPanel == null)
        {
            selectedSkillPanel = FindObjectOfType<SelectedSkillPanelUI>(true);
        }

        button.onClick.RemoveAllListeners();

        if (selectedSkillPanel != null)
        {
            button.onClick.AddListener(OnClick);
        }
        else
        {
            Debug.LogWarning("[SkillListItem] SelectedSkillPanelUI 를 찾을 수 없습니다.");
        }
    }

    private void OnClick()
    {
        if (skillData == null || selectedSkillPanel == null) return;
        selectedSkillPanel.OnSkillItemClicked(skillData);
    }
    
    /// <summary>
    /// 현재 설정된 스킬 데이터 가져오기
    /// </summary>
    public SkillData GetSkillData()
    {
        return skillData;
    }
}
