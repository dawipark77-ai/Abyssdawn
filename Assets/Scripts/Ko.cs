using System.Text.RegularExpressions;

/// <summary>
/// [2026-10-15] 한국어 표시 도우미.
///  - 조사: 이름 끝 글자의 받침에 맞춰 이/가 · 은/는 · 을/를 · 과/와 · 으로/로 를 붙인다 (서식 태그는 무시)
///  - 상태이상 · 무기 종류 같은 enum 이름을 한국어로
/// </summary>
public static class Ko
{
    private static readonly Regex Tag = new Regex("<[^>]*>");

    /// <summary>받침 있음 = true, 없음 = false, 한글이 아니면 null.</summary>
    private static bool? Batchim(string word, out bool rieul)
    {
        rieul = false;
        if (string.IsNullOrEmpty(word)) return null;
        string s = Tag.Replace(word, "").TrimEnd(' ', '!', '.', ')', ']');
        if (s.Length == 0) return null;
        char c = s[s.Length - 1];
        if (c >= 0xAC00 && c <= 0xD7A3)
        {
            int jong = (c - 0xAC00) % 28;
            rieul = jong == 8;
            return jong != 0;
        }
        if (c >= '0' && c <= '9') { int d = c - '0'; rieul = d == 1 || d == 7 || d == 8; return d == 0 || d == 1 || d == 3 || d == 6 || d == 7 || d == 8; }
        return null;
    }

    private static string J(string word, string withB, string without, string unknown)
    {
        bool r; var b = Batchim(word, out r);
        return word + (b == null ? unknown : b.Value ? withB : without);
    }

    public static string I(string w) => J(w, "이", "가", "이(가)");
    public static string Eun(string w) => J(w, "은", "는", "은(는)");
    public static string Eul(string w) => J(w, "을", "를", "을(를)");
    public static string Gwa(string w) => J(w, "과", "와", "과(와)");
    public static string Ui(string w) => w + "의";
    public static string Ro(string w)
    {
        bool r; var b = Batchim(w, out r);
        if (b == null) return w + "(으)로";
        return w + (b.Value && !r ? "으로" : "로");
    }

    /// <summary>상태이상 이름 (enum · variantId 모두). 모르는 것은 그대로.</summary>
    public static string Status(object v)
    {
        if (v == null) return "";
        string s = v.ToString();
        if (s.StartsWith("Stun")) return "기절";
        if (s.StartsWith("Bleed")) return "출혈";
        if (s.StartsWith("Poison")) return "독";
        if (s.StartsWith("Ignite") || s.StartsWith("Burn")) return "점화";
        if (s.StartsWith("Weakness")) return "약화";
        if (s.StartsWith("Slow")) return "둔화";
        if (s.StartsWith("Silence")) return "침묵";
        if (s.StartsWith("Blind")) return "실명";
        if (s.StartsWith("Enrage")) return "분노";
        if (s.StartsWith("Curse")) return "저주";
        return s;
    }

    /// <summary>[2026-10-17] 직업 이름 (CharacterClass.className 은 영어 키로 유지).</summary>
    public static string Job(string className)
    {
        switch (className)
        {
            case "Warrior": return "전사";
            case "Thief": case "Rogue": return "도적";
            case "Monk": return "몽크";
            case "Wizard": case "Mage": return "마법사";
            case "Sister": return "수녀";
            case "None": case "": case null: return "없음";
        }
        return className;
    }

    /// <summary>무기 종류 enum 이름.</summary>
    public static string Weapon(object v)
    {
        if (v == null) return "";
        switch (v.ToString())
        {
            case "Sword": return "검";
            case "Dagger": return "단검";
            case "Bow": return "활";
            case "Katana": return "도";
            case "Spear": return "창";
            case "Polearm": return "폴암";
            case "Axe": return "도끼";
            case "Hammer": return "망치";
            case "Mace": return "철퇴";
            case "Staff": return "지팡이";
            case "Wand": return "완드";
            case "Shield": return "방패";
            case "Crossbow": return "석궁";
            case "Greatsword": return "대검";
            case "Unarmed": case "Fist": return "맨손";
            case "Dual": return "쌍수";
            case "None": case "Any": return "무기";
        }
        return v.ToString();
    }

    /// <summary>스탯 수정치 종류 이름 (버프 로그).</summary>
    public static string Stat(object v)
    {
        if (v == null) return "";
        switch (v.ToString())
        {
            case "Attack": return "ATK";
            case "Defense": return "DEF";
            case "Magic": return "MAG";
            case "Agility": case "Speed": return "AGI";
            case "Luck": return "LUK";
            case "Accuracy": return "명중";
            case "Evasion": return "회피";
            case "CritChance": return "치명타 확률";
            case "CritDamage": return "치명타 피해";
            case "ArmorPen": return "방어 관통";
            case "StatusResist": return "상태이상 저항";
            case "DamageTaken": return "받는 피해";
            case "HealingReceived": return "받는 회복";
            case "MaxHP": return "최대 HP";
            case "MaxMP": return "최대 MP";
        }
        return v.ToString();
    }
}
