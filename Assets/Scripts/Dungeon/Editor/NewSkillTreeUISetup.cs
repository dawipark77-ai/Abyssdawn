using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// Tools/AbyssDawn/Setup New SkillTree UI
///
/// 실행하면:
///   1. 씬에서 LoreTree_Panel을 찾아 비활성화
///   2. 같은 부모 아래 NewSkillTree_Panel 생성 (전체 UI 계층)
///   3. LoreButtonController.loreTreePanel → 새 패널로 교체
///   4. 17개 트리 정의(스켈레톤) 자동 생성 — skills 배열은 수동 할당
/// </summary>
public static class NewSkillTreeUISetup
{
    private const string MenuPath = "Tools/AbyssDawn/Setup New SkillTree UI";

    // ── 레이아웃 상수 ────────────────────────────────────────────────
    private const float PanelW      = 1080f;
    private const float PanelH      = 1920f;
    private const float HeaderH     = 260f;   // category pager + tree pager
    private const float PopupH      = 560f;
    private const float CategoryH   = 120f;
    private const float TreePagerH  = 120f;
    private const float DividerH    = 2f;

    // ─────────────────────────────────────────────────────────────────

    [MenuItem(MenuPath)]
    private static void Run()
    {
        // ── 1. 기존 LoreTree_Panel 비활성화 ──────────────────────────
        GameObject oldPanel = GameObject.Find("LoreTree_Panel");
        Transform  panelParent = null;

        if (oldPanel != null)
        {
            panelParent = oldPanel.transform.parent;
            oldPanel.SetActive(false);
            Debug.Log("[NewSkillTreeUISetup] LoreTree_Panel 비활성화 완료");
        }
        else
        {
            Debug.LogWarning("[NewSkillTreeUISetup] LoreTree_Panel을 찾지 못했습니다. Canvas 최상위에 생성합니다.");
        }

        // 부모를 못 찾으면 씬의 첫 번째 Canvas를 사용
        if (panelParent == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            panelParent = canvas != null ? canvas.transform : null;
        }

        if (panelParent == null)
        {
            Debug.LogError("[NewSkillTreeUISetup] Canvas를 찾을 수 없습니다. 씬에 Canvas를 추가하세요.");
            return;
        }

        // 기존에 이미 만들어진 패널 제거
        Transform existing = panelParent.Find("NewSkillTree_Panel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        // ── 2. 루트 패널 ─────────────────────────────────────────────
        GameObject root = CreatePanel("NewSkillTree_Panel", panelParent, PanelW, PanelH);
        SetAnchorsStretch(root.GetComponent<RectTransform>());
        var rootImg = root.AddComponent<Image>();
        rootImg.color = new Color(0.07f, 0.07f, 0.10f, 0.97f);
        root.SetActive(false); // 기본 비활성화

        // NewSkillTreeUI 컴포넌트
        var uiCtrl = root.AddComponent<NewSkillTreeUI>();

        // ── 3. 헤더 영역 ─────────────────────────────────────────────
        // 3-a. 카테고리 페이저
        var catPager = CreateGroup("CategoryPager", root.transform,
                                   PanelW, CategoryH, Vector2.up, Vector2.up,
                                   new Vector2(0f, -20f));
        AddHorizontalLayout(catPager, 12f);

        var catPrev = CreateArrowButton("BtnCatPrev", catPager.transform, "◀");
        var catContainer = CreateLabel("CategoryName", catPager.transform,
                                       PanelW - 160f, CategoryH,
                                       "무기", 32f, Color.white);
        catContainer.alignment = TextAlignmentOptions.Center;
        var catNext = CreateArrowButton("BtnCatNext", catPager.transform, "▶");

        uiCtrl.categoryPrevBtn  = catPrev;
        uiCtrl.categoryNextBtn  = catNext;
        uiCtrl.categoryNameText = catContainer;

        // 3-b. 트리 페이저
        var treePager = CreateGroup("TreePager", root.transform,
                                    PanelW, TreePagerH, Vector2.up, Vector2.up,
                                    new Vector2(0f, -(20f + CategoryH + 10f)));
        AddHorizontalLayout(treePager, 12f);

        var treePrev = CreateArrowButton("BtnTreePrev", treePager.transform, "◀");

        // 트리 아이콘 + 이름 컨테이너
        var treeIconHolder = CreatePanel("TreeIconHolder", treePager.transform, 100f, 100f);
        var treeIconImg    = treeIconHolder.AddComponent<Image>();
        treeIconImg.preserveAspect = true;

        var treeNameTxt = CreateLabel("TreeName", treePager.transform,
                                      PanelW - 380f, TreePagerH,
                                      "Sword Lore", 28f, new Color(0.9f, 0.85f, 0.5f, 1f));
        treeNameTxt.alignment = TextAlignmentOptions.Left;

        var lpLabelTxt = CreateLabel("LPLabel", treePager.transform,
                                     140f, TreePagerH, "LP  0", 24f,
                                     new Color(0.6f, 0.9f, 1.0f, 1f));
        lpLabelTxt.alignment = TextAlignmentOptions.Right;

        var treeNext = CreateArrowButton("BtnTreeNext", treePager.transform, "▶");

        uiCtrl.treePrevBtn  = treePrev;
        uiCtrl.treeNextBtn  = treeNext;
        uiCtrl.treeIconImage = treeIconImg;
        uiCtrl.treeNameText  = treeNameTxt;
        uiCtrl.lpText        = lpLabelTxt;

        // 3-c. 구분선
        float divY = -(20f + CategoryH + 10f + TreePagerH + 8f);
        var divider = CreatePanel("Divider", root.transform, PanelW, DividerH);
        var divRT   = divider.GetComponent<RectTransform>();
        divRT.anchorMin = divRT.anchorMax = Vector2.up;
        divRT.pivot = new Vector2(0.5f, 1f);
        divRT.sizeDelta = new Vector2(PanelW, DividerH);
        divRT.anchoredPosition = new Vector2(0f, divY);
        divider.AddComponent<Image>().color = new Color(0.4f, 0.4f, 0.5f, 0.7f);

        // ── 4. ScrollRect ─────────────────────────────────────────────
        float scrollTop = 20f + CategoryH + 10f + TreePagerH + 8f + DividerH + 4f;
        float scrollH   = PanelH - scrollTop - PopupH - 10f;

        var scrollGO = CreatePanel("SkillScrollView", root.transform, PanelW, scrollH);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = scrollRT.anchorMax = Vector2.up;
        scrollRT.pivot     = new Vector2(0.5f, 1f);
        scrollRT.anchoredPosition = new Vector2(0f, -scrollTop);

        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0f);
        var scrollMask = scrollGO.AddComponent<Mask>();
        scrollMask.showMaskGraphic = false;

        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical   = true;
        sr.scrollSensitivity = 30f;

        // Viewport
        var viewportGO = CreatePanel("Viewport", scrollGO.transform, PanelW, scrollH);
        SetAnchorsStretch(viewportGO.GetComponent<RectTransform>());
        viewportGO.AddComponent<Image>().color = Color.clear;
        sr.viewport = viewportGO.GetComponent<RectTransform>();

        // Content
        var contentGO = CreatePanel("Content", viewportGO.transform, PanelW, 2000f);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
        sr.content = contentRT;

        uiCtrl.scrollRect    = sr;
        uiCtrl.scrollContent = contentRT;

        // ── 5. 닫기 버튼 ─────────────────────────────────────────────
        var closeBtn  = CreateTextButton("BtnClose", root.transform, 80f, 80f,
                                         "✕", 28f, new Color(0.8f, 0.3f, 0.3f, 1f));
        var closeBtnRT = closeBtn.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = closeBtnRT.anchorMax = Vector2.one;
        closeBtnRT.pivot     = Vector2.one;
        closeBtnRT.anchoredPosition = new Vector2(-12f, -12f);
        uiCtrl.closeButton = closeBtn.GetComponent<Button>();

        // ── 6. 스킬 디테일 팝업 ──────────────────────────────────────
        var popup = BuildDetailPopup(root.transform, PanelW, PopupH);
        uiCtrl.detailPopup = popup;

        // ── 7. 17개 트리 정의 ─────────────────────────────────────────
        uiCtrl.trees = BuildDefaultTrees();

        // ── 8. LoreButtonController → 새 패널 연결 ───────────────────
        var loreCtrl = Object.FindFirstObjectByType<LoreButtonController>();
        if (loreCtrl != null)
        {
            var so = new SerializedObject(loreCtrl);
            so.FindProperty("loreTreePanel").objectReferenceValue = root;
            so.ApplyModifiedProperties();
            Debug.Log("[NewSkillTreeUISetup] LoreButtonController.loreTreePanel → NewSkillTree_Panel 연결 완료");
        }
        else
        {
            Debug.LogWarning("[NewSkillTreeUISetup] LoreButtonController를 찾지 못했습니다. loreTreePanel은 수동 연결이 필요합니다.");
        }

        // ── 완료 ─────────────────────────────────────────────────────
        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;

        Debug.Log("[NewSkillTreeUISetup] ✅ NewSkillTree_Panel 생성 완료!\n" +
                  "→ NewSkillTreeUI.trees 배열에 각 트리의 SkillData를 인스펙터에서 할당하세요.");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Detail popup builder
    // ─────────────────────────────────────────────────────────────────

    private static NewSkillDetailPopup BuildDetailPopup(Transform parent, float w, float h)
    {
        // Dim background (full screen, behind panel)
        var dimGO  = CreatePanel("PopupDimBG", parent, PanelW, PanelH);
        SetAnchorsStretch(dimGO.GetComponent<RectTransform>());
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.4f);
        var dimBtn = dimGO.AddComponent<Button>();
        // No transition visual for dim bg
        var dimColors = dimBtn.colors;
        dimColors.normalColor   = new Color(0f, 0f, 0f, 0.4f);
        dimColors.highlightedColor = dimColors.normalColor;
        dimColors.pressedColor  = dimColors.normalColor;
        dimBtn.colors           = dimColors;

        // Popup root (anchored bottom)
        var popupRoot = CreatePanel("SkillDetailPopup", parent, w, h);
        var popupRT   = popupRoot.GetComponent<RectTransform>();
        popupRT.anchorMin = new Vector2(0f, 0f);
        popupRT.anchorMax = new Vector2(1f, 0f);
        popupRT.pivot     = new Vector2(0.5f, 0f);
        popupRT.offsetMin = popupRT.offsetMax = Vector2.zero;
        popupRT.sizeDelta = new Vector2(0f, h);
        popupRoot.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.98f);

        var popup        = popupRoot.AddComponent<NewSkillDetailPopup>();
        popup.panelRect  = popupRT;
        popup.dimBgButton= dimBtn;

        // ── Skill header row ────────────────────────────────────────
        float pad = 24f;
        float iconSize = 80f;

        var iconGO  = CreatePanel("SkillIcon", popupRoot.transform, iconSize, iconSize);
        var iconRT  = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = iconRT.anchorMax = Vector2.up;
        iconRT.pivot     = new Vector2(0f, 1f);
        iconRT.anchoredPosition = new Vector2(pad, -pad);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        popup.skillIconImage = iconImg;

        float nameX   = pad + iconSize + 16f;
        float nameW   = w - nameX - pad;
        var nameTxt   = CreateLabel("SkillName", popupRoot.transform,
                                    nameW, 38f, "Skill Name", 26f, Color.white);
        SetAnchoredTopLeft(nameTxt.GetComponent<RectTransform>(), nameX, pad);
        popup.skillNameText = nameTxt;

        var typeTxt = CreateLabel("SkillType", popupRoot.transform,
                                   nameW, 26f, "Physical / Active", 18f,
                                   new Color(0.7f, 0.7f, 0.7f, 1f));
        SetAnchoredTopLeft(typeTxt.GetComponent<RectTransform>(), nameX, pad + 42f);
        popup.skillTypeText = typeTxt;

        // ── Description ─────────────────────────────────────────────
        float descY = pad + iconSize + 16f;
        var descTxt = CreateLabel("Description", popupRoot.transform,
                                   w - pad * 2f, 90f, "스킬 설명", 20f,
                                   new Color(0.88f, 0.88f, 0.88f, 1f));
        SetAnchoredTopLeft(descTxt.GetComponent<RectTransform>(), pad, descY);
        descTxt.enableWordWrapping = true;
        popup.descriptionText = descTxt;

        // ── Stats row ────────────────────────────────────────────────
        float statsY = descY + 100f;
        float cellW  = (w - pad * 2f) / 4f;

        popup.damageText   = MakeStatCell("Damage",   popupRoot.transform, pad,             statsY, cellW, "배율 1.0x");
        popup.mpCostText   = MakeStatCell("MPCost",   popupRoot.transform, pad + cellW,     statsY, cellW, "MP 0");
        popup.lpCostText   = MakeStatCell("LPCost",   popupRoot.transform, pad + cellW * 2, statsY, cellW, "LP 1");
        popup.hitCountText = MakeStatCell("HitCount", popupRoot.transform, pad + cellW * 3, statsY, cellW, "단타");

        // ── Prerequisites row ────────────────────────────────────────
        float prereqY = statsY + 60f;
        var prereqRow = CreatePanel("PrerequisitesRow", popupRoot.transform,
                                    w - pad * 2f, 36f);
        SetAnchoredTopLeft(prereqRow.GetComponent<RectTransform>(), pad, prereqY);
        var prereqTxt = CreateLabel("PrereqText", prereqRow.transform,
                                    w - pad * 2f, 36f, "", 18f,
                                    new Color(0.7f, 0.7f, 0.5f, 1f));
        prereqTxt.alignment = TextAlignmentOptions.Left;
        SetAnchorsStretch(prereqTxt.GetComponent<RectTransform>());
        popup.prerequisitesText = prereqTxt;
        popup.prerequisitesRow  = prereqRow;

        // ── Buttons ─────────────────────────────────────────────────
        float btnY   = prereqY + 50f;
        float btnW   = (w - pad * 2f - 20f) / 2f;

        var learnBtn = CreateTextButton("LearnButton", popupRoot.transform,
                                        btnW, 68f, "습득", 26f,
                                        new Color(0.25f, 0.80f, 0.45f, 1f));
        SetAnchoredTopLeft(learnBtn.GetComponent<RectTransform>(), pad, btnY);
        popup.learnButton      = learnBtn.GetComponent<Button>();
        popup.learnButtonLabel = learnBtn.GetComponentInChildren<TextMeshProUGUI>();

        var closeBtn = CreateTextButton("CloseButton", popupRoot.transform,
                                         btnW, 68f, "닫기", 24f,
                                         new Color(0.50f, 0.50f, 0.55f, 1f));
        SetAnchoredTopLeft(closeBtn.GetComponent<RectTransform>(), pad + btnW + 20f, btnY);
        popup.closeButton = closeBtn.GetComponent<Button>();

        // Start hidden (no slide-in yet)
        dimGO.transform.SetParent(popupRoot.transform, false);
        dimGO.transform.SetAsFirstSibling();
        popupRoot.SetActive(false);

        return popup;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Default tree definitions (17개)
    // ─────────────────────────────────────────────────────────────────

    private static NewSkillTreeUI.SkillTreeDefinition[] BuildDefaultTrees()
    {
        var weapon = NewSkillTreeUI.SkillTreeCategory.Weapon;
        var util   = NewSkillTreeUI.SkillTreeCategory.Utility;
        var magic  = NewSkillTreeUI.SkillTreeCategory.Magic;

        return new[]
        {
            Def("Dagger Lore",      weapon),
            Def("Sword Lore",       weapon),
            Def("Greatsword Lore",  weapon),
            Def("Axe Lore",         weapon),
            Def("Hammer Lore",      weapon),
            Def("Spear Lore",       weapon),
            Def("Polearm Lore",     weapon),
            Def("Bow Lore",         weapon),
            Def("Crossbow Lore",    weapon),
            Def("Rod Lore",         weapon),
            Def("Staff Lore",       weapon),
            Def("Shield Lore",      weapon),
            Def("Dual Lore",        weapon),
            Def("Unarmed Lore",     weapon),
            Def("Warfare",          util),
            Def("Combat Arts",      util),
            Def("Divine Magic",     magic),
        };
    }

    private static NewSkillTreeUI.SkillTreeDefinition Def(string name,
        NewSkillTreeUI.SkillTreeCategory cat)
    {
        return new NewSkillTreeUI.SkillTreeDefinition
        {
            treeName = name,
            category = cat,
            skills   = new SkillData[0]
        };
    }

    // ─────────────────────────────────────────────────────────────────
    //  UI helpers
    // ─────────────────────────────────────────────────────────────────

    private static GameObject CreatePanel(string name, Transform parent, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    private static GameObject CreateGroup(string name, Transform parent,
        float w, float h, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos)
    {
        var go = CreatePanel(name, parent, w, h);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, h);
        rt.anchoredPosition = pos;
        return go;
    }

    private static void AddHorizontalLayout(GameObject go, float spacing)
    {
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing         = spacing;
        hlg.childAlignment  = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(16, 16, 0, 0);
    }

    private static Button CreateArrowButton(string name, Transform parent, string label)
    {
        var go   = CreateTextButton(name, parent, 70f, 70f, label, 32f,
                                    new Color(0.25f, 0.25f, 0.35f, 1f));
        return go.GetComponent<Button>();
    }

    private static GameObject CreateTextButton(string name, Transform parent,
        float w, float h, string text, float fontSize, Color bgColor)
    {
        var go  = CreatePanel(name, parent, w, h);
        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bgColor * 1.25f;
        colors.pressedColor     = bgColor * 0.75f;
        btn.colors = colors;

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(go.transform, false);
        SetAnchorsStretch(labelGO.GetComponent<RectTransform>());
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return go;
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent,
        float w, float h, string text, float fontSize, Color color)
    {
        var go  = CreatePanel(name, parent, w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static TextMeshProUGUI MakeStatCell(string name, Transform parent,
        float x, float y, float w, string defaultText)
    {
        var cell = CreatePanel(name + "Cell", parent, w - 4f, 52f);
        SetAnchoredTopLeft(cell.GetComponent<RectTransform>(), x, y);
        cell.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f, 0.8f);

        var tmp = CreateLabel(name + "Txt", cell.transform,
                              w - 8f, 52f, defaultText, 17f,
                              new Color(0.85f, 0.85f, 0.85f, 1f));
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static void SetAnchorsStretch(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredTopLeft(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = Vector2.up;
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
    }
}
