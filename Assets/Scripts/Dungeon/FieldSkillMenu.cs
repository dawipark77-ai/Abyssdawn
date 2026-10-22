using System.Collections.Generic;
using Abyssdawn;
using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-15] 던전 SKILLS 버튼 — 드퀘처럼 "지금 쓸 수 있는 스킬" 목록 (전투 스킬 창과 같은 카드 모양).
///  - 목록: 배운 스킬 중 SkillData.usableInField 가 켜진 것 (회복 마법 등)
///  - 카드를 누르면 "누구에게?" → 파티(주인공 + 동료)에서 고르면 사용 (비용 MP / HP%)
///  - 효과: 회복(Recovery 효과 % × 대상 최대 HP) · Detoxify(독 해제) · Inner Cleanse(상태이상 1개 해제) · Share Energy(동료 HP 20%)
/// 스킬 트리는 옆의 SKILL SET 버튼이 연다.
/// </summary>
public class FieldSkillMenu : MonoBehaviour
{
    private static readonly Color Gold = new Color(0.86f, 0.74f, 0.47f);
    private static readonly Color Cream = new Color(0.93f, 0.90f, 0.82f);
    private static readonly Color Muted = new Color(0.55f, 0.53f, 0.50f);
    private const float ShareEnergyHealPercent = 0.2f; // 전투와 같은 값

    private GameObject _root;
    private RectTransform _list;
    private GameObject _picker;
    private TMP_FontAsset _font;

    private void Awake()
    {
        var b = GetComponent<Button>();
        if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(Toggle); }
    }

    public void Toggle()
    {
        if (_root != null && _root.activeSelf) { Close(); return; }
        Open();
    }

    public void Open()
    {
        if (_root == null) Build();
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        if (_picker != null) Destroy(_picker);
        if (_root != null) _root.SetActive(false);
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
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (f != null && f.name == "NotoSansKR SDF") { _font = f; break; }
        return _font;
    }

    // ── 창 ──
    private void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : transform.root;
        _root = new GameObject("FieldSkill_Panel", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(parent, false);
        var rt = (RectTransform)_root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(0f, 345f); rt.offsetMax = Vector2.zero; // 아래 파티 카드는 보이게
        var dim = _root.GetComponent<Image>(); dim.color = new Color(0f, 0f, 0f, 0.6f);

        var frame = Panel(rt, "Frame", 40f, 140f, 1000f, 1000f);
        Text(frame, "스킬", 0f, 26f, 1000f, 70f, 54f, Gold, TextAlignmentOptions.Center).characterSpacing = 10f;
        Text(frame, "<color=#9E8A5E>스킬 트리는 스킬 세트에서</color>", 0f, 96f, 1000f, 40f, 26f, Muted, TextAlignmentOptions.Center);
        _list = new GameObject("List", typeof(RectTransform)).GetComponent<RectTransform>();
        _list.SetParent(frame, false);
        Place(_list, 30f, 160f, 940f, 700f);
        var back = MakeButton(frame, "Back", "뒤로", 1000f - 30f - 220f, 1000f - 30f - 90f, 220f, 90f);
        back.onClick.AddListener(Close);
    }

    private void Refresh()
    {
        for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
        var hero = Hero();
        var skills = new List<SkillData>();
        if (hero != null && hero.statData != null && hero.statData.learnedSkills != null)
            foreach (var s in hero.statData.learnedSkills)
                if (s != null && s.usableInField && !skills.Contains(s)) skills.Add(s);

        if (skills.Count == 0)
        {
            var t = Text(_list, "<color=#888888>지금 쓸 수 있는 스킬이 없다.</color>\n<size=70%><color=#666666>회복 마법처럼 전투 밖에서도 쓰는 스킬을 기억해 내면 여기에 나온다.</color></size>", 0f, 200f, 940f, 200f, 36f, Muted, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.Normal;
            return;
        }
        const float cw = 460f, ch = 110f, gap = 20f;
        for (int i = 0; i < skills.Count; i++)
        {
            var s = skills[i];
            float x = (i % 2) * (cw + gap), y = (i / 2) * (ch + gap);
            string why = CannotUse(hero, s);
            var card = Panel(_list, "Card_" + s.skillName, x, y, cw, ch);
            var iconBg = NewImage("IconBg", card, new Color(0.03f, 0.03f, 0.03f, 1f)); Place(iconBg.rectTransform, 12f, 12f, 86f, 86f);
            var icon = NewImage("Icon", iconBg.rectTransform, Color.white); Inset(icon.rectTransform, 3f);
            icon.sprite = s.skillIcon; icon.preserveAspect = true; icon.color = s.skillIcon != null ? (why == null ? Color.white : new Color(1f, 1f, 1f, 0.35f)) : new Color(1f, 1f, 1f, 0f);
            Text(card, s.skillName, 112f, 14f, cw - 124f, 46f, 32f, why == null ? Cream : Muted, TextAlignmentOptions.MidlineLeft);
            string cost = s.mpCost > 0 ? $"MP {s.mpCost}" : "";
            if (s.hpCostPercent > 0) cost += (cost.Length > 0 ? " · " : "") + $"HP {s.hpCostPercent}%";
            Text(card, why != null ? $"<color=#FF6B6B>{why}</color>" : cost, 112f, 62f, cw - 124f, 36f, 24f, Muted, TextAlignmentOptions.MidlineLeft);
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            if (why == null) { var cap = s; btn.onClick.AddListener(() => PickTarget(cap)); }
            else btn.interactable = false;
            DetailClickTarget.Attach(icon, () => DetailClickTarget.ForSkill(s)); // 아이콘만 누르면 설명
        }
        _list.sizeDelta = new Vector2(_list.sizeDelta.x, Mathf.CeilToInt(skills.Count / 2f) * (ch + gap));
    }

    private static string CannotUse(PlayerStats hero, SkillData s)
    {
        if (hero == null) return "—";
        if (s.mpCost > 0 && hero.currentMP < s.mpCost) return "MP 부족";
        if (s.hpCostPercent > 0 && hero.currentHP <= Mathf.Max(1, Mathf.RoundToInt(hero.maxHP * s.hpCostPercent / 100f))) return "HP 부족";
        return null;
    }

    // ── 대상 고르기 ──
    private void PickTarget(SkillData skill)
    {
        if (_picker != null) Destroy(_picker);
        var hero = Hero();
        bool selfOnly = skill.Key == "Inner Cleanse";
        bool alliesOnly = skill.Key == "Share Energy";

        _picker = new GameObject("TargetPicker", typeof(RectTransform), typeof(Image), typeof(Button));
        _picker.transform.SetParent(_root.transform, false);
        var prt = (RectTransform)_picker.transform; prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
        _picker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        _picker.GetComponent<Button>().onClick.AddListener(() => Destroy(_picker));

        var box = Panel(prt, "Box", 140f, 380f, 800f, 640f);
        Text(box, $"{skill.skillName} — 누구에게?", 0f, 24f, 800f, 60f, 38f, Gold, TextAlignmentOptions.Center);
        float y = 110f;
        if (!alliesOnly && hero != null)
        {
            var b = MakeButton(box, "Hero", $"{hero.playerName}   <size=75%><color=#BBBBBB>HP {hero.currentHP}/{hero.maxHP}</color></size>", 60f, y, 680f, 90f);
            b.onClick.AddListener(() => { Use(skill, hero, null, -1); });
            y += 104f;
        }
        if (!selfOnly)
        {
            var roster = CompanionPartyPersistence.ActiveRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var e = roster[i];
                if (e == null) continue;
                var so = CompanionPartyPersistence.LoadCompanion(e.resourcePath);
                if (so == null) continue;
                int idx = i;
                var b = MakeButton(box, "Ally" + i, $"{so.MonsterName}   <size=75%><color=#BBBBBB>HP {e.currentHP}/{so.HP}</color></size>", 60f, y, 680f, 90f);
                b.onClick.AddListener(() => Use(skill, null, e, idx));
                y += 104f;
            }
        }
        if (y == 110f) Text(box, "<color=#888888>쓸 대상이 없다.</color>", 0f, 200f, 800f, 60f, 32f, Muted, TextAlignmentOptions.Center);
        var cancel = MakeButton(box, "Cancel", "취소", 290f, 640f - 30f - 86f, 220f, 86f);
        cancel.onClick.AddListener(() => Destroy(_picker));
    }

    // ── 쓰기 ──
    private void Use(SkillData skill, PlayerStats heroTarget, CompanionPartyPersistence.Entry allyTarget, int rosterIndex)
    {
        var hero = Hero();
        if (hero == null || CannotUse(hero, skill) != null) { Destroy(_picker); Refresh(); return; }

        // 비용
        if (skill.mpCost > 0) hero.currentMP = hero.currentMP - skill.mpCost;
        if (skill.hpCostPercent > 0) hero.currentHP = Mathf.Max(1, hero.currentHP - Mathf.Max(1, Mathf.RoundToInt(hero.maxHP * skill.hpCostPercent / 100f)));

        string msg = Apply(skill, hero, heroTarget, allyTarget, rosterIndex);
        if (DungeonHud.Instance != null) DungeonHud.Instance.Toast(msg);
        if (GameManager.Instance != null) GameManager.Instance.SaveFromPlayer(hero);
        hero.NotifyStatusChanged();
        var bar = FindFirstObjectByType<DungeonPartyBar>(); if (bar != null) bar.Refresh();
        Destroy(_picker);
        Refresh();
    }

    private string Apply(SkillData skill, PlayerStats hero, PlayerStats heroTarget, CompanionPartyPersistence.Entry ally, int rosterIndex)
    {
        string who = heroTarget != null ? heroTarget.playerName : (ally != null ? CompanionPartyPersistence.LoadCompanion(ally.resourcePath)?.MonsterName : "?");
        switch (skill.Key)
        {
            case "Detoxify":
                if (heroTarget != null && DungeonFieldStatus.Has(StatusEffectType.Poison)) { DungeonFieldStatus.Remove(StatusEffectType.Poison); Flash(heroTarget, rosterIndex, null); return $"{skill.skillName}: <color=#9FFF9F>{who}의 독이 나았다.</color>"; }
                if (ally != null && DungeonFieldStatus.CompanionHas(ally.id, StatusEffectType.Poison)) { DungeonFieldStatus.RemoveCompanion(ally.id, StatusEffectType.Poison); Flash(null, rosterIndex, null); return $"{skill.skillName}: <color=#9FFF9F>{who}의 독이 나았다.</color>"; } // [2026-10-21] 동료
                Flash(heroTarget, rosterIndex, null);
                return $"{skill.skillName}: <color=#AAAAAA>{Ko.Eun(who)} 독에 걸려 있지 않다.</color>";
            case "Inner Cleanse":
                foreach (var kv in DungeonFieldStatus.ActiveIcons())
                {
                    DungeonFieldStatus.Remove(kv.Key);
                    Flash(hero, -1, null);
                    return $"{skill.skillName}: <color=#9FFF9F>{Ko.I(hero.playerName)} {DungeonFieldStatus.NameOf(kv.Key)}을(를) 씻어 냈다.</color>";
                }
                Flash(hero, -1, null);
                return $"{skill.skillName}: <color=#AAAAAA>씻어 낼 것이 없다.</color>";
            case "Share Energy":
                return HealTarget(skill, heroTarget, ally, rosterIndex, ShareEnergyHealPercent, who);
        }
        // 회복 효과 (Recovery %): 대상 최대 HP 의 %
        float pct = 0f;
        if (skill.Effects != null)
            foreach (var e in skill.Effects)
                if (e != null && e.effectType == EffectType.Recovery && (e.recoveryTarget == RecoveryTarget.HP || e.recoveryTarget == RecoveryTarget.Both)) pct = Mathf.Max(pct, e.effectAmount / 100f);
        if (pct > 0f) return HealTarget(skill, heroTarget, ally, rosterIndex, pct, who);
        return $"{skill.skillName}: <color=#AAAAAA>여기서는 아무 일도 없다.</color>";
    }

    private string HealTarget(SkillData skill, PlayerStats heroTarget, CompanionPartyPersistence.Entry ally, int rosterIndex, float pct, string who)
    {
        int healed = 0;
        if (heroTarget != null)
        {
            int before = heroTarget.currentHP;
            heroTarget.Heal(Mathf.Max(1, Mathf.RoundToInt(heroTarget.maxHP * pct)));
            healed = heroTarget.currentHP - before;
        }
        else if (ally != null)
        {
            var so = CompanionPartyPersistence.LoadCompanion(ally.resourcePath);
            int max = so != null ? so.HP : ally.currentHP;
            int before = ally.currentHP;
            ally.currentHP = Mathf.Min(max, ally.currentHP + Mathf.Max(1, Mathf.RoundToInt(max * pct)));
            healed = ally.currentHP - before;
        }
        Flash(heroTarget, rosterIndex, healed > 0 ? $"+{healed}" : null);
        return $"{skill.skillName}: <color=#9FFF9F>{who} +{healed} HP</color>";
    }

    private static void Flash(PlayerStats heroTarget, int rosterIndex, string number)
    {
        RectTransform card = null;
        var bar = DungeonPartyBar.Instance;
        if (heroTarget != null) card = CardFx.CardOf(heroTarget);
        else if (bar != null) card = bar.CompanionCardRect(rosterIndex);
        if (card == null) return;
        CardFx.Flash(card, new Color(0.6f, 1f, 0.7f, 0.5f));
        if (number != null && heroTarget == null) CardFx.Number(card, number, CardFx.HealColor, 10f); // 주인공은 Heal() 이 숫자를 띄움
    }

    // ── 작은 도우미 (부모 왼쪽 위 기준) ──
    private static Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false; return img;
    }
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); }
    private static void Inset(RectTransform rt, float d) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(d, d); rt.offsetMax = new Vector2(-d, -d); }

    private RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h)
    {
        var frame = NewImage(name, parent, new Color(Gold.r, Gold.g, Gold.b, 0.8f)); Place(frame.rectTransform, x, y, w, h); frame.raycastTarget = true;
        var bg = NewImage("Bg", frame.rectTransform, new Color(0.05f, 0.04f, 0.03f, 0.97f)); Inset(bg.rectTransform, 3f);
        return frame.rectTransform;
    }

    private TextMeshProUGUI Text(RectTransform parent, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>(); if (Font() != null) t.font = Font();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.richText = true; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        Place(t.rectTransform, x, y, w, h); return t;
    }

    private Button MakeButton(RectTransform parent, string name, string label, float x, float y, float w, float h)
    {
        var frame = Panel(parent, name, x, y, w, h);
        var b = frame.gameObject.AddComponent<Button>(); b.targetGraphic = frame.GetComponent<Image>();
        Text(frame, label, 0f, 0f, w, h, 34f, Gold, TextAlignmentOptions.Center);
        return b;
    }
}
