using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [2026-10-15] 씬에 박혀 있는 영어 글자(버튼·제목·스탯 칸)를 씬이 열릴 때 한국어로 바꾼다.
///  - 글자가 표와 정확히 같을 때만 바꾼다 (공백·줄바꿈은 무시하고 비교). 꺼져 있는 오브젝트도 포함
///  - 코드가 나중에 넣는 글자는 각 코드에서 한국어로 넣는다 — 여기는 씬에 저장된 글자만
///  - 스킬 트리 노드에 박힌 스킬 이름도 SkillData 의 고정 이름(Key) → 표시 이름으로 바꾼다
/// </summary>
public static class SceneLabelsKo
{
    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
    {
        // 전투 명령
        { "Attack", "공격" }, { "Skill", "스킬" }, { "Item", "아이템" }, { "Defend", "방어" }, { "Flee", "도망" },
        { "Move", "이동" }, { "Back", "뒤로" }, { "YES", "예" }, { "NO", "아니오" }, { "Skill List", "스킬 목록" },
        { "Item List", "아이템 목록" }, { "Chalice Of Dawn", "새벽의 잔" },
        // 스탯 칸
        // (스탯 이름 ATK·DEF·MAG·AGI·LUK·STR 은 영문 그대로 — 2026-10-17)
        { "Name :", "이름 :" }, { "Class:", "직업" }, { "EXP :", "EXP :" }, { "Skill Point", "스킬 포인트" },
        // 던전 메뉴 · 창
        { "ITEM", "아이템" }, { "ITEMS", "아이템" }, { "Items", "아이템" }, { "ITEM LIST", "아이템 목록" }, { "INVENTORY", "소지품" },
        { "SKILLS", "스킬" }, { "SKILL", "스킬" }, { "SKILL SET", "스킬 세트" }, { "STATUS", "상태" }, { "EQUIP", "장착" },
        { "Oath & Path", "서약 & 패스" }, { "OATH & BINDING", "서약 & 제약" }, { "Oath", "서약" }, { "Binding", "제약" },
        { "BATTEL SET", "전투 설정" }, { "BATTLE SET", "전투 설정" }, { "CAMP", "야영" }, { "Make Camp", "야영" },
        { "CLOSE", "닫기" }, { "CANCEL", "취소" }, { "Leave", "떠나기" }, { "Discard", "버리기" }, { "Learn", "기억하기" }, { "Release", "놓아주기" },
        { "Position", "자리" }, { "Forward", "앞으로" }, { "Left", "왼쪽" }, { "Right", "오른쪽" }, { "L Side", "왼쪽" }, { "R Side", "오른쪽" },
        { "Personality", "성향" }, { "Memory of Species", "종의 기억" }, { "ENTROPY", "엔트로피" }, { "Tactic", "전술" }, { "ECT", "기타" },
        { "Active Skills", "액티브 스킬" }, { "ACTIVE SKILLS", "액티브 스킬" }, { "Passive Skills", "패시브 스킬" }, { "Physical / Active", "물리 / 액티브" },
        { "Item Name", "아이템 이름" }, { "Skill Name", "스킬 이름" }, { "ALL", "전체" },
        // Lore 트리 분류
        // [2026-10-18] WEAPONARY · DISCIPLINE · Arcane Mystery 큰 분류 탭은 영어 그대로 (사용자 지시)
        { "UTILITY", "보조학" },
        { "WARFARE", "전쟁학" }, { "Warfare", "전쟁학" }, { "COMBAT ARTS", "전투학" }, { "Combat Arts", "전투학" },
        { "DEVINE", "신성" }, { "FORMATION", "진형" }, { "MOMENTUM", "기세" }, { "DECEPTION", "기만" }, { "Instinct", "본능" },
        { "EXPEDITION", "탐험" }, { "Expedition", "탐험" }, { "QUARTERMASTERY", "보급학" }, { "Lore", "로어" },
        { "Swords", "검" }, { "Sword Lore", "검술학" }, { "Daggers", "단검" }, { "Dagger Lore", "단검학" }, { "Axes", "도끼" }, { "Axe Lore", "도끼학" },
        { "Spears", "창" }, { "Maces", "철퇴" }, { "Shields", "방패" }, { "Shield Lore", "방패학" }, { "Martial", "격투" }, { "Martial Arts", "격투학" },
        { "Two_Handed Swords", "양손 검" }, { "Two_Handed Maces", "양손 철퇴" }, { "Two_Handed Axes", "양손 도끼" },
        { "Bow", "활" }, { "Crossbow", "석궁" }, { "Ranged Weapons", "원거리 무기" },
        // 타이틀
        { "TAP TO BEGIN", "눌러서 시작" },
    };

    private static Dictionary<string, string> _norm;
    private static readonly Regex Ws = new Regex(@"\s+");
    private static string Norm(string s) => Ws.Replace(s ?? "", " ").Trim().TrimEnd(':').Trim();

    private static Dictionary<string, string> Table()
    {
        if (_norm != null) return _norm;
        _norm = new Dictionary<string, string>();
        foreach (var kv in Map) _norm[Norm(kv.Key)] = kv.Value;
        // 스킬 이름 (트리 노드 등 씬에 박힌 이름)
        foreach (var s in Resources.LoadAll<AbyssdawnBattle.SkillData>(""))
            if (s != null && !string.IsNullOrEmpty(s.skillKey) && s.skillKey != s.skillName && !_norm.ContainsKey(Norm(s.skillKey)))
                _norm[Norm(s.skillKey)] = s.skillName;
        return _norm;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

    /// <summary>scene 안의 모든 글자(꺼진 것 포함)를 표대로 바꾼다.</summary>
    public static void Apply(Scene scene)
    {
        var t = Table();
        foreach (var tmp in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (tmp == null || tmp.gameObject.scene != scene) continue;
            string ko = Translate(tmp.text, t);
            if (ko == null) continue;
            tmp.text = ko;
            // 한글은 영어보다 넓다 — 버튼 칸을 넘으면 두 줄로 꺾이지 않고 글자가 줄어들게
            if (!tmp.enableAutoSizing)
            {
                tmp.fontSizeMax = tmp.fontSize;
                tmp.fontSizeMin = Mathf.Max(10f, tmp.fontSize * 0.45f);
                tmp.enableAutoSizing = true;
            }
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            // 버튼 안 글자면 글자 칸을 버튼 크기(안쪽 여백 6)에 맞춘다 — 칸이 버튼보다 넓으면 자동 줄임이 안 걸려 글자가 넘친다
            var btn = tmp.GetComponentInParent<UnityEngine.UI.Button>(true);
            if (btn != null && btn.transform != tmp.transform)
            {
                var rt = tmp.rectTransform;
                var parentRt = (RectTransform)btn.transform;
                var c = new Vector3[4]; parentRt.GetWorldCorners(c);
                var mine = new Vector3[4]; rt.GetWorldCorners(mine);
                float pw = c[2].x - c[0].x, mw = mine[2].x - mine[0].x;
                if (mw >= pw - 20f && rt.parent == parentRt) // 글자 칸이 버튼 폭과 거의 같으면 안쪽 여백을 둔다
                {
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = new Vector2(10f, 4f); rt.offsetMax = new Vector2(-10f, -4f);
                }
                else if (mw >= pw - 20f) // 버튼 아래 한 단계 더 들어간 글자 — 폭만 줄인다
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x - 20f, rt.sizeDelta.y);
                tmp.ForceMeshUpdate();
            }
        }
        foreach (var ui in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>())
        {
            if (ui == null || ui.gameObject.scene != scene) continue;
            string ko = Translate(ui.text, t);
            if (ko != null) ui.text = ko;
        }
    }

    private static string Translate(string text, Dictionary<string, string> t)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string n = Norm(text);
        string ko;
        if (!t.TryGetValue(n, out ko)) return null;
        string trimmed = text.TrimEnd();
        if (trimmed.EndsWith(":")) ko += " :";   // "ATK :" 처럼 끝의 콜론은 살린다
        return ko.EndsWith(" : :") ? ko.Substring(0, ko.Length - 2) : ko;
    }

    /// <summary>이 글자가 표에서 en 의 한국어(또는 en 그대로)인가 — 글자로 버튼을 찾는 코드용.</summary>
    public static bool Is(string label, string en)
    {
        if (label == null) return false;
        string n = Norm(label);
        if (n == Norm(en)) return true;
        string ko;
        return Map.TryGetValue(en, out ko) && n == Norm(ko);
    }
}
