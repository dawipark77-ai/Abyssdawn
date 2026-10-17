using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using AbyssdawnBattle;

/// <summary>
/// [2026-10-17] 스킬 상세 글 — 스톤샤드 툴팁 형식을 아비스던식으로.
///  스톤샤드가 읽기 쉬운 이유: ① 이름 → 종류 한 줄 → 핵심 수치 → 표 순서가 늘 같다
///  ② 표는 "왼쪽 이름 · 오른쪽 값" 으로 줄을 맞추고 값에만 색 (이득 초록 · 손해 빨강 · 특수 파랑)
///  ③ 설명 문장은 맨 아래, 작고 흐리게. 한 덩어리 문단에 모든 걸 섞지 않는다.
///  아비스던은 금테 틀·금색 제목을 유지하고, 안쪽을 이 순서로 채운다.
///   Subtitle  : "검 장착 시 · 패시브" (설명 맨 앞 [ ] 꼬리표 → 없으면 종류)
///   Body      : 핵심 수치 줄 → 표(소모·재사용·명중·치명타·대상·위치·필요 무기·등급) → 효과 설명(숫자 색칠) → 선행 조건
/// </summary>
public static class SkillTooltip
{
    public const string Gold = "#D9B76A", Muted = "#9A8F7A", Text = "#E6E1D6", Good = "#8FD47A", Bad = "#E0745A", Info = "#7FB6E6", Mana = "#7FA8FF";

    private static readonly Regex LeadingTag = new Regex(@"^\s*\[([^\]]*)\]\s*");

    /// <summary>종류 한 줄 (이름 바로 아래, 흐린 글자).</summary>
    public static string Subtitle(SkillData s)
    {
        if (s == null) return "";
        string tag = TagOf(s.description);
        string kind = s.IsPermanent ? "상시" : s.IsPassive ? "패시브" : "액티브";
        string tier = s.TierLabel();
        string line = !string.IsNullOrEmpty(tag) ? tag : kind;
        if (!string.IsNullOrEmpty(tier)) line += "  ·  " + tier;
        return line;
    }

    /// <summary>본문 (핵심 수치 · 표 · 효과 · 조건). rich text — TMP 상자 하나에 넣는다.</summary>
    public static string Body(SkillData s)
    {
        if (s == null) return "";
        var sb = new StringBuilder();

        // ① 핵심 수치 (공격 스킬만) — 스톤샤드의 "23 Crushing Damage" 줄
        string key = KeyLine(s);
        if (key != null) sb.Append("<align=center><size=120%><color=").Append(Gold).Append(">").Append(key).Append("</color></size></align>\n").Append(Divider());

        // ② 표 — 왼쪽 이름, 오른쪽 값
        var rows = new List<string[]>();
        if (!s.IsPassive && !s.IsPermanent)
        {
            if (s.hpCostPercent > 0) rows.Add(new[] { "소모", C(Bad, "HP " + Num(s.hpCostPercent) + "%") });
            if (s.mpCost > 0) rows.Add(new[] { "소모", C(Mana, "MP " + s.mpCost) });
            if (s.hpCostPercent <= 0 && s.mpCost <= 0) rows.Add(new[] { "소모", C(Muted, "없음") });
            if (s.cooldownTurns > 0) rows.Add(new[] { "재사용 대기", C(Text, s.cooldownTurns + "턴") });
            if (s.usesPerBattle > 0) rows.Add(new[] { "전투당", C(Text, s.usesPerBattle + "번") });
            if (s.damageType != DamageType.None && s.minMult > 0f) rows.Add(new[] { "명중", C(Text, Num(s.accuracy * 100f) + "%") });
            if (s.preemptive) rows.Add(new[] { "행동 순서", C(Info, "누구보다 먼저") });
        }
        if (s.critBonusPercent > 0f) rows.Add(new[] { "치명타 확률", C(Good, "+" + Num(s.critBonusPercent) + "%") });
        if (s.armorBreakPercent > 0f) rows.Add(new[] { "방어구 파괴", C(Good, Num(s.armorBreakPercent * (s.armorBreakPercent < 1f ? 100f : 1f)) + "%") });
        if (!s.IsPassive && !s.IsPermanent && s.targeting != null) rows.Add(new[] { "대상", C(Text, TargetText(s)) });
        if (s.targeting != null && s.targeting.allowedCasterSlots != SlotMask.None
            && (s.targeting.allowedCasterSlots & SlotMask.Any) != SlotMask.Any) // 1~4 다 되면 제한 없음 (중앙 칸 비트가 더 켜져 있어도)
            rows.Add(new[] { "사용 위치", C(Bad, SlotText(s.targeting.allowedCasterSlots)) });
        if (s.weaponCategory != WeaponCategory.None) rows.Add(new[] { "필요 무기", C(Text, Ko.Weapon(s.weaponCategory)) });
        if (s.maxRank > 1) rows.Add(new[] { "등급", C(Text, "최대 Lv " + s.maxRank) });

        foreach (var r in rows) sb.Append(Row(r[0], r[1]));
        if (rows.Count > 0) sb.Append(Divider());

        // ③ 효과 설명 — 숫자에 색, 맨 앞 꼬리표는 위 종류 줄로 옮겼으므로 뺀다
        string desc = LeadingTag.Replace(s.description ?? "", "", 1).Trim();
        if (desc.Length > 0) sb.Append("<color=").Append(Text).Append(">").Append(ColorNumbers(desc)).Append("</color>\n");

        // ④ 조건
        if (s.prerequisiteSkills != null && s.prerequisiteSkills.Count > 0)
        {
            var names = new List<string>();
            foreach (var p in s.prerequisiteSkills) if (p != null) names.Add(p.skillName);
            if (names.Count > 0) sb.Append("\n<size=85%><color=").Append(Muted).Append(">선행: ").Append(string.Join(", ", names.ToArray())).Append("</color></size>");
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static string KeyLine(SkillData s)
    {
        if (s.IsPassive || s.IsPermanent || s.damageType == DamageType.None || s.minMult <= 0f) return null;
        string stat = StatAbbr(s.scalingStat);
        if (stat == null) return null;
        string mult = Mathf_Approx(s.minMult, s.maxMult) ? Num(s.minMult * 100f) + "%" : Num(s.minMult * 100f) + "~" + Num(s.maxMult * 100f) + "%";
        string kind = s.damageType == DamageType.Magic ? "마법 피해" : "물리 피해";
        string hits = s.hitCount > 1 ? "  ×" + s.hitCount + "회" : "";
        return stat + " " + mult + " " + kind + hits;
    }

    private static bool Mathf_Approx(float a, float b) { return System.Math.Abs(a - b) < 0.001f; }

    private static string StatAbbr(ScaleStat st)
    {
        switch (st)
        {
            case ScaleStat.Attack: return "ATK";
            case ScaleStat.Defense: return "DEF";
            case ScaleStat.Magic: return "MAG";
            case ScaleStat.Agility: return "AGI";
            case ScaleStat.Luck: return "LUK";
        }
        return null;
    }

    private static string TargetText(SkillData s)
    {
        switch (s.targeting.targetFaction)
        {
            case TargetFaction.Self: return "자신";
            case TargetFaction.Ally: return "아군";
            case TargetFaction.All: return "모두";
        }
        SlotMask t = s.targeting.allowedTargetSlots;
        if (t == SlotMask.Back) return "적 후열";
        if (t == SlotMask.Front) return "적 전열";
        return "적";
    }

    private static string SlotText(SlotMask m)
    {
        if (m == SlotMask.Front) return "전열 (1·2)";
        if (m == SlotMask.Back) return "후열 (3·4)";
        var parts = new List<string>();
        if ((m & SlotMask.Slot1) != 0) parts.Add("1");
        if ((m & SlotMask.Slot2) != 0) parts.Add("2");
        if ((m & SlotMask.Slot3) != 0) parts.Add("3");
        if ((m & SlotMask.Slot4) != 0) parts.Add("4");
        return string.Join("·", parts.ToArray()) + "번 자리";
    }

    /// <summary>설명 맨 앞 [ ] 꼬리표 내용 (없으면 null).</summary>
    public static string TagOf(string description)
    {
        if (string.IsNullOrEmpty(description)) return null;
        var m = LeadingTag.Match(description);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    // ── 모양 도우미 ──
    private static string Row(string label, string value)
    {
        return "<align=left><color=" + Muted + ">" + label + "</color><line-height=0>\n<align=right>" + value + "<line-height=1em>\n";
    }

    private static string Divider()
    {
        return "<align=center><color=#5C5040><size=70%>━━━━━━━━━━━━━━━━━━━━</size></color></align>\n";
    }

    private static string C(string color, string text) { return "<color=" + color + ">" + text + "</color>"; }

    private static string Num(float v)
    {
        return System.Math.Abs(v - System.Math.Round(v)) < 0.05f ? ((int)System.Math.Round(v)).ToString() : v.ToString("0.#");
    }

    // +숫자 → 초록, -숫자 → 빨강, ×숫자 · 그 밖의 숫자+% → 금색 (태그 안은 건드리지 않음)
    private static readonly Regex Plus = new Regex(@"(?<![\w#=])\+\d+(?:\.\d+)?%?p?");
    private static readonly Regex Minus = new Regex(@"(?<![\w#=~\-])[-−]\d+(?:\.\d+)?%?p?");
    private static readonly Regex Pct = new Regex(@"(?<![\w#=+\-−>])[×x]?\d+(?:\.\d+)?(?:~\d+(?:\.\d+)?)?%");

    public static string ColorNumbers(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') >= 0) return text; // 이미 꾸민 글은 그대로
        text = Plus.Replace(text, m => C(Good, m.Value));
        text = Minus.Replace(text, m => C(Bad, m.Value));
        text = Pct.Replace(text, m => C(Gold, m.Value));
        return text;
    }
}
