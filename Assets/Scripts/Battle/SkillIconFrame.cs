using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-09] 모든 스킬 아이콘에 아비스던 테두리를 두른다.
/// [2026-10-19] 테두리 그림을 사용자가 준 금속 사각 틀(Resources/UI/SkillFrame — 가운데·바깥 귀퉁이 투명)로 바꿈.
///   틀의 안쪽 구멍이 아이콘 가장자리에 딱 맞도록 아이콘보다 사방 12% 크게 덮는다 (아이콘 크기에 따라 틀 굵기도 같이 커짐).
/// 스킬 아이콘 스프라이트를 넣는 곳마다 Apply(iconImage) 를 부르면 된다 (여러 번 불러도 하나만 생김).
/// </summary>
public static class SkillIconFrame
{
    /// <summary>아비스던 황금 (새벽빛 테두리·배운 스킬 글자와 같은 색).</summary>
    public static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);
    /// <summary>배운 스킬 — 틀을 원래 색 그대로 밝게.</summary>
    public static readonly Color BrightGold = new Color(1f, 1f, 1f, 1f);
    /// <summary>보통 — 틀을 조금 어둡게.</summary>
    public static readonly Color Normal = new Color(0.78f, 0.78f, 0.78f, 1f);

    private const string ChildName = "SkillGoldFrame";
    private const float Overhang = 0.07f; // 틀 안쪽 구멍 = 아이콘 크기. [2026-10-20] 얇은 틀(가운데 장식 띠 제거)로 바꿔 12% → 7%
    private static Sprite _frame;

    private static Sprite Frame
    {
        get
        {
            if (_frame == null) _frame = Resources.Load<Sprite>("UI/SkillFrame");
            if (_frame == null) _frame = Resources.Load<Sprite>("UI/GlowBorder");
            return _frame;
        }
    }

    /// <param name="icon">스킬 아이콘 Image</param>
    /// <param name="outset">(예전 인자 — 이제 틀은 아이콘 크기에 비례해 덮으므로 쓰지 않음)</param>
    /// <param name="bright">true = 배운 스킬처럼 밝게</param>
    /// <param name="showEmpty">true = 아이콘 그림이 없어도 빈 틀을 보여 줌 (스킬 트리 노드 등)</param>
    public static void Apply(Image icon, float outset = 2f, bool bright = false, bool showEmpty = false)
    {
        if (icon == null) return;
        Transform t = icon.transform.Find(ChildName);
        Image img;
        if (t == null)
        {
            if (Frame == null) return;
            var go = new GameObject(ChildName, typeof(RectTransform));
            go.transform.SetParent(icon.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // 아이콘에 레이아웃 그룹이 있어도 테두리는 자리를 차지하지 않음
            img = go.AddComponent<Image>();
            img.raycastTarget = false;
        }
        else img = t.GetComponent<Image>();
        if (img == null) return;

        img.sprite = Frame;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        var rt = (RectTransform)img.transform;
        rt.anchorMin = new Vector2(-Overhang, -Overhang);
        rt.anchorMax = new Vector2(1f + Overhang, 1f + Overhang);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        img.color = bright ? BrightGold : Normal;
        // 아이콘이 비어 있으면 테두리도 숨김 (showEmpty 면 빈 틀을 그대로)
        img.gameObject.SetActive(icon.enabled && (icon.sprite != null || showEmpty));
    }
}
