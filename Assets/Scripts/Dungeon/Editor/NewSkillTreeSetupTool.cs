#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// Tools/AbyssDawn/Setup New SkillTree UI
///
/// 씬에 NewSkillTree_Panel을 자동 생성한다.
///   - 기존 LoreTree_Panel은 비활성화 (삭제 X)
///   - LoreButtonController 는 수정하지 않음
///   - SwordSkillTreeManager.allNodes 의 SkillData 를 읽어
///     첫 번째 SkillTreeDefinition(단검 트리)에 자동 할당
/// </summary>
public static class NewSkillTreeSetupTool
{
    const float ScreenW  = 1080f;
    const float ScreenH  = 1920f;
    const float TopH     = 280f;   // Category + Tree pager 합산
    const float InfoBarH = 70f;
    const float DivH     = 3f;
    const float BtnSize  = 80f;
    const float ArrowW   = 72f;
    const float ArrowH   = 72f;

    // ── Menu entry ─────────────────────────────────────────────────

    [MenuItem("Tools/AbyssDawn/Setup New SkillTree UI")]
    static void Run()
    {
        // 씬에 Canvas 찾기
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "씬에 Canvas가 없습니다.\nCanvas를 먼저 생성해 주세요.", "OK");
            return;
        }

        // 기존 패널 처리
        var existing = canvas.transform.Find("NewSkillTree_Panel");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Confirm",
                "NewSkillTree_Panel이 이미 존재합니다.\n덮어씁니까?", "덮어쓰기", "취소"))
                return;
            Object.DestroyImmediate(existing.gameObject);
        }

        // 기존 LoreTree_Panel 비활성화
        var oldPanel = FindDeep(canvas.transform, "LoreTree_Panel");
        if (oldPanel != null)
        {
            oldPanel.gameObject.SetActive(false);
            Debug.Log("[NewSkillTreeSetupTool] LoreTree_Panel 비활성화 완료.");
        }

        // 패널 생성
        var panelGO = BuildFullPanel(canvas.transform);

        // SwordSkillTreeManager 의 skillData 를 첫 번째 트리에 자동 할당
        var mgr = Object.FindObjectOfType<SwordSkillTreeManager>();
        var ui  = panelGO.GetComponent<NewSkillTreeUI>();
        if (mgr != null && ui != null && mgr.allNodes != null)
            AutoPopulateFirstTree(ui, mgr);

        // PlayerStatData 자동 연결
        if (ui != null && ui.playerStatData == null)
        {
            var psd = Resources.Load<PlayerStatData>("PlayerStatData")
                   ?? Resources.Load<PlayerStatData>("HeroData");
            if (psd != null)
            {
                ui.playerStatData = psd;
                Debug.Log("[NewSkillTreeSetupTool] PlayerStatData 자동 연결 완료.");
            }
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Selection.activeGameObject = panelGO;
        Debug.Log("[NewSkillTreeSetupTool] ✅ NewSkillTree_Panel 생성 완료!");
        EditorUtility.DisplayDialog("완료",
            "NewSkillTree_Panel 생성이 완료됐습니다.\n\n" +
            "Inspector에서:\n" +
            "  • trees[] 배열에 각 트리 스킬 SO 할당\n" +
            "  • 트리 아이콘 스프라이트 지정\n\n" +
            "이후 Play 모드에서 바로 동작합니다.", "OK");
    }

    // ── Main builder ───────────────────────────────────────────────

    static GameObject BuildFullPanel(Transform canvasRoot)
    {
        // 루트 패널 (전체 화면)
        var panel = CreateRect("NewSkillTree_Panel", canvasRoot);
        StretchFull(panel);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.10f, 0.97f);

        float remainH = ScreenH - TopH - DivH - InfoBarH;

        // ── 1. Category Pager ──────────────────────────────────────
        var catRow = CreateRect("CategoryPager_Row", panel);
        SetAnchored(catRow, 0f, ScreenH - 130f, ScreenW, 110f);
        catRow.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 1f);

        var (catPrev, catNext, catIcon, catName) = BuildPager(catRow, "Category");

        // ── 2. Tree Pager ──────────────────────────────────────────
        var treeRow = CreateRect("TreePager_Row", panel);
        SetAnchored(treeRow, 0f, ScreenH - 130f - 140f, ScreenW, 130f);
        treeRow.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.14f, 1f);
        // 스와이프 감지용 이미지 (투명)
        var swipeImg = treeRow.gameObject.GetComponent<Image>();
        swipeImg.color = new Color(0f, 0f, 0f, 0.001f);
        swipeImg.raycastTarget = true;

        var (treePrev, treeNext, treeIcon, treeName) = BuildPager(treeRow, "Tree");

        // ── 3. 구분선 ──────────────────────────────────────────────
        var divider = CreateRect("Divider", panel);
        SetAnchored(divider, 0f, ScreenH - TopH - DivH, ScreenW, DivH);
        var divImg = divider.gameObject.AddComponent<Image>();
        divImg.color = new Color(0.4f, 0.4f, 0.6f, 0.6f);

        // ── 4. InfoBar ─────────────────────────────────────────────
        var infoBar = CreateRect("InfoBar", panel);
        SetAnchored(infoBar, 0f, ScreenH - TopH - DivH - InfoBarH, ScreenW, InfoBarH);
        infoBar.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.12f, 1f);

        var infoNameT = CreateTMP("InfoName_Text", infoBar);
        SetAnchored(infoNameT, 20f, 5f, ScreenW * 0.5f - 20f, InfoBarH - 10f);
        var infoTmp = infoNameT.gameObject.GetComponent<TextMeshProUGUI>();
        if (infoTmp == null) infoTmp = infoNameT.gameObject.AddComponent<TextMeshProUGUI>();
        infoTmp.text = "Sword Lore";  infoTmp.fontSize = 26; infoTmp.color = Color.white;
        infoTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var lpT = CreateTMP("LP_Text", infoBar);
        SetAnchored(lpT, ScreenW * 0.5f, 5f, ScreenW * 0.45f, InfoBarH - 10f);
        var lpTmp = lpT.gameObject.GetComponent<TextMeshProUGUI>();
        if (lpTmp == null) lpTmp = lpT.gameObject.AddComponent<TextMeshProUGUI>();
        lpTmp.text = "LP  0"; lpTmp.fontSize = 26; lpTmp.color = new Color(0.9f, 0.8f, 0.3f, 1f);
        lpTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var closeBtn = CreateButton("Close_Btn", infoBar,
            ScreenW - BtnSize - 8f, (InfoBarH - BtnSize) * 0.5f, BtnSize, BtnSize,
            new Color(0.5f, 0.2f, 0.2f, 1f), "✕", 28);

        // ── 5. Scroll View ─────────────────────────────────────────
        float scrollY   = 0f;
        float scrollH   = ScreenH - TopH - DivH - InfoBarH;
        var scrollGO = new GameObject("SkillTree_ScrollView");
        scrollGO.transform.SetParent(panel, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        SetAnchored(scrollRT, 0f, scrollY, ScreenW, scrollH);

        var scrollImg  = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0.3f);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;

        // Viewport
        var viewportGO = CreateRect("Viewport", scrollRT);
        StretchFull(viewportGO);
        var vpMask = viewportGO.gameObject.AddComponent<Image>();
        vpMask.color = Color.clear;
        var mask = viewportGO.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = viewportGO;

        // Content
        var contentGO = CreateRect("Content", viewportGO);
        var contentRT = contentGO;
        contentRT.anchorMin  = new Vector2(0f, 1f);
        contentRT.anchorMax  = new Vector2(1f, 1f);
        contentRT.pivot      = new Vector2(0.5f, 1f);
        contentRT.offsetMin  = Vector2.zero;
        contentRT.offsetMax  = Vector2.zero;
        contentRT.sizeDelta  = new Vector2(0f, 1200f); // ContentSizeFitter가 런타임에 조정
        scrollRect.content = contentRT;

        // ── 6. Detail Popup ────────────────────────────────────────
        var popupGO = BuildDetailPopup(panel);

        // ── NewSkillTreeUI 컴포넌트 ────────────────────────────────
        var uiComp = panel.gameObject.AddComponent<NewSkillTreeUI>();

        // Category pager refs
        uiComp.categoryPrevBtn  = catPrev;
        uiComp.categoryNextBtn  = catNext;
        uiComp.categoryIconImage = catIcon;
        uiComp.categoryNameText  = catName;

        // Tree pager refs
        uiComp.treePrevBtn      = treePrev;
        uiComp.treeNextBtn      = treeNext;
        uiComp.treeIconImage     = treeIcon;
        uiComp.treeNameText      = treeName;

        // Info bar refs
        uiComp.lpText            = lpTmp;

        // Scroll
        uiComp.scrollRect        = scrollRect;
        uiComp.scrollContent     = contentRT;

        // Close (optional — NewSkillTreeUI has no closeButton field, but popup does)

        // Popup
        var popupComp = popupGO.GetComponent<NewSkillDetailPopup>();
        uiComp.detailPopup = popupComp;

        // Default tree definitions
        uiComp.trees = BuildDefaultTreeDefs();

        // ── Close button for entire panel ─────────────────────────
        // NewSkillTreeUI 에는 closeButton 필드 있음
        uiComp.closeButton = closeBtn;

        return panel.gameObject;
    }

    // ── Pager row helper ──────────────────────────────────────────

    static (Button prev, Button next, Image icon, TextMeshProUGUI name)
        BuildPager(RectTransform parent, string prefix)
    {
        float rowH = parent.sizeDelta.y > 0 ? parent.sizeDelta.y : 110f;

        // Prev button
        var prevBtn = CreateButton(prefix + "Prev_Btn", parent,
            8f, (rowH - ArrowH) * 0.5f, ArrowW, ArrowH,
            new Color(0.2f, 0.2f, 0.3f, 1f), "◀", 28);

        // Next button
        var nextBtn = CreateButton(prefix + "Next_Btn", parent,
            ScreenW - ArrowW - 8f, (rowH - ArrowH) * 0.5f, ArrowW, ArrowH,
            new Color(0.2f, 0.2f, 0.3f, 1f), "▶", 28);

        // Center area
        float centerX = ArrowW + 16f;
        float centerW = ScreenW - (ArrowW + 16f) * 2f;

        // Icon
        var iconGO  = CreateRect(prefix + "Icon", parent);
        SetAnchored(iconGO, centerX + 8f, (rowH - 64f) * 0.5f, 64f, 64f);
        var iconImg = iconGO.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;

        // Name text
        var nameGO  = CreateRect(prefix + "Name_Text", parent);
        SetAnchored(nameGO, centerX + 80f, (rowH - 50f) * 0.5f, centerW - 88f, 50f);
        var nameTmp = nameGO.gameObject.AddComponent<TextMeshProUGUI>();
        nameTmp.text      = prefix == "Category" ? "무기" : "단검";
        nameTmp.fontSize  = prefix == "Category" ? 32 : 36;
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        nameTmp.color     = Color.white;

        return (prevBtn, nextBtn, iconImg, nameTmp);
    }

    // ── Detail Popup builder ──────────────────────────────────────

    static GameObject BuildDetailPopup(RectTransform parent)
    {
        // Full-screen overlay root
        var popRoot = CreateRect("DetailPopup", parent);
        StretchFull(popRoot);

        // Dark overlay (button to close)
        var dimGO = new GameObject("DimBG", typeof(RectTransform), typeof(Image), typeof(Button));
        dimGO.transform.SetParent(popRoot, false);
        var dimRT = dimGO.GetComponent<RectTransform>();
        StretchFull(dimRT);
        dimGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Card panel (bottom sheet, 900px tall)
        float cardH = 860f;
        var cardGO  = CreateRect("PopupCard", popRoot);
        var cardRT  = cardGO;
        cardRT.anchorMin = new Vector2(0f, 0f);
        cardRT.anchorMax = new Vector2(1f, 0f);
        cardRT.pivot     = new Vector2(0.5f, 0f);
        cardRT.offsetMin = Vector2.zero;
        cardRT.offsetMax = new Vector2(0f, cardH);
        var cardImg      = cardGO.gameObject.AddComponent<Image>();
        cardImg.color    = new Color(0.10f, 0.10f, 0.16f, 0.98f);

        float yOff = cardH - 20f; // top padding

        // Header: icon + name + type
        float iconSz = 90f;
        var iconImgRT = CreateRect("SkillIcon", cardGO);
        SetAnchored(iconImgRT, 20f, yOff - iconSz - 16f, iconSz, iconSz);
        var skillIconImg = iconImgRT.gameObject.AddComponent<Image>();
        skillIconImg.preserveAspect = true;

        var nameRT = CreateRect("SkillName_Text", cardGO);
        SetAnchored(nameRT, 20f + iconSz + 16f, yOff - 52f, ScreenW - iconSz - 80f, 50f);
        var nameTmp = nameRT.gameObject.AddComponent<TextMeshProUGUI>();
        nameTmp.text = "Skill Name"; nameTmp.fontSize = 38;
        nameTmp.color = Color.white;

        var typeRT = CreateRect("SkillType_Text", cardGO);
        SetAnchored(typeRT, 20f + iconSz + 16f, yOff - iconSz - 8f, ScreenW - iconSz - 80f, 34f);
        var typeTmp = typeRT.gameObject.AddComponent<TextMeshProUGUI>();
        typeTmp.text = "Active / Physical"; typeTmp.fontSize = 24;
        typeTmp.color = new Color(0.7f, 0.7f, 0.9f, 1f);

        yOff -= iconSz + 24f;

        // Separator
        var sep1 = CreateRect("Sep1", cardGO);
        SetAnchored(sep1, 16f, yOff - 3f, ScreenW - 32f, 2f);
        sep1.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.45f, 0.7f);
        yOff -= 12f;

        // Description
        var descRT = CreateRect("Description_Text", cardGO);
        SetAnchored(descRT, 20f, yOff - 120f, ScreenW - 40f, 120f);
        var descTmp = descRT.gameObject.AddComponent<TextMeshProUGUI>();
        descTmp.text = "Description goes here."; descTmp.fontSize = 26;
        descTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        descTmp.enableWordWrapping = true;
        yOff -= 130f;

        // Stats row (damage / mp / lp / hits)
        float statsH = 60f;
        float cellW  = (ScreenW - 40f) / 4f;
        var (dmgT, mpT, lpT, hitT) = BuildStatsRow(cardGO, 20f, yOff - statsH, cellW, statsH);
        yOff -= statsH + 10f;

        // Separator 2
        var sep2 = CreateRect("Sep2", cardGO);
        SetAnchored(sep2, 16f, yOff - 3f, ScreenW - 32f, 2f);
        sep2.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.45f, 0.5f);
        yOff -= 10f;

        // Prerequisites row
        var prereqRow = CreateRect("Prerequisites_Row", cardGO);
        SetAnchored(prereqRow, 16f, yOff - 50f, ScreenW - 32f, 50f);
        var prereqTmp = prereqRow.gameObject.AddComponent<TextMeshProUGUI>();
        prereqTmp.text = "필요: —"; prereqTmp.fontSize = 24;
        prereqTmp.color = new Color(0.7f, 0.7f, 0.5f, 1f);
        yOff -= 60f;

        // Learn + Close buttons
        float btnH  = 90f;
        float btnW  = (ScreenW - 60f) * 0.65f;
        float cBtnW = (ScreenW - 60f) * 0.30f;

        var learnBtn = CreateButton("Learn_Btn", cardGO,
            20f, yOff - btnH, btnW, btnH,
            new Color(0.20f, 0.65f, 0.35f, 1f), "습득", 34);

        var learnLabel = learnBtn.GetComponentInChildren<TextMeshProUGUI>();

        var closeBtn = CreateButton("Close_Btn", cardGO,
            20f + btnW + 20f, yOff - btnH, cBtnW, btnH,
            new Color(0.40f, 0.18f, 0.18f, 1f), "닫기", 30);

        // NewSkillDetailPopup 컴포넌트
        var popup         = popRoot.gameObject.AddComponent<NewSkillDetailPopup>();
        popup.panelRect   = cardRT;
        popup.dimBgButton = dimGO.GetComponent<Button>();
        popup.skillIconImage    = skillIconImg;
        popup.skillNameText     = nameTmp;
        popup.skillTypeText     = typeTmp;
        popup.descriptionText   = descTmp;
        popup.damageText        = dmgT;
        popup.mpCostText        = mpT;
        popup.lpCostText        = lpT;
        popup.hitCountText      = hitT;
        popup.prerequisitesText = prereqTmp;
        popup.prerequisitesRow  = prereqRow.gameObject;
        popup.learnButton       = learnBtn;
        popup.learnButtonLabel  = learnLabel;
        popup.closeButton       = closeBtn;

        popRoot.gameObject.SetActive(false);
        return popRoot.gameObject;
    }

    static (TextMeshProUGUI dmg, TextMeshProUGUI mp,
            TextMeshProUGUI lp,  TextMeshProUGUI hit)
        BuildStatsRow(RectTransform parent, float x, float y, float cellW, float h)
    {
        var (_, t0) = BuildStatCell(parent, "Damage",    x,               y, cellW, h, "배율\n—");
        var (_, t1) = BuildStatCell(parent, "MP",        x + cellW,       y, cellW, h, "MP\n0");
        var (_, t2) = BuildStatCell(parent, "LP",        x + cellW * 2f,  y, cellW, h, "LP\n0");
        var (_, t3) = BuildStatCell(parent, "Hits",      x + cellW * 3f,  y, cellW, h, "타\n1");
        return (t0, t1, t2, t3);
    }

    static (RectTransform rt, TextMeshProUGUI tmp)
        BuildStatCell(RectTransform parent, string label, float x, float y, float w, float h, string defText)
    {
        var cellRT = CreateRect("Stat_" + label, parent);
        SetAnchored(cellRT, x + 2f, y, w - 4f, h);
        var cellImg = cellRT.gameObject.AddComponent<Image>();
        cellImg.color = new Color(0.15f, 0.15f, 0.22f, 1f);

        var tmp = cellRT.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = defText;
        tmp.fontSize  = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.enableWordWrapping = false;
        return (cellRT, tmp);
    }

    // ── Default tree definitions ──────────────────────────────────

    static NewSkillTreeUI.SkillTreeDefinition[] BuildDefaultTreeDefs()
    {
        (string name, NewSkillTreeUI.SkillTreeCategory cat)[] defs =
        {
            // Weapon 14
            ("단검",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("도",        NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("대검",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("도끼",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("망치",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("창",        NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("폴암",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("활",        NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("쇠뇌",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("한손지팡이", NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("양손지팡이", NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("방패",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("듀얼",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            ("격투",      NewSkillTreeUI.SkillTreeCategory.Weapon),
            // Utility 2
            ("전쟁학",    NewSkillTreeUI.SkillTreeCategory.Utility),
            ("전투기술",  NewSkillTreeUI.SkillTreeCategory.Utility),
            // Magic 1
            ("신성마법",  NewSkillTreeUI.SkillTreeCategory.Magic),
        };

        var result = new NewSkillTreeUI.SkillTreeDefinition[defs.Length];
        for (int i = 0; i < defs.Length; i++)
            result[i] = new NewSkillTreeUI.SkillTreeDefinition
            {
                treeName = defs[i].name,
                category = defs[i].cat,
                skills   = new SkillData[0],
            };
        return result;
    }

    // ── SwordSkillTreeManager → first tree ───────────────────────

    static void AutoPopulateFirstTree(NewSkillTreeUI ui, SwordSkillTreeManager mgr)
    {
        if (ui.trees == null || ui.trees.Length == 0) return;

        var skillList = new List<SkillData>();
        foreach (var node in mgr.allNodes)
        {
            if (node != null && node.skillData != null)
                skillList.Add(node.skillData);
        }

        if (skillList.Count == 0) return;

        ui.trees[0].skills = skillList.ToArray();
        Debug.Log($"[NewSkillTreeSetupTool] 첫 번째 트리({ui.trees[0].treeName})에 " +
                  $"{skillList.Count}개 스킬 자동 할당 완료.");
    }

    // ── Low-level UI helpers ──────────────────────────────────────

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static RectTransform CreateRect(string name, RectTransform parent)
        => CreateRect(name, parent.transform);

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    /// <summary>anchoredPosition 기준 (좌하 기준 좌표계, bottom-left anchor)</summary>
    static void SetAnchored(RectTransform rt,
        float x, float y, float w, float h)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.zero;
        rt.pivot            = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    static Button CreateButton(string name, RectTransform parent,
        float x, float y, float w, float h, Color bgColor, string label, int fontSize)
    {
        var rt = CreateRect(name, parent);
        SetAnchored(rt, x, y, w, h);

        var img   = rt.gameObject.AddComponent<Image>();
        img.color = bgColor;

        var btn = rt.gameObject.AddComponent<Button>();

        var textGO  = CreateRect("Label", rt);
        StretchFull(textGO);
        var tmp         = textGO.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text        = label;
        tmp.fontSize    = fontSize;
        tmp.alignment   = TextAlignmentOptions.Center;
        tmp.color       = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    static RectTransform CreateTMP(string name, RectTransform parent)
    {
        var rt = CreateRect(name, parent);
        rt.gameObject.AddComponent<TextMeshProUGUI>();
        return rt;
    }

    // ── Deep child search ─────────────────────────────────────────

    static Transform FindDeep(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
