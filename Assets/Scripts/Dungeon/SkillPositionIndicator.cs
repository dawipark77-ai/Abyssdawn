using AbyssdawnBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-19] 스킬 사용 위치 · 목표 표시 (다키스트 던전의 추천 위치/추천 목표처럼).
///  - 위 줄 = 적 (빨간 불: 노릴 수 있는 칸), 아래 줄 = 아군 (노랑 불: 이 칸에서 쓸 수 있음)
///  - 사각 칸 4개에 왼쪽부터 1·2·3·4, 2와 3 사이에 전열/후열 구분선
///  - 패시브 · 상시 스킬은 숨김
/// SkillData.targeting.allowedCasterSlots / allowedTargetSlots (SlotMask: 1·2·4·8) 를 읽는다.
/// </summary>
public static class SkillPositionIndicator
{
    public const string ObjName = "PositionIndicator";
    private const float Box = 34f, Gap = 6f, Split = 14f;

    private static readonly Color AllyOn = new Color(0.95f, 0.76f, 0.30f, 1f);
    private static readonly Color EnemyOn = new Color(0.86f, 0.24f, 0.20f, 1f); // [2026-10-20] 목표 = 빨강 (초록 → 빨강)
    private static readonly Color Off = new Color(0.11f, 0.10f, 0.09f, 1f);
    private static readonly Color OffEdge = new Color(0.30f, 0.27f, 0.21f, 1f);
    private static readonly Color OnText = new Color(0.08f, 0.07f, 0.05f, 1f);
    private static readonly Color OffText = new Color(0.40f, 0.37f, 0.31f, 1f);
    private static readonly Color Label = new Color(0.70f, 0.62f, 0.43f, 1f);

    /// <summary>표시 묶음을 만든다 (이미 있으면 그대로). 위치·크기는 부르는 쪽이 정함.</summary>
    public static RectTransform Build(Transform parent, TMP_FontAsset font)
    {
        var existing = parent.Find(ObjName) as RectTransform;
        if (existing != null) return existing;
        var root = new GameObject(ObjName, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        float rowW = Box * 4 + Gap * 2 + Split;
        root.sizeDelta = new Vector2(rowW + 70f, Box * 2 + 10f);
        for (int row = 0; row < 2; row++) // 0 = 적(위), 1 = 아군(아래)
        {
            float y = row == 0 ? (Box + 10f) * 0.5f : -(Box + 10f) * 0.5f;
            var lab = NewText(root, row == 0 ? "목표" : "위치", font, 18f, Label);
            lab.alignment = TextAlignmentOptions.MidlineRight;
            Place(lab.rectTransform, new Vector2(-rowW * 0.5f + 35f - 8f, y), new Vector2(60f, Box), new Vector2(1f, 0.5f));
            for (int i = 0; i < 4; i++)
            {
                float x = -rowW * 0.5f + 35f + i * (Box + Gap) + (i >= 2 ? Split - Gap : 0f) + Box * 0.5f;
                var edge = NewImage(root, $"Edge{row}{i}", OffEdge);
                Place(edge.rectTransform, new Vector2(x, y), new Vector2(Box, Box), new Vector2(0.5f, 0.5f));
                var fill = NewImage(edge.rectTransform, "Fill", Off);
                var frt = fill.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
                var num = NewText(edge.rectTransform, (i + 1).ToString(), font, 20f, OffText);
                num.fontStyle = FontStyles.Bold; num.alignment = TextAlignmentOptions.Center;
                var nrt = num.rectTransform; nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one; nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            }
            // 전열 | 후열 구분선
            var sep = NewImage(root, $"Split{row}", Label);
            float sx = -rowW * 0.5f + 35f + 2 * (Box + Gap) + (Split - Gap) * 0.5f - Gap * 0.5f;
            Place(sep.rectTransform, new Vector2(sx, y), new Vector2(2f, Box + 6f), new Vector2(0.5f, 0.5f));
        }
        return root;
    }

    /// <summary>[2026-10-22] 한 번에 상대 전원을 치는 스킬 — 목표 칸이 전부 빨강 + 숫자 대신 "A".</summary>
    public static bool HitsAll(SkillData skill)
    {
        return skill != null && skill.Key == "Sonic Wave"; // 박쥐 음파 (BattleManager.EnemySkillSonicWave)
    }

    public static void Refresh(Transform parent, SkillData skill)
    {
        var root = parent != null ? parent.Find(ObjName) : null;
        if (root == null) return;
        bool show = skill != null && skill.usageType == UsageType.Active;
        root.gameObject.SetActive(show);
        if (!show) return;
        int caster = skill.targeting != null ? (int)skill.targeting.allowedCasterSlots & 15 : 15; // 몬스터 255(어디서나) → 15
        bool enemyTarget = skill.targeting == null || skill.targeting.targetFaction == TargetFaction.Enemy;
        int target = enemyTarget && skill.targeting != null ? (int)skill.targeting.allowedTargetSlots & 15 : (enemyTarget ? 15 : 0);
        if (caster == 0) caster = 15;
        bool all = enemyTarget && HitsAll(skill);
        if (all) target = 15;
        for (int i = 0; i < 4; i++)
        {
            var e = root.Find($"Edge0{i}");
            var t = e != null ? e.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null) t.text = all ? "A" : (i + 1).ToString();
        }
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 4; i++)
            {
                var edge = root.Find($"Edge{row}{i}");
                if (edge == null) continue;
                bool on = ((row == 0 ? target : caster) & (1 << i)) != 0;
                Color c = row == 0 ? EnemyOn : AllyOn;
                edge.GetComponent<Image>().color = on ? c : OffEdge;
                var fill = edge.Find("Fill"); if (fill != null) fill.GetComponent<Image>().color = on ? c * new Color(0.85f, 0.85f, 0.85f, 1f) : Off;
                var t = edge.GetComponentInChildren<TMP_Text>(true); if (t != null) t.color = on ? OnText : OffText;
            }
    }

    private static Image NewImage(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(Transform parent, string text, TMP_FontAsset font, float size, Color c)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = c; t.raycastTarget = false; t.enableWordWrapping = false;
        return t;
    }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
    }
}
