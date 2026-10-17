using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-12] 스킬 트리(Lore) 패널 모양 — 씬의 구조(탭 · 스킬 포인트 · 카테고리 아이콘 띠 · 트리 페이지 · 노드)는 그대로 두고,
/// 패널이 처음 열릴 때 코드로 팝업창과 같은 톤(가로형 금테 패널 · 금색 글자 · 한글 글꼴)으로 다시 입힌다.
///  - 탭: 배경 없이 글자만. 고른 탭은 금색 + 밑줄, 나머지는 회색
///  - 카테고리 아이콘: 황금 테두리, 이름은 아이콘 아래. 지금 열린 트리의 아이콘만 밝게
///  - 트리 제목: 금색 + 아래 구분선
///  - 노드: 이름을 아이콘 아래로 (잠김 회색 · 배울 수 있음 흰색 · 배움 금색)
/// LoreTree_Panel 에 붙인다. 글꼴은 인스펙터의 font (비어 있으면 씬에서 NotoSansKR 을 쓰는 글자를 찾아 씀).
/// </summary>
public class LoreTreeStyle : MonoBehaviour
{
    public TMP_FontAsset font;

    private static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);
    private static readonly Color GoldDim = new Color(0.70f, 0.62f, 0.43f, 1f);
    private static readonly Color Grey = new Color(0.62f, 0.62f, 0.64f, 1f);
    private static readonly Color LockedText = new Color(0.66f, 0.66f, 0.68f, 1f);
    private static readonly Color EmptySlot = new Color(0.13f, 0.12f, 0.11f, 1f);

    private const float CompactNodeScale = 0.6f;   // 이보다 작은 노드는 이름 한 줄
    private const float CompactLabelWidth = 124f;  // 한 줄 이름 칸 너비 (트리 좌표 기준)
    private const float CompactLabelMin = 12f;
    private const float NodeLabelSize = 19f;   // 화면에 보이는 크기 (노드 크기 배율과 상관없이 같게)
    private const float CategoryLabelSize = 18f;

    private LoreCategoryController _cat;
    private LoreCategoryTabController _tabs;
    private Transform _pageRoot;
    private bool _styled;

    private readonly List<TabView> _tabViews = new List<TabView>();
    private readonly List<CategoryView> _categories = new List<CategoryView>();
    private readonly List<SkillTreeNode> _nodes = new List<SkillTreeNode>();
    private GameObject _lastOpenPage;

    private class TabView { public GameObject viewport; public TMP_Text label; public Image underline; }
    private class CategoryView { public Image icon; public TMP_Text label; public GameObject page; public bool selected; public bool hasPage; }

    private void OnEnable()
    {
        if (!_styled) StyleAll();
        _lastOpenPage = null; // 다시 열면 선택 표시를 한 번 새로 그림
    }

    private void LateUpdate()
    {
        if (!_styled) return;
        RefreshTabs();
        RefreshCategories();
        RefreshNodeLabels();
    }

    // ───────────────────────────── 처음 한 번 ─────────────────────────────

    private void StyleAll()
    {
        _cat = GetComponentInChildren<LoreCategoryController>(true);
        _tabs = GetComponentInChildren<LoreCategoryTabController>(true);
        if (_cat == null) return;
        _pageRoot = _cat.lorePageParent != null ? _cat.lorePageParent : transform.Find("Lore_Page");
        if (font == null) font = FindKoreanFont();

        StyleFrame();
        StyleTabs();
        StyleSkillPoints();
        MakeCloseButton();
        StyleArrows();
        StyleCategoryIcons();
        StylePages();
        StyleDetailPopup();
        _styled = true;
    }

    private TMP_FontAsset FindKoreanFont()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            if (t != null && t.font != null && t.font.name.Contains("NotoSansKR")) return t.font;
        return null;
    }

    private void SetFont(TMP_Text t)
    {
        if (t != null && font != null) t.font = font;
    }

    /// <summary>패널 전체 배경 = 가로형 금테 패널, 트리 페이지 위에 금색 구분선.</summary>
    private void StyleFrame()
    {
        Sprite frame = DungeonHud.PopupSprite;
        var bg = _cat.GetComponent<Image>();
        if (bg != null && frame != null)
        {
            bg.sprite = frame;
            bg.type = Image.Type.Sliced;
            bg.fillCenter = true;
            bg.pixelsPerUnitMultiplier = 1.4f;
            bg.color = Color.white;
        }
        if (_pageRoot != null)
        {
            var pageImg = _pageRoot.GetComponent<Image>();
            if (pageImg != null) { pageImg.sprite = null; pageImg.color = new Color(0f, 0f, 0f, 0.35f); }

            // 트리 페이지 둘레 금테 (페이지 그림 위, 상세 팝업 아래)
            Sprite border = Resources.Load<Sprite>("UI/GlowBorder");
            if (border != null)
            {
                var edge = MakeImage("PageFrame", _pageRoot, GoldDim);
                edge.sprite = border; edge.type = Image.Type.Sliced; edge.fillCenter = false;
                var ert = edge.rectTransform;
                ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one; ert.offsetMin = new Vector2(-3f, -3f); ert.offsetMax = new Vector2(3f, 3f);
                edge.transform.SetAsLastSibling();
                Transform popup = _pageRoot.Find("SkillDetail_Popup");
                if (popup != null) popup.SetAsLastSibling();
            }

            // 트리 페이지 바로 위 구분선
            var pageRt = (RectTransform)_pageRoot;
            var catRt = (RectTransform)_cat.transform;
            Vector3 topWorld = pageRt.TransformPoint(new Vector3(0f, pageRt.rect.yMax, 0f));
            Vector2 local = catRt.InverseTransformPoint(topWorld);
            var line = MakeImage("HeaderDivider", _cat.transform, GoldDim * new Color(1f, 1f, 1f, 0.8f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(pageRt.rect.width - 40f, 2f);
            line.rectTransform.anchoredPosition = new Vector2(0f, local.y + 10f);
        }
    }

    /// <summary>WEAPONARY / DISCIPLINE / Arcane Mystery: 배경 없이 글자, 고른 탭에 금색 밑줄.</summary>
    private void StyleTabs()
    {
        if (_tabs == null) return;
        foreach (var tab in _tabs.tabs)
        {
            if (tab.button == null) continue;
            tab.selectedColor = new Color(0f, 0f, 0f, 0f);
            tab.normalColor = new Color(0f, 0f, 0f, 0f);
            var img = tab.button.GetComponent<Image>();
            if (img != null) { img.sprite = null; img.color = new Color(0f, 0f, 0f, 0f); }

            var label = tab.button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                SetFont(label);
                label.fontSize = 31f;
                label.fontStyle = FontStyles.Bold;
                label.characterSpacing = 6f;
                label.alignment = TextAlignmentOptions.Center;
                label.text = label.text.Trim().ToUpperInvariant();
            }
            var under = MakeImage("TabUnderline", tab.button.transform, Gold);
            var urt = under.rectTransform;
            urt.anchorMin = new Vector2(0.5f, 0f); urt.anchorMax = new Vector2(0.5f, 0f); urt.pivot = new Vector2(0.5f, 0.5f);
            float w = label != null ? Mathf.Min(((RectTransform)tab.button.transform).rect.width - 20f, label.preferredWidth + 16f) : 200f;
            urt.sizeDelta = new Vector2(w, 3f);
            urt.anchoredPosition = new Vector2(0f, -4f);
            _tabViews.Add(new TabView { viewport = tab.viewport, label = label, underline = under });
        }
    }

    /// <summary>"Skill Point 2" → 작은 금색 머리글 + 큰 숫자.</summary>
    private void StyleSkillPoints()
    {
        Transform sp = _cat.transform.Find("SkillsPoint");
        if (sp == null) return;
        foreach (var t in sp.GetComponentsInChildren<TMP_Text>(true))
        {
            SetFont(t);
            if (t.GetComponent<SkillPointDisplay>() != null)
            {
                t.fontSize = 40f; t.fontStyle = FontStyles.Bold; t.color = Gold;
                t.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                t.text = "스킬 포인트";
                t.fontSize = 21f; t.fontStyle = FontStyles.Bold; t.color = GoldDim; t.characterSpacing = 4f;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.enableWordWrapping = false;
            }
        }
    }

    /// <summary>[2026-10-12] 오른쪽 위 CLOSE 버튼 (트리를 아직 안 골랐을 때도 닫을 수 있게).</summary>
    private void MakeCloseButton()
    {
        if (_cat.transform.Find("CloseButton") != null) return;
        var go = new GameObject("CloseButton", typeof(RectTransform));
        go.transform.SetParent(_cat.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        Transform sp = _cat.transform.Find("SkillsPoint");
        float y = sp != null ? ((RectTransform)sp).anchoredPosition.y : 598f;
        rt.sizeDelta = new Vector2(150f, 54f);
        rt.anchoredPosition = new Vector2(((RectTransform)_cat.transform).rect.width * 0.5f - 110f, y);
        var img = go.AddComponent<Image>();
        Sprite frame = DungeonHud.PopupSprite;
        if (frame != null) { img.sprite = frame; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f; img.color = Color.white; }
        else img.color = new Color(0f, 0f, 0f, 0.6f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ClosePanel);

        var tgo = new GameObject("Label", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var t = tgo.AddComponent<TextMeshProUGUI>();
        SetFont(t);
        t.text = "닫기"; t.fontSize = 22f; t.fontStyle = FontStyles.Bold; t.characterSpacing = 4f;
        t.color = Gold; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
    }

    private void ClosePanel()
    {
        var lb = FindFirstObjectByType<LoreButtonController>();
        if (lb != null) lb.CloseLorePanel();
        if (gameObject.activeSelf) gameObject.SetActive(false); // 버튼 컨트롤러가 패널을 모르는 경우
    }

    /// <summary>◀ ▶ : 배경 없이 금색 화살표.</summary>
    private void StyleArrows()
    {
        foreach (string n in new[] { "BtnTreePrev", "BtnTreeNext" })
        {
            Transform b = _cat.transform.Find(n);
            if (b == null) continue;
            var img = b.GetComponent<Image>();
            if (img != null) { img.sprite = null; img.color = new Color(0f, 0f, 0f, 0f); }
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) { t.color = Gold; t.fontSize = 40f; }
        }
    }

    /// <summary>카테고리 아이콘: 황금 테두리 + 이름은 아이콘 아래. 그림이 없는 칸은 어두운 빈 칸.</summary>
    private void StyleCategoryIcons()
    {
        var pageOf = new Dictionary<Button, GameObject>();
        foreach (var m in _cat.lorePageMappings)
            if (m != null && m.categoryButton != null && m.lorePagePanel != null) pageOf[m.categoryButton] = m.lorePagePanel;

        foreach (var tab in _tabs != null ? _tabs.tabs : new List<LoreCategoryTabController.CategoryTab>())
        {
            if (tab.viewport == null) continue;
            // 아이콘 아래 이름이 잘리지 않게 띠의 가림 영역을 아래로 조금 넓힘
            var clip = tab.viewport.GetComponent<RectMask2D>();
            if (clip != null) clip.padding = new Vector4(0f, -46f, 0f, 0f);
            foreach (var btn in tab.viewport.GetComponentsInChildren<Button>(true))
            {
                var img = btn.GetComponent<Image>();
                if (img == null) continue;
                bool hasArt = img.sprite != null && img.sprite.name != "UISprite";
                img.preserveAspect = true;
                if (!hasArt) img.color = EmptySlot;

                var label = btn.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    SetFont(label);
                    label.text = label.text.Replace("_", " ").Trim();
                    label.fontSize = CategoryLabelSize;
                    label.fontStyle = FontStyles.Bold;
                    label.alignment = TextAlignmentOptions.Top;
                    label.enableWordWrapping = true;
                    label.lineSpacing = -18f;
                    label.overflowMode = TextOverflowModes.Overflow;
                    label.raycastTarget = false;
                    var rt = label.rectTransform;
                    rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -6f);
                    rt.sizeDelta = new Vector2(0f, 44f);
                    label.transform.SetAsLastSibling();
                }
                GameObject page;
                pageOf.TryGetValue(btn, out page);
                var view = new CategoryView { icon = img, label = label, page = page, hasPage = page != null, selected = true };
                ApplyCategoryLook(view, false);
                _categories.Add(view);
            }
        }
    }

    private static void ApplyCategoryLook(CategoryView v, bool selected)
    {
        if (v.selected == selected) return;
        v.selected = selected;
        bool hasArt = v.icon.sprite != null && v.icon.sprite.name != "UISprite";
        if (hasArt) v.icon.color = selected ? Color.white : (v.hasPage ? new Color(0.78f, 0.78f, 0.78f, 1f) : new Color(0.45f, 0.45f, 0.45f, 1f));
        SkillIconFrame.Apply(v.icon, 2f, selected);
        if (v.label != null) v.label.color = selected ? Gold : (v.hasPage ? Grey : new Color(0.42f, 0.42f, 0.44f, 1f));
    }

    /// <summary>트리 페이지마다: 제목 금색 + 구분선, 노드 이름은 아이콘 아래.</summary>
    private void StylePages()
    {
        if (_pageRoot == null) return;
        foreach (Transform page in _pageRoot)
        {
            if (page.GetComponent<SwordSkillTreeManager>() == null) continue;

            // 제목 = 노드에 속하지 않은 첫 글자
            foreach (var t in page.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<SkillTreeNode>(true) != null) continue;
                SetFont(t);
                t.fontSize = 44f; t.fontStyle = FontStyles.Bold; t.color = Gold; t.characterSpacing = 3f;
                var trt = t.rectTransform;
                var line = MakeImage("TitleDivider", trt.parent, GoldDim);
                var lrt = line.rectTransform;
                lrt.anchorMin = trt.anchorMin; lrt.anchorMax = trt.anchorMax; lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(320f, 2f);
                lrt.anchoredPosition = trt.anchoredPosition + new Vector2(0f, -trt.rect.height * 0.5f - 2f);
                break;
            }

            foreach (var n in page.GetComponentsInChildren<SkillTreeNode>(true))
            {
                if (n == null || n.IsDuplicateNode()) continue;
                StyleNode(n);
                _nodes.Add(n);
            }
        }
    }

    /// <summary>
    /// [2026-10-13] 스킬 상세 팝업 — 구성(배경 · 아이콘 · 이름 · 구분선 · 종류 · 설명 · 배우기 · 닫기)은 그대로,
    /// 글꼴·색·버튼만 팝업창 톤으로. 종류(Passive 등) 아래에 구분선을 하나 더.
    /// </summary>
    private void StyleDetailPopup()
    {
        if (_pageRoot == null) return;
        Transform pop = _pageRoot.Find("SkillDetail_Popup");
        if (pop == null) return;

        var name = FindText(pop, "Name_text");
        if (name != null) { SetFont(name); name.fontSize = 40f; name.fontStyle = FontStyles.Bold; name.color = Gold; name.characterSpacing = 1f; name.enableAutoSizing = true; name.fontSizeMin = 26f; name.fontSizeMax = 40f; }

        var cost = FindText(pop, "Cost_Text");
        if (cost != null) { SetFont(cost); cost.fontSize = 28f; cost.fontStyle = FontStyles.Bold; cost.color = GoldDim; cost.characterSpacing = 3f; }

        var desc = FindText(pop, "Description_Text");
        if (desc != null)
        {
            SetFont(desc);
            desc.fontSize = 30f; desc.fontStyle = FontStyles.Normal;
            desc.color = new Color(0.92f, 0.90f, 0.85f, 1f);
            desc.lineSpacing = 14f; desc.paragraphSpacing = 10f;
            desc.margin = new Vector4(16f, 6f, 16f, 6f);
        }

        // 종류(Passive) 아래 구분선 — 이름 아래 구분선(Divider (1))을 복제
        Transform div1 = pop.Find("Divider (1)");
        if (div1 != null && cost != null && pop.Find("Divider (2)") == null)
        {
            var div2 = Instantiate(div1.gameObject, pop).transform;
            div2.name = "Divider (2)";
            var d1 = (RectTransform)div1; var d2 = (RectTransform)div2;
            float costBottom = cost.rectTransform.anchoredPosition.y - 32f; // 한 줄 글자 바로 아래
            d2.anchoredPosition = new Vector2(d1.anchoredPosition.x, costBottom - d1.rect.height * 0.5f);
            div2.SetSiblingIndex(div1.GetSiblingIndex() + 1);
        }

        // 배우기 버튼: 가로형 금테 + 금색 글자
        var learn = pop.Find("Learn_Button");
        if (learn != null)
        {
            var img = learn.GetComponent<Image>();
            Sprite frame = DungeonHud.PopupSprite;
            if (img != null && frame != null) { img.sprite = frame; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f; img.color = Color.white; }
            var lrt = (RectTransform)learn; lrt.sizeDelta = new Vector2(Mathf.Max(lrt.sizeDelta.x, 240f), Mathf.Max(lrt.sizeDelta.y, 72f));
            // 창 테두리에 걸치지 않게 창 안쪽 아래로
            var pbg = pop.Find("Background_Image") as RectTransform;
            if (pbg != null) lrt.anchoredPosition = new Vector2(pbg.anchoredPosition.x, pbg.anchoredPosition.y - pbg.rect.height * 0.5f + lrt.sizeDelta.y * 0.5f + 44f);
            var lt = learn.GetComponentInChildren<TMP_Text>(true);
            if (lt != null) { SetFont(lt); lt.fontSize = 30f; lt.fontStyle = FontStyles.Bold; lt.color = Gold; lt.characterSpacing = 3f; }
        }

        // 닫기 버튼: 창 바깥으로 삐져나온 빨간 X 대신, 창 안쪽 오른쪽 위의 금테 X
        var close = pop.Find("Close_Button");
        if (close != null)
        {
            var crt = (RectTransform)close;
            var bg = pop.Find("Background_Image") as RectTransform;
            if (bg != null) crt.anchoredPosition = bg.anchoredPosition + new Vector2(bg.rect.width * 0.5f - 52f, bg.rect.height * 0.5f - 52f);
            crt.sizeDelta = new Vector2(56f, 56f);
            var img = close.GetComponent<Image>();
            Sprite frame = DungeonHud.PopupSprite;
            if (img != null && frame != null) { img.sprite = frame; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 4f; img.color = Color.white; }
            var red = close.Find("Image"); if (red != null) red.gameObject.SetActive(false);
            var x = close.GetComponentInChildren<TMP_Text>(true);
            if (x != null) { SetFont(x); x.text = "X"; x.fontSize = 30f; x.fontStyle = FontStyles.Bold; x.color = Gold; x.alignment = TextAlignmentOptions.Center; }
        }

        // [2026-10-17] 창 전체(틀·글자·버튼)를 크게 — 717×933 → ×1.3 (화면 폭 1080 안)
        var bgImg = pop.Find("Background_Image");
        if (bgImg != null && bgImg.localScale.x < 1.01f)
        {
            foreach (Transform c in pop)
            {
                var rt = c as RectTransform;
                if (rt == null) continue;
                rt.anchoredPosition *= DetailPopupScale;
                rt.localScale = Vector3.one * DetailPopupScale;
            }
        }
    }

    private const float DetailPopupScale = 1.3f;

    private static TMP_Text FindText(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private void StyleNode(SkillTreeNode n)
    {
        // 칸 바탕: 그림 없는 기본 사각형이면 어두운 빈 칸
        var slot = n.GetComponent<Image>();
        if (slot != null && slot != n.skillIcon && (slot.sprite == null || slot.sprite.name == "UISprite"))
            slot.color = EmptySlot;

        var label = n.skillNameText;
        if (label == null) return;
        float scale = Mathf.Max(0.2f, n.transform.localScale.x);
        SetFont(label);
        label.fontSize = NodeLabelSize / scale;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Top;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -8f / scale);
        rt.sizeDelta = new Vector2(16f / scale, 56f / scale);

        // 이름 뒤 어두운 이름판 (무늬 배경 위에서도 읽히게)
        var plate = MakeImage("NamePlate", n.transform, new Color(0f, 0f, 0f, 0.62f));
        var prt = plate.rectTransform;
        prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f); prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -5f / scale);
        prt.sizeDelta = new Vector2(10f / scale, 44f / scale);
        plate.transform.SetAsLastSibling();
        label.transform.SetAsLastSibling();

        // [2026-10-14] 노드가 작은 빽빽한 트리(격투술 등): 이름을 한 줄로 — 두 줄이면 아래 줄 노드에 가려진다.
        //   글자가 길면 칸 너비(CompactLabelWidth) 안에서 저절로 작아짐.
        if (scale < CompactNodeScale)
        {
            float nodeW = ((RectTransform)n.transform).rect.width;
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = NodeLabelSize / scale;
            label.fontSizeMin = CompactLabelMin / scale;
            label.alignment = TextAlignmentOptions.Center;
            rt.anchoredPosition = new Vector2(0f, -4f / scale);
            rt.sizeDelta = new Vector2(CompactLabelWidth / scale - nodeW, 24f / scale);
            prt.anchoredPosition = new Vector2(0f, -3f / scale);
            prt.sizeDelta = new Vector2(CompactLabelWidth / scale - nodeW, 27f / scale);
        }
    }

    // ───────────────────────────── 매 프레임 (선택 표시) ─────────────────────────────

    private void RefreshTabs()
    {
        foreach (var t in _tabViews)
        {
            bool on = t.viewport != null && t.viewport.activeSelf;
            if (t.label != null) t.label.color = on ? Gold : Grey;
            if (t.underline != null && t.underline.gameObject.activeSelf != on) t.underline.gameObject.SetActive(on);
        }
    }

    private void RefreshCategories()
    {
        GameObject open = _cat != null ? _cat.GetCurrentOpenPage() : null;
        if (open == _lastOpenPage && open != null) return;
        _lastOpenPage = open;
        foreach (var c in _categories) ApplyCategoryLook(c, c.page != null && c.page == open);
    }

    private void RefreshNodeLabels()
    {
        foreach (var n in _nodes)
        {
            if (n == null || n.skillNameText == null || !n.isActiveAndEnabled) continue;
            var s = n.GetState();
            n.skillNameText.color = s == SkillTreeNode.SkillState.Learned ? Gold
                                  : s == SkillTreeNode.SkillState.Available ? Color.white : LockedText;
            // 그림이 아직 없는 스킬은 회색 판 대신 어두운 빈 칸
            if (n.skillIcon != null && (n.skillIcon.sprite == null || n.skillIcon.sprite.name == "UISprite")) n.skillIcon.color = EmptySlot;
            // 등급 표시(Lv 0/3)도 보이는 크기를 맞춤 (글꼴은 기본 글꼴 — 한글 글꼴 아틀라스에 v 가 없어 Lv 가 깨짐)
            var rank = n.transform.Find("RankLabel");
            if (rank != null)
            {
                var rt = rank.GetComponent<TMP_Text>();
                if (rt != null) { if (TMP_Settings.defaultFontAsset != null && rt.font != TMP_Settings.defaultFontAsset) rt.font = TMP_Settings.defaultFontAsset; rt.fontSize = 17f / Mathf.Max(0.2f, n.transform.localScale.x); }
            }
        }
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        Transform existing = parent.Find(name);
        Image img = existing != null ? existing.GetComponent<Image>() : null;
        if (img == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            img = go.AddComponent<Image>();
        }
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
