using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 새 스킬트리 UI 메인 컨트롤러.
///
/// 기존 SwordSkillTreeManager / SkillTreeNode / SkillData SO 구조는 일절 수정하지 않음.
/// PlayerStatData(learnedSkills, skillPoints)를 직접 읽고 쓰는 방식으로
/// 기존 SwordSkillTreeManager와 동일한 데이터 레이어를 공유한다.
/// </summary>
public class NewSkillTreeUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    //  Public types
    // ─────────────────────────────────────────────────────────────────

    public enum NodeState { Locked, Available, Learned }
    public enum SkillTreeCategory { Weapon, Utility, Magic }

    [Serializable]
    public class SkillTreeDefinition
    {
        public string treeName = "Unnamed";
        public Sprite treeIcon;
        public SkillTreeCategory category = SkillTreeCategory.Weapon;
        [Tooltip("이 트리에 속한 SkillData SO 배열")]
        public SkillData[] skills;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ─────────────────────────────────────────────────────────────────

    [Header("Data")]
    public PlayerStatData playerStatData;

    [Header("Tree Definitions (모든 트리 목록)")]
    public SkillTreeDefinition[] trees;

    [Header("Category Pager")]
    public Button categoryPrevBtn;
    public Button categoryNextBtn;
    public TextMeshProUGUI categoryNameText;
    public Image categoryIconImage;

    [Header("Tree Pager")]
    public Button treePrevBtn;
    public Button treeNextBtn;
    public TextMeshProUGUI treeNameText;
    public Image treeIconImage;

    [Header("LP Display")]
    public TextMeshProUGUI lpText;

    [Header("Scroll View")]
    public ScrollRect scrollRect;
    public RectTransform scrollContent;

    [Header("Close")]
    public Button closeButton;

    [Header("Detail Popup")]
    public NewSkillDetailPopup detailPopup;

    [Header("Visual")]
    public Color connectionLineColor = new Color(0.55f, 0.55f, 0.60f, 0.85f);

    // ─────────────────────────────────────────────────────────────────
    //  Layout constants
    // ─────────────────────────────────────────────────────────────────

    private const float NodeSize       = 82f;
    private const float NodeNameHeight = 26f;
    private const float NodeTotalH     = NodeSize + NodeNameHeight + 4f;
    private const float HGap           = 22f;
    private const float TierGap        = 72f;
    private const float TopPad         = 24f;
    private const float BotPad         = 48f;
    private const float LineThickness  = 2.5f;

    private static readonly string[] CategoryNames = { "무기", "유틸리티", "마법" };

    // ─────────────────────────────────────────────────────────────────
    //  Runtime state
    // ─────────────────────────────────────────────────────────────────

    private int _categoryIndex; // which of the 3 categories
    private int _treeIndexInCat; // index within current category's trees

    private Dictionary<SkillTreeCategory, List<int>> _catMap; // category → global tree indices
    private List<NewSkillTreeNode> _activeNodes = new();
    private Dictionary<string, int> _tierCache = new();

    // Swipe detection
    private Vector2 _swipeStart;
    private bool _swiping;
    private const float SwipeThreshold = 60f;

    // ─────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (playerStatData == null)
        {
            playerStatData = Resources.Load<PlayerStatData>("PlayerStatData")
                          ?? Resources.Load<PlayerStatData>("HeroData");
        }
    }

    private void Start()
    {
        BuildCategoryMap();
        WireButtons();
        ShowCategory(0);
    }

    private void OnEnable()
    {
        // Refresh LP and node states whenever the panel is opened
        if (_activeNodes.Count > 0)
        {
            RefreshNodeStates();
            UpdateLPText();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Category paging
    // ─────────────────────────────────────────────────────────────────

    private void BuildCategoryMap()
    {
        _catMap = new Dictionary<SkillTreeCategory, List<int>>
        {
            { SkillTreeCategory.Weapon,  new List<int>() },
            { SkillTreeCategory.Utility, new List<int>() },
            { SkillTreeCategory.Magic,   new List<int>() },
        };

        if (trees == null) return;

        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] != null)
                _catMap[trees[i].category].Add(i);
        }
    }

    private void WireButtons()
    {
        categoryPrevBtn?.onClick.AddListener(PrevCategory);
        categoryNextBtn?.onClick.AddListener(NextCategory);
        treePrevBtn?.onClick.AddListener(PrevTree);
        treeNextBtn?.onClick.AddListener(NextTree);
        closeButton?.onClick.AddListener(() => gameObject.SetActive(false));

        // Swipe on the scroll content area
        if (scrollRect != null)
        {
            var swipeDetector = scrollRect.viewport.gameObject
                .AddComponent<SwipeHandler>();
            swipeDetector.OnSwipeLeft  = NextTree;
            swipeDetector.OnSwipeRight = PrevTree;
        }
    }

    private void ShowCategory(int index)
    {
        var cats = (SkillTreeCategory[])Enum.GetValues(typeof(SkillTreeCategory));
        _categoryIndex = Mathf.Clamp(index, 0, cats.Length - 1);

        if (categoryNameText != null)
            categoryNameText.text = CategoryNames[_categoryIndex];

        // Reset to first tree in this category
        _treeIndexInCat = 0;
        ShowCurrentTree();
    }

    private void PrevCategory()
    {
        int total = 3;
        ShowCategory((_categoryIndex - 1 + total) % total);
    }

    private void NextCategory()
    {
        int total = 3;
        ShowCategory((_categoryIndex + 1) % total);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Tree paging
    // ─────────────────────────────────────────────────────────────────

    private List<int> CurrentCatIndices()
    {
        var cat = (SkillTreeCategory)_categoryIndex;
        return _catMap.TryGetValue(cat, out var list) ? list : new List<int>();
    }

    private void PrevTree()
    {
        var list = CurrentCatIndices();
        if (list.Count == 0) return;
        _treeIndexInCat = (_treeIndexInCat - 1 + list.Count) % list.Count;
        ShowCurrentTree();
    }

    private void NextTree()
    {
        var list = CurrentCatIndices();
        if (list.Count == 0) return;
        _treeIndexInCat = (_treeIndexInCat + 1) % list.Count;
        ShowCurrentTree();
    }

    private void ShowCurrentTree()
    {
        var list = CurrentCatIndices();
        if (list.Count == 0)
        {
            ClearContent();
            if (treeNameText != null) treeNameText.text = "(없음)";
            if (treeIconImage != null) treeIconImage.enabled = false;
            return;
        }

        _treeIndexInCat = Mathf.Clamp(_treeIndexInCat, 0, list.Count - 1);
        int globalIdx = list[_treeIndexInCat];
        SkillTreeDefinition def = trees[globalIdx];

        if (treeNameText != null)  treeNameText.text     = def.treeName;
        if (treeIconImage != null) { treeIconImage.sprite  = def.treeIcon;
                                     treeIconImage.enabled = def.treeIcon != null; }

        UpdateLPText();
        BuildTreeView(def);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Tree node layout
    // ─────────────────────────────────────────────────────────────────

    private void BuildTreeView(SkillTreeDefinition def)
    {
        ClearContent();

        if (def.skills == null || def.skills.Length == 0) return;

        _tierCache.Clear();

        // 1. Compute tier for each skill
        var tierMap = new Dictionary<string, int>();
        foreach (var s in def.skills)
            if (s != null) ComputeTier(s, tierMap);

        // 2. Group skills by tier
        var byTier = new SortedDictionary<int, List<SkillData>>();
        foreach (var s in def.skills)
        {
            if (s == null || !tierMap.ContainsKey(s.skillID)) continue;
            int t = tierMap[s.skillID];
            if (!byTier.ContainsKey(t)) byTier[t] = new List<SkillData>();
            byTier[t].Add(s);
        }

        // 3. Compute content size
        int numTiers = byTier.Count;
        float contentH = TopPad + numTiers * NodeTotalH + Mathf.Max(0, numTiers - 1) * TierGap + BotPad;
        float contentW = scrollContent.rect.width > 10f ? scrollContent.rect.width : 1080f;
        scrollContent.sizeDelta = new Vector2(contentW, contentH);

        // 4. Build position lookup
        var nodePosMap = new Dictionary<string, Vector2>();
        int tierRow = 0;
        foreach (var kv in byTier)
        {
            List<SkillData> row = kv.Value;
            float rowY = -(TopPad + tierRow * (NodeTotalH + TierGap) + NodeTotalH / 2f);

            float totalRowW = row.Count * NodeSize + (row.Count - 1) * HGap;
            float startX = -totalRowW / 2f + NodeSize / 2f;

            for (int i = 0; i < row.Count; i++)
            {
                float x = startX + i * (NodeSize + HGap);
                nodePosMap[row[i].skillID] = new Vector2(x, rowY);
            }
            tierRow++;
        }

        // 5. Draw connection lines (under nodes → add first)
        foreach (var s in def.skills)
        {
            if (s?.prerequisiteSkills == null) continue;
            foreach (var prereq in s.prerequisiteSkills)
            {
                if (prereq == null) continue;
                if (!nodePosMap.ContainsKey(s.skillID) || !nodePosMap.ContainsKey(prereq.skillID)) continue;

                Vector2 fromCenter = nodePosMap[prereq.skillID];
                Vector2 toCenter   = nodePosMap[s.skillID];

                // Connect bottom-center of parent to top-center of child
                Vector2 lineFrom = new Vector2(fromCenter.x, fromCenter.y - NodeTotalH / 2f);
                Vector2 lineTo   = new Vector2(toCenter.x,   toCenter.y   + NodeTotalH / 2f);

                DrawConnectionLine(lineFrom, lineTo);
            }
        }

        // 6. Spawn nodes (on top of lines)
        foreach (var s in def.skills)
        {
            if (s == null || !nodePosMap.ContainsKey(s.skillID)) continue;

            NewSkillTreeNode node = CreateNodeObject(s, scrollContent);
            node.GetComponent<RectTransform>().anchoredPosition = nodePosMap[s.skillID];
            node.SetState(GetNodeState(s));
            _activeNodes.Add(node);
        }

        // 7. Reset scroll position
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearContent()
    {
        _activeNodes.Clear();
        if (scrollContent == null) return;
        for (int i = scrollContent.childCount - 1; i >= 0; i--)
            Destroy(scrollContent.GetChild(i).gameObject);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Node factory (pure code, no prefab)
    // ─────────────────────────────────────────────────────────────────

    private NewSkillTreeNode CreateNodeObject(SkillData data, Transform parent)
    {
        // Root
        var root = new GameObject(data.skillName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(NodeSize, NodeTotalH);
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 1f);
        rootRT.pivot     = new Vector2(0.5f, 0.5f);

        // Button (top NodeSize×NodeSize area)
        var btnGO = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(root.transform, false);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0f, 1f - NodeSize / NodeTotalH);
        btnRT.anchorMax = Vector2.one;
        btnRT.offsetMin = btnRT.offsetMax = Vector2.zero;

        // Background
        var bgImg = btnGO.GetComponent<Image>();
        bgImg.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        // Border
        var borderGO  = MakeChildImage(btnGO.transform, "Border", NodeSize, NodeSize);
        var borderImg = borderGO.GetComponent<Image>();
        borderImg.color = new Color(0.92f, 0.80f, 0.30f, 1f);

        // Icon
        var iconGO  = MakeChildImage(btnGO.transform, "Icon", NodeSize - 24f, NodeSize - 24f);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;
        if (data.skillIcon != null) iconImg.sprite = data.skillIcon;

        // Learned badge (small green dot, top-right)
        var badgeGO  = MakeChildImage(btnGO.transform, "Badge", 20f, 20f);
        var badgeRT  = badgeGO.GetComponent<RectTransform>();
        badgeRT.anchorMin = badgeRT.anchorMax = Vector2.one;
        badgeRT.pivot     = Vector2.one;
        badgeRT.anchoredPosition = new Vector2(-2f, -2f);
        badgeGO.GetComponent<Image>().color = new Color(0.25f, 0.90f, 0.45f, 1f);
        badgeGO.SetActive(false);

        // Locked overlay (semi-transparent)
        var lockGO  = MakeChildImage(btnGO.transform, "Lock", NodeSize, NodeSize);
        lockGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        lockGO.SetActive(false);

        // Name label (bottom strip)
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(root.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = new Vector2(1f, 1f - NodeSize / NodeTotalH);
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = data.skillName;
        tmp.fontSize  = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        // Assemble NewSkillTreeNode component
        var node = root.AddComponent<NewSkillTreeNode>();
        node.bgImage      = bgImg;
        node.borderImage  = borderImg;
        node.iconImage    = iconImg;
        node.learnedBadge = badgeGO;
        node.lockedOverlay= lockGO;
        node.nameLabel    = tmp;
        node.button       = btnGO.GetComponent<Button>();
        node.Setup(data, OnNodeClicked);

        return node;
    }

    private static GameObject MakeChildImage(Transform parent, string name, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Connection lines
    // ─────────────────────────────────────────────────────────────────

    private void DrawConnectionLine(Vector2 from, Vector2 to)
    {
        var go  = new GameObject("Line", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(scrollContent, false);

        var img = go.GetComponent<Image>();
        img.color = connectionLineColor;

        float dist  = Vector2.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(dist, LineThickness);
        rt.anchoredPosition  = (from + to) * 0.5f;
        rt.localEulerAngles  = new Vector3(0f, 0f, angle);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Skill state / learning
    // ─────────────────────────────────────────────────────────────────

    public NodeState GetNodeState(SkillData skill)
    {
        if (IsLearned(skill)) return NodeState.Learned;
        if (PrerequisitesMet(skill)) return NodeState.Available;
        return NodeState.Locked;
    }

    private bool IsLearned(SkillData skill)
    {
        if (playerStatData?.learnedSkills == null) return false;
        foreach (var s in playerStatData.learnedSkills)
            if (s != null && s.skillID == skill.skillID) return true;
        return false;
    }

    private bool PrerequisitesMet(SkillData skill)
    {
        if (skill.prerequisiteSkills == null || skill.prerequisiteSkills.Count == 0)
            return true;
        foreach (var pre in skill.prerequisiteSkills)
            if (pre != null && !IsLearned(pre)) return false;
        return true;
    }

    private bool CanLearn(SkillData skill)
    {
        if (IsLearned(skill)) return false;
        if (!PrerequisitesMet(skill)) return false;
        int lp = playerStatData != null ? playerStatData.skillPoints : 0;
        return lp >= skill.requiredLorePoints;
    }

    private void OnNodeClicked(NewSkillTreeNode node)
    {
        if (detailPopup == null) return;
        SkillData data  = node.SkillData;
        NodeState state = GetNodeState(data);

        detailPopup.Show(data, state, () =>
        {
            if (CanLearn(data))
                LearnSkill(data);
        });
    }

    private void LearnSkill(SkillData skill)
    {
        if (playerStatData == null) return;

        // Deduct LP
        playerStatData.skillPoints -= skill.requiredLorePoints;

        // Add to learned list
        if (playerStatData.learnedSkills == null)
            playerStatData.learnedSkills = new List<SkillData>();
        if (!playerStatData.learnedSkills.Contains(skill))
            playerStatData.learnedSkills.Add(skill);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(playerStatData);
        UnityEditor.AssetDatabase.SaveAssets();
#endif

        RefreshNodeStates();
        UpdateLPText();

        Debug.Log($"[NewSkillTreeUI] ✅ {skill.skillName} 습득! 잔여 LP: {playerStatData.skillPoints}");
    }

    private void RefreshNodeStates()
    {
        foreach (var node in _activeNodes)
            if (node != null)
                node.SetState(GetNodeState(node.SkillData));
    }

    private void UpdateLPText()
    {
        if (lpText == null) return;
        int lp = playerStatData != null ? playerStatData.skillPoints : 0;
        lpText.text = $"LP  {lp}";
    }

    // ─────────────────────────────────────────────────────────────────
    //  Tier computation (memoized DFS)
    // ─────────────────────────────────────────────────────────────────

    private int ComputeTier(SkillData skill, Dictionary<string, int> cache)
    {
        if (cache.TryGetValue(skill.skillID, out int cached)) return cached;

        if (skill.prerequisiteSkills == null || skill.prerequisiteSkills.Count == 0)
        {
            cache[skill.skillID] = 0;
            return 0;
        }

        int maxPre = -1;
        foreach (var pre in skill.prerequisiteSkills)
            if (pre != null)
                maxPre = Mathf.Max(maxPre, ComputeTier(pre, cache));

        int tier = maxPre + 1;
        cache[skill.skillID] = tier;
        return tier;
    }
}

// ─────────────────────────────────────────────────────────────────────
//  Swipe helper (inner class as separate MB so it can be added to viewport)
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// 스크롤뷰 뷰포트에 붙여 좌우 스와이프를 감지한다.
/// </summary>
public class SwipeHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public Action OnSwipeLeft;
    public Action OnSwipeRight;

    private Vector2 _dragStart;

    public void OnBeginDrag(PointerEventData e) => _dragStart = e.position;

    public void OnEndDrag(PointerEventData e)
    {
        float delta = e.position.x - _dragStart.x;
        float absDelta = Mathf.Abs(delta);
        float absVert  = Mathf.Abs(e.position.y - _dragStart.y);

        // Only trigger if more horizontal than vertical, and exceeds threshold
        if (absDelta < 60f || absVert > absDelta) return;

        if (delta < 0) OnSwipeLeft?.Invoke();
        else           OnSwipeRight?.Invoke();
    }
}
