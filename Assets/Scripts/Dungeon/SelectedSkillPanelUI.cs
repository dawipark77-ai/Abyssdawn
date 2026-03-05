using UnityEngine;

/// <summary>
/// 선택된 스킬 패널 열기/닫기 전용 스크립트
/// </summary>
public class SelectedSkillPanelUI : MonoBehaviour
{
    /// <summary>
    /// 패널을 보이게 하고 싶을 때 호출
    /// (다른 버튼이나 코드에서 사용할 수 있음)
    /// </summary>
    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// CLOSE 버튼의 OnClick 에 연결할 함수
    /// </summary>
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}


