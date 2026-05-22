using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DungeonStatusView : MonoBehaviour
{
    [System.Serializable]
    public class CharacterSlot
    {
        [Header("UI Elements")]
        public GameObject slotRoot;        // 슬롯 전체를 끄고 켤 때 사용 (없으면 비워도 됨)
        public TextMeshProUGUI nameText;   // 캐릭터 이름
        public TextMeshProUGUI hpText;     // Max HP 표시 (기존 hpText를 Max HP 용도로 사용)
        public TextMeshProUGUI currentHpText; // Current HP 표시 (새로 추가)
        public TextMeshProUGUI mpText;     // Max MP 표시 (기존 mpText를 Max MP 용도로 사용)
        public TextMeshProUGUI currentMpText; // Current MP 표시 (새로 추가)
        public TextMeshProUGUI levelText;  // 레벨 (예: "Lv. 1")
        public TextMeshProUGUI classText;  // 직업 (예: "Class: Warrior")
        public TextMeshProUGUI expText;    // 경험치 (예: "EXP: 0/100")
        
        [Header("Stats")]
        public TextMeshProUGUI strText;    // 공격력 (STR)
        public TextMeshProUGUI defText;    // 방어력 (DEF)
        public TextMeshProUGUI magText;    // 마법력 (MAG)
        public TextMeshProUGUI agiText;    // 민첩성 (AGI)
        public TextMeshProUGUI lukText;    // 행운 (LUK)
        
        [Header("Optional")]
        public Image portraitImage;        // 초상화 (있다면)

        // ─── [PlusButton 시스템] FreeStatPoints > 0일 때만 표시되는 + 버튼들 ───
        [Header("Plus Buttons (Free Stat Allocation)")]
        public Button hpPlusButton;
        public Button mpPlusButton;
        public Button strPlusButton;
        public Button defPlusButton;
        public Button magPlusButton;
        public Button agiPlusButton;
        public Button lukPlusButton;

        [Header("Stat Text Shift (Plus Button 표시 시 왼쪽으로)")]
        [Tooltip("PlusButton 활성 시 텍스트를 왼쪽으로 옮길 거리(px). 양수")]
        public float plusShiftDistance = 30f;
    }

    [Header("Slot Configuration")]
    public List<CharacterSlot> slots = new List<CharacterSlot>();

    // 각 텍스트의 원래 anchoredPosition을 저장 (PlusButton OFF 시 복귀용)
    // key: TextMeshProUGUI 인스턴스ID, value: 원래 anchoredPosition
    private readonly Dictionary<int, Vector2> _originalTextPositions = new Dictionary<int, Vector2>();

    void OnEnable()
    {
        // 패널이 켜질 때마다 데이터 갱신
        Debug.Log("[DungeonStatusView] ========== OnEnable 호출됨 ==========");

        // [NEW] 이벤트 구독
        PlayerStats.OnStatusChanged += UpdateUI;

        // [PlusButton] OnClick 바인딩 (씬 활성마다 안전하게 재등록)
        BindPlusButtons();

        // [FIX] 맵 씬 활성화 시 컴포넌트 상태 확인
        // [2026-05-07] currentHP/MP는 PlayerStatData에서 분리되어 PlayerStats가 직접 보유
        Debug.Log("[DungeonStatusView] 강제 데이터 갱신 시작...");
        PlayerStats playerInScene = FindFirstObjectByType<PlayerStats>();
        if (playerInScene != null)
        {
            string assetName = playerInScene.statData != null ? playerInScene.statData.name : "(no statData)";
            Debug.Log($"[DungeonStatusView] OnEnable 확인: statAsset={assetName}, HP={playerInScene.currentHP}, MP={playerInScene.currentMP}");
        }
        UpdateUI();
        Invoke(nameof(UpdateUI), 0.1f);  // 0.1초 후 다시 갱신 (초기화 완료 대기)
        Invoke(nameof(UpdateUI), 0.3f);  // 0.3초 후 한 번 더 갱신 (확실한 동기화)
    }

    void OnDisable()
    {
        // [NEW] 이벤트 구독 해제
        PlayerStats.OnStatusChanged -= UpdateUI;
    }

    private int _updateUiCallCount = 0;
    public void UpdateUI()
    {
        _updateUiCallCount++;
        Debug.Log($"[DungeonStatusView] ========== UpdateUI 호출됨 (#{_updateUiCallCount}, frame={Time.frameCount}) ==========");

        // [FIX] PlayerStats 찾기 - 씬에 있는 유효한 인스턴스 사용
        PlayerStats playerInScene = FindFirstObjectByType<PlayerStats>();

        if (playerInScene == null)
        {
            Debug.LogError("[DungeonStatusView] ✗ PlayerStats를 찾을 수 없습니다! 씬에 PlayerStats 컴포넌트가 있는지 확인하세요.");
            return;
        }

        // [DEBUG] PlayerStats 인스턴스 정보 (씬 전환 시 인스턴스가 바뀌는지 체크)
        Debug.Log($"[DungeonStatusView] ✓ PlayerStats 발견:");
        Debug.Log($"  └─ GameObject: {playerInScene.gameObject.name}");
        Debug.Log($"  └─ InstanceID: {playerInScene.GetInstanceID()}");
        Debug.Log($"  └─ Scene: {playerInScene.gameObject.scene.name}");

        // [FIX] statData(SO) 연결 확인
        if (playerInScene.statData == null)
        {
            Debug.LogError("[DungeonStatusView] ✗ statData(SO)가 연결되지 않았습니다!");
            return;
        }

        Debug.Log($"[DungeonStatusView] ✓ statData 연결됨: {playerInScene.statData.name}");

        // [FIX] 데이터 소스 고정: SO 에셋을 최우선으로 읽기
        int hp = playerInScene.currentHP;              // 프로퍼티 경유
        int maxHp = playerInScene.maxHP;                 // 계산된 값
        int mp = playerInScene.currentMP;              // 프로퍼티 경유
        int maxMp = playerInScene.maxMP;                 // 계산된 값

        Debug.Log($"[DungeonStatusView] SO에서 직접 읽은 데이터:");
        Debug.Log($"  └─ HP: {hp}/{maxHp}");
        Debug.Log($"  └─ MP: {mp}/{maxMp}");

        // SO가 단일 소스이므로 GameManager 저장은 생략

        // 슬롯이 비어있으면 리턴
        if (slots.Count == 0)
        {
            Debug.LogWarning("[DungeonStatusView] slots가 비어있습니다!");
            return;
        }

        // [FIX] 첫 번째 슬롯에 PlayerStats의 실시간 데이터 표시
        var slot = slots[0];

        // 슬롯 활성화
        if (slot.slotRoot != null) slot.slotRoot.SetActive(true);

        // 텍스트 갱신
        if (slot.nameText != null) slot.nameText.text = playerInScene.playerName;

        // HP 표시: 보정치 포함
        if (slot.hpText != null)
        {
            int hpBonus = playerInScene.GetHPBonus();

            if (hpBonus != 0)
            {
                slot.hpText.text = $"{maxHp}({hpBonus:+#;-#;0})";
            }
            else
            {
                slot.hpText.text = maxHp.ToString();
            }
        }

        // Current HP 표시 - 위에서 읽은 변수 사용
        if (slot.currentHpText != null)
        {
            slot.currentHpText.text = $"{hp}/{maxHp}";
            Debug.Log($"[DungeonStatusView] UI 업데이트: currentHpText = '{hp}/{maxHp}'");
        }
        else
        {
            Debug.LogWarning("[DungeonStatusView] currentHpText가 null입니다! Inspector에서 연결하세요.");
        }

        // MP 표시: 보정치 포함
        if (slot.mpText != null)
        {
            int mpBonus = playerInScene.GetMPBonus();

            if (mpBonus != 0)
            {
                slot.mpText.text = $"{maxMp}({mpBonus:+#;-#;0})";
            }
            else
            {
                slot.mpText.text = maxMp.ToString();
            }
        }

        // Current MP 표시 - 위에서 읽은 변수 사용
        if (slot.currentMpText != null)
        {
            slot.currentMpText.text = $"{mp}/{maxMp}";
            Debug.Log($"[DungeonStatusView] UI 업데이트: currentMpText = '{mp}/{maxMp}'");
        }
        else
        {
            Debug.LogWarning("[DungeonStatusView] currentMpText가 null입니다! Inspector에서 연결하세요.");
        }

        if (slot.levelText != null) slot.levelText.text = $"{playerInScene.level}";

        // Class 표시
        if (slot.classText != null)
        {
            string displayClass = string.IsNullOrEmpty(playerInScene.jobClass) ? "None" : playerInScene.jobClass;
            slot.classText.text = displayClass;
        }

        if (slot.expText != null) slot.expText.text = $"{playerInScene.exp} / {playerInScene.maxExp}";

        // 상세 스탯 - 보정치와 함께 표시
        if (slot.strText != null)
        {
            int bonus = playerInScene.GetAttackBonus();
            if (bonus != 0)
                slot.strText.text = $"{playerInScene.Attack}({bonus:+#;-#;0})";
            else
                slot.strText.text = $"{playerInScene.Attack}";
        }

        if (slot.defText != null)
        {
            int bonus = playerInScene.GetDefenseBonus();
            if (bonus != 0)
                slot.defText.text = $"{playerInScene.Defense}({bonus:+#;-#;0})";
            else
                slot.defText.text = $"{playerInScene.Defense}";
        }

        if (slot.magText != null)
        {
            int bonus = playerInScene.GetMagicBonus();
            if (bonus != 0)
                slot.magText.text = $"{playerInScene.Magic}({bonus:+#;-#;0})";
            else
                slot.magText.text = $"{playerInScene.Magic}";
        }

        if (slot.agiText != null)
        {
            int bonus = playerInScene.GetAgilityBonus();
            if (bonus != 0)
                slot.agiText.text = $"{playerInScene.Agility}({bonus:+#;-#;0})";
            else
                slot.agiText.text = $"{playerInScene.Agility}";
        }

        if (slot.lukText != null)
        {
            int bonus = playerInScene.GetLuckBonus();
            if (bonus != 0)
                slot.lukText.text = $"{playerInScene.Luck}({bonus:+#;-#;0})";
            else
                slot.lukText.text = $"{playerInScene.Luck}";
        }

        // 남은 슬롯은 비활성화
        for (int i = 1; i < slots.Count; i++)
        {
            if (slots[i].slotRoot != null) slots[i].slotRoot.SetActive(false);
        }

        // [PlusButton] 자유 분배 포인트에 따른 + 버튼 표시/숨김 + 텍스트 위치 이동
        Debug.Log("[DungeonStatusView] ▶ UpdateUI 끝 직전 — UpdatePlusButtons 호출 시도");
        UpdatePlusButtons(slot, playerInScene);
        Debug.Log("[DungeonStatusView] ◀ UpdatePlusButtons 반환됨");
    }

    // ─────────────────────────────────────────────────────────────────────
    // [PlusButton 시스템]
    // FreeStatPoints > 0 일 때만 각 스탯의 + 버튼을 켜고, 해당 텍스트를
    // plusShiftDistance 만큼 왼쪽으로 옮긴다(겹침 방지). 포인트가 0이면 원위치 복귀.
    // ─────────────────────────────────────────────────────────────────────

    private void BindPlusButtons()
    {
        Debug.Log("[DungeonStatusView] === BindPlusButtons 진입 ===");
        if (slots == null || slots.Count == 0)
        {
            Debug.LogError("[DungeonStatusView] ✗ BindPlusButtons: slots 비어있음. Inspector에 슬롯 추가 필요.");
            return;
        }
        var slot = slots[0];
        Debug.Log($"[DungeonStatusView] BindPlusButtons: slots[0]에 + 버튼 onClick 바인딩 시도. plusShiftDistance={slot.plusShiftDistance}");

        BindOne(slot.hpPlusButton,  () => SafeInvoke(p => p.AddHP()));
        BindOne(slot.mpPlusButton,  () => SafeInvoke(p => p.AddMP()));
        BindOne(slot.strPlusButton, () => SafeInvoke(p => p.AddSTR()));
        BindOne(slot.defPlusButton, () => SafeInvoke(p => p.AddDEF()));
        BindOne(slot.magPlusButton, () => SafeInvoke(p => p.AddMAG()));
        BindOne(slot.agiPlusButton, () => SafeInvoke(p => p.AddAGI()));
        BindOne(slot.lukPlusButton, () => SafeInvoke(p => p.AddLUK()));
    }

    private void BindOne(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null)
        {
            Debug.LogWarning("[DungeonStatusView] BindOne: btn == NULL → Inspector에 PlusButton 연결 안 됨. 바인딩 스킵.");
            return;
        }
        btn.onClick.RemoveListener(action); // 중복 등록 방지
        btn.onClick.AddListener(action);
        Debug.Log($"[DungeonStatusView] BindOne: '{btn.name}' onClick 바인딩 완료");
    }

    private void SafeInvoke(System.Action<PlayerStats> op)
    {
        var p = FindFirstObjectByType<PlayerStats>();
        if (p == null)
        {
            Debug.LogWarning("[DungeonStatusView] PlusButton 클릭 시 PlayerStats를 찾지 못했습니다.");
            return;
        }
        op(p);
        // PlayerStats 측에서 OnStatusChanged를 발동하므로 자동 갱신되지만,
        // 안전하게 한 번 더 호출.
        UpdateUI();
    }

    private void UpdatePlusButtons(CharacterSlot slot, PlayerStats player)
    {
        Debug.Log("[DungeonStatusView] === UpdatePlusButtons 진입 ===");

        if (slot == null)
        {
            Debug.LogError("[DungeonStatusView] ✗ UpdatePlusButtons: slot == null! 종료");
            return;
        }
        if (player == null)
        {
            Debug.LogError("[DungeonStatusView] ✗ UpdatePlusButtons: player == null! 종료");
            return;
        }

        int free = player.FreeStatPoints;
        bool show = free > 0;
        Debug.Log($"[DungeonStatusView] FreeStatPoints = {free} → show = {show}");

        // 각 PlusButton 참조 null 여부 체크
        Debug.Log($"[DungeonStatusView] PlusButton refs check:");
        Debug.Log($"  └─ hpPlusButton  : {(slot.hpPlusButton  == null ? "NULL" : slot.hpPlusButton.name)}");
        Debug.Log($"  └─ mpPlusButton  : {(slot.mpPlusButton  == null ? "NULL" : slot.mpPlusButton.name)}");
        Debug.Log($"  └─ strPlusButton : {(slot.strPlusButton == null ? "NULL" : slot.strPlusButton.name)}");
        Debug.Log($"  └─ defPlusButton : {(slot.defPlusButton == null ? "NULL" : slot.defPlusButton.name)}");
        Debug.Log($"  └─ magPlusButton : {(slot.magPlusButton == null ? "NULL" : slot.magPlusButton.name)}");
        Debug.Log($"  └─ agiPlusButton : {(slot.agiPlusButton == null ? "NULL" : slot.agiPlusButton.name)}");
        Debug.Log($"  └─ lukPlusButton : {(slot.lukPlusButton == null ? "NULL" : slot.lukPlusButton.name)}");

        TogglePlus("HP",  slot.hpPlusButton,  slot.hpText,  show, slot.plusShiftDistance);
        TogglePlus("MP",  slot.mpPlusButton,  slot.mpText,  show, slot.plusShiftDistance);
        TogglePlus("STR", slot.strPlusButton, slot.strText, show, slot.plusShiftDistance);
        TogglePlus("DEF", slot.defPlusButton, slot.defText, show, slot.plusShiftDistance);
        TogglePlus("MAG", slot.magPlusButton, slot.magText, show, slot.plusShiftDistance);
        TogglePlus("AGI", slot.agiPlusButton, slot.agiText, show, slot.plusShiftDistance);
        TogglePlus("LUK", slot.lukPlusButton, slot.lukText, show, slot.plusShiftDistance);

        Debug.Log("[DungeonStatusView] === UpdatePlusButtons 종료 ===");
    }

    private void TogglePlus(string label, Button btn, TMP_Text text, bool show, float shift)
    {
        if (btn == null)
        {
            Debug.LogWarning($"[DungeonStatusView] TogglePlus({label}): btn == NULL → Inspector에 PlusButton이 연결돼 있지 않음. 스킵.");
        }
        else
        {
            bool wasActive = btn.gameObject.activeSelf;
            if (wasActive != show) btn.gameObject.SetActive(show);
            Debug.Log($"[DungeonStatusView] TogglePlus({label}): btn='{btn.name}' wasActive={wasActive} → setActive={show} (실제 activeSelf={btn.gameObject.activeSelf})");

            // 부모 체인이 비활성이면 실제로 안 보일 수 있음
            if (show && !btn.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[DungeonStatusView] TogglePlus({label}): SetActive(true) 했지만 activeInHierarchy=false. 부모 GameObject가 꺼져 있을 가능성. 부모: '{(btn.transform.parent != null ? btn.transform.parent.name : "(root)")}'");
            }
        }

        if (text == null)
        {
            Debug.LogWarning($"[DungeonStatusView] TogglePlus({label}): text == NULL → 텍스트 위치 이동 스킵.");
            return;
        }
        RectTransform rt = text.rectTransform;
        int id = rt.GetInstanceID();

        // 최초 1회 원위치 저장
        if (!_originalTextPositions.ContainsKey(id))
        {
            _originalTextPositions[id] = rt.anchoredPosition;
            Debug.Log($"[DungeonStatusView] TogglePlus({label}): 원위치 캐시 저장 = {rt.anchoredPosition}");
        }

        Vector2 origin = _originalTextPositions[id];
        Vector2 target = show
            ? new Vector2(origin.x - Mathf.Abs(shift), origin.y)
            : origin;

        if (rt.anchoredPosition != target)
        {
            Debug.Log($"[DungeonStatusView] TogglePlus({label}): 텍스트 위치 이동 {rt.anchoredPosition} → {target} (shift={shift})");
            rt.anchoredPosition = target;
        }
    }
}
