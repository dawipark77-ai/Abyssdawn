using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탐색 (픽셀 던전·이상한 던전식). 서치 버튼(이름 "Search_Button")을 누르면 플레이어 주변으로 탐색 물결이 퍼지고,
/// 반경 안의 숨겨진 것(지금은 모르는 함정)을 찾아 자동 지도에 드러낸다.
///  - 범위: radius 칸 (기본 3). 시야와 같은 규칙 — 벽·문 너머는 찾지 못함.
///  - 한 턴이 걸린다 (costsTurn): 제자리에서 한 걸음만큼 시간이 흘러 위험도가 오르고 인카운터가 날 수 있다.
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
    public int radius = 3;
    [Tooltip("탐색하면 한 턴이 지난다 (위험도 상승·인카운터 판정, 걸음 회복·빛 소모 한 걸음)")]
    public bool costsTurn = true;

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
            Debug.Log($"[DungeonSearch] '{buttonName}' 버튼 연결 (반경 {radius}칸)");
        }
        else Debug.LogWarning($"[DungeonSearch] '{buttonName}' 버튼을 찾지 못했습니다. F 키로만 탐색할 수 있습니다.");
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F)) Search();
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
        int searchR = radius + (FieldSkills.Has(FieldSkills.TrapDetection) ? FieldSkills.TrapDetectSearchBonus : 0); // 함정 감지: 반경 +1
        _map.FindHiddenAround(pos, searchR, _found);

        // 1) 탐색 물결 — 플레이어에서 반경 끝까지 퍼지며 옅어진다
        AutomapRenderer automap = _map.automapRenderer;
        Vector3 center = automap != null ? automap.CellCenter(pos) : _player.transform.position;
        float cell = automap != null ? Mathf.Min(automap.CellSize().x, automap.CellSize().y) : 1f;
        float maxR = (searchR + 0.5f) * cell;
        float stagger = rippleSeconds * 0.25f;
        var rings = new List<LineRenderer>();
        for (int i = 0; i < Mathf.Max(1, rippleCount); i++) rings.Add(NewRing("SearchRipple", rippleColor, lineWidth * cell));

        float total = rippleSeconds + stagger * (rings.Count - 1);
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < rings.Count; i++)
            {
                float k = Mathf.Clamp01((t - i * stagger) / rippleSeconds);
                float ease = 1f - (1f - k) * (1f - k);
                SetRing(rings[i], center, Mathf.Lerp(0.2f * cell, maxR, ease), Alpha(rippleColor, (k <= 0f ? 0f : 1f - k * k) * (i == 0 ? 1f : 0.55f)));
            }
            yield return null;
        }
        foreach (var r in rings) Destroy(r.gameObject);

        // 2) 찾은 것 드러내기 + 그 자리에서 번쩍
        _map.RevealFound(_found);
        if (_found.Count > 0)
        {
            var marks = new List<LineRenderer>();
            foreach (Vector2Int p in _found) marks.Add(NewRing("SearchFound", foundColor, lineWidth * 1.4f * cell));
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = t / 0.6f;
                float pulse = Mathf.Repeat(k * 2f, 1f); // 두 번 퍼짐
                for (int i = 0; i < marks.Count; i++)
                    SetRing(marks[i], automap != null ? automap.CellCenter(_found[i]) : center,
                            Mathf.Lerp(0.15f, 0.7f, pulse) * cell, Alpha(foundColor, 1f - pulse));
                yield return null;
            }
            foreach (var m in marks) Destroy(m.gameObject);
        }

        // 생존 전문가: 10% 확률로 약초
        string herb = "";
        if (FieldSkills.Has(FieldSkills.Survivalist) && Random.value < FieldSkills.SurvivalistHerbChance)
        {
            var item = Resources.Load<AbyssdawnBattle.ConsumableItemSO>(HerbResource);
            if (item != null && ConsumableInventory.Instance != null && ConsumableInventory.Instance.AddItem(item, 1) > 0)
                herb = $"\n<size=80%><color=#9FFF9F>You gather a {item.itemName}.</color></size>";
        }

        Toast((_found.Count == 0
            ? "<color=#AAAAAA>You search the area... nothing.</color>"
            : _found.Count == 1
                ? $"<color=#FF8A70>You found a hidden {MapManager.TrapLabel(_map.FloorData.GetCell(_found[0]).trap)}!</color>"
                : $"<color=#FF8A70>You found {_found.Count} hidden traps!</color>") + herb);
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
