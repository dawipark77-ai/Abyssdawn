using UnityEngine;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// 플레이어의 장비 장착을 관리하는 클래스
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    [Header("Equipment Data Reference")]
    [Tooltip("플레이어 데이터 에셋 - 장비 정보가 여기에 저장됨")]
    public PlayerStatData playerStatData;

    [Header("UI Reference")]
    [Tooltip("장비 UI 컨트롤러 - 장비 변경 시 자동으로 UI 업데이트")]
    public EquipmentUIController equipmentUIController;

    [Header("Equipment Slots (Runtime)")]
    [Tooltip("오른손 장비 - playerStatData에서 로드됨")]
    public EquipmentData rightHand;
    [Tooltip("왼손 장비 - playerStatData에서 로드됨")]
    public EquipmentData leftHand;
    [Tooltip("몸통 장비 - playerStatData에서 로드됨")]
    public EquipmentData body;
    [Tooltip("장신구 1 - playerStatData에서 로드됨")]
    public EquipmentData accessory1;
    [Tooltip("장신구 2 - playerStatData에서 로드됨")]
    public EquipmentData accessory2;

    private PlayerStats playerStats;

    void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogError("[EquipmentManager] PlayerStats 컴포넌트를 찾을 수 없습니다!");
        }

        // PlayerStatData에서 장비 로드
        LoadEquipmentFromData();
    }

    void Start()
    {
        // 시작 시 장비 보정치 반영
        RefreshStats();
        
        // PlayerStats에 스탯 업데이트 알림
        if (playerStats != null)
        {
            playerStats.NotifyStatusChanged();
            Debug.Log("[EquipmentManager] PlayerStats에 스탯 변경 이벤트 발동");
        }
    }

    /// <summary>
    /// PlayerStatData에서 장비 정보를 로드합니다.
    /// </summary>
    private void LoadEquipmentFromData()
    {
        if (playerStatData == null)
        {
            Debug.LogWarning("[EquipmentManager] PlayerStatData가 설정되지 않았습니다. 장비를 로드할 수 없습니다.");
            return;
        }

        rightHand = playerStatData.rightHand;
        leftHand = playerStatData.leftHand;
        body = playerStatData.body;
        accessory1 = playerStatData.accessory1;
        accessory2 = playerStatData.accessory2;

        // 양손 무기 규칙 강제 (인스펙터 직접 편집 방어)
        if (rightHand != null && rightHand.isTwoHanded && leftHand != null)
        {
            Debug.Log($"[EquipmentManager] Two-handed rule enforced on load: unequipping '{leftHand.equipmentName}' from left hand.");
            leftHand = null;
            playerStatData.leftHand = null;
        }

        Debug.Log("[EquipmentManager] PlayerStatData에서 장비를 로드했습니다.");
        Debug.Log($"  - Right Hand: {(rightHand != null ? rightHand.equipmentName : "None")}");
        Debug.Log($"  - Left Hand: {(leftHand != null ? leftHand.equipmentName : "None")}");
        Debug.Log($"  - Body: {(body != null ? body.equipmentName : "None")}");
        Debug.Log($"  - Accessory 1: {(accessory1 != null ? accessory1.equipmentName : "None")}");
        Debug.Log($"  - Accessory 2: {(accessory2 != null ? accessory2.equipmentName : "None")}");
        
        // 장비 보너스 합산 로그
        int totalAttack = GetTotalAttackBonus();
        int totalDefense = GetTotalDefenseBonus();
        int totalMagic = GetTotalMagicBonus();
        int totalHP = GetTotalHPBonus();
        int totalMP = GetTotalMPBonus();
        int totalAgi = GetTotalAgiBonus();
        int totalLuck = GetTotalLuckBonus();
        
        Debug.Log($"[EquipmentManager] 총 장비 보너스:");
        Debug.Log($"  - Attack: +{totalAttack}");
        Debug.Log($"  - Defense: +{totalDefense}");
        Debug.Log($"  - Magic: +{totalMagic}");
        Debug.Log($"  - HP: +{totalHP}");
        Debug.Log($"  - MP: +{totalMP}");
        Debug.Log($"  - Agility: {(totalAgi >= 0 ? "+" : "")}{totalAgi}");
        Debug.Log($"  - Luck: +{totalLuck}");
    }
    
    // 디버그용 보너스 합산 메서드들
    private int GetTotalAttackBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.attackBonus;
        if (leftHand != null) total += leftHand.attackBonus;
        if (body != null) total += body.attackBonus;
        if (accessory1 != null) total += accessory1.attackBonus;
        if (accessory2 != null) total += accessory2.attackBonus;
        return total;
    }
    
    private int GetTotalDefenseBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.defenseBonus;
        if (leftHand != null) total += leftHand.defenseBonus;
        if (body != null) total += body.defenseBonus;
        if (accessory1 != null) total += accessory1.defenseBonus;
        if (accessory2 != null) total += accessory2.defenseBonus;
        return total;
    }
    
    private int GetTotalMagicBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.magicBonus;
        if (leftHand != null) total += leftHand.magicBonus;
        if (body != null) total += body.magicBonus;
        if (accessory1 != null) total += accessory1.magicBonus;
        if (accessory2 != null) total += accessory2.magicBonus;
        return total;
    }
    
    private int GetTotalHPBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.hpBonus;
        if (leftHand != null) total += leftHand.hpBonus;
        if (body != null) total += body.hpBonus;
        if (accessory1 != null) total += accessory1.hpBonus;
        if (accessory2 != null) total += accessory2.hpBonus;
        return total;
    }
    
    private int GetTotalMPBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.mpBonus;
        if (leftHand != null) total += leftHand.mpBonus;
        if (body != null) total += body.mpBonus;
        if (accessory1 != null) total += accessory1.mpBonus;
        if (accessory2 != null) total += accessory2.mpBonus;
        return total;
    }
    
    private int GetTotalAgiBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.agiBonus;
        if (leftHand != null) total += leftHand.agiBonus;
        if (body != null) total += body.agiBonus;
        if (accessory1 != null) total += accessory1.agiBonus;
        if (accessory2 != null) total += accessory2.agiBonus;
        return total;
    }
    
    private int GetTotalLuckBonus()
    {
        int total = 0;
        if (rightHand != null) total += rightHand.luckBonus;
        if (leftHand != null) total += leftHand.luckBonus;
        if (body != null) total += body.luckBonus;
        if (accessory1 != null) total += accessory1.luckBonus;
        if (accessory2 != null) total += accessory2.luckBonus;
        return total;
    }

    /// <summary>
    /// 현재 장비 상태를 PlayerStatData에 저장합니다.
    /// </summary>
    private void SaveEquipmentToData()
    {
        if (playerStatData == null) return;

        playerStatData.rightHand = rightHand;
        playerStatData.leftHand = leftHand;
        playerStatData.body = body;
        playerStatData.accessory1 = accessory1;
        playerStatData.accessory2 = accessory2;

        Debug.Log("[EquipmentManager] 장비를 PlayerStatData에 저장했습니다.");

        // UI 업데이트
        if (equipmentUIController != null)
        {
            equipmentUIController.RefreshAllSlots();
        }
    }

    /// <summary>
    /// 장비를 장착합니다.
    /// </summary>
    /// <param name="equipment">장착할 장비 데이터</param>
    /// <returns>장착 성공 여부</returns>
    public bool EquipItem(EquipmentData equipment)
    {
        if (equipment == null)
        {
            Debug.LogWarning("[EquipmentManager] 장착하려는 장비가 null입니다.");
            return false;
        }

        switch (equipment.equipmentType)
        {
            case EquipmentType.Hand:
                // 한손 무기: 오른손 우선, 차있으면 왼손에 장착
                // 양손 무기가 장착되어 있으면 해제
                if (rightHand != null && rightHand.equipmentType == EquipmentType.TwoHanded)
                {
                    Debug.Log($"[EquipmentManager] 양손 무기 {rightHand.equipmentName}을(를) 해제하고 {equipment.equipmentName}을(를) 장착합니다.");
                    rightHand = equipment;
                }
                else if (rightHand == null)
                {
                    rightHand = equipment;
                    Debug.Log($"[EquipmentManager] {equipment.equipmentName}을(를) 오른손에 장착합니다.");
                }
                else if (leftHand == null)
                {
                    leftHand = equipment;
                    Debug.Log($"[EquipmentManager] {equipment.equipmentName}을(를) 왼손에 장착합니다.");
                }
                else
                {
                    // 오른손 장비를 교체
                    Debug.Log($"[EquipmentManager] {rightHand.equipmentName}을(를) 해제하고 {equipment.equipmentName}을(를) 오른손에 장착합니다.");
                    rightHand = equipment;
                }
                break;

            case EquipmentType.TwoHanded:
                // 양손 무기: 오른손에 장착, 왼손은 비워야 함
                if (leftHand != null)
                {
                    Debug.Log($"[EquipmentManager] 양손 무기 장착을 위해 {leftHand.equipmentName}을(를) 해제합니다.");
                    leftHand = null;
                }
                if (rightHand != null)
                {
                    Debug.Log($"[EquipmentManager] {rightHand.equipmentName}을(를) 해제하고 {equipment.equipmentName}을(를) 장착합니다.");
                }
                rightHand = equipment;
                Debug.Log($"[EquipmentManager] 양손 무기 {equipment.equipmentName} 장착 완료!");
                break;

            case EquipmentType.Armour:
                if (body != null)
                {
                    Debug.Log($"[EquipmentManager] {body.equipmentName}을(를) 해제하고 {equipment.equipmentName}을(를) 장착합니다.");
                }
                body = equipment;
                break;

            case EquipmentType.Accessory:
                // Accessory는 빈 슬롯에 자동으로 장착
                if (accessory1 == null)
                {
                    accessory1 = equipment;
                }
                else if (accessory2 == null)
                {
                    accessory2 = equipment;
                }
                else
                {
                    Debug.LogWarning("[EquipmentManager] 장신구 슬롯이 모두 찼습니다. 먼저 해제해주세요.");
                    return false;
                }
                break;
        }

        RefreshStats();
        SaveEquipmentToData(); // 장비 변경 시 데이터에 저장
        Debug.Log($"[EquipmentManager] {equipment.equipmentName} 장착 완료!");
        return true;
    }

    /// <summary>
    /// 장비를 해제합니다.
    /// </summary>
    /// <param name="slot">해제할 슬롯 ("RightHand", "LeftHand", "Body", "Accessory1", "Accessory2")</param>
    /// <returns>해제 성공 여부</returns>
    public bool UnequipItem(string slot)
    {
        EquipmentData unequippedItem = null;

        switch (slot)
        {
            case "RightHand":
                unequippedItem = rightHand;
                rightHand = null;
                break;
            case "LeftHand":
                unequippedItem = leftHand;
                leftHand = null;
                break;
            case "Body":
                unequippedItem = body;
                body = null;
                break;
            case "Accessory1":
                unequippedItem = accessory1;
                accessory1 = null;
                break;
            case "Accessory2":
                unequippedItem = accessory2;
                accessory2 = null;
                break;
        }

        if (unequippedItem != null)
        {
            RefreshStats();
            SaveEquipmentToData(); // 장비 변경 시 데이터에 저장
            Debug.Log($"[EquipmentManager] {unequippedItem.equipmentName} 해제 완료!");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 장착된 모든 장비의 보정치를 합산하여 PlayerStats에 반영합니다.
    /// </summary>
    public void RefreshStats()
    {
        if (playerStats == null) return;

        // PlayerStats의 GetEquipmentAgilityBonus, GetEquipmentLuckBonus, GetEquipmentAccuracyBonus가
        // 이 메서드를 통해 장비 보정치를 가져오도록 구현되어 있습니다.
        // 실제 계산은 PlayerStats의 해당 메서드에서 수행됩니다.

        // 스탯 변경 이벤트 발동 (UI 업데이트용)
        playerStats.NotifyStatusChanged();
    }

    /// <summary>
    /// 장착된 모든 장비 리스트를 반환합니다.
    /// </summary>
    public List<EquipmentData> GetEquippedItems()
    {
        List<EquipmentData> items = new List<EquipmentData>();
        if (rightHand != null) items.Add(rightHand);
        if (leftHand != null) items.Add(leftHand);
        if (body != null) items.Add(body);
        if (accessory1 != null) items.Add(accessory1);
        if (accessory2 != null) items.Add(accessory2);
        return items;
    }

    /// <summary>
    /// 특정 타입의 장비가 장착되어 있는지 확인합니다.
    /// </summary>
    public bool HasEquipment(EquipmentType type)
    {
        switch (type)
        {
            case EquipmentType.Hand:
            case EquipmentType.TwoHanded:
                return rightHand != null || leftHand != null;
            case EquipmentType.Armour:
                return body != null;
            case EquipmentType.Accessory:
                return accessory1 != null || accessory2 != null;
            default:
                return false;
        }
    }

    /// <summary>
    /// 양손 무기가 장착되어 있는지 확인합니다.
    /// </summary>
    public bool IsTwoHandedEquipped()
    {
        return rightHand != null && rightHand.equipmentType == EquipmentType.TwoHanded;
    }
}

