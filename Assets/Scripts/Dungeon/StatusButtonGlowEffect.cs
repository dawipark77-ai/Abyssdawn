using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 버튼에 부착하여 PlayerStats의 특정 포인트(FreeStatPoints 또는 skillPoints)가
/// 0보다 클 때 황금빛 테두리를 표시. 별도 황금빛 테두리 이미지 GameObject를 Inspector에서
/// 연결받아 SetActive 토글 + alpha 깜빡임.
///
/// 클래스명은 역사적 이유로 StatusButtonGlowEffect 유지(씬/프리팹 참조 보존).
/// 실제로는 Status, Skills 등 여러 버튼에 재사용 가능 — Watched Stat enum으로 어느 포인트를 볼지 선택.
///
/// 사용 방법:
///   1) 버튼 자식으로 GoldenBorder Image GameObject 생성 (황금색 스프라이트, 버튼 위에 겹치도록 배치).
///   2) 버튼 GameObject에 이 컴포넌트 부착.
///   3) Inspector:
///      - Golden Border 필드에 자식 GoldenBorder 드래그
///      - Watched Stat을 선택 (Status 버튼=FreeStatPoints, Skills 버튼=SkillPoints)
///   4) blink 관련 값은 기본값으로 시작, 취향대로 조정.
/// </summary>
public class StatusButtonGlowEffect : MonoBehaviour
{
    /// <summary>어느 PlayerStats 포인트를 감시할지 선택. 기본값은 FreeStatPoints(=Status 버튼 기존 동작 호환).</summary>
    public enum WatchedStat
    {
        FreeStatPoints,  // 자유 스탯 포인트 (Status 버튼용)
        SkillPoints,     // 스킬 포인트(SP) (Skills 버튼용)
    }

    [Header("Glow Target")]
    [Tooltip("해당 포인트 > 0일 때 표시할 황금빛 테두리 이미지 GameObject. 비워두면 동작 안 함.")]
    public GameObject goldenBorder;

    [Tooltip("감시할 포인트 종류. Status 버튼=FreeStatPoints, Skills 버튼=SkillPoints")]
    public WatchedStat watchedStat = WatchedStat.FreeStatPoints;

    [Header("Border Style")]
    [Tooltip("테두리 색 (기본: 옅은 황금색). alpha 는 깜빡임이 덮어씀")]
    public Color borderColor = new Color(0.96f, 0.87f, 0.58f, 1f);

    [Tooltip("켜면 Golden Border 이미지에 스프라이트가 없을 때 Resources/UI/GlowBorder(가운데가 빈 테두리 프레임)를 씌워 테두리만 반짝이게 함")]
    public bool useFrameSprite = true;

    [Header("Blink Animation")]
    [Tooltip("깜빡임 ON/OFF. 끄면 maxAlpha로 고정 표시")]
    public bool blinkEnabled = true;

    [Tooltip("깜빡임 속도(라디안/초). 클수록 빠르게 깜빡")]
    [Range(0.5f, 10f)]
    public float blinkSpeed = 2.5f;

    [Tooltip("최소 alpha (sin 파형의 바닥)")]
    [Range(0f, 1f)]
    public float minAlpha = 0.35f;

    [Tooltip("최대 alpha (sin 파형의 천장)")]
    [Range(0f, 1f)]
    public float maxAlpha = 1f;

    [Header("Dawn Silver Lining (2026-10-07)")]
    [Tooltip("새벽빛 테두리 — 오른쪽 위 모서리가 옅은 황금빛으로 가장 밝고, 테두리를 따라 멀어질수록 옅어져 반대편(왼쪽 아래)은 검게 꺼져 있다. 끄면 예전 금빛 테두리")]
    public bool dawnLining = true;
    [Tooltip("새벽빛 색 — 아비스던의 옅은 황금")]
    public Color liningColor = new Color(0.96f, 0.87f, 0.58f, 1f);
    [Tooltip("빛이 시작되는 오른쪽 위 모서리 끝의 반짝임 (황금이 하얗게 타오르는 부분)")]
    public Color highlightColor = new Color(1f, 0.97f, 0.86f, 1f);
    [Tooltip("꺼진 쪽 테두리 색 (검정)")]
    public Color unlitColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("테두리 두께 (px)")]
    public float frameThickness = 6f;
    [Tooltip("새벽빛이 닿는 거리 (0~1, 오른쪽 위 모서리 → 왼쪽 아래 모서리 대각선 기준). 숨쉬며 이 범위 안에서 늘었다 줄었다 한다")]
    [Range(0.2f, 1f)] public float reachMin = 0.45f, reachMax = 0.85f;
    [Tooltip("새벽빛 숨쉬기 속도 = 깜빡임 속도 × 이 값 (밝아오는 새벽 느낌으로 느리게)")]
    [Range(0.2f, 2f)] public float liningSpeedMult = 0.65f;

    // 내부 상태
    private Coroutine _blinkRoutine;
    private Graphic[] _glowGraphics; // goldenBorder 및 그 자식 모든 Graphic — alpha 일괄 적용 (새벽빛 테두리 제외)
    private bool _subscribed = false;
    private RawImage _dawnFrame;
    private Texture2D _dawnTex;
    private Color32[] _dawnPixels;
    private float[] _dawnDist, _dawnMask;  // 픽셀마다: 오른쪽 위에서의 거리(0~1), 테두리 안(1)/밖(0) — 안티에일리어스 포함
    private int _texW, _texH;
    private float _lastReach = -1f;

    private void Awake()
    {
        Debug.Log($"[Glow-DIAG] Awake — GameObject='{gameObject.name}', activeInHierarchy={gameObject.activeInHierarchy}, goldenBorder field={(goldenBorder == null ? "NULL" : goldenBorder.name)}");
        ApplyBorderStyle();
        BuildDawnFrame();
    }

    // ─────────────────────────────────────────
    // 새벽빛 테두리 — 오른쪽 위 모서리부터 옅은 황금빛, 테두리를 따라 점점 옅어져 반대편은 검정
    // ─────────────────────────────────────────

    private void BuildDawnFrame()
    {
        if (!dawnLining || goldenBorder == null) return;
        var host = (RectTransform)transform;                    // 버튼 크기 기준 (금테 오브젝트는 높이가 버튼과 다를 수 있음)
        float w = Mathf.Max(20f, host.rect.width), h = Mathf.Max(20f, host.rect.height);

        // 금빛 테두리 그림은 숨기고 (오브젝트는 켜고 끄기용으로 그대로) 새벽빛 테두리를 그 위치에 그린다
        var gold = goldenBorder.GetComponent<Image>();
        if (gold != null) gold.enabled = false;

        var go = new GameObject("DawnFrame", typeof(RectTransform));
        go.transform.SetParent(goldenBorder.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = host.TransformPoint(host.rect.center);     // 버튼 가운데
        rt.sizeDelta = new Vector2(w, h);
        _dawnFrame = go.AddComponent<RawImage>();
        _dawnFrame.raycastTarget = false;

        // 화면에서 선명하도록 2배 해상도
        _texW = Mathf.RoundToInt(w * 2f); _texH = Mathf.RoundToInt(h * 2f);
        _dawnTex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        _dawnPixels = new Color32[_texW * _texH];
        _dawnDist = new float[_texW * _texH];
        _dawnMask = new float[_texW * _texH];
        float t = frameThickness * 2f;
        float diag = Mathf.Sqrt(_texW * (float)_texW + _texH * (float)_texH);
        for (int y = 0; y < _texH; y++)
            for (int x = 0; x < _texW; x++)
            {
                int i = y * _texW + x;
                // 테두리 띠: 바깥 가장자리에서 두께 t 안쪽까지 (1px 부드럽게)
                float edge = Mathf.Min(Mathf.Min(x + 0.5f, _texW - x - 0.5f), Mathf.Min(y + 0.5f, _texH - y - 0.5f));
                _dawnMask[i] = Mathf.Clamp01(t - edge + 0.5f);
                // 오른쪽 위 모서리(텍스처 y 는 아래가 0)에서의 거리, 대각선 길이로 0~1
                float dx = _texW - 1 - x, dy = _texH - 1 - y;
                _dawnDist[i] = Mathf.Sqrt(dx * dx + dy * dy) / diag;
            }
        _dawnFrame.texture = _dawnTex;
        PaintDawnFrame(reachMax, 1f);
    }

    /// <summary>reach 까지 새벽빛(옅은 황금)이 닿고 그 너머는 검정. 모서리 끝은 하얗게 (빛이 시작되는 곳).</summary>
    private void PaintDawnFrame(float reach, float intensity)
    {
        if (_dawnTex == null) return;
        Color lit = liningColor, dark = unlitColor;
        for (int i = 0; i < _dawnPixels.Length; i++)
        {
            float m = _dawnMask[i];
            if (m <= 0f) { _dawnPixels[i] = new Color32(0, 0, 0, 0); continue; }
            float k = Mathf.Clamp01(1f - _dawnDist[i] / Mathf.Max(0.01f, reach));
            k = k * k * (3f - 2f * k);                  // 부드럽게 옅어짐
            k *= intensity;
            Color c = Color.Lerp(dark, lit, k);
            if (k > 0.8f) c = Color.Lerp(c, highlightColor, (k - 0.8f) / 0.2f); // 모서리 끝만 하얗게
            c.a = m;
            _dawnPixels[i] = c;
        }
        _dawnTex.SetPixels32(_dawnPixels);
        _dawnTex.Apply(false);
    }

    /// <summary>새벽빛 숨쉬기: 닿는 거리가 reachMin~reachMax 로 늘었다 줄었다, 밝기 0.75~1.</summary>
    private void AnimateDawnLining(float time)
    {
        if (_dawnTex == null) return;
        float s = (Mathf.Sin(time * blinkSpeed * liningSpeedMult) + 1f) * 0.5f;
        s = s * s * (3f - 2f * s);
        float reach = Mathf.Lerp(reachMin, reachMax, s);
        if (Mathf.Abs(reach - _lastReach) < 0.004f) return;   // 거의 같으면 다시 그리지 않음
        _lastReach = reach;
        PaintDawnFrame(reach, Mathf.Lerp(0.75f, 1f, s));
    }

    private void OnDestroy()
    {
        if (_dawnTex != null) Destroy(_dawnTex);
    }

    /// <summary>
    /// [2026-10-05] 버튼 전체가 하얗게 번쩍이던 것을 옅은 황금색 테두리만 반짝이도록.
    /// 스프라이트 없는 Image(=꽉 찬 사각형)면 테두리 프레임을 9-slice 로 씌우고 가운데는 비운다.
    /// </summary>
    private void ApplyBorderStyle()
    {
        if (goldenBorder == null) return;
        var img = goldenBorder.GetComponent<Image>();
        if (img == null) return;
        if (useFrameSprite && img.sprite == null)
        {
            var frame = Resources.Load<Sprite>("UI/GlowBorder");
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Sliced;
                img.fillCenter = false;
            }
        }
        Color c = borderColor;
        c.a = img.color.a;
        img.color = c;
    }

    private void Start()
    {
        // Start는 첫 Update 직전 1회 호출. PlayerStats가 같은 프레임에 Awake/OnEnable을 끝낸 이후라 상태가 안정적.
        Debug.Log("[Glow-DIAG] Start — 안정화 후 RefreshGlowState 재호출 (PlayerStats 초기화 완료 시점 동기화)");
        RefreshGlowState("Start");
    }

    private int _refreshCallCount = 0;

    private void OnEnable()
    {
        Debug.Log($"[Glow-DIAG] OnEnable 호출됨. GameObject='{gameObject.name}', activeInHierarchy={gameObject.activeInHierarchy}, frame={Time.frameCount}");
        PlayerStats.OnStatusChanged += OnPlayerStatusChanged;
        _subscribed = true;
        // 활성화 즉시 한 번 동기화 (씬 진입 시 이미 FreeStatPoints > 0일 수 있음)
        Debug.Log("[Glow-DIAG] OnEnable → 즉시 RefreshGlowState 호출 (init sync)");
        RefreshGlowState("OnEnable");
    }

    private void OnDisable()
    {
        Debug.Log($"[Glow-DIAG] OnDisable 호출됨. GameObject='{gameObject.name}', frame={Time.frameCount}");
        if (_subscribed)
        {
            PlayerStats.OnStatusChanged -= OnPlayerStatusChanged;
            _subscribed = false;
        }
        StopBlink();
    }

    private void OnPlayerStatusChanged()
    {
        Debug.Log("[Glow-DIAG] OnPlayerStatusChanged 이벤트 수신 → RefreshGlowState 호출");
        RefreshGlowState("Event:OnStatusChanged");
    }

    /// <summary>FreeStatPoints 값을 읽어 goldenBorder 표시/숨김 + 깜빡임 시작/중단.</summary>
    private void RefreshGlowState(string caller = "(unknown)")
    {
        _refreshCallCount++;
        Debug.Log($"[Glow-DIAG] === RefreshGlowState #{_refreshCallCount} 진입 === caller='{caller}', frame={Time.frameCount}");

        if (goldenBorder == null)
        {
            Debug.LogWarning("[Glow-DIAG] ✗ goldenBorder == NULL → Inspector 필드 미연결. 함수 종료.");
            return;
        }
        Debug.Log($"[Glow-DIAG] goldenBorder OK: '{goldenBorder.name}', activeSelf={goldenBorder.activeSelf}, activeInHierarchy={goldenBorder.activeInHierarchy}");

        var player = FindFirstObjectByType<PlayerStats>();
        if (player == null)
        {
            Debug.LogWarning("[Glow-DIAG] ✗ FindFirstObjectByType<PlayerStats>() == NULL → 씬에 PlayerStats 없음. shouldGlow=false 처리.");
        }
        else
        {
            Debug.Log($"[Glow-DIAG] PlayerStats 발견: name='{player.playerName}', InstanceID={player.GetInstanceID()}, scene='{player.gameObject.scene.name}'");
        }

        int pointValue = 0;
        if (player != null)
        {
            switch (watchedStat)
            {
                case WatchedStat.FreeStatPoints: pointValue = player.FreeStatPoints; break;
                case WatchedStat.SkillPoints:    pointValue = player.skillPoints;    break;
            }
        }
        bool shouldGlow = pointValue > 0;
        Debug.Log($"[Glow-DIAG] watchedStat={watchedStat}, value={pointValue} → shouldGlow={shouldGlow}");

        if (shouldGlow)
        {
            bool wasActive = goldenBorder.activeSelf;
            if (!wasActive)
            {
                goldenBorder.SetActive(true);
                Debug.Log($"[Glow-DIAG] ✓ goldenBorder.SetActive(true) 호출. 호출 후 activeSelf={goldenBorder.activeSelf}, activeInHierarchy={goldenBorder.activeInHierarchy}");
                if (!goldenBorder.activeInHierarchy)
                {
                    Debug.LogWarning($"[Glow-DIAG] ⚠ SetActive(true) 했지만 activeInHierarchy=false. 부모 GameObject가 꺼져 있을 가능성. 부모='{(goldenBorder.transform.parent != null ? goldenBorder.transform.parent.name : "(root)")}'");
                }
            }
            else
            {
                Debug.Log("[Glow-DIAG] goldenBorder 이미 active 상태 — SetActive 스킵");
            }

            CacheGraphicsIfNeeded();
            Debug.Log($"[Glow-DIAG] Graphics 캐시: {(_glowGraphics == null ? "NULL" : _glowGraphics.Length.ToString() + " 개")}");

            if (_blinkRoutine == null)
            {
                _blinkRoutine = StartCoroutine(BlinkRoutine());
                Debug.Log("[Glow-DIAG] ✓ BlinkRoutine 코루틴 시작");
            }
            else
            {
                Debug.Log("[Glow-DIAG] BlinkRoutine 이미 실행 중 — 중복 시작 안 함");
            }
        }
        else
        {
            Debug.Log("[Glow-DIAG] shouldGlow=false → StopBlink + SetActive(false)");
            StopBlink();
            if (goldenBorder.activeSelf) goldenBorder.SetActive(false);
        }
        Debug.Log($"[Glow-DIAG] === RefreshGlowState #{_refreshCallCount} 종료 ===");
    }

    private void CacheGraphicsIfNeeded()
    {
        if (_glowGraphics != null && _glowGraphics.Length > 0) return;
        if (goldenBorder == null) return;
        // goldenBorder 자체 + 모든 자식 Graphic (Image, TMP_Text 등) 일괄 캐시 — 새벽빛(새벽빛)은 따로 움직이므로 제외
        var all = goldenBorder.GetComponentsInChildren<Graphic>(true);
        var list = new System.Collections.Generic.List<Graphic>();
        foreach (var g in all) if (g != _dawnFrame) list.Add(g);
        _glowGraphics = list.ToArray();
    }

    private void StopBlink()
    {
        if (_blinkRoutine != null)
        {
            StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
        }
        // alpha 원복 (maxAlpha로 — 다시 켤 때 즉시 보이게)
        if (_glowGraphics != null)
        {
            foreach (var g in _glowGraphics)
            {
                if (g == null) continue;
                Color c = g.color;
                c.a = maxAlpha;
                g.color = c;
            }
        }
    }

    private IEnumerator BlinkRoutine()
    {
        float startTime = Time.unscaledTime;
        while (true)
        {
            float a;
            if (blinkEnabled)
            {
                float t = (Mathf.Sin((Time.unscaledTime - startTime) * blinkSpeed) + 1f) * 0.5f; // 0..1
                a = Mathf.Lerp(minAlpha, maxAlpha, t);
            }
            else
            {
                a = maxAlpha;
            }

            if (_glowGraphics != null)
            {
                foreach (var g in _glowGraphics)
                {
                    if (g == null) continue;
                    Color c = g.color;
                    c.a = a;
                    g.color = c;
                }
            }
            AnimateDawnLining(Time.unscaledTime - startTime);
            yield return null;
        }
    }
}
