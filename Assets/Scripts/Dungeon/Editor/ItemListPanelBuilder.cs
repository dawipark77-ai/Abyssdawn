using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// [2026-10-07] 아이템 창(ITEM LIST · 소지품, 2열 카드 + 스크롤)을 지금 열린 던전 씬의 Canvas 아래에 만든다.
/// 한 번 만든 뒤에는 하이라키(Canvas/ItemList_Panel)에서 직접 고치면 된다 — 이 도구를 다시 돌리면 새로 만들어 덮어쓴다.
/// 메뉴: Abyssdawn → UI → Build Item List Panel
///  - 배경: Images/BattleUI/SubPanelBackground 1 (다른 창과 같은 금테 배경)
///  - 스크롤바 손잡이: Images/Scroll_bar
///  - 메뉴의 ITEM 버튼(Canvas/MenuPanel/ITEM/Item)을 새 창의 Open() 으로 바꿔 연결 (기존 InventoryOverlay 는 그대로 둠)
/// </summary>
public static class ItemListPanelBuilder
{
    const string PanelName = "ItemList_Panel";
    const string FontPath = "Assets/Fonts/NotoSansKR SDF.asset";
    const string FramePath = "Assets/Images/BattleUI/SubPanelBackground 1.png";
    const string ScrollBarPath = "Assets/Images/Scroll_bar.png";
    const string ClosePath = "Assets/Images/Close_button.png";

    static readonly Color Gold = Hex("E8C77A");
    static readonly Color GoldDim = Hex("B89A62");
    static readonly Color TextLight = Hex("F0DDB0");
    static readonly Color TextSub = Hex("A89270");
    static readonly Color BoxDark = new Color(0.05f, 0.045f, 0.035f, 1f);
    static readonly Color LineGold = new Color(0.72f, 0.58f, 0.32f, 0.55f);

    static TMP_FontAsset _font;

    [MenuItem("Abyssdawn/UI/Build Item List Panel")]
    static void BuildFromMenu()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Item List", "플레이 중에는 만들 수 없습니다. 플레이를 멈춘 뒤 다시 실행하세요.", "확인"); return; }
        Canvas canvas = FindCanvas();
        if (canvas != null && canvas.transform.Find(PanelName) != null &&
            !EditorUtility.DisplayDialog("Item List", "이미 ItemList_Panel 이 있습니다. 지우고 새로 만들까요?\n(하이라키에서 고친 내용은 사라집니다)", "새로 만들기", "취소"))
            return;
        Debug.Log(Build());
    }

    /// <summary>만들고 결과 문구를 돌려준다. 씬은 저장하지 않음(더러운 상태로 표시만).</summary>
    public static string Build()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null) return "[ItemListPanelBuilder] 씬에 'Canvas' 가 없습니다.";
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        Transform old = canvas.transform.Find(PanelName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ── 루트: 화면 전체를 어둡게 + 뒤 클릭 막기 ──
        RectTransform root = Rect(canvas.transform, PanelName);
        Stretch(root);
        Image dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        ItemListPanel panel = root.gameObject.AddComponent<ItemListPanel>();
        panel.root = root.gameObject;

        // ── 금테 배경 ──
        RectTransform frame = Rect(root, "Frame");
        Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1720f));
        Image frameImg = frame.gameObject.AddComponent<Image>();
        frameImg.sprite = LoadSprite(FramePath);
        frameImg.color = Color.white;

        // ── 머리: 제목 / 부제 / 닫기 ──
        Text(Top(frame, "Title", 0f, -64f, 700f, 84f), "ITEM LIST", 62f, Gold, FontStyles.Bold, TextAlignmentOptions.Center).characterSpacing = 14f;
        Text(Top(frame, "SubTitle", 0f, -146f, 400f, 44f), "소지품", 30f, GoldDim, FontStyles.Normal, TextAlignmentOptions.Center);
        RectTransform close = Rect(frame, "CloseButton");
        Place(close, Vector2.one, Vector2.one, new Vector2(-46f, -46f), new Vector2(84f, 84f));
        Image closeImg = close.gameObject.AddComponent<Image>();
        closeImg.sprite = LoadSprite(ClosePath);
        closeImg.preserveAspect = true;
        panel.closeButton = AddButton(close.gameObject, closeImg);

        // ── 카테고리 탭 ──
        RectTransform tabBar = Top(frame, "CategoryTabs", 0f, -200f, 900f, 104f);
        var h = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 4f; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
        AddTab(panel, tabBar, ItemListPanel.Filter.All, "전체", null, "ALL");
        AddTab(panel, tabBar, ItemListPanel.Filter.Weapon, "무기", LoadSprite("Assets/Images/Sword01.png"), null);
        AddTab(panel, tabBar, ItemListPanel.Filter.Armour, "방어구", LoadSprite("Assets/Images/Armour01.png"), null);
        AddTab(panel, tabBar, ItemListPanel.Filter.Accessory, "장신구", LoadSprite("Assets/Images/Accessory02.png"), null);
        AddTab(panel, tabBar, ItemListPanel.Filter.Recovery, "회복", ItemIcon("Item_Equipments/Items/HP_Potion"), null);
        AddTab(panel, tabBar, ItemListPanel.Filter.Cure, "치료", ItemIcon("Item_Equipments/Items/Antidote"), null);
        AddTab(panel, tabBar, ItemListPanel.Filter.Battle, "전투", ItemIcon("Item_Equipments/Items/Smoke_Bomb"), null);
        AddTab(panel, tabBar, ItemListPanel.Filter.Special, "특수", ItemIcon("Item_Equipments/Items/Dawn_Chalice"), null);

        Line(Top(frame, "Divider", 0f, -314f, 900f, 3f));

        // ── 목록 (스크롤) ──
        // [2026-10-08] 상세는 팝업(InventoryOverlay 의 DetailPanel)으로 보므로 목록을 아래 줄 바로 위까지 늘림
        RectTransform scrollRt = Top(frame, "ItemScroll", 0f, -332f, 920f, 1254f);
        Image scrollHit = scrollRt.gameObject.AddComponent<Image>();
        scrollHit.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        RectTransform viewport = Rect(scrollRt, "Viewport");
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-56f, 0f); // 오른쪽은 스크롤바 자리
        viewport.gameObject.AddComponent<RectMask2D>();
        Image vpHit = viewport.gameObject.AddComponent<Image>();
        vpHit.color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(420f, 150f);
        grid.spacing = new Vector2(16f, 16f);
        grid.padding = new RectOffset(4, 4, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperLeft; // 한 개여도 왼쪽부터
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        panel.cardTemplate = BuildCard(content);
        scroll.viewport = viewport;
        scroll.content = content;

        // 스크롤바: 가는 금줄(트랙) + Scroll_bar 손잡이
        RectTransform bar = Rect(scrollRt, "Scrollbar");
        bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(48f, 0f);
        Image barHit = bar.gameObject.AddComponent<Image>();
        barHit.color = new Color(0f, 0f, 0f, 0f);
        RectTransform track = Rect(bar, "Track");
        track.anchorMin = new Vector2(0.5f, 0f); track.anchorMax = new Vector2(0.5f, 1f);
        track.sizeDelta = new Vector2(4f, -20f); track.anchoredPosition = Vector2.zero;
        Image trackImg = track.gameObject.AddComponent<Image>();
        trackImg.color = new Color(0.72f, 0.58f, 0.32f, 0.35f);
        trackImg.raycastTarget = false;
        RectTransform area = Rect(bar, "Sliding Area");
        Stretch(area);
        RectTransform handle = Rect(area, "Handle");
        Stretch(handle);
        Image handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.sprite = LoadSprite(ScrollBarPath);
        handleImg.preserveAspect = true;
        Scrollbar sb = bar.gameObject.AddComponent<Scrollbar>();
        sb.handleRect = handle;
        sb.targetGraphic = handleImg;
        sb.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        TextMeshProUGUI empty = Text(Rect(scrollRt, "EmptyText"), "소지품이 없습니다.", 30f, TextSub, FontStyles.Italic, TextAlignmentOptions.Center);
        Place(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-28f, -140f), new Vector2(700f, 60f));
        empty.gameObject.SetActive(false);

        panel.scroll = scroll;
        panel.content = content;
        panel.emptyText = empty;

        // ── 상세 칸 ──
        RectTransform detail = Top(frame, "DetailSection", 0f, -1110f, 920f, 470f);
        Box(detail.gameObject);

        TextMeshProUGUI hint = Text(Rect(detail, "DetailHint"), "아이템을 선택하세요", 30f, TextSub, FontStyles.Italic, TextAlignmentOptions.Center);
        Stretch(hint.rectTransform);
        panel.detailHint = hint.gameObject;

        RectTransform dc = Rect(detail, "DetailContent");
        Stretch(dc);
        panel.detailRoot = dc.gameObject;

        RectTransform iconFrame = Left(dc, "IconFrame", 28f, -28f, 112f, 112f);
        Box(iconFrame.gameObject);
        RectTransform dIcon = Rect(iconFrame, "Icon");
        Stretch(dIcon, 10f);
        panel.detailIcon = dIcon.gameObject.AddComponent<Image>();
        panel.detailIcon.preserveAspect = true;
        panel.detailIcon.raycastTarget = false;

        panel.detailName = Text(Left(dc, "Name", 160f, -30f, 440f, 52f), "Item Name", 36f, TextLight, FontStyles.Bold, TextAlignmentOptions.Left);
        panel.detailName.textWrappingMode = TextWrappingModes.NoWrap;
        panel.detailName.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform tag = Rect(dc, "Tag");
        Place(tag, Vector2.one, Vector2.one, new Vector2(-28f, -38f), new Vector2(300f, 40f));
        panel.detailTag = Text(tag, "분류 · 소비", 22f, GoldDim, FontStyles.Normal, TextAlignmentOptions.Right);
        panel.detailDesc = Text(Left(dc, "Desc", 160f, -88f, 732f, 116f), "“설명”", 25f, Hex("CDBB95"), FontStyles.Italic, TextAlignmentOptions.TopLeft);

        Line(Top(dc, "Divider", 0f, -220f, 864f, 2f));

        RectTransform stats = Top(dc, "StatBoxes", 0f, -238f, 864f, 106f);
        var sh = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
        sh.spacing = 18f; sh.childControlWidth = true; sh.childControlHeight = true; sh.childForceExpandWidth = true; sh.childForceExpandHeight = true;
        panel.statValues = new TextMeshProUGUI[3];
        panel.statLabels = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform b = Rect(stats, "StatBox_" + (i + 1));
            Box(b.gameObject);
            RectTransform val = Rect(b, "Value");
            Place(val, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -12f), new Vector2(-16f, 48f));
            val.pivot = new Vector2(0.5f, 1f); val.anchoredPosition = new Vector2(0f, -12f);
            panel.statValues[i] = Text(val, "+30% HP", 32f, TextLight, FontStyles.Bold, TextAlignmentOptions.Center);
            panel.statValues[i].textWrappingMode = TextWrappingModes.NoWrap;
            panel.statValues[i].enableAutoSizing = true; panel.statValues[i].fontSizeMin = 18f; panel.statValues[i].fontSizeMax = 32f;
            RectTransform lab = Rect(b, "Label");
            Place(lab, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(-16f, 32f));
            lab.pivot = new Vector2(0.5f, 0f); lab.anchoredPosition = new Vector2(0f, 12f);
            panel.statLabels[i] = Text(lab, "회복", 20f, TextSub, FontStyles.Normal, TextAlignmentOptions.Center);
        }

        // 버튼: 버리기 / 사용
        RectTransform discard = Rect(dc, "DiscardButton");
        Place(discard, Vector2.zero, Vector2.zero, new Vector2(28f, 26f), new Vector2(300f, 84f));
        discard.pivot = Vector2.zero; discard.anchoredPosition = new Vector2(28f, 26f);
        Image discardImg = discard.gameObject.AddComponent<Image>();
        discardImg.color = new Color(0.16f, 0.07f, 0.05f, 0.95f);
        Outline(discard.gameObject, LineGold);
        panel.discardButton = AddButton(discard.gameObject, discardImg);
        Stretch(Text(Rect(discard, "Label"), "버리기", 30f, Hex("E6B9A0"), FontStyles.Bold, TextAlignmentOptions.Center).rectTransform);

        RectTransform use = Rect(dc, "UseButton");
        Place(use, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 26f), new Vector2(520f, 84f));
        use.pivot = new Vector2(1f, 0f); use.anchoredPosition = new Vector2(-28f, 26f);
        Image useImg = use.gameObject.AddComponent<Image>();
        useImg.color = new Color(0.46f, 0.33f, 0.12f, 0.97f);
        Outline(use.gameObject, Gold);
        panel.useButton = AddButton(use.gameObject, useImg);
        panel.useButtonLabel = Text(Rect(use, "Label"), "사용 · USE", 32f, Hex("FFF0C8"), FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(panel.useButtonLabel.rectTransform);
        dc.gameObject.SetActive(false);
        detail.gameObject.SetActive(false); // [2026-10-08] 상세 팝업을 쓰므로 숨김 (필요하면 하이라키에서 켜면 됨)

        // ── 아래 줄 ── (위 구분선과 대칭인 금줄: 목록 아래끝에서 15px)
        Line(Top(frame, "BottomDivider", 0f, -1601f, 900f, 3f));
        RectTransform footer = Top(frame, "Footer", 0f, -1606f, 920f, 70f);
        RectTransform bag = Rect(footer, "BagText");
        Place(bag, new Vector2(0f, 0f), new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero);
        bag.offsetMin = new Vector2(20f, 0f); bag.offsetMax = Vector2.zero;
        panel.bagText = Text(bag, "소비 0종 · 0개   장비 0", 24f, GoldDim, FontStyles.Normal, TextAlignmentOptions.Left);
        RectTransform gold = Rect(footer, "GoldText");
        Place(gold, new Vector2(0.62f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        gold.offsetMin = Vector2.zero; gold.offsetMax = new Vector2(-20f, 0f);
        panel.goldText = Text(gold, "0 G", 32f, Hex("FFD24A"), FontStyles.Bold, TextAlignmentOptions.Right);

        root.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Item List Panel");

        string wired = WireMenuButton(panel);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[ItemListPanelBuilder] Canvas/" + PanelName + " 생성 완료. " + wired;
    }

    // ─────────────────────────────────────────

    static ItemListCard BuildCard(RectTransform content)
    {
        RectTransform card = Rect(content, "ItemCard_Template");
        card.sizeDelta = new Vector2(420f, 150f);
        Image bg = card.gameObject.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.06f, 0.05f, 1f);
        Outline border = Outline(card.gameObject, new Color(0.55f, 0.43f, 0.22f, 0.55f));
        ItemListCard c = card.gameObject.AddComponent<ItemListCard>();
        c.background = bg;
        c.border = border;
        c.button = AddButton(card.gameObject, bg);

        RectTransform iconFrame = Rect(card, "IconFrame");
        Place(iconFrame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(112f, 112f));
        iconFrame.pivot = new Vector2(0f, 0.5f); iconFrame.anchoredPosition = new Vector2(16f, 0f);
        Image f = iconFrame.gameObject.AddComponent<Image>();
        f.color = new Color(0.11f, 0.09f, 0.06f, 1f);
        f.raycastTarget = false;
        Outline(iconFrame.gameObject, new Color(0.72f, 0.58f, 0.32f, 0.35f));
        RectTransform icon = Rect(iconFrame, "Icon");
        Stretch(icon, 10f);
        c.icon = icon.gameObject.AddComponent<Image>();
        c.icon.preserveAspect = true;
        c.icon.raycastTarget = false;

        c.nameText = Text(Left(card, "Name", 142f, -22f, 266f, 44f), "Item Name", 29f, TextLight, FontStyles.Bold, TextAlignmentOptions.Left);
        c.nameText.textWrappingMode = TextWrappingModes.NoWrap;
        c.nameText.overflowMode = TextOverflowModes.Ellipsis;
        c.subText = Text(Left(card, "Sub", 142f, -68f, 266f, 34f), "분류 · 효과", 21f, TextSub, FontStyles.Normal, TextAlignmentOptions.Left);
        c.subText.textWrappingMode = TextWrappingModes.NoWrap;
        c.subText.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform qty = Rect(card, "Qty");
        Place(qty, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(150f, 40f));
        qty.pivot = new Vector2(1f, 0f); qty.anchoredPosition = new Vector2(-16f, 12f);
        c.qtyText = Text(qty, "×3", 27f, Gold, FontStyles.Bold, TextAlignmentOptions.Right);

        card.gameObject.SetActive(false);
        return c;
    }

    static void AddTab(ItemListPanel panel, RectTransform bar, ItemListPanel.Filter filter, string label, Sprite sprite, string iconText)
    {
        RectTransform t = Rect(bar, "Tab_" + filter);
        Image hit = t.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        Button b = AddButton(t.gameObject, hit);

        Image icon = null;
        RectTransform iconRt = Rect(t, "Icon");
        Place(iconRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(58f, 58f));
        iconRt.pivot = new Vector2(0.5f, 1f); iconRt.anchoredPosition = new Vector2(0f, -8f);
        if (sprite != null || iconText == null)
        {
            icon = iconRt.gameObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
            Text(iconRt, iconText, 26f, Gold, FontStyles.Bold, TextAlignmentOptions.Center);

        RectTransform lab = Rect(t, "Label");
        Place(lab, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(0f, 28f));
        lab.pivot = new Vector2(0.5f, 0f); lab.anchoredPosition = new Vector2(0f, 8f);
        Text(lab, label, 19f, GoldDim, FontStyles.Normal, TextAlignmentOptions.Center);

        RectTransform mark = Rect(t, "SelectedMark");
        Place(mark, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), Vector2.zero, new Vector2(0f, 4f));
        mark.pivot = new Vector2(0.5f, 0f); mark.anchoredPosition = Vector2.zero;
        Image m = mark.gameObject.AddComponent<Image>();
        m.color = Gold;
        m.raycastTarget = false;
        mark.gameObject.SetActive(filter == ItemListPanel.Filter.All);

        panel.tabs.Add(new ItemListPanel.CategoryTab { filter = filter, button = b, selectedMark = mark.gameObject, icon = icon });
    }

    /// <summary>메뉴의 ITEM 버튼을 새 창으로 연결 (기존 OpenInventory 연결은 뺀다).</summary>
    static string WireMenuButton(ItemListPanel panel)
    {
        Button target = null;
        foreach (Button b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!b.gameObject.scene.IsValid() || b.gameObject.scene != panel.gameObject.scene) continue;
            for (int k = 0; k < b.onClick.GetPersistentEventCount(); k++)
                if (b.onClick.GetPersistentMethodName(k) == "OpenInventory" || b.onClick.GetPersistentMethodName(k) == "Open" && b.onClick.GetPersistentTarget(k) is ItemListPanel)
                    target = b;
        }
        if (target == null) return "메뉴의 ITEM 버튼(OpenInventory 연결)을 찾지 못해 연결하지 않았습니다.";

        Undo.RecordObject(target, "Wire Item List");
        for (int k = target.onClick.GetPersistentEventCount() - 1; k >= 0; k--)
        {
            string m = target.onClick.GetPersistentMethodName(k);
            if (m == "OpenInventory" || (m == "Open" && target.onClick.GetPersistentTarget(k) is ItemListPanel))
                UnityEventTools.RemovePersistentListener(target.onClick, k);
        }
        UnityEventTools.AddVoidPersistentListener(target.onClick, new UnityAction(panel.Open));
        EditorUtility.SetDirty(target);
        return "ITEM 버튼(" + target.name + ") → ItemListPanel.Open 연결.";
    }

    // ─────────────────────────────────────────
    // 도우미
    // ─────────────────────────────────────────

    static Canvas FindCanvas()
    {
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "Canvas" && g.GetComponent<Canvas>() != null) return g.GetComponent<Canvas>();
        return null;
    }

    static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
        if (anchorMin == anchorMax && anchorMin == new Vector2(0.5f, 0.5f)) rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    /// <summary>부모 위쪽 가운데 기준 (x = 가운데에서, y = 위에서 아래로 음수).</summary>
    static RectTransform Top(Transform parent, string name, float x, float y, float w, float hgt)
    {
        RectTransform rt = Rect(parent, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, hgt);
        return rt;
    }

    /// <summary>부모 왼쪽 위 기준.</summary>
    static RectTransform Left(Transform parent, string name, float x, float y, float w, float hgt)
    {
        RectTransform rt = Rect(parent, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, hgt);
        return rt;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static Button AddButton(GameObject go, Graphic target)
    {
        var b = go.AddComponent<Button>();
        b.targetGraphic = target;
        ColorBlock cb = b.colors;
        cb.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f); // 못 누르는 버튼은 확실히 어둡게
        b.colors = cb;
        return b;
    }

    static Outline Outline(GameObject go, Color color)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(2f, -2f);
        return o;
    }

    static void Box(GameObject go)
    {
        var img = go.AddComponent<Image>();
        img.color = BoxDark;
        img.raycastTarget = false;
        Outline(go, LineGold);
    }

    static void Line(RectTransform rt)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = LineGold;
        img.raycastTarget = false;
    }

    static Sprite LoadSprite(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        Debug.LogWarning("[ItemListPanelBuilder] 스프라이트를 찾지 못함: " + path);
        return null;
    }

    static Sprite ItemIcon(string resourcesPath)
    {
        var c = Resources.Load<AbyssdawnBattle.ConsumableItemSO>(resourcesPath);
        if (c == null) return null;
        return c.itemIcon != null ? c.itemIcon : c.flatIcon != null ? c.flatIcon : c.icon;
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }
}
