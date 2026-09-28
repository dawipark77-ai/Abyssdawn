using UnityEngine;
using UnityEngine.Tilemaps;

public enum DungeonDirection { North, East, South, West }

public class DungeonGridPlayer : MonoBehaviour
{
    [Header("References")]
    public Tilemap wallTilemap;  // 층 데이터가 없을 때만 쓰는 옛 충돌 검사용
    public Tilemap floorTilemap; // 칸 좌표 → 월드 좌표 정렬용
    public MapManager mapManager; // 층 데이터(이동 판정·지도 공개·계단) 참조
    public Vector2Int gridPos = new Vector2Int(2, 2);
    public DungeonDirection facing = DungeonDirection.North;
    [HideInInspector] public bool inputLocked; // 전체 지도 등이 열려 있는 동안 이동 금지

    [Header("Minimap Settings")]
    public RectTransform playerCursor;
    public float minimapCellSize = 40f; 
    public Vector2 minimapOrigin = new Vector2(-520, -940);

    void Start()
    {
        EnsureReferences();
        
        // [SO] PlayerStats가 스스로 statData를 로드하므로 GameManager 연동 제거
        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<PlayerStats>();
            Debug.Log("[DungeonGridPlayer] PlayerStats component was missing. Auto-added.");
        }

        UpdateWorldPosition();
        UpdateView();
        // 층 생성·배치·지도 공개는 MapManager 가 담당한다 (층 데이터 기준).
    }

    void Update()
    {
        // Absolute Cardinal Movement
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveNorth();
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveSouth();
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveWest();
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveEast();
    }

    // Public methods for UI Buttons (Cardinal)
    public void MoveNorth() => TryMoveTo(DungeonDirection.North);
    public void MoveSouth() => TryMoveTo(DungeonDirection.South);
    public void MoveWest()  => TryMoveTo(DungeonDirection.West);
    public void MoveEast()  => TryMoveTo(DungeonDirection.East);

    private void TryMoveTo(DungeonDirection dir)
    {
        if (inputLocked) return;
        EnsureReferences();

        facing = dir;

        Vector2Int nextPos = gridPos + GetDirVector(dir);
        if (!CanWalk(nextPos))
        {
            Debug.Log($"[Player] 이동 불가: 막힌 칸 ({nextPos.x}, {nextPos.y})");
            UpdateView();
            return;
        }

        gridPos = nextPos;
        UpdateWorldPosition();
        UpdateView();

        // 지도 공개 + 계단·마을 입구 처리. 층을 떠났으면 인카운터 판정하지 않는다.
        if (mapManager != null && mapManager.OnPlayerEntered(gridPos)) return;

        // 랜덤 인카운터 체크 (이동에 성공했을 때만 1회)
        DungeonEncounter.Instance?.CheckEncounter(gridPos);

        Debug.Log($"[Player] Moved to ({gridPos.x}, {gridPos.y}) | Facing: {facing}");
    }

    /// <summary>이동 가능 여부는 층 데이터로 판정. 층 데이터가 없을 때만 옛 벽 타일맵을 본다.</summary>
    private bool CanWalk(Vector2Int pos)
    {
        if (mapManager != null && mapManager.FloorData != null)
            return mapManager.FloorData.IsWalkable(pos);

        if (mapManager != null && (pos.x < 0 || pos.y < 0 || pos.x >= mapManager.width || pos.y >= mapManager.height))
            return false;
        return wallTilemap == null || !wallTilemap.HasTile(new Vector3Int(pos.x, -pos.y, 0));
    }

    private Vector2Int GetDirVector(DungeonDirection dir)
    {
        switch (dir)
        {
            case DungeonDirection.North: return new Vector2Int(0, -1);
            case DungeonDirection.East: return new Vector2Int(1, 0);
            case DungeonDirection.South: return new Vector2Int(0, 1);
            case DungeonDirection.West: return new Vector2Int(-1, 0);
        }
        return Vector2Int.zero;
    }

    public Vector2Int GetForwardVector()
    {
        return GetDirVector(facing);
    }

    /// <summary>
    /// 이동/텔레포트 전에 필요한 참조를 자동으로 채워 그리드 정렬이 어긋나지 않도록 함.
    /// </summary>
    private void EnsureReferences()
    {
        if (mapManager == null)
        {
            mapManager = FindFirstObjectByType<MapManager>();
        }

        if (wallTilemap == null)
        {
            if (mapManager != null) wallTilemap = mapManager.wallTilemap;

            if (wallTilemap == null)
            {
                GameObject gridObj = GameObject.Find("Grid");
                if (gridObj != null)
                {
                    Transform wallTilemapTransform = gridObj.transform.Find("Wall");
                    if (wallTilemapTransform != null)
                    {
                        wallTilemap = wallTilemapTransform.GetComponent<Tilemap>();
                    }
                }
            }
        }

        if (floorTilemap == null)
        {
            if (mapManager != null) floorTilemap = mapManager.floorTilemap;
            else
            {
                var mgr = FindFirstObjectByType<MapManager>();
                if (mgr != null) floorTilemap = mgr.floorTilemap;
            }

            if (floorTilemap == null)
            {
                GameObject gridObj = GameObject.Find("Grid");
                if (gridObj != null)
                {
                    Transform floorTilemapTransform = gridObj.transform.Find("Floor");
                    if (floorTilemapTransform != null)
                    {
                        floorTilemap = floorTilemapTransform.GetComponent<Tilemap>();
                    }
                }
            }
        }
    }

    void UpdateWorldPosition()
    {
        EnsureReferences();

        // 타일맵의 셀 센터에 정렬 (Grid Cell Size 및 Offset을 자동 반영)
        Tilemap positionTilemap = floorTilemap != null ? floorTilemap : wallTilemap;
        if (positionTilemap != null)
        {
            Vector3Int cell = new Vector3Int(gridPos.x, -gridPos.y, 0);
            transform.position = positionTilemap.GetCellCenterWorld(cell);
        }
        else
        {
            // 타일맵을 못 찾았을 때의 안전한 기본값(셀 크기 1, y 반전)
            transform.position = new Vector3(gridPos.x, -gridPos.y, 0);
        }
    }

    private void UpdateView()
    {
        // Update Minimap Cursor
        if (playerCursor != null)
        {
            // Position: Grid * Size
            playerCursor.anchoredPosition = minimapOrigin + new Vector2(gridPos.x * minimapCellSize, gridPos.y * minimapCellSize);
            
            // Rotation: North=0, East=-90, South=180, West=90 (assuming Sprite points UP)
            float zRot = 0;
            switch (facing)
            {
                case DungeonDirection.North: zRot = 0; break;
                case DungeonDirection.East:  zRot = -90; break;
                case DungeonDirection.South: zRot = 180; break;
                case DungeonDirection.West:  zRot = 90; break;
            }
            playerCursor.localRotation = Quaternion.Euler(0, 0, zRot);
        }
    }

    /// <summary>순간 이동 (층 배치·복원용). 인카운터·계단 판정은 하지 않는다. 지도 공개는 MapManager 가 한다.</summary>
    public void Teleport(Vector2Int pos)
    {
        EnsureReferences();
        gridPos = pos;
        UpdateWorldPosition();
        UpdateView();
        Debug.Log($"[Player] Teleported to ({pos.x}, {pos.y})");
    }

    private void OnDrawGizmos()
    {
        // Draw Player
        Gizmos.color = Color.green;
        Vector3 pPos = new Vector3(gridPos.x, 0, gridPos.y);
        Gizmos.DrawSphere(pPos, 0.5f);
        
        Vector2Int fwd = GetForwardVector();
        Gizmos.DrawLine(pPos, pPos + new Vector3(fwd.x, 0, fwd.y));
    }
}
