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

    // 내부 상태
    private Coroutine _blinkRoutine;
    private Graphic[] _glowGraphics; // goldenBorder 및 그 자식 모든 Graphic — alpha 일괄 적용
    private bool _subscribed = false;

    private void Awake()
    {
        Debug.Log($"[Glow-DIAG] Awake — GameObject='{gameObject.name}', activeInHierarchy={gameObject.activeInHierarchy}, goldenBorder field={(goldenBorder == null ? "NULL" : goldenBorder.name)}");
        ApplyBorderStyle();
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
        // goldenBorder 자체 + 모든 자식 Graphic (Image, TMP_Text 등) 일괄 캐시
        _glowGraphics = goldenBorder.GetComponentsInChildren<Graphic>(true);
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
            yield return null;
        }
    }
}
