using System.Collections.Generic;
using UnityEngine;

// ※ C# 5 문법만 사용 (DungeonFloorData.cs 머리말 참고).

/// <summary>
/// 지도 공개 규칙 (로그 / 이상한 던전 표준). 문 너머는 보이지 않는다.
///  - 방 안에 서면: 그 방 전체만 드러난다. 문은 문 막대로만 표시되고, 문 바깥 통로는 나가 봐야 보인다.
///  - 통로에 서면: 주변 한 칸(대각선 포함)의 통로 칸만 드러난다. 문 앞에 서도 방 안은 들어가야 보인다.
/// 드러난 칸은 revealed 집합에 누적되어 지도에 계속 남는다 (DungeonPersistentData.revealedTiles).
/// [2026-09-28] 방에 들어가면 문 바깥 통로 칸까지, 문 앞 통로에 서면 방 가장자리까지 보이던 것을 막음.
/// </summary>
public static class FloorVisibility
{
    /// <summary>pos 에 섰을 때 새로 드러난 칸이 하나라도 있으면 true.</summary>
    public static bool RevealAround(DungeonFloorData data, Vector2Int pos, HashSet<Vector2Int> revealed)
    {
        if (data == null || revealed == null) return false;
        bool changed = false;

        FloorRoom room = data.GetRoomAt(pos);
        if (room != null)
        {
            // 방 영역 안이라도 깎인 모서리·기둥(암반)은 제외 — 방 칸만
            for (int x = room.bounds.x; x < room.bounds.xMax; x++)
                for (int y = room.bounds.y; y < room.bounds.yMax; y++)
                    if (data.cells[x, y].roomId == room.id) changed |= revealed.Add(new Vector2Int(x, y));
            return changed;
        }

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int p = new Vector2Int(pos.x + dx, pos.y + dy);
                if (data.IsWalkable(p) && data.GetCell(p).terrain == FloorTerrain.Corridor)
                    changed |= revealed.Add(p);
            }
        }
        return changed;
    }
}
