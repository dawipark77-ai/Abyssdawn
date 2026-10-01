using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 연출 — 전부 코드로 만든다 (씬에 배치할 것 없음). 전투 씬(BattleManager 가 있을 때)에서만 동작.
///  - 피해 숫자 팝업: 적(스프라이트 머리 위) / 아군(상태 카드 위). 크리티컬은 크게 + "CRITICAL", 빗나감은 "MISS"
///  - 회복 숫자 팝업: 같은 위치에 초록색 "+숫자" (PlayerStats.Heal / EnemyStats.Heal)
///  - 크리티컬: 순간 멈춤(히트 스톱) + 화면 흔들림
///  - 모아 치기 예고: 적 머리 위에 깜빡이는 경고 표시 (EnemyStats.pendingChargeSkill)
/// 행동 순서는 드퀘·진여신전생식으로 미리 보여주지 않는다 (해결 단계 시작 때 AGI × 랜덤으로 정해짐).
/// 한글 글꼴이 없어(LiberationSans) 화면 문구는 영어.
/// </summary>
public class BattleFx : MonoBehaviour
{
    [Header("크리티컬 연출")]
    public float hitStopSeconds = 0.08f;
    public float shakeSeconds = 0.18f;
    public float shakeMagnitude = 0.12f;

    private static BattleFx _instance;
    private static bool _pendingAllyCrit;

    private RectTransform _root;
    private readonly Dictionary<EnemyStats, TextMeshProUGUI> _intents = new Dictionary<EnemyStats, TextMeshProUGUI>();
    private Coroutine _hitStop, _shake;
    private Vector3 _camBasePos;
    private bool _shaking;

    // ─────────────────────────────────────────
    // 공개 API (정적) — 전투 씬이 아니면 아무것도 하지 않는다
    // ─────────────────────────────────────────

    private static BattleFx Get()
    {
        if (_instance != null) return _instance;
        if (FindFirstObjectByType<BattleManager>() == null) return null;
        var go = new GameObject("BattleFX");
        _instance = go.AddComponent<BattleFx>();
        return _instance;
    }

    /// <summary>적이 피해를 입음.</summary>
    public static void EnemyHit(EnemyStats e, int damage, bool crit)
    {
        var fx = Get();
        if (fx == null || e == null) return;
        fx.Popup(fx.EnemyAnchor(e), damage.ToString(), crit ? new Color(1f, 0.72f, 0.2f) : Color.white, crit);
        if (crit) fx.CritImpact();
    }

    public static void EnemyMiss(EnemyStats e)
    {
        var fx = Get();
        if (fx == null || e == null) return;
        fx.Popup(fx.EnemyAnchor(e), "MISS", new Color(0.7f, 0.7f, 0.75f), false);
    }

    /// <summary>다음 아군 피해가 크리티컬임을 표시 (BattleManager 가 TakeDamage 직전에 호출).</summary>
    public static void MarkAllyCritical() => _pendingAllyCrit = true;

    /// <summary>아군이 피해를 입음 (PlayerStats.TakeDamage 가 호출).</summary>
    public static void AllyHit(PlayerStats p, int damage)
    {
        bool crit = _pendingAllyCrit;
        _pendingAllyCrit = false;
        var fx = Get();
        if (fx == null || p == null) return;
        Vector2? pos = fx.AllyAnchor(p);
        if (pos == null) return;
        fx.Popup(pos.Value, damage.ToString(), crit ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.55f, 0.5f), crit);
        if (crit) fx.CritImpact();
    }

    public static void AllyMiss(PlayerStats p)
    {
        var fx = Get();
        if (fx == null || p == null) return;
        Vector2? pos = fx.AllyAnchor(p);
        if (pos != null) fx.Popup(pos.Value, "MISS", new Color(0.7f, 0.7f, 0.75f), false);
    }

    private static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);

    /// <summary>아군이 회복함 (PlayerStats.Heal 이 호출). amount = 실제로 오른 HP — 상태 카드 위에 초록 "+숫자".</summary>
    public static void AllyHeal(PlayerStats p, int amount)
    {
        var fx = Get();
        if (fx == null || p == null) return;
        Vector2? pos = fx.AllyAnchor(p);
        if (pos != null) fx.Popup(pos.Value, $"+{Mathf.Max(0, amount)}", HealColor, false);
    }

    /// <summary>적이 회복함 (EnemyStats.Heal 이 호출). 머리 위에 초록 "+숫자".</summary>
    public static void EnemyHeal(EnemyStats e, int amount)
    {
        var fx = Get();
        if (fx == null || e == null) return;
        fx.Popup(fx.EnemyAnchor(e), $"+{Mathf.Max(0, amount)}", HealColor, false);
    }

    /// <summary>적 머리 위 예고 표시 (모아 치기 등).</summary>
    public static void SetIntent(EnemyStats e, string label)
    {
        var fx = Get();
        if (fx == null || e == null) return;
        if (!fx._intents.TryGetValue(e, out var t) || t == null)
        {
            t = fx.CreateText(fx._root, "Intent", 34, FontStyles.Bold);
            t.rectTransform.sizeDelta = new Vector2(420f, 60f);
            fx._intents[e] = t;
        }
        t.text = label;
        t.color = new Color(1f, 0.45f, 0.15f);
        t.gameObject.SetActive(true);
    }

    public static void ClearIntent(EnemyStats e)
    {
        if (_instance == null || e == null) return;
        if (_instance._intents.TryGetValue(e, out var t) && t != null) Destroy(t.gameObject);
        _instance._intents.Remove(e);
    }

    // ─────────────────────────────────────────
    // 내부
    // ─────────────────────────────────────────

    private void Awake()
    {
        var canvasGo = new GameObject("BattleFX Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // 전투 UI 위 (적 상태창 100 보다는 아래)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        _root = (RectTransform)canvasGo.transform;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_hitStop != null && Time.timeScale < 1f) Time.timeScale = 1f;
    }

    private void LateUpdate()
    {
        // 예고 표시가 적을 따라다니고 깜빡인다
        var dead = new List<EnemyStats>();
        foreach (var kv in _intents)
        {
            if (kv.Key == null || kv.Value == null || kv.Key.IsDead()) { dead.Add(kv.Key); continue; }
            kv.Value.rectTransform.anchoredPosition = EnemyAnchor(kv.Key) + new Vector2(0f, 40f);
            Color c = kv.Value.color;
            c.a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 9f);
            kv.Value.color = c;
        }
        foreach (var e in dead)
        {
            if (_intents.TryGetValue(e, out var t) && t != null) Destroy(t.gameObject);
            _intents.Remove(e);
        }
    }

    /// <summary>적 스프라이트 머리 위의 캔버스 좌표.</summary>
    private Vector2 EnemyAnchor(EnemyStats e)
    {
        Vector3 world = e.transform.position;
        var sr = e.GetComponent<SpriteRenderer>();
        if (sr == null) sr = e.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) world = new Vector3(sr.bounds.center.x, sr.bounds.max.y, sr.bounds.center.z);
        Camera cam = Camera.main;
        Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(world) : new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
        return ScreenToLocal(screen);
    }

    /// <summary>아군 상태 카드 가운데의 캔버스 좌표.</summary>
    private Vector2? AllyAnchor(PlayerStats p)
    {
        var bm = FindFirstObjectByType<BattleManager>();
        RectTransform card = bm != null ? bm.GetPlayerCardRect(p) : null;
        if (card == null) return null;
        Canvas c = card.GetComponentInParent<Canvas>();
        Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, card.TransformPoint(card.rect.center));
        return ScreenToLocal(screen);
    }

    private Vector2 ScreenToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
        return local;
    }

    private void Popup(Vector2 pos, string text, Color color, bool crit)
    {
        var t = CreateText(_root, "Popup", crit ? 76 : 56, FontStyles.Bold);
        t.rectTransform.sizeDelta = new Vector2(400f, 120f);
        t.text = crit ? $"<size=45%>CRITICAL</size>\n{text}" : text;
        t.color = color;
        t.rectTransform.anchoredPosition = pos + new Vector2(Random.Range(-20f, 20f), 0f);
        StartCoroutine(PopupRoutine(t, crit));
    }

    private IEnumerator PopupRoutine(TextMeshProUGUI t, bool crit)
    {
        const float dur = 0.85f;
        Vector2 start = t.rectTransform.anchoredPosition;
        Color c0 = t.color;
        for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            float k = e / dur;
            t.rectTransform.anchoredPosition = start + new Vector2(0f, 90f * (1f - (1f - k) * (1f - k)));
            float punch = crit ? 1f + 0.5f * Mathf.Max(0f, 1f - k * 5f) : 1f; // 크리티컬은 처음에 크게 튀어나옴
            t.rectTransform.localScale = Vector3.one * punch;
            Color c = c0;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            t.color = c;
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    private void CritImpact()
    {
        if (hitStopSeconds > 0f && _hitStop == null && Time.timeScale > 0.5f) _hitStop = StartCoroutine(HitStop());
        if (shakeSeconds > 0f)
        {
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(Shake());
        }
    }

    private IEnumerator HitStop()
    {
        float prev = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopSeconds);
        if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = prev; // 그사이 다른 곳(일시정지 등)이 바꿨으면 건드리지 않음
        _hitStop = null;
    }

    private IEnumerator Shake()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        if (!_shaking) _camBasePos = cam.transform.position;
        _shaking = true;
        for (float e = 0f; e < shakeSeconds; e += Time.unscaledDeltaTime)
        {
            if (cam == null) yield break;
            float m = shakeMagnitude * (1f - e / shakeSeconds);
            cam.transform.position = _camBasePos + new Vector3(Random.Range(-m, m), Random.Range(-m, m), 0f);
            yield return null;
        }
        if (cam != null) cam.transform.position = _camBasePos;
        _shaking = false;
        _shake = null;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.richText = true;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 220);
        return t;
    }
}
