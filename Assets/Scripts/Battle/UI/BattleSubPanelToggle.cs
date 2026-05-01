using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fight 영역: Item ↔ ItemPanel 토글, Skill ↔ SkillPanel 토글.
/// Attack / Defend / Flee 등은 누르면 서브 패널만 닫음 (기존 BattleUIManager 리스너에 추가만 함).
/// </summary>
[DefaultExecutionOrder(1000)]
public class BattleSubPanelToggle : MonoBehaviour
{
    [Header("열기·토글용 (RemoveAllListeners 후 재연결)")]
    public Button itemButton;
    public Button skillButton;

    [Header("패널")]
    public GameObject itemPanel;
    public GameObject skillPanel;

    [Header("스킬 패널 내용 채우기")]
    [Tooltip("비우면 씬에서 자동 검색. Skill 클릭 시 OnSkillButton으로 스킬 버튼을 생성합니다.")]
    public BattleManager battleManager;

    [Tooltip("SubPanels 등 — 부모가 꺼져 있으면 자식 패널이 화면에 안 나옵니다.")]
    public GameObject subPanelsRoot;

    [Tooltip("한쪽 열 때 반대쪽 패널 끄기")]
    public bool closeOtherWhenOpen = true;

    [Header("서브 패널만 닫기 (추가 리스너만 걸림 — 대전 버튼들)")]
    public Button attackButton;
    public Button defendButton;
    public Button fleeButton;

    [Tooltip("켜면 리스너가 다음 프레임에 연결됨 — 그동안 첫 클릭이 무시될 수 있음. 보통 끔(DefaultExecutionOrder로 충분).")]
    [SerializeField] private bool rewireAfterOneFrame = false;

    GameObject _resolvedItemPanel;
    GameObject _resolvedSkillPanel;

    private void Start()
    {
        if (rewireAfterOneFrame)
            StartCoroutine(DeferWireButtons());
        else
            WireButtons();
    }

    IEnumerator DeferWireButtons()
    {
        yield return null;
        WireButtons();
    }

    void WireButtons()
    {
        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(ToggleItemPanel);
        }
        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(ToggleSkillPanel);
        }

        AppendClosePanelsOnly(attackButton);
        AppendClosePanelsOnly(defendButton);
        AppendClosePanelsOnly(fleeButton);
    }

    void AppendClosePanelsOnly(Button b)
    {
        if (b == null) return;
        b.onClick.AddListener(CloseAllPanels);
    }

    /// <summary>
    /// 인스펙터에 비어 있으면 SubPanels/ItemPanel 등을 찾아 캐시합니다.
    /// itemPanel 미할당 시 첫 클릭에 SubPanels만 켜지고 ItemPanel은 안 켜지던 문제를 막습니다.
    /// </summary>
    void ResolvePanelRefsIfNeeded()
    {
        bool needSubRoot = subPanelsRoot == null;
        bool needItem = itemPanel == null && _resolvedItemPanel == null;
        bool needSkill = skillPanel == null && _resolvedSkillPanel == null;
        if (!needSubRoot && !needItem && !needSkill)
            return;

        Transform subTr = null;
        if (subPanelsRoot != null)
            subTr = subPanelsRoot.transform;
        else
        {
            subTr = transform.Find("SubPanels");
            if (subTr == null && transform.parent != null)
                subTr = transform.parent.Find("SubPanels");
            if (subTr == null)
            {
                GameObject found = GameObject.Find("SubPanels");
                if (found != null)
                    subTr = found.transform;
            }
            if (subTr != null && subPanelsRoot == null)
                subPanelsRoot = subTr.gameObject;
        }

        if (subTr != null)
        {
            if (itemPanel == null && _resolvedItemPanel == null)
            {
                Transform t = subTr.Find("ItemPanel");
                if (t != null)
                    _resolvedItemPanel = t.gameObject;
            }
            if (skillPanel == null && _resolvedSkillPanel == null)
            {
                Transform t = subTr.Find("SkillPanel");
                if (t != null)
                    _resolvedSkillPanel = t.gameObject;
            }
        }
    }

    GameObject GetItemPanel()
    {
        if (itemPanel != null)
            return itemPanel;
        ResolvePanelRefsIfNeeded();
        if (_resolvedItemPanel != null)
            return _resolvedItemPanel;
        _resolvedItemPanel = BattleItemPanel.FindItemPanelGameObjectInScene();
        return _resolvedItemPanel;
    }

    GameObject GetSkillPanel()
    {
        if (skillPanel != null)
            return skillPanel;
        ResolvePanelRefsIfNeeded();
        if (_resolvedSkillPanel != null)
            return _resolvedSkillPanel;
        _resolvedSkillPanel = BattleItemPanel.FindSkillPanelGameObjectInScene();
        return _resolvedSkillPanel;
    }

    /// <summary>Item 버튼: 열려 있으면 닫고, 아니면 연다.</summary>
    public void ToggleItemPanel()
    {
        ResolvePanelRefsIfNeeded();

        GameObject ip = GetItemPanel();
        if (ip != null && ip.activeSelf)
        {
            ip.SetActive(false);
            return;
        }

        if (closeOtherWhenOpen)
        {
            GameObject sp = GetSkillPanel();
            if (sp != null)
                sp.SetActive(false);
        }

        if (ip != null)
            BattleItemPanel.ActivateWithParents(ip);
    }

    /// <summary>Skill 버튼: 열려 있으면 닫고, 아니면 <see cref="BattleManager.OnSkillButton"/>으로 연다.</summary>
    public void ToggleSkillPanel()
    {
        ResolvePanelRefsIfNeeded();
        GameObject spOpen = GetSkillPanel();
        if (spOpen != null && spOpen.activeSelf)
        {
            spOpen.SetActive(false);
            return;
        }

        BattleManager bm = ResolveBattleManager();
        if (bm != null)
        {
            bm.OnSkillButton();
            return;
        }

        ResolvePanelRefsIfNeeded();
        GameObject ip = GetItemPanel();
        GameObject sp = GetSkillPanel();
        if (closeOtherWhenOpen && ip != null)
            ip.SetActive(false);
        if (sp != null)
            BattleItemPanel.ActivateWithParents(sp);
    }

    BattleManager ResolveBattleManager()
    {
        if (battleManager != null)
            return battleManager;
        return Object.FindFirstObjectByType<BattleManager>();
    }

    /// <summary>항상 연 상태로 열기 (다른 스크립트용).</summary>
    public void OpenItemPanel()
    {
        ResolvePanelRefsIfNeeded();
        GameObject sp = GetSkillPanel();
        if (closeOtherWhenOpen && sp != null)
            sp.SetActive(false);
        GameObject ip = GetItemPanel();
        if (ip != null)
            BattleItemPanel.ActivateWithParents(ip);
    }

    /// <summary>항상 연 상태로 열기 (다른 스크립트용).</summary>
    public void OpenSkillPanel()
    {
        BattleManager bm = ResolveBattleManager();
        if (bm != null)
            bm.OnSkillButton();
        else
        {
            ResolvePanelRefsIfNeeded();
            GameObject ip = GetItemPanel();
            if (closeOtherWhenOpen && ip != null)
                ip.SetActive(false);
            GameObject sp = GetSkillPanel();
            if (sp != null)
                BattleItemPanel.ActivateWithParents(sp);
        }
    }

    public void CloseAllPanels()
    {
        GameObject ip = GetItemPanel();
        if (ip != null)
            ip.SetActive(false);
        GameObject sp = GetSkillPanel();
        if (sp != null)
            sp.SetActive(false);
    }
}
