using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;       // Button, Image
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

        [Tooltip("슬롯 클릭용 Button (비워두면 slotRoot에서 자동 탐색). 선택 강조도 이 버튼의 이미지에 적용.")]
        public Button button;
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

    [Header("Swap 설정")]
    [Tooltip("선택된 슬롯 강조 색 (버튼 이미지 틴트)")]
    public Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);

    // ─────────────────────────────────────────────────────────────────
    // [2026-06-13 Tactics swap] 클릭→선택→맞바꿈. 이번엔 "동료끼리만".
    //   Hero(MainLine Slot1)는 선택은 되되 교환은 막고 로그만 남긴 뒤 선택 해제.
    // ─────────────────────────────────────────────────────────────────
    private enum SlotRegion { Hero, Active, Wait }

    private bool _hasSelection = false;
    private SlotRegion _selRegion;
    private int _selIndex = -1;          // Hero면 0(의미 없음)

    private Image _hlImage;              // 현재 강조 중인 이미지
    private Color _hlOriginalColor;     // 강조 전 원래 색 (복원용)

    private void OnEnable()
    {
        WireButtons();   // onClick 동적 연결 (RemoveAll → AddListener, 인덱스 클로저 캡처)
        ClearSelection();
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

    // ════════════════════════ Swap (동료끼리만) ════════════════════════

    /// <summary>7개 슬롯 Button의 onClick을 코드로 연결. 인덱스를 로컬 복사해 클로저로 캡처.</summary>
    private void WireButtons()
    {
        WireOne(heroSlot, SlotRegion.Hero, 0);
        for (int i = 0; i < activeSlots.Count; i++)
            WireOne(activeSlots[i], SlotRegion.Active, i);
        for (int i = 0; i < reserveSlots.Count; i++)
            WireOne(reserveSlots[i], SlotRegion.Wait, i);
    }

    private void WireOne(SlotRefs slot, SlotRegion region, int index)
    {
        if (slot == null) return;
        Button btn = ResolveButton(slot);
        if (btn == null)
        {
            Debug.LogWarning($"[TacticsSwap] {region} slot[{index}]에 Button을 찾지 못함 — 클릭 비활성");
            return;
        }
        slot.button = btn;   // 캐시 (강조 시 재사용)

        int capturedIndex = index;       // 클로저 캡처용 로컬 복사
        SlotRegion capturedRegion = region;
        btn.onClick.RemoveAllListeners();   // 인스펙터 영구 리스너는 영향 없음(런타임 AddListener만 제거)
        btn.onClick.AddListener(() => OnSlotClicked(capturedRegion, capturedIndex));
    }

    /// <summary>slot.button이 비어 있으면 slotRoot에서 Button을 탐색.</summary>
    private Button ResolveButton(SlotRefs slot)
    {
        if (slot == null) return null;
        if (slot.button != null) return slot.button;
        if (slot.slotRoot != null)
        {
            var b = slot.slotRoot.GetComponent<Button>();
            if (b != null) return b;
            b = slot.slotRoot.GetComponentInChildren<Button>(true);
            if (b != null) return b;
        }
        return null;
    }

    /// <summary>슬롯 클릭 진입점. 첫 클릭=선택, 같은 슬롯 재클릭=취소, 다른 슬롯=맞바꿈 시도.</summary>
    private void OnSlotClicked(SlotRegion region, int index)
    {
        // 첫 클릭 → 선택
        if (!_hasSelection)
        {
            _hasSelection = true;
            _selRegion = region;
            _selIndex = index;
            ApplyHighlight(GetSlot(region, index));
            Debug.Log($"[TacticsSwap] 선택: {region} slot[{index}]");
            return;
        }

        // 같은 슬롯 재클릭 → 취소
        if (region == _selRegion && index == _selIndex)
        {
            Debug.Log($"[TacticsSwap] 선택 취소: {region} slot[{index}]");
            ClearSelection();
            return;
        }

        // 다른 슬롯 → 맞바꿈 시도 후 선택 해제 + 화면 갱신
        TrySwap(_selRegion, _selIndex, region, index);
        ClearSelection();
        Refresh();
    }

    /// <summary>두 슬롯 영역/인덱스로 실제 데이터 교환. Hero가 끼면 막고 로그만.</summary>
    private void TrySwap(SlotRegion ra, int ia, SlotRegion rb, int ib)
    {
        // Hero 관련 swap은 이번 범위 밖 — 막고 로그만
        if (ra == SlotRegion.Hero || rb == SlotRegion.Hero)
        {
            Debug.Log("[TacticsSwap] Hero는 아직 이동 불가 — swap 취소 (다음 작업: 파티 순서 데이터 모델)");
            return;
        }

        if (ra == SlotRegion.Active && rb == SlotRegion.Active)
        {
            CompanionPartyPersistence.SwapActive(ia, ib);
        }
        else if (ra == SlotRegion.Wait && rb == SlotRegion.Wait)
        {
            CompanionPartyPersistence.SwapWait(ia, ib);
        }
        else
        {
            // 한쪽 활성 + 한쪽 대기 (순서 무관하게 정규화)
            int activeIdx = (ra == SlotRegion.Active) ? ia : ib;
            int waitIdx   = (ra == SlotRegion.Wait)   ? ia : ib;
            CompanionPartyPersistence.SwapActiveWait(activeIdx, waitIdx);
        }
    }

    private SlotRefs GetSlot(SlotRegion region, int index)
    {
        switch (region)
        {
            case SlotRegion.Hero:   return heroSlot;
            case SlotRegion.Active: return (index >= 0 && index < activeSlots.Count)  ? activeSlots[index]  : null;
            case SlotRegion.Wait:   return (index >= 0 && index < reserveSlots.Count) ? reserveSlots[index] : null;
            default: return null;
        }
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _selIndex = -1;
        ClearHighlight();
    }

    /// <summary>슬롯 버튼 이미지를 selectedColor로 틴트. 기존 강조는 먼저 해제.</summary>
    private void ApplyHighlight(SlotRefs slot)
    {
        ClearHighlight();
        if (slot == null) return;
        Image img = GetSlotImage(slot);
        if (img == null) return;
        _hlImage = img;
        _hlOriginalColor = img.color;
        img.color = selectedColor;
    }

    private void ClearHighlight()
    {
        if (_hlImage != null)
        {
            _hlImage.color = _hlOriginalColor;
            _hlImage = null;
        }
    }

    private Image GetSlotImage(SlotRefs slot)
    {
        Button btn = ResolveButton(slot);
        if (btn != null && btn.targetGraphic is Image tg) return tg;
        if (slot != null && slot.slotRoot != null) return slot.slotRoot.GetComponent<Image>();
        return null;
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
