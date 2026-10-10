using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using AbyssdawnBattle;

public class StatusUIHandler : MonoBehaviour
{
    [Header("Data")]
    public PlayerStatData playerStatData;

    [Header("UI Roots")]
    public Transform statusPanel;
    public string passiveContainerName = "PassiveSkills";
    public string activeContainerName = "ActiveSkills";

    [Header("Slots (auto-filled if empty)")]
    public List<Image> passiveSlots = new List<Image>();
    public List<Image> activeSlots = new List<Image>();

    void Awake()
    {
        if (statusPanel == null) statusPanel = transform;
    }

    void OnEnable()
    {
        // 캐시 초기화: 기존 슬롯 리스트를 무조건 비우고 새로 찾기
        passiveSlots.Clear();
        activeSlots.Clear();

        AutoCacheSlotsIfNeeded();
        PlayerStats.OnStatusChanged += RefreshUI;
        RefreshUI();
    }

    void OnDisable()
    {
        PlayerStats.OnStatusChanged -= RefreshUI;
    }

    public void RefreshUI()
    {
        if (playerStatData == null)
        {
            Debug.LogWarning("[StatusUIHandler] playerStatData is null.");
            ClearSlots(passiveSlots);
            ClearSlots(activeSlots);
            return;
        }

        ApplySkillIcons(passiveSlots, playerStatData.equippedPassives);
        ApplySkillIcons(activeSlots, playerStatData.equippedSkills);
        RefreshPermanentSkills();
    }

    // ───────────── [2026-10-11] 상시 스킬 칸 (Active Skills 아래) ─────────────
    // 배운 상시 스킬(UsageType.Permanent / fieldSkill)을 작은 아이콘으로 배운 순서대로 늘어놓는다.
    // 등급 스킬은 아이콘 오른쪽 아래에 Lv. 누르면 상세 팝업. 칸은 코드로 만든다 (씬 편집 없이 모든 상태창에 적용).
    private const string PermanentContainerName = "PermanentSkills";
    private RectTransform _permGrid;
    private readonly List<GameObject> _permIcons = new List<GameObject>();

    private void RefreshPermanentSkills()
    {
        if (!EnsurePermanentSection()) return;

        foreach (var go in _permIcons) if (go != null) { go.SetActive(false); Destroy(go); } // Destroy 는 프레임 끝 — 그 전까지 겹쳐 보이지 않게
        _permIcons.Clear();

        var list = new List<SkillData>();
        if (playerStatData.learnedSkills != null)
            foreach (var s in playerStatData.learnedSkills)
                if (s != null && s.IsPermanent && !list.Contains(s)) list.Add(s);

        const float size = 66f, gap = 10f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((_permGrid.rect.width + gap) / (size + gap)));
        float rowW = Mathf.Min(list.Count, cols) * (size + gap) - gap;
        for (int i = 0; i < list.Count; i++)
        {
            SkillData skill = list[i];
            int row = i / cols, col = i % cols;
            var go = new GameObject("PermSkill_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_permGrid, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-rowW / 2f + size / 2f + col * (size + gap), -row * (size + gap));
            var img = go.GetComponent<Image>();
            img.sprite = skill.skillIcon;
            img.preserveAspect = true;
            img.color = skill.skillIcon != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
            SkillIconFrame.Apply(img, 2f, true);

            int rank = playerStatData.SkillRank(skill);
            if (skill.maxRank > 1)
            {
                var lv = new GameObject("Lv", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                lv.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)lv.transform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(2f, 1f); lrt.offsetMax = new Vector2(-3f, 0f);
                var t = lv.GetComponent<TMPro.TextMeshProUGUI>();
                t.text = "Lv" + rank;
                t.fontSize = 17f; t.fontStyle = TMPro.FontStyles.Bold;
                t.alignment = TMPro.TextAlignmentOptions.BottomRight;
                t.color = new Color(0.96f, 0.87f, 0.58f, 1f);
                t.outlineWidth = 0.25f; t.outlineColor = Color.black;
                t.raycastTarget = false;
            }

            SkillData captured = skill;
            DetailClickTarget.Attach(img, () => DetailClickTarget.ForSkill(captured));
            _permIcons.Add(go);
        }
    }

    /// <summary>Active Skills 제목·칸 아래에 "Permanent Skills" 제목과 아이콘 칸을 한 번 만든다.</summary>
    private bool EnsurePermanentSection()
    {
        if (_permGrid != null) return true;
        if (statusPanel == null) return false;
        Transform activeRoot = null;
        foreach (var t in statusPanel.GetComponentsInChildren<Transform>(true))
            if (t.name == activeContainerName) { activeRoot = t; break; }
        if (activeRoot == null || activeRoot.parent == null) return false;
        var parent = (RectTransform)activeRoot.parent;

        Transform existing = parent.Find(PermanentContainerName);
        RectTransform title;
        if (existing != null) title = (RectTransform)existing;
        else
        {
            // 제목은 Active Skills 제목을 그대로 복제 (같은 글꼴·크기) — 자식 슬롯은 지운다
            var activeLabel = (RectTransform)activeRoot;
            var go = new GameObject(PermanentContainerName, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            title = (RectTransform)go.transform;
            title.anchorMin = activeLabel.anchorMin; title.anchorMax = activeLabel.anchorMax; title.pivot = activeLabel.pivot;
            var src = activeLabel.GetComponent<TMPro.TMP_Text>();
            var dst = go.GetComponent<TMPro.TextMeshProUGUI>();
            dst.text = "Permanent Skills";
            if (src != null) { dst.font = src.font; dst.fontSharedMaterial = src.fontSharedMaterial; dst.fontSize = src.fontSize * 0.8f; dst.color = src.color; dst.alignment = src.alignment; dst.fontStyle = src.fontStyle; }
            dst.enableWordWrapping = false;
            dst.raycastTarget = false;

            // 액티브 칸 맨 아래 슬롯 밑에 둔다
            float bottom = activeLabel.anchoredPosition.y - activeLabel.sizeDelta.y / 2f;
            foreach (RectTransform c in activeRoot)
                bottom = Mathf.Min(bottom, activeLabel.anchoredPosition.y + c.anchoredPosition.y - c.sizeDelta.y / 2f);
            float h = activeLabel.sizeDelta.y * 0.8f;
            title.sizeDelta = new Vector2(Mathf.Max(activeLabel.sizeDelta.x, 330f), h);
            title.anchoredPosition = new Vector2(activeLabel.anchoredPosition.x, bottom - 18f - h / 2f);
        }

        var gridT = title.Find("Grid");
        if (gridT == null)
        {
            var g = new GameObject("Grid", typeof(RectTransform));
            g.transform.SetParent(title, false);
            gridT = g.transform;
        }
        _permGrid = (RectTransform)gridT;
        _permGrid.anchorMin = _permGrid.anchorMax = new Vector2(0.5f, 0f);
        _permGrid.pivot = new Vector2(0.5f, 1f);
        _permGrid.sizeDelta = new Vector2(Mathf.Max(title.sizeDelta.x, 330f), 140f);
        _permGrid.anchoredPosition = new Vector2(0f, -6f);
        return true;
    }

    private void ApplySkillIcons(List<Image> slots, List<SkillData> skills)
    {
        if (slots == null) return;
        int count = (skills != null) ? skills.Count : 0;

        for (int i = 0; i < slots.Count; i++)
        {
            Image slot = slots[i];
            if (slot == null) continue;

            SkillData skill = (i < count) ? skills[i] : null;
            Sprite icon = (skill != null) ? skill.skillIcon : null;
            SetSlotIcon(slot, icon);
            // [2026-10-11] 아이콘을 누르면 스킬 상세 팝업 (빈 칸이면 아무 일 없음)
            SkillData captured = skill;
            DetailClickTarget.Attach(slot, () => DetailClickTarget.ForSkill(captured));
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

    private void ClearSlots(List<Image> slots)
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            SetSlotIcon(slot, null);
        }
    }

    private void AutoCacheSlotsIfNeeded()
    {
        if (statusPanel == null) return;

        // GetComponentsInChildren로 모든 Transform 가져오기
        Transform[] allTransforms = statusPanel.GetComponentsInChildren<Transform>(true);

        Transform passiveRoot = System.Array.Find(allTransforms, t => t.name == passiveContainerName);
        if (passiveRoot != null)
        {
            Debug.Log($"[StatusUIHandler] {passiveContainerName} 부모 찾음!");
            passiveSlots = CollectSlotImages(passiveRoot);
            Debug.Log($"[StatusUIHandler] Passive slots found: {passiveSlots.Count}");
        }
        else
        {
            Debug.LogWarning($"[StatusUIHandler] '{passiveContainerName}' 컨테이너를 찾을 수 없습니다.");
        }

        Transform activeRoot = System.Array.Find(allTransforms, t => t.name == activeContainerName);
        if (activeRoot != null)
        {
            Debug.Log($"[StatusUIHandler] {activeContainerName} 부모 찾음!");
            activeSlots = CollectSlotImages(activeRoot);
            Debug.Log($"[StatusUIHandler] Active slots found: {activeSlots.Count}");
        }
        else
        {
            Debug.LogWarning($"[StatusUIHandler] '{activeContainerName}' 컨테이너를 찾을 수 없습니다.");
        }
    }

    private List<Image> CollectSlotImages(Transform root)
    {
        List<Image> images = new List<Image>();
        List<Transform> slotTransforms = new List<Transform>();

        // 자식들 중에서 이름에 "Slot"을 포함하는 모든 오브젝트를 가져오기
        foreach (Transform child in root)
        {
            if (child.name.Contains("Slot"))
            {
                slotTransforms.Add(child);
            }
        }

        // 이름 순서대로 정렬 (Slot1, Slot2, Slot3...)
        slotTransforms.Sort((a, b) => string.Compare(a.name, b.name));

        // Image 컴포넌트 찾기
        foreach (Transform slotTransform in slotTransforms)
        {
            Image img = slotTransform.GetComponent<Image>();
            if (img == null)
            {
                img = slotTransform.GetComponentInChildren<Image>(true);
            }
            if (img != null)
            {
                images.Add(img);
                Debug.Log($"[StatusUIHandler] Slot 찾음: {slotTransform.name}");
            }
        }

        return images;
    }

}





