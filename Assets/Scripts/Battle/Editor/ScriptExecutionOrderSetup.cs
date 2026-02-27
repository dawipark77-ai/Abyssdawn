using UnityEngine;
using UnityEditor;

/// <summary>
/// Script Execution Order를 자동으로 설정하는 Editor 스크립트
/// EquipmentManager가 PlayerStats보다 먼저 실행되도록 보장
/// </summary>
[InitializeOnLoad]
public class ScriptExecutionOrderSetup
{
    static ScriptExecutionOrderSetup()
    {
        // EquipmentManager: -50 (먼저 실행)
        // PlayerStats: 0 (기본값)
        
        SetExecutionOrder("EquipmentManager", -50);
        SetExecutionOrder("PlayerStats", 0);
        
        Debug.Log("[ScriptExecutionOrderSetup] Script execution order configured: EquipmentManager (-50) < PlayerStats (0)");
    }

    private static void SetExecutionOrder(string scriptName, int order)
    {
        // 모든 MonoScript 찾기
        foreach (MonoScript monoScript in MonoImporter.GetAllRuntimeMonoScripts())
        {
            if (monoScript.name == scriptName)
            {
                if (MonoImporter.GetExecutionOrder(monoScript) != order)
                {
                    MonoImporter.SetExecutionOrder(monoScript, order);
                    Debug.Log($"[ScriptExecutionOrderSetup] Set {scriptName} execution order to {order}");
                }
                return;
            }
        }
    }
}








