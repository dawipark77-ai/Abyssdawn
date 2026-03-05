using UnityEngine;

/// <summary>
/// Status / Equip / Skill(Magic) / Tactic / Lore 등
/// 메인 하단 버튼으로 여는 패널들을 한 번에 관리하는 매니저.
/// 한 번에 하나의 패널만 열리도록 보장합니다.
/// </summary>
public class MainMenuPanelManager : MonoBehaviour
{
    [Header("단일로 관리할 패널들")]
    public GameObject statusPanel;
    public GameObject equipPanel;
    public GameObject skillPanel;
    public GameObject tacticPanel;
    public GameObject ectPanel;
    public GameObject lorePanel;
    public GameObject selectedSkillPanel;

    private GameObject currentOpenPanel;

    private void Awake()
    {
        // 시작 시 열린 패널이 있으면 기록
        if (statusPanel != null && statusPanel.activeSelf) currentOpenPanel = statusPanel;
        else if (equipPanel != null && equipPanel.activeSelf) currentOpenPanel = equipPanel;
        else if (skillPanel != null && skillPanel.activeSelf) currentOpenPanel = skillPanel;
        else if (tacticPanel != null && tacticPanel.activeSelf) currentOpenPanel = tacticPanel;
        else if (ectPanel != null && ectPanel.activeSelf) currentOpenPanel = ectPanel;
        else if (lorePanel != null && lorePanel.activeSelf) currentOpenPanel = lorePanel;
        else if (selectedSkillPanel != null && selectedSkillPanel.activeSelf) currentOpenPanel = selectedSkillPanel;
    }

    /// <summary>
    /// 외부에서 패널을 열 때는 이 메서드를 통해 열어야
    /// 다른 패널이 자동으로 닫힙니다.
    /// </summary>
    public void OpenPanel(GameObject panelToOpen)
    {
        if (panelToOpen == null) return;

        // 이미 열려 있는 패널이 있고, 그것과 다르면 닫기
        if (currentOpenPanel != null && currentOpenPanel != panelToOpen)
        {
            currentOpenPanel.SetActive(false);
        }

        // 토글: 이미 열려 있으면 닫고, 아니면 열기
        bool willOpen = !panelToOpen.activeSelf;
        panelToOpen.SetActive(willOpen);

        currentOpenPanel = willOpen ? panelToOpen : null;
    }

    // 아래 메서드들은 버튼 OnClick 에 직접 연결하기 쉽게 만든 헬퍼들
    public void ToggleStatusPanel()       => OpenPanel(statusPanel);
    public void ToggleEquipPanel()        => OpenPanel(equipPanel);
    public void ToggleSkillPanel()        => OpenPanel(skillPanel);
    public void ToggleTacticPanel()       => OpenPanel(tacticPanel);
    public void ToggleEctPanel()          => OpenPanel(ectPanel);
    public void ToggleLorePanel()         => OpenPanel(lorePanel);
    public void ToggleSelectedSkillPanel()=> OpenPanel(selectedSkillPanel);
}


