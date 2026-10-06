using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

/// <summary>
/// 위저드리풍 자동 지도. DungeonFloorData + 드러난 칸 목록을 읽어 지도 전체를 메시 하나로 그린다.
///  - 드러난 걸을 수 있는 칸: 바닥 채움 (반투명)
///  - 드러난 칸과 암반 사이 경계: 흰 벽선
///  - 문: 방과 통로가 만나는 경계에 짧은 막대
///  - 아이콘: 계단(계단 모양 + 위/아래 화살표), 마을 입구(◆), 보물상자(열림/닫힘), 회복의 샘(사용 전/후),
///    함정(✕ — 밟아서 발견한 것만, 종류별 색 / 다 쓴 함정은 회색)
/// 드러나지 않은 곳은 아무것도 그리지 않는다 (= 안개).
///
/// 벽 조각마다 오브젝트를 만들던 기존 방식(DungeonWallLineDrawer) 대신 메시 1개라 맵 크기와 무관하게 가볍다.
/// 좌표는 기존 타일맵(referenceTilemap)의 칸 중심을 그대로 쓰므로 플레이어 위치(DungeonGridPlayer)와 정확히 맞는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AutomapRenderer : MonoBehaviour
{
    [Header("Colors")]
    [Tooltip("드러난 통로 바닥 채움 — 배경이 20% 비쳐 보이는 반투명 검정")]
    public Color floorColor = new Color(0f, 0f, 0f, 0.8f);
    [Tooltip("드러난 방 바닥 채움 — 통로와 같은 반투명 검정 (격자는 그 위에 보임)")]
    public Color roomFloorColor = new Color(0f, 0f, 0f, 0.8f);
    public Color wallColor = Color.white;
    [Tooltip("닫힌 문 (칸 경계 전체를 막는 막대)")]
    public Color doorColor = new Color(0.35f, 0.65f, 1f, 1f);
    [Tooltip("층 전체에 깔리는 칸 격자 (세계수의 미궁식 지도 방안). a=0 이면 격자 없음")]
    public Color gridColor = new Color(1f, 1f, 1f, 0.04f);
    [Tooltip("드러난 바닥 위의 격자 (칸이 보일 정도로만 옅게)")]
    public Color revealedGridColor = new Color(1f, 1f, 1f, 0.08f);
    [Tooltip("안개: 가 본 곳이지만 지금 시야 밖인 칸을 덮는 색 (픽셀 던전식 흐림). a=0 이면 흐림 없음")]
    public Color rememberedFogColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("내려가는 계단")]
    public Color stairsColor = new Color(0.45f, 0.9f, 1f, 1f);
    [Tooltip("올라가는 계단")]
    public Color stairsUpColor = new Color(0.6f, 1f, 0.55f, 1f);
    public Color townGateColor = new Color(1f, 0.86f, 0.25f, 1f);
    public Color chestColor = new Color(1f, 0.76f, 0.22f, 1f);
    public Color springColor = new Color(0.35f, 0.7f, 1f, 1f);
    public Color spentColor = new Color(0.5f, 0.5f, 0.52f, 0.85f);
    public Color spikeTrapColor = new Color(1f, 0.3f, 0.25f, 1f);
    public Color teleportTrapColor = new Color(0.8f, 0.45f, 1f, 1f);
    public Color alarmTrapColor = new Color(1f, 0.6f, 0.2f, 1f);
    public Color pitfallTrapColor = new Color(0.9f, 0.2f, 0.45f, 1f);
    // [2026-10-07] 상태이상 함정
    public Color poisonTrapColor = new Color(0.45f, 0.95f, 0.35f, 1f);
    public Color bladeTrapColor = new Color(0.95f, 0.15f, 0.2f, 1f);
    public Color flameTrapColor = new Color(1f, 0.5f, 0.1f, 1f);
    public Color gasTrapColor = new Color(0.65f, 0.7f, 1f, 1f);
    public Color rockfallTrapColor = new Color(0.75f, 0.65f, 0.5f, 1f);
    public Color netTrapColor = new Color(0.9f, 0.85f, 0.55f, 1f);
    [Tooltip("벽 화로(불 있는 방) — 바깥 불꽃 / 안쪽 불꽃")]
    public Color brazierColor = new Color(1f, 0.55f, 0.15f, 1f);
    public Color brazierCoreColor = new Color(1f, 0.92f, 0.55f, 1f);

    [Header("Size (칸 크기 대비 비율)")]
    [Range(0.01f, 0.3f)] public float wallThickness = 0.08f;
    [Range(0.1f, 1f)] public float doorLength = 1f;
    [Range(0.01f, 0.4f)] public float doorThickness = 0.16f;
    [Range(0.005f, 0.1f)] public float gridThickness = 0.025f;
    [Range(0.1f, 1f)] public float iconSize = 0.6f;
    [Range(0.1f, 1f)] public float stairsIconSize = 0.85f;

    [Header("Rendering")]
    [Tooltip("비우면 Sprite-Unlit 셰이더로 자동 생성")]
    public Material material;
    public string sortingLayerName = "Default";
    [Tooltip("배경(0) 위, 플레이어 표시(10) 아래")]
    public int sortingOrder = 5;

    private DungeonFloorData _data;
    private DungeonFloorState _state;
    private HashSet<Vector2Int> _revealed;
    private HashSet<Vector2Int> _visible; // 지금 시야 안의 칸 (null 이면 흐림 처리 안 함)
    private Tilemap _reference;
    private Mesh _mesh;
    private float _lineScale = 1f;

    private readonly List<Vector3> _verts = new List<Vector3>();
    private readonly List<Color> _colors = new List<Color>();
    private readonly List<int> _tris = new List<int>();

    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, -1), // 북
        new Vector2Int(1, 0),  // 동
        new Vector2Int(0, 1),  // 남
        new Vector2Int(-1, 0), // 서
    };

    private void Awake()
    {
        // 월드 좌표로 정점을 만들므로 변환은 항상 원점/단위
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        _mesh = new Mesh { name = "Automap" };
        _mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = _mesh;

        var mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = material != null ? material : CreateDefaultMaterial();
        mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>층 데이터 + 그 층의 탐험 기록(드러난 칸·연 상자·발견한 함정·쓴 샘)을 연결하고 다시 그린다.</summary>
    public void Bind(DungeonFloorData data, DungeonFloorState state, Tilemap referenceTilemap)
    {
        _data = data;
        _state = state;
        _revealed = state != null ? state.revealed : null;
        _reference = referenceTilemap;
        Rebuild();
    }

    /// <summary>지금 보이는 칸 목록을 연결 (MapManager 가 매 걸음 갱신). 이 밖의 기억된 칸은 흐리게 그린다.</summary>
    public void SetVisible(HashSet<Vector2Int> visible)
    {
        _visible = visible;
    }

    /// <summary>벽선·문 굵기 배율. 전체 지도처럼 축소해 볼 때 선이 너무 가늘어지지 않게 키운다 (평소 1).</summary>
    public void SetLineScale(float scale)
    {
        scale = Mathf.Max(0.1f, scale);
        if (Mathf.Approximately(scale, _lineScale)) return;
        _lineScale = scale;
        Rebuild();
    }

    /// <summary>드러난 칸이 바뀌었을 때 호출. 층 전체를 다시 그린다 (수천 칸 규모에서도 1ms 미만).</summary>
    public void Rebuild()
    {
        if (_mesh == null) Awake();
        _verts.Clear();
        _colors.Clear();
        _tris.Clear();

        if (_data != null && _revealed != null)
        {
            Vector2 cs = CellSize();
            float t = Mathf.Min(cs.x, cs.y) * wallThickness * _lineScale;

            // 0) 층 전체 격자 (지도 방안) — 가장 아래
            if (gridColor.a > 0f) AddFloorGrid(cs);

            // 1) 바닥 채움 — 먼저 그려야 선이 위에 올라간다. 방은 불투명 검정, 통로는 반투명
            foreach (var p in _revealed)
            {
                if (!_data.IsWalkable(p)) continue;
                Color fill = _data.GetCell(p).terrain == FloorTerrain.Room ? roomFloorColor : floorColor;
                if (fill.a <= 0f) continue;
                AddRect(CellCenter(p), cs.x * 0.5f, cs.y * 0.5f, fill);
            }

            // 1-b) 드러난 바닥 위 격자 (칸 경계가 또렷하게 보이도록 바닥보다 조금 진하게)
            if (revealedGridColor.a > 0f)
            {
                float gt = Mathf.Min(cs.x, cs.y) * gridThickness;
                foreach (var p in _revealed)
                {
                    if (!_data.IsWalkable(p)) continue;
                    Vector3 c = CellCenter(p);
                    // 오른쪽·아래 경계만 그려 겹치지 않게 (이웃도 걸을 수 있는 칸일 때만 — 벽 쪽은 벽선이 그림)
                    if (_data.IsWalkable(p + Dirs[1])) AddRect(new Vector3(c.x + cs.x * 0.5f, c.y, c.z), gt * 0.5f, cs.y * 0.5f, revealedGridColor);
                    if (_data.IsWalkable(p + Dirs[2])) AddRect(new Vector3(c.x, c.y - cs.y * 0.5f, c.z), cs.x * 0.5f, gt * 0.5f, revealedGridColor);
                }
            }

            // 2) 벽선 — 드러난 칸과 암반 사이 경계. 모서리가 끊기지 않게 두께만큼 길게.
            foreach (var p in _revealed)
            {
                if (!_data.IsWalkable(p)) continue;
                Vector3 c = CellCenter(p);
                for (int d = 0; d < 4; d++)
                {
                    if (_data.IsWalkable(p + Dirs[d])) continue;
                    AddEdgeLine(c, d, cs, cs.x * 0.5f + t * 0.5f, cs.y * 0.5f + t * 0.5f, t, wallColor);
                }
            }

            // 3) 닫힌 문 — 방 칸 또는 문 바깥 칸이 드러나 있으면 표시.
            //    칸 경계 전체 + 벽 두께만큼 양끝을 늘려 양옆 벽선과 틈 없이 이어진다 (= 확실히 닫힌 문)
            float dt = Mathf.Min(cs.x, cs.y) * doorThickness * _lineScale;
            foreach (var door in _data.doors)
            {
                if (!_revealed.Contains(door.roomCell) && !_revealed.Contains(door.corridorCell)) continue;
                Vector3 a = CellCenter(door.roomCell);
                Vector3 b = CellCenter(door.corridorCell);
                Vector3 mid = (a + b) * 0.5f;
                bool vertical = Mathf.Abs(a.x - b.x) > Mathf.Abs(a.y - b.y); // 좌우로 붙은 문 → 경계선은 세로
                if (vertical) AddRect(mid, dt * 0.5f, cs.y * doorLength * 0.5f + t * 0.5f, doorColor);
                else AddRect(mid, cs.x * doorLength * 0.5f + t * 0.5f, dt * 0.5f, doorColor);
            }

            // 4) 아이콘 — 드러난 칸에 있는 것만 (함정은 밟아서 발견한 것만)
            if (_revealed.Contains(_data.stairsPos)) AddStairsIcon(CellCenter(_data.stairsPos), cs, false, stairsColor);
            if (_data.hasStairsUp && _revealed.Contains(_data.stairsUpPos)) AddStairsIcon(CellCenter(_data.stairsUpPos), cs, true, stairsUpColor);
            if (_data.hasTownGate && _revealed.Contains(_data.townGatePos)) AddDiamond(CellCenter(_data.townGatePos), cs, townGateColor);

            foreach (var room in _data.rooms)
                if (room.lit && _revealed.Contains(room.brazierCell)) AddFlameIcon(CellCenter(room.brazierCell), cs);
            foreach (var p in _data.chests)
                if (_revealed.Contains(p)) AddChestIcon(CellCenter(p), cs, _state.openedChests.Contains(p));
            foreach (var p in _data.springs)
                if (_revealed.Contains(p)) AddSpringIcon(CellCenter(p), cs, _state.usedSprings.Contains(p));
            foreach (var p in _data.traps)
            {
                if (!_state.knownTraps.Contains(p)) continue;
                FloorTrapType type = _data.GetCell(p).trap;
                // 해제했거나, 한 번 쓰는 함정을 이미 밟았으면 회색. 찾기만 한 함정은 아직 살아 있다
                bool dead = _state.disarmedTraps.Contains(p) || (_state.sprungTraps.Contains(p) && IsTrapSpent(type));
                AddTrapIcon(CellCenter(p), cs, dead ? spentColor : TrapColor(type));
            }

            // 5) 안개 — 가 본 곳이지만 지금 시야 밖인 칸을 어둡게 덮는다 (벽선·아이콘까지 흐리게, 맨 위에 그림).
            //    벽이 있는 쪽으로만 벽선 두께 절반만큼 넓혀, 이웃한 밝은 칸은 건드리지 않는다.
            if (_visible != null && rememberedFogColor.a > 0f)
            {
                float half = t * 0.5f + 0.001f;
                foreach (var p in _revealed)
                {
                    if (_visible.Contains(p) || !_data.IsWalkable(p)) continue;
                    Vector3 c = CellCenter(p);
                    float left = cs.x * 0.5f + (_data.IsWalkable(p + Dirs[3]) ? 0f : half);
                    float right = cs.x * 0.5f + (_data.IsWalkable(p + Dirs[1]) ? 0f : half);
                    float up = cs.y * 0.5f + (_data.IsWalkable(p + Dirs[0]) ? 0f : half);
                    float down = cs.y * 0.5f + (_data.IsWalkable(p + Dirs[2]) ? 0f : half);
                    AddQuad(new Vector3(c.x - left, c.y - down, c.z), new Vector3(c.x - left, c.y + up, c.z),
                            new Vector3(c.x + right, c.y + up, c.z), new Vector3(c.x + right, c.y - down, c.z), rememberedFogColor);
                }
            }
        }

        _mesh.Clear();
        _mesh.indexFormat = _verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_tris, 0);
        _mesh.RecalculateBounds();
    }

    // ─────────────────────────────────────────
    // 좌표
    // ─────────────────────────────────────────

    /// <summary>격자 좌표(y 아래로 증가) → 월드 칸 중심. DungeonGridPlayer.UpdateWorldPosition 과 동일 규칙.</summary>
    public Vector3 CellCenter(Vector2Int p)
    {
        if (_reference != null) return _reference.GetCellCenterWorld(new Vector3Int(p.x, -p.y, 0));
        return new Vector3(p.x + 0.5f, -p.y + 0.5f, 0f);
    }

    public Vector2 CellSize()
    {
        if (_reference != null && _reference.layoutGrid != null)
        {
            Vector3 s = _reference.layoutGrid.cellSize;
            Vector3 scale = _reference.transform.lossyScale;
            return new Vector2(Mathf.Abs(s.x * scale.x), Mathf.Abs(s.y * scale.y));
        }
        return Vector2.one;
    }

    /// <summary>층 전체의 월드 사각형 (카메라 범위 제한용).</summary>
    public Rect FloorWorldRect()
    {
        if (_data == null) return new Rect();
        Vector2 cs = CellSize();
        Vector3 topLeft = CellCenter(new Vector2Int(0, 0));
        Vector3 bottomRight = CellCenter(new Vector2Int(_data.width - 1, _data.height - 1));
        float xMin = topLeft.x - cs.x * 0.5f;
        float xMax = bottomRight.x + cs.x * 0.5f;
        float yMax = topLeft.y + cs.y * 0.5f;
        float yMin = bottomRight.y - cs.y * 0.5f;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // ─────────────────────────────────────────
    // 도형
    // ─────────────────────────────────────────

    /// <summary>d = 0북 1동 2남 3서. 격자 북(y-1) = 월드 위(+y).</summary>
    private void AddEdgeLine(Vector3 c, int d, Vector2 cs, float halfLenX, float halfLenY, float t, Color color)
    {
        float hx = cs.x * 0.5f, hy = cs.y * 0.5f;
        switch (d)
        {
            case 0: AddRect(new Vector3(c.x, c.y + hy, c.z), halfLenX, t * 0.5f, color); break;
            case 2: AddRect(new Vector3(c.x, c.y - hy, c.z), halfLenX, t * 0.5f, color); break;
            case 1: AddRect(new Vector3(c.x + hx, c.y, c.z), t * 0.5f, halfLenY, color); break;
            case 3: AddRect(new Vector3(c.x - hx, c.y, c.z), t * 0.5f, halfLenY, color); break;
        }
    }

    private void AddRect(Vector3 center, float halfW, float halfH, Color color)
    {
        int i = _verts.Count;
        _verts.Add(new Vector3(center.x - halfW, center.y - halfH, center.z));
        _verts.Add(new Vector3(center.x - halfW, center.y + halfH, center.z));
        _verts.Add(new Vector3(center.x + halfW, center.y + halfH, center.z));
        _verts.Add(new Vector3(center.x + halfW, center.y - halfH, center.z));
        for (int k = 0; k < 4; k++) _colors.Add(color);
        _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
    }

    /// <summary>층 전체(가로·세로 모든 칸 경계)에 옅은 격자선. 드러나지 않은 곳에도 깔려 지도 방안처럼 보인다.</summary>
    private void AddFloorGrid(Vector2 cs)
    {
        float gt = Mathf.Min(cs.x, cs.y) * gridThickness;
        Rect r = FloorWorldRect();
        for (int x = 0; x <= _data.width; x++)
        {
            float wx = r.xMin + x * cs.x;
            AddRect(new Vector3(wx, r.center.y, 0f), gt * 0.5f, r.height * 0.5f, gridColor);
        }
        for (int y = 0; y <= _data.height; y++)
        {
            float wy = r.yMax - y * cs.y;
            AddRect(new Vector3(r.center.x, wy, 0f), r.width * 0.5f, gt * 0.5f, gridColor);
        }
    }

    /// <summary>
    /// 발견한 함정 중 다 써서 더는 작동하지 않는 것. 장치가 다시 감기는 가시·칼날·화염은 계속 작동한다
    /// (밟을 때마다 다시 발동 — 함정 해제로 없앨 수 있다).
    /// </summary>
    public static bool IsTrapSpent(FloorTrapType type)
    {
        return type != FloorTrapType.Spike && type != FloorTrapType.Blade && type != FloorTrapType.FlameVent;
    }

    private Color TrapColor(FloorTrapType type)
    {
        switch (type)
        {
            case FloorTrapType.Teleport: return teleportTrapColor;
            case FloorTrapType.Alarm: return alarmTrapColor;
            case FloorTrapType.Pitfall: return pitfallTrapColor;
            case FloorTrapType.PoisonDart: return poisonTrapColor;
            case FloorTrapType.Blade: return bladeTrapColor;
            case FloorTrapType.FlameVent: return flameTrapColor;
            case FloorTrapType.BlindingGas: return gasTrapColor;
            case FloorTrapType.Rockfall: return rockfallTrapColor;
            case FloorTrapType.Net: return netTrapColor;
            default: return spikeTrapColor;
        }
    }

    // 아이콘은 칸 중심 기준 -1~1 단위 좌표로 그린다 (s = 반지름, mirror = 좌우 반전).
    private static Vector3 P(Vector3 c, float s, float x, float y, bool mirror)
    {
        return new Vector3(c.x + (mirror ? -x : x) * s, c.y + y * s, c.z);
    }

    private void AddRectLocal(Vector3 c, float s, float x0, float y0, float x1, float y1, bool mirror, Color color)
    {
        AddQuad(P(c, s, x0, y0, mirror), P(c, s, x0, y1, mirror), P(c, s, x1, y1, mirror), P(c, s, x1, y0, mirror), color);
    }

    private void AddTriangle(Vector3 a, Vector3 b, Vector3 d, Color color)
    {
        int i = _verts.Count;
        _verts.Add(a); _verts.Add(b); _verts.Add(d);
        for (int k = 0; k < 3; k++) _colors.Add(color);
        // 스프라이트 셰이더는 양면(Cull Off)이라 감긴 방향(좌우 반전 포함)은 상관없다.
        // 반투명 색이 두 번 겹쳐 진해지므로 양면으로 두 번 넣지 않는다.
        _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
    }

    private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        AddTriangle(a, b, c, color);
        AddTriangle(a, c, d, color);
    }

    /// <summary>
    /// 계단: 3단 계단 실루엣 + 화살표.
    ///  올라가는 계단 = 오른쪽으로 올라가는 계단 + 위 화살표(왼쪽)
    ///  내려가는 계단 = 좌우 반전(오른쪽으로 내려가는 계단) + 아래 화살표(오른쪽)
    /// </summary>
    private void AddStairsIcon(Vector3 c, Vector2 cs, bool up, Color color)
    {
        float s = Mathf.Min(cs.x, cs.y) * stairsIconSize * 0.5f;
        bool mirror = !up;

        // 받침 (배경 그림 위에서도 잘 보이게)
        AddRectLocal(c, s, -1.05f, -1.05f, 1.05f, 1.05f, false, new Color(0f, 0f, 0f, 0.55f));

        // 계단 3단: x -0.2~1.0 을 3등분, 한 단 높이 0.55
        const float x0 = -0.2f, w = 0.4f, h = 0.55f, bottom = -0.9f;
        for (int i = 0; i < 3; i++)
            AddRectLocal(c, s, x0 + i * w, bottom, x0 + (i + 1) * w, bottom + (i + 1) * h, mirror, color);

        // 화살표: 계단이 없는 빈 쪽 (x ≈ -0.62)
        const float ax = -0.62f;
        if (up)
        {
            AddRectLocal(c, s, ax - 0.11f, -0.85f, ax + 0.11f, 0.25f, mirror, color);
            AddTriangle(P(c, s, ax - 0.34f, 0.2f, mirror), P(c, s, ax, 0.92f, mirror), P(c, s, ax + 0.34f, 0.2f, mirror), color);
        }
        else
        {
            AddRectLocal(c, s, ax - 0.11f, -0.25f, ax + 0.11f, 0.85f, mirror, color);
            AddTriangle(P(c, s, ax - 0.34f, -0.2f, mirror), P(c, s, ax, -0.92f, mirror), P(c, s, ax + 0.34f, -0.2f, mirror), color);
        }
    }

    /// <summary>보물상자: 몸통 + 뚜껑 + 자물쇠. 연 상자는 회색에 뚜껑이 들린 모양.</summary>
    private void AddChestIcon(Vector3 c, Vector2 cs, bool opened)
    {
        float s = Mathf.Min(cs.x, cs.y) * iconSize * 0.5f;
        Color body = opened ? spentColor : chestColor;
        Color dark = new Color(0.25f, 0.15f, 0.05f, 1f);
        AddRectLocal(c, s, -0.8f, -0.65f, 0.8f, 0.15f, false, body);
        if (opened)
        {
            AddRectLocal(c, s, -0.7f, 0.0f, 0.7f, 0.15f, false, new Color(0.1f, 0.1f, 0.1f, 1f)); // 빈 속
            AddRectLocal(c, s, -0.8f, 0.45f, 0.8f, 0.75f, false, body);                           // 들린 뚜껑
        }
        else
        {
            AddRectLocal(c, s, -0.8f, 0.22f, 0.8f, 0.6f, false, Color.Lerp(body, Color.white, 0.25f));
            AddRectLocal(c, s, -0.12f, -0.15f, 0.12f, 0.32f, false, dark);
        }
    }

    /// <summary>회복의 샘: 파란 원 + 밝은 속. 쓴 샘은 회색 원 + 어두운 속.</summary>
    private void AddSpringIcon(Vector3 c, Vector2 cs, bool used)
    {
        float s = Mathf.Min(cs.x, cs.y) * iconSize * 0.5f;
        AddCircle(c, s * 0.85f, used ? spentColor : springColor);
        AddCircle(c, s * 0.42f, used ? new Color(0.12f, 0.12f, 0.14f, 1f) : new Color(0.8f, 0.93f, 1f, 1f));
    }

    /// <summary>벽 화로: 바깥 불꽃(주황 물방울) + 안쪽 불꽃(노랑) + 받침.</summary>
    private void AddFlameIcon(Vector3 c, Vector2 cs)
    {
        float s = Mathf.Min(cs.x, cs.y) * iconSize * 0.5f;
        AddRectLocal(c, s, -0.45f, -0.9f, 0.45f, -0.7f, false, new Color(0.35f, 0.25f, 0.2f, 1f));   // 받침
        AddTriangle(P(c, s, -0.55f, -0.6f, false), P(c, s, 0f, 0.95f, false), P(c, s, 0.55f, -0.6f, false), brazierColor);
        AddCircle(P(c, s, 0f, -0.45f, false), s * 0.5f, brazierColor);
        AddTriangle(P(c, s, -0.28f, -0.5f, false), P(c, s, 0f, 0.4f, false), P(c, s, 0.28f, -0.5f, false), brazierCoreColor);
    }

    /// <summary>함정: ✕ 표시.</summary>
    private void AddTrapIcon(Vector3 c, Vector2 cs, Color color)
    {
        float s = Mathf.Min(cs.x, cs.y) * iconSize * 0.5f;
        const float t = 0.2f; // 획 두께의 절반 (단위 좌표)
        AddQuad(P(c, s, -0.8f - t, -0.8f + t, false), P(c, s, 0.8f - t, 0.8f + t, false),
                P(c, s, 0.8f + t, 0.8f - t, false), P(c, s, -0.8f + t, -0.8f - t, false), color);
        AddQuad(P(c, s, -0.8f - t, 0.8f - t, false), P(c, s, 0.8f - t, -0.8f - t, false),
                P(c, s, 0.8f + t, -0.8f + t, false), P(c, s, -0.8f + t, 0.8f + t, false), color);
    }

    private void AddCircle(Vector3 c, float r, Color color)
    {
        const int segments = 18;
        int center = _verts.Count;
        _verts.Add(c);
        _colors.Add(color);
        for (int k = 0; k <= segments; k++)
        {
            float a = k * Mathf.PI * 2f / segments;
            _verts.Add(new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, c.z));
            _colors.Add(color);
        }
        for (int k = 0; k < segments; k++)
        {
            _tris.Add(center); _tris.Add(center + k + 2); _tris.Add(center + k + 1);
        }
    }

    private void AddDiamond(Vector3 c, Vector2 cs, Color color)
    {
        float s = Mathf.Min(cs.x, cs.y) * iconSize * 0.5f;
        int i = _verts.Count;
        _verts.Add(new Vector3(c.x, c.y + s, c.z));
        _verts.Add(new Vector3(c.x + s, c.y, c.z));
        _verts.Add(new Vector3(c.x, c.y - s, c.z));
        _verts.Add(new Vector3(c.x - s, c.y, c.z));
        for (int k = 0; k < 4; k++) _colors.Add(color);
        _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
    }

    private static Material CreateDefaultMaterial()
    {
        string[] shaders =
        {
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Sprites/Default",
            "UI/Default",
        };
        foreach (var name in shaders)
        {
            Shader s = Shader.Find(name);
            if (s != null) return new Material(s) { name = "Automap (" + name + ")" };
        }
        Debug.LogError("[AutomapRenderer] 사용할 수 있는 셰이더를 찾지 못했습니다. Material 필드에 직접 지정하세요.");
        return null;
    }
}
