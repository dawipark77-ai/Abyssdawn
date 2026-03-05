using UnityEngine;
using UnityEngine.UI;
using AbyssdawnBattle;

/// <summary>
/// 선택된 스킬 패널 열기/닫기 + 배틀 세트 선택 관리
/// </summary>
public class SelectedSkillPanelUI : MonoBehaviour
{
    [Header("데이터")]
    [Tooltip("HeroData.asset을 연결 (없으면 Resources/HeroData 에서 자동 로드)")]
    public PlayerStatData playerStatData;

    [Header("패시브 슬롯")]
    public Image[] passiveSlotImages;
    public Button[] passiveSlotButtons;

    [Header("액티브 슬롯")]
    public Image[] activeSlotImages;
    public Button[] activeSlotButtons;

    [Header("색상 설정")]
    public Color normalSlotColor = Color.white;
    public Color highlightSlotColor = new Color(1f, 0.95f, 0.5f, 1f);
    public Color disabledSlotColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    public Color emptyIconColor = new Color(1f, 1f, 1f, 0.2f);

    private SkillData currentSelectedSkill;

    private void Awake()
    {
        // PlayerStatData 자동 로드
        if (playerStatData == null)
        {
            playerStatData = Resources.Load<PlayerStatData>("HeroData");
            if (playerStatData == null)
            {
                playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
            }
        }

        // 슬롯 버튼 클릭 이벤트 연결
        if (passiveSlotButtons != null)
        {
            for (int i = 0; i < passiveSlotButtons.Length; i++)
            {
                int index = i;
                if (passiveSlotButtons[i] != null)
                {
                    passiveSlotButtons[i].onClick.AddListener(() => OnPassiveSlotClicked(index));
                }
            }
        }

        if (activeSlotButtons != null)
        {
            for (int i = 0; i < activeSlotButtons.Length; i++)
            {
                int index = i;
                if (activeSlotButtons[i] != null)
                {
                    activeSlotButtons[i].onClick.AddListener(() => OnActiveSlotClicked(index));
                }
            }
        }
    }

    private void OnEnable()
    {
        RefreshSlotsFromPlayerData();
        ClearSelection();
    }

    /// <summary>
    /// 패널을 보이게 하고 싶을 때 호출
    /// (다른 버튼이나 코드에서 사용할 수 있음)
    /// </summary>
    public void OpenPanel()
    {
        gameObject.SetActive(true);
        RefreshSlotsFromPlayerData();
        ClearSelection();
    }

    /// <summary>
    /// CLOSE 버튼의 OnClick 에 연결할 함수
    /// </summary>
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 좌측 스킬 리스트 아이템을 탭했을 때 호출
    /// </summary>
    public void OnSkillItemClicked(SkillData skill)
    {
        if (skill == null) return;

        currentSelectedSkill = skill;
        UpdateSlotHighlights();
    }

    /// <summary>
    /// PlayerStatData 의 장착 정보로부터 슬롯 UI 갱신
    /// </summary>
    private void RefreshSlotsFromPlayerData()
    {
        if (playerStatData == null) return;

        // 패시브
        if (passiveSlotImages != null)
        {
            for (int i = 0; i < passiveSlotImages.Length; i++)
            {
                SkillData skill = null;
                if (playerStatData.equippedPassives != null && i < playerStatData.equippedPassives.Count)
                {
                    skill = playerStatData.equippedPassives[i];
                }

                UpdateSlotIcon(passiveSlotImages[i], skill);
            }
        }

        // 액티브
        if (activeSlotImages != null)
        {
            for (int i = 0; i < activeSlotImages.Length; i++)
            {
                SkillData skill = null;
                if (playerStatData.equippedSkills != null && i < playerStatData.equippedSkills.Count)
                {
                    skill = playerStatData.equippedSkills[i];
                }

                UpdateSlotIcon(activeSlotImages[i], skill);
            }
        }

        ResetSlotColors();
    }

    private void UpdateSlotIcon(Image slotImage, SkillData skill)
    {
        if (slotImage == null) return;

        if (skill != null && skill.skillIcon != null)
        {
            slotImage.sprite = skill.skillIcon;
            slotImage.color = normalSlotColor;
        }
        else
        {
            slotImage.sprite = null;
            slotImage.color = emptyIconColor;
        }
    }

    private void ResetSlotColors()
    {
        if (passiveSlotImages != null)
        {
            foreach (var img in passiveSlotImages)
            {
                if (img != null) img.color = (img.sprite != null ? normalSlotColor : emptyIconColor);
            }
        }

        if (activeSlotImages != null)
        {
            foreach (var img in activeSlotImages)
            {
                if (img != null) img.color = (img.sprite != null ? normalSlotColor : emptyIconColor);
            }
        }
    }

    private void ClearSelection()
    {
        currentSelectedSkill = null;
        ResetSlotColors();
    }

    /// <summary>
    /// 현재 선택된 스킬에 따라 어떤 슬롯이 유효한지 하이라이트
    /// </summary>
    private void UpdateSlotHighlights()
    {
        ResetSlotColors();

        if (currentSelectedSkill == null)
        {
            return;
        }

        bool isPassive = currentSelectedSkill.IsPassive;

        // 패시브 슬롯 하이라이트
        if (passiveSlotImages != null)
        {
            foreach (var img in passiveSlotImages)
            {
                if (img == null) continue;
                img.color = isPassive ? highlightSlotColor : disabledSlotColor;
            }
        }

        // 액티브 슬롯 하이라이트
        if (activeSlotImages != null)
        {
            foreach (var img in activeSlotImages)
            {
                if (img == null) continue;
                img.color = isPassive ? disabledSlotColor : highlightSlotColor;
            }
        }
    }

    private void OnPassiveSlotClicked(int index)
    {
        if (currentSelectedSkill == null) return;
        if (!currentSelectedSkill.IsPassive)
        {
            Debug.Log("[SelectedSkillPanelUI] 액티브 스킬은 패시브 슬롯에 넣을 수 없습니다.");
            return;
        }

        if (playerStatData == null) return;

        EnsureListSize(playerStatData.equippedPassives, passiveSlotImages != null ? passiveSlotImages.Length : index + 1);
        playerStatData.equippedPassives[index] = currentSelectedSkill;

        if (passiveSlotImages != null && index >= 0 && index < passiveSlotImages.Length)
        {
            UpdateSlotIcon(passiveSlotImages[index], currentSelectedSkill);
        }

        Debug.Log($"[SelectedSkillPanelUI] 패시브 슬롯 {index} 에 {currentSelectedSkill.skillName} 장착");
        ClearSelection();
    }

    private void OnActiveSlotClicked(int index)
    {
        if (currentSelectedSkill == null) return;
        if (!currentSelectedSkill.IsActive)
        {
            Debug.Log("[SelectedSkillPanelUI] 패시브 스킬은 액티브 슬롯에 넣을 수 없습니다.");
            return;
        }

        if (playerStatData == null) return;

        EnsureListSize(playerStatData.equippedSkills, activeSlotImages != null ? activeSlotImages.Length : index + 1);
        playerStatData.equippedSkills[index] = currentSelectedSkill;

        if (activeSlotImages != null && index >= 0 && index < activeSlotImages.Length)
        {
            UpdateSlotIcon(activeSlotImages[index], currentSelectedSkill);
        }

        Debug.Log($"[SelectedSkillPanelUI] 액티브 슬롯 {index} 에 {currentSelectedSkill.skillName} 장착");
        ClearSelection();
    }

    private void EnsureListSize(System.Collections.Generic.List<SkillData> list, int size)
    {
        if (list == null) return;
        while (list.Count < size)
        {
            list.Add(null);
        }
    }
}

