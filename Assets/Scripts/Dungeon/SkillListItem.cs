using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 스킬 리스트 아이템 UI 컴포넌트
/// SkillListItem 프레팹에 붙여서 사용
/// </summary>
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
    }
    
    /// <summary>
    /// 스킬 데이터 설정 및 UI 업데이트
    /// </summary>
    public void SetSkillData(SkillData skill)
    {
        skillData = skill;
        UpdateUI();
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
    /// 현재 설정된 스킬 데이터 가져오기
    /// </summary>
    public SkillData GetSkillData()
    {
        return skillData;
    }
}



