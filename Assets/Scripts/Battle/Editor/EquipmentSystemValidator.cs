using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// 장비 시스템이 제대로 설정되어 있는지 검증하는 에디터 툴
/// </summary>
public class EquipmentSystemValidator : EditorWindow
{
    [MenuItem("Tools/Validate Equipment System")]
    public static void ShowWindow()
    {
        GetWindow<EquipmentSystemValidator>("Equipment System Validator");
    }

    void OnGUI()
    {
        GUILayout.Label("Equipment System Validation", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Check Current Scene", GUILayout.Height(30)))
        {
            ValidateCurrentScene();
        }

        GUILayout.Space(10);
        
        if (GUILayout.Button("Auto-Fix Equipment System", GUILayout.Height(30)))
        {
            AutoFixEquipmentSystem();
        }
    }

    private void ValidateCurrentScene()
    {
        Debug.Log("========== Equipment System Validation ==========");
        
        // PlayerStats 찾기
        PlayerStats[] allPlayers = FindObjectsOfType<PlayerStats>();
        
        if (allPlayers.Length == 0)
        {
            Debug.LogError("[Validation] ✗ PlayerStats를 찾을 수 없습니다!");
            return;
        }

        foreach (var player in allPlayers)
        {
            Debug.Log($"\n[Validation] Checking: {player.gameObject.name}");
            
            // 1. PlayerStatData 연결 확인
            if (player.statData == null)
            {
                Debug.LogError($"  ✗ statData가 연결되지 않았습니다!", player);
            }
            else
            {
                Debug.Log($"  ✓ statData: {player.statData.name}");
            }

            // 2. EquipmentManager 확인
            EquipmentManager equipMgr = player.GetComponent<EquipmentManager>();
            if (equipMgr == null)
            {
                Debug.LogError($"  ✗ EquipmentManager 컴포넌트가 없습니다!", player);
            }
            else
            {
                Debug.Log($"  ✓ EquipmentManager 존재");
                
                // 3. EquipmentManager의 PlayerStatData 연결 확인
                if (equipMgr.playerStatData == null)
                {
                    Debug.LogError($"  ✗ EquipmentManager의 playerStatData가 연결되지 않았습니다!", equipMgr);
                }
                else if (equipMgr.playerStatData != player.statData)
                {
                    Debug.LogWarning($"  ⚠ EquipmentManager와 PlayerStats의 statData가 다릅니다!", equipMgr);
                }
                else
                {
                    Debug.Log($"  ✓ EquipmentManager.playerStatData: {equipMgr.playerStatData.name}");
                    
                    // 4. 장비 로드 확인
                    Debug.Log($"  장비 상태:");
                    Debug.Log($"    - Right Hand: {(equipMgr.playerStatData.rightHand != null ? equipMgr.playerStatData.rightHand.equipmentName : "None")}");
                    Debug.Log($"    - Left Hand: {(equipMgr.playerStatData.leftHand != null ? equipMgr.playerStatData.leftHand.equipmentName : "None")}");
                    Debug.Log($"    - Body: {(equipMgr.playerStatData.body != null ? equipMgr.playerStatData.body.equipmentName : "None")}");
                    Debug.Log($"    - Accessory 1: {(equipMgr.playerStatData.accessory1 != null ? equipMgr.playerStatData.accessory1.equipmentName : "None")}");
                    Debug.Log($"    - Accessory 2: {(equipMgr.playerStatData.accessory2 != null ? equipMgr.playerStatData.accessory2.equipmentName : "None")}");
                }
            }
        }

        Debug.Log("\n========== Validation Complete ==========");
    }

    private void AutoFixEquipmentSystem()
    {
        Debug.Log("========== Auto-Fixing Equipment System ==========");
        
        PlayerStats[] allPlayers = FindObjectsOfType<PlayerStats>();
        
        if (allPlayers.Length == 0)
        {
            Debug.LogError("[Auto-Fix] PlayerStats를 찾을 수 없습니다!");
            return;
        }

        foreach (var player in allPlayers)
        {
            Debug.Log($"\n[Auto-Fix] Fixing: {player.gameObject.name}");
            
            // EquipmentManager 추가 또는 확인
            EquipmentManager equipMgr = player.GetComponent<EquipmentManager>();
            if (equipMgr == null)
            {
                equipMgr = player.gameObject.AddComponent<EquipmentManager>();
                Debug.Log($"  ✓ EquipmentManager 컴포넌트 추가됨");
            }

            // PlayerStatData 자동 연결
            if (player.statData != null && equipMgr.playerStatData == null)
            {
                equipMgr.playerStatData = player.statData;
                EditorUtility.SetDirty(equipMgr);
                Debug.Log($"  ✓ EquipmentManager.playerStatData 자동 연결: {player.statData.name}");
            }
        }

        Debug.Log("\n========== Auto-Fix Complete ==========");
        Debug.Log("씬을 저장하세요! (Ctrl+S)");
    }
}

