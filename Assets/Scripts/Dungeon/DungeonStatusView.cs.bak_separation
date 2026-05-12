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
    }

    [Header("Slot Configuration")]
    public List<CharacterSlot> slots = new List<CharacterSlot>();

    void OnEnable()
    {
        // 패널이 켜질 때마다 데이터 갱신
        Debug.Log("[DungeonStatusView] ========== OnEnable 호출됨 ==========");

        // [NEW] 이벤트 구독
        PlayerStats.OnStatusChanged += UpdateUI;

        // [FIX] 맵 씬 활성화 시 SO 에셋 값을 가장 먼저 확인
        Debug.Log("[DungeonStatusView] 강제 데이터 갱신 시작...");
        PlayerStats playerInScene = FindFirstObjectByType<PlayerStats>();
        if (playerInScene != null && playerInScene.statData != null)
        {
            Debug.Log($"[DungeonStatusView] OnEnable statData 확인: {playerInScene.statData.name} (HP={playerInScene.statData.currentHP}, MP={playerInScene.statData.currentMP})");
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

    public void UpdateUI()
    {
        Debug.Log("[DungeonStatusView] ========== UpdateUI 호출됨 ==========");

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
    }
}
