using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-14] 상태창 'Personality' 칸 — 새 게임에서 고른 성향의 그림(Resources/Persona) 과 이름.
/// 그림을 누르면 상세 팝업 (얻는 것 · 잃는 것).
/// 칸 구조: Personality / Slot1 (Image) · Text (TMP)
/// </summary>
public class PersonalitySlotUI : MonoBehaviour
{
    private void OnEnable()
    {
        Refresh();
        PlayerStats.OnStatusChanged += Refresh;
    }

    private void OnDisable()
    {
        PlayerStats.OnStatusChanged -= Refresh;
    }

    public static DispositionData Current()
    {
        string n = SaveSystem.Disposition;
        if (string.IsNullOrEmpty(n)) return null;
        if (!n.StartsWith("Disposition_")) n = "Disposition_" + n;
        return Resources.Load<DispositionData>("Dispositions/" + n);
    }

    public void Refresh()
    {
        var d = Current();
        var slot = transform.Find("Slot1");
        var img = slot != null ? slot.GetComponent<Image>() : null;
        if (img != null)
        {
            img.sprite = d != null ? d.icon : null;
            img.preserveAspect = true;
            img.color = d != null && d.icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            DetailClickTarget.Attach(img, () =>
            {
                var cur = Current();
                if (cur == null) return null;
                return (Name(cur), "성향", cur.icon, string.IsNullOrEmpty(cur.descriptionKR) ? cur.description : cur.descriptionKR);
            });
        }
        var t = transform.Find("Text");
        var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
        if (tmp != null) tmp.text = d != null ? Name(d) : "---";
    }

    private static string Name(DispositionData d)
        => string.IsNullOrEmpty(d.dispositionNameKR) ? d.dispositionName : d.dispositionNameKR;
}
