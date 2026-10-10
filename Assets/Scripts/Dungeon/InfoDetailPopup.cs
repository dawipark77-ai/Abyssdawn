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

    /// <summary>팝업을 연다. icon 이 없으면 아이콘 칸을 숨기고 글을 넓게 쓴다.</summary>
    public static void Show(string title, string tag, Sprite icon, string description)
    {
        if (_instance == null)
        {
            var go = new GameObject("[InfoDetailPopup]");
            _instance = go.AddComponent<InfoDetailPopup>();
            _instance.Build();
        }
        _instance.Fill(title, tag, icon, description);
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

        // 패널 (가로형 테두리)
        Image panel = NewImage(dim.rectTransform, "Panel", Color.white, true);
        Sprite frame = DungeonHud.PopupSprite;
        if (frame != null) { panel.sprite = frame; panel.type = Image.Type.Sliced; }
        else panel.color = new Color(0.07f, 0.07f, 0.09f, 0.96f);
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(940f, 620f);
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

        _tag = NewText(prt, "Tag", font, 28, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _tag.color = new Color(0.96f, 0.87f, 0.58f, 1f);
        _body = NewText(prt, "Description", font, 32, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _body.enableWordWrapping = true;
        _body.enableAutoSizing = true; _body.fontSizeMin = 20f; _body.fontSizeMax = 32f;
        _body.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        // 닫기
        Image closeBg = NewImage(prt, "Close", Color.white, true);
        if (frame != null) { closeBg.sprite = frame; closeBg.type = Image.Type.Sliced; closeBg.pixelsPerUnitMultiplier = 3f; }
        else closeBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        Place(closeBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(220f, 72f));
        var closeBtn = closeBg.gameObject.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);
        var closeLabel = NewText(closeBg.rectTransform, "Label", font, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        closeLabel.text = "Close";
        Stretch(closeLabel.rectTransform);

        _root.SetActive(false);
    }

    private void Fill(string title, string tag, Sprite icon, string description)
    {
        _title.text = title ?? "";
        _tag.text = tag ?? "";
        _body.text = string.IsNullOrEmpty(description) ? "<color=#888888>(No description)</color>" : description;

        bool hasIcon = icon != null;
        _icon.sprite = icon;
        _icon.enabled = hasIcon;
        if (hasIcon) SkillIconFrame.Apply(_icon, 4f);
        else { var f = _icon.transform.Find("SkillGoldFrame"); if (f != null) f.gameObject.SetActive(false); }

        // 아이콘이 있으면 오른쪽에, 없으면 패널 전체 폭으로
        float left = hasIcon ? 280f : 60f;
        RectTransform tagRT = _tag.rectTransform, body = _body.rectTransform;
        tagRT.anchorMin = new Vector2(0f, 1f); tagRT.anchorMax = new Vector2(1f, 1f); tagRT.pivot = new Vector2(0f, 1f);
        tagRT.offsetMin = new Vector2(left, -186f); tagRT.offsetMax = new Vector2(-50f, -130f);
        body.anchorMin = new Vector2(0f, 0f); body.anchorMax = new Vector2(1f, 1f); body.pivot = new Vector2(0f, 1f);
        body.offsetMin = new Vector2(left, 112f); body.offsetMax = new Vector2(-50f, -196f);

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
        string kind = s.IsPermanent ? "Permanent" : s.IsPassive ? "Passive" : "Active";
        string tag = kind;
        if (s.mpCost > 0) tag += $" · MP {s.mpCost}";
        if (s.hpCostPercent > 0) tag += $" · HP {s.hpCostPercent}%";
        if (s.cooldownTurns > 0) tag += $" · Cooldown {s.cooldownTurns}";
        if (s.weaponCategory != AbyssdawnBattle.WeaponCategory.None) tag += $" · {s.weaponCategory}";
        var hero = FieldSkills.HeroData();
        if (s.maxRank > 1 && hero != null) tag += $" · Lv {hero.SkillRank(s)}/{s.maxRank}";
        return (s.skillName, tag, s.skillIcon, s.description);
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
        string name = !string.IsNullOrEmpty(t.traitNameEnglish) ? t.traitNameEnglish : t.traitName;
        string body = t.description ?? "";
        if (!string.IsNullOrEmpty(t.flavorText)) body += $"\n\n<i><color=#999999>{t.flavorText}</color></i>";
        return (name, "Trait of Species (3-set bonus)", t.traitIcon, body);
    }
}
