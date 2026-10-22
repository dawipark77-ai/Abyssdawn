using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [2026-10-11] 상태창 아이콘(스킬·종의 기억·종의 특성)을 누르면 뜨는 상세 팝업.
/// 스킬 상세 팝업처럼: 위 이름 + 금색 구분선, 왼쪽 큰 아이콘(황금 테두리), 오른쪽 종류·비용 줄과 설명. 바깥을 누르거나 Close/ESC 로 닫는다.
/// 코드로 한 번 만들어 두고 재사용한다 (팝업 규칙: 가로형 SubPanelBackground 테두리, 한 번에 창 하나).
/// </summary>
public class InfoDetailPopup : MonoBehaviour
{
    private const int SortingOrder = 95; // 던전 HUD 오버레이(90)·상태창보다 위

    private static InfoDetailPopup _instance;

    private GameObject _root;
    private TextMeshProUGUI _title, _tag, _body;
    private Image _icon;
    private RectTransform _panel, _indicator;
    private Image _bodyDivider;
    private const float PanelH = 620f, PanelHWithIndicator = 700f;

    /// <summary>[2026-10-22] 다음 Show 에 목표/위치 칸을 함께 그릴 스킬 (몬스터 스킬만 — DetailClickTarget.ForSkill 이 넣고 Show 가 비움).</summary>
    public static AbyssdawnBattle.SkillData NextPositionSkill;

    /// <summary>팝업을 연다. icon 이 없으면 아이콘 칸을 숨기고 글을 넓게 쓴다.</summary>
    public static void Show(string title, string tag, Sprite icon, string description)
    {
        if (_instance == null)
        {
            var go = new GameObject("[InfoDetailPopup]");
            _instance = go.AddComponent<InfoDetailPopup>();
            _instance.Build();
        }
        var posSkill = NextPositionSkill;
        NextPositionSkill = null;
        _instance.Fill(title, tag, icon, description, posSkill);
    }

    public static void Hide()
    {
        if (_instance != null && _instance._root != null) _instance._root.SetActive(false);
    }

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.activeSelf;

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

    // ─────────────────────────────────────────

    private void Build()
    {
        var canvasGo = new GameObject("InfoDetailCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 어두운 바탕 — 누르면 닫힘
        Image dim = NewImage((RectTransform)canvasGo.transform, "Dim", new Color(0f, 0f, 0f, 0.6f), true);
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<ClickToClose>();
        _root = dim.gameObject;

        // 패널 — [2026-10-17] 스테이터스 창과 같은 드퀘식: 검은 반투명 + 금테 이중선
        Image panel = NewImage(dim.rectTransform, "Panel", new Color(0f, 0f, 0f, 0.95f), true);
        Sprite frame = DungeonHud.PopupSprite;
        AddGoldFrame(panel.rectTransform, "Frame", new Color(0.86f, 0.74f, 0.45f, 0.95f), 3f);
        AddGoldFrame(panel.rectTransform, "FrameInner", new Color(0.86f, 0.74f, 0.45f, 0.30f), -8f);
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(940f, PanelH);
        _panel = prt;
        prt.anchoredPosition = Vector2.zero;

        TMP_FontAsset font = FindKoreanFont();

        _title = NewText(prt, "Title", font, 44, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(820f, 64f));

        // 금색 구분선
        Image line = NewImage(prt, "Divider", new Color(0.96f, 0.87f, 0.58f, 0.75f), false);
        Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(640f, 3f));

        _icon = NewImage(prt, "Icon", Color.white, false);
        _icon.preserveAspect = true;
        Place(_icon.rectTransform, new Vector2(0f, 1f), new Vector2(150f, -240f), new Vector2(190f, 190f));

        // [2026-10-22] 몬스터 스킬 — 오른쪽 위(종류 줄 옆) 목표(빨강)/위치(노랑) 칸
        _indicator = SkillPositionIndicator.Build(prt, font);
        _indicator.anchorMin = _indicator.anchorMax = new Vector2(1f, 1f);
        _indicator.pivot = new Vector2(0.5f, 0.5f);
        _indicator.anchoredPosition = new Vector2(-190f, -200f);
        _indicator.localScale = Vector3.one * 1.2f;
        _indicator.gameObject.SetActive(false);

        // 설명 위 구분선 (단순한 한 줄)
        _bodyDivider = NewImage(prt, "BodyDivider", new Color(0.86f, 0.74f, 0.45f, 0.35f), false);
        _bodyDivider.gameObject.SetActive(false);

        _tag = NewText(prt, "Tag", font, 28, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _tag.color = new Color(0.96f, 0.87f, 0.58f, 1f);
        // 설명 — [2026-10-17] 넘치면 스크롤 (글자를 줄이지 않는다)
        var vpGo = new GameObject("BodyViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        vpGo.transform.SetParent(prt, false);
        _bodyViewport = (RectTransform)vpGo.transform;
        var vpImg = vpGo.GetComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true; // 끌어서 스크롤
        _body = NewText(_bodyViewport, "Description", font, 32, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _body.enableWordWrapping = true;
        _body.enableAutoSizing = false;
        _body.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
        brt.offsetMin = new Vector2(0f, 0f); brt.offsetMax = new Vector2(0f, 0f);
        var fitter = _body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _bodyScroll = vpGo.AddComponent<ScrollRect>();
        _bodyScroll.content = brt; _bodyScroll.viewport = _bodyViewport;
        _bodyScroll.horizontal = false; _bodyScroll.vertical = true;
        _bodyScroll.movementType = ScrollRect.MovementType.Clamped;
        _bodyScroll.scrollSensitivity = 30f;

        // 닫기
        Image closeBg = NewImage(prt, "Close", Color.white, true);
        if (frame != null) { closeBg.sprite = frame; closeBg.type = Image.Type.Sliced; closeBg.pixelsPerUnitMultiplier = 3f; }
        else closeBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        Place(closeBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -56f), new Vector2(220f, 72f)); // [2026-10-17] 창 아래 (테두리에 잘리지 않게)
        var closeBtn = closeBg.gameObject.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);
        var closeLabel = NewText(closeBg.rectTransform, "Label", font, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        closeLabel.text = "닫기";
        Stretch(closeLabel.rectTransform);

        _root.SetActive(false);
    }

    private RectTransform _bodyViewport;
    private ScrollRect _bodyScroll;

    /// <summary>[2026-10-17] 금테 선 (GlowBorder 9분할, 가운데 비움). outset 음수면 안쪽.</summary>
    private static void AddGoldFrame(RectTransform parent, string name, Color color, float outset)
    {
        var sp = Resources.Load<Sprite>("UI/GlowBorder");
        if (sp == null) return;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sp; img.type = Image.Type.Sliced; img.fillCenter = false; img.color = color; img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset); rt.offsetMax = new Vector2(outset, outset);
    }

    private void Fill(string title, string tag, Sprite icon, string description, AbyssdawnBattle.SkillData positionSkill)
    {
        bool showPos = positionSkill != null && positionSkill.usageType == AbyssdawnBattle.UsageType.Active && icon != null;
        _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, showPos ? PanelHWithIndicator : PanelH); // 칸이 들어갈 만큼 위아래로 늘림
        if (showPos) SkillPositionIndicator.Refresh(_panel, positionSkill);
        else _indicator.gameObject.SetActive(false);

        _title.text = title ?? "";
        _tag.text = tag ?? "";
        _body.text = string.IsNullOrEmpty(description) ? "<color=#888888>(설명 없음)</color>" : description;

        bool hasIcon = icon != null;
        _icon.sprite = icon;
        _icon.enabled = hasIcon;
        if (hasIcon) SkillIconFrame.Apply(_icon, 4f);
        else { var f = _icon.transform.Find("SkillGoldFrame"); if (f != null) f.gameObject.SetActive(false); }

        // 아이콘이 있으면 오른쪽에, 없으면 패널 전체 폭으로
        float left = hasIcon ? 280f : 60f;
        RectTransform tagRT = _tag.rectTransform, body = _bodyViewport;
        tagRT.anchorMin = new Vector2(0f, 1f); tagRT.anchorMax = new Vector2(1f, 1f); tagRT.pivot = new Vector2(0f, 1f);
        tagRT.offsetMin = new Vector2(left, -186f); tagRT.offsetMax = new Vector2(showPos ? -340f : -50f, -130f); // 목표/위치 칸 자리 비움
        body.anchorMin = new Vector2(0f, 0f); body.anchorMax = new Vector2(1f, 1f); body.pivot = new Vector2(0f, 1f);
        body.offsetMin = new Vector2(left, 40f); body.offsetMax = new Vector2(-50f, showPos ? -366f : -196f); // 닫기가 창 밖으로 나가 글 칸이 넓어짐
        _bodyDivider.gameObject.SetActive(showPos);
        if (showPos)
        {
            var drt = _bodyDivider.rectTransform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.offsetMin = new Vector2(left, -352f); drt.offsetMax = new Vector2(-50f, -350f);
        }
        Canvas.ForceUpdateCanvases();
        if (_bodyScroll != null) _bodyScroll.verticalNormalizedPosition = 1f; // 맨 위부터

        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
    }

    // ─────────────────────────────────────────

    private static TMP_FontAsset FindKoreanFont()
    {
        foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.font != null && t.font.name.Contains("NotoSansKR")) return t.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static Image NewImage(RectTransform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI NewText(RectTransform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.fontStyle = style; t.alignment = align;
        t.raycastTarget = false;
        t.color = Color.white;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>어두운 바탕을 누르면 닫힘 (패널 안을 누른 것은 패널이 먼저 받아서 닫히지 않음).</summary>
    private class ClickToClose : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (e.pointerCurrentRaycast.gameObject == gameObject) Hide();
        }
    }
}

/// <summary>
/// [2026-10-11] 아이콘에 붙여 두면 누를 때 InfoDetailPopup 을 연다. 내용은 getInfo 가 그때그때 만든다 (빈 칸이면 null → 아무 일 없음).
/// </summary>
public class DetailClickTarget : MonoBehaviour, IPointerClickHandler
{
    public Func<(string title, string tag, Sprite icon, string description)?> getInfo;

    public static void Attach(Graphic icon, Func<(string title, string tag, Sprite icon, string description)?> getInfo)
    {
        if (icon == null) return;
        var t = icon.GetComponent<DetailClickTarget>();
        if (t == null) t = icon.gameObject.AddComponent<DetailClickTarget>();
        t.getInfo = getInfo;
        icon.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData e)
    {
        var info = getInfo != null ? getInfo() : null;
        if (info == null) return;
        InfoDetailPopup.Show(info.Value.title, info.Value.tag, info.Value.icon, info.Value.description);
    }

    // ── 내용 만들기 (스킬·종의 기억·종의 특성) ──

    public static (string, string, Sprite, string)? ForSkill(AbyssdawnBattle.SkillData s)
    {
        if (s == null) return null;
        string kind = s.IsPermanent ? "상시" : s.IsPassive ? "패시브" : "액티브";
        string tag = kind;
        if (s.mpCost > 0) tag += $" · MP {s.mpCost}";
        if (s.hpCostPercent > 0) tag += $" · HP {s.hpCostPercent}%";
        if (s.cooldownTurns > 0) tag += $" · Cooldown {s.cooldownTurns}";
        if (s.weaponCategory != AbyssdawnBattle.WeaponCategory.None) tag += $" · {s.weaponCategory}";
        var hero = FieldSkills.HeroData();
        if (s.maxRank > 1 && hero != null) tag += $" · Lv {hero.SkillRank(s)}/{s.maxRank}";
        else { string tier = s.TierLabel(); if (tier != null) tag += $" · {tier}"; } // [2026-10-15] 단계 스킬
        bool monster = s is AbyssdawnBattle.MonsterSkillData;
        InfoDetailPopup.NextPositionSkill = monster ? s : null; // [2026-10-22] 몬스터 스킬엔 목표/위치 칸
        string desc = monster ? SkillTooltip.ColorMonsterNumbers(s.description) : s.description; // [2026-10-22] 수치 색
        return (s.skillName, tag, s.skillIcon, desc);
    }

    public static (string, string, Sprite, string)? ForMemory(AbyssdawnBattle.MemoryOfSpeciesData m)
    {
        if (m == null) return null;
        string tag = $"Memory of Species · {m.species}" + (string.IsNullOrEmpty(m.subtitle) ? "" : $" · {m.subtitle}");
        string body = m.description ?? "";
        string stats = m.GetStatSummary();
        if (!string.IsNullOrEmpty(stats)) body += (body.Length > 0 ? "\n\n" : "") + $"<color=#C9A86A>{stats}</color>";
        if (!string.IsNullOrEmpty(m.flavorText)) body += $"\n\n<i><color=#999999>{m.flavorText}</color></i>";
        return (m.memoryName, tag, m.memoryIcon, body);
    }

    public static (string, string, Sprite, string)? ForTrait(AbyssdawnBattle.TraitsOfSpeciesData t)
    {
        if (t == null) return null;
        string name = !string.IsNullOrEmpty(t.traitName) ? t.traitName : t.traitNameEnglish;
        string body = t.description ?? "";
        if (!string.IsNullOrEmpty(t.flavorText)) body += $"\n\n<i><color=#999999>{t.flavorText}</color></i>";
        return (name, "종의 특성 (3세트 효과)", t.traitIcon, body);
    }
}
