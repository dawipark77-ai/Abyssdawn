using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 창(ItemListPanel)의 카드 한 장. 하이라키의 ItemCard_Template 에 붙어 있고, 실행 중 복제된다.
/// 모양(크기·색·글꼴)은 하이라키에서 마음대로 고쳐도 된다 — 아래 참조만 연결돼 있으면 동작한다.
/// </summary>
public class ItemListCard : MonoBehaviour
{
    [Header("참조")]
    public Button button;
    [Tooltip("카드 바탕")]
    public Image background;
    [Tooltip("카드 테두리 (Outline 효과). 장착 중 = 금색, 새벽의 잔 = 파랑, 선택 = 밝게")]
    public Outline border;
    public Image icon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI subText;
    public TextMeshProUGUI qtyText;

    [Header("색")]
    public Color normalBackground = new Color(0.07f, 0.06f, 0.05f, 1f);
    public Color selectedBackground = new Color(0.20f, 0.15f, 0.07f, 1f);
    public Color normalBorder = new Color(0.55f, 0.43f, 0.22f, 0.55f);
    public Color equippedBorder = new Color(0.95f, 0.76f, 0.30f, 1f);
    public Color chaliceBorder = new Color(0.35f, 0.65f, 1f, 1f);
    public Color selectedBorder = new Color(1f, 0.88f, 0.55f, 1f);

    private Color _baseBorder;

    public void Setup(Sprite sprite, string itemName, string sub, string qty, bool equipped, bool chalice, Action onClick)
    {
        if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; icon.preserveAspect = true; }
        if (nameText != null) nameText.text = itemName;
        if (subText != null) subText.text = sub;
        if (qtyText != null) { qtyText.text = qty; qtyText.gameObject.SetActive(!string.IsNullOrEmpty(qty)); }
        _baseBorder = chalice ? chaliceBorder : equipped ? equippedBorder : normalBorder;
        SetSelected(false);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }

    public void SetSelected(bool selected)
    {
        if (background != null) background.color = selected ? selectedBackground : normalBackground;
        if (border != null) border.effectColor = selected ? selectedBorder : _baseBorder;
    }
}
