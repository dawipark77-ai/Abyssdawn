using System;
using System.Collections;
using System.Collections.Generic;
using Abyssdawn;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전투 진입 전환 연출 (JRPG 인카운터 연출). 전부 코드로 만든다 — 씬에 배치할 것 없음.
///
/// 흐름: 던전 화면을 한 장 캡처 → 화면 맨 위에 덮음 → 그 사진으로 연출 → 그동안 전투 씬을 미리 불러 둠
///       → 연출이 끝나면(화면이 검게 덮인 상태) 전투 씬으로 바꾸고 → 검은 막을 걷어 전투 화면 공개.
/// 전투 → 던전 복귀, 게임 오버 이동은 LoadSceneWithFade(검게 → 불러오기 → 걷기).
///
/// 연출(무작위): 번쩍+흔들림 / 줌 인·아웃 반복(파판6) / 모자이크 / 조리개 / 블라인드 / 시계 방향 / 대각선 /
///               어둠이 번짐 / 유리 깨짐 / 발톱 베기 / 몬스터 실루엣 / 심장 박동
/// 수치는 실행 중 Hierarchy 의 "EncounterTransition" 오브젝트에서 바꿔 볼 수 있다 (DontDestroyOnLoad).
/// </summary>
public class EncounterTransition : MonoBehaviour
{
    public enum Effect
    {
        FlashShake, ZoomPulse, Mosaic, IrisClose, Blinds, ClockWipe, DiagonalWipe,
        DarkCreep, GlassShatter, ClawSlash, Silhouette, Heartbeat,
    }

    [Header("연출 선택")]
    [Tooltip("끄면 아래 Forced Effect 만 사용")]
    public bool randomEffect = true;
    public Effect forcedEffect = Effect.GlassShatter;
    [Tooltip("테스트용: 무작위 대신 순서대로 하나씩 (모든 연출 확인할 때)")]
    public bool cycleInOrder = false;

    [Header("속도 · 공개")]
    [Tooltip("1 = 기본, 2 = 두 배 빠르게")]
    [Range(0.25f, 3f)] public float speed = 1f;
    [Tooltip("전투 화면이 드러나는 시간 (초)")]
    public float revealSeconds = 0.35f;
    [Tooltip("전투 → 던전 등 일반 장면 전환의 어두워지는·밝아지는 시간 (초)")]
    public float fadeSeconds = 0.35f;

    public static bool IsPlaying { get; private set; }

    private static EncounterTransition _instance;
    private static int _cycle;

    private GameObject _canvasGo;
    private RectTransform _root;
    private RawImage _shot;
    private RectTransform _fx;
    private Image _black;
    private Texture2D _capture;
    private readonly List<RenderTexture> _tempRTs = new List<RenderTexture>();
    private Vector2 _focus; // 캔버스 좌표 (가운데 원점) — 조리개·깨짐 중심 = 플레이어 위치

    private static Sprite _white;
    private static Texture2D _hardHole, _softHole;

    // ─────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────

    /// <summary>던전 → 전투. 무작위 연출 후 전투 씬 공개. monsters = 이번에 나올 몬스터 (실루엣용, 없어도 됨).</summary>
    public static void EnterBattle(string battleScene, MonsterSO[] monsters)
    {
        if (IsPlaying) return;
        Ensure().StartCoroutine(_instance.EnterRoutine(battleScene, monsters));
    }

    /// <summary>일반 장면 전환: 검게 → 불러오기 → 걷기 (전투 → 던전 복귀, 게임 오버 등).</summary>
    public static void LoadSceneWithFade(string sceneName)
    {
        if (IsPlaying) return;
        Ensure().StartCoroutine(_instance.FadeRoutine(() => SceneManager.LoadSceneAsync(sceneName)));
    }

    public static void LoadSceneWithFade(int buildIndex)
    {
        if (IsPlaying) return;
        Ensure().StartCoroutine(_instance.FadeRoutine(() => SceneManager.LoadSceneAsync(buildIndex)));
    }

    // ─────────────────────────────────────────
    // 흐름
    // ─────────────────────────────────────────

    private static EncounterTransition Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("EncounterTransition");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<EncounterTransition>();
        _instance.Build();
        return _instance;
    }

    private void OnDestroy()
    {
        if (_instance == this) { _instance = null; IsPlaying = false; }
    }

    private IEnumerator EnterRoutine(string battleScene, MonsterSO[] monsters)
    {
        IsPlaying = true;
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) player.LockInput(this);
        _focus = player != null ? WorldToCanvas(player.transform.position) : Vector2.zero;

        // 1) 화면 캡처 (UI 포함 최종 화면)
        yield return new WaitForEndOfFrame();
        _capture = ScreenCapture.CaptureScreenshotAsTexture();
        Show();
        _shot.texture = _capture;
        _shot.gameObject.SetActive(true);

        // 2) 전투 씬 미리 불러오기 (연출이 끝날 때까지 전환 보류)
        AsyncOperation op = SceneManager.LoadSceneAsync(battleScene);
        if (op != null) op.allowSceneActivation = false;

        // 3) 연출 — 끝나면 검은 막이 화면 전체를 덮는다
        Effect e = PickEffect(monsters);
        Debug.Log($"[EncounterTransition] 전투 진입 연출: {e}");
        yield return StartCoroutine(Run(e, monsters));
        SetAlpha(_black, 1f);

        // 4) 전투 씬으로 전환
        if (op != null)
        {
            while (op.progress < 0.9f) yield return null;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
        }
        yield return null;

        // 5) 정리 후 공개
        ClearFx();
        yield return Tween(revealSeconds, k => SetAlpha(_black, 1f - k), unscaledRaw: true);
        Hide();
        IsPlaying = false;
    }

    private IEnumerator FadeRoutine(Func<AsyncOperation> load)
    {
        IsPlaying = true;
        Show();
        yield return Tween(fadeSeconds, k => SetAlpha(_black, k), unscaledRaw: true);
        AsyncOperation op = load();
        if (op != null) while (!op.isDone) yield return null;
        yield return null;
        yield return Tween(fadeSeconds, k => SetAlpha(_black, 1f - k), unscaledRaw: true);
        Hide();
        IsPlaying = false;
    }

    private Effect PickEffect(MonsterSO[] monsters)
    {
        bool hasSilhouette = FirstSprite(monsters) != null;
        var all = (Effect[])Enum.GetValues(typeof(Effect));
        if (cycleInOrder)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Effect c = all[_cycle++ % all.Length];
                if (c != Effect.Silhouette || hasSilhouette) return c;
            }
        }
        if (!randomEffect) return forcedEffect == Effect.Silhouette && !hasSilhouette ? Effect.FlashShake : forcedEffect;
        Effect pick;
        do { pick = all[UnityEngine.Random.Range(0, all.Length)]; } while (pick == Effect.Silhouette && !hasSilhouette);
        return pick;
    }

    private IEnumerator Run(Effect e, MonsterSO[] monsters)
    {
        switch (e)
        {
            case Effect.FlashShake: return FlashShake();
            case Effect.ZoomPulse: return ZoomPulse();
            case Effect.Mosaic: return Mosaic();
            case Effect.IrisClose: return HoleClose(HardHole, Color.black, 0.7f, 0f);
            case Effect.Blinds: return Blinds();
            case Effect.ClockWipe: return ClockWipe();
            case Effect.DiagonalWipe: return DiagonalWipe();
            case Effect.DarkCreep: return HoleClose(SoftHole, Color.black, 1.1f, 14f);
            case Effect.GlassShatter: return GlassShatter();
            case Effect.ClawSlash: return ClawSlash();
            case Effect.Silhouette: return Silhouette(monsters);
            default: return Heartbeat();
        }
    }

    // ─────────────────────────────────────────
    // 연출
    // ─────────────────────────────────────────

    /// <summary>1. 하얗게 세 번 번쩍이며 흔들림 (포켓몬·드퀘).</summary>
    private IEnumerator FlashShake()
    {
        Image flash = NewImage("Flash", new Color(1f, 1f, 1f, 0f), _fx);
        for (int i = 0; i < 3; i++)
        {
            yield return Tween(0.13f, k =>
            {
                SetAlpha(flash, 1f - k);
                float m = 40f * (1f - k);
                _shot.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-m, m), UnityEngine.Random.Range(-m, m));
            });
            yield return Wait(0.04f);
        }
        _shot.rectTransform.anchoredPosition = Vector2.zero;
        yield return Tween(0.2f, k => SetAlpha(_black, k));
    }

    /// <summary>2. 파판6식: 확대·축소를 빠르게 반복하며 잔상이 번지고 점점 하얗게 → 암전.</summary>
    private IEnumerator ZoomPulse()
    {
        RawImage ghost1 = NewRaw("Ghost1", _capture, new Color(1f, 1f, 1f, 0.35f));
        RawImage ghost2 = NewRaw("Ghost2", _capture, new Color(1f, 1f, 1f, 0.22f));
        Image glow = NewImage("Glow", new Color(1f, 1f, 1f, 0f), _fx);
        yield return Tween(1.0f, k =>
        {
            float wave = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 5f));
            float scale = 1f + (0.06f + 0.4f * k) * wave;
            float rot = Mathf.Sin(k * Mathf.PI * 5f) * 5f * k;
            _shot.rectTransform.localScale = Vector3.one * scale;
            _shot.rectTransform.localEulerAngles = new Vector3(0f, 0f, rot);
            ghost1.rectTransform.localScale = Vector3.one * scale * (1f + 0.08f * k);
            ghost2.rectTransform.localScale = Vector3.one * scale * (1f + 0.18f * k);
            ghost1.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rot);
            SetAlpha(glow, 0.55f * k * wave);
        });
        SetAlpha(glow, 1f);
        yield return Wait(0.06f);
        yield return Tween(0.18f, k => SetAlpha(_black, k));
    }

    /// <summary>3. 화면이 점점 굵은 픽셀로 깨짐 (슈퍼패미컴 모자이크).</summary>
    private IEnumerator Mosaic()
    {
        int[] blocks = { 3, 6, 12, 24, 48, 96 };
        foreach (int b in blocks)
        {
            var rt = new RenderTexture(Mathf.Max(1, _capture.width / b), Mathf.Max(1, _capture.height / b), 0) { filterMode = FilterMode.Point };
            Graphics.Blit(_capture, rt);
            _tempRTs.Add(rt);
            _shot.texture = rt;
            yield return Wait(0.1f);
        }
        yield return Tween(0.22f, k => SetAlpha(_black, k));
    }

    /// <summary>
    /// 4·8. 구멍이 뚫린 막이 좁혀 든다 — 조리개(딱딱한 원) / 어둠이 번짐(부드러운 가장자리).
    /// 중심 = 플레이어 위치. shake = 캡처 화면 흔들림 세기.
    /// </summary>
    private IEnumerator HoleClose(Texture2D hole, Color color, float seconds, float shake)
    {
        Vector2 size = _root.rect.size;
        float startR = FarthestCorner(_focus, size) * 1.15f;
        RawImage ring = NewRaw("Hole", hole, color);
        ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        Image[] bands = { NewImage("L", color, _fx), NewImage("R", color, _fx), NewImage("T", color, _fx), NewImage("B", color, _fx) };
        foreach (var b in bands) b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        yield return Tween(seconds, k =>
        {
            float ease = k * k; // 처음엔 천천히, 끝에 빠르게 닫힘
            float r = Mathf.Lerp(startR, 0f, ease);
            PlaceHole(ring, bands, _focus, r, size);
            if (shake > 0f)
            {
                float m = shake * k;
                _shot.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-m, m), UnityEngine.Random.Range(-m, m));
            }
        });
        _shot.rectTransform.anchoredPosition = Vector2.zero;
        SetAlpha(_black, 1f);
    }

    /// <summary>5. 가로 줄무늬가 차례로 내려와 닫힘 (블라인드).</summary>
    private IEnumerator Blinds()
    {
        const int count = 12;
        Vector2 size = _root.rect.size;
        float h = size.y / count;
        var bars = new List<Image>();
        for (int i = 0; i < count; i++)
        {
            Image bar = NewImage($"Bar{i}", Color.black, _fx);
            RectTransform rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * h);
            rt.sizeDelta = new Vector2(0f, h + 1f);
            rt.localScale = new Vector3(1f, 0f, 1f);
            bars.Add(bar);
        }
        yield return Tween(0.6f, k =>
        {
            for (int i = 0; i < count; i++)
            {
                float local = Mathf.Clamp01((k - i * 0.035f) / 0.2f);
                bars[i].rectTransform.localScale = new Vector3(1f, local, 1f);
            }
        });
        SetAlpha(_black, 1f);
    }

    /// <summary>6. 시계 방향으로 쓸어 닫힘.</summary>
    private IEnumerator ClockWipe()
    {
        Image wipe = NewImage("Clock", Color.black, _fx);
        wipe.sprite = White;
        wipe.type = Image.Type.Filled;
        wipe.fillMethod = Image.FillMethod.Radial360;
        wipe.fillOrigin = (int)Image.Origin360.Top;
        wipe.fillClockwise = true;
        wipe.fillAmount = 0f;
        // 정사각형으로 키워 화면 모서리까지 덮는다
        float side = _root.rect.size.magnitude;
        wipe.rectTransform.anchorMin = wipe.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        wipe.rectTransform.sizeDelta = new Vector2(side, side);
        yield return Tween(0.65f, k => wipe.fillAmount = 1f - (1f - k) * (1f - k));
        SetAlpha(_black, 1f);
    }

    /// <summary>7. 비스듬한 검은 막이 옆에서 쓸고 지나가며 닫힘.</summary>
    private IEnumerator DiagonalWipe()
    {
        Vector2 size = _root.rect.size;
        float len = 2f * (size.x + size.y);
        Image panel = NewImage("Diagonal", Color.black, _fx);
        RectTransform rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(len, len);
        rt.localEulerAngles = new Vector3(0f, 0f, 30f);
        Image edge = NewImage("Edge", new Color(1f, 0.85f, 0.5f, 0.9f), rt); // 앞쪽 금빛 가장자리
        edge.rectTransform.anchorMin = new Vector2(1f, 0f);
        edge.rectTransform.anchorMax = new Vector2(1f, 1f);
        edge.rectTransform.pivot = new Vector2(1f, 0.5f);
        edge.rectTransform.sizeDelta = new Vector2(10f, 0f);
        yield return Tween(0.5f, k =>
        {
            float ease = k * k;
            rt.anchoredPosition = new Vector2(Mathf.Lerp(-len, 0f, ease), 0f);
        });
        SetAlpha(_black, 1f);
    }

    /// <summary>9. 금이 가고 → 화면이 삼각형 조각으로 깨져 흩어지며 떨어짐.</summary>
    private IEnumerator GlassShatter()
    {
        // 금 가는 선 + 번쩍
        Image flash = NewImage("Flash", new Color(1f, 1f, 1f, 0.85f), _fx);
        var cracks = new List<Image>();
        for (int i = 0; i < 9; i++)
        {
            Image c = NewImage($"Crack{i}", new Color(1f, 1f, 1f, 0.95f), _fx);
            RectTransform rt = c.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = _focus;
            rt.sizeDelta = new Vector2(UnityEngine.Random.Range(250f, 700f), UnityEngine.Random.Range(3f, 7f));
            rt.localEulerAngles = new Vector3(0f, 0f, i * 40f + UnityEngine.Random.Range(-15f, 15f));
            rt.localScale = new Vector3(0f, 1f, 1f);
            cracks.Add(c);
        }
        yield return Tween(0.12f, k =>
        {
            SetAlpha(flash, 0.85f * (1f - k));
            foreach (var c in cracks) c.rectTransform.localScale = new Vector3(k, 1f, 1f);
        });
        yield return Wait(0.12f);

        // 조각으로 나누기: 격자 꼭짓점을 흔들어 삼각형 조각들
        Vector2 size = _root.rect.size;
        const int cols = 6, rows = 10;
        var pts = new Vector2[cols + 1, rows + 1];
        for (int x = 0; x <= cols; x++)
            for (int y = 0; y <= rows; y++)
            {
                Vector2 p = new Vector2(-size.x / 2f + x * size.x / cols, -size.y / 2f + y * size.y / rows);
                if (x > 0 && x < cols && y > 0 && y < rows)
                    p += new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f) * size.x / cols, UnityEngine.Random.Range(-0.35f, 0.35f) * size.y / rows);
                pts[x, y] = p;
            }

        Image backdrop = NewImage("Backdrop", Color.black, _fx);
        backdrop.transform.SetAsFirstSibling();
        foreach (var c in cracks) Destroy(c.gameObject);
        Destroy(flash.gameObject);
        _shot.gameObject.SetActive(false);

        var shards = new List<Shard>();
        for (int x = 0; x < cols; x++)
            for (int y = 0; y < rows; y++)
            {
                Vector2 a = pts[x, y], b = pts[x + 1, y], c = pts[x + 1, y + 1], d = pts[x, y + 1];
                bool flip = (x + y) % 2 == 0;
                AddShard(shards, flip ? new[] { a, b, c } : new[] { a, b, d }, size);
                AddShard(shards, flip ? new[] { a, c, d } : new[] { b, c, d }, size);
            }

        const float gravity = -2600f;
        yield return Tween(0.95f, k =>
        {
            float t = k * 0.95f;
            foreach (var s in shards)
            {
                float lt = Mathf.Max(0f, t - s.delay);
                RectTransform rt = s.graphic.rectTransform;
                rt.anchoredPosition = s.origin + s.velocity * lt + new Vector2(0f, 0.5f * gravity * lt * lt);
                rt.localEulerAngles = new Vector3(0f, 0f, s.spin * lt);
                Color col = s.graphic.color;
                col.a = lt < 0.45f ? 1f : Mathf.Clamp01(1f - (lt - 0.45f) / 0.35f);
                s.graphic.color = col;
            }
        });
        SetAlpha(_black, 1f);
    }

    /// <summary>유리 조각 하나의 움직임 (처음 위치 + 속도·중력·회전, 충격점에서 먼 조각일수록 늦게 출발).</summary>
    private struct Shard
    {
        public TransitionShard graphic;
        public Vector2 origin, velocity;
        public float spin, delay;
    }

    private void AddShard(List<Shard> list, Vector2[] poly, Vector2 size)
    {
        Vector2 center = Vector2.zero;
        foreach (var p in poly) center += p;
        center /= poly.Length;
        var verts = new Vector2[poly.Length];
        var uvs = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            verts[i] = poly[i] - center;
            uvs[i] = new Vector2(poly[i].x / size.x + 0.5f, poly[i].y / size.y + 0.5f);
        }
        var go = new GameObject("Shard", typeof(RectTransform));
        go.transform.SetParent(_fx, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = center;
        var g = go.AddComponent<TransitionShard>();
        g.Setup(_capture, verts, uvs);

        Vector2 dir = center - _focus;
        float dist = dir.magnitude;
        dir = dist > 0.01f ? dir / dist : UnityEngine.Random.insideUnitCircle.normalized;
        list.Add(new Shard
        {
            graphic = g,
            origin = center,
            velocity = dir * UnityEngine.Random.Range(250f, 800f) + new Vector2(0f, UnityEngine.Random.Range(150f, 450f)),
            spin = UnityEngine.Random.Range(-420f, 420f),
            delay = Mathf.Clamp01(dist / 1400f) * 0.18f, // 충격 지점에서 가까운 조각부터
        });
    }

    /// <summary>10. 붉은 발톱 자국 세 줄 → 가운데 자국을 따라 화면이 두 쪽으로 갈라져 밀려남.</summary>
    private IEnumerator ClawSlash()
    {
        const float angle = -35f;
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Vector2 normal = new Vector2(-dir.y, dir.x);
        Vector2 size = _root.rect.size;
        float len = size.magnitude * 1.1f;

        var marks = new List<Image>();
        for (int i = 0; i < 3; i++)
        {
            Image m = NewImage($"Claw{i}", new Color(0.85f, 0.05f, 0.05f, 0.95f), _fx);
            RectTransform rt = m.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = -dir * len / 2f + normal * (i - 1) * 90f;
            rt.sizeDelta = new Vector2(len, i == 1 ? 26f : 18f);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
            rt.localScale = new Vector3(0f, 1f, 1f);
            marks.Add(m);
        }
        for (int i = 0; i < marks.Count; i++)
        {
            RectTransform rt = marks[i].rectTransform;
            yield return Tween(0.07f, k => rt.localScale = new Vector3(k, 1f, 1f));
            yield return Wait(0.04f);
        }
        Image flash = NewImage("Flash", new Color(1f, 0.2f, 0.2f, 0.5f), _fx);
        yield return Tween(0.12f, k => SetAlpha(flash, 0.5f * (1f - k)));

        // 화면을 가운데 자국 선으로 두 쪽
        Image backdrop = NewImage("Backdrop", Color.black, _fx);
        backdrop.transform.SetAsFirstSibling();
        Vector2 hx = new Vector2(size.x / 2f, 0f), hy = new Vector2(0f, size.y / 2f);
        var rect = new List<Vector2> { -hx - hy, hx - hy, hx + hy, -hx + hy };
        TransitionShard upper = MakeHalf(ClipHalfPlane(rect, normal, true), size);
        TransitionShard lower = MakeHalf(ClipHalfPlane(rect, normal, false), size);
        _shot.gameObject.SetActive(false);

        yield return Tween(0.6f, k =>
        {
            float ease = k * k;
            upper.rectTransform.anchoredPosition = normal * 650f * ease;
            lower.rectTransform.anchoredPosition = -normal * 650f * ease;
            upper.rectTransform.localEulerAngles = new Vector3(0f, 0f, 7f * ease);
            lower.rectTransform.localEulerAngles = new Vector3(0f, 0f, -7f * ease);
            foreach (var m in marks) SetAlpha(m, 0.95f * (1f - k));
            Color c = Color.white; c.a = 1f - Mathf.Clamp01((k - 0.5f) / 0.5f);
            upper.color = c; lower.color = c;
        });
        SetAlpha(_black, 1f);
    }

    private TransitionShard MakeHalf(List<Vector2> poly, Vector2 size)
    {
        var go = new GameObject("Half", typeof(RectTransform));
        go.transform.SetParent(_fx, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        var uvs = new Vector2[poly.Count];
        for (int i = 0; i < poly.Count; i++) uvs[i] = new Vector2(poly[i].x / size.x + 0.5f, poly[i].y / size.y + 0.5f);
        var g = go.AddComponent<TransitionShard>();
        g.Setup(_capture, poly.ToArray(), uvs);
        return g;
    }

    /// <summary>볼록 다각형을 원점을 지나는 직선(법선 n)으로 자른 한쪽 (Sutherland–Hodgman).</summary>
    private static List<Vector2> ClipHalfPlane(List<Vector2> poly, Vector2 n, bool positive)
    {
        var result = new List<Vector2>();
        float sign = positive ? 1f : -1f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
            float da = Vector2.Dot(a, n) * sign, db = Vector2.Dot(b, n) * sign;
            if (da >= 0f) result.Add(a);
            if ((da >= 0f) != (db >= 0f)) result.Add(a + (b - a) * (da / (da - db)));
        }
        return result;
    }

    /// <summary>11. 암전 속에 이번에 나올 몬스터의 검은 실루엣(붉은 윤곽)이 번쩍.</summary>
    private IEnumerator Silhouette(MonsterSO[] monsters)
    {
        Image backdrop = NewImage("Backdrop", new Color(0f, 0f, 0f, 0f), _fx);
        Image flash = NewImage("Flash", new Color(1f, 1f, 1f, 0f), _fx);
        yield return Tween(0.12f, k => { SetAlpha(backdrop, k); SetAlpha(flash, k * 0.7f); });

        var sprites = new List<Sprite>();
        if (monsters != null)
            foreach (var m in monsters)
                if (m != null && m.Sprite != null && !sprites.Contains(m.Sprite) && sprites.Count < 3) sprites.Add(m.Sprite);

        var figures = new List<RectTransform>();
        float spacing = 330f;
        for (int i = 0; i < sprites.Count; i++)
        {
            var holder = new GameObject("Silhouette", typeof(RectTransform));
            holder.transform.SetParent(_fx, false);
            var hrt = (RectTransform)holder.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(480f, 560f);
            hrt.anchoredPosition = new Vector2((i - (sprites.Count - 1) / 2f) * spacing, 60f);
            Image rim = NewImage("Rim", new Color(0.8f, 0.05f, 0.05f, 1f), hrt);
            rim.sprite = sprites[i]; rim.preserveAspect = true;
            rim.rectTransform.localScale = Vector3.one * 1.06f;
            Image body = NewImage("Body", Color.black, hrt);
            body.sprite = sprites[i]; body.preserveAspect = true;
            figures.Add(hrt);
        }
        flash.transform.SetAsLastSibling();
        yield return Tween(0.6f, k =>
        {
            SetAlpha(flash, 0.7f * (1f - k) * (1f - k));
            float s = Mathf.Lerp(0.85f, 1.05f, 1f - (1f - k) * (1f - k));
            foreach (var f in figures)
            {
                f.localScale = Vector3.one * s;
                f.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(-1.2f, 1.2f) * (1f - k));
            }
        });
        yield return Wait(0.15f);
        yield return Tween(0.18f, k => SetAlpha(_black, k));
    }

    /// <summary>12. 붉은 테두리가 심장 박동처럼 두 번 두근 → 세 번째에 어둠이 닫힘.</summary>
    private IEnumerator Heartbeat()
    {
        Vector2 size = _root.rect.size;
        Color red = new Color(0.45f, 0f, 0f, 1f);
        RawImage vignette = NewRaw("Vignette", SoftHole, new Color(red.r, red.g, red.b, 0f));
        vignette.rectTransform.anchorMin = vignette.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        float full = FarthestCorner(Vector2.zero, size) * 4f * 0.95f; // 구멍 반지름 = 크기/4
        vignette.rectTransform.sizeDelta = new Vector2(full, full);

        for (int beat = 0; beat < 2; beat++)
        {
            yield return Tween(0.13f, k =>
            {
                SetRawAlpha(vignette, 0.85f * k);
                _shot.rectTransform.localScale = Vector3.one * (1f + 0.035f * k);
            });
            yield return Tween(0.25f, k =>
            {
                SetRawAlpha(vignette, Mathf.Lerp(0.85f, 0.25f, k));
                _shot.rectTransform.localScale = Vector3.one * (1f + 0.035f * (1f - k));
            });
            yield return Wait(0.08f);
        }
        Destroy(vignette.gameObject);
        yield return HoleClose(SoftHole, red, 0.45f, 10f);
    }

    // ─────────────────────────────────────────
    // 화면 만들기 · 도우미
    // ─────────────────────────────────────────

    private void Build()
    {
        _canvasGo = new GameObject("Canvas", typeof(RectTransform));
        _canvasGo.transform.SetParent(transform, false);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // 모든 UI 위
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        _canvasGo.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)_canvasGo.transform;

        Image blocker = NewImage("InputBlocker", new Color(0f, 0f, 0f, 0f), _root); // 전환 중 터치 막기
        blocker.raycastTarget = true;

        var shotGo = new GameObject("Shot", typeof(RectTransform));
        shotGo.transform.SetParent(_root, false);
        _shot = shotGo.AddComponent<RawImage>();
        _shot.raycastTarget = false;
        Stretch(_shot.rectTransform);

        var fxGo = new GameObject("Fx", typeof(RectTransform));
        fxGo.transform.SetParent(_root, false);
        _fx = (RectTransform)fxGo.transform;
        Stretch(_fx);

        _black = NewImage("Black", new Color(0f, 0f, 0f, 0f), _root);
        _canvasGo.SetActive(false);
    }

    private void Show()
    {
        _canvasGo.SetActive(true);
        _shot.gameObject.SetActive(false);
        ResetShot();
        SetAlpha(_black, 0f);
    }

    private void Hide()
    {
        ClearFx();
        _canvasGo.SetActive(false);
    }

    private void ResetShot()
    {
        _shot.rectTransform.anchoredPosition = Vector2.zero;
        _shot.rectTransform.localScale = Vector3.one;
        _shot.rectTransform.localEulerAngles = Vector3.zero;
        _shot.color = Color.white;
    }

    private void ClearFx()
    {
        foreach (Transform c in _fx) Destroy(c.gameObject);
        foreach (var rt in _tempRTs) if (rt != null) rt.Release();
        _tempRTs.Clear();
        _shot.texture = null;
        _shot.gameObject.SetActive(false);
        ResetShot();
        if (_capture != null) { Destroy(_capture); _capture = null; }
    }

    private Image NewImage(string name, Color color, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        return img;
    }

    private RawImage NewRaw(string name, Texture tex, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_fx, false);
        var raw = go.AddComponent<RawImage>();
        raw.texture = tex;
        raw.color = color;
        raw.raycastTarget = false;
        Stretch(raw.rectTransform);
        return raw;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        Color c = g.color; c.a = Mathf.Clamp01(a); g.color = c;
    }

    private static void SetRawAlpha(RawImage g, float a) => SetAlpha(g, a);

    /// <summary>구멍 막 배치: ring = 가운데 구멍 텍스처(구멍 반지름 = 크기/4), bands = 바깥 네 띠.</summary>
    private static void PlaceHole(RawImage ring, Image[] bands, Vector2 c, float r, Vector2 size)
    {
        float s = Mathf.Max(0.01f, r * 4f);
        ring.rectTransform.anchoredPosition = c;
        ring.rectTransform.sizeDelta = new Vector2(s, s);
        float left = c.x - s / 2f, right = c.x + s / 2f, bottom = c.y - s / 2f, top = c.y + s / 2f;
        float W = size.x / 2f + 2f, H = size.y / 2f + 2f;
        SetBand(bands[0], -W, left, -H, H);
        SetBand(bands[1], right, W, -H, H);
        SetBand(bands[2], left, right, top, H);
        SetBand(bands[3], left, right, -H, bottom);
    }

    private static void SetBand(Image band, float x0, float x1, float y0, float y1)
    {
        float w = Mathf.Max(0f, x1 - x0), h = Mathf.Max(0f, y1 - y0);
        band.rectTransform.sizeDelta = new Vector2(w + 1f, h + 1f);
        band.rectTransform.anchoredPosition = new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
    }

    private static float FarthestCorner(Vector2 c, Vector2 size)
    {
        float hx = size.x / 2f, hy = size.y / 2f, best = 0f;
        foreach (var p in new[] { new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy) })
            best = Mathf.Max(best, Vector2.Distance(c, p));
        return best;
    }

    private Vector2 WorldToCanvas(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null || _root == null) return Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, cam.WorldToScreenPoint(world), null, out Vector2 local);
        return local;
    }

    private static MonsterSO FirstSprite(MonsterSO[] monsters)
    {
        if (monsters == null) return null;
        foreach (var m in monsters) if (m != null && m.Sprite != null) return m;
        return null;
    }

    private static Sprite White
    {
        get
        {
            if (_white == null) _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            return _white;
        }
    }

    private static Texture2D HardHole => _hardHole != null ? _hardHole : (_hardHole = MakeHole(256, 0.01f));
    private static Texture2D SoftHole => _softHole != null ? _softHole : (_softHole = MakeHole(256, 0.35f));

    /// <summary>가운데 원(반지름 = 크기/4)이 투명하고 바깥이 불투명한 흰 텍스처. softness = 가장자리 번짐 폭(반지름 비율).</summary>
    private static Texture2D MakeHole(int size, float softness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        float radius = size * 0.25f;
        float inner = radius * (1f - softness);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                float a = softness <= 0.02f ? (d > radius ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, radius, d));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private float Speed => Mathf.Max(0.1f, speed);

    private IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds / Speed;
        while (Time.unscaledTime < end) yield return null;
    }

    /// <summary>seconds 동안 step(0→1). unscaledRaw = 속도 배율 무시.</summary>
    private IEnumerator Tween(float seconds, Action<float> step, bool unscaledRaw = false)
    {
        float dur = Mathf.Max(0.001f, unscaledRaw ? seconds : seconds / Speed);
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            step(t / dur);
            yield return null;
        }
        step(1f);
    }
}
