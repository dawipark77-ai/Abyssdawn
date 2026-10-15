using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [2026-10-15] 픽셀 던전처럼 지도 칸을 누르면 그 칸까지 걸어간다 (모바일).
///  - 이미 드러난 칸만 목표가 되고, 드러난 걸을 수 있는 칸으로만 최단 경로(BFS). 아는 함정은 피해서 간다 (목표가 함정이면 그 칸만 예외)
///  - 한 칸씩 보통 이동과 똑같이 걷는다 → 인카운터·함정·상자·계단 확인 등 무슨 일이 생기면 그 자리에서 멈춤
///  - 걷는 중에 다시 누르면 새 목표로, 같은 칸을 누르면 멈춤
///  - 창(메뉴·확인창)이 열려 있거나 UI 를 누른 것이면 무시
/// DungeonGridPlayer 가 Start 에서 자기에게 붙인다.
/// </summary>
public class DungeonTapToMove : MonoBehaviour
{
    public float stepInterval = 0.14f;
    [Tooltip("손가락이 이만큼(픽셀) 넘게 움직이면 누름이 아니라 끌기로 본다")]
    public float dragThreshold = 25f;

    private DungeonGridPlayer _player;
    private Coroutine _walk;
    private Vector2 _downPos;
    private bool _down, _downOverUi;
    private Vector2Int _target;

    private void Awake() { _player = GetComponent<DungeonGridPlayer>(); }

    private void Update()
    {
        if (_player == null) return;
        if (Input.GetMouseButtonDown(0))
        {
            _down = true; _downPos = Input.mousePosition;
            _downOverUi = EventSystem.current != null && (EventSystem.current.IsPointerOverGameObject() ||
                          (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)));
        }
        if (Input.GetMouseButtonUp(0) && _down)
        {
            _down = false;
            if (_downOverUi || ((Vector2)Input.mousePosition - _downPos).magnitude > dragThreshold) return;
            OnTap(Input.mousePosition);
        }
    }

    private bool Blocked()
    {
        if (_player.IsInputLocked || EncounterTransition.IsPlaying) return true;
        if (DungeonPanelGroup.Instance != null && DungeonPanelGroup.Instance.AnyOpen) return true;
        var hud = DungeonHud.Instance;
        if (hud != null && (hud.IsDialogOpen || hud.IsTownOpen || hud.IsShopOpen)) return true;
        var full = FindFirstObjectByType<DungeonFullMap>();
        return full != null && full.IsOpen;
    }

    private void OnTap(Vector3 screen)
    {
        if (Blocked()) return;
        var mm = _player.mapManager;
        if (mm == null || mm.FloorData == null || mm.FloorState == null || _player.floorTilemap == null || Camera.main == null) return;
        Vector3 world = Camera.main.ScreenToWorldPoint(screen); world.z = 0f;
        Vector3Int c = _player.floorTilemap.WorldToCell(world);
        var cell = new Vector2Int(c.x, -c.y);
        if (_walk != null && cell == _target) { Stop(); return; } // 같은 칸 다시 누름 → 멈춤
        if (cell == _player.gridPos) return;
        if (!mm.FloorData.IsWalkable(cell) || !mm.FloorState.revealed.Contains(cell)) return;
        var path = FindPath(mm, _player.gridPos, cell);
        if (path == null) return;
        Stop();
        _target = cell;
        _walk = StartCoroutine(Walk(path));
    }

    public void Stop()
    {
        if (_walk != null) StopCoroutine(_walk);
        _walk = null;
    }

    private IEnumerator Walk(List<Vector2Int> path)
    {
        foreach (var next in path)
        {
            if (Blocked()) break;
            Vector2Int before = _player.gridPos;
            Vector2Int d = next - before;
            if (Mathf.Abs(d.x) + Mathf.Abs(d.y) != 1) break; // 경로가 어긋남 (순간이동 함정 등)
            DungeonDirection dir = d.x > 0 ? DungeonDirection.East : d.x < 0 ? DungeonDirection.West : d.y > 0 ? DungeonDirection.South : DungeonDirection.North;
            _player.StepOnce(dir);
            if (_player.gridPos != next) break;   // 못 갔음 (함정 확인창 등)
            yield return new WaitForSeconds(stepInterval);
            if (Blocked()) break;                 // 인카운터·확인창·상자 등 무슨 일이 생김
        }
        _walk = null;
    }

    /// <summary>드러난 걸을 수 있는 칸만 지나는 최단 경로 (시작 칸 제외, 목표 포함). 아는 함정은 목표가 아니면 피한다.</summary>
    private static List<Vector2Int> FindPath(MapManager mm, Vector2Int from, Vector2Int to)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var q = new Queue<Vector2Int>();
        q.Enqueue(from); prev[from] = from;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            if (p == to) break;
            foreach (var d in dirs)
            {
                var n = p + d;
                if (prev.ContainsKey(n)) continue;
                if (!mm.FloorData.IsWalkable(n) || !mm.FloorState.revealed.Contains(n)) continue;
                if (n != to && mm.FloorState.knownTraps.Contains(n) && !mm.FloorState.disarmedTraps.Contains(n)) continue;
                prev[n] = p; q.Enqueue(n);
            }
        }
        if (!prev.ContainsKey(to)) return null;
        var path = new List<Vector2Int>();
        for (var p = to; p != from; p = prev[p]) path.Add(p);
        path.Reverse();
        return path;
    }
}
