using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AbyssdawnBattle;

/// <summary>
/// 스킬 트리 노드 사이 연결선. 선행 스킬(SkillData.prerequisiteSkills) 노드 → 다음 노드로 선을 긋는다.
/// 실행 시 트리(예: SwordLore) 안에 "Links" 묶음을 만들어 노드 뒤에 그린다 — 씬에 저장되지 않음.
/// 위치는 Refresh 때마다 노드의 실제 위치로 다시 계산하므로 노드를 옮겨도 선이 따라간다.
///   - 둘 다 배움          : 옅은 황금색 + 은은한 광채
///   - 선행만 배움(해금됨)  : 밝은 흰색
///   - 잠김               : 어두운 회색
/// SwordSkillTreeManager 가 붙이고 노드 상태가 바뀔 때마다 Refresh 한다.
/// </summary>
public class SkillTreeLinks : MonoBehaviour
{
    [Header("선 모양")]
    public float thickness = 6f;
    public float glowThickness = 18f;

    [Header("색")]
    public Color learnedColor = new Color(0.96f, 0.87f, 0.58f, 1f);
    public Color learnedGlowColor = new Color(0.96f, 0.82f, 0.45f, 0.28f);
    public Color availableColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);
    public Color availableGlowColor = new Color(1f, 1f, 1f, 0.12f);
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);

    private RectTransform _root;
    private readonly List<Image> _pool = new List<Image>();
    private int _used;

    public void Refresh(SkillTreeNode[] nodes)
    {
        if (nodes == null) return;
        EnsureRoot();
        _used = 0;

        var bySkill = new Dictionary<SkillData, SkillTreeNode>();
        foreach (var n in nodes)
            if (n != null && n.skillData != null && !bySkill.ContainsKey(n.skillData)) bySkill[n.skillData] = n;

        foreach (var child in nodes)
        {
            if (child == null || child.skillData == null || child.skillData.prerequisiteSkills == null) continue;
            foreach (var pre in child.skillData.prerequisiteSkills)
            {
                SkillTreeNode parent;
                if (pre == null || !bySkill.TryGetValue(pre, out parent)) continue;
                bool parentLearned = parent.GetState() == SkillTreeNode.SkillState.Learned;
                bool childLearned = child.GetState() == SkillTreeNode.SkillState.Learned;
                Vector2 a = LocalCenter(parent.transform as RectTransform);
                Vector2 b = LocalCenter(child.transform as RectTransform);
                if (parentLearned && childLearned)
                {
                    DrawLine(a, b, glowThickness, learnedGlowColor);
                    DrawLine(a, b, thickness, learnedColor);
                }
                else if (parentLearned)
                {
                    DrawLine(a, b, glowThickness, availableGlowColor);
                    DrawLine(a, b, thickness, availableColor);
                }
                else
                {
                    DrawLine(a, b, thickness, lockedColor);
                }
            }
        }

        for (int i = _used; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
    }

    private void EnsureRoot()
    {
        if (_root != null) return;
        var go = new GameObject("Links", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _root = (RectTransform)go.transform;
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;
        _root.sizeDelta = Vector2.zero;
        _root.SetAsFirstSibling(); // 노드 뒤에 그려지도록
    }

    private Vector2 LocalCenter(RectTransform rt)
    {
        if (rt == null) return Vector2.zero;
        Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
        return _root.InverseTransformPoint(worldCenter);
    }

    private void DrawLine(Vector2 a, Vector2 b, float width, Color color)
    {
        Image img;
        if (_used < _pool.Count) img = _pool[_used];
        else
        {
            var go = new GameObject("Link", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            img = go.AddComponent<Image>();
            img.raycastTarget = false; // 노드 클릭을 막지 않도록
            _pool.Add(img);
        }
        _used++;
        img.gameObject.SetActive(true);
        img.color = color;

        Vector2 d = b - a;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a;
        rt.sizeDelta = new Vector2(d.magnitude, width);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }
}
