using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StatusPanel의 슬롯 버튼(SlotButton1~4)으로 스테이터스 탭을 전환하는 컨트롤러.
/// 한 번에 하나의 슬롯 내용만 표시한다.
///
/// 슬롯 활성(SetActive) 제어는 이 컴포넌트가 단독으로 맡는다:
///   - DungeonStatusView는 slots[0].slotRoot=None이라 SetActive를 안 함 (텍스트만 갱신)
///   - CompanionSlotView는 텍스트/아이콘만 채움 (slotRoot SetActive 안 함)
///
/// 동작:
///   - Awake: 각 SlotButton.onClick을 코드로 동적 연결 → SelectSlot(index)
///   - OnEnable(패널 열림): 기본 탭(index 0 = Slot1/Hero)만 표시
///   - 버튼 클릭: 해당 content만 SetActive(true), 나머지 SetActive(false)
///   - 빈 슬롯(동료 없음)도 버튼 누르면 그대로 SetActive(true) — 동료 유무로 막지 않음
/// </summary>
public class StatusTabController : MonoBehaviour
{
    [System.Serializable]
    public class SlotTab
    {
        [Tooltip("이 슬롯의 버튼 (SlotButton1~4)")]
        public Button slotButton;

        [Tooltip("이 버튼이 보여줄 내용 GameObject (Character_Slot1 / Companion_SLot2~4)")]
        public GameObject content;
    }

    [Header("Slot Tabs (Element 0 = Slot1/Hero 기본 탭)")]
    [Tooltip("순서대로 Slot1(Hero), Slot2, Slot3, Slot4. Element 0이 기본 탭.")]
    public List<SlotTab> tabs = new List<SlotTab>();

    [Header("기본 탭")]
    [Tooltip("패널 열릴 때 기본으로 표시할 탭 인덱스 (0 = 첫 번째 = Slot1/Hero)")]
    public int defaultTabIndex = 0;

    private bool _listenersBound = false;

    private void Awake()
    {
        BindButtonListeners();
    }

    private void OnEnable()
    {
        // 패널 열릴 때마다 기본 탭으로 초기화 (Slot1/Hero)
        SelectSlot(defaultTabIndex);
    }

    /// <summary>각 SlotButton.onClick에 SelectSlot 핸들러를 코드로 연결.</summary>
    private void BindButtonListeners()
    {
        if (_listenersBound) return;

        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (tab == null || tab.slotButton == null)
            {
                Debug.LogWarning($"[StatusTabController] tabs[{i}]의 slotButton이 null입니다. 연결 스킵.");
                continue;
            }

            int index = i; // 클로저 캡처용 로컬 복사
            tab.slotButton.onClick.AddListener(() => SelectSlot(index));
            Debug.Log($"[StatusTabController] SlotButton[{i}] ('{tab.slotButton.name}') onClick 연결 완료");
        }

        _listenersBound = true;
    }

    /// <summary>
    /// 지정 인덱스 슬롯의 content만 표시하고 나머지는 숨긴다.
    /// 빈 슬롯(동료 없음)도 무조건 SetActive(true) — 동료 유무로 막지 않음.
    /// </summary>
    public void SelectSlot(int index)
    {
        Debug.Log($"[StatusTabController] SelectSlot({index}) — tabs.Count={tabs.Count}");

        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (tab == null || tab.content == null) continue;

            bool show = (i == index);
            if (tab.content.activeSelf != show)
            {
                tab.content.SetActive(show);
            }
        }
    }

    private void OnDestroy()
    {
        // 동적 연결한 listener 정리 (씬 전환 안전)
        foreach (var tab in tabs)
        {
            if (tab != null && tab.slotButton != null)
            {
                tab.slotButton.onClick.RemoveAllListeners();
            }
        }
    }
}
