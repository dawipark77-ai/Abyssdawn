using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-12] 상태창(StatusPanel) 모양 — 씬 구조(기본 정보 · 스탯 · 장비 · 성격 · 종의 기억 · 스킬 칸)는 그대로 두고,
/// 처음 열릴 때 코드로 팝업창과 같은 톤으로 다시 입힌다.
///  - 바깥: 가로형 금테 패널 / 안쪽 상자: 어두운 반투명 + 옅은 금테 (회색 외곽선 제거)
///  - 머리글(Personality · Memory of Species · Passive/Active/Permanent Skills): 금색 굵은 글자
///  - 항목 이름(HP : · STR : …): 흐린 금색 / 값: 흰색
///  - 아이콘 칸: 비어 있으면 어두운 빈 칸 + 옅은 금테, 장비 이름이 없으면 회색 "Empty"
/// StatusPanel 에 붙인다.
/// </summary>
public class StatusPanelStyle : MonoBehaviour
{
    public TMP_FontAsset font;

    private static readonly Color Gold = new Color(0.96f, 0.87f, 0.58f, 1f);
    private static readonly Color GoldDim = new Color(0.72f, 0.64f, 0.45f, 1f);
    private static readonly Color FrameDim = new Color(0.72f, 0.64f, 0.45f, 0.55f);
    private static readonly Color EmptySlot = new Color(0.10f, 0.09f, 0.08f, 0.9f);
    // [2026-10-17] 드퀘식 반투명 창: 검은 반투명 + 아비스던 금테 (바깥 금선 + 안쪽 가는 금선)
    private static readonly Color BoxColor = new Color(0f, 0f, 0f, 0.85f); // [2026-10-18] 드퀘 레퍼런스처럼 검은 반투명 (남색 아님)
    private static readonly Color FrameBright = new Color(0.86f, 0.74f, 0.45f, 0.95f);
    private static readonly Color FrameInner = new Color(0.86f, 0.74f, 0.45f, 0.30f);

    private static readonly HashSet<string> BoxNames = new HashSet<string>
        { "BasicPanel", "Memory of Species", "Personality", "StatusPanel", "EquipmentPanel", "BattleSkills" };
    private static readonly HashSet<string> HeaderNames = new HashSet<string>
        { "Memory of Species", "Personality", "PassiveSkills", "ActiveSkills", "PermanentSkills" };
    private static readonly HashSet<string> EquipSlotNames = new HashSet<string>
        { "Hand1", "Hand2", "Armour", "Accessory" };

    private bool _styled;
    private Sprite _border;
    private readonly List<Image> _equipSlots = new List<Image>();
    private readonly List<TMP_Text> _equipNames = new List<TMP_Text>();

    private void OnEnable()
    {
        if (!_styled) StyleAll();
    }

    private void LateUpdate()
    {
        if (!_styled) return;
        // 장비 칸: 그림이 없으면 흰 사각형 대신 빈 칸(뒤의 어두운 판이 보이게)
        foreach (var img in _equipSlots)
        {
            if (img == null) continue;
            Color want = img.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            if (img.color != want) img.color = want;
            if (img.sprite != null) img.preserveAspect = true;
        }
        foreach (var t in _equipNames)
        {
            if (t == null) continue;
            string s = t.text.Trim();
            bool empty = s.Length == 0 || s == "Empty" || s == "New Text";
            Color want = empty ? new Color(0.5f, 0.5f, 0.52f, 1f) : Color.white;
            if (empty && s != "Empty") t.text = "Empty";
            if (t.color != want) t.color = want;
        }
        // 상시 스킬 머리글은 나중에(StatusUIHandler 가) 만들어지므로 보이면 한 번 꾸민다
        foreach (var h in GetComponentsInChildren<TMP_Text>(false))
            if (h.name == "PermanentSkills" && h.color != Gold) StyleHeader(h);
    }

    private void StyleAll()
    {
        if (font == null)
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (t != null && t.font != null && t.font.name.Contains("NotoSansKR")) { font = t.font; break; }
        _border = Resources.Load<Sprite>("UI/GlowBorder");

        // 바깥 패널 — [2026-10-17] 드퀘처럼 큰 판은 없애고 (뒤 던전이 비쳐 보임), 안쪽 상자들이 각각 반투명 창으로 떠 있게
        var root = GetComponent<Image>();
        if (root != null) { root.sprite = null; root.color = new Color(0f, 0f, 0f, 0.45f); } // 뒤를 조금 어둡게 (클릭 막기는 그대로)

        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            if (img.transform == transform) continue;
            string n = img.name;
            if (BoxNames.Contains(n) && img.GetComponent<TMP_Text>() == null) StyleBox(img);
            else if (EquipSlotNames.Contains(n) && img.transform.parent != null && img.transform.parent.name == "EquipmentPanel") StyleEquipSlot(img);
            else if (IsIconSlot(img)) StyleIconSlot(img);
        }

        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.GetComponentInParent<Button>(true) != null && t.transform.parent != null && t.transform.parent.GetComponent<Button>() != null) continue;
            if (HeaderNames.Contains(t.name)) { StyleHeader(t); continue; }
            if (t.name == "Traits_text" || (t.name == "Text" && t.transform.parent != null && t.transform.parent.name == "Personality"))
            { SetFont(t); t.fontStyle = FontStyles.Bold; t.color = Color.white; t.fontSize = Mathf.Min(t.fontSize, 26f); continue; }
            string s = t.text.Trim();
            if (IsLabel(s)) StyleLabel(t);
        }
        _styled = true;
    }

    private static bool IsLabel(string s)
    {
        if (s.Length == 0 || s.Length > 12) return false;
        return s.EndsWith(":") || s.Contains(":/") || s == "Lv." || s.StartsWith("HP :") || s.StartsWith("MP :");
    }

    /// <summary>스킬·종의 기억·성격·특성 아이콘 칸 (Slot1~6, Traits_Image).</summary>
    private static bool IsIconSlot(Image img)
    {
        string n = img.name;
        if (n == "Traits_Image") return true;
        if (!n.StartsWith("Slot") || img.transform.parent == null) return false;
        string p = img.transform.parent.name;
        return p == "PassiveSkills" || p == "ActiveSkills" || p == "Memory of Species" || p == "Personality";
    }

    private void SetFont(TMP_Text t)
    {
        if (t != null && font != null) t.font = font;
    }

    private void StyleBox(Image box)
    {
        box.sprite = null;
        box.color = BoxColor;
        var outline = box.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
        AddFrame(box.transform, "BoxFrame", FrameBright, 3f);     // 바깥 금선 (또렷하게)
        AddFrame(box.transform, "BoxFrameInner", FrameInner, -6f); // 안쪽 가는 금선 (이중 테두리)
    }

    private void StyleHeader(TMP_Text t)
    {
        SetFont(t);
        t.fontSize = Mathf.Min(t.fontSize, 32f);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 2f;
        t.color = Gold;
        t.enableWordWrapping = false;
    }

    private void StyleLabel(TMP_Text t)
    {
        SetFont(t);
        t.fontStyle = FontStyles.Bold;
        t.fontSize = Mathf.Min(t.fontSize, 32f);
        t.color = GoldDim;
        // 바로 아래 자식 글자 = 값
        foreach (Transform c in t.transform)
        {
            var v = c.GetComponent<TMP_Text>();
            if (v == null) continue;
            SetFont(v);
            v.color = Color.white;
            v.fontStyle = FontStyles.Bold;
            v.fontSize = Mathf.Min(v.fontSize, 32f);
        }
    }

    private void StyleEquipSlot(Image slot)
    {
        AddBack(slot);
        _equipSlots.Add(slot);
        var name = slot.GetComponentInChildren<TMP_Text>(true);
        if (name != null)
        {
            SetFont(name);
            name.fontSize = 32f;
            name.fontStyle = FontStyles.Bold;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            _equipNames.Add(name);
        }
    }

    private void StyleIconSlot(Image slot)
    {
        AddBack(slot);
    }

    /// <summary>칸 뒤에 어두운 판 + 옅은 금테 (칸 그림이 비어 있어도 자리가 보이게).</summary>
    private void AddBack(Image slot)
    {
        var outline = slot.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
        Transform parent = slot.transform.parent;
        string backName = slot.name + "_Back" + slot.transform.GetSiblingIndex(); // 같은 이름 칸(장신구 2개)도 따로
        if (parent.Find(backName) != null) return;
        var go = new GameObject(backName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var srt = (RectTransform)slot.transform;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = srt.anchorMin; rt.anchorMax = srt.anchorMax; rt.pivot = srt.pivot;
        rt.anchoredPosition = srt.anchoredPosition; rt.sizeDelta = srt.sizeDelta;
        rt.localScale = srt.localScale;
        go.transform.SetSiblingIndex(slot.transform.GetSiblingIndex()); // 칸 바로 뒤
        var img = go.AddComponent<Image>();
        img.color = EmptySlot;
        img.raycastTarget = false;
        AddFrame(go.transform, "SlotFrame", FrameDim, 2f);
    }

    private void AddFrame(Transform parent, string name, Color color, float outset)
    {
        if (_border == null || parent.Find(name) != null) return;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        var img = go.AddComponent<Image>();
        img.sprite = _border; img.type = Image.Type.Sliced; img.fillCenter = false;
        img.color = color; img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset); rt.offsetMax = new Vector2(outset, outset);
    }
}
