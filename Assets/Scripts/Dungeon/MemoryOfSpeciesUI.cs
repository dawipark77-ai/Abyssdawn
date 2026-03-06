using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 종의 기억 UI 관리 스크립트
/// HeroData의 종의 기억 3개와 활성화된 특성을 UI에 표시
/// </summary>
public class MemoryOfSpeciesUI : MonoBehaviour
{
    [Header("PlayerData Reference")]
    [Tooltip("HeroData.asset을 연결")]
    public PlayerStatData playerStatData;

    [Header("Memory Slots (슬롯 이미지)")]
    [Tooltip("종의 기억 슬롯 1 이미지")]
    public Image slot1Image;
    
    [Tooltip("종의 기억 슬롯 2 이미지")]
    public Image slot2Image;
    
    [Tooltip("종의 기억 슬롯 3 이미지")]
    public Image slot3Image;

    [Header("Trait Display (특성 표시)")]
    [Tooltip("특성 이미지 (나중에 사용)")]
    public Image traitsImage;
    
    [Tooltip("특성 텍스트")]
    public TextMeshProUGUI traitsText;

    [Header("Empty Slot Settings")]
    [Tooltip("빈 슬롯일 때 표시할 색상")]
    public Color emptySlotColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    
    [Tooltip("채워진 슬롯일 때 표시할 색상")]
    public Color filledSlotColor = Color.white;

    private void Start()
    {
        UpdateUI();
    }

    private void OnEnable()
    {
        UpdateUI();
    }

    /// <summary>
    /// UI 업데이트 - HeroData의 데이터를 읽어서 표시
    /// </summary>
    public void UpdateUI()
    {
        if (playerStatData == null)
        {
            Debug.LogWarning("[MemoryOfSpeciesUI] PlayerStatData가 연결되지 않았습니다!");
            return;
        }

        // 슬롯 1 업데이트
        UpdateSlot(slot1Image, playerStatData.memorySlot1);
        
        // 슬롯 2 업데이트
        UpdateSlot(slot2Image, playerStatData.memorySlot2);
        
        // 슬롯 3 업데이트
        UpdateSlot(slot3Image, playerStatData.memorySlot3);

        // 특성 업데이트
        UpdateTrait(playerStatData.activeTrait);
    }

    /// <summary>
    /// 개별 슬롯 업데이트
    /// </summary>
    private void UpdateSlot(Image slotImage, MemoryOfSpeciesData memoryData)
    {
        if (slotImage == null) return;

        if (memoryData != null && memoryData.memoryIcon != null)
        {
            // 종의 기억이 장착되어 있고 아이콘이 있는 경우
            slotImage.sprite = memoryData.memoryIcon;
            slotImage.color = filledSlotColor;
        }
        else
        {
            // 비어있거나 아이콘이 없는 경우
            slotImage.sprite = null;
            slotImage.color = emptySlotColor;
        }
    }

    /// <summary>
    /// 특성 표시 업데이트
    /// </summary>
    private void UpdateTrait(TraitsOfSpeciesData traitData)
    {
        if (traitsText == null) return;

        if (traitData != null)
        {
            // 특성이 활성화된 경우
            traitsText.text = traitData.traitNameEnglish;
            
            // 나중에 아이콘 추가 시
            if (traitsImage != null && traitData.traitIcon != null)
            {
                traitsImage.sprite = traitData.traitIcon;
                traitsImage.color = filledSlotColor;
                traitsImage.gameObject.SetActive(true);
            }
            else if (traitsImage != null)
            {
                traitsImage.gameObject.SetActive(false);
            }
        }
        else
        {
            // 특성이 비활성화된 경우
            traitsText.text = "---";
            
            if (traitsImage != null)
            {
                traitsImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 에디터에서 값 변경 시 자동 업데이트 (테스트용)
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateUI();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터 전용 - Inspector 컨텍스트 메뉴에서 수동 업데이트
    /// </summary>
    [ContextMenu("Update UI (Force)")]
    private void ForceUpdateUI()
    {
        UpdateUI();
        Debug.Log("[MemoryOfSpeciesUI] UI 강제 업데이트 완료!");
    }

    /// <summary>
    /// 에디터 전용 - 자동 연결 시도
    /// </summary>
    [ContextMenu("Auto Connect UI Elements")]
    private void AutoConnectUIElements()
    {
        // Slot1, Slot2, Slot3 찾기
        Transform slot1 = transform.Find("Slot1");
        Transform slot2 = transform.Find("Slot2");
        Transform slot3 = transform.Find("Slot3");
        
        if (slot1 != null) slot1Image = slot1.GetComponent<Image>();
        if (slot2 != null) slot2Image = slot2.GetComponent<Image>();
        if (slot3 != null) slot3Image = slot3.GetComponent<Image>();

        // Traits 찾기
        Transform traits = transform.Find("Traits");
        if (traits != null)
        {
            Transform traitsImageTransform = traits.Find("Traits_Image");
            Transform traitsTextTransform = traits.Find("Traits_Text");
            
            if (traitsImageTransform != null) traitsImage = traitsImageTransform.GetComponent<Image>();
            if (traitsTextTransform != null) traitsText = traitsTextTransform.GetComponent<TextMeshProUGUI>();
        }

        // HeroData 자동 로드
        if (playerStatData == null)
        {
            playerStatData = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerStatData>("Assets/HeroData.asset");
        }

        Debug.Log("[MemoryOfSpeciesUI] 자동 연결 완료!");
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}






