using UnityEngine;

/// <summary>
/// 코드로 그리는 작은 스프라이트 (이미지 파일 없이). 한 번 만들어 재사용한다.
///  Arrow: 위를 가리키는 내비게이션 화살표 (흰색 + 어두운 테두리, SpriteRenderer.color 로 색 지정). 1 × 1 월드 단위.
/// </summary>
public static class DungeonIconSprites
{
    private const int Size = 64;
    private static Sprite _arrow;

    public static Sprite Arrow
    {
        get
        {
            if (_arrow == null) _arrow = CreateArrow();
            return _arrow;
        }
    }

    private static Sprite CreateArrow()
    {
        // 화살촉 모양 (0~1 좌표, 위가 +y): 꼭짓점 → 오른쪽 날개 → 안쪽 홈 → 왼쪽 날개
        Vector2[] fill =
        {
            new Vector2(0.5f, 0.94f),
            new Vector2(0.88f, 0.1f),
            new Vector2(0.5f, 0.3f),
            new Vector2(0.12f, 0.1f),
        };
        Vector2[] outline = Scale(fill, new Vector2(0.5f, 0.48f), 1.16f);

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "DungeonArrow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color[Size * Size];
        const int ss = 4; // 가장자리를 부드럽게: 한 픽셀을 4×4 로 나눠 표본 추출
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int inFill = 0, inOutline = 0;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / ss) / Size, (y + (sy + 0.5f) / ss) / Size);
                        if (Inside(fill, p)) inFill++;
                        else if (Inside(outline, p)) inOutline++;
                    }
                }
                float total = ss * ss;
                float a = (inFill + inOutline) / total;
                float white = inFill / total;
                // 흰 몸통 + 검은 테두리 (색은 SpriteRenderer.color 가 곱해져 몸통만 물든 것처럼 보이도록 테두리는 거의 검정)
                float c = a > 0f ? white / a : 0f;
                px[y * Size + x] = new Color(c, c, c, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    private static Vector2[] Scale(Vector2[] poly, Vector2 center, float s)
    {
        var r = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++) r[i] = center + (poly[i] - center) * s;
        return r;
    }

    /// <summary>점이 다각형 안에 있는지 (짝홀 규칙, 오목 다각형 가능).</summary>
    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }
}
