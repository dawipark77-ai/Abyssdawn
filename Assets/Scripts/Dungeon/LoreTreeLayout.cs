using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

/// <summary>
/// [2026-10-19] 스킬 트리 자동 배치 — 씬에 손으로 놓은 위치 대신 스킬 데이터(선행 스킬)로 줄·칸을 정한다.
///  - 선행이 없는 스킬 = 맨 위 줄(시작 스킬). 자식은 부모 바로 아래 줄, 부모는 자식들 가운데 위 (스톤샤드처럼 사용·패시브가 섞임)
///  - 서로 이어지지 않은 묶음은 왼쪽부터 채우고, 한 줄이 MaxCols 칸을 넘으면 아래로 내려 쌓음
///  - 노드 크기는 칸 크기에 맞춰 모두 같게
/// 순서는 씬에 놓였던 가로 위치(x) 순서를 따름 — 작가가 정한 좌우 순서를 지킴.
/// </summary>
public static class LoreTreeLayout
{
    public const int MaxCols = 7; // [2026-10-20] 4 → 6 → 7 (검술: 강베기·역베기·만드리토 한 줄 → 7칸)
    private const float TitleSpace = 120f;  // 위쪽 제목 자리
    private const float BottomSpace = 40f;
    private const float SideMargin = 30f;
    private const float MaxCellW = 250f, MaxCellH = 250f;
    public const float MaxIcon = 140f;

    private class Block { public SkillTreeNode root; public float width; public int depth; }

    public static void Apply(Transform page)
    {
        var nodes = new List<SkillTreeNode>();
        foreach (var n in page.GetComponentsInChildren<SkillTreeNode>(true))
            if (n != null && n.skillData != null && !n.IsDuplicateNode()) nodes.Add(n);
        if (nodes.Count == 0) return;
        var container = nodes[0].transform.parent as RectTransform;
        nodes.RemoveAll(n => n.transform.parent != container);

        var by = new Dictionary<SkillData, SkillTreeNode>();
        foreach (var n in nodes) if (!by.ContainsKey(n.skillData)) by[n.skillData] = n;
        System.Func<SkillTreeNode, List<SkillTreeNode>> parentsOf = n =>
        {
            var l = new List<SkillTreeNode>();
            if (n.skillData.prerequisiteSkills != null)
                foreach (var p in n.skillData.prerequisiteSkills) { SkillTreeNode pn; if (p != null && by.TryGetValue(p, out pn) && pn != n) l.Add(pn); }
            return l;
        };

        // 깊이 (선행 사슬 길이)
        var depth = new Dictionary<SkillTreeNode, int>();
        System.Func<SkillTreeNode, int, int> D = null;
        D = (n, guard) =>
        {
            int d; if (depth.TryGetValue(n, out d)) return d;
            if (guard > 20) return 0;
            d = 0; foreach (var p in parentsOf(n)) d = Mathf.Max(d, D(p, guard + 1) + 1);
            depth[n] = d; return d;
        };
        foreach (var n in nodes) D(n, 0);

        // 자식은 가장 깊은(바로 위) 부모 아래에 붙인다
        var children = new Dictionary<SkillTreeNode, List<SkillTreeNode>>();
        var roots = new List<SkillTreeNode>();
        foreach (var n in nodes)
        {
            var ps = parentsOf(n);
            if (ps.Count == 0) { roots.Add(n); continue; }
            SkillTreeNode best = ps[0];
            foreach (var p in ps) if (depth[p] > depth[best]) best = p;
            List<SkillTreeNode> l; if (!children.TryGetValue(best, out l)) { l = new List<SkillTreeNode>(); children[best] = l; }
            l.Add(n);
        }
        System.Comparison<SkillTreeNode> byX = (a, b) => ((RectTransform)a.transform).anchoredPosition.x.CompareTo(((RectTransform)b.transform).anchoredPosition.x);
        System.Comparison<SkillTreeNode> byYX = (a, b) =>
        {
            float ay = ((RectTransform)a.transform).anchoredPosition.y, by2 = ((RectTransform)b.transform).anchoredPosition.y;
            if (Mathf.Abs(ay - by2) > 40f) return by2.CompareTo(ay); // 위에 있던 것 먼저
            return byX(a, b);
        };
        roots.Sort(byYX);
        foreach (var kv in children) kv.Value.Sort(byX);

        // 묶음(뿌리 하나의 자손 전체)마다 상대 칸 계산
        var col = new Dictionary<SkillTreeNode, float>();
        var blocks = new List<Block>();
        foreach (var r in roots)
        {
            float next = 0f; int maxD = 0;
            System.Action<SkillTreeNode> place = null;
            place = n =>
            {
                maxD = Mathf.Max(maxD, depth[n] - depth[r]);
                List<SkillTreeNode> ch;
                if (!children.TryGetValue(n, out ch) || ch.Count == 0) { col[n] = next; next += 1f; return; }
                foreach (var c in ch) place(c);
                col[n] = (col[ch[0]] + col[ch[ch.Count - 1]]) * 0.5f;
            };
            place(r);
            blocks.Add(new Block { root = r, width = Mathf.Max(1f, next), depth = maxD + 1 });
        }

        // 묶음을 줄(띠)에 채우기
        int maxCols = MaxCols;
        foreach (var b in blocks) maxCols = Mathf.Max(maxCols, Mathf.CeilToInt(b.width));
        var bandOf = new Dictionary<Block, int>(); var offsetOf = new Dictionary<Block, float>();
        var bandRows = new List<int>(); var bandWidth = new List<float>();
        int band = 0; float used = 0f; bandRows.Add(0); bandWidth.Add(0f);
        foreach (var b in blocks)
        {
            if (used > 0f && used + b.width > maxCols) { band++; used = 0f; bandRows.Add(0); bandWidth.Add(0f); }
            bandOf[b] = band; offsetOf[b] = used; used += b.width;
            bandRows[band] = Mathf.Max(bandRows[band], b.depth); bandWidth[band] = used;
        }
        int totalRows = 0; var bandStartRow = new List<int>();
        foreach (var r in bandRows) { bandStartRow.Add(totalRows); totalRows += r; }

        // 크기
        Rect area = container.rect;
        float W = area.width - SideMargin * 2f, H = area.height - TitleSpace - BottomSpace;
        float cellW = Mathf.Min(MaxCellW, W / maxCols);
        float cellH = Mathf.Min(MaxCellH, H / Mathf.Max(1, totalRows));
        float icon = Mathf.Min(MaxIcon, cellW * 0.58f, cellH * 0.52f);
        float top = area.height * 0.5f - TitleSpace; // 가운데 기준 좌표 (앵커 0.5)

        foreach (var b in blocks)
        {
            int bi = bandOf[b];
            float bandLeft = -bandWidth[bi] * cellW * 0.5f; // 띠마다 가운데 정렬
            System.Action<SkillTreeNode> put = null;
            put = n =>
            {
                var rt = (RectTransform)n.transform;
                int row = bandStartRow[bi] + depth[n] - depth[b.root];
                float x = bandLeft + (offsetOf[b] + col[n] + 0.5f) * cellW;
                float y = top - (row + 0.42f) * cellH;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(x, y);
                float baseW = Mathf.Max(1f, rt.rect.width);
                rt.localScale = Vector3.one * (icon / baseW);
                List<SkillTreeNode> ch;
                if (children.TryGetValue(n, out ch)) foreach (var c in ch) put(c);
            };
            put(b.root);
        }
    }
}
