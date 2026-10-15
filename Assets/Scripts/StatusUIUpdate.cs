using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using AbyssdawnBattle;

public class StatusUIUpdate : MonoBehaviour
{
    [Header("Data")]
    public PlayerStatData playerStats;

    [Header("UI Roots")]
    public Transform statusPanel;

    [Header("Slots (Optional Manual Override)")]
    [SerializeField] private Image[] passiveSlots;
    [SerializeField] private Image[] activeSlots;

    void OnEnable()
    {
        if (statusPanel == null) statusPanel = transform;

        // 캐시 초기화: 기존 슬롯 리스트를 무조건 비우고 새로 찾기
        passiveSlots = new Image[3];
        activeSlots = new Image[6];

        CacheSlots();
        UpdateUI();
    }

    private void CacheSlots()
    {
        // 1) Skills 부모 찾기
        Transform[] allTransforms = statusPanel.GetComponentsInChildren<Transform>(true);
        Transform skillsRoot = null;
        foreach (Transform t in allTransforms)
        {
            if (t.name == "Skills")
            {
                skillsRoot = t;
                break;
            }
        }

        if (skillsRoot == null)
        {
            Debug.LogWarning("[StatusUIUpdate] 'Skills' 부모를 찾을 수 없습니다.");
            return;
        }

        // 2) Skills 내부에서 PassiveSkills/ActiveSkills 찾기
        Transform passiveContainer = null;
        Transform activeContainer = null;
        Transform[] skillsChildren = skillsRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in skillsChildren)
        {
            if (passiveContainer == null && t.name == "PassiveSkills")
            {
                passiveContainer = t;
            }
            if (activeContainer == null && t.name == "ActiveSkills")
            {
                activeContainer = t;
            }
            if (passiveContainer != null && activeContainer != null) break;
        }

        // Passive 슬롯들 찾기
        if (passiveContainer != null)
        {
            Debug.Log("[StatusUIUpdate] PassiveSkills 부모 찾음!");

            // 자식들 중에서 이름에 "Slot"을 포함하는 모든 오브젝트를 순서대로 가져오기
            List<Transform> slotTransforms = new List<Transform>();
            foreach (Transform child in passiveContainer)
            {
                if (child.name.Contains("Slot"))
                {
                    slotTransforms.Add(child);
                }
            }

            // 이름 순서대로 정렬 (Slot1, Slot2, Slot3...)
            slotTransforms.Sort((a, b) => string.Compare(a.name, b.name));

            for (int i = 0; i < Mathf.Min(passiveSlots.Length, slotTransforms.Count); i++)
            {
                Image img = slotTransforms[i].GetComponent<Image>();
                if (img == null)
                    img = slotTransforms[i].GetComponentInChildren<Image>(true);

                passiveSlots[i] = img;
                Debug.Log($"[StatusUIUpdate] Passive Slot{i + 1} 찾음: {slotTransforms[i].name}");
            }
        }
        else
        {
            Debug.LogWarning("[StatusUIUpdate] 'PassiveSkills' 컨테이너를 찾을 수 없습니다.");
        }

        // Active 슬롯들 찾기
        if (activeContainer != null)
        {
            Debug.Log("[StatusUIUpdate] ActiveSkills 부모 찾음!");

            // 자식들 중에서 이름에 "Slot"을 포함하는 모든 오브젝트를 순서대로 가져오기
            List<Transform> slotTransforms = new List<Transform>();
            foreach (Transform child in activeContainer)
            {
                if (child.name.Contains("Slot"))
                {
                    slotTransforms.Add(child);
                }
            }

            // 이름 순서대로 정렬 (Slot1, Slot2, Slot3...)
            slotTransforms.Sort((a, b) => string.Compare(a.name, b.name));

            for (int i = 0; i < Mathf.Min(activeSlots.Length, slotTransforms.Count); i++)
            {
                Image img = slotTransforms[i].GetComponent<Image>();
                if (img == null)
                    img = slotTransforms[i].GetComponentInChildren<Image>(true);

                activeSlots[i] = img;
                Debug.Log($"[StatusUIUpdate] Active Slot{i + 1} 찾음: {slotTransforms[i].name}");
            }
        }
        else
        {
            Debug.LogWarning("[StatusUIUpdate] 'ActiveSkills' 컨테이너를 찾을 수 없습니다.");
        }
    }

    private Image[] EnsureSlotArray(Image[] slots, int expectedCount)
    {
        if (slots == null || slots.Length != expectedCount)
        {
            return new Image[expectedCount];
        }
        return slots;
    }

    public void UpdateUI()
    {
        if (playerStats == null)
        {
            Debug.LogWarning("[StatusUIUpdate] playerStats is null.");
            ClearSlots(passiveSlots);
            ClearSlots(activeSlots);
            return;
        }
        try
        {
            ApplySkillIcons(passiveSlots, playerStats.equippedPassives);
            ApplySkillIcons(activeSlots, playerStats.equippedSkills);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[StatusUIUpdate] UpdateUI failed: {ex.Message}");
            ClearSlots(passiveSlots);
            ClearSlots(activeSlots);
        }
    }

    private void ApplySkillIcons(Image[] slots, List<SkillData> skills)
    {
        if (slots == null) return;
        int count = (skills != null) ? skills.Count : 0;

        for (int i = 0; i < slots.Length; i++)
        {
            Image slot = slots[i];
            if (slot == null) continue;

            SkillData skill = (i < count) ? skills[i] : null;
            Sprite icon = (skill != null) ? skill.skillIcon : null;
            SetSlotIcon(slot, icon);
            SetFallbackName(slot, skill != null && icon == null ? skill.skillName : null);
        }
    }

    private void SetSlotIcon(Image slot, Sprite icon)
    {
        slot.sprite = icon;
        Color c = slot.color;
        c.a = (icon != null) ? 1f : 0f;
        slot.color = c;

        // 아이콘을 넣은 후 명시적으로 활성화
        slot.gameObject.SetActive(true);
    }

    /// <summary>[2026-10-15] 아이콘이 없는 스킬은 칸이 비어 보이지 않게 이름 글자로 대신 보여 준다.</summary>
    private static void SetFallbackName(Image slot, string name)
    {
        var t = slot.transform.Find("FallbackName");
        if (string.IsNullOrEmpty(name)) { if (t != null) t.gameObject.SetActive(false); return; }
        TMPro.TextMeshProUGUI tmp;
        if (t == null)
        {
            var go = new GameObject("FallbackName", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(slot.transform, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(2, 2); rt.offsetMax = new Vector2(-2, -2);
            tmp = go.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 8; tmp.fontSizeMax = 18;
            tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.raycastTarget = false;
            tmp.color = new Color(0.83f, 0.65f, 0.38f);
            var other = slot.GetComponentInParent<Canvas>() != null ? slot.transform.root.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) : null;
            if (other != null) tmp.font = other.font;
        }
        else tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
        tmp.gameObject.SetActive(true);
        tmp.text = name;
    }

    private void ClearSlots(List<Image> slots)
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            SetSlotIcon(slot, null);
        }
    }

    private void ClearSlots(Image[] slots)
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            SetSlotIcon(slot, null);
        }
    }

}

