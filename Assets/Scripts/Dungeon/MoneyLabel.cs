using TMPro;
using UnityEngine;

/// <summary>
/// 던전 화면의 소지금 글자 ("Money"). PlayerWallet 이 바뀔 때마다 "123 G" 로 갱신한다.
/// MapManager 가 실행 시 이름으로 찾아 붙인다 (그 오브젝트나 자식의 TextMeshPro).
/// </summary>
public class MoneyLabel : MonoBehaviour
{
    public TextMeshProUGUI text;
    [Tooltip("{0} 자리에 금액")]
    public string format = "{0} G";

    /// <summary>이름이 objectName 인 오브젝트(비활성 포함)를 찾아 붙인다. 이미 있으면 그대로.</summary>
    public static MoneyLabel Bind(string objectName)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != objectName || t.GetComponentInParent<Canvas>(true) == null) continue;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = t.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) continue;
            var label = tmp.GetComponent<MoneyLabel>();
            if (label == null) label = tmp.gameObject.AddComponent<MoneyLabel>();
            label.text = tmp;
            label.Refresh(PlayerWallet.Gold);
            return label;
        }
        Debug.LogWarning($"[MoneyLabel] 소지금 글자 '{objectName}' 을(를) 찾지 못했습니다.");
        return null;
    }

    private void OnEnable()
    {
        PlayerWallet.OnChanged += Refresh;
        Refresh(PlayerWallet.Gold);
    }

    private void OnDisable()
    {
        PlayerWallet.OnChanged -= Refresh;
    }

    private void Refresh(int gold)
    {
        if (text == null) text = GetComponent<TextMeshProUGUI>();
        if (text != null) text.text = string.Format(format, gold);
    }
}
