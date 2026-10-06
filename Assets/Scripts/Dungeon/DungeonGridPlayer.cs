using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum DungeonDirection { North, East, South, West }

/// <summary>
/// 던전 격자 이동 (탑다운, 절대 방향 N/E/S/W).
///  - 한 칸씩 부드럽게 미끄러지듯 이동 (논리 위치는 즉시 바뀌고, 그림만 따라간다)
///  - 꾹 누르면 연속 이동 (키보드·화면 이동 패드 모두). 새 방 입장·계단·보물 등 일이 생기면 자동으로 멈춘다
///  - 벽에 부딪히면 살짝 튕기는 반응
///  - 빨간 사각형 대신 바라보는 방향을 가리키는 화살표 표시
///  - 전체 지도·확인창 등이 열려 있으면 이동 잠금 (LockInput / UnlockInput)
/// </summary>
public class DungeonGridPlayer : MonoBehaviour
{
    [Header("References")]
    public Tilemap wallTilemap;  // 층 데이터가 없을 때만 쓰는 옛 충돌 검사용
    public Tilemap floorTilemap; // 칸 좌표 → 월드 좌표 정렬용
    public MapManager mapManager; // 층 데이터(이동 판정·지도 공개·계단) 참조
    public Vector2Int gridPos = new Vector2Int(2, 2);
    public DungeonDirection facing = DungeonDirection.North;

    [Header("Movement Feel")]
    [Tooltip("칸 사이를 미끄러지는 시간 (초)")]
    public float stepDuration = 0.1f;
    [Tooltip("꾹 누를 때 연속 이동이 시작되기까지 (초)")]
    public float holdDelay = 0.28f;
    [Tooltip("연속 이동 간격 (초). stepDuration 보다 짧으면 끊겨 보인다")]
    public float holdInterval = 0.13f;
    [Tooltip("벽에 부딪혔을 때 튕기는 거리 (칸 크기 비율)")]
    public float bumpDistance = 0.18f;
    public float bumpDuration = 0.14f;

    [Header("Marker")]
    [Tooltip("플레이어 표시를 방향 화살표로 바꾼다 (기존 스프라이트는 숨김)")]
    public bool useFacingArrow = true;
    public Color arrowColor = new Color(1f, 0.3f, 0.22f, 1f);
    [Tooltip("화살표 크기 (칸 크기 비율)")]
    public float arrowSize = 0.85f;

    [Header("Minimap Settings (옛 UI 커서, 연결 안 되어 있으면 무시)")]
    public RectTransform playerCursor;
    public float minimapCellSize = 40f;
    public Vector2 minimapOrigin = new Vector2(-520, -940);

    // ── 이동 잠금 (전체 지도, 확인창 등) ──
    private readonly HashSet<object> _locks = new HashSet<object>();
    public bool IsInputLocked => _locks.Count > 0;

    // ── 꾹 누르기 ──
    private bool _holding;
    private DungeonDirection _holdDir;
    private float _nextRepeatTime;

    // ── 그림 이동 ──
    private Vector3 _visualFrom, _visualTo;
    private float _animT = 1f;
    private float _animDuration;
    private bool _animIsBump;
    private Vector3 _bumpOffset;

    private Transform _arrow;

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

        if (useFacingArrow) SetupFacingArrow();
        SnapToGrid();
        UpdateView();
        // 층 생성·배치·지도 공개는 MapManager 가 담당한다 (층 데이터 기준).
    }

    void Update()
    {
        // 키보드: 누르는 순간 한 칸, 계속 누르고 있으면 연속 이동
        ReadKey(DungeonDirection.North, KeyCode.UpArrow, KeyCode.W);
        ReadKey(DungeonDirection.South, KeyCode.DownArrow, KeyCode.S);
        ReadKey(DungeonDirection.West, KeyCode.LeftArrow, KeyCode.A);
        ReadKey(DungeonDirection.East, KeyCode.RightArrow, KeyCode.D);

        if (_holding && Time.time >= _nextRepeatTime)
        {
            _nextRepeatTime = Time.time + holdInterval;
            TryMoveTo(_holdDir, true);
        }

        AnimateVisual();
    }

    private void ReadKey(DungeonDirection dir, KeyCode a, KeyCode b)
    {
        if (Input.GetKeyDown(a) || Input.GetKeyDown(b)) BeginHold(dir);
        else if (Input.GetKeyUp(a) || Input.GetKeyUp(b)) EndHold(dir);
    }

    // ─────────────────────────────────────────
    // 입력 API (화면 버튼 · 이동 패드)
    // ─────────────────────────────────────────

    // 한 번 누르면 한 칸 (예전 버튼 연결 호환)
    public void MoveNorth() => TryMoveTo(DungeonDirection.North, false);
    public void MoveSouth() => TryMoveTo(DungeonDirection.South, false);
    public void MoveWest()  => TryMoveTo(DungeonDirection.West, false);
    public void MoveEast()  => TryMoveTo(DungeonDirection.East, false);

    /// <summary>누르기 시작: 즉시 한 칸 + 계속 누르고 있으면 연속 이동.</summary>
    public void BeginHold(DungeonDirection dir)
    {
        _holding = true;
        _holdDir = dir;
        _nextRepeatTime = Time.time + holdDelay;
        TryMoveTo(dir, false);
    }

    /// <summary>손을 뗌. 다른 방향을 누르는 중이면 무시.</summary>
    public void EndHold(DungeonDirection dir)
    {
        if (_holding && _holdDir == dir) _holding = false;
    }

    /// <summary>연속 이동을 멈춘다 (새 방 입장, 계단, 보물, 함정, 확인창 등). 다시 누르면 이어서 이동.</summary>
    public void InterruptHold()
    {
        _holding = false;
    }

    public void LockInput(object owner)
    {
        if (owner == null) return;
        _locks.Add(owner);
        InterruptHold();
    }

    public void UnlockInput(object owner)
    {
        if (owner != null) _locks.Remove(owner);
    }

    // ─────────────────────────────────────────
    // 이동
    // ─────────────────────────────────────────

    private void TryMoveTo(DungeonDirection dir, bool isRepeat)
    {
        if (IsInputLocked) return;
        EnsureReferences();

        facing = dir;
        UpdateArrowRotation();

        Vector2Int nextPos = gridPos + GetDirVector(dir);
        if (!CanWalk(nextPos))
        {
            // 연속 이동 중 벽에 닿으면 조용히 멈춤, 직접 누른 경우에만 튕김
            if (isRepeat) InterruptHold();
            else PlayBump(dir);
            UpdateView();
            return;
        }

        // 함정 직전 (탐험 스킬 — 함정 감지 멈춤 / 함정 해제 선택창). 막혔으면 제자리
        if (mapManager != null && mapManager.BeforeStep(nextPos, () => CompleteStep(nextPos)))
        {
            InterruptHold();
            UpdateView();
            return;
        }

        CompleteStep(nextPos);
    }

    /// <summary>실제로 한 칸 들어간다 (함정 해제 창에서 "그냥 지나가기"를 골랐을 때도 여기로).</summary>
    private void CompleteStep(Vector2Int nextPos)
    {
        gridPos = nextPos;
        StartSlide();
        UpdateView();

        // 지도 공개 + 계단·보물·함정 등 처리. 무슨 일이 있었으면 이번 걸음은 인카운터 판정하지 않는다.
        if (mapManager != null && mapManager.OnPlayerEntered(gridPos)) return;

        // 랜덤 인카운터 체크 (이동에 성공했을 때만 1회)
        DungeonEncounter.Instance?.CheckEncounter(gridPos);
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

    /// <summary>순간 이동 (층 배치·복원·전송 함정용). 인카운터·계단 판정은 하지 않는다. 지도 공개는 MapManager 가 한다.</summary>
    public void Teleport(Vector2Int pos)
    {
        EnsureReferences();
        gridPos = pos;
        SnapToGrid();
        UpdateView();
        Debug.Log($"[Player] Teleported to ({pos.x}, {pos.y})");
    }

    // ─────────────────────────────────────────
    // 그림 (미끄러짐 · 튕김 · 화살표)
    // ─────────────────────────────────────────

    private Vector3 GridToWorld(Vector2Int p)
    {
        Tilemap positionTilemap = floorTilemap != null ? floorTilemap : wallTilemap;
        if (positionTilemap != null) return positionTilemap.GetCellCenterWorld(new Vector3Int(p.x, -p.y, 0));
        // 타일맵을 못 찾았을 때의 안전한 기본값(셀 크기 1, y 반전)
        return new Vector3(p.x, -p.y, 0);
    }

    private Vector2 CellSize()
    {
        Tilemap t = floorTilemap != null ? floorTilemap : wallTilemap;
        if (t != null && t.layoutGrid != null)
        {
            Vector3 s = t.layoutGrid.cellSize;
            return new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.y));
        }
        return Vector2.one;
    }

    private void SnapToGrid()
    {
        EnsureReferences();
        transform.position = GridToWorld(gridPos);
        _visualTo = transform.position;
        _animT = 1f;
    }

    private void StartSlide()
    {
        _visualFrom = transform.position; // 이전 이동이 끝나기 전이어도 지금 보이는 곳에서 이어 간다
        _visualTo = GridToWorld(gridPos);
        _animIsBump = false;
        _animDuration = Mathf.Max(0.001f, stepDuration);
        _animT = stepDuration > 0f ? 0f : 1f;
        if (_animT >= 1f) transform.position = _visualTo;
    }

    private void PlayBump(DungeonDirection dir)
    {
        _visualTo = GridToWorld(gridPos);
        Vector2Int v = GetDirVector(dir);
        float d = Mathf.Min(CellSize().x, CellSize().y) * bumpDistance;
        _bumpOffset = new Vector3(v.x * d, -v.y * d, 0f); // 격자 북(y-1) = 월드 위(+y)
        _animIsBump = true;
        _animDuration = Mathf.Max(0.001f, bumpDuration);
        _animT = 0f;
    }

    private void AnimateVisual()
    {
        if (_animT >= 1f) return;
        _animT = Mathf.Min(1f, _animT + Time.deltaTime / _animDuration);
        if (_animIsBump)
        {
            transform.position = _visualTo + _bumpOffset * Mathf.Sin(_animT * Mathf.PI);
        }
        else
        {
            float e = 1f - (1f - _animT) * (1f - _animT); // 감속
            transform.position = Vector3.Lerp(_visualFrom, _visualTo, e);
        }
    }

    /// <summary>기존 사각형 스프라이트를 숨기고, 방향 화살표 자식 오브젝트를 만든다.</summary>
    private void SetupFacingArrow()
    {
        if (_arrow != null) return;
        EnsureReferences();

        var baseRenderer = GetComponent<SpriteRenderer>();
        int order = baseRenderer != null ? baseRenderer.sortingOrder : 10;
        string layer = baseRenderer != null ? baseRenderer.sortingLayerName : "Default";
        if (baseRenderer != null) baseRenderer.enabled = false;

        var go = new GameObject("FacingArrow");
        _arrow = go.transform;
        _arrow.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DungeonIconSprites.Arrow;
        sr.color = arrowColor;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order + 1;

        // 스프라이트는 1 x 1 월드 단위 → 부모 크기를 상쇄해 칸 크기 비율로 맞춘다
        Vector3 parentScale = transform.lossyScale;
        float size = Mathf.Min(CellSize().x, CellSize().y) * arrowSize;
        _arrow.localScale = new Vector3(
            size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        UpdateArrowRotation();
    }

    private void UpdateArrowRotation()
    {
        if (_arrow == null) return;
        _arrow.localRotation = Quaternion.Euler(0, 0, FacingAngle(facing));
    }

    private static float FacingAngle(DungeonDirection d)
    {
        switch (d)
        {
            case DungeonDirection.East: return -90f;
            case DungeonDirection.South: return 180f;
            case DungeonDirection.West: return 90f;
            default: return 0f;
        }
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

    private void UpdateView()
    {
        UpdateArrowRotation();

        // Update Minimap Cursor
        if (playerCursor != null)
        {
            // Position: Grid * Size
            playerCursor.anchoredPosition = minimapOrigin + new Vector2(gridPos.x * minimapCellSize, gridPos.y * minimapCellSize);
            playerCursor.localRotation = Quaternion.Euler(0, 0, FacingAngle(facing));
        }
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
