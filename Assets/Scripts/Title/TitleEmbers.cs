using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-14] 타이틀 배경의 불씨 — 작은 금빛 점이 9초 동안 260만큼 천천히 떠오르며 희미하게 나타났다 사라진다.
/// Claude Design ui_kits/title 의 .ember 6개를 그대로 옮김 (위치 = 디자인 영역 430×932 기준 왼쪽 %, 아래 %).
///   투명도: 0% → 0, 10% → 0.6, 90% → 0.4, 100% → 0 (사이는 직선), 움직임은 일정한 속도.
/// </summary>
public class TitleEmbers : MonoBehaviour
{
    [System.Serializable]
    public struct Ember
    {
        [Range(0f, 1f)] public float left;   // 디자인 영역 왼쪽에서 (0~1)
        [Range(0f, 1f)] public float bottom; // 디자인 영역 아래에서 (0~1)
        public float delay;                  // 처음 떠오르기까지 (초)
    }

    public Sprite emberSprite;
    public Color color = new Color(0.831f, 0.655f, 0.380f, 1f); // #d4a761
    [Tooltip("불씨 그림 한 변 (디자인 px). 가운데 2px 점 + 둘레 번짐")]
    public float spriteSize = 16f;
    public float duration = 9f;
    public float rise = 260f;
    public Vector2 designSize = new Vector2(430f, 932f);

    public List<Ember> embers = new List<Ember>
    {
        new Ember { left = 0.22f, bottom = 0.36f, delay = 0f },
        new Ember { left = 0.68f, bottom = 0.48f, delay = 2.4f },
        new Ember { left = 0.38f, bottom = 0.28f, delay = 4.7f },
        new Ember { left = 0.80f, bottom = 0.54f, delay = 1.1f },
        new Ember { left = 0.12f, bottom = 0.42f, delay = 6.2f },
        new Ember { left = 0.54f, bottom = 0.32f, delay = 3.5f },
    };

    private readonly List<Image> _images = new List<Image>();
    private readonly List<Vector2> _starts = new List<Vector2>();
    private float _t0;

    private void Start()
    {
        _t0 = Time.time;
        foreach (var e in embers)
        {
            var go = new GameObject("Ember", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); // 디자인 영역 왼쪽 아래 기준
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(spriteSize, spriteSize);
            // CSS: left/bottom 이 2×2 점의 왼쪽 아래 → 점 가운데는 +1
            var start = new Vector2(e.left * designSize.x + 1f, e.bottom * designSize.y + 1f);
            rt.anchoredPosition = start;
            var img = go.GetComponent<Image>();
            img.sprite = emberSprite;
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0f);
            _images.Add(img);
            _starts.Add(start);
        }
    }

    private void Update()
    {
        float now = Time.time - _t0;
        for (int i = 0; i < _images.Count; i++)
        {
            float local = now - embers[i].delay;
            float alpha = 0f, y = 0f;
            if (local >= 0f)
            {
                float p = (local % duration) / duration;
                y = rise * p;
                alpha = p < 0.1f ? Mathf.Lerp(0f, 0.6f, p / 0.1f)
                      : p < 0.9f ? Mathf.Lerp(0.6f, 0.4f, (p - 0.1f) / 0.8f)
                      : Mathf.Lerp(0.4f, 0f, (p - 0.9f) / 0.1f);
            }
            ((RectTransform)_images[i].transform).anchoredPosition = _starts[i] + new Vector2(0f, y);
            _images[i].color = new Color(color.r, color.g, color.b, alpha);
        }
    }
}
