using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Abyssdawn;            // CompanionSO
// CompanionPartyPersistence 는 글로벌 namespace

/// <summary>
/// Tactics 화면에 파티 정보를 표시하는 컴포넌트 (1차: 표시만, swap은 다음 작업).
///
/// 표시 매핑:
///   - heroSlot (MainLine Slot1) = Hero (PlayerStats) — 실제 level
///   - activeSlots[0/1/2] (MainLine Slot2/3/4) = ActiveRoster[0/1/2] — LV "—"
///   - reserveSlots[0/1/2] (Reserves Slot1~3) = WaitlistPaths[0/1/2] — LV "—", HP 풀피(max/max)
///
/// 데이터 소스가 3종(PlayerStats / Entry / WaitEntry)이라 Hero는 단일 필드, 동료는 리스트로 분리.
/// 갱신: OnEnable 1회 (CompanionSlotView/ReservesView와 동일). 빈 슬롯은 틀 유지 + 값 비움.
/// </summary>
public class TacticsView : MonoBehaviour
{
    [System.Serializable]
    public class SlotRefs
    {
        [Tooltip("슬롯 전체 GameObject (선택 — 빈 슬롯도 틀 표시라 보통 항상 활성)")]
        public GameObject slotRoot;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI lvText;
        public TextMeshProUGUI hpText;   // current / max
        public TextMeshProUGUI mpText;   // current / max
    }

    [Header("Hero (MainLine Slot1)")]
    public SlotRefs heroSlot;

    [Header("Active Companions (MainLine Slot2/3/4 = ActiveRoster 0/1/2)")]
    public List<SlotRefs> activeSlots = new List<SlotRefs>();

    [Header("Reserve Companions (Reserves Slot1~3 = WaitlistPaths 0/1/2)")]
    public List<SlotRefs> reserveSlots = new List<SlotRefs>();

    [Header("표시 설정")]
    [Tooltip("레벨 없는 동료의 LV 칸 표시 문자열")]
    public string lvPlaceholder = "—";

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>Hero + 활성 + 대기 슬롯 전체 갱신. 외부 호출 가능.</summary>
    public void Refresh()
    {
        // ── Hero ──
        var player = FindFirstObjectByType<PlayerStats>();
        FillHero(heroSlot, player);

        // ── 활성 동료 (ActiveRoster) ──
        var roster = CompanionPartyPersistence.ActiveRoster;
        for (int i = 0; i < activeSlots.Count; i++)
        {
            var slot = activeSlots[i];
            if (slot == null) continue;
            if (slot.slotRoot != null) slot.slotRoot.SetActive(true);

            if (roster == null || i >= roster.Count || roster[i] == null)
            {
                ClearSlot(slot);
                continue;
            }
            FillActive(slot, roster[i]);
        }

        // ── 대기 동료 (WaitlistPaths) ──
        var waitlist = CompanionPartyPersistence.WaitlistPaths;
        for (int i = 0; i < reserveSlots.Count; i++)
        {
            var slot = reserveSlots[i];
            if (slot == null) continue;
            if (slot.slotRoot != null) slot.slotRoot.SetActive(true);

            if (waitlist == null || i >= waitlist.Count || waitlist[i] == null)
            {
                ClearSlot(slot);
                continue;
            }
            FillReserve(slot, waitlist[i]);
        }

        Debug.Log($"[TacticsView] Refresh — active={CompanionPartyPersistence.CountActive()}, wait={CompanionPartyPersistence.CountWait()}, hero={(player != null ? player.playerName : "NULL")}");
    }

    private void FillHero(SlotRefs slot, PlayerStats player)
    {
        if (slot == null) return;
        if (slot.slotRoot != null) slot.slotRoot.SetActive(true);

        if (player == null)
        {
            Debug.LogWarning("[TacticsView] Hero PlayerStats를 찾지 못함 — heroSlot 비움");
            ClearSlot(slot);
            return;
        }

        if (slot.nameText != null) slot.nameText.text = player.playerName;
        if (slot.lvText != null)   slot.lvText.text   = player.level.ToString();   // Hero는 실제 레벨
        if (slot.hpText != null)   slot.hpText.text   = $"{player.currentHP} / {player.maxHP}";
        if (slot.mpText != null)   slot.mpText.text   = $"{player.currentMP} / {player.maxMP}";
    }

    private void FillActive(SlotRefs slot, CompanionPartyPersistence.Entry entry)
    {
        CompanionSO so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
        if (so == null)
        {
            Debug.LogWarning($"[TacticsView] 활성: LoadCompanion 실패 '{entry.resourcePath}' → 값 비움");
            ClearSlot(slot);
            return;
        }
        if (slot.nameText != null) slot.nameText.text = so.CompanionName;
        if (slot.lvText != null)   slot.lvText.text   = lvPlaceholder;             // 동료 LV "—"
        if (slot.hpText != null)   slot.hpText.text   = $"{entry.currentHP} / {so.HP}";   // 현재HP(entry) / 최대(so)
        if (slot.mpText != null)   slot.mpText.text   = $"{entry.currentMP} / {so.MP}";
    }

    private void FillReserve(SlotRefs slot, CompanionPartyPersistence.WaitEntry wait)
    {
        CompanionSO so = CompanionPartyPersistence.LoadCompanion(wait.resourcePath);
        if (so == null)
        {
            Debug.LogWarning($"[TacticsView] 대기: LoadCompanion 실패 '{wait.resourcePath}' → 값 비움");
            ClearSlot(slot);
            return;
        }
        if (slot.nameText != null) slot.nameText.text = so.CompanionName;
        if (slot.lvText != null)   slot.lvText.text   = lvPlaceholder;            // 동료 LV "—"
        if (slot.hpText != null)   slot.hpText.text   = $"{so.HP} / {so.HP}";     // 대기 중 풀피 (max/max)
        if (slot.mpText != null)   slot.mpText.text   = $"{so.MP} / {so.MP}";
    }

    /// <summary>빈 슬롯 — 값 텍스트만 비움(라벨은 씬 정적 텍스트라 안 건드림). 틀은 유지.</summary>
    private void ClearSlot(SlotRefs slot)
    {
        if (slot == null) return;
        if (slot.nameText != null) slot.nameText.text = "";
        if (slot.lvText != null)   slot.lvText.text   = "";
        if (slot.hpText != null)   slot.hpText.text   = "";
        if (slot.mpText != null)   slot.mpText.text   = "";
    }
}
