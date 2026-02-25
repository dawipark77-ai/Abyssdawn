using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 장비 UI를 PlayerStatData와 연동하여 자동으로 업데이트하는 컨트롤러
/// </summary>
public class EquipmentUIController : MonoBehaviour
{
    [Header("Data Reference")]
    [Tooltip("플레이어 데이터 에셋 - 장비 정보를 가져옴")]
    public PlayerStatData playerStatData;

    [Header("UI Slot References")]
    [Tooltip("Hand1 슬롯 (Image 컴포넌트가 있는 오브젝트)")]
    public GameObject hand1Slot;
    
    [Tooltip("Hand2 슬롯 (Image 컴포넌트가 있는 오브젝트)")]
    public GameObject hand2Slot;
    
    [Tooltip("Armour 슬롯 (Image 컴포넌트가 있는 오브젝트)")]
    public GameObject armourSlot;
    
    [Tooltip("Accessory1 슬롯 (Image 컴포넌트가 있는 오브젝트)")]
    public GameObject accessory1Slot;
    
    [Tooltip("Accessory2 슬롯 (Image 컴포넌트가 있는 오브젝트)")]
    public GameObject accessory2Slot;

    [Header("Empty Slot Settings")]
    [Tooltip("장비가 없을 때 표시할 기본 스프라이트 (선택 사항)")]
    public Sprite emptySlotSprite;
    
    [Tooltip("장비가 없을 때 표시할 텍스트")]
    public string emptySlotText = "Empty";

    void Start()
    {
        RefreshAllSlots();
    }

    /// <summary>
    /// 모든 장비 슬롯 UI를 업데이트합니다.
    /// </summary>
    public void RefreshAllSlots()
    {
        if (playerStatData == null)
        {
            Debug.LogWarning("[EquipmentUIController] PlayerStatData가 설정되지 않았습니다.");
            return;
        }

        UpdateSlot(hand1Slot, playerStatData.rightHand);
        UpdateSlot(hand2Slot, playerStatData.leftHand);
        UpdateSlot(armourSlot, playerStatData.body);
        UpdateSlot(accessory1Slot, playerStatData.accessory1);
        UpdateSlot(accessory2Slot, playerStatData.accessory2);

        Debug.Log("[EquipmentUIController] 모든 장비 슬롯 UI를 업데이트했습니다.");
    }

    /// <summary>
    /// 특정 슬롯의 UI를 업데이트합니다.
    /// </summary>
    /// <param name="slotObject">슬롯 GameObject (Image 컴포넌트가 있는 오브젝트)</param>
    /// <param name="equipment">표시할 장비 데이터 (null이면 빈 슬롯)</param>
    private void UpdateSlot(GameObject slotObject, EquipmentData equipment)
    {
        if (slotObject == null)
        {
            Debug.LogWarning("[EquipmentUIController] 슬롯 오브젝트가 null입니다.");
            return;
        }

        // Image 컴포넌트 가져오기
        Image slotImage = slotObject.GetComponent<Image>();
        if (slotImage == null)
        {
            Debug.LogWarning($"[EquipmentUIController] {slotObject.name}에 Image 컴포넌트가 없습니다.");
            return;
        }

        // 자식 Text (TMP) 컴포넌트 가져오기
        TextMeshProUGUI slotText = slotObject.GetComponentInChildren<TextMeshProUGUI>();
        if (slotText == null)
        {
            Debug.LogWarning($"[EquipmentUIController] {slotObject.name}의 자식에 TextMeshProUGUI 컴포넌트가 없습니다.");
        }

        // 장비 정보 업데이트
        if (equipment != null)
        {
            // 장비가 있을 때
            if (equipment.equipmentIcon != null)
            {
                slotImage.sprite = equipment.equipmentIcon;
                slotImage.color = Color.white; // 불투명하게
            }
            else
            {
                slotImage.sprite = emptySlotSprite;
                slotImage.color = new Color(1f, 1f, 1f, 0.5f); // 반투명
            }

            if (slotText != null)
            {
                slotText.text = equipment.equipmentName;
            }
        }
        else
        {
            // 장비가 없을 때 (빈 슬롯)
            slotImage.sprite = emptySlotSprite;
            slotImage.color = new Color(1f, 1f, 1f, 0.3f); // 더 투명하게
            
            if (slotText != null)
            {
                slotText.text = emptySlotText;
            }
        }
    }

    /// <summary>
    /// 특정 슬롯만 업데이트합니다.
    /// </summary>
    public void RefreshSlot(string slotName)
    {
        if (playerStatData == null) return;

        switch (slotName)
        {
            case "Hand1":
            case "RightHand":
                UpdateSlot(hand1Slot, playerStatData.rightHand);
                break;
            case "Hand2":
            case "LeftHand":
                UpdateSlot(hand2Slot, playerStatData.leftHand);
                break;
            case "Armour":
            case "Body":
                UpdateSlot(armourSlot, playerStatData.body);
                break;
            case "Accessory1":
                UpdateSlot(accessory1Slot, playerStatData.accessory1);
                break;
            case "Accessory2":
                UpdateSlot(accessory2Slot, playerStatData.accessory2);
                break;
            default:
                Debug.LogWarning($"[EquipmentUIController] 알 수 없는 슬롯 이름: {slotName}");
                break;
        }
    }
}





