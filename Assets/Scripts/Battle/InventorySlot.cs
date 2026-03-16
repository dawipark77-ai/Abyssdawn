using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

public class InventorySlot : MonoBehaviour
{
    [Header("참조")]
    public Image iconImage;
    public Image borderImage;
    public Image slotBackground;
    public TextMeshProUGUI quantityText;
    public Button button;

    private static readonly Color RareBorderColor    = new Color(0.85f, 0.70f, 0.10f, 1f);
    private static readonly Color DefaultBorderColor = new Color(0.25f, 0.25f, 0.32f, 0.6f);
    private static readonly Color DawnChaliceColor   = new Color(0.20f, 0.55f, 0.85f, 1f);
    private static readonly Color PlaceholderColor   = new Color(0.30f, 0.30f, 0.38f, 1f);

    // ── 장비 아이템 ───────────────────────────────────────────
    public void Setup(EquipmentData item, Action onClick)
    {
        if (item == null) { SetEmpty(); return; }

        SetIcon(item.equipmentIcon);

        if (borderImage != null)
            borderImage.color = DefaultBorderColor;

        if (quantityText != null)
            quantityText.gameObject.SetActive(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    // ── 소비 아이템 ───────────────────────────────────────────
    public void SetupConsumable(ConsumableItemSO item, int quantity, Action onClick)
    {
        if (item == null) { SetEmpty(); return; }

        SetIcon(item.itemIcon != null ? item.itemIcon : item.flatIcon);

        if (borderImage != null)
            borderImage.color = item.isDawnChalice ? DawnChaliceColor : DefaultBorderColor;

        if (quantityText != null)
        {
            quantityText.gameObject.SetActive(true);
            quantityText.text = quantity.ToString();
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }

    public void SetRareBorder(bool rare)
    {
        if (borderImage != null)
            borderImage.color = rare ? RareBorderColor : DefaultBorderColor;
    }

    public void SetEmpty()
    {
        iconImage.enabled = false;
        if (quantityText != null) quantityText.gameObject.SetActive(false);
        button.onClick.RemoveAllListeners();
    }

    private void SetIcon(Sprite sprite)
    {
        iconImage.enabled = true;
        if (sprite != null)
        {
            iconImage.sprite = sprite;
            iconImage.color  = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color  = PlaceholderColor;
        }
    }
}
