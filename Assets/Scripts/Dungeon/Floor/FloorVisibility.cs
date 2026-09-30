using System.Collections.Generic;
using UnityEngine;

// ※ C# 5 문법만 사용 (DungeonFloorData.cs 머리말 참고).

/// <summary>
/// 지도 공개 규칙 — 안개(시야 반경). 방이든 통로든 플레이어 주변 반경 radius 칸만 드러난다.
///  - 반경은 둥글게 (칸 거리 ≤ radius + 0.5).
///  - 벽·기둥(암반)은 시야를 가린다 (플레이어 → 칸 직선 위에 암반이 있으면 안 보임).
///  - 문 너머는 보이지 않는다: 방 안에서는 그 방 칸만, 통로에서는 통로 칸만.
///  - 반경은 MapManager 가 정한다: 기본 3, 불 있는 방·횃불 +2, 어두운 층은 1.
/// 드러난 칸은 revealed 집합에 누적되어 지도에 계속 남는다 (DungeonFloorState.revealed).
/// 지금 보이는 칸(visible)은 매 걸음 새로 계산 — 기억만 남은 칸은 자동 지도에서 흐리게 (픽셀 던전식 안개).
/// [2026-09-30] 방에 들어가면 방 전체가 보이던 방식 → 방도 시야 반경만큼만 (안개 시스템).
/// </summary>
public static class FloorVisibility
{
    /// <summary>
    /// pos 에서 지금 보이는 칸을 visible 에 채운다 (기존 내용은 지움). 지도 기억(revealed)과 달리 매 걸음 새로 계산 —
    /// 자동 지도는 보이는 칸은 또렷하게, 기억만 남은 칸은 흐리게 그린다 (픽셀 던전식).
    /// </summary>
    public static void ComputeVisible(DungeonFloorData data, Vector2Int pos, int radius, HashSet<Vector2Int> visible)
    {
        if (visible == null) return;
        visible.Clear();
        if (data == null || !data.IsWalkable(pos)) return;
        visible.Add(pos);

        int r = radius < 0 ? 0 : radius;
        float limit = (r + 0.5f) * (r + 0.5f);
        FloorRoom room = data.GetRoomAt(pos);

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (dx * dx + dy * dy > limit) continue;
                Vector2Int p = new Vector2Int(pos.x + dx, pos.y + dy);
                if (!SameZone(data, p, room)) continue;
                if (!HasLineOfSight(data, pos, p, room)) continue;
                visible.Add(p);
            }
        }
    }

    /// <summary>pos 에서 보이는 칸을 계산해 visible 에 넣고 지도 기억(revealed)에 더한다. 새로 기억된 칸이 있으면 true.</summary>
    public static bool RevealAround(DungeonFloorData data, Vector2Int pos, HashSet<Vector2Int> revealed, int radius, HashSet<Vector2Int> visible)
    {
        if (revealed == null) return false;
        if (visible == null) visible = new HashSet<Vector2Int>();
        ComputeVisible(data, pos, radius, visible);
        bool changed = false;
        foreach (Vector2Int p in visible) changed |= revealed.Add(p);
        return changed;
    }

    /// <summary>문 너머는 다른 구역: 방 안이면 같은 방 칸만, 통로면 통로 칸만.</summary>
    private static bool SameZone(DungeonFloorData data, Vector2Int p, FloorRoom room)
    {
        if (!data.IsWalkable(p)) return false;
        FloorCell c = data.GetCell(p);
        return room != null ? c.roomId == room.id : c.terrain == FloorTerrain.Corridor;
    }

    /// <summary>from → to 직선(브레젠험) 위의 중간 칸이 모두 같은 구역의 걸을 수 있는 칸이어야 보인다.</summary>
    private static bool HasLineOfSight(DungeonFloorData data, Vector2Int from, Vector2Int to, FloorRoom room)
    {
        int x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
        int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            if (x0 == x1 && y0 == y1) return true;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
            if (x0 == x1 && y0 == y1) return true;
            if (!SameZone(data, new Vector2Int(x0, y0), room)) return false;
        }
    }
}
