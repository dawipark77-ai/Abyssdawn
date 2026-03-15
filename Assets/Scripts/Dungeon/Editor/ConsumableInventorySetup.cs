#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools > Abyssdawn > Setup Consumable Inventory
/// InventoryOverlay 오브젝트에 ConsumableInventory 컴포넌트를 추가하고
/// InventoryUIManager와 자동으로 연결합니다.
/// </summary>
public static class ConsumableInventorySetup
{
    [MenuItem("Tools/Abyssdawn/Setup Consumable Inventory", priority = 212)]
    public static void Setup()
    {
        // ── InventoryOverlay 탐색 ─────────────────────────────
        GameObject overlayObj = FindInScene("InventoryOverlay");
        if (overlayObj == null)
        {
            EditorUtility.DisplayDialog("ConsumableInventorySetup",
                "'InventoryOverlay' 오브젝트를 씬에서 찾을 수 없습니다.\n" +
                "Tools > Abyssdawn > Create Inventory UI 를 먼저 실행해 주세요.", "확인");
            return;
        }

        // ── InventoryUIManager 확인 ───────────────────────────
        InventoryUIManager uiManager = overlayObj.GetComponent<InventoryUIManager>();
        if (uiManager == null)
        {
            EditorUtility.DisplayDialog("ConsumableInventorySetup",
                "'InventoryOverlay'에 InventoryUIManager가 없습니다.", "확인");
            return;
        }

        // ── ConsumableInventory 추가 (없으면) ─────────────────
        ConsumableInventory inv = overlayObj.GetComponent<ConsumableInventory>();
        if (inv == null)
        {
            inv = Undo.AddComponent<ConsumableInventory>(overlayObj);
            Debug.Log("[ConsumableInventorySetup] ConsumableInventory 컴포넌트를 추가했습니다.");
        }
        else
        {
            Debug.Log("[ConsumableInventorySetup] ConsumableInventory가 이미 존재합니다. 연결만 갱신합니다.");
        }

        // ── InventoryUIManager에 참조 연결 ────────────────────
        Undo.RecordObject(uiManager, "Link ConsumableInventory");
        uiManager.consumableInventory = inv;

        EditorUtility.SetDirty(overlayObj);
        EditorUtility.SetDirty(uiManager);

        Debug.Log("[ConsumableInventorySetup] 연결 완료: InventoryOverlay → ConsumableInventory");
        EditorUtility.DisplayDialog("ConsumableInventorySetup",
            "완료!\n\n" +
            "InventoryOverlay 오브젝트에 ConsumableInventory가 추가되었습니다.\n\n" +
            "다음 단계:\n" +
            "1. InventoryOverlay Inspector에서\n" +
            "   ConsumableInventory > Dawn Chalice Item 필드에\n" +
            "   Dawn_Chalice.asset 을 연결하세요.\n\n" +
            "2. Ctrl+S 씬 저장", "확인");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == name) return go;
        }
        return null;
    }
}
#endif
