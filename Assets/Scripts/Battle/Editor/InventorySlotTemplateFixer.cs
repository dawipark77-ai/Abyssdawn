#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > Abyssdawn > Fix Inventory Slot Template References
/// ItemSlot_Template 의 InventorySlot 컴포넌트 참조를
/// 자식 오브젝트 이름으로 찾아 자동 연결합니다.
///   iconImage    ← "Icon" 자식의 Image
///   borderImage  ← "Border" 자식의 Image
///   quantityText ← "QuantityBadge/QtyText" 자식의 TextMeshProUGUI
///   button       ← 자기 자신(ItemSlot_Template)의 Button
/// </summary>
public static class InventorySlotTemplateFixer
{
    [MenuItem("Tools/Abyssdawn/Fix Inventory Slot Template References", priority = 214)]
    public static void Fix()
    {
        // ── ItemSlot_Template 찾기 ─────────────────────────────
        var overlay = GameObject.Find("InventoryOverlay");
        if (overlay == null) { Fail("InventoryOverlay 를 찾을 수 없습니다."); return; }

        var content = overlay.transform
            .Find("GridScrollRect/Viewport/Content");
        if (content == null) { Fail("GridScrollRect/Viewport/Content 를 찾을 수 없습니다."); return; }

        Transform templateT = content.Find("ItemSlot_Template");
        if (templateT == null) { Fail("Content 안에 ItemSlot_Template 가 없습니다."); return; }

        var template = templateT.gameObject;
        Undo.RecordObject(template, "Fix Slot Template References");

        var slot = template.GetComponent<InventorySlot>();
        if (slot == null)
        {
            slot = template.AddComponent<InventorySlot>();
            Debug.Log("[SlotFixer] InventorySlot 컴포넌트 추가");
        }

        // ── Button ────────────────────────────────────────────
        var btn = template.GetComponent<Button>();
        if (btn == null) btn = template.AddComponent<Button>();
        slot.button = btn;
        Debug.Log($"[SlotFixer] button   → {btn.gameObject.name}");

        // ── iconImage (Icon 자식) ──────────────────────────────
        var iconT = templateT.Find("Icon");
        if (iconT != null)
        {
            slot.iconImage = iconT.GetComponent<Image>();
            Debug.Log($"[SlotFixer] iconImage  → {iconT.name}");
        }
        else Debug.LogWarning("[SlotFixer] 'Icon' 자식을 찾을 수 없습니다.");

        // ── borderImage (Border 자식) ──────────────────────────
        var borderT = templateT.Find("Border");
        if (borderT != null)
        {
            slot.borderImage = borderT.GetComponent<Image>();
            Debug.Log($"[SlotFixer] borderImage → {borderT.name}");
        }
        else Debug.LogWarning("[SlotFixer] 'Border' 자식을 찾을 수 없습니다.");

        // ── quantityText (QuantityBadge/QtyText) ───────────────
        var qtyT = templateT.Find("QuantityBadge/QtyText");
        if (qtyT != null)
        {
            slot.quantityText = qtyT.GetComponent<TextMeshProUGUI>();
            Debug.Log($"[SlotFixer] quantityText → {qtyT.name}");
        }
        else Debug.LogWarning("[SlotFixer] 'QuantityBadge/QtyText' 자식을 찾을 수 없습니다.");

        EditorUtility.SetDirty(template);
        Debug.Log("[SlotFixer] 완료 — Ctrl+S 로 씬을 저장해 주세요.");

        EditorUtility.DisplayDialog("Fix Slot Template References",
            "ItemSlot_Template 참조 연결 완료!\n\n" +
            "  iconImage    ← Icon\n" +
            "  borderImage  ← Border\n" +
            "  quantityText ← QuantityBadge/QtyText\n" +
            "  button       ← ItemSlot_Template\n\n" +
            "이제 플레이 모드에서 장비는 equipmentIcon,\n" +
            "소비 아이템은 itemIcon 이 자동 표시됩니다.\n\n" +
            "Ctrl+S 로 씬을 저장해 주세요.", "확인");
    }

    private static void Fail(string msg)
    {
        EditorUtility.DisplayDialog("Fix Slot Template References", msg, "확인");
        Debug.LogError("[SlotFixer] " + msg);
    }
}
#endif
