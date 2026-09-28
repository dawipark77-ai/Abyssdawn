using System;
using System.Collections.Generic;
using UnityEngine;

// ※ C# 5 문법만 사용 (DungeonFloorData.cs 머리말 참고).

/// <summary>
/// 로그(Rogue) / 이상한 던전 계열 표준 방식의 층 생성기.
///
///  1. 층을 구획 격자(열 × 행)로 나눈다.
///  2. 구획마다 랜덤 크기의 방을 하나 둔다. 일부는 "빈 구획" — 방 없이 통로 교차점 1칸만.
///  3. 연결 설계도(그래프)를 먼저 만든다: 구획 = 점, 상하좌우 이웃 = 선.
///     모든 구획이 이어지는 신장 트리 → 여분 연결 몇 개 추가(돌아가는 길).
///  4. 설계도의 선마다 마주 보는 방 벽에 문을 정하고, 문에서 문까지 폭 1칸 통로를 한 번 꺾어 판다.
///     꺾는 지점은 두 방 사이 틈에서만 고르므로 다른 방을 관통하지 않고, 방 옆면에 달라붙지도 않는다.
///  5. 남은 공간은 암반으로 둔다 (미로로 채우지 않음).
///  6. 시작 위치와 계단(시작 방이 아닌 랜덤 방)을 방 안에 둔다. 마을 층이면 마을 입구도 방 안에 둔다.
///  7. 시작점에서 모든 방·계단·마을 입구까지 갈 수 있는지 검증. 실패하면 시드 +1 로 재시도(결정론 유지).
///
/// 전용 난수기(System.Random)를 쓰므로 Unity 전역 난수(인카운터·전투)와 섞이지 않는다.
/// 같은 (층, 시드, 설정)이면 항상 같은 층이 나온다 — 전투 후 복귀 시 그대로 재생성.
/// </summary>
public static class RogueFloorGenerator
{
    private const int MaxAttempts = 10;

    // 방·교차점이 놓일 수 있는 구획 안쪽 범위: 왼쪽/위 1칸, 오른쪽/아래 2칸을 비운다.
    // → 이웃한 두 방 사이에 최소 3칸 틈이 생겨, 통로를 꺾어도 방 옆면에 닿지 않는다.
    private const int MarginLow = 1;
    private const int MarginHigh = 2;

    public static DungeonFloorData Generate(int floorNumber, int seed, FloorTableEntry settings)
    {
        if (settings == null) settings = FloorTableDefaults.Find(null, floorNumber);

        DungeonFloorData last = null;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int attemptSeed = unchecked(seed + attempt);
            DungeonFloorData data = TryGenerate(floorNumber, seed, attemptSeed, settings);
            string error;
            if (Validate(data, out error)) return data;

            Debug.LogWarning("[RogueFloorGenerator] B" + floorNumber + " seed " + attemptSeed + " 검증 실패: " + error + " → 재시도");
            last = data;
        }
        Debug.LogError("[RogueFloorGenerator] B" + floorNumber + " 생성 " + MaxAttempts + "회 모두 검증 실패 — 마지막 결과 사용");
        return last;
    }

    // ─────────────────────────────────────────────
    // 생성
    // ─────────────────────────────────────────────

    private static DungeonFloorData TryGenerate(int floorNumber, int seed, int attemptSeed, FloorTableEntry cfg)
    {
        System.Random rng = new System.Random(attemptSeed);

        int cols = Math.Max(1, cfg.sectionCols);
        int rows = Math.Max(1, cfg.sectionRows);
        int minRoomW = Math.Max(1, cfg.minRoomWidth);
        int minRoomH = Math.Max(1, cfg.minRoomHeight);
        int secW = Math.Max(cfg.sectionWidth, minRoomW + MarginLow + MarginHigh);
        int secH = Math.Max(cfg.sectionHeight, minRoomH + MarginLow + MarginHigh);
        int total = cols * rows;

        DungeonFloorData data = new DungeonFloorData(floorNumber, seed, cfg.floorType, cols * secW, rows * secH);
        data.generatedSeed = attemptSeed;

        // ── 1~2. 빈 구획 결정 (방은 최소 2개: 시작 방 + 계단 방) ──
        int maxGone = Clamp(cfg.maxGoneSections, 0, Math.Max(0, total - 2));
        int minGone = Clamp(cfg.minGoneSections, 0, maxGone);
        int goneCount = rng.Next(minGone, maxGone + 1);
        bool[] gone = new bool[total];
        List<int> order = Range(total);
        Shuffle(order, rng);
        for (int i = 0; i < goneCount; i++) gone[order[i]] = true;

        // ── 2. 구획마다 방 또는 교차점 ──
        FloorRoom[] sectionRooms = new FloorRoom[total];
        for (int sy = 0; sy < rows; sy++)
        {
            for (int sx = 0; sx < cols; sx++)
            {
                int idx = sy * cols + sx;
                int innerX = sx * secW + MarginLow;
                int innerY = sy * secH + MarginLow;
                int innerW = secW - MarginLow - MarginHigh;
                int innerH = secH - MarginLow - MarginHigh;

                FloorRoom room = new FloorRoom();
                room.id = data.rooms.Count;
                room.section = new Vector2Int(sx, sy);
                room.isGone = gone[idx];

                if (room.isGone)
                {
                    int px = innerX + rng.Next(innerW);
                    int py = innerY + rng.Next(innerH);
                    room.bounds = new RectInt(px, py, 1, 1);
                    SetCorridor(data, px, py);
                }
                else
                {
                    int w = rng.Next(Math.Min(minRoomW, innerW), innerW + 1);
                    int h = rng.Next(Math.Min(minRoomH, innerH), innerH + 1);
                    int x = innerX + rng.Next(innerW - w + 1);
                    int y = innerY + rng.Next(innerH - h + 1);
                    room.bounds = new RectInt(x, y, w, h);
                    for (int cx = x; cx < x + w; cx++)
                    {
                        for (int cy = y; cy < y + h; cy++)
                        {
                            data.cells[cx, cy].terrain = FloorTerrain.Room;
                            data.cells[cx, cy].roomId = room.id;
                        }
                    }
                }

                data.rooms.Add(room);
                sectionRooms[idx] = room;
            }
        }

        // ── 3. 연결 설계도 ──
        List<int[]> edges = new List<int[]>();       // {a, b}, a 가 왼쪽/위
        for (int sy = 0; sy < rows; sy++)
        {
            for (int sx = 0; sx < cols; sx++)
            {
                int idx = sy * cols + sx;
                if (sx + 1 < cols) edges.Add(new int[] { idx, idx + 1 });
                if (sy + 1 < rows) edges.Add(new int[] { idx, idx + cols });
            }
        }
        bool[] used = new bool[edges.Count];

        // 3-a. 신장 트리 (랜덤 프림): 연결된 집합과 아닌 집합 사이의 선 중 하나를 랜덤으로 계속 고른다.
        bool[] inTree = new bool[total];
        inTree[rng.Next(total)] = true;
        int treeCount = 1;
        List<int> frontier = new List<int>();
        while (treeCount < total)
        {
            frontier.Clear();
            for (int e = 0; e < edges.Count; e++)
            {
                if (used[e]) continue;
                if (inTree[edges[e][0]] != inTree[edges[e][1]]) frontier.Add(e);
            }
            if (frontier.Count == 0) break; // 격자라 발생하지 않음 (안전장치)

            int pick = frontier[rng.Next(frontier.Count)];
            used[pick] = true;
            inTree[edges[pick][0]] = true;
            inTree[edges[pick][1]] = true;
            treeCount++;
        }

        // 3-b. 여분 연결 (돌아가는 길)
        List<int> unused = new List<int>();
        for (int e = 0; e < edges.Count; e++) if (!used[e]) unused.Add(e);
        Shuffle(unused, rng);
        int extraMax = Math.Max(0, cfg.maxExtraLinks);
        int extraMin = Clamp(cfg.minExtraLinks, 0, extraMax);
        int extra = Math.Min(rng.Next(extraMin, extraMax + 1), unused.Count);
        for (int i = 0; i < extra; i++) used[unused[i]] = true;

        // 3-c. 빈 구획은 막다른 통로 끝이 되지 않도록 최소 2갈래 연결
        for (int idx = 0; idx < total; idx++)
        {
            if (!gone[idx]) continue;
            while (Degree(edges, used, idx) < 2)
            {
                List<int> candidates = new List<int>();
                for (int e = 0; e < edges.Count; e++)
                {
                    if (used[e]) continue;
                    if (edges[e][0] == idx || edges[e][1] == idx) candidates.Add(e);
                }
                if (candidates.Count == 0) break;
                used[candidates[rng.Next(candidates.Count)]] = true;
            }
        }

        // ── 4. 설계도대로 통로 파기 ──
        for (int e = 0; e < edges.Count; e++)
        {
            if (!used[e]) continue;
            FloorRoom a = sectionRooms[edges[e][0]];
            FloorRoom b = sectionRooms[edges[e][1]];
            bool horizontal = edges[e][1] == edges[e][0] + 1;
            if (horizontal) ConnectHorizontal(data, rng, a, b);
            else ConnectVertical(data, rng, a, b);
        }

        // ── 6. 시작 / 계단 / 마을 입구 ──
        List<FloorRoom> realRooms = new List<FloorRoom>();
        for (int i = 0; i < data.rooms.Count; i++) if (!data.rooms[i].isGone) realRooms.Add(data.rooms[i]);

        List<Vector2Int> taken = new List<Vector2Int>();

        FloorRoom startRoom = realRooms[rng.Next(realRooms.Count)];
        data.startPos = RandomFreeCell(rng, startRoom.bounds, taken);
        taken.Add(data.startPos);

        FloorRoom stairsRoom = PickRoomExcept(rng, realRooms, startRoom, null);
        data.stairsPos = RandomFreeCell(rng, stairsRoom.bounds, taken);
        taken.Add(data.stairsPos);
        data.cells[data.stairsPos.x, data.stairsPos.y].feature = FloorFeature.StairsDown;

        if (cfg.floorType == FloorType.Town)
        {
            FloorRoom gateRoom = PickRoomExcept(rng, realRooms, startRoom, stairsRoom);
            data.townGatePos = RandomFreeCell(rng, gateRoom.bounds, taken);
            data.hasTownGate = true;
            data.cells[data.townGatePos.x, data.townGatePos.y].feature = FloorFeature.TownGate;
        }

        return data;
    }

    /// <summary>A(왼쪽) ↔ B(오른쪽). A 오른쪽 벽의 문 → 한 번 꺾기 → B 왼쪽 벽의 문.</summary>
    private static void ConnectHorizontal(DungeonFloorData data, System.Random rng, FloorRoom a, FloorRoom b)
    {
        Vector2Int pa, pb;
        if (a.isGone) pa = a.bounds.position;
        else
        {
            int y = a.bounds.y + rng.Next(a.bounds.height);
            pa = new Vector2Int(a.bounds.xMax, y);
            AddDoor(data, a, new Vector2Int(a.bounds.xMax - 1, y), pa);
        }

        if (b.isGone) pb = b.bounds.position;
        else
        {
            int y = b.bounds.y + rng.Next(b.bounds.height);
            pb = new Vector2Int(b.bounds.x - 1, y);
            AddDoor(data, b, new Vector2Int(b.bounds.x, y), pb);
        }

        // 꺾는 열: 방이면 문 칸에서 한 칸 더 떨어진 곳부터 → 세로 통로가 방 옆면에 달라붙지 않음
        int lo = a.isGone ? pa.x : pa.x + 1;
        int hi = b.isGone ? pb.x : pb.x - 1;
        if (lo > hi) { lo = Math.Min(pa.x, pb.x); hi = Math.Max(pa.x, pb.x); }
        int tx = lo + rng.Next(hi - lo + 1);

        DigHorizontal(data, pa.x, tx, pa.y);
        DigVertical(data, tx, pa.y, pb.y);
        DigHorizontal(data, tx, pb.x, pb.y);
    }

    /// <summary>A(위) ↔ B(아래). A 아래 벽의 문 → 한 번 꺾기 → B 위 벽의 문.</summary>
    private static void ConnectVertical(DungeonFloorData data, System.Random rng, FloorRoom a, FloorRoom b)
    {
        Vector2Int pa, pb;
        if (a.isGone) pa = a.bounds.position;
        else
        {
            int x = a.bounds.x + rng.Next(a.bounds.width);
            pa = new Vector2Int(x, a.bounds.yMax);
            AddDoor(data, a, new Vector2Int(x, a.bounds.yMax - 1), pa);
        }

        if (b.isGone) pb = b.bounds.position;
        else
        {
            int x = b.bounds.x + rng.Next(b.bounds.width);
            pb = new Vector2Int(x, b.bounds.y - 1);
            AddDoor(data, b, new Vector2Int(x, b.bounds.y), pb);
        }

        int lo = a.isGone ? pa.y : pa.y + 1;
        int hi = b.isGone ? pb.y : pb.y - 1;
        if (lo > hi) { lo = Math.Min(pa.y, pb.y); hi = Math.Max(pa.y, pb.y); }
        int ty = lo + rng.Next(hi - lo + 1);

        DigVertical(data, pa.x, pa.y, ty);
        DigHorizontal(data, pa.x, pb.x, ty);
        DigVertical(data, pb.x, ty, pb.y);
    }

    private static void AddDoor(DungeonFloorData data, FloorRoom room, Vector2Int roomCell, Vector2Int corridorCell)
    {
        FloorDoor door = new FloorDoor(roomCell, corridorCell);
        room.doors.Add(door);
        data.doors.Add(door);
    }

    private static void DigHorizontal(DungeonFloorData data, int x0, int x1, int y)
    {
        int from = Math.Min(x0, x1), to = Math.Max(x0, x1);
        for (int x = from; x <= to; x++) SetCorridor(data, x, y);
    }

    private static void DigVertical(DungeonFloorData data, int x, int y0, int y1)
    {
        int from = Math.Min(y0, y1), to = Math.Max(y0, y1);
        for (int y = from; y <= to; y++) SetCorridor(data, x, y);
    }

    /// <summary>암반만 통로로 바꾼다 (방 칸은 절대 덮어쓰지 않음).</summary>
    private static void SetCorridor(DungeonFloorData data, int x, int y)
    {
        if (x < 0 || y < 0 || x >= data.width || y >= data.height) return;
        if (data.cells[x, y].terrain != FloorTerrain.Rock) return;
        data.cells[x, y].terrain = FloorTerrain.Corridor;
        data.cells[x, y].roomId = -1;
    }

    private static FloorRoom PickRoomExcept(System.Random rng, List<FloorRoom> rooms, FloorRoom exclude1, FloorRoom exclude2)
    {
        List<FloorRoom> pool = new List<FloorRoom>();
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i] != exclude1 && rooms[i] != exclude2) pool.Add(rooms[i]);
        if (pool.Count == 0)
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i] != exclude1) pool.Add(rooms[i]);
        if (pool.Count == 0) pool.AddRange(rooms);
        return pool[rng.Next(pool.Count)];
    }

    private static Vector2Int RandomFreeCell(System.Random rng, RectInt bounds, List<Vector2Int> taken)
    {
        List<Vector2Int> free = new List<Vector2Int>();
        for (int x = bounds.x; x < bounds.xMax; x++)
            for (int y = bounds.y; y < bounds.yMax; y++)
            {
                Vector2Int p = new Vector2Int(x, y);
                if (!taken.Contains(p)) free.Add(p);
            }
        if (free.Count == 0) return bounds.position;
        return free[rng.Next(free.Count)];
    }

    // ─────────────────────────────────────────────
    // 검증
    // ─────────────────────────────────────────────

    /// <summary>시작점에서 모든 걸을 수 있는 칸(모든 방·계단·마을 입구 포함)에 도달 가능한지.</summary>
    public static bool Validate(DungeonFloorData data, out string error)
    {
        error = null;
        if (data == null) { error = "data null"; return false; }
        if (!data.IsWalkable(data.startPos)) { error = "시작 위치가 걸을 수 없는 칸"; return false; }
        if (data.GetCell(data.stairsPos).feature != FloorFeature.StairsDown) { error = "계단 없음"; return false; }
        if (data.stairsPos == data.startPos) { error = "계단이 시작 위치와 겹침"; return false; }

        bool[,] seen = new bool[data.width, data.height];
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(data.startPos);
        seen[data.startPos.x, data.startPos.y] = true;
        int reached = 0;
        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            reached++;
            for (int d = 0; d < 4; d++)
            {
                Vector2Int n = c + dirs[d];
                if (!data.IsWalkable(n) || seen[n.x, n.y]) continue;
                seen[n.x, n.y] = true;
                q.Enqueue(n);
            }
        }

        int walkable = 0;
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.cells[x, y].IsWalkable) walkable++;

        if (reached != walkable) { error = "끊긴 구역 있음 (도달 " + reached + " / 전체 " + walkable + ")"; return false; }
        if (data.hasTownGate && !seen[data.townGatePos.x, data.townGatePos.y]) { error = "마을 입구 도달 불가"; return false; }
        return true;
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────

    private static int Degree(List<int[]> edges, bool[] used, int node)
    {
        int d = 0;
        for (int e = 0; e < edges.Count; e++)
            if (used[e] && (edges[e][0] == node || edges[e][1] == node)) d++;
        return d;
    }

    private static List<int> Range(int count)
    {
        List<int> list = new List<int>(count);
        for (int i = 0; i < count; i++) list.Add(i);
        return list;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private static int Clamp(int v, int min, int max)
    {
        return v < min ? min : (v > max ? max : v);
    }
}
