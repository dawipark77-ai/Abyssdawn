#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Abyssdawn > Fix Inventory Viewport Mask
/// GridScrollRect > Viewport 의 Mask → RectMask2D 로 교체합니다.
/// Mask(alpha=0 Image) 가 스텐실을 쓰지 않아 슬롯이 안 보이는 문제를 수정합니다.
/// </summary>
public static class InventoryViewportFixer
{
    [MenuItem("Tools/Abyssdawn/Fix Inventory Viewport Mask", priority = 213)]
    public static void Fix()
    {
        var overlay = GameObject.Find("InventoryOverlay");
        if (overlay == null)
        {
            EditorUtility.DisplayDialog("Fix Viewport Mask",
                "씬에서 InventoryOverlay 를 찾을 수 없습니다.", "확인");
            return;
        }

        var gridScrollRect = overlay.transform.Find("GridScrollRect");
        if (gridScrollRect == null)
        {
            EditorUtility.DisplayDialog("Fix Viewport Mask",
                "GridScrollRect 를 찾을 수 없습니다.", "확인");
            return;
        }

        var viewport = gridScrollRect.Find("Viewport");
        if (viewport == null)
        {
            EditorUtility.DisplayDialog("Fix Viewport Mask",
                "Viewport 를 찾을 수 없습니다.", "확인");
            return;
        }

        Undo.RecordObject(viewport.gameObject, "Fix Inventory Viewport Mask");

        // Mask 제거 (alpha=0 Image 로 인해 스텐실이 작동 안 하는 문제)
        var oldMask = viewport.GetComponent<Mask>();
        if (oldMask != null)
        {
            Undo.DestroyObjectImmediate(oldMask);
            Debug.Log("[ViewportFixer] Mask 제거 완료");
        }

        // Image 도 제거 (RectMask2D 는 Image 불필요)
        var img = viewport.GetComponent<Image>();
        if (img != null)
        {
            Undo.DestroyObjectImmediate(img);
            Debug.Log("[ViewportFixer] Image 제거 완료");
        }

        // RectMask2D 로 교체 (셰이더 기반 클리핑 — 스텐실 불필요)
        if (viewport.GetComponent<RectMask2D>() == null)
        {
            Undo.AddComponent<RectMask2D>(viewport.gameObject);
            Debug.Log("[ViewportFixer] RectMask2D 추가 완료");
        }

        EditorUtility.SetDirty(viewport.gameObject);
        Debug.Log("[ViewportFixer] 완료 — Ctrl+S 로 씬을 저장해 주세요.");

        EditorUtility.DisplayDialog("Fix Viewport Mask",
            "완료!\n\nViewport의 Mask(alpha=0) → RectMask2D 로 교체했습니다.\n\n" +
            "플레이 모드에서 인벤토리를 열면 슬롯이 보여야 합니다.\n" +
            "Ctrl+S 로 씬을 저장해 주세요.", "확인");
    }
}
#endif
