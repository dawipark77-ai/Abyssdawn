using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// WEAPONARY / UTILITY / Arcane Mystery 버튼 클릭 시
/// 해당 Viewport만 열고 나머지는 닫는 탭 전환 컨트롤러
/// </summary>
public class LoreCategoryTabController : MonoBehaviour
{
    [System.Serializable]
    public class CategoryTab
    {
        [Tooltip("카테고리 버튼 (WEAPONARY, UTILITY, Arcane Mystery 등)")]
        public Button button;

        [Tooltip("이 버튼에 대응하는 Viewport 오브젝트")]
        public GameObject viewport;

        [Header("선택 색상 (선택사항)")]
        [Tooltip("선택됐을 때 버튼 배경색")]
        public Color selectedColor = new Color(0.3f, 0.5f, 1f, 1f);

        [Tooltip("미선택 상태 버튼 배경색")]
        public Color normalColor = new Color(1f, 1f, 1f, 0f);
    }

    [Header("탭 목록")]
    public List<CategoryTab> tabs = new List<CategoryTab>();

    [Header("설정")]
    [Tooltip("시작 시 자동으로 열 탭 인덱스 (0부터 시작, -1이면 모두 닫음)")]
    public int defaultTabIndex = 0;

    private int _currentIndex = -1;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int idx = i; // 클로저 캡처용
            if (tabs[idx].button != null)
                tabs[idx].button.onClick.AddListener(() => OpenTab(idx));
        }
    }

    private void Start()
    {
        // 모두 닫고 기본 탭 열기
        CloseAll();
        if (defaultTabIndex >= 0 && defaultTabIndex < tabs.Count)
            OpenTab(defaultTabIndex);
    }

    // ──────────────────────────────────────────────────────
    /// <summary>
    /// 해당 인덱스 탭을 열고 나머지는 닫습니다.
    /// 이미 열려있는 탭을 다시 누르면 닫힙니다 (토글).
    /// </summary>
    public void OpenTab(int index)
    {
        // 같은 탭 다시 누르면 토글 (닫기)
        if (_currentIndex == index)
        {
            CloseAll();
            return;
        }

        _currentIndex = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool isSelected = (i == index);

            // Viewport 활성/비활성
            if (tabs[i].viewport != null)
                tabs[i].viewport.SetActive(isSelected);

            // 버튼 배경색 변경
            if (tabs[i].button != null)
            {
                var img = tabs[i].button.GetComponent<Image>();
                if (img != null)
                    img.color = isSelected ? tabs[i].selectedColor : tabs[i].normalColor;
            }
        }
    }

    /// <summary>
    /// 모든 탭을 닫습니다.
    /// </summary>
    public void CloseAll()
    {
        _currentIndex = -1;

        foreach (var tab in tabs)
        {
            if (tab.viewport != null)
                tab.viewport.SetActive(false);

            if (tab.button != null)
            {
                var img = tab.button.GetComponent<Image>();
                if (img != null)
                    img.color = tab.normalColor;
            }
        }
    }
}
