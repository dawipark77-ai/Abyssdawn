using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 화면의 창(패널)들을 한 번에 하나만 열리게 한다.
/// 어떤 창이 새로 열리면, 그전에 열려 있던 다른 창은 자동으로 닫히고 새 창은 맨 앞으로 와서 살짝 페이드 인된다.
/// 전체 지도가 열려 있으면 그것도 닫는다 (전체 지도를 열 때는 반대로 창들을 닫음 — DungeonFullMap).
///
/// 각 버튼(Skills, Oath & Path, 하단 메뉴 등)이 제각각 SetActive 로 창을 열기 때문에,
/// 버튼 코드를 고치지 않고 "창이 켜진 순간"을 감지해 처리한다. 같은 프레임에 함께 켜진 창들은 서로 닫지 않는다.
/// MapManager 가 실행 시 Canvas 에 자동으로 붙인다.
/// </summary>
public class DungeonPanelGroup : MonoBehaviour
{
    /// <summary>Canvas 바로 아래의 창 이름들 (항상 떠 있는 메뉴·버튼은 제외).</summary>
    public static readonly string[] DefaultPanelNames =
    {
        "ItemPanel", "StatusPanel", "LoreTree_Panel", "SelectedSkill_Panel", "Class", "InventoryOverlay", "Tactics",
    };

    [Tooltip("새로 열린 창이 나타나는 시간 (초). 0이면 바로")]
    public float fadeInSeconds = 0.12f;

    private readonly List<GameObject> _panels = new List<GameObject>();
    private readonly List<bool> _wasActive = new List<bool>();

    public static DungeonPanelGroup Instance { get; private set; }

    /// <summary>
    /// 창들이 들어 있는 Canvas 를 찾는다. 이름이 같은 오브젝트가 여럿일 수 있어,
    /// 이름이 canvasName 이면서 등록할 창을 하나라도 자식으로 가진 Canvas 를 고른다.
    /// </summary>
    public static Transform FindPanelCanvas(string canvasName)
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c.name != canvasName) continue;
            foreach (string panel in DefaultPanelNames)
                if (c.transform.Find(panel) != null) return c.transform;
        }
        return null;
    }

    /// <summary>canvas 아래에서 창들을 이름으로 찾아 등록한다 (꺼져 있는 창도 찾음).</summary>
    public static DungeonPanelGroup Setup(Transform canvas)
    {
        if (canvas == null) return null;
        var group = canvas.GetComponent<DungeonPanelGroup>();
        if (group == null) group = canvas.gameObject.AddComponent<DungeonPanelGroup>();

        group._panels.Clear();
        group._wasActive.Clear();
        foreach (string name in DefaultPanelNames)
        {
            Transform t = canvas.Find(name);
            if (t == null) continue;
            group._panels.Add(t.gameObject);
            group._wasActive.Add(t.gameObject.activeSelf);
        }
        Instance = group;
        Debug.Log($"[DungeonPanelGroup] 창 {group._panels.Count}개를 '하나씩만 열림'으로 관리합니다.");
        return group;
    }

    public bool AnyOpen
    {
        get
        {
            foreach (var p in _panels) if (p != null && p.activeSelf) return true;
            return false;
        }
    }

    /// <summary>모든 창 닫기 (전체 지도를 열 때 등).</summary>
    public void CloseAll()
    {
        for (int i = 0; i < _panels.Count; i++)
        {
            if (_panels[i] != null && _panels[i].activeSelf) _panels[i].SetActive(false);
            _wasActive[i] = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        bool anyNew = false;
        for (int i = 0; i < _panels.Count; i++)
        {
            GameObject p = _panels[i];
            if (p != null && p.activeSelf && !_wasActive[i]) { anyNew = true; break; }
        }

        if (anyNew)
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                GameObject p = _panels[i];
                if (p == null || !p.activeSelf) continue;
                if (_wasActive[i])
                {
                    // 전에 열려 있던 창 → 닫기
                    p.SetActive(false);
                }
                else
                {
                    // 이번에 열린 창 → 맨 앞으로 (파티 카드·메뉴 등 Canvas 안의 다른 것들 위에) + 페이드 인
                    p.transform.SetAsLastSibling();
                    if (fadeInSeconds > 0f) StartCoroutine(FadeIn(p));
                }
            }

            var fullMap = FindFirstObjectByType<DungeonFullMap>();
            if (fullMap != null && fullMap.IsOpen) fullMap.SetOpen(false);
        }

        for (int i = 0; i < _panels.Count; i++)
            _wasActive[i] = _panels[i] != null && _panels[i].activeSelf;
    }

    private IEnumerator FadeIn(GameObject panel)
    {
        var group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();
        for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            if (panel == null || !panel.activeSelf) break;
            group.alpha = t / fadeInSeconds;
            yield return null;
        }
        if (group != null) group.alpha = 1f;
    }
}
