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
        RefreshSkillSetBar();
    }

    // ───────────────────────────── 처음 한 번 ─────────────────────────────

    private void StyleAll()
    {
        _cat = GetComponentInChildren<LoreCategoryController>(true);
        _tabs = GetComponentInChildren<LoreCategoryTabController>(true);
        if (_cat == null) return;
        _pageRoot = _cat.lorePageParent != null ? _cat.lorePageParent : transform.Find("Lore_Page");
        if (font == null) font = FindKoreanFont();

        FillScreen(); // [2026-10-20] 좌우·위는 화면 끝까지, 아래는 캐릭터 카드 윗선
        StyleFrame();
        StyleTabs();
        StyleSkillPoints();
        MakeCloseButton();
        StyleArrows();
        StyleCategoryIcons();
        RelayoutPanel(); // [2026-10-19] 무기 아이콘 작게·위로, 트리 칸 세로로 넓게, 스킬 세트 + 닫기는 맨 아래
        StylePages();
        StyleDetailPopup();
        BuildSkillSetBar(); // [2026-10-19] 아래 스킬 세트 줄
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
        // [2026-10-19] 패널 배경 = 씬에 놓인 어두운 판(KakaoTalk_20260323_224725279) 그대로. 트리 페이지는 투명 — 모든 트리가 같은 어두운 톤
        var bg = _cat.GetComponent<Image>();
        if (bg != null) bg.color = Color.white;
        if (_pageRoot != null)
        {
            // 스킬 세트 줄 자리 — 트리 페이지 아래를 비운다
            var prt0 = (RectTransform)_pageRoot;
            var pageImg = _pageRoot.GetComponent<Image>();
            if (pageImg != null) { pageImg.sprite = null; pageImg.color = new Color(0f, 0f, 0f, 0f); }

            // 트리 페이지 둘레 금테 (페이지 그림 위, 상세 팝업 아래)
            // [2026-10-20] 컨테이너(금테 상자) 없음 — 스킬이 맨 뒤 배경 위에 바로 보이게
            Sprite border = null;
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
            var line = MakeImage("HeaderDivider", _cat.transform, new Color(0f, 0f, 0f, 0f)); // [2026-10-20] 상자 윗변 선도 없앰 (자리 계산용으로만 남김)
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
            // [2026-10-19] 탭 = 아비스던 가로형 배경 (고른 탭은 밝게, 나머지는 어둡게)
            tab.selectedColor = Color.white;
            tab.normalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            var img = tab.button.GetComponent<Image>();
            Sprite tabFrame = DungeonHud.PopupSprite;
            if (img != null && tabFrame != null) { img.sprite = tabFrame; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f; img.fillCenter = true; }
            var trt0 = (RectTransform)tab.button.transform; trt0.sizeDelta = new Vector2(trt0.sizeDelta.x, Mathf.Max(trt0.sizeDelta.y, 64f));

            var label = tab.button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                SetFont(label);
                label.fontSize = 26f;
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
                t.fontSize = 26f; t.fontStyle = FontStyles.Bold; t.color = Gold; // [2026-10-19] 40 → 26
                t.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                t.text = "스킬 포인트";
                t.fontSize = 17f; t.fontStyle = FontStyles.Bold; t.color = GoldDim; t.characterSpacing = 2f; // [2026-10-19] 21 → 17
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
            if (clip != null) clip.padding = Vector4.zero; // [2026-10-20] 아이콘 이름 없음 → 가림 영역 넓힐 필요 없음
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
                    label.gameObject.SetActive(false); // [2026-10-20] 아이콘만 — 이름은 지움
                }
                GameObject page;
                pageOf.TryGetValue(btn, out page);
                var view = new CategoryView { icon = img, label = label, page = page, hasPage = page != null, selected = true };
                ApplyCategoryLook(view, false);
                _categories.Add(view);
            }
        }
    }

    /// <summary>[2026-10-20] 패널을 화면 좌우·위 끝까지, 아래는 캐릭터 카드 윗선까지.</summary>
    private void FillScreen()
    {
        var me = (RectTransform)transform;
        me.anchorMin = Vector2.zero; me.anchorMax = Vector2.one;
        float bottom = me.offsetMin.y;
        var bar = GameObject.Find(PartyCardVisuals.PartyBarName);
        if (bar != null && me.parent != null)
        {
            float cardsTop = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var slot = PartyCardVisuals.FindSlot(bar.transform, i) as RectTransform;
                if (slot != null) cardsTop = Mathf.Max(cardsTop, RectIn(slot, me.parent).yMax);
            }
            if (cardsTop > float.MinValue) bottom = cardsTop - ((RectTransform)me.parent).rect.yMin;
        }
        me.offsetMin = new Vector2(0f, bottom);
        me.offsetMax = Vector2.zero;
        var catRt = (RectTransform)_cat.transform;
        catRt.offsetMin = Vector2.zero; catRt.offsetMax = Vector2.zero;
        if (_pageRoot != null)
        {
            var prt = (RectTransform)_pageRoot;
            prt.offsetMin = new Vector2(12f, prt.offsetMin.y); prt.offsetMax = new Vector2(-12f, prt.offsetMax.y);
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
            // [2026-10-19] 트리마다 있던 배경 그림을 걷어낸다 (패널의 어두운 판이 그대로 보이게)
            var pimg = page.GetComponent<Image>();
            if (pimg != null) { pimg.sprite = null; pimg.color = new Color(0f, 0f, 0f, 0f); }
            foreach (var bgImg in page.GetComponentsInChildren<Image>(true))
            {
                if (bgImg.transform == page || bgImg.GetComponentInParent<SkillTreeNode>(true) != null) continue;
                var brt = bgImg.rectTransform;
                if (brt.rect.width > 500f && brt.rect.height > 400f && bgImg.GetComponent<Mask>() == null && bgImg.GetComponent<ScrollRect>() == null) bgImg.enabled = false; // 큰 장식 그림
            }
            LoreTreeLayout.Apply(page); // [2026-10-19] 선행 스킬로 자동 배치

            // 제목 = 노드에 속하지 않은 첫 글자. [2026-10-19] 나머지(예전 소제목 — 자동 배치로 의미 없어짐)는 숨김
            bool titleDone = false;
            foreach (var t in page.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<SkillTreeNode>(true) != null) continue;
                if (titleDone) { t.gameObject.SetActive(false); continue; }
                titleDone = true;
                SetFont(t);
                t.fontSize = 44f; t.fontStyle = FontStyles.Bold; t.color = Gold; t.characterSpacing = 3f;
                var trt = t.rectTransform;
                // [2026-10-19] 페이지가 줄어도 제목은 페이지 안 맨 위에
                if (trt.anchorMin == new Vector2(0.5f, 0.5f) && trt.anchorMax == trt.anchorMin) // 스크롤 칸(격투술) 안이어도 그 칸의 맨 위
                    trt.anchoredPosition = new Vector2(trt.anchoredPosition.x, ((RectTransform)trt.parent).rect.height * 0.5f - 52f);
                // [2026-10-20] 트리 이름 뒤에 아비스던 가로형 배경 (글자만 떠 있으면 어색함) — 밑줄 대신
                var plate = MakeImage("TitlePlate", trt.parent, Color.white);
                Sprite pf = DungeonHud.PopupSprite;
                if (pf != null) { plate.sprite = pf; plate.type = Image.Type.Sliced; plate.pixelsPerUnitMultiplier = 2.2f; }
                else plate.color = new Color(0f, 0f, 0f, 0.6f);
                var lrt = plate.rectTransform;
                lrt.anchorMin = trt.anchorMin; lrt.anchorMax = trt.anchorMax; lrt.pivot = new Vector2(0.5f, 0.5f);
                // [2026-10-20] 한글화(SceneLabelsKo)가 켠 자동 크기의 최대값이 트리마다 달라(36/56) 크기가 들쭉날쭉 → 끄고 단검술 크기 36 고정
                t.enableAutoSizing = false;
                t.fontSize = 36f;
                lrt.sizeDelta = new Vector2(Mathf.Max(300f, t.GetPreferredValues(t.text).x + 140f), 74f);
                lrt.anchoredPosition = trt.anchoredPosition;
                plate.transform.SetSiblingIndex(t.transform.GetSiblingIndex()); // 글자 뒤
                var oldLine = trt.parent.Find("TitleDivider"); if (oldLine != null) oldLine.gameObject.SetActive(false);
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

        // [2026-10-19] 종류 줄(검 장착 시 · 패시브)을 이름 아래 구분선 바로 밑으로 올린다
        Transform div1 = pop.Find("Divider (1)");
        if (cost != null && div1 != null)
        {
            var crt0 = cost.rectTransform;
            crt0.sizeDelta = new Vector2(crt0.sizeDelta.x, 44f);
            crt0.anchoredPosition = new Vector2(crt0.anchoredPosition.x, ((RectTransform)div1).anchoredPosition.y - 36f);
            cost.alignment = TextAlignmentOptions.Center;
        }
        // 종류 아래 구분선 — 이름 아래 구분선(Divider (1))을 복제
        if (div1 != null && cost != null && pop.Find("Divider (2)") == null)
        {
            var div2 = Instantiate(div1.gameObject, pop).transform;
            div2.name = "Divider (2)";
            var d1 = (RectTransform)div1; var d2 = (RectTransform)div2;
            float costBottom = cost.rectTransform.anchoredPosition.y - 24f;
            d2.anchoredPosition = new Vector2(d1.anchoredPosition.x, costBottom - d1.rect.height * 0.5f);
            div2.SetSiblingIndex(div1.GetSiblingIndex() + 1);
            // 그 아래 빈 자리에 사용 위치 · 목표 표시 (다키스트 던전식)
            var ind = SkillPositionIndicator.Build(pop, font);
            ind.anchoredPosition = new Vector2(d1.anchoredPosition.x - 10f, d2.anchoredPosition.y - 66f);
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

        // [2026-10-20] 설명이 길면 잘리지 않고 스크롤 — 설명 칸 위 ~ 배우기 버튼 위
        if (desc != null && desc.transform.parent == pop)
        {
            var r = RectIn(desc.rectTransform, pop);
            float bottomY = r.yMin;
            var lb = pop.Find("Learn_Button") as RectTransform;
            if (lb != null) bottomY = RectIn(lb, pop).yMax + 10f;
            var vpGo = new GameObject("DescScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            var vp = (RectTransform)vpGo.transform;
            vp.SetParent(pop, false);
            vp.SetSiblingIndex(desc.transform.GetSiblingIndex());
            vp.anchorMin = vp.anchorMax = vp.pivot = new Vector2(0.5f, 0.5f);
            var popRect = ((RectTransform)pop).rect;
            vp.sizeDelta = new Vector2(r.width, Mathf.Max(80f, r.yMax - bottomY));
            vp.anchoredPosition = new Vector2(r.center.x - popRect.center.x, (r.yMax + bottomY) * 0.5f - popRect.center.y);
            vpGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var drt = desc.rectTransform;
            drt.SetParent(vp, false);
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(0.5f, 1f);
            drt.anchoredPosition = Vector2.zero; drt.sizeDelta = new Vector2(0f, drt.sizeDelta.y);
            desc.overflowMode = TextOverflowModes.Overflow;
            var fit = desc.GetComponent<ContentSizeFitter>(); if (fit == null) fit = desc.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = vpGo.GetComponent<ScrollRect>();
            sr.content = drt; sr.viewport = vp; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f; sr.inertia = false;
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

    // ───────────────────────────── [2026-10-19] 아래 스킬 세트 줄 ─────────────────────────────
    private const float SkillSetBarH = 150f;
    private const float CloseH = 72f, BottomPad = 26f, IconScale = 0.72f, IconGap = 30f;

    // 공간(space) 기준 사각형
    private static Rect RectIn(RectTransform rt, Transform space)
    {
        var c = new Vector3[4]; rt.GetWorldCorners(c);
        Vector3 a = space.InverseTransformPoint(c[0]), b = space.InverseTransformPoint(c[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
    private static void MoveBy(RectTransform rt, Transform space, Vector2 d)
    {
        // space 좌표의 이동량 → 부모 좌표 이동량
        Vector3 w0 = space.TransformPoint(Vector3.zero), w1 = space.TransformPoint(new Vector3(d.x, d.y, 0f));
        Vector3 p0 = rt.parent.InverseTransformPoint(w0), p1 = rt.parent.InverseTransformPoint(w1);
        rt.anchoredPosition += (Vector2)(p1 - p0);
    }

    /// <summary>[2026-10-19] 아래 캐릭터 카드 줄: 3·4번 카드를 합친 영역과 카드 윗선 (space 좌표). 못 찾으면 false.</summary>
    private static bool PartyCardsArea(Transform space, out Rect cards34, out float cardsTop)
    {
        cards34 = new Rect(); cardsTop = 0f;
        var bar = GameObject.Find(PartyCardVisuals.PartyBarName);
        if (bar == null) return false;
        bool any = false; bool any34 = false;
        for (int i = 0; i < 4; i++)
        {
            var slot = PartyCardVisuals.FindSlot(bar.transform, i) as RectTransform;
            if (slot == null) continue;
            var r = RectIn(slot, space);
            cardsTop = any ? Mathf.Max(cardsTop, r.yMax) : r.yMax; any = true;
            if (i >= 2) { cards34 = any34 ? Rect.MinMaxRect(Mathf.Min(cards34.xMin, r.xMin), Mathf.Min(cards34.yMin, r.yMin), Mathf.Max(cards34.xMax, r.xMax), Mathf.Max(cards34.yMax, r.yMax)) : r; any34 = true; }
        }
        return any && any34;
    }

    /// <summary>[2026-10-19] 탭 바로 아래에 스킬 포인트·작은 무기 아이콘 줄, 그 아래부터 트리 칸, 맨 아래에 스킬 세트 → 닫기.</summary>
    private void RelayoutPanel()
    {
        Transform me = transform;
        var meRt = (RectTransform)me;
        float top = meRt.rect.yMax, bottom = meRt.rect.yMin;

        float tabBottom = top;
        if (_tabs != null) foreach (var tab in _tabs.tabs) if (tab.button != null) tabBottom = Mathf.Min(tabBottom, RectIn((RectTransform)tab.button.transform, me).yMin);

        // 스킬 포인트: 탭 바로 아래 왼쪽
        Transform sp = _cat.transform.Find("SkillsPoint");
        float y = tabBottom - 10f;
        if (sp != null)
        {
            var r = RectIn((RectTransform)sp, me);
            MoveBy((RectTransform)sp, me, new Vector2(0f, (y - r.height * 0.5f) - r.center.y));
            y -= r.height + 4f;
        }

        // 무기 아이콘 줄: 작게 (가운데 맞춤) + 칸 사이 간격 넓힘 (8 → 30)
        foreach (var sc in GetComponentsInChildren<LoreCategoryScroller>(true)) sc.spacing = IconGap;
        if (_tabs != null) foreach (var tab in _tabs.tabs)
            if (tab.viewport != null) foreach (var h in tab.viewport.GetComponentsInChildren<HorizontalLayoutGroup>(true)) h.spacing = IconGap;
        float rowCenter = y;
        if (_tabs != null)
        {
            foreach (var tab in _tabs.tabs)
            {
                if (tab.viewport == null) continue;
                var vrt = (RectTransform)tab.viewport.transform;
                if (vrt.localScale.x > IconScale + 0.01f) vrt.sizeDelta = new Vector2(vrt.sizeDelta.x / IconScale, vrt.sizeDelta.y); // 보이는 폭은 그대로 → 아이콘이 더 많이 보임
                vrt.localScale = Vector3.one * IconScale;
                var r = RectIn(vrt, me);
                MoveBy(vrt, me, new Vector2(meRt.rect.center.x - r.center.x, (y - r.height * 0.5f) - r.center.y));
                rowCenter = y - r.height * 0.5f;
            }
            float rowH = 0f;
            foreach (var tab in _tabs.tabs) if (tab.viewport != null) rowH = Mathf.Max(rowH, RectIn((RectTransform)tab.viewport.transform, me).height);
            y -= rowH + 14f; // [2026-10-20] 아이콘 아래 이름 없음
        }
        // ◀ ▶ 화살표: 아이콘 줄 높이에 맞춤
        foreach (string n in new[] { "BtnTreePrev", "BtnTreeNext" })
        {
            var b = _cat.transform.Find(n) as RectTransform;
            if (b == null) continue;
            b.localScale = Vector3.one * 0.8f;
            var r = RectIn(b, me);
            MoveBy(b, me, new Vector2(0f, rowCenter - r.center.y));
        }
        // 헤더 구분선 (StyleFrame 에서 만든 것) 은 아이콘 줄 아래로
        var div = _cat.transform.Find("HeaderDivider") as RectTransform;
        if (div != null) { var r = RectIn(div, me); MoveBy(div, me, new Vector2(0f, (y + 8f) - r.center.y)); }

        // [2026-10-19] 닫기 = 아래 캐릭터 카드 3·4번 자리 위, 스킬 세트 = 카드 바로 위
        var close = _cat.transform.Find("CloseButton") as RectTransform;
        float closeTop = bottom + BottomPad + CloseH;
        Rect cards34; float cardsTop;
        if (PartyCardsArea(me, out cards34, out cardsTop))
        {
            closeTop = cardsTop - 6f; // 스킬 세트 바닥 = closeTop + 12 = 카드 윗선 + 6
            if (close != null)
            {
                // [2026-10-19] 2번·3번 카드 사이, 카드 윗부분 가운데 (사용자 표시 자리)
                var ct = close.GetComponentInChildren<TMP_Text>(true); if (ct != null) { ct.fontSize = 28f; ct.characterSpacing = 6f; }
                close.sizeDelta = new Vector2(220f, 64f);
                close.SetAsLastSibling();
                var r = RectIn(close, me);
                MoveBy(close, me, new Vector2(cards34.xMin - r.center.x, (cardsTop - 36f) - r.center.y));
            }
        }
        else if (close != null)
        {
            close.sizeDelta = new Vector2(240f, CloseH);
            var r = RectIn(close, me);
            MoveBy(close, me, new Vector2(meRt.rect.center.x - r.center.x, (bottom + BottomPad + CloseH * 0.5f) - r.center.y));
        }

        // 트리 칸: 아이콘 줄 아래 ~ 스킬 세트 위
        if (_pageRoot != null)
        {
            var prt = (RectTransform)_pageRoot;
            var r = RectIn(prt, me);
            float wantTop = y - 4f;
            float wantBottom = closeTop + 12f + SkillSetBarH + 12f;
            prt.offsetMax += new Vector2(0f, wantTop - r.yMax);
            prt.offsetMin += new Vector2(0f, wantBottom - r.yMin);
        }
    }
    private RectTransform _setBar;
    private readonly List<Image> _setIcons = new List<Image>();
    private readonly List<TMP_Text> _setNames = new List<TMP_Text>();
    private float _setNext;

    private void BuildSkillSetBar()
    {
        if (_pageRoot == null || transform.Find("SkillSetBar") != null) return;
        var prt = (RectTransform)_pageRoot;
        var me = (RectTransform)transform;
        Vector2 pageBottom = me.InverseTransformPoint(prt.TransformPoint(new Vector3(prt.rect.center.x, prt.rect.yMin, 0f)));
        var go = new GameObject("SkillSetBar", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _setBar = (RectTransform)go.transform;
        _setBar.anchorMin = _setBar.anchorMax = new Vector2(0.5f, 0.5f); _setBar.pivot = new Vector2(0.5f, 1f);
        _setBar.sizeDelta = new Vector2(prt.rect.width, SkillSetBarH);
        _setBar.anchoredPosition = new Vector2(pageBottom.x, pageBottom.y - 12f);
        // [2026-10-20] 스킬 세트도 상자(어두운 바탕·금테) 없이 배경 위에 칸만
        Sprite border = null;
        if (border != null)
        {
            var edge = MakeImage("Edge", _setBar, GoldDim);
            edge.sprite = border; edge.type = Image.Type.Sliced; edge.fillCenter = false;
            var ert = edge.rectTransform; ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one; ert.offsetMin = new Vector2(-2f, -2f); ert.offsetMax = new Vector2(2f, 2f);
        }
        // [2026-10-20] 스킬 세트 위 경계선 — 상세 팝업의 구분선 그림
        Sprite divSp = null;
        var popDiv = _pageRoot.Find("SkillDetail_Popup/Divider (1)");
        if (popDiv != null && popDiv.GetComponent<Image>() != null) divSp = popDiv.GetComponent<Image>().sprite;
        if (divSp != null)
        {
            var dv = MakeImage("SetDivider", _setBar, new Color(1f, 1f, 1f, 0.6f)); // [2026-10-20] 얇고 길게, 투명도 60%
            dv.sprite = divSp; dv.preserveAspect = false; dv.raycastTarget = false;
            var drt = dv.rectTransform; drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(prt.rect.width * 0.96f, 14f); drt.anchoredPosition = new Vector2(0f, 10f);
        }
        var title = NewLabel(_setBar, "스킬 세트", 22f, Gold);
        var trt = title.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f); trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(22f, -4f); trt.sizeDelta = new Vector2(300f, 34f); // [2026-10-20] 칸과 겹쳐 잘리던 것 — 위로
        title.alignment = TextAlignmentOptions.MidlineLeft;

        const float size = 76f, gap = 26f, groupGap = 40f; // 틀이 사방 12% 넘치므로 칸 사이를 넉넉히
        float total = size * 9 + gap * 7 + groupGap;
        float x = -total * 0.5f + size * 0.5f;
        for (int i = 0; i < 9; i++)
        {
            if (i == 3) x += groupGap - gap; // [2026-10-19] 왼쪽 패시브 3 | 오른쪽 액티브 6
            var slot = MakeImage("Slot" + i, _setBar, EmptySlot);
            var srt = slot.rectTransform; srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(size, size); srt.anchoredPosition = new Vector2(x, -22f); // [2026-10-20] 글자 아래로 간격
            slot.preserveAspect = true;
            var nm = NewLabel(_setBar, "", 16f, GoldDim);
            var nrt = nm.rectTransform; nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(size + gap, 24f); nrt.anchoredPosition = new Vector2(x, -4f - size * 0.5f - 18f);
            nm.enableAutoSizing = true; nm.fontSizeMin = 11f; nm.fontSizeMax = 16f;
            _setIcons.Add(slot); _setNames.Add(nm);
            x += size + gap;
        }
        // 액티브 6 | 패시브 3 구분선
        var sep = MakeImage("Split", _setBar, GoldDim * new Color(1f, 1f, 1f, 0.6f));
        var sprt = sep.rectTransform; sprt.anchorMin = sprt.anchorMax = new Vector2(0.5f, 0.5f);
        sprt.sizeDelta = new Vector2(2f, size); sprt.anchoredPosition = new Vector2(-total * 0.5f + size * 3 + gap * 2 + groupGap * 0.5f, -22f);
        RefreshSkillSetBar(true);
    }

    private void RefreshSkillSetBar(bool force = false)
    {
        if (_setBar == null) return;
        if (!force && Time.unscaledTime < _setNext) return;
        _setNext = Time.unscaledTime + 0.4f;
        PlayerStats hero = null;
        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None)) if (p != null && p.statData != null && !p.IsRecruitedCompanion) { hero = p; break; }
        var data = hero != null ? hero.statData : null;
        for (int i = 0; i < _setIcons.Count; i++)
        {
            AbyssdawnBattle.SkillData s = null;
            if (data != null)
            {
                if (i < 3) { if (data.equippedPassives != null && i < data.equippedPassives.Count) s = data.equippedPassives[i]; }
                else { int k = i - 3; if (data.equippedSkills != null && k < data.equippedSkills.Count) s = data.equippedSkills[k]; }
            }
            var img = _setIcons[i];
            img.sprite = s != null ? s.skillIcon : null;
            img.color = s != null && s.skillIcon != null ? Color.white : EmptySlot;
            SkillIconFrame.Apply(img, 2f, s != null, true);
            _setNames[i].text = ""; // [2026-10-19] 아이콘 아래 이름 없음
        }
    }

    private TMP_Text NewLabel(Transform parent, string text, float size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        SetFont(t);
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.enableWordWrapping = false;
        return t;
    }

    private static TMP_Text FindText(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private void StyleNode(SkillTreeNode n)
    {
        n.requiredSkillPoints = 1; // [2026-10-20] 모든 스킬 SP 1 (씬 노드에 남은 2·3 값 무시)
        // [2026-10-20] 칸마다 따로 깔린 바탕은 없앰 — 트리 전체 배경이 그대로 보이게 (틀과 아이콘만)
        var slot = n.GetComponent<Image>();
        if (slot != null && slot != n.skillIcon && (slot.sprite == null || slot.sprite.name == "UISprite"))
            slot.color = new Color(0f, 0f, 0f, 0f);

        var label = n.skillNameText;
        if (label == null) return;
        // [2026-10-19] 씬에서 꺼진 묶음 안에 있던 이름(전쟁술·신성)은 노드 바로 아래로 옮겨 보이게 (묶음 자체는 켜지 않음)
        if (!label.transform.parent.gameObject.activeSelf && label.transform.parent != n.transform) label.transform.SetParent(n.transform, false);
        label.gameObject.SetActive(true);
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
        rt.anchoredPosition = new Vector2(0f, -22f / scale); // [2026-10-19] 틀(사방 12%) 아래로
        rt.sizeDelta = new Vector2(16f / scale, 56f / scale);

        // [2026-10-19] 아이콘 아래 이름은 모두 뺀다 (사용자 지시) — 이름은 눌러서 상세 창에서
        label.gameObject.SetActive(false);
        var oldPlate = n.transform.Find("NamePlate"); if (oldPlate != null) oldPlate.gameObject.SetActive(false);
        return;
#pragma warning disable CS0162
        // 이름 뒤 어두운 이름판 (무늬 배경 위에서도 읽히게)
        var plate = MakeImage("NamePlate", n.transform, new Color(0f, 0f, 0f, 0.62f));
        var prt = plate.rectTransform;
        prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f); prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -19f / scale);
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

#pragma warning restore CS0162
    // ───────────────────────────── 매 프레임 (선택 표시) ─────────────────────────────

    private void RefreshTabs()
    {
        foreach (var t in _tabViews)
        {
            bool on = t.viewport != null && t.viewport.activeSelf;
            if (t.label != null) t.label.color = on ? Gold : Grey;
            if (t.underline != null && t.underline.gameObject.activeSelf) t.underline.gameObject.SetActive(false); // [2026-10-19] 밑줄 대신 배경 밝기로
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
            if (n == null || !n.isActiveAndEnabled) continue;
            if (n.skillNameText != null && n.skillNameText.gameObject.activeSelf) n.skillNameText.gameObject.SetActive(false); // [2026-10-19] 이름 없음
            // 그림이 아직 없는 스킬은 회색 판 대신 어두운 빈 칸
            if (n.skillIcon != null && (n.skillIcon.sprite == null || n.skillIcon.sprite.name == "UISprite")) n.skillIcon.color = new Color(0f, 0f, 0f, 0.35f); // [2026-10-20] 그림 없는 칸 — 배경이 비치게 옅게
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
