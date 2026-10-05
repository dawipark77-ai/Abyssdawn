using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lore_Category 아이콘 띠를 BtnTreePrev/BtnTreeNext로 한 칸씩 스크롤합니다.
/// Content 오브젝트를 좌우로 이동시켜 Viewport 마스크로 잘려 보이게 합니다.
///
/// [2026-10-06] 탭(WEAPONARY / UTILITY / Arcane Mystery)마다 Viewport 가 따로 있으므로,
///   지금 켜져 있는 Viewport 의 Content 를 따라 스크롤한다. 예전에는 Weaponart_Viewport 하나에 고정돼
///   다른 탭에서 버튼을 누르면 숨겨진 무기 띠만 움직였다. 탭별 스크롤 위치는 따로 기억.
///   넘길 것이 없으면(아이콘이 칸 안에 다 보이면) 버튼을 비활성화.
/// </summary>
public class LoreCategoryScroller : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("기본(첫) Viewport. 탭별 Viewport 는 Viewports 목록 또는 자동 검색으로 찾음")]
    public RectTransform viewport;

    [Tooltip("기본 Viewport 의 Content (Horizontal Layout Group 오브젝트)")]
    public RectTransform content;

    [Tooltip("탭별 Viewport 목록. 비워두면 이 오브젝트의 자식 중 이름에 'Viewport' 가 들어가고 'Content' 자식이 있는 것을 자동으로 찾음")]
    public List<RectTransform> viewports = new List<RectTransform>();

    [Tooltip("이전 버튼")]
    public Button btnPrev;

    [Tooltip("다음 버튼")]
    public Button btnNext;

    [Header("슬롯 설정")]
    [Tooltip("아이콘 1개의 너비 (px). 0이면 첫 아이콘 너비를 자동으로 읽음")]
    public float slotWidth = 0f;

    [Tooltip("아이콘 사이 간격. Content 에 Horizontal Layout Group 이 있으면 그 Spacing 을 우선 사용")]
    public float spacing = 8f;

    [Tooltip("실행 시 Viewport 에 마스크(RectMask2D)가 없으면 붙여서 칸 밖 아이콘을 잘라냄")]
    public bool addMaskIfMissing = true;

    [Header("애니메이션")]
    [Tooltip("스크롤 이동 시간 (초)")]
    public float scrollDuration = 0.2f;

    // ── 내부 상태 ──────────────────────────────────────────
    private readonly Dictionary<RectTransform, int> _indexByContent = new Dictionary<RectTransform, int>();
    private RectTransform _activeViewport;
    private RectTransform _defaultViewport, _defaultContent; // 인스펙터에 연결된 처음 짝
    private int _currentIndex = 0;
    private int _totalItems   = 0;
    private float _itemStep   = 0f;   // 한 칸 이동 거리 (아이콘 너비 + 간격)
    private bool _isScrolling = false;
    private Coroutine _scrollCoroutine;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (btnPrev != null) btnPrev.onClick.AddListener(OnPrev);
        if (btnNext != null) btnNext.onClick.AddListener(OnNext);
        _defaultViewport = viewport;
        _defaultContent = content;
        CollectViewports();
    }

    private void Start()
    {
        // ContentSizeFitter가 레이아웃을 계산한 뒤 Refresh해야 정확한 너비를 읽을 수 있음
        StartCoroutine(RefreshNextFrame());
    }

    private void OnEnable()
    {
        _activeViewport = null; // 패널을 다시 열면 지금 탭 기준으로 다시 맞춤
    }

    private void LateUpdate()
    {
        // 탭이 바뀌어 켜진 Viewport 가 달라지면 그 띠로 전환
        RectTransform active = FindActiveViewport();
        if (active != _activeViewport) SwitchTo(active);
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return new WaitForEndOfFrame();
        Refresh();
    }

    private void CollectViewports()
    {
        if (viewport != null && !viewports.Contains(viewport)) viewports.Add(viewport);
        if (viewports.Count <= 1)
        {
            foreach (Transform child in transform)
            {
                var rt = child as RectTransform;
                if (rt == null || !child.name.Contains("Viewport") || child.Find("Content") == null) continue;
                if (!viewports.Contains(rt)) viewports.Add(rt);
            }
        }
        if (addMaskIfMissing)
        {
            foreach (var v in viewports)
                if (v != null && v.GetComponent<Mask>() == null && v.GetComponent<RectMask2D>() == null)
                    v.gameObject.AddComponent<RectMask2D>();
        }
    }

    private RectTransform FindActiveViewport()
    {
        foreach (var v in viewports)
            if (v != null && v.gameObject.activeInHierarchy) return v;
        return null;
    }

    private RectTransform ContentOf(RectTransform v)
    {
        if (v == null) return null;
        var c = v.Find("Content") as RectTransform;
        if (c != null) return c;
        // 'Content' 라는 자식이 없으면 처음 인스펙터에 연결된 짝을 사용
        return v == _defaultViewport ? _defaultContent : null;
    }

    /// <summary>켜진 탭의 띠로 바꾼다. 이전 띠의 위치는 기억해 둔다.</summary>
    private void SwitchTo(RectTransform v)
    {
        if (_activeViewport != null && content != null) _indexByContent[content] = _currentIndex;
        if (_scrollCoroutine != null) { StopCoroutine(_scrollCoroutine); _scrollCoroutine = null; _isScrolling = false; }

        _activeViewport = v;
        if (v == null) { UpdateButtons(); return; }

        viewport = v;
        content = ContentOf(v);
        int saved;
        _currentIndex = (content != null && _indexByContent.TryGetValue(content, out saved)) ? saved : 0;
        slotWidth = 0f; // 탭마다 아이콘 크기가 다를 수 있으니 다시 읽음
        Refresh();
    }

    /// <summary>
    /// 아이콘 수가 바뀌었을 때 호출해 상태를 재계산합니다.
    /// </summary>
    public void Refresh()
    {
        if (content == null) { UpdateButtons(); return; }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        _totalItems = content.childCount;

        // slotWidth가 0이면 첫 번째 자식 너비를 자동으로 읽음
        if (slotWidth <= 0f && _totalItems > 0)
        {
            var firstChild = content.GetChild(0).GetComponent<RectTransform>();
            if (firstChild != null)
                slotWidth = firstChild.rect.width;
        }

        var hlg = content.GetComponent<HorizontalLayoutGroup>();
        float gap = hlg != null ? hlg.spacing : spacing;
        _itemStep = slotWidth + gap;

        // 인덱스가 범위를 벗어났으면 클램프
        _currentIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, _totalItems - 1));

        // 위치 즉시 적용 (애니메이션 없이). 마지막 아이콘이 오른쪽 끝을 넘어가지 않게.
        SetContentX(Mathf.Min(0f, Mathf.Max(-_currentIndex * _itemStep, GetMaxScrollX())), animate: false);

        UpdateButtons();
    }

    // ──────────────────────────────────────────────────────
    private void OnPrev()
    {
        if (_activeViewport == null || content == null) return;
        if (_isScrolling || _currentIndex <= 0) return;
        _currentIndex--;
        SetContentX(Mathf.Min(0f, -_currentIndex * _itemStep), animate: true);
        UpdateButtons();
    }

    private void OnNext()
    {
        if (_activeViewport == null || content == null) return;
        if (_isScrolling || !CanScrollNext()) return;
        _currentIndex++;
        float targetX = Mathf.Max(-_currentIndex * _itemStep, GetMaxScrollX());
        SetContentX(targetX, animate: true);
        UpdateButtons();
    }

    // 마지막 아이콘이 Viewport 오른쪽 끝에 닿는 X 위치 (띠가 칸보다 좁으면 0 — 넘길 것 없음)
    private float GetMaxScrollX()
    {
        if (viewport == null || content == null) return 0f;
        return Mathf.Min(0f, -(content.rect.width - viewport.rect.width));
    }

    private bool CanScrollNext()
    {
        if (content == null) return false;
        float maxScroll = GetMaxScrollX();
        float currentX  = content.anchoredPosition.x;
        // 현재 위치가 최대 스크롤 위치보다 오른쪽(여유 있음)이면 스크롤 가능
        return currentX > maxScroll + 1f;
    }

    // ──────────────────────────────────────────────────────
    private void SetContentX(float targetX, bool animate)
    {
        if (content == null) return;

        if (!animate || scrollDuration <= 0f || !isActiveAndEnabled)
        {
            content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
            return;
        }

        if (_scrollCoroutine != null)
            StopCoroutine(_scrollCoroutine);
        _scrollCoroutine = StartCoroutine(SmoothScroll(targetX));
    }

    private IEnumerator SmoothScroll(float targetX)
    {
        _isScrolling = true;

        float startX  = content.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < scrollDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scrollDuration);
            // EaseOut 느낌
            t = 1f - (1f - t) * (1f - t);
            content.anchoredPosition = new Vector2(
                Mathf.Lerp(startX, targetX, t),
                content.anchoredPosition.y
            );
            yield return null;
        }

        content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
        _isScrolling = false;
        _scrollCoroutine = null;
        UpdateButtons();
    }

    // ──────────────────────────────────────────────────────
    private void UpdateButtons()
    {
        bool has = _activeViewport != null && content != null;
        SetButton(btnPrev, has && _currentIndex > 0 && content.anchoredPosition.x < -1f);
        SetButton(btnNext, has && CanScrollNext());
    }

    [Tooltip("넘길 것이 없어 꺼진 버튼의 투명도 (버튼 색 설정과 상관없이 흐리게 보이도록)")]
    [Range(0f, 1f)] public float disabledAlpha = 0.3f;

    private void SetButton(Button b, bool on)
    {
        if (b == null) return;
        b.interactable = on;
        var cg = b.GetComponent<CanvasGroup>();
        if (cg == null) cg = b.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = on ? 1f : disabledAlpha;
    }

    // ──────────────────────────────────────────────────────
    /// <summary>
    /// 외부에서 특정 인덱스로 바로 이동 (예: 선택된 카테고리 표시)
    /// </summary>
    public void ScrollToIndex(int index, bool animate = true)
    {
        _currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _totalItems - 1));
        SetContentX(Mathf.Min(0f, Mathf.Max(-_currentIndex * _itemStep, GetMaxScrollX())), animate);
        UpdateButtons();
    }
}
