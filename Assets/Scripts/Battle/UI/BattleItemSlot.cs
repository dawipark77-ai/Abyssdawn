using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 전투 중 ItemPanel 안에 표시되는 단일 아이템 슬롯.
/// 클릭 시 BattleManager.UseItemInBattle 호출 → 아이템 사용 + 패널 닫기.
/// 사용자 메시지 명세에 따른 OnUseButtonClicked 진입점 보유.
/// </summary>
public class BattleItemSlot : MonoBehaviour
{
    [Header("아이템 데이터")]
    [Tooltip("이 슬롯이 표시/사용할 ConsumableItemSO. Inspector에서 할당.")]
    public ConsumableItemSO item;

    [Header("UI 참조")]
    public Image iconImage;
    public TextMeshProUGUI chargeText;
    [Tooltip("Use 버튼. Inspector에서 할당하지 않으면 같은 GameObject 또는 자식에서 자동 검색.")]
    public Button useButton;

    [Header("연동 (자동 검색됨)")]
    public BattleManager battleManager;
    [Tooltip("사용 후 자동으로 닫을 부모 패널. 자동 GetComponentInParent로 검색.")]
    public BattleItemPanel parentPanel;

    private void Awake()
    {
        Debug.Log($"[BattleItemSlot] Awake 시작 — gameObject={gameObject.name}, item={(item != null ? item.itemName : "NULL")}", this);

        // 자동 참조 (인스펙터 미할당 시)
        if (battleManager == null)
            battleManager = Object.FindFirstObjectByType<BattleManager>();
        if (parentPanel == null)
            parentPanel = GetComponentInParent<BattleItemPanel>(true);
        if (useButton == null)
            useButton = GetComponentInChildren<Button>(true);

        Debug.Log($"[BattleItemSlot] Awake 자동 검색 결과 — battleManager={battleManager != null}, parentPanel={parentPanel != null}, useButton={useButton != null}", this);

        // 사용 버튼 listener 연결
        if (useButton != null)
        {
            useButton.onClick.RemoveAllListeners();
            useButton.onClick.AddListener(OnUseButtonClicked);
            Debug.Log($"[BattleItemSlot] Awake — useButton listener 등록 완료 (button GameObject: {useButton.gameObject.name}, interactable={useButton.interactable})", this);
        }
        else
        {
            Debug.LogWarning($"[BattleItemSlot] {gameObject.name}: useButton을 찾지 못했습니다.", this);
        }
    }

    private void OnEnable()
    {
        Debug.Log($"[BattleItemSlot] OnEnable 시작 — gameObject={gameObject.name}, ConsumableInventory.Instance={(ConsumableInventory.Instance != null)}", this);
        RefreshUI();
        if (ConsumableInventory.Instance != null)
            ConsumableInventory.Instance.OnInventoryChanged += RefreshUI;
    }

    private void OnDisable()
    {
        Debug.Log($"[BattleItemSlot] OnDisable — gameObject={gameObject.name}", this);
        if (ConsumableInventory.Instance != null)
            ConsumableInventory.Instance.OnInventoryChanged -= RefreshUI;
    }

    /// <summary>
    /// 아이콘과 충전량/수량 텍스트, 사용 가능 여부를 갱신.
    /// </summary>
    public void RefreshUI()
    {
        Debug.Log($"[BattleItemSlot] RefreshUI 시작 — item={(item != null ? item.itemName : "NULL")}, iconImage={iconImage != null}, chargeText={chargeText != null}, useButton={useButton != null}", this);

        if (item == null)
        {
            Debug.LogWarning($"[BattleItemSlot] RefreshUI — item이 null이라 슬롯 비활성화 처리", this);
            if (iconImage != null) iconImage.enabled = false;
            if (chargeText != null) chargeText.text = "";
            if (useButton != null) useButton.interactable = false;
            return;
        }

        // 아이콘 — 전투 슬롯은 플랫 아이콘 우선 (없으면 메인 아이콘)
        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = item.flatIcon != null ? item.flatIcon : item.itemIcon;
        }

        // 수량/충전 텍스트
        if (chargeText != null)
        {
            if (item.isDawnChalice && ConsumableInventory.Instance != null)
            {
                chargeText.text = $"{ConsumableInventory.Instance.dawnChaliceCharges}/{ConsumableInventory.Instance.dawnChaliceMaxCharges}";
            }
            else if (item.isChargeable)
            {
                chargeText.text = $"{item.currentCharges}/{item.maxCharges}";
            }
            else
            {
                int qty = ConsumableInventory.Instance != null ? ConsumableInventory.Instance.GetQuantity(item) : 0;
                chargeText.text = $"x{qty}";
            }
        }

        // 사용 가능 여부 — 보유 중 + 전투 사용 가능
        if (useButton != null)
        {
            bool hasItem = ConsumableInventory.Instance != null && ConsumableInventory.Instance.HasItem(item);
            useButton.interactable = hasItem && item.usableInBattle;
            Debug.Log($"[BattleItemSlot] RefreshUI — useButton.interactable={useButton.interactable} (hasItem={hasItem}, usableInBattle={item.usableInBattle})", this);
        }
    }

    /// <summary>
    /// 사용 버튼 클릭 핸들러.
    /// BattleManager.UseItemInBattle 호출 → ItemPanel 닫기.
    /// </summary>
    public void OnUseButtonClicked()
    {
        Debug.Log("[BattleItemSlot] ★★★ OnUseButtonClicked 호출됨! ★★★", this);

        if (item == null)
        {
            Debug.LogWarning("[BattleItemSlot] item이 null — 사용 불가", this);
            return;
        }

        if (battleManager == null)
        {
            Debug.LogError("[BattleItemSlot] battleManager 참조 없음 — 사용 불가", this);
            return;
        }

        // [GUARD 1] 이미 턴 진행 중이면 무시 — ResolutionQueue 도중 재진입 차단
        if (battleManager.IsTurnInProgress)
        {
            Debug.Log("[BattleItemSlot] 턴 진행 중 — 아이템 사용 불가 (연타 방지)", this);
            return;
        }

        // [GUARD 2] 버튼 즉시 비활성화 — 같은 프레임 내 중복 클릭 차단
        // (RefreshUI가 다음에 호출되면 interactable이 자동 재계산됨)
        if (useButton != null)
            useButton.interactable = false;

        // 사용자(주인공) — BattleManager.player 사용
        PlayerStats user = battleManager.player;
        if (user == null)
        {
            Debug.LogError("[BattleItemSlot] BattleManager.player가 null — 사용 불가", this);
            return;
        }

        // 보유 / 사용 가능 체크
        if (ConsumableInventory.Instance == null || !ConsumableInventory.Instance.HasItem(item))
        {
            Debug.LogWarning($"[BattleItemSlot] {item.itemName} 보유 0 — 사용 불가", this);
            return;
        }

        // 회복량 계산 (hpRecoveryPercent × maxHP)
        int healAmount = Mathf.RoundToInt(item.hpRecoveryPercent * user.maxHP);

        Debug.Log($"[BattleItemSlot] {item.itemName} 사용 → user={user.playerName}, heal={healAmount}");

        // 실제 사용 위임
        battleManager.UseItemInBattle(user, item, healAmount);

        // ItemPanel 닫기
        if (parentPanel != null)
            parentPanel.Close();
    }
}
