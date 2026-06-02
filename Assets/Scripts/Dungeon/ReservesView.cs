using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Abyssdawn;            // CompanionSO
// CompanionPartyPersistence 는 글로벌 namespace

/// <summary>
/// 대기열(Reserves) 동료 명단을 CompanionPartyPersistence.WaitlistPaths 기반으로 표시.
/// 대기 슬롯 3칸(Reserves1~3). 각 칸에 Name / LV / HP / MP 표시.
///
/// 데이터 소스:
///   - WaitlistPaths[i] (resourcePath, string) → LoadCompanion → CompanionSO
///   - HP/MP는 CompanionSO.HP / CompanionSO.MP (대기 중 풀피, current 미보관)
///   - LV는 CompanionSO에 레벨 없음 → "—" 표시 (레벨업 안 하는 고정 스탯 동료)
///
/// 갱신: OnEnable 1회 (WaitlistPaths는 던전에서 변동 없음).
/// 빈 대기 슬롯(동료 없음)은 slotRoot.SetActive(false)로 숨김.
/// </summary>
public class ReservesView : MonoBehaviour
{
    [System.Serializable]
    public class ReserveSlot
    {
        [Tooltip("이 대기 슬롯 전체 GameObject (Reserves1~3). 동료 없으면 SetActive(false)로 숨김.")]
        public GameObject slotRoot;

        [Header("값 텍스트 (라벨은 씬의 정적 텍스트, 여기엔 값 _text만 연결)")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI lvText;    // 동료는 레벨 없음 → "—"
        public TextMeshProUGUI hpText;    // "max / max" (대기 중 풀피)
        public TextMeshProUGUI mpText;    // "max / max"
    }

    [Header("Reserve Slots (Reserves1=0, Reserves2=1, Reserves3=2 — WaitlistPaths 인덱스와 1:1)")]
    public List<ReserveSlot> slots = new List<ReserveSlot>();

    [Header("표시 설정")]
    [Tooltip("레벨 없는 대기 동료의 LV 칸에 표시할 문자열")]
    public string lvPlaceholder = "—";

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>WaitlistPaths를 읽어 각 대기 슬롯을 갱신. 외부에서도 호출 가능.</summary>
    public void Refresh()
    {
        var waitlist = CompanionPartyPersistence.WaitlistPaths;
        Debug.Log($"[ReservesView] Refresh — slots={slots.Count}, WaitlistPaths.Count={(waitlist != null ? waitlist.Count : 0)}");

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            // 해당 인덱스에 대기 동료가 없으면 슬롯 숨김
            if (waitlist == null || i >= waitlist.Count || string.IsNullOrEmpty(waitlist[i]))
            {
                if (slot.slotRoot != null) slot.slotRoot.SetActive(false);
                continue;
            }

            CompanionSO so = CompanionPartyPersistence.LoadCompanion(waitlist[i]);
            if (so == null)
            {
                Debug.LogWarning($"[ReservesView] 대기 슬롯 {i}: LoadCompanion 실패 '{waitlist[i]}' → 슬롯 숨김");
                if (slot.slotRoot != null) slot.slotRoot.SetActive(false);
                continue;
            }

            if (slot.slotRoot != null) slot.slotRoot.SetActive(true);
            FillSlot(slot, so);
        }
    }

    private void FillSlot(ReserveSlot slot, CompanionSO so)
    {
        if (slot.nameText != null) slot.nameText.text = so.CompanionName;
        if (slot.lvText != null)   slot.lvText.text   = lvPlaceholder;          // 레벨 없음 → "—"
        if (slot.hpText != null)   slot.hpText.text   = $"{so.HP} / {so.HP}";   // 대기 중 풀피 (max/max)
        if (slot.mpText != null)   slot.mpText.text   = $"{so.MP} / {so.MP}";
    }
}
