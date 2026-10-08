using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-09] 모든 스킬 아이콘에 아비스던 고유의 옅은 황금 테두리를 두른다.
/// 아이콘 Image 의 자식으로 9-slice 프레임(Resources/UI/GlowBorder, 가운데 빈 테두리)을 하나 붙인다.
/// 스킬 아이콘 스프라이트를 넣는 곳마다 Apply(iconImage) 를 부르면 된다 (여러 번 불러도 하나만 생김).
/// </summary>
public static class SkillIconFrame
{
    /// <summary>아비스던 황금 (새벽빛 테두리·배운 스킬 테두리와 같은 색).</summary>
    public static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);

    private const string ChildName = "SkillGoldFrame";
    private static Sprite _frame;

    /// <summary>배운 스킬 테두리 — 더 밝은 황금 (바깥 배운 스킬 테두리와 겹쳐 더 환하게).</summary>
    public static readonly Color BrightGold = new Color(1f, 0.95f, 0.74f, 1f);

    /// <param name="icon">스킬 아이콘 Image</param>
    /// <param name="outset">아이콘 바깥으로 얼마나 넓힐지 (픽셀). 0 이면 아이콘 가장자리에 딱 맞음</param>
    /// <param name="bright">true = 배운 스킬처럼 더 밝은 황금</param>
    public static void Apply(Image icon, float outset = 2f, bool bright = false)
    {
        if (icon == null) return;
        Transform t = icon.transform.Find(ChildName);
        Image img;
        if (t == null)
        {
            if (_frame == null) _frame = Resources.Load<Sprite>("UI/GlowBorder");
            if (_frame == null) return;
            var go = new GameObject(ChildName, typeof(RectTransform));
            go.transform.SetParent(icon.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // 아이콘에 레이아웃 그룹이 있어도 테두리는 자리를 차지하지 않음
            img = go.AddComponent<Image>();
            img.sprite = _frame;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.raycastTarget = false;
            img.color = Gold;
        }
        else img = t.GetComponent<Image>();
        if (img == null) return;

        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
        rt.SetAsLastSibling();
        img.color = bright ? BrightGold : Gold;
        // 아이콘이 비어 있으면 테두리도 숨김
        img.gameObject.SetActive(icon.enabled && icon.sprite != null);
    }
}
