using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lore_Category 아이콘 띠를 BtnTreePrev/BtnTreeNext로 한 칸씩 스크롤합니다.
/// Content 오브젝트를 좌우로 이동시켜 Viewport 마스크로 잘려 보이게 합니다.
/// </summary>
public class LoreCategoryScroller : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("Mask가 붙어있는 Viewport RectTransform")]
    public RectTransform viewport;

    [Tooltip("아이콘들이 들어있는 Content RectTransform (Horizontal Layout Group 오브젝트)")]
    public RectTransform content;

    [Tooltip("이전 버튼")]
    public Button btnPrev;

    [Tooltip("다음 버튼")]
    public Button btnNext;

    [Header("슬롯 설정")]
    [Tooltip("아이콘 1개의 너비 (px). 자동 계산하려면 0으로 두세요.")]
    public float slotWidth = 0f;

    [Tooltip("아이콘 사이 간격 (Horizontal Layout Group의 Spacing 값과 일치시키세요)")]
    public float spacing = 8f;

    [Header("애니메이션")]
    [Tooltip("스크롤 이동 시간 (초)")]
    public float scrollDuration = 0.2f;

    // ── 내부 상태 ──────────────────────────────────────────
    private int _currentIndex = 0;
    private int _totalItems   = 0;
    private float _itemStep   = 0f;   // 한 칸 이동 거리 (slotWidth + spacing)
    private bool _isScrolling = false;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (btnPrev != null) btnPrev.onClick.AddListener(OnPrev);
        if (btnNext != null) btnNext.onClick.AddListener(OnNext);
    }

    private void Start()
    {
        // ContentSizeFitter가 레이아웃을 계산한 뒤 Refresh해야 정확한 너비를 읽을 수 있음
        StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return new WaitForEndOfFrame();
        Refresh();
    }

    /// <summary>
    /// 아이콘 수가 바뀌었을 때 호출해 상태를 재계산합니다.
    /// </summary>
    public void Refresh()
    {
        if (content == null) return;

        _totalItems = content.childCount;

        // slotWidth가 0이면 첫 번째 자식 너비를 자동으로 읽음
        if (slotWidth <= 0f && _totalItems > 0)
        {
            var firstChild = content.GetChild(0).GetComponent<RectTransform>();
            if (firstChild != null)
                slotWidth = firstChild.rect.width;
        }

        _itemStep = slotWidth + spacing;

        // 인덱스가 범위를 벗어났으면 클램프
        _currentIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, _totalItems - 1));

        // 위치 즉시 적용 (애니메이션 없이)
        SetContentX(-_currentIndex * _itemStep, animate: false);

        UpdateButtons();
    }

    // ──────────────────────────────────────────────────────
    private void OnPrev()
    {
        if (_isScrolling || _currentIndex <= 0) return;
        _currentIndex--;
        SetContentX(-_currentIndex * _itemStep, animate: true);
        UpdateButtons();
    }

    private void OnNext()
    {
        if (_isScrolling || !CanScrollNext()) return;
        _currentIndex++;
        float targetX = Mathf.Max(-_currentIndex * _itemStep, GetMaxScrollX());
        SetContentX(targetX, animate: true);
        UpdateButtons();
    }

    // 마지막 아이콘이 Viewport 오른쪽 끝에 닿는 X 위치
    private float GetMaxScrollX()
    {
        if (viewport == null) return float.NegativeInfinity;
        // Content 너비 - Viewport 너비 = 스크롤 가능한 최대 거리
        return -(content.rect.width - viewport.rect.width);
    }

    private bool CanScrollNext()
    {
        float maxScroll = GetMaxScrollX();
        float currentX  = content.anchoredPosition.x;
        // 현재 위치가 최대 스크롤 위치보다 오른쪽(여유 있음)이면 스크롤 가능
        return currentX > maxScroll + 1f;
    }

    // ──────────────────────────────────────────────────────
    private void SetContentX(float targetX, bool animate)
    {
        if (content == null) return;

        if (!animate || scrollDuration <= 0f)
        {
            content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);
            return;
        }

        if (_scrollCoroutine != null)
            StopCoroutine(_scrollCoroutine);
        _scrollCoroutine = StartCoroutine(SmoothScroll(targetX));
    }

    private Coroutine _scrollCoroutine;

    private IEnumerator SmoothScroll(float targetX)
    {
        _isScrolling = true;

        float startX  = content.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < scrollDuration)
        {
            elapsed += Time.deltaTime;
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
    }

    // ──────────────────────────────────────────────────────
    private void UpdateButtons()
    {
        if (btnPrev != null) btnPrev.interactable = (_currentIndex > 0);
        if (btnNext != null) btnNext.interactable = CanScrollNext();
    }

    // ──────────────────────────────────────────────────────
    /// <summary>
    /// 외부에서 특정 인덱스로 바로 이동 (예: 선택된 카테고리 표시)
    /// </summary>
    public void ScrollToIndex(int index, bool animate = true)
    {
        _currentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, _totalItems - 1));
        SetContentX(-_currentIndex * _itemStep, animate);
        UpdateButtons();
    }
}
