using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-14] 새 게임으로 던전에 처음 들어올 때 — "눈을 뜨는" 연출.
///   검은 화면 → 아주 흐릿한 던전이 서서히 밝아지며 점점 선명해짐 → 다 선명해지면 화면 UI(HUD)가 나타남.
/// 흐림: 카메라를 아주 낮은 해상도로 그린 뒤 화면 크기로 늘려 보여 주고(부드럽게 번짐), 해상도를 조금씩 올린다.
/// 연출 동안 주인공은 움직이지 않는다.
/// </summary>
public class WakeUpTransition : MonoBehaviour
{
    public float holdBlack = 0.5f;   // 처음 검은 채로
    public float sharpen = 2.6f;     // 흐림 → 선명
    public float hudFade = 0.6f;     // 마지막에 HUD 나타남

    private Camera _cam;
    private RenderTexture _rt;
    private RawImage _view;
    private Image _black;
    private readonly List<CanvasGroup> _hud = new List<CanvasGroup>();
    private readonly List<float> _hudAlpha = new List<float>();

    public static void Play()
    {
        var go = new GameObject("WakeUpTransition");
        go.AddComponent<WakeUpTransition>();
    }

    private IEnumerator Start()
    {
        // 맨 위 캔버스: 흐린 화면 + 검은 막
        var cgo = new GameObject("WakeUpCanvas", typeof(RectTransform), typeof(Canvas));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        _view = NewFull<RawImage>("View", cgo.transform);
        _black = NewFull<Image>("Black", cgo.transform);
        _black.color = Color.black;

        // 화면 UI 잠깐 숨김 (우리 캔버스 말고 화면 위 캔버스 전부)
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c == null || !c.isRootCanvas || c == canvas || c.renderMode == RenderMode.WorldSpace) continue;
            if (c.GetComponentInParent<EncounterTransition>() != null) continue;
            var g = c.GetComponent<CanvasGroup>();
            if (g == null) g = c.gameObject.AddComponent<CanvasGroup>();
            _hud.Add(g); _hudAlpha.Add(g.alpha);
            g.alpha = 0f;
        }

        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);

        _cam = Camera.main;
        yield return new WaitForSecondsRealtime(holdBlack);

        // 흐림 → 선명 (밝아지는 것과 함께)
        for (float t = 0f; t < sharpen; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / sharpen);
            float sharp = k * k * (3f - 2f * k);              // 부드럽게
            float scale = Mathf.Lerp(1f / 64f, 1f, sharp * sharp);
            RenderAt(scale);
            float light = Mathf.Clamp01(t / (sharpen * 0.65f));
            _black.color = new Color(0f, 0f, 0f, 1f - light * light * (3f - 2f * light));
            yield return null;
        }
        _black.color = new Color(0f, 0f, 0f, 0f);

        // 카메라를 원래대로 (화면에 직접)
        if (_cam != null) _cam.targetTexture = null;
        _view.enabled = false;
        ReleaseRt();

        // HUD 나타남
        for (float t = 0f; t < hudFade; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < _hud.Count; i++) if (_hud[i] != null) _hud[i].alpha = Mathf.Lerp(0f, _hudAlpha[i], t / hudFade);
            yield return null;
        }
        Finish();
        if (player != null) player.UnlockInput(this);
        Destroy(gameObject);
    }

    private void RenderAt(float scale)
    {
        if (_cam == null) return;
        int w = Mathf.Max(4, Mathf.RoundToInt(Screen.width * scale));
        int h = Mathf.Max(4, Mathf.RoundToInt(Screen.height * scale));
        if (_rt != null && _rt.width == w && _rt.height == h) return;
        var next = RenderTexture.GetTemporary(w, h, 24);
        next.filterMode = FilterMode.Bilinear;
        _cam.targetTexture = next;
        _view.texture = next;
        ReleaseRt();
        _rt = next;
    }

    private void ReleaseRt()
    {
        if (_rt == null) return;
        if (_cam != null && _cam.targetTexture == _rt) return;
        RenderTexture.ReleaseTemporary(_rt);
        _rt = null;
    }

    private void Finish()
    {
        for (int i = 0; i < _hud.Count; i++) if (_hud[i] != null) _hud[i].alpha = _hudAlpha[i];
    }

    private void OnDestroy()
    {
        // 중간에 씬이 바뀌어도 카메라·HUD는 원래대로
        if (_cam != null && _cam.targetTexture != null && _cam.targetTexture == _rt) _cam.targetTexture = null;
        if (_rt != null) { RenderTexture.ReleaseTemporary(_rt); _rt = null; }
        Finish();
    }

    private static T NewFull<T>(string name, Transform parent) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var g = go.GetComponent<T>();
        g.raycastTarget = true; // 연출 중에는 화면을 눌러도 아무 일 없게
        return g;
    }
}
