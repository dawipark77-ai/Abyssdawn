using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Status 버튼에 부착하여 PlayerStats.FreeStatPoints > 0 일 때 황금빛 테두리를 표시.
/// 별도 황금빛 테두리 이미지 GameObject를 Inspector에서 연결받아 SetActive 토글 + alpha 깜빡임.
///
/// 사용 방법:
///   1) Status 버튼 자식으로 GoldenBorder Image GameObject 생성 (황금색 스프라이트, 버튼 위에 겹치도록 배치).
///   2) Status 버튼 GameObject에 이 컴포넌트 부착.
///   3) Inspector의 Golden Border 필드에 자식 GoldenBorder 드래그.
///   4) blink 관련 값은 기본값으로 시작, 취향대로 조정.
/// </summary>
public class StatusButtonGlowEffect : MonoBehaviour
{
    [Header("Glow Target")]
    [Tooltip("FreeStatPoints > 0일 때 표시할 황금빛 테두리 이미지 GameObject. 비워두면 동작 안 함.")]
    public GameObject goldenBorder;

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

        int free = (player != null) ? player.FreeStatPoints : 0;
        bool shouldGlow = free > 0;
        Debug.Log($"[Glow-DIAG] FreeStatPoints={free} → shouldGlow={shouldGlow}");

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
