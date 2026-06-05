using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동료 방출(Release) 컨트롤러. 활성 슬롯(Companion_SLot2/3/4)과 대기 슬롯(Reserves1/2/3)의
/// Release 버튼을 인덱스로 연결하고, YesorNo 확인 패널 → YES 시 방출 → Refresh.
///
/// 식별: 고정 3칸이라 인덱스=슬롯 고정. 버튼이 자기 슬롯 인덱스만 알면 됨.
///   - 활성 i → CompanionPartyPersistence.ReleaseActive(i) (ActiveRoster[i] = null)
///   - 대기 i → CompanionPartyPersistence.ReleaseWait(i)   (WaitlistPaths[i] = null)
///
/// YesorNo: 영입 다이얼로그의 동적 YES/NO 패턴 재활용 (listener add/remove + 코루틴 폴링).
///   - 던전 씬용이라 BattleManager가 아닌 이 컴포넌트가 직접 처리.
/// </summary>
public class ReleaseController : MonoBehaviour
{
    [Header("Release Buttons — Active (Companion_SLot2/3/4, index 0/1/2)")]
    [Tooltip("활성 슬롯 Release 버튼 3개. 순서 = ActiveRoster 인덱스 (Slot2=0, Slot3=1, Slot4=2).")]
    public Button[] activeReleaseButtons = new Button[3];

    [Header("Release Buttons — Reserve (Reserves1/2/3, index 0/1/2)")]
    [Tooltip("대기 슬롯 Release 버튼 3개. 순서 = WaitlistPaths 인덱스 (Reserves1=0, 2=1, 3=2).")]
    public Button[] reserveReleaseButtons = new Button[3];

    [Header("YesorNo 확인 패널")]
    [Tooltip("방출 확인 패널 GameObject. 평소 비활성, 방출 버튼 누르면 활성.")]
    public GameObject confirmPanel;
    [Tooltip("YES 버튼")] public Button yesButton;
    [Tooltip("NO 버튼")] public Button noButton;

    [Header("갱신 대상 뷰 (방출 후 Refresh)")]
    public CompanionSlotView companionSlotView;   // 활성 슬롯 표시
    public ReservesView reservesView;             // 대기 슬롯 표시

    private bool _bound = false;
    private int? _confirmChoice = null;   // null=대기, 0=NO, 1=YES
    private Coroutine _confirmRoutine;

    private void OnEnable()
    {
        BindReleaseButtons();
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void BindReleaseButtons()
    {
        if (_bound) return;

        if (activeReleaseButtons != null)
        {
            for (int i = 0; i < activeReleaseButtons.Length; i++)
            {
                if (activeReleaseButtons[i] == null) continue;
                int index = i; // 클로저 캡처
                activeReleaseButtons[i].onClick.AddListener(() => OnReleaseClicked(true, index));
                Debug.Log($"[Release] 활성 Release 버튼[{i}] 연결");
            }
        }
        if (reserveReleaseButtons != null)
        {
            for (int i = 0; i < reserveReleaseButtons.Length; i++)
            {
                if (reserveReleaseButtons[i] == null) continue;
                int index = i;
                reserveReleaseButtons[i].onClick.AddListener(() => OnReleaseClicked(false, index));
                Debug.Log($"[Release] 대기 Release 버튼[{i}] 연결");
            }
        }
        _bound = true;
    }

    /// <summary>Release 버튼 클릭 → YesorNo 확인 후 방출.</summary>
    /// <param name="isActive">true=활성 슬롯, false=대기 슬롯</param>
    /// <param name="slotIndex">0/1/2</param>
    private void OnReleaseClicked(bool isActive, int slotIndex)
    {
        Debug.Log($"[Release] Release 버튼 클릭 — {(isActive ? "활성" : "대기")} slot={slotIndex}");
        if (_confirmRoutine != null) StopCoroutine(_confirmRoutine);
        _confirmRoutine = StartCoroutine(ConfirmAndRelease(isActive, slotIndex));
    }

    private System.Collections.IEnumerator ConfirmAndRelease(bool isActive, int slotIndex)
    {
        // YesorNo 패널 표시 + 동적 listener (영입 다이얼로그 패턴)
        if (confirmPanel != null) confirmPanel.SetActive(true);

        _confirmChoice = null;
        UnityEngine.Events.UnityAction yesAction = () => { if (_confirmChoice == null) _confirmChoice = 1; };
        UnityEngine.Events.UnityAction noAction  = () => { if (_confirmChoice == null) _confirmChoice = 0; };
        if (yesButton != null) yesButton.onClick.AddListener(yesAction);
        if (noButton != null) noButton.onClick.AddListener(noAction);

        // 패널/버튼 미연결이면 대기 불가 → 안전하게 취소
        if (yesButton == null || noButton == null)
        {
            Debug.LogWarning("[Release] yesButton/noButton 미연결 — 확인 불가, 방출 취소");
            if (confirmPanel != null) confirmPanel.SetActive(false);
            yield break;
        }

        Debug.Log("[Release] YesorNo 패널 표시, 사용자 입력 대기");
        while (_confirmChoice == null) yield return null;

        if (yesButton != null) yesButton.onClick.RemoveListener(yesAction);
        if (noButton != null) noButton.onClick.RemoveListener(noAction);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (_confirmChoice == 1)
        {
            bool ok = isActive
                ? CompanionPartyPersistence.ReleaseActive(slotIndex)
                : CompanionPartyPersistence.ReleaseWait(slotIndex);
            Debug.Log($"[Release] YES → {(isActive ? "ReleaseActive" : "ReleaseWait")}({slotIndex}) 반환={ok}");

            // 화면 갱신 — 빈 슬롯으로 보이게
            if (companionSlotView != null) companionSlotView.Refresh();
            if (reservesView != null) reservesView.Refresh();
        }
        else
        {
            Debug.Log("[Release] NO → 방출 취소");
        }

        _confirmChoice = null;
    }

    private void OnDestroy()
    {
        // 동적 연결 정리
        if (activeReleaseButtons != null)
            foreach (var b in activeReleaseButtons) if (b != null) b.onClick.RemoveAllListeners();
        if (reserveReleaseButtons != null)
            foreach (var b in reserveReleaseButtons) if (b != null) b.onClick.RemoveAllListeners();
    }
}
