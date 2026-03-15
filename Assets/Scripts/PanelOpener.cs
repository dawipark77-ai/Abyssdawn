using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 부착하면 클릭 시 targetPanel을 열거나 토글합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class PanelOpener : MonoBehaviour
{
    [Tooltip("클릭 시 열릴 패널")]
    public GameObject targetPanel;

    [Tooltip("targetPanel을 열 때 함께 닫을 패널 (선택)")]
    public GameObject closeOnOpen;

    [Tooltip("true면 버튼을 다시 눌러 패널을 닫을 수 있습니다")]
    public bool toggleMode = true;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (targetPanel == null) return;

        if (toggleMode && targetPanel.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (targetPanel != null)
            targetPanel.SetActive(true);

        if (closeOnOpen != null)
            closeOnOpen.SetActive(false);
    }

    public void Close()
    {
        if (targetPanel != null)
            targetPanel.SetActive(false);
    }
}
