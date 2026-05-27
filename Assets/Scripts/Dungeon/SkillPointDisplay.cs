using UnityEngine;
using TMPro;

/// <summary>
/// LoreTree_Panel 등에서 현재 SP(skillPoints) 잔량을 실시간 표시.
/// PlayerStats.OnStatusChanged 이벤트를 구독하여 자동 갱신.
///
/// 사용 방법:
///   1) SP 숫자를 표시할 TMP_Text가 있는 GameObject 선택
///      (예: LoreTree_Panel/SkillsPoint/Points)
///   2) 이 컴포넌트 부착
///   3) Inspector의 Points Text 필드에 같은 GameObject(또는 다른 TMP_Text) 드래그
///   4) Format은 기본 "{0}" — 숫자만 표시. "SP: {0}" 같이 변경 가능
///
/// 갱신 타이밍:
///   - 컴포넌트 OnEnable (패널 열릴 때)
///   - PlayerStats.OnStatusChanged 이벤트 발동 시 (학습 차감, 레벨업 증가 등)
/// </summary>
public class SkillPointDisplay : MonoBehaviour
{
    [Header("Display Target")]
    [Tooltip("SP 잔량을 표시할 TMP_Text. 비워두면 동작 안 함.")]
    public TMP_Text pointsText;

    [Header("Format")]
    [Tooltip("표시 형식. {0} 자리에 SP 숫자가 들어감. 기본 '{0}' (숫자만). 'SP: {0}' 같이 변경 가능.")]
    public string format = "{0}";

    [Header("Diagnostics")]
    [Tooltip("Debug.Log로 갱신 흐름 출력")]
    public bool diagnosticLogs = false;

    private bool _subscribed = false;

    private void OnEnable()
    {
        if (diagnosticLogs) Debug.Log($"[SkillPointDisplay] OnEnable — GameObject='{gameObject.name}', pointsText={(pointsText == null ? "NULL" : pointsText.name)}");
        PlayerStats.OnStatusChanged += Refresh;
        _subscribed = true;
        Refresh(); // 즉시 1회 동기화 (패널 열릴 때 현재 값 반영)
    }

    private void OnDisable()
    {
        if (_subscribed)
        {
            PlayerStats.OnStatusChanged -= Refresh;
            _subscribed = false;
        }
        if (diagnosticLogs) Debug.Log($"[SkillPointDisplay] OnDisable — GameObject='{gameObject.name}'");
    }

    /// <summary>현재 SP 값을 읽어 pointsText에 표시.</summary>
    public void Refresh()
    {
        if (pointsText == null)
        {
            if (diagnosticLogs) Debug.LogWarning("[SkillPointDisplay] pointsText == NULL — Inspector 필드 미연결, 스킵");
            return;
        }

        var player = FindFirstObjectByType<PlayerStats>();
        int sp = (player != null) ? player.skillPoints : 0;

        string newText = string.Format(format, sp);
        if (pointsText.text != newText)
        {
            pointsText.text = newText;
            if (diagnosticLogs) Debug.Log($"[SkillPointDisplay] Refresh: SP={sp} → pointsText.text='{newText}' (player={(player == null ? "NULL" : player.playerName)})");
        }
    }
}
