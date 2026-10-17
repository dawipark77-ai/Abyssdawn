using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탐색 (픽셀 던전·이상한 던전식). 서치 버튼(이름 "Search_Button")을 누르면 플레이어 주변으로 탐색 물결이 퍼지고,
/// 반경 안의 숨겨진 것(지금은 모르는 함정)을 찾아 자동 지도에 드러낸다.
///  - 범위: 기본 1칸, 함정 탐지 Lv2/3/5 → 2/3/4칸 (FieldSkills.SearchRadius, 2026-10-15). 시야와 같은 규칙 — 벽·문 너머는 찾지 못함.
///  - [2026-10-15] 턴을 쓰지 않는다 (costsTurn 기본 false) — 서치로 인카운터가 나지 않게
///    걸음 회복·빛 소모도 한 걸음만큼 진행. 무한 연타를 막는 대가.
///  - 물결이 도는 동안은 이동이 잠긴다.
/// MapManager 가 실행 시 자동으로 붙이고 버튼을 이름으로 찾아 연결한다. 키보드 테스트: F 키.
/// </summary>
public class DungeonSearch : MonoBehaviour
{
    public const string DefaultButtonName = "Search_Button";
    public const string HerbResource = "Item_Equipments/Items/Medicinal_Herb";

    [Header("탐색")]
    [Tooltip("탐색 반경 (칸)")]
    public int radius = 1; // (표시용 — 실제 반경은 FieldSkills.SearchRadius)
    [Tooltip("탐색하면 한 턴이 지난다 (위험도 상승·인카운터 판정, 걸음 회복·빛 소모 한 걸음)")]
    public bool costsTurn = false;

    [Header("연출")]
    [Tooltip("물결이 반경 끝까지 퍼지는 시간 (초)")]
    public float rippleSeconds = 0.5f;
    [Tooltip("물결 개수 (조금씩 늦게 따라 퍼짐)")]
    public int rippleCount = 2;
    public Color rippleColor = new Color(0.62f, 0.86f, 1f, 0.9f);
    [Tooltip("찾은 함정 위에서 번쩍이는 표시 색")]
    public Color foundColor = new Color(1f, 0.4f, 0.3f, 1f);
    [Tooltip("물결 선 굵기 (칸 크기 대비)")]
    public float lineWidth = 0.08f;
    [Tooltip("지도(5) 위, 플레이어(10) 아래")]
    public int sortingOrder = 8;

    private MapManager _map;
    private DungeonGridPlayer _player;
    private bool _busy;
    private Material _lineMaterial;
    private readonly List<Vector2Int> _found = new List<Vector2Int>();

    public void Setup(MapManager map, string buttonName)
    {
        _map = map;
        _player = FindFirstObjectByType<DungeonGridPlayer>();

        Button button = null;
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.name == buttonName) { button = b; break; }
        if (button != null)
        {
            button.onClick.RemoveListener(Search);
            button.onClick.AddListener(Search);
            Debug.Log($"[DungeonSearch] '{buttonName}' 버튼 연결 (반경 {FieldSkills.SearchRadius()}칸)");
        }
        else Debug.LogWarning($"[DungeonSearch] '{buttonName}' 버튼을 찾지 못했습니다. F 키로만 탐색할 수 있습니다.");
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Q)) Search(); // [2026-10-17] Q 키로도 서치
    }
#endif

    private void OnDestroy()
    {
        if (_lineMaterial != null) Destroy(_lineMaterial);
    }

    /// <summary>서치 버튼. 이동 잠금(확인창·전환 연출 등) 중이거나 전체 지도가 열려 있으면 무시.</summary>
    public void Search()
    {
        if (_busy || _map == null || _map.FloorData == null) return;
        if (_player == null) _player = FindFirstObjectByType<DungeonGridPlayer>();
        if (_player == null || _player.IsInputLocked || EncounterTransition.IsPlaying) return;
        var fullMap = FindFirstObjectByType<DungeonFullMap>();
        if (fullMap != null && fullMap.IsOpen) return;
        StartCoroutine(SearchRoutine());
    }

    private IEnumerator SearchRoutine()
    {
        _busy = true;
        _player.LockInput(this);
        Vector2Int pos = _player.gridPos;
        int searchR = FieldSkills.SearchRadius(); // [2026-10-15] 기본 1칸, 함정 탐지 등급으로 2·3·4칸 (성향 보너스 없음)
        _map.FindHiddenAround(pos, searchR, _found);

        // 1) 탐색 물결 — [2026-10-14] 레트로 느낌: 바닥 칸 크기의 사각 타일이 한 칸씩 사각 링으로 퍼진다.
        //    칸마다 계단식으로(부드럽지 않게) 밝아졌다 사라진다. 벽(바위) 칸에는 그리지 않음.
        AutomapRenderer automap = _map.automapRenderer;
        Vector3 center = automap != null ? automap.CellCenter(pos) : _player.transform.position;
        Vector2 cellSize = automap != null ? automap.CellSize() : Vector2.one;
        float ringStep = rippleSeconds / Mathf.Max(1, searchR);
        var tiles = new List<SpriteRenderer>();
        var tileRing = new List<int>();
        for (int dy = -searchR; dy <= searchR; dy++)
            for (int dx = -searchR; dx <= searchR; dx++)
            {
                int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                if (d == 0) continue;
                var c = new Vector2Int(pos.x + dx, pos.y + dy);
                if (!_map.FloorData.InBounds(c) || !_map.FloorData.IsWalkable(c)) continue;
                Vector3 at = automap != null ? automap.CellCenter(c) : center + new Vector3(dx * cellSize.x, dy * cellSize.y, 0f);
                tiles.Add(NewTile("SearchTile", at, cellSize * 0.86f, rippleColor));
                tileRing.Add(d);
            }

        float total = ringStep * searchR + TileSteps.Length * TileStepSeconds;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                float local = t - (tileRing[i] - 1) * ringStep;
                int step = local < 0f ? -1 : Mathf.FloorToInt(local / TileStepSeconds);
                float a = step < 0 || step >= TileSteps.Length ? 0f : TileSteps[step] * (1f - 0.12f * (tileRing[i] - 1));
                tiles[i].color = Alpha(rippleColor, a);
            }
            yield return null;
        }
        foreach (var r in tiles) Destroy(r.gameObject);

        // 2) 찾은 것 드러내기 + 그 칸이 사각 타일로 두 번 깜빡
        _map.RevealFound(_found);
        if (_found.Count > 0)
        {
            var marks = new List<SpriteRenderer>();
            foreach (Vector2Int f in _found)
                marks.Add(NewTile("SearchFound", automap != null ? automap.CellCenter(f) : center, cellSize * 0.92f, foundColor));
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                bool on = Mathf.FloorToInt(t / 0.1f) % 2 == 0; // 0.1초 간격 깜빡 (3번 켜짐)
                foreach (var m in marks) m.color = Alpha(foundColor, on ? 0.75f : 0f);
                yield return null;
            }
            foreach (var m in marks) Destroy(m.gameObject);
        }

        // 생존 전문가: 5% 확률로 약초 (등급 무관)
        string herb = "";
        if (FieldSkills.Has(FieldSkills.Survivalist) && Random.value < FieldSkills.SurvivalistHerbChance)
        {
            var item = Resources.Load<AbyssdawnBattle.ConsumableItemSO>(HerbResource);
            if (item != null && ConsumableInventory.Instance != null && ConsumableInventory.Instance.AddItem(item, 1) > 0)
                herb = $"\n<size=80%><color=#9FFF9F>{item.itemName}을(를) 채집했다.</color></size>";
        }

        Toast((_found.Count == 0
            ? "<color=#AAAAAA>주변을 살폈다... 아무것도 없다.</color>"
            : _found.Count == 1
                ? $"<color=#FF8A70>숨은 {MapManager.TrapLabel(_map.FloorData.GetCell(_found[0]).trap)}을(를) 찾았다!</color>"
                : $"<color=#FF8A70>숨은 함정 {_found.Count}개를 찾았다!</color>") + herb);
        Debug.Log($"[DungeonSearch] B{_map.FloorData.floorNumber} {pos} 반경 {searchR} 탐색 → 숨은 함정 {_found.Count}개");

        _player.UnlockInput(this);
        _busy = false;

        // 3) 한 턴 경과 — 걸음과 같은 대가 (회복·빛·위험도·인카운터)
        if (costsTurn)
        {
            _map.PassTurnInPlace();
            DungeonEncounter.Instance?.CheckEncounter(pos);
        }
    }

    private static void Toast(string message)
    {
        var hud = DungeonHud.Instance;
        if (hud != null) hud.Toast(message);
    }

    private static Color Alpha(Color c, float a)
    {
        c.a *= Mathf.Clamp01(a);
        return c;
    }

    // ─────────────────────────────────────────
    // 원 그리기 (LineRenderer)
    // ─────────────────────────────────────────

    private const int RingSegments = 48;

    // 사각 타일 밝기 단계 (레트로 — 단계마다 뚝뚝 끊겨 변함)
    private static readonly float[] TileSteps = { 0.55f, 0.4f, 0.25f, 0.12f };
    private const float TileStepSeconds = 0.06f;
    private static Sprite _tileSprite;

    private SpriteRenderer NewTile(string name, Vector3 at, Vector2 size, Color color)
    {
        if (_tileSprite == null)
        {
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            _tileSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        var go = new GameObject(name);
        go.transform.position = new Vector3(at.x, at.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _tileSprite;
        sr.color = Alpha(color, 0f);
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    private LineRenderer NewRing(string name, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = RingSegments;
        lr.widthMultiplier = width;
        lr.numCapVertices = 0;
        lr.material = LineMaterial();
        lr.sortingOrder = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = lr.endColor = Alpha(color, 0f);
        return lr;
    }

    private static void SetRing(LineRenderer lr, Vector3 center, float r, Color color)
    {
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, center.z));
        }
        lr.startColor = lr.endColor = color;
    }

    private Material LineMaterial()
    {
        if (_lineMaterial != null) return _lineMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        _lineMaterial = new Material(shader);
        return _lineMaterial;
    }
}
