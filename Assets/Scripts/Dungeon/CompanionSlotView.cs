using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Abyssdawn;            // CompanionSO
using AbyssdawnBattle;      // SkillData
// CompanionPartyPersistence, PassiveData 는 글로벌 namespace

/// <summary>
/// 던전 화면의 동료 스테이터스 슬롯(Slot2/3/4)을 CompanionPartyPersistence.ActiveRoster
/// 기반으로 채우는 컴포넌트. 부모 1개에 부착하고 slots 리스트로 3개 슬롯을 관리한다.
/// (DungeonStatusView의 slots 패턴과 동일)
///
/// 데이터 소스:
///   - currentHP/MP : ActiveRoster[i].currentHP / currentMP (전투 중 변동치)
///   - maxHP/MP, STR(=ATK)/DEF/MAG/AGI/LUK, 스킬 : LoadCompanion(resourcePath)로 얻은 CompanionSO
///
/// 갱신: OnEnable 자가 갱신(동료 데이터는 던전 진입 시점 고정이라 1회로 충분).
/// </summary>
public class CompanionSlotView : MonoBehaviour
{
    [System.Serializable]
    public class CompanionSlot
    {
        [Header("Root")]
        [Tooltip("이 슬롯 전체 GameObject. 동료가 없으면 SetActive(false)로 숨김.")]
        public GameObject slotRoot;

        [Header("BasicPanel")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI expText;   // 동료는 레벨업 없음 → "—"
        public TextMeshProUGUI hpText;    // currentHP / maxHP
        public TextMeshProUGUI mpText;    // currentMP / maxMP

        [Header("StatusPanel (STR = CompanionSO.ATK)")]
        public TextMeshProUGUI statHpText;
        public TextMeshProUGUI statMpText;
        public TextMeshProUGUI strText;
        public TextMeshProUGUI defText;
        public TextMeshProUGUI magText;
        public TextMeshProUGUI agiText;
        public TextMeshProUGUI lukText;

        [Header("BattleSkills")]
        // PassiveSkills를 ActiveSkills보다 먼저 선언 → Inspector에서 위쪽에 표시.
        [Tooltip("PassiveSkills 아이콘 3칸 (PassiveSkills Slot1~3). 채워지지 않는 칸은 SetActive(false).")]
        public Image[] passiveSkillIcons = new Image[3];   // 3칸

        [Tooltip("ActiveSkills 아이콘 6칸 (ActiveSkills Slot1~6). 채워지지 않는 칸은 SetActive(false).")]
        public Image[] activeSkillIcons = new Image[6];    // 6칸
    }

    [Header("Slots (Slot2=0, Slot3=1, Slot4=2 — ActiveRoster 인덱스와 1:1)")]
    public List<CompanionSlot> slots = new List<CompanionSlot>();

    [Header("표시 설정")]
    [Tooltip("레벨업 없는 동료의 EXP 칸에 표시할 문자열")]
    public string expPlaceholder = "—";

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>ActiveRoster를 읽어 각 슬롯을 갱신. 외부에서도 호출 가능.</summary>
    public void Refresh()
    {
        var roster = CompanionPartyPersistence.ActiveRoster;
        Debug.Log($"[CompanionSlotView] Refresh — slots={slots.Count}, ActiveRoster.Count={(roster != null ? roster.Count : 0)}");

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            // 해당 인덱스에 동료가 없으면 슬롯 숨김
            if (roster == null || i >= roster.Count || roster[i] == null)
            {
                if (slot.slotRoot != null) slot.slotRoot.SetActive(false);
                continue;
            }

            var entry = roster[i];
            CompanionSO so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
            if (so == null)
            {
                Debug.LogWarning($"[CompanionSlotView] 슬롯 {i}: LoadCompanion 실패 '{entry.resourcePath}' → 슬롯 숨김");
                if (slot.slotRoot != null) slot.slotRoot.SetActive(false);
                continue;
            }

            if (slot.slotRoot != null) slot.slotRoot.SetActive(true);
            FillSlot(slot, entry, so);
        }
    }

    private void FillSlot(CompanionSlot slot, CompanionPartyPersistence.Entry entry, CompanionSO so)
    {
        // ── BasicPanel ──
        if (slot.nameText != null) slot.nameText.text = so.CompanionName;
        if (slot.expText != null)  slot.expText.text  = expPlaceholder;   // 동료는 레벨업 없음
        if (slot.hpText != null)   slot.hpText.text   = $"{entry.currentHP} / {so.HP}";
        if (slot.mpText != null)   slot.mpText.text   = $"{entry.currentMP} / {so.MP}";

        // ── StatusPanel (STR = ATK) ──
        if (slot.statHpText != null) slot.statHpText.text = $"{entry.currentHP} / {so.HP}";
        if (slot.statMpText != null) slot.statMpText.text = $"{entry.currentMP} / {so.MP}";
        if (slot.strText != null)    slot.strText.text    = so.ATK.ToString();
        if (slot.defText != null)    slot.defText.text    = so.DEF.ToString();
        if (slot.magText != null)    slot.magText.text    = so.MAG.ToString();
        if (slot.agiText != null)    slot.agiText.text    = so.AGI.ToString();
        if (slot.lukText != null)    slot.lukText.text    = so.LUK.ToString();

        // ── BattleSkills: ActiveSkills (SkillData.skillIcon) ──
        FillSkillIcons(slot.activeSkillIcons, so.ActiveSkills);

        // ── BattleSkills: PassiveSkills (PassiveData.passiveIcon) ──
        FillPassiveIcons(slot.passiveSkillIcons, so.PassiveSkills);
    }

    private void FillSkillIcons(Image[] icons, IReadOnlyList<SkillData> skills)
    {
        if (icons == null) return;
        int count = (skills != null) ? skills.Count : 0;

        for (int i = 0; i < icons.Length; i++)
        {
            Image img = icons[i];
            if (img == null) continue;

            if (i < count && skills[i] != null && skills[i].skillIcon != null)
            {
                img.sprite = skills[i].skillIcon;
                img.gameObject.SetActive(true);
            }
            else
            {
                img.gameObject.SetActive(false); // 빈 칸 숨김
            }
        }
    }

    private void FillPassiveIcons(Image[] icons, IReadOnlyList<PassiveData> passives)
    {
        if (icons == null) return;
        int count = (passives != null) ? passives.Count : 0;

        for (int i = 0; i < icons.Length; i++)
        {
            Image img = icons[i];
            if (img == null) continue;

            if (i < count && passives[i] != null && passives[i].passiveIcon != null)
            {
                img.sprite = passives[i].passiveIcon;
                img.gameObject.SetActive(true);
            }
            else
            {
                img.gameObject.SetActive(false); // 빈 칸 숨김
            }
        }
    }
}
