using System.Collections.Generic;
using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-14] Class 패널 (메뉴 'Oath&Path') 내용 채우기.
///  - OATH & BINDING 탭: 직업의 서약 2개 (맹세한 것은 금색, 나머지는 회색) + 맹세한 서약의 제약
///  - PATH 탭: 직업 패스 스킬 목록 — LP 로 배운다 (패시브는 늘 켜짐, 액티브는 전투 스킬 목록에 들어감)
/// 씬의 기존 텍스트 칸을 그대로 쓰고, PATH 목록 줄만 코드로 만든다.
/// </summary>
public class OathPathPanel : MonoBehaviour
{
    private static readonly Color Gold = new Color(0.83f, 0.65f, 0.38f);
    private static readonly Color Text = new Color(0.90f, 0.87f, 0.80f);
    private static readonly Color Muted = new Color(0.55f, 0.53f, 0.50f);
    private static readonly Color Cost = new Color(0.85f, 0.45f, 0.40f);
    private static readonly Color RowBg = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color RowLearned = new Color(0.30f, 0.22f, 0.08f, 0.70f);

    private TMP_FontAsset _font;
    private RectTransform _content;
    private TextMeshProUGUI _pathTitle;

    private static readonly Color FrameGold = new Color(0.86f, 0.74f, 0.47f, 0.9f); // 옅은 황금색 (아비스던 테두리)
    private bool _setup;

    /// <summary>[2026-10-15] 한 번만: 닫기 버튼 연결 · 탭 크게 · 배경 테두리.</summary>
    private void Setup()
    {
        if (_setup) return;
        _setup = true;
        var close = transform.Find("Close_Button");
        var cb = close != null ? close.GetComponent<Button>() : null;
        if (cb != null)
        {
            cb.onClick.RemoveAllListeners();
            cb.onClick.AddListener(() =>
            {
                var ctl = FindFirstObjectByType<OathPathButtonController>();
                if (ctl != null) ctl.CloseClassPanel(); else gameObject.SetActive(false);
            });
            StyleButton(cb, 36f);
        }
        foreach (var n in new[] { "TabButtons/Tab_OathBinding", "TabButtons/Tab_ClassPath" })
        {
            var t = transform.Find(n);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b != null) StyleButton(b, 36f);
        }
        AddFrame(transform.Find("Oath&Binding /BackGround") ?? transform.Find("Oath&Binding/BackGround"));
        AddFrame(transform.Find("Class_Path/Background"));
        // 탭·닫기는 페이지 배경보다 위에 그려야 보인다
        var tabs = transform.Find("TabButtons"); if (tabs != null) tabs.SetAsLastSibling();
        if (close != null) close.SetAsLastSibling();
        // 탭은 창 위 왼쪽 (좁게 — 가운데 제목을 가리지 않게), 닫기는 위 오른쪽. 아래에 두면 메뉴 줄에 가려짐
        var bgT = transform.Find("Oath&Binding /BackGround") ?? transform.Find("Oath&Binding/BackGround");
        if (bgT != null && tabs != null)
        {
            var c = new Vector3[4]; ((RectTransform)bgT).GetWorldCorners(c);
            Vector3 tl = tabs.InverseTransformPoint(c[1]);
            var t1 = tabs.Find("Tab_OathBinding") as RectTransform; var t2 = tabs.Find("Tab_ClassPath") as RectTransform;
            if (t1 != null) { t1.sizeDelta = new Vector2(150f, 72f); t1.anchoredPosition = new Vector2(tl.x + 30f + 75f, tl.y - 30f - 36f); }
            if (t2 != null) { t2.sizeDelta = new Vector2(150f, 72f); t2.anchoredPosition = new Vector2(tl.x + 30f + 75f + 160f, tl.y - 30f - 36f); }
            if (close != null)
            {
                Vector3 tr = close.parent.InverseTransformPoint(c[2]);
                ((RectTransform)close).sizeDelta = new Vector2(150f, 72f);
                ((RectTransform)close).anchoredPosition = new Vector2(tr.x - 30f - 75f, tr.y - 30f - 36f);
            }
        }
        // 패스 목록을 창 너비에 맞게 넓힘 (예전 603 → 880)
        var sv = transform.Find("Class_Path/Scroll View") as RectTransform;
        if (sv != null) sv.sizeDelta = new Vector2(880f, sv.sizeDelta.y);
    }

    private void StyleButton(Button b, float size)
    {
        var rt = (RectTransform)b.transform;
        rt.sizeDelta = new Vector2(200f, 76f);
        var img = b.GetComponent<Image>();
        if (img != null) img.color = new Color(0.05f, 0.04f, 0.03f, 0.92f);
        var o = b.GetComponent<Outline>() ?? b.gameObject.AddComponent<Outline>();
        o.effectColor = FrameGold; o.effectDistance = new Vector2(2f, -2f);
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { if (Font() != null) tmp.font = Font(); tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center; }
    }

    private void AddFrame(Transform bg)
    {
        if (bg == null || bg.parent.Find(bg.name + "_Frame") != null) return;
        var go = new GameObject(bg.name + "_Frame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(bg.parent, false);
        go.transform.SetSiblingIndex(bg.GetSiblingIndex()); // 배경 바로 뒤
        var rt = (RectTransform)go.transform; var brt = (RectTransform)bg;
        rt.anchorMin = brt.anchorMin; rt.anchorMax = brt.anchorMax; rt.pivot = brt.pivot;
        rt.anchoredPosition = brt.anchoredPosition; rt.sizeDelta = brt.sizeDelta + new Vector2(12f, 12f);
        var img = go.GetComponent<Image>(); img.color = FrameGold; img.raycastTarget = false;
    }

    private void OnEnable()
    {
        Setup();
        Refresh();
        PlayerStats.OnStatusChanged += Refresh;
    }

    private void OnDisable()
    {
        PlayerStats.OnStatusChanged -= Refresh;
    }

    // 지금 열린 탭 글씨는 금색, 나머지는 회색
    private void LateUpdate()
    {
        TabColor("TabButtons/Tab_OathBinding", "Oath&Binding ");
        TabColor("TabButtons/Tab_ClassPath", "Class_Path");
    }

    private void TabColor(string tab, string page)
    {
        var t = transform.Find(tab);
        var p = transform.Find(page);
        var tmp = t != null ? t.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (tmp != null && p != null) tmp.color = p.gameObject.activeSelf ? Gold : Muted;
    }

    private static PlayerStats Hero()
    {
        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            if (p != null && !p.IsRecruitedCompanion) return p;
        return FindFirstObjectByType<PlayerStats>();
    }

    private TMP_FontAsset Font()
    {
        if (_font != null) return _font;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name == "NotoSansKR SDF") { _font = f; break; }
        return _font;
    }

    public void Refresh()
    {
        var hero = Hero();
        var sd = hero != null ? hero.statData : null;
        var job = sd != null ? sd.currentJob : null;
        FillOath(sd, job);
        FillPath(hero, sd, job);
    }

    // ── OATH & BINDING ──
    private void FillOath(PlayerStatData sd, CharacterClass job)
    {
        var page = transform.Find("Oath&Binding ") ?? transform.Find("Oath&Binding");
        if (page == null) return;
        var sworn = sd != null ? sd.currentOath : null;
        var oaths = job != null ? job.oaths : null;

        Set(page, "Class", JobName(job), 34f, Gold, TextAlignmentOptions.Center);
        for (int i = 0; i < 2; i++)
        {
            OathBindingData o = oaths != null && i < oaths.Count ? oaths[i] : null;
            bool isSworn = o != null && o == sworn;
            Set(page, $"Oath {i + 1}", o != null ? o.oathName : "—", 40f, isSworn ? Gold : Muted, TextAlignmentOptions.Center);
            string desc = o == null ? "" : (isSworn ? "<color=#D4A761>맹세함</color>\n\n" : "<color=#777777>맹세하지 않음</color>\n\n") + o.oathDescription;
            Set(page, $"Oath_Descliption {i + 1}", desc, 30f, isSworn ? Text : Muted, TextAlignmentOptions.Top);
        }
        Set(page, "Binding ", "제약", 40f, Cost, TextAlignmentOptions.Center);
        Set(page, "Binding", "제약", 40f, Cost, TextAlignmentOptions.Center);
        string binding = sworn == null ? "<color=#777777>맹세한 서약이 없다.</color>"
            : $"<color=#AAAAAA>[{StrengthName(sworn.bindingStrength)}]</color>  {sworn.bindingDescription}";
        Set(page, "Binding_Descliption ", binding, 30f, Text, TextAlignmentOptions.Top);
        Set(page, "Binding_Descliption", binding, 30f, Text, TextAlignmentOptions.Top);
    }

    private static string JobName(CharacterClass job)
    {
        if (job == null) return "";
        switch (job.className)
        {
            case "Warrior": return "전사";
            case "Thief": return "도적";
            case "Monk": return "몽크";
            case "Mage": return "마법사";
            case "Sister": return "수녀";
        }
        return job.className;
    }

    private static string StrengthName(OathBindingStrength s)
        => s == OathBindingStrength.Weak ? "약함" : s == OathBindingStrength.Medium ? "중간" : "강함";

    private void Set(Transform page, string child, string text, float size, Color color, TextAlignmentOptions align)
    {
        var t = page.Find(child);
        var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        if (tmp == null) return;
        if (Font() != null) tmp.font = Font();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.enableAutoSizing = true; tmp.fontSizeMax = size; tmp.fontSizeMin = size * 0.65f; // 칸을 넘치면 조금 줄임
    }

    // ── PATH ──
    private void FillPath(PlayerStats hero, PlayerStatData sd, CharacterClass job)
    {
        var page = transform.Find("Class_Path");
        if (page == null) return;
        if (_content == null)
        {
            var c = page.Find("Scroll View/Viewport/Content");
            if (c == null) return;
            _content = (RectTransform)c;
            foreach (Transform old in _content) old.gameObject.SetActive(false); // 옛 빈 노드 숨김
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            var v = _content.gameObject.GetComponent<VerticalLayoutGroup>() ?? _content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12); v.spacing = 12f;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = _content.gameObject.GetComponent<ContentSizeFitter>() ?? _content.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = page.Find("Title");
            _pathTitle = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            var ch = _content.GetChild(i);
            if (ch.name.StartsWith("PathRow")) Destroy(ch.gameObject);
        }

        int lp = hero != null ? hero.skillPoints : 0;
        if (_pathTitle != null)
        {
            if (Font() != null) _pathTitle.font = Font();
            _pathTitle.fontSize = 40f; _pathTitle.color = Gold; _pathTitle.alignment = TextAlignmentOptions.Center;
            _pathTitle.text = $"{JobName(job)} 패스   <size=75%><color=#E6DECC>LP {lp}</color></size>";
        }

        var list = job != null ? job.pathSkills : null;
        if (list == null || list.Count == 0)
        {
            Row(null, "<color=#777777>이 직업의 패스는 아직 열리지 않았다.</color>", false, false, null);
            return;
        }
        foreach (var p in list)
        {
            if (p == null) continue;
            bool learned = sd != null && sd.HasPathSkill(p.skillID);
            bool can = !learned && lp >= p.lpCost;
            var skill = p;
            Row(p, Describe(p), learned, can, () => Learn(hero, sd, skill));
        }
    }

    private static string Describe(PathSkillData p)
    {
        bool active = p.type != PathSkillType.Passive;
        string kind = active ? "<color=#7CC8FF>액티브</color>" : "<color=#B8D88A>패시브</color>";
        string cost = p.costType == PathSkillCostType.HPPercent ? $"  <color=#D9736A>HP {p.costValue:0.#}%</color>"
                    : p.costType == PathSkillCostType.MPPercent ? $"  <color=#7CC8FF>MP {p.costValue:0.#}%</color>" : "";
        string s = $"<size=115%><color=#D4A761>{p.skillName}</color></size>   <size=85%>T{p.tier} · {kind}{cost}</size>\n"
                 + $"<size=90%>{p.effectDescription}</size>";
        if (!string.IsNullOrEmpty(p.drawbackDescription))
            s += $"\n<size=85%><color=#D9736A>대가: {p.drawbackDescription}</color></size>";
        return s;
    }

    private void Row(PathSkillData p, string body, bool learned, bool canLearn, System.Action onLearn)
    {
        var row = new GameObject("PathRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_content, false);
        row.GetComponent<Image>().color = learned ? RowLearned : RowBg;
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 12, 12); h.spacing = 12f;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;

        var txt = NewText(row.transform, "Body", body, 28f, Text, TextAlignmentOptions.TopLeft);
        var le = txt.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1f;

        if (p == null) return;
        var btnGo = new GameObject("Learn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnGo.transform.SetParent(row.transform, false);
        var ble = btnGo.GetComponent<LayoutElement>(); ble.preferredWidth = 150f; ble.preferredHeight = 76f; ble.minWidth = 150f;
        btnGo.GetComponent<Image>().color = learned ? new Color(0, 0, 0, 0) : canLearn ? new Color(0.35f, 0.26f, 0.10f, 1f) : new Color(0.15f, 0.15f, 0.15f, 1f);
        var btn = btnGo.GetComponent<Button>();
        btn.interactable = canLearn;
        string label = learned ? "<color=#D4A761>배움</color>" : $"배우기\n<size=75%>LP {p.lpCost}</size>";
        var bt = NewText(btnGo.transform, "Text", label, 28f, canLearn ? Gold : Muted, TextAlignmentOptions.Center);
        var brt = (RectTransform)bt.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        if (canLearn && onLearn != null) btn.onClick.AddListener(() => onLearn());
    }

    private TextMeshProUGUI NewText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (Font() != null) t.font = Font();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.enableWordWrapping = true; t.richText = true; t.raycastTarget = false;
        return t;
    }

    private void Learn(PlayerStats hero, PlayerStatData sd, PathSkillData p)
    {
        if (hero == null || sd == null || p == null || sd.HasPathSkill(p.skillID) || hero.skillPoints < p.lpCost) return;
        hero.skillPoints = hero.skillPoints - p.lpCost;
        if (sd.learnedPathSkills == null) sd.learnedPathSkills = new List<PathSkillData>();
        sd.learnedPathSkills.Add(p);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(sd);
#endif
        Debug.Log($"[OathPathPanel] 패스 스킬 배움: {p.skillName} (LP 남음 {hero.skillPoints})");
        Refresh();
    }
}
