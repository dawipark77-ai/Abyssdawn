#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Tools > Abyssdawn > Link Item Button to Inventory
/// StatusMenuPanel > Item 버튼 클릭 시 InventoryOverlay를 열도록 연결합니다.
/// </summary>
public static class ItemPanelLinker
{
    [MenuItem("Tools/Abyssdawn/Link Item Button to Inventory", priority = 201)]
    public static void Link()
    {
        // ── InventoryOverlay 탐색 ─────────────────────────────
        GameObject overlayObj = FindInScene("InventoryOverlay");
        if (overlayObj == null)
        {
            EditorUtility.DisplayDialog("ItemPanelLinker",
                "'InventoryOverlay' 오브젝트를 씬에서 찾을 수 없습니다.\n" +
                "Tools > Abyssdawn > Create Inventory UI 를 먼저 실행해 주세요.", "확인");
            return;
        }

        InventoryUIManager invManager = overlayObj.GetComponent<InventoryUIManager>();
        if (invManager == null)
        {
            EditorUtility.DisplayDialog("ItemPanelLinker",
                "'InventoryOverlay'에 InventoryUIManager 컴포넌트가 없습니다.", "확인");
            return;
        }

        // ── StatusMenuPanel > Item 탐색 ───────────────────────
        GameObject statusMenuPanel = FindInScene("StatusMenuPanel");
        if (statusMenuPanel == null)
        {
            EditorUtility.DisplayDialog("ItemPanelLinker",
                "'StatusMenuPanel' 오브젝트를 씬에서 찾을 수 없습니다.", "확인");
            return;
        }

        Transform itemTransform = statusMenuPanel.transform.Find("Item");
        if (itemTransform == null)
        {
            EditorUtility.DisplayDialog("ItemPanelLinker",
                "StatusMenuPanel 안에 'Item' 자식을 찾을 수 없습니다.", "확인");
            return;
        }

        GameObject itemObj = itemTransform.gameObject;

        // ── 기존 PanelOpener 제거 (잘못 연결된 경우 정리) ──────
        PanelOpener oldOpener = itemObj.GetComponent<PanelOpener>();
        if (oldOpener != null)
        {
            Undo.DestroyObjectImmediate(oldOpener);
            Debug.Log("[ItemPanelLinker] 기존 PanelOpener를 제거했습니다.");
        }

        // ── Button 컴포넌트 확보 ───────────────────────────────
        Button btn = itemObj.GetComponent<Button>();
        if (btn == null)
        {
            btn = Undo.AddComponent<Button>(itemObj);
            Debug.Log("[ItemPanelLinker] Button 컴포넌트를 추가했습니다.");
        }

        // ── onClick에 InventoryUIManager.OpenInventory 연결 ────
        Undo.RecordObject(btn, "Link Item to Inventory");
        btn.onClick.RemoveAllListeners();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            btn.onClick,
            invManager.OpenInventory);

        // InventoryOverlay는 초기 비활성화 상태 유지
        overlayObj.SetActive(false);

        EditorUtility.SetDirty(btn);
        EditorUtility.SetDirty(overlayObj);

        Debug.Log("[ItemPanelLinker] 연결 완료: StatusMenuPanel > Item → InventoryOverlay.OpenInventory()");
        EditorUtility.DisplayDialog("ItemPanelLinker",
            "연결 완료!\n\n" +
            "버튼: StatusMenuPanel > Item\n" +
            "동작: InventoryOverlay.OpenInventory()\n\n" +
            "씬을 저장해 주세요. (Ctrl+S)", "확인");
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
