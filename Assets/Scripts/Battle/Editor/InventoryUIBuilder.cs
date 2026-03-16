#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Tools > Abyssdawn > Create Inventory UI
/// 전체화면 인벤토리 오버레이를 현재 씬의 Canvas 위에 생성합니다.
/// </summary>
public static class InventoryUIBuilder
{
    // ─── 팔레트 ──────────────────────────────────────────────
    private static readonly Color C_Overlay        = new Color(0.00f, 0.00f, 0.00f, 0.88f);
    private static readonly Color C_PanelBg        = new Color(0.09f, 0.09f, 0.14f, 0.97f);
    private static readonly Color C_HeaderBg       = new Color(0.06f, 0.06f, 0.10f, 1.00f);
    private static readonly Color C_TabActive      = new Color(0.56f, 0.42f, 0.12f, 1.00f);
    private static readonly Color C_TabInactive    = new Color(0.18f, 0.18f, 0.25f, 1.00f);
    private static readonly Color C_SlotBg         = new Color(0.20f, 0.20f, 0.28f, 1.00f);
    private static readonly Color C_SlotBorder     = new Color(0.25f, 0.25f, 0.32f, 0.60f);
    private static readonly Color C_DetailBg       = new Color(0.08f, 0.08f, 0.13f, 0.98f);
    private static readonly Color C_HandleBar      = new Color(0.35f, 0.35f, 0.42f, 1.00f);
    private static readonly Color C_BtnPrimary     = new Color(0.56f, 0.42f, 0.12f, 1.00f);
    private static readonly Color C_BtnDiscard     = new Color(0.50f, 0.10f, 0.10f, 1.00f);
    private static readonly Color C_TextWhite      = Color.white;
    private static readonly Color C_TextDim        = new Color(0.65f, 0.65f, 0.70f, 1.00f);
    private static readonly Color C_Divider        = new Color(0.30f, 0.30f, 0.38f, 0.50f);

    private const float DETAIL_HEIGHT   = 400f;
    private const float HEADER_HEIGHT   = 56f;
    private const float TAB_HEIGHT      = 44f;
    private const float HANDLE_HEIGHT   = 6f;
    private const float CLOSE_BTN_H     = 52f;
    private const float ACTION_ROW_H    = 56f;

    // ═════════════════════════════════════════════════════════
    //  메뉴 진입점
    // ═════════════════════════════════════════════════════════

    [MenuItem("Tools/Abyssdawn/Create Inventory UI", priority = 200)]
    public static void Build()
    {
        Canvas canvas = FindOrCreateCanvas();
        Transform canvasT = canvas.transform;

        if (canvasT.Find("InventoryOverlay") != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "인벤토리 UI",
                "이미 InventoryOverlay가 존재합니다. 재생성하시겠습니까?",
                "재생성", "취소");

            if (!replace) return;
            Object.DestroyImmediate(canvasT.Find("InventoryOverlay").gameObject);
        }

        GameObject root = BuildInventoryUI(canvasT);
        Selection.activeGameObject = root;
        Undo.RegisterCreatedObjectUndo(root, "Create Inventory UI");
        Debug.Log("[InventoryUIBuilder] 인벤토리 UI 생성 완료 → " + root.name);
    }

    // ═════════════════════════════════════════════════════════
    //  전체 계층 조립
    // ═════════════════════════════════════════════════════════

    private static GameObject BuildInventoryUI(Transform canvasT)
    {
        // ── 루트 오버레이 ────────────────────────────────────
        var overlay = MakeImage("InventoryOverlay", canvasT, C_Overlay);
        Stretch(overlay);
        overlay.SetActive(false);

        var manager = overlay.AddComponent<InventoryUIManager>();
        manager.inventoryRoot = overlay;

        // ── 헤더 ─────────────────────────────────────────────
        var header = BuildHeader(overlay.transform, manager);

        // ── 탭바 ─────────────────────────────────────────────
        BuildTabBar(overlay.transform, manager);

        // ── 그리드 스크롤 영역 ────────────────────────────────
        BuildGridArea(overlay.transform, manager);

        // ── 하단 상세 패널 ────────────────────────────────────
        BuildDetailPanel(overlay.transform, manager);

        // ── 하단 닫기 버튼 ────────────────────────────────────
        BuildCloseButton(overlay.transform, manager);

        return overlay;
    }

    // ═════════════════════════════════════════════════════════
    //  헤더 (타이틀)
    // ═════════════════════════════════════════════════════════

    private static GameObject BuildHeader(Transform parent, InventoryUIManager mgr)
    {
        var header = MakeImage("Header", parent, C_HeaderBg);
        AnchorRect(header, 0f, 1f, 1f, 1f, 0f, -HEADER_HEIGHT);

        var title = MakeTMP("TitleText", header.transform, "인벤토리",
                            24, FontStyles.Bold, C_TextWhite, TextAlignmentOptions.Left);
        AnchorRect(title.gameObject, 0.02f, 0.8f, 0f, 1f, 0f, 0f);

        return header;
    }

    // ═════════════════════════════════════════════════════════
    //  탭바
    // ═════════════════════════════════════════════════════════

    private static void BuildTabBar(Transform parent, InventoryUIManager mgr)
    {
        float topOff = HEADER_HEIGHT;

        var bar = MakeImage("TabBar", parent, C_HeaderBg);
        AnchorRect(bar, 0f, 1f, 1f, 1f, -topOff - TAB_HEIGHT, -topOff);

        // 탭 3개를 수평으로 균등 배치
        string[] labels    = { "전체", "장비", "소비" };
        Button[] buttons   = new Button[3];
        TextMeshProUGUI[] texts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            float xMin = i / 3f;
            float xMax = (i + 1) / 3f;

            var tabObj = MakeImage($"TabButton_{labels[i]}", bar.transform, C_TabInactive);
            AnchorRect(tabObj, xMin, xMax, 0f, 1f, 4f, -4f, 4f, -4f);

            var btn = tabObj.AddComponent<Button>();
            var nav = Navigation.defaultNavigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var label = MakeTMP("Label", tabObj.transform, labels[i],
                                18, FontStyles.Normal, C_TextDim, TextAlignmentOptions.Center);
            StretchInset(label, 4f);

            buttons[i] = btn;
            texts[i]   = label;
        }

        mgr.tabAll        = buttons[0];  mgr.tabAllText        = texts[0];
        mgr.tabEquipment  = buttons[1];  mgr.tabEquipmentText  = texts[1];
        mgr.tabConsumable = buttons[2];  mgr.tabConsumableText = texts[2];
    }

    // ═════════════════════════════════════════════════════════
    //  그리드 스크롤 영역
    // ═════════════════════════════════════════════════════════

    private static void BuildGridArea(Transform parent, InventoryUIManager mgr)
    {
        float topOff    = HEADER_HEIGHT + TAB_HEIGHT;
        float bottomOff = CLOSE_BTN_H;

        // ScrollRect 컨테이너
        var scrollObj = new GameObject("GridScrollRect", typeof(RectTransform),
                                                          typeof(ScrollRect),
                                                          typeof(Image));
        scrollObj.transform.SetParent(parent, false);
        scrollObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        AnchorRect(scrollObj, 0f, 1f, 0f, 1f, bottomOff, -topOff);

        var scrollRect     = scrollObj.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        // Viewport — RectMask2D 사용 (Mask+alpha=0 Image 대신, 셰이더 기반 클리핑)
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObj.transform, false);
        Stretch(viewport);
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        // Content (GridLayoutGroup)
        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(viewport.transform, false);

        var contentRT       = contentObj.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var grid               = contentObj.AddComponent<GridLayoutGroup>();
        grid.padding           = new RectOffset(12, 12, 12, 12);
        grid.spacing           = new Vector2(10f, 10f);
        grid.cellSize          = new Vector2(80f, 80f);
        grid.constraint        = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount   = 4;
        grid.childAlignment    = TextAnchor.UpperLeft;

        var csf            = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit    = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;

        // ── 슬롯 템플릿 ──────────────────────────────────────
        var template = BuildSlotTemplate(contentObj.transform);
        template.SetActive(false);

        mgr.gridScrollRect    = scrollRect;
        mgr.gridContent       = contentRT;
        mgr.itemSlotTemplate  = template;
    }

    // ─────────────────────────────────────────────────────────
    //  슬롯 템플릿
    // ─────────────────────────────────────────────────────────

    private static GameObject BuildSlotTemplate(Transform parent)
    {
        var slot = MakeImage("ItemSlot_Template", parent, C_SlotBg);
        var btn  = slot.AddComponent<Button>();
        var nav  = Navigation.defaultNavigation; nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        // 테두리
        var border    = MakeImage("Border", slot.transform, C_SlotBorder);
        Stretch(border);
        border.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        // 아이콘
        var iconObj   = MakeImage("Icon", slot.transform, Color.clear);
        StretchInset(iconObj, 10f);

        // 수량 뱃지 (우하단)
        var badge     = MakeImage("QuantityBadge", slot.transform, new Color(0f, 0f, 0f, 0.7f));
        var badgeRT   = badge.GetComponent<RectTransform>();
        badgeRT.anchorMin  = new Vector2(0.5f, 0f);
        badgeRT.anchorMax  = new Vector2(1.0f, 0f);
        badgeRT.pivot      = new Vector2(1f, 0f);
        badgeRT.sizeDelta  = new Vector2(0f, 22f);
        badgeRT.offsetMin  = new Vector2(0f, 0f);
        badgeRT.offsetMax  = new Vector2(0f, 22f);

        var badgeText = MakeTMP("QtyText", badge.transform, "1",
                                12, FontStyles.Bold, C_TextWhite, TextAlignmentOptions.Center);
        StretchInset(badgeText, 2f);

        var slotComp         = slot.AddComponent<InventorySlot>();
        slotComp.iconImage   = iconObj.GetComponent<Image>();
        slotComp.borderImage = border.GetComponent<Image>();
        slotComp.quantityText = badgeText;
        slotComp.button      = btn;

        return slot;
    }

    // ═════════════════════════════════════════════════════════
    //  하단 상세 패널
    // ═════════════════════════════════════════════════════════

    private static void BuildDetailPanel(Transform parent, InventoryUIManager mgr)
    {
        var panel = MakeImage("DetailPanel", parent, C_DetailBg);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0f, 0f);
        rt.anchorMax       = new Vector2(1f, 0f);
        rt.pivot           = new Vector2(0.5f, 0f);
        rt.sizeDelta       = new Vector2(0f, DETAIL_HEIGHT);
        rt.anchoredPosition = new Vector2(0f, -DETAIL_HEIGHT);  // 화면 밖

        mgr.detailPanel       = panel;
        mgr.detailPanelHeight = DETAIL_HEIGHT;

        float cursor = DETAIL_HEIGHT;

        // ── 핸들바 ────────────────────────────────────────────
        cursor -= HANDLE_HEIGHT + 10f;
        var handle = MakeImage("HandleBar", panel.transform, C_HandleBar);
        var handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin       = new Vector2(0.35f, 1f);
        handleRT.anchorMax       = new Vector2(0.65f, 1f);
        handleRT.pivot           = new Vector2(0.5f, 1f);
        handleRT.sizeDelta       = new Vector2(0f, HANDLE_HEIGHT);
        handleRT.anchoredPosition = new Vector2(0f, -8f);

        // ── 아이템 정보 행 ────────────────────────────────────
        float infoRowH = 90f;
        cursor -= infoRowH + 8f;

        var infoRow = new GameObject("ItemInfoRow", typeof(RectTransform),
                                                    typeof(HorizontalLayoutGroup));
        infoRow.transform.SetParent(panel.transform, false);
        var infoRT         = infoRow.GetComponent<RectTransform>();
        infoRT.anchorMin   = new Vector2(0f, 1f);
        infoRT.anchorMax   = new Vector2(1f, 1f);
        infoRT.pivot       = new Vector2(0.5f, 1f);
        infoRT.sizeDelta   = new Vector2(-24f, infoRowH);
        infoRT.anchoredPosition = new Vector2(0f, -30f);

        var infoHLG              = infoRow.GetComponent<HorizontalLayoutGroup>();
        infoHLG.spacing          = 14f;
        infoHLG.padding          = new RectOffset(0, 0, 0, 0);
        infoHLG.childForceExpandWidth  = false;
        infoHLG.childForceExpandHeight = true;

        // 아이콘 (대형)
        var iconContainer = MakeImage("ItemIcon", infoRow.transform, C_SlotBg);
        var iconLE        = iconContainer.AddComponent<LayoutElement>();
        iconLE.minWidth   = infoRowH;
        iconLE.minHeight  = infoRowH;
        iconLE.flexibleWidth = 0f;
        mgr.detailIcon    = iconContainer.GetComponent<Image>();

        // 텍스트 그룹
        var textGroup = new GameObject("TextGroup", typeof(RectTransform),
                                                    typeof(VerticalLayoutGroup));
        textGroup.transform.SetParent(infoRow.transform, false);
        var textLE            = textGroup.AddComponent<LayoutElement>();
        textLE.flexibleWidth  = 1f;
        var textVLG           = textGroup.GetComponent<VerticalLayoutGroup>();
        textVLG.spacing       = 4f;
        textVLG.childForceExpandWidth  = true;
        textVLG.childForceExpandHeight = false;

        mgr.detailNameText = MakeTMP("NameText", textGroup.transform, "아이템 이름",
                                     20, FontStyles.Bold, C_TextWhite,
                                     TextAlignmentOptions.TopLeft);

        mgr.detailTypeText = MakeTMP("TypeText", textGroup.transform, "장비 종류",
                                     15, FontStyles.Normal, C_TextDim,
                                     TextAlignmentOptions.TopLeft);

        mgr.detailDescText = MakeTMP("DescText", textGroup.transform, "아이템 설명",
                                     14, FontStyles.Normal, C_TextDim,
                                     TextAlignmentOptions.TopLeft);
        mgr.detailDescText.enableWordWrapping = true;

        // ── 구분선 ────────────────────────────────────────────
        float divY = -(30f + infoRowH + 12f);
        var divider = MakeImage("Divider", panel.transform, C_Divider);
        var divRT   = divider.GetComponent<RectTransform>();
        divRT.anchorMin       = new Vector2(0.02f, 1f);
        divRT.anchorMax       = new Vector2(0.98f, 1f);
        divRT.pivot           = new Vector2(0.5f, 1f);
        divRT.sizeDelta       = new Vector2(0f, 1f);
        divRT.anchoredPosition = new Vector2(0f, divY);

        // ── 스탯 목록 ─────────────────────────────────────────
        float statListY = divY - 8f;
        var statList = new GameObject("StatList", typeof(RectTransform),
                                                  typeof(VerticalLayoutGroup),
                                                  typeof(ContentSizeFitter));
        statList.transform.SetParent(panel.transform, false);

        var statRT             = statList.GetComponent<RectTransform>();
        statRT.anchorMin       = new Vector2(0f, 1f);
        statRT.anchorMax       = new Vector2(1f, 1f);
        statRT.pivot           = new Vector2(0.5f, 1f);
        statRT.sizeDelta       = new Vector2(-24f, 0f);
        statRT.anchoredPosition = new Vector2(0f, statListY);

        var statVLG            = statList.GetComponent<VerticalLayoutGroup>();
        statVLG.spacing        = 4f;
        statVLG.padding        = new RectOffset(0, 0, 4, 4);
        statVLG.childForceExpandWidth  = true;
        statVLG.childForceExpandHeight = false;

        var statCSF            = statList.GetComponent<ContentSizeFitter>();
        statCSF.verticalFit    = ContentSizeFitter.FitMode.PreferredSize;

        mgr.statListContainer = statList.transform;

        // ── 액션 버튼 행 ──────────────────────────────────────
        var actionRow = new GameObject("ActionRow", typeof(RectTransform),
                                                    typeof(HorizontalLayoutGroup));
        actionRow.transform.SetParent(panel.transform, false);

        var actionRT           = actionRow.GetComponent<RectTransform>();
        actionRT.anchorMin     = new Vector2(0f, 0f);
        actionRT.anchorMax     = new Vector2(1f, 0f);
        actionRT.pivot         = new Vector2(0.5f, 0f);
        actionRT.sizeDelta     = new Vector2(-24f, ACTION_ROW_H);
        actionRT.anchoredPosition = new Vector2(0f, CLOSE_BTN_H + 8f);

        var actionHLG              = actionRow.GetComponent<HorizontalLayoutGroup>();
        actionHLG.spacing          = 12f;
        actionHLG.childForceExpandWidth  = true;
        actionHLG.childForceExpandHeight = true;

        // 주 액션 버튼 (장착 / 해제)
        var primaryObj   = MakeButton("PrimaryButton", actionRow.transform,
                                       C_BtnPrimary, "장착", 18,
                                       out var primaryText);
        mgr.primaryButton     = primaryObj.GetComponent<Button>();
        mgr.primaryButtonText = primaryText;

        // 버리기 버튼
        var discardObj = MakeButton("DiscardButton", actionRow.transform,
                                     C_BtnDiscard, "버리기", 18,
                                     out _);
        mgr.discardButton = discardObj.GetComponent<Button>();
    }

    // ═════════════════════════════════════════════════════════
    //  하단 닫기 버튼
    // ═════════════════════════════════════════════════════════

    private static void BuildCloseButton(Transform parent, InventoryUIManager mgr)
    {
        var closeObj = MakeButton("CloseButton", parent,
                                   new Color(0.14f, 0.14f, 0.20f, 1f),
                                   "닫기", 18, out _);

        var rt             = closeObj.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0f, 0f);
        rt.anchorMax       = new Vector2(1f, 0f);
        rt.pivot           = new Vector2(0.5f, 0f);
        rt.sizeDelta       = new Vector2(0f, CLOSE_BTN_H);
        rt.anchoredPosition = Vector2.zero;

        mgr.closeButton = closeObj.GetComponent<Button>();
    }

    // ═════════════════════════════════════════════════════════
    //  Canvas 탐색 / 생성
    // ═════════════════════════════════════════════════════════

    private static Canvas FindOrCreateCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;

        var obj    = new GameObject("InventoryCanvas");
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler               = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;

        obj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(obj, "Create Inventory Canvas");
        return canvas;
    }

    // ═════════════════════════════════════════════════════════
    //  헬퍼 — GameObject 팩토리
    // ═════════════════════════════════════════════════════════

    private static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var obj   = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj;
    }

    private static TextMeshProUGUI MakeTMP(string name, Transform parent, string text,
                                            int size, FontStyles style, Color color,
                                            TextAlignmentOptions align)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var tmp         = obj.GetComponent<TextMeshProUGUI>();
        tmp.text        = text;
        tmp.fontSize    = size;
        tmp.fontStyle   = style;
        tmp.color       = color;
        tmp.alignment   = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static GameObject MakeButton(string name, Transform parent,
                                          Color bgColor, string label, int fontSize,
                                          out TextMeshProUGUI labelTmp)
    {
        var obj = MakeImage(name, parent, bgColor);
        var btn = obj.AddComponent<Button>();
        var nav = Navigation.defaultNavigation; nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        labelTmp = MakeTMP("Label", obj.transform, label, fontSize,
                           FontStyles.Bold, C_TextWhite, TextAlignmentOptions.Center);
        StretchInset(labelTmp.gameObject, 4f);
        return obj;
    }

    // ═════════════════════════════════════════════════════════
    //  헬퍼 — RectTransform 설정
    // ═════════════════════════════════════════════════════════

    /// anchorMin/Max (0~1), offsetMin.y / offsetMax.y (픽셀)
    private static void AnchorRect(GameObject go,
                                    float xMin, float xMax,
                                    float yMin, float yMax,
                                    float offsetMinY = 0f, float offsetMaxY = 0f,
                                    float offsetMinX = 0f, float offsetMaxX = 0f)
    {
        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = new Vector2(xMin, yMin);
        rt.anchorMax  = new Vector2(xMax, yMax);
        rt.offsetMin  = new Vector2(offsetMinX, offsetMinY);
        rt.offsetMax  = new Vector2(offsetMaxX, offsetMaxY);
    }

    private static void Stretch(GameObject go)
    {
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void StretchInset(GameObject go, float inset)
    {
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void StretchInset(TextMeshProUGUI tmp, float inset)
        => StretchInset(tmp.gameObject, inset);
}
#endif
