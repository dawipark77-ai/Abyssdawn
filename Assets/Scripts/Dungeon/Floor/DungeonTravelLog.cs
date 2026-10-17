using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-10-15] 던전 여행 로그 — 왼쪽 아래(메뉴 줄 바로 위)에 최근 3줄, 작은 글씨.
///  - 새 기록이 없으면 그대로 유지 (흐려지지 않음). 어두운 바탕 위라 검은 지도에서도 보인다
///  - 로그를 누르면 이번 탐험의 전체 기록 창
///  - DungeonHud.Toast 로 나가는 알림이 그대로 한 줄씩 쌓인다 (여러 줄 알림은 " · " 로 이어 한 줄)
/// 기록은 static 이라 전투 씬을 다녀와도 이어진다. 새 탐험(타이틀·게임 오버)에서 Clear.
/// </summary>
public class DungeonTravelLog : MonoBehaviour, IPointerClickHandler
{
    public const int VisibleLines = 3;
    public const int MaxHistory = 120;
    private const float LineH = 32f;

    private static readonly List<string> _history = new List<string>();
    private static readonly List<float> _times = new List<float>();
    private static DungeonTravelLog _instance;

    private readonly List<TextMeshProUGUI> _lines = new List<TextMeshProUGUI>();
    private GameObject _full;
    private TextMeshProUGUI _fullText;
    private TMP_FontAsset _font;

    public static void Clear() { _history.Clear(); _times.Clear(); if (_instance != null) _instance.Redraw(); }

    public static void Add(string message)
    {
        string line = Flatten(message);
        if (string.IsNullOrEmpty(line)) return;
        _history.Add(line);
        _times.Add(Time.unscaledTime);
        while (_history.Count > MaxHistory) { _history.RemoveAt(0); _times.RemoveAt(0); }
        if (_instance != null) _instance.Redraw();
    }

    /// <summary>여러 줄 알림 → 한 줄 (크기 태그는 빼고 색은 남김).</summary>
    private static string Flatten(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = Regex.Replace(s, @"</?size[^>]*>", "");
        s = Regex.Replace(s, @"\s*\n\s*", "  ·  ");
        return s.Trim();
    }

    /// <summary>던전 HUD 가 부른다 — 아래쪽(메뉴 줄 위)에 로그 칸을 만든다.</summary>
    public static DungeonTravelLog Create(Transform hudRoot, TMP_FontAsset font)
    {
        var canvasGo = new GameObject("TravelLog", typeof(RectTransform));
        canvasGo.transform.SetParent(hudRoot, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // 지도 위, 메뉴 창(씬 Canvas)들 아래 — 창을 열면 가려짐
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var box = new GameObject("Lines", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)box.transform;
        // 왼쪽 아래, 메뉴 줄 바로 위 — 너비는 화면의 약 70%
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(20f, 505f); rt.sizeDelta = new Vector2(760f, VisibleLines * LineH + 14f);
        var bg = box.GetComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.6f); bg.raycastTarget = true;

        var log = box.AddComponent<DungeonTravelLog>();
        log._font = font;
        for (int i = 0; i < VisibleLines; i++)
        {
            var t = NewText(rt, "Line" + i, 23f, font);
            var lrt = t.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0f, 0f);
            lrt.anchoredPosition = new Vector2(12f, 7f + (VisibleLines - 1 - i) * LineH); lrt.sizeDelta = new Vector2(-24f, LineH);
            log._lines.Add(t);
        }
        return log;
    }

    private static TextMeshProUGUI NewText(RectTransform parent, string name, float size, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = Color.white; t.alignment = TextAlignmentOptions.BottomLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true; t.raycastTarget = false;
        t.outlineWidth = 0.2f; t.outlineColor = new Color32(0, 0, 0, 255);
        return t;
    }

    private void OnEnable() { _instance = this; Redraw(); }
    private void OnDisable() { if (_instance == this) _instance = null; }


    private void Redraw()
    {
        int start = Mathf.Max(0, _history.Count - VisibleLines);
        for (int i = 0; i < _lines.Count; i++)
        {
            int h = start + i;
            if (_lines[i] != null) _lines[i].text = h < _history.Count ? _history[h] : "";
        }
        if (_fullText != null && _full != null && _full.activeSelf) _fullText.text = string.Join("\n", _history);
    }

    // 누르면 전체 기록 (다시 누르면 닫힘)
    public void OnPointerClick(PointerEventData e)
    {
        if (_full == null) BuildFull();
        bool show = !_full.activeSelf;
        _full.SetActive(show);
        if (show) { _fullText.text = _history.Count > 0 ? string.Join("\n", _history) : "<color=#888888>아직 기록이 없다.</color>"; Canvas.ForceUpdateCanvases(); var sr = _full.GetComponentInChildren<ScrollRect>(); if (sr != null) sr.verticalNormalizedPosition = 0f; }
    }

    private void BuildFull()
    {
        var parent = (RectTransform)transform.parent;
        _full = new GameObject("FullLog", typeof(RectTransform), typeof(Image), typeof(Button));
        _full.transform.SetParent(parent, false);
        var rt = (RectTransform)_full.transform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(30f, 505f); rt.offsetMax = new Vector2(-30f, -170f);
        // 드퀘식: 굵은 크림색 테두리 (네 변을 따로 — 안쪽이 비쳐 회색이 되지 않게) + 바탕 80% 불투명
        var bg = _full.GetComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0f); // 누름 받기용 (투명)
        var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(rt, false);
        var irt = (RectTransform)inner.transform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(8f, 8f); irt.offsetMax = new Vector2(-8f, -8f);
        var iimg = inner.GetComponent<Image>(); iimg.color = new Color(0.02f, 0.02f, 0.05f, 0.8f); iimg.raycastTarget = false;
        var cream = new Color(0.94f, 0.92f, 0.85f, 1f);
        for (int i = 0; i < 4; i++)
        {
            var e = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            e.transform.SetParent(rt, false);
            var ert = (RectTransform)e.transform;
            const float w = 8f;
            switch (i)
            {
                case 0: ert.anchorMin = new Vector2(0f, 1f); ert.anchorMax = Vector2.one; ert.pivot = new Vector2(0.5f, 1f); ert.sizeDelta = new Vector2(0f, w); break;
                case 1: ert.anchorMin = Vector2.zero; ert.anchorMax = new Vector2(1f, 0f); ert.pivot = new Vector2(0.5f, 0f); ert.sizeDelta = new Vector2(0f, w); break;
                case 2: ert.anchorMin = Vector2.zero; ert.anchorMax = new Vector2(0f, 1f); ert.pivot = new Vector2(0f, 0.5f); ert.sizeDelta = new Vector2(w, 0f); break;
                default: ert.anchorMin = new Vector2(1f, 0f); ert.anchorMax = Vector2.one; ert.pivot = new Vector2(1f, 0.5f); ert.sizeDelta = new Vector2(w, 0f); break;
            }
            ert.anchoredPosition = Vector2.zero;
            var ei = e.GetComponent<Image>(); ei.color = cream; ei.raycastTarget = false;
        }
        _full.GetComponent<Button>().onClick.AddListener(() => _full.SetActive(false));

        var title = NewText(rt, "Title", 36f, _font);
        title.text = "<color=#D4A761>여행 기록</color>"; title.alignment = TextAlignmentOptions.Center;
        var trt = title.rectTransform; trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -16f); trt.sizeDelta = new Vector2(0f, 56f);

        var view = new GameObject("View", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(rt, false);
        var vrt = (RectTransform)view.transform; vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = new Vector2(34f, 30f); vrt.offsetMax = new Vector2(-34f, -84f);
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(vrt, false);
        var crt = (RectTransform)content.transform; crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f); crt.sizeDelta = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>(); vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _fullText = NewText(crt, "Text", 28f, _font);
        _fullText.textWrappingMode = TextWrappingModes.Normal; _fullText.alignment = TextAlignmentOptions.TopLeft; _fullText.lineSpacing = 10f;

        var sr = view.GetComponent<ScrollRect>(); sr.content = crt; sr.horizontal = false; sr.viewport = vrt; sr.movementType = ScrollRect.MovementType.Clamped;
        _full.SetActive(false);
    }
}
