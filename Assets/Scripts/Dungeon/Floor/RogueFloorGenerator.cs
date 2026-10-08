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
///     2층부터는 시작 자리에 올라가는 계단을 둔다 (위층에서 내려와 도착하는 곳).
///  7. 보물상자·숨겨진 함정·회복의 샘을 배치한다 (문 칸 제외).
///  8. 시작점에서 모든 방·계단·마을 입구까지 갈 수 있는지 검증. 실패하면 시드 +1 로 재시도(결정론 유지).
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
                    if (rng.NextDouble() < cfg.shapedRoomChance) ShapeRoom(data, rng, room);
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
        // reserved = 방 영역 + 둘레 1칸. 두 줄 통로의 둘째 줄·막다른 길은 여기에 들어가지 않는다
        //   → 방 모양(깎인 모서리·기둥)을 망가뜨리지 않고, 통로가 문 이외의 곳에서 방에 닿지 않는다.
        bool[,] reserved = BuildReserved(data);
        for (int e = 0; e < edges.Count; e++)
        {
            if (!used[e]) continue;
            FloorRoom a = sectionRooms[edges[e][0]];
            FloorRoom b = sectionRooms[edges[e][1]];
            bool horizontal = edges[e][1] == edges[e][0] + 1;
            List<Vector2Int> path = horizontal ? ConnectHorizontal(data, rng, a, b) : ConnectVertical(data, rng, a, b);
            if (rng.NextDouble() < cfg.wideCorridorChance) WidenPath(data, path, reserved, rng);
        }

        // ── 4-b. 막다른 길 ──
        int deadMax = Math.Max(0, cfg.maxDeadEnds);
        int deadEnds = rng.Next(Clamp(cfg.minDeadEnds, 0, deadMax), deadMax + 1);
        for (int i = 0; i < deadEnds; i++) TryDigDeadEnd(data, rng, reserved);

        // ── 6. 시작 / 계단 / 마을 입구 ──
        List<FloorRoom> realRooms = new List<FloorRoom>();
        for (int i = 0; i < data.rooms.Count; i++) if (!data.rooms[i].isGone) realRooms.Add(data.rooms[i]);

        List<Vector2Int> taken = new List<Vector2Int>();
        DoorZones zones = new DoorZones(data);

        FloorRoom startRoom = PickRoomWithClearCell(rng, realRooms, null, null, data, taken, zones);
        data.startPos = PickFeatureCell(data, rng, startRoom.bounds, taken, zones);
        taken.Add(data.startPos);

        FloorRoom stairsRoom = PickRoomWithClearCell(rng, realRooms, startRoom, null, data, taken, zones);
        data.stairsPos = PickFeatureCell(data, rng, stairsRoom.bounds, taken, zones);
        taken.Add(data.stairsPos);
        data.cells[data.stairsPos.x, data.stairsPos.y].feature = FloorFeature.StairsDown;

        if (cfg.floorType == FloorType.Town)
        {
            FloorRoom gateRoom = PickRoomWithClearCell(rng, realRooms, startRoom, stairsRoom, data, taken, zones);
            data.townGatePos = PickFeatureCell(data, rng, gateRoom.bounds, taken, zones);
            data.hasTownGate = true;
            data.cells[data.townGatePos.x, data.townGatePos.y].feature = FloorFeature.TownGate;
        }

        // ── 7. 올라가는 계단: 2층부터, 위층에서 내려와 도착하는 시작 자리에 ──
        if (floorNumber > 1)
        {
            data.hasStairsUp = true;
            data.stairsUpPos = data.startPos;
            data.cells[data.startPos.x, data.startPos.y].feature = FloorFeature.StairsUp;
        }

        // ── 8. 탐험 요소 (보물상자 · 숨겨진 함정 · 회복의 샘) ──
        // 이 단계의 난수는 모두 배치 이후에 쓰므로, 요소를 추가해도 방·통로 모양은 바뀌지 않는다.
        PlaceContents(data, rng, cfg, realRooms, startRoom, zones);

        // ── 9. 불 있는 방 (벽 화로) — 난수를 맨 뒤에 써서 방·통로·요소 배치는 바뀌지 않는다 ──
        PlaceBraziers(data, rng, cfg, realRooms);

        // ── 10. [2026-10-08] 횃불 — 화로 자리는 벽 횃불로, 그 밖에 무작위 벽·바닥 횃불. 별도 난수라 위 배치는 그대로 ──
        PlaceTorches(data, new System.Random(unchecked(attemptSeed * 31 + 7)), realRooms);

        return data;
    }

    // 횃불 배치 규칙 (2026-10-08)
    private const double RoomFloorTorchChance = 0.25;   // 방마다 바닥(가운데) 횃불
    private const double CorridorWallTorchChance = 0.04; // 벽에 붙은 통로 칸마다 벽 횃불
    private const int TorchMinSpacing = 4;              // 횃불끼리 최소 거리 (칸)

    private static void PlaceTorches(DungeonFloorData data, System.Random rng, List<FloorRoom> realRooms)
    {
        // 1) 화로가 있던 방 → 그 자리에 벽 횃불
        foreach (FloorRoom room in realRooms)
            if (room.lit) TryAddTorch(data, room.brazierCell, true);

        // 2) 방 가운데 바닥 횃불
        foreach (FloorRoom room in realRooms)
        {
            if (rng.NextDouble() >= RoomFloorTorchChance) continue;
            List<Vector2Int> inner = new List<Vector2Int>();
            for (int x = room.bounds.x; x < room.bounds.xMax; x++)
                for (int y = room.bounds.y; y < room.bounds.yMax; y++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    FloorCell c = data.cells[x, y];
                    if (c.roomId != room.id || c.feature != FloorFeature.None) continue;
                    bool nearWall = false;
                    for (int d = 0; d < 4; d++) if (!data.IsWalkable(p + Dir4[d])) nearWall = true;
                    if (!nearWall) inner.Add(p);
                }
            if (inner.Count > 0) TryAddTorch(data, inner[rng.Next(inner.Count)], false);
        }

        // 3) 통로 벽 횃불
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                FloorCell c = data.cells[x, y];
                if (!c.IsWalkable || c.terrain != FloorTerrain.Corridor || c.feature != FloorFeature.None) continue;
                if (rng.NextDouble() >= CorridorWallTorchChance) continue;
                TryAddTorch(data, new Vector2Int(x, y), true);
            }
    }

    private static void TryAddTorch(DungeonFloorData data, Vector2Int p, bool onWall)
    {
        if (!data.IsWalkable(p)) return;
        foreach (FloorTorch t in data.torches)
            if (Math.Max(Math.Abs(t.cell.x - p.x), Math.Abs(t.cell.y - p.y)) < TorchMinSpacing) return;
        Vector2Int wall = Vector2Int.zero;
        if (onWall)
        {
            for (int d = 0; d < 4; d++) if (!data.IsWalkable(p + Dir4[d])) { wall = Dir4[d]; break; }
            if (wall == Vector2Int.zero) onWall = false;
        }
        data.torches.Add(new FloorTorch { cell = p, onWall = onWall, wallDir = wall });
    }

    /// <summary>방마다 litRoomChance 확률로 벽에 불을 건다. 불은 벽에 붙은 방 칸(요소 없는 칸) 하나에 표시.</summary>
    private static void PlaceBraziers(DungeonFloorData data, System.Random rng, FloorTableEntry cfg, List<FloorRoom> realRooms)
    {
        for (int i = 0; i < realRooms.Count; i++)
        {
            FloorRoom room = realRooms[i];
            if (rng.NextDouble() >= cfg.litRoomChance) continue;

            List<Vector2Int> wallSide = new List<Vector2Int>();
            for (int x = room.bounds.x; x < room.bounds.xMax; x++)
            {
                for (int y = room.bounds.y; y < room.bounds.yMax; y++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    FloorCell c = data.cells[x, y];
                    if (c.roomId != room.id || c.feature != FloorFeature.None) continue;
                    // 상하좌우 중 암반이 있으면 벽 옆 칸
                    for (int d = 0; d < 4; d++)
                    {
                        if (!data.IsWalkable(p + Dir4[d])) { wallSide.Add(p); break; }
                    }
                }
            }
            if (wallSide.Count == 0) continue;
            room.lit = true;
            room.brazierCell = wallSide[rng.Next(wallSide.Count)];
        }
    }

    /// <summary>
    /// 문 주변 구역. 계단·마을 입구·상자·샘·시작 자리는 문 바로 앞에 두지 않는다 (함정은 예외).
    ///  near   = 문 칸(방 쪽)과 그 둘레 8칸 — 가장 먼저 피한다
    ///  beside = 문 칸과 상하좌우 4칸 — 방이 작아 near 를 다 피할 수 없을 때 그래도 피한다
    ///  door   = 문 칸 자체 (방 쪽·통로 쪽)
    /// </summary>
    private class DoorZones
    {
        public readonly HashSet<Vector2Int> near = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> beside = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> door = new HashSet<Vector2Int>();

        public DoorZones(DungeonFloorData data)
        {
            for (int i = 0; i < data.doors.Count; i++)
            {
                Vector2Int r = data.doors[i].roomCell;
                door.Add(r);
                door.Add(data.doors[i].corridorCell);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2Int p = new Vector2Int(r.x + dx, r.y + dy);
                        near.Add(p);
                        if (dx == 0 || dy == 0) beside.Add(p);
                    }
                }
            }
        }
    }

    /// <summary>문 둘레 8칸 밖의 빈칸이 하나라도 있는 방인지.</summary>
    private static bool HasClearCell(DungeonFloorData data, RectInt bounds, List<Vector2Int> taken, DoorZones zones)
    {
        for (int x = bounds.x; x < bounds.xMax; x++)
        {
            for (int y = bounds.y; y < bounds.yMax; y++)
            {
                Vector2Int p = new Vector2Int(x, y);
                if (!taken.Contains(p) && data.cells[x, y].terrain == FloorTerrain.Room &&
                    data.cells[x, y].feature == FloorFeature.None && !zones.near.Contains(p)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// exclude 를 뺀 방 중 문에서 떨어진 빈칸이 있는 방을 랜덤으로. 그런 방이 없으면 PickRoomExcept 와 같은 규칙.
    /// → 작은 방에 문이 여럿이라 계단·상자가 문 앞에 붙는 일을 줄인다.
    /// </summary>
    private static FloorRoom PickRoomWithClearCell(System.Random rng, List<FloorRoom> rooms, FloorRoom exclude1, FloorRoom exclude2,
                                                   DungeonFloorData data, List<Vector2Int> taken, DoorZones zones)
    {
        List<FloorRoom> pool = new List<FloorRoom>();
        for (int i = 0; i < rooms.Count; i++)
        {
            FloorRoom r = rooms[i];
            if (r == exclude1 || r == exclude2) continue;
            if (HasClearCell(data, r.bounds, taken, zones)) pool.Add(r);
        }
        if (pool.Count > 0) return pool[rng.Next(pool.Count)];
        return PickRoomExcept(rng, rooms, exclude1, exclude2);
    }

    /// <summary>테스트용: 방이 작아 문 바로 옆에 둘 수밖에 없었던 횟수.</summary>
    public static int DoorFallbackCount;

    /// <summary>
    /// 특수 요소(계단·마을 입구·상자·샘·시작 자리)를 놓을 칸. 문에서 떨어진 칸을 우선하고,
    /// 방이 작아 불가능하면 단계적으로 조건을 푼다: 문 둘레 8칸 밖 → 문 상하좌우 밖 → 문 칸만 아니면 → 아무 빈칸.
    /// </summary>
    private static Vector2Int PickFeatureCell(DungeonFloorData data, System.Random rng, RectInt bounds, List<Vector2Int> taken, DoorZones zones)
    {
        for (int tier = 0; tier < 4; tier++)
        {
            List<Vector2Int> free = new List<Vector2Int>();
            for (int x = bounds.x; x < bounds.xMax; x++)
            {
                for (int y = bounds.y; y < bounds.yMax; y++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    if (taken.Contains(p)) continue;
                    if (data.cells[x, y].terrain != FloorTerrain.Room) continue; // 깎인 모서리·기둥 제외
                    if (data.cells[x, y].feature != FloorFeature.None) continue;
                    if (tier == 0 && zones.near.Contains(p)) continue;
                    if (tier == 1 && zones.beside.Contains(p)) continue;
                    if (tier == 2 && zones.door.Contains(p)) continue;
                    free.Add(p);
                }
            }
            if (free.Count > 0)
            {
                if (tier > 0) DoorFallbackCount++;
                return free[rng.Next(free.Count)];
            }
        }
        return bounds.position;
    }

    /// <summary>PickFeatureCell 결과가 실제로 빈칸일 때만 true (방에 빈칸이 하나도 없으면 false).</summary>
    private static bool TryPickFeatureCell(DungeonFloorData data, System.Random rng, RectInt bounds, List<Vector2Int> taken, DoorZones zones, out Vector2Int result)
    {
        result = PickFeatureCell(data, rng, bounds, taken, zones);
        return !taken.Contains(result) && data.cells[result.x, result.y].terrain == FloorTerrain.Room &&
               data.cells[result.x, result.y].feature == FloorFeature.None;
    }

    /// <summary>
    /// 보물상자·함정·샘 배치.
    /// 상자·샘은 문 바로 앞을 피해 문에서 떨어진 칸에 (PickFeatureCell). 함정은 문 칸만 아니면 어디든.
    /// 함정은 시작 방에 두지 않는다 (도착하자마자 밟는 일 방지).
    /// </summary>
    private static void PlaceContents(DungeonFloorData data, System.Random rng, FloorTableEntry cfg,
                                      List<FloorRoom> realRooms, FloorRoom startRoom, DoorZones zones)
    {
        List<Vector2Int> taken = new List<Vector2Int>();
        taken.Add(data.startPos);

        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        for (int i = 0; i < data.doors.Count; i++)
        {
            blocked.Add(data.doors[i].roomCell);
            blocked.Add(data.doors[i].corridorCell);
        }

        List<FloorRoom> otherRooms = new List<FloorRoom>();
        for (int i = 0; i < realRooms.Count; i++) if (realRooms[i] != startRoom) otherRooms.Add(realRooms[i]);
        if (otherRooms.Count == 0) otherRooms.AddRange(realRooms);

        // 보물상자: 아무 방 (시작 방 포함 — 첫 방에서 바로 발견하는 재미)
        int chestMax = Math.Max(0, cfg.maxChests);
        int chests = rng.Next(Clamp(cfg.minChests, 0, chestMax), chestMax + 1);
        for (int i = 0; i < chests; i++)
        {
            Vector2Int p;
            FloorRoom room = PickRoomWithClearCell(rng, realRooms, null, null, data, taken, zones);
            if (!TryPickFeatureCell(data, rng, room.bounds, taken, zones, out p)) continue;
            data.cells[p.x, p.y].feature = FloorFeature.Chest;
            data.chests.Add(p);
        }

        // 회복의 샘: 확률로 하나, 시작 방이 아닌 방
        if (rng.NextDouble() < cfg.springChance)
        {
            Vector2Int p;
            FloorRoom room = PickRoomWithClearCell(rng, realRooms, otherRooms.Count < realRooms.Count ? startRoom : null, null, data, taken, zones);
            if (TryPickFeatureCell(data, rng, room.bounds, taken, zones, out p))
            {
                data.cells[p.x, p.y].feature = FloorFeature.Spring;
                data.springs.Add(p);
            }
        }

        // 숨겨진 함정: 시작 방을 뺀 방·통로의 빈 칸
        List<Vector2Int> trapCells = new List<Vector2Int>();
        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
            {
                Vector2Int p = new Vector2Int(x, y);
                FloorCell c = data.cells[x, y];
                if (!c.IsWalkable || c.feature != FloorFeature.None || blocked.Contains(p)) continue;
                if (c.roomId == startRoom.id) continue;
                trapCells.Add(p);
            }
        }
        Shuffle(trapCells, rng);
        int trapMax = Math.Max(0, cfg.maxTraps);
        int traps = Math.Min(rng.Next(Clamp(cfg.minTraps, 0, trapMax), trapMax + 1), trapCells.Count);

        // [2026-10-07] 함정 배치 패턴 — 함정의 6할까지는 '노릴 만한 자리'에 먼저 둔다 (나머지는 무작위)
        //  · 보물상자 지키기: 상자 상하좌우 한 칸 (상자마다 60%)
        //  · 막다른 길 끝: 통로 끝 칸 (전부 후보)
        //  · 길목: 문 바로 바깥 통로의 한 칸 더 바깥 (문마다 30%)
        HashSet<Vector2Int> allowed = new HashSet<Vector2Int>(trapCells);
        List<Vector2Int> pattern = new List<Vector2Int>();
        for (int i = 0; i < data.chests.Count; i++)
        {
            if (rng.NextDouble() >= 0.6) continue;
            List<Vector2Int> around = new List<Vector2Int>();
            for (int d = 0; d < 4; d++)
            {
                Vector2Int n = data.chests[i] + Dir4[d];
                if (allowed.Contains(n)) around.Add(n);
            }
            if (around.Count > 0) pattern.Add(around[rng.Next(around.Count)]);
        }
        foreach (Vector2Int p in trapCells)
        {
            if (data.cells[p.x, p.y].terrain != FloorTerrain.Corridor) continue;
            int open = 0;
            for (int d = 0; d < 4; d++) if (data.IsWalkable(p + Dir4[d])) open++;
            if (open == 1) pattern.Add(p);
        }
        for (int i = 0; i < data.doors.Count; i++)
        {
            if (rng.NextDouble() >= 0.3) continue;
            Vector2Int outward = data.doors[i].corridorCell + (data.doors[i].corridorCell - data.doors[i].roomCell);
            if (allowed.Contains(outward)) pattern.Add(outward);
        }
        Shuffle(pattern, rng);
        int patternMax = (traps * 6 + 9) / 10; // 올림
        List<Vector2Int> order = new List<Vector2Int>();
        HashSet<Vector2Int> picked = new HashSet<Vector2Int>();
        for (int i = 0; i < pattern.Count && order.Count < patternMax; i++)
            if (picked.Add(pattern[i])) order.Add(pattern[i]);
        for (int i = 0; i < trapCells.Count && order.Count < traps; i++)
            if (picked.Add(trapCells[i])) order.Add(trapCells[i]);

        for (int i = 0; i < order.Count; i++)
        {
            Vector2Int p = order[i];
            data.cells[p.x, p.y].feature = FloorFeature.Trap;
            data.cells[p.x, p.y].trap = PickTrapType(rng, data.floorNumber);
            data.traps.Add(p);
        }
    }

    /// <summary>
    /// 층이 깊을수록 함정 종류가 늘어난다 (괄호 = 가중치). [2026-10-07 개편] 초반부터 다양하게 + 새 함정 2종
    ///  1층~ 가시(3)·독침(3)·칼날(3)·그물(2)·석궁(2) / 2층~ 전송(2)·경보(2)·실명 가스(2)·마나 흡수(2)
    ///  3층~ 화염(2)·낙석(2) / 4층~ 구멍(1)
    /// </summary>
    private static FloorTrapType PickTrapType(System.Random rng, int floor)
    {
        List<FloorTrapType> pool = new List<FloorTrapType>();
        AddWeight(pool, FloorTrapType.Spike, 3);
        AddWeight(pool, FloorTrapType.PoisonDart, 3);
        AddWeight(pool, FloorTrapType.Blade, 3);
        AddWeight(pool, FloorTrapType.Net, 2);
        AddWeight(pool, FloorTrapType.Crossbow, 2);
        if (floor >= 2) { AddWeight(pool, FloorTrapType.Teleport, 2); AddWeight(pool, FloorTrapType.Alarm, 2); AddWeight(pool, FloorTrapType.BlindingGas, 2); AddWeight(pool, FloorTrapType.ManaDrain, 2); }
        if (floor >= 3) { AddWeight(pool, FloorTrapType.FlameVent, 2); AddWeight(pool, FloorTrapType.Rockfall, 2); }
        if (floor >= 4) { AddWeight(pool, FloorTrapType.Pitfall, 1); }
        return pool[rng.Next(pool.Count)];
    }

    private static void AddWeight(List<FloorTrapType> pool, FloorTrapType type, int weight)
    {
        for (int i = 0; i < weight; i++) pool.Add(type);
    }


    /// <summary>
    /// A(왼쪽) ↔ B(오른쪽). A 오른쪽 벽의 문 → 한 번 꺾기 → B 왼쪽 벽의 문. 판 통로 칸들을 순서대로 돌려준다.
    /// 문은 벽면에서 실제 방 칸인 줄만 고른다 (L자·십자 방은 벽면 일부가 깎여 있음).
    /// </summary>
    private static List<Vector2Int> ConnectHorizontal(DungeonFloorData data, System.Random rng, FloorRoom a, FloorRoom b)
    {
        Vector2Int pa, pb;
        if (a.isGone) pa = a.bounds.position;
        else
        {
            int y = PickEdgeRow(data, rng, a, a.bounds.xMax - 1);
            pa = new Vector2Int(a.bounds.xMax, y);
            AddDoor(data, a, new Vector2Int(a.bounds.xMax - 1, y), pa);
        }

        if (b.isGone) pb = b.bounds.position;
        else
        {
            int y = PickEdgeRow(data, rng, b, b.bounds.x);
            pb = new Vector2Int(b.bounds.x - 1, y);
            AddDoor(data, b, new Vector2Int(b.bounds.x, y), pb);
        }

        // 꺾는 열: 방이면 문 칸에서 한 칸 더 떨어진 곳부터 → 세로 통로가 방 옆면에 달라붙지 않음
        int lo = a.isGone ? pa.x : pa.x + 1;
        int hi = b.isGone ? pb.x : pb.x - 1;
        if (lo > hi) { lo = Math.Min(pa.x, pb.x); hi = Math.Max(pa.x, pb.x); }
        int tx = lo + rng.Next(hi - lo + 1);

        List<Vector2Int> path = new List<Vector2Int>();
        DigHorizontal(data, pa.x, tx, pa.y, path);
        DigVertical(data, tx, pa.y, pb.y, path);
        DigHorizontal(data, tx, pb.x, pb.y, path);
        return path;
    }

    /// <summary>A(위) ↔ B(아래). A 아래 벽의 문 → 한 번 꺾기 → B 위 벽의 문.</summary>
    private static List<Vector2Int> ConnectVertical(DungeonFloorData data, System.Random rng, FloorRoom a, FloorRoom b)
    {
        Vector2Int pa, pb;
        if (a.isGone) pa = a.bounds.position;
        else
        {
            int x = PickEdgeColumn(data, rng, a, a.bounds.yMax - 1);
            pa = new Vector2Int(x, a.bounds.yMax);
            AddDoor(data, a, new Vector2Int(x, a.bounds.yMax - 1), pa);
        }

        if (b.isGone) pb = b.bounds.position;
        else
        {
            int x = PickEdgeColumn(data, rng, b, b.bounds.y);
            pb = new Vector2Int(x, b.bounds.y - 1);
            AddDoor(data, b, new Vector2Int(x, b.bounds.y), pb);
        }

        int lo = a.isGone ? pa.y : pa.y + 1;
        int hi = b.isGone ? pb.y : pb.y - 1;
        if (lo > hi) { lo = Math.Min(pa.y, pb.y); hi = Math.Max(pa.y, pb.y); }
        int ty = lo + rng.Next(hi - lo + 1);

        List<Vector2Int> path = new List<Vector2Int>();
        DigVertical(data, pa.x, pa.y, ty, path);
        DigHorizontal(data, pa.x, pb.x, ty, path);
        DigVertical(data, pb.x, ty, pb.y, path);
        return path;
    }

    /// <summary>세로 벽면(열 edgeX)에서 방 칸인 줄 중 하나.</summary>
    private static int PickEdgeRow(DungeonFloorData data, System.Random rng, FloorRoom room, int edgeX)
    {
        List<int> rows = new List<int>();
        for (int y = room.bounds.y; y < room.bounds.yMax; y++)
            if (data.cells[edgeX, y].roomId == room.id) rows.Add(y);
        return rows.Count > 0 ? rows[rng.Next(rows.Count)] : room.bounds.y + room.bounds.height / 2;
    }

    /// <summary>가로 벽면(줄 edgeY)에서 방 칸인 열 중 하나.</summary>
    private static int PickEdgeColumn(DungeonFloorData data, System.Random rng, FloorRoom room, int edgeY)
    {
        List<int> cols = new List<int>();
        for (int x = room.bounds.x; x < room.bounds.xMax; x++)
            if (data.cells[x, edgeY].roomId == room.id) cols.Add(x);
        return cols.Count > 0 ? cols[rng.Next(cols.Count)] : room.bounds.x + room.bounds.width / 2;
    }

    // ─────────────────────────────────────────────
    // 모양 다양성: 방 모양 · 두 줄 통로 · 막다른 길
    // ─────────────────────────────────────────────

    /// <summary>
    /// 직사각형 방을 다른 모양으로 깎는다 (깎은 칸 = 암반). 네 벽면 모두 방 칸이 최소 한 칸은 남아 문을 낼 수 있다.
    ///  L자: 한 모서리를 깎음 / 십자: 네 모서리를 깎음 / 고리: 가운데를 비움(두께 2) / 기둥: 안쪽 네 곳에 기둥 1칸
    /// </summary>
    private static void ShapeRoom(DungeonFloorData data, System.Random rng, FloorRoom room)
    {
        RectInt b = room.bounds;
        int w = b.width, h = b.height;
        List<int> shapes = new List<int>();
        if (w >= 4 && h >= 4) shapes.Add(0);
        if (w >= 5 && h >= 5) { shapes.Add(1); shapes.Add(3); }
        if (w >= 6 && h >= 6) shapes.Add(2);
        if (shapes.Count == 0) return;

        switch (shapes[rng.Next(shapes.Count)])
        {
            case 0: // L자
            {
                int cw = 1 + rng.Next(w / 2);
                int ch = 1 + rng.Next(h / 2);
                int corner = rng.Next(4);
                int x0 = (corner == 0 || corner == 2) ? b.x : b.xMax - cw;
                int y0 = (corner == 0 || corner == 1) ? b.y : b.yMax - ch;
                CarveRock(data, x0, y0, cw, ch);
                break;
            }
            case 1: // 십자
            {
                int cw = Math.Max(1, w / 3), ch = Math.Max(1, h / 3);
                CarveRock(data, b.x, b.y, cw, ch);
                CarveRock(data, b.xMax - cw, b.y, cw, ch);
                CarveRock(data, b.x, b.yMax - ch, cw, ch);
                CarveRock(data, b.xMax - cw, b.yMax - ch, cw, ch);
                break;
            }
            case 2: // 고리 (가운데 암반, 둘레 두께 2)
                CarveRock(data, b.x + 2, b.y + 2, w - 4, h - 4);
                break;
            default: // 기둥 넷
                CarveRock(data, b.x + 1, b.y + 1, 1, 1);
                CarveRock(data, b.xMax - 2, b.y + 1, 1, 1);
                CarveRock(data, b.x + 1, b.yMax - 2, 1, 1);
                CarveRock(data, b.xMax - 2, b.yMax - 2, 1, 1);
                break;
        }
    }

    private static void CarveRock(DungeonFloorData data, int x0, int y0, int w, int h)
    {
        for (int x = x0; x < x0 + w; x++)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                if (x < 0 || y < 0 || x >= data.width || y >= data.height) continue;
                data.cells[x, y].terrain = FloorTerrain.Rock;
                data.cells[x, y].roomId = -1;
            }
        }
    }

    /// <summary>방 영역 + 둘레 1칸 (빈 구획 제외).</summary>
    private static bool[,] BuildReserved(DungeonFloorData data)
    {
        bool[,] reserved = new bool[data.width, data.height];
        for (int i = 0; i < data.rooms.Count; i++)
        {
            FloorRoom r = data.rooms[i];
            if (r.isGone) continue;
            for (int x = r.bounds.x - 1; x <= r.bounds.xMax; x++)
                for (int y = r.bounds.y - 1; y <= r.bounds.yMax; y++)
                    if (x >= 0 && y >= 0 && x < data.width && y < data.height) reserved[x, y] = true;
        }
        return reserved;
    }

    /// <summary>
    /// 통로를 두 줄로 넓힌다: 경로 칸마다 진행 방향의 옆 칸을 하나 더 판다 (한쪽으로만, 경로 내내 같은 쪽).
    /// 방 둘레(reserved)와 지도 가장자리는 파지 않으므로 문 앞은 한 줄로 남는다.
    /// </summary>
    private static void WidenPath(DungeonFloorData data, List<Vector2Int> path, bool[,] reserved, System.Random rng)
    {
        if (path.Count < 2) return;
        int side = rng.Next(2) == 0 ? 1 : -1;
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int prev = path[i > 0 ? i - 1 : i + 1];
            Vector2Int p = path[i];
            bool horizontalStep = prev.y == p.y;
            Vector2Int lane = horizontalStep ? new Vector2Int(p.x, p.y + side) : new Vector2Int(p.x + side, p.y);
            DigExtra(data, lane, reserved);
            // 꺾이는 곳의 대각선도 채워 모서리가 톱니가 되지 않게
            DigExtra(data, new Vector2Int(p.x + side, p.y + side), reserved);
        }
    }

    private static void DigExtra(DungeonFloorData data, Vector2Int p, bool[,] reserved)
    {
        if (p.x < 1 || p.y < 1 || p.x >= data.width - 1 || p.y >= data.height - 1) return;
        if (reserved[p.x, p.y] || data.cells[p.x, p.y].terrain != FloorTerrain.Rock) return;
        data.cells[p.x, p.y].terrain = FloorTerrain.Corridor;
        data.cells[p.x, p.y].roomId = -1;
    }

    private static readonly Vector2Int[] Dir4 = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };

    /// <summary>
    /// 막다른 길: 기존 통로 칸에서 암반 쪽으로 2~5칸 곧게 판다. 다른 통로·방에 닿으면 멈추고, 2칸이 안 되면 취소.
    /// (이상한 던전·세계수의 미궁의 "가 봐야 아는 길")
    /// </summary>
    private static void TryDigDeadEnd(DungeonFloorData data, System.Random rng, bool[,] reserved)
    {
        List<Vector2Int> corridors = new List<Vector2Int>();
        for (int x = 1; x < data.width - 1; x++)
            for (int y = 1; y < data.height - 1; y++)
                if (data.cells[x, y].terrain == FloorTerrain.Corridor && !reserved[x, y]) corridors.Add(new Vector2Int(x, y));
        if (corridors.Count == 0) return;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2Int start = corridors[rng.Next(corridors.Count)];
            Vector2Int dir = Dir4[rng.Next(4)];
            int want = 2 + rng.Next(4);
            List<Vector2Int> dug = new List<Vector2Int>();
            Vector2Int cur = start;
            for (int step = 0; step < want; step++)
            {
                Vector2Int n = cur + dir;
                if (n.x < 1 || n.y < 1 || n.x >= data.width - 1 || n.y >= data.height - 1) break;
                if (reserved[n.x, n.y] || data.cells[n.x, n.y].terrain != FloorTerrain.Rock) break;
                // 새 칸의 이웃(온 곳 제외)이 모두 암반이어야 다른 길과 붙지 않는 깔끔한 막다른 길이 된다
                bool touches = false;
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int m = n + Dir4[d];
                    if (m == cur) continue;
                    if (data.IsWalkable(m)) { touches = true; break; }
                }
                if (touches) break;
                data.cells[n.x, n.y].terrain = FloorTerrain.Corridor;
                data.cells[n.x, n.y].roomId = -1;
                dug.Add(n);
                cur = n;
            }
            if (dug.Count >= 2) return;
            for (int i = 0; i < dug.Count; i++) data.cells[dug[i].x, dug[i].y].terrain = FloorTerrain.Rock;
        }
    }

    private static void AddDoor(DungeonFloorData data, FloorRoom room, Vector2Int roomCell, Vector2Int corridorCell)
    {
        FloorDoor door = new FloorDoor(roomCell, corridorCell);
        room.doors.Add(door);
        data.doors.Add(door);
    }

    /// <summary>x0 → x1 방향으로 판다 (path 에 진행 순서대로 추가).</summary>
    private static void DigHorizontal(DungeonFloorData data, int x0, int x1, int y, List<Vector2Int> path)
    {
        int step = x1 >= x0 ? 1 : -1;
        for (int x = x0; ; x += step)
        {
            SetCorridor(data, x, y);
            AddPath(path, new Vector2Int(x, y));
            if (x == x1) break;
        }
    }

    /// <summary>y0 → y1 방향으로 판다 (path 에 진행 순서대로 추가).</summary>
    private static void DigVertical(DungeonFloorData data, int x, int y0, int y1, List<Vector2Int> path)
    {
        int step = y1 >= y0 ? 1 : -1;
        for (int y = y0; ; y += step)
        {
            SetCorridor(data, x, y);
            AddPath(path, new Vector2Int(x, y));
            if (y == y1) break;
        }
    }

    private static void AddPath(List<Vector2Int> path, Vector2Int p)
    {
        if (path.Count == 0 || path[path.Count - 1] != p) path.Add(p);
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
