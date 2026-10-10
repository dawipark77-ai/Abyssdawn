using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-11] 한 카테고리 안의 트리 둘 사이를 오가는 버튼 (원정술 = 야전술 · 보급술).
/// 버튼에 붙여 두면 누를 때 target 페이지를 켜고 지금 페이지(이 버튼이 들어 있는 페이지)를 끈다.
/// 지금 보고 있는 쪽 버튼은 밝게, 다른 쪽은 어둡게 표시한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class LoreSubPageSwitch : MonoBehaviour
{
    [Tooltip("누르면 열 페이지")]
    public GameObject target;
    [Tooltip("이 버튼이 가리키는 페이지가 지금 페이지와 같으면(현재 탭) 밝게")]
    public bool isCurrentTab;

    public Color activeColor = new Color(0.96f, 0.87f, 0.58f, 1f);
    public Color inactiveColor = new Color(0.55f, 0.52f, 0.45f, 1f);

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        foreach (var t in GetComponentsInChildren<TMPro.TMP_Text>(true)) t.color = isCurrentTab ? activeColor : inactiveColor;
    }

    private void OnClick()
    {
        if (isCurrentTab || target == null) return;
        // 이 버튼이 속한 페이지 = target 과 같은 부모 아래의 조상
        Transform page = transform;
        while (page.parent != null && page.parent != target.transform.parent) page = page.parent;
        target.SetActive(true);
        if (page != null && page.gameObject != target) page.gameObject.SetActive(false);
    }
}
