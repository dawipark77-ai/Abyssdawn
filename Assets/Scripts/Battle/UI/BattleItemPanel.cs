using UnityEngine;

/// <summary>
/// 전투 중 Item 메뉴용 패널. BattleUIManager가 Item 버튼에서 <see cref="Open"/> 호출.
/// </summary>
public class BattleItemPanel : MonoBehaviour
{
    [Tooltip("씬 시작 시 패널 끄기 (레이아웃 편집 끄려면 인스펙터에서 해제)")]
    [SerializeField] private bool hideOnAwake = true;

    private void Awake()
    {
        if (hideOnAwake)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Transform.Find는 비활성 트리에서 실패할 수 있고, ItemPanel에 이 컴포넌트가 없으면 FindFirstObjectByType도 실패합니다.
    /// SubPanels가 꺼져 있어도 <see cref="GameObject.Find"/> + 자식 전체 검색으로 ItemPanel 오브젝트를 찾습니다.
    /// </summary>
    public static GameObject FindItemPanelGameObjectInScene()
    {
        BattleItemPanel bip = Object.FindFirstObjectByType<BattleItemPanel>(FindObjectsInactive.Include);
        if (bip != null)
            return bip.gameObject;

        GameObject sub = GameObject.Find("SubPanels");
        if (sub == null)
            return null;

        foreach (Transform t in sub.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "ItemPanel")
                return t.gameObject;
        }

        return null;
    }

    /// <summary><see cref="FindItemPanelGameObjectInScene"/>와 동일 이유로 이름 기준 검색.</summary>
    public static GameObject FindSkillPanelGameObjectInScene()
    {
        GameObject sub = GameObject.Find("SubPanels");
        if (sub == null)
            return null;

        foreach (Transform t in sub.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "SkillPanel")
                return t.gameObject;
        }

        return null;
    }

    /// <summary>패널만 켜면 부모(SubPanels 등)가 꺼져 있으면 화면에 안 나옵니다. 스킬 패널과 동일하게 부모 체인을 먼저 켭니다.</summary>
    public static void ActivateWithParents(GameObject panel)
    {
        if (panel == null) return;
        Transform p = panel.transform.parent;
        while (p != null)
        {
            if (!p.gameObject.activeSelf)
                p.gameObject.SetActive(true);
            p = p.parent;
        }
        panel.SetActive(true);
    }

    public void Open()
    {
        ActivateWithParents(gameObject);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}
