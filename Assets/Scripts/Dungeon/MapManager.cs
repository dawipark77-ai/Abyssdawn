using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 던전 층 지휘자.
///   층 설정표 → RogueFloorGenerator(로그식 생성) → DungeonFloorData(층 데이터, 유일한 기준)
///   → AutomapRenderer(위저드리풍 자동 지도)로 그리고, 플레이어 배치·지도 공개·계단·마을 입구를 처리한다.
///
/// 같은 층은 (층 번호, 시드)로 항상 똑같이 재생성되므로, 전투·마을에서 돌아오면
/// DungeonPersistentData 의 시드·위치·드러난 지도로 떠났던 상태 그대로 복원된다.
///
/// [2026-09-29] 방 + 최근접 복도 + 남은 공간 미로 채우기 방식에서 로그식 구획 생성으로 교체.
///              이전 버전은 git 태그 backup/pre-map-rework 에 있다.
/// </summary>
public class MapManager : MonoBehaviour
{
    [Header("Tilemaps (칸 좌표 기준용 — 새 방식에서는 타일을 그리지 않고 비워둔다)")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public Tilemap fogTilemap;

    [Header("Floor Generation")]
    [Tooltip("층 설정표. 비우면 코드에 내장된 베타 10층 설정을 사용")]
    public FloorTable floorTable;
    [Tooltip("층을 만들 때마다 콘솔에 글자 지도를 출력 (베타 테스트용)")]
    public bool logFloorAscii = true;

    [Header("Automap")]
    [Tooltip("비우면 실행 시 'Automap' 오브젝트를 자동 생성")]
    public AutomapRenderer automapRenderer;

    [Header("Camera")]
    [Tooltip("층이 화면보다 클 때 카메라가 플레이어를 따라가게 한다 (DungeonCameraFollow 자동 부착)")]
    public bool followCamera = true;
    [Tooltip("평소 지도 확대 정도 (카메라 Orthographic Size). 작을수록 지도가 크게 보인다. 씬 원래 값은 14. 플레이 중 바꾸면 바로 반영")]
    public float mapViewSize = 8f;

    [Header("Full Map")]
    [Tooltip("전체 지도를 여닫는 버튼 오브젝트 이름 (실행 시 이름으로 찾아 자동 연결)")]
    public string fullMapButtonName = DungeonFullMap.DefaultButtonName;

    [Header("Town")]
    [Tooltip("마을 입구 타일을 밟았을 때 불러올 씬 이름. 빌드 설정에 없으면 이동하지 않고 경고만 남긴다")]
    public string townSceneName = "Town";

    [HideInInspector] public int width;
    [HideInInspector] public int height;

    public DungeonFloorData FloorData { get; private set; }
    public FloorTableEntry CurrentFloorSettings { get; private set; }

    /// <summary>다음 층으로 내려가는 계단 위치 (기존 API 호환).</summary>
    public Vector2Int ExitPos => FloorData != null ? FloorData.stairsPos : new Vector2Int(-1, -1);

    private DungeonGridPlayer _player;
    private float _sceneEncounterChance = -1f;

    void Start()
    {
        if (DungeonPersistentData.currentFloor < 1) DungeonPersistentData.currentFloor = 1;

        bool restoring = DungeonPersistentData.hasSavedState;
        if (!restoring)
        {
            DungeonPersistentData.currentSeed = Random.Range(int.MinValue, int.MaxValue);
            DungeonPersistentData.revealedTiles.Clear();
        }

        _player = FindFirstObjectByType<DungeonGridPlayer>();
        if (DungeonEncounter.Instance != null) _sceneEncounterChance = DungeonEncounter.Instance.encounterChance;

        HideLegacyMapVisuals();
        BuildCurrentFloor(restoring);

        if (_player != null) DungeonInputBinder.BindMovePad(_player);

        DungeonFullMap fullMap = GetComponent<DungeonFullMap>();
        if (fullMap == null) fullMap = gameObject.AddComponent<DungeonFullMap>();
        fullMap.Setup(this, fullMapButtonName);
    }

    private void OnValidate()
    {
        // 플레이 중 인스펙터에서 확대 값을 바꾸면 바로 카메라에 반영 (플레이를 끝내면 값은 되돌아가니 기억해 두고 다시 입력)
        if (!Application.isPlaying || Camera.main == null) return;
        DungeonCameraFollow follow = Camera.main.GetComponent<DungeonCameraFollow>();
        if (follow != null) follow.viewSize = mapViewSize;
    }

    /// <summary>계단에 도달 → 다음 층 (B1 → B2 ...).</summary>
    public void GenerateNextFloor()
    {
        DungeonPersistentData.currentFloor++;
        DungeonPersistentData.hasSavedState = false;
        DungeonPersistentData.revealedTiles.Clear();
        DungeonPersistentData.lastPlayerGridPos = Vector2Int.zero;
        DungeonPersistentData.lastPlayerFacing = DungeonDirection.North;
        DungeonPersistentData.currentSeed = Random.Range(int.MinValue, int.MaxValue);

        BuildCurrentFloor(false);
    }

    /// <summary>
    /// 플레이어가 pos 칸에 들어왔을 때 DungeonGridPlayer 가 호출.
    /// 지도를 공개하고 계단·마을 입구를 처리한다. 층을 떠났으면 true (인카운터 판정 생략).
    /// </summary>
    public bool OnPlayerEntered(Vector2Int pos)
    {
        if (FloorData == null) return false;
        RevealAt(pos);

        FloorFeature feature = FloorData.GetCell(pos).feature;
        if (feature == FloorFeature.StairsDown)
        {
            Debug.Log($"[MapManager] B{DungeonPersistentData.currentFloor} 계단 도달 {pos} → 다음 층");
            GenerateNextFloor();
            return true;
        }
        if (feature == FloorFeature.TownGate)
        {
            return DungeonTownGate.Enter(townSceneName, _player);
        }
        return false;
    }

    /// <summary>pos 기준으로 지도를 공개하고, 새로 드러난 칸이 있으면 자동 지도를 다시 그린다.</summary>
    public void RevealAt(Vector2Int pos)
    {
        if (FloorData == null) return;
        if (FloorVisibility.RevealAround(FloorData, pos, DungeonPersistentData.revealedTiles) && automapRenderer != null)
            automapRenderer.Rebuild();
    }

    // ─────────────────────────────────────────
    // 층 만들기
    // ─────────────────────────────────────────

    private void BuildCurrentFloor(bool restorePlayer)
    {
        int floor = DungeonPersistentData.currentFloor;
        CurrentFloorSettings = floorTable != null && floorTable.entries != null && floorTable.entries.Count > 0
            ? floorTable.GetEntry(floor)
            : FloorTableDefaults.Find(null, floor);

        FloorData = RogueFloorGenerator.Generate(floor, DungeonPersistentData.currentSeed, CurrentFloorSettings);
        width = FloorData.width;
        height = FloorData.height;

        ApplyEncounterChance();

        EnsureAutomapRenderer();
        automapRenderer.Bind(FloorData, DungeonPersistentData.revealedTiles, floorTilemap);

        // 플레이어 배치: 복원이면 저장 위치(걸을 수 있을 때만), 아니면 시작 방
        if (_player == null) _player = FindFirstObjectByType<DungeonGridPlayer>();
        Vector2Int spawn = FloorData.startPos;
        DungeonDirection facing = DungeonDirection.North;
        if (restorePlayer)
        {
            Vector2Int saved = DungeonPersistentData.lastPlayerGridPos;
            if (FloorData.IsWalkable(saved))
            {
                spawn = saved;
                facing = DungeonPersistentData.lastPlayerFacing;
            }
            else
            {
                Debug.LogWarning($"[MapManager] 저장된 위치 {saved}가 이 층에서 걸을 수 없는 칸이라 시작 위치 {spawn}에 배치합니다.");
            }
        }

        if (_player != null)
        {
            _player.facing = facing;
            _player.Teleport(spawn);
        }
        else
        {
            Debug.LogWarning("[MapManager] DungeonGridPlayer 를 찾지 못해 플레이어를 배치하지 못했습니다.");
        }

        RevealAt(spawn);
        SetupCamera();
        LogFloor(floor, restorePlayer);
    }

    private void ApplyEncounterChance()
    {
        DungeonEncounter enc = DungeonEncounter.Instance;
        if (enc == null) return;
        if (_sceneEncounterChance < 0f) _sceneEncounterChance = enc.encounterChance;
        enc.encounterChance = CurrentFloorSettings.encounterChance >= 0f
            ? CurrentFloorSettings.encounterChance
            : _sceneEncounterChance;
    }

    private void EnsureAutomapRenderer()
    {
        if (automapRenderer != null) return;
        automapRenderer = FindFirstObjectByType<AutomapRenderer>();
        if (automapRenderer != null) return;

        var go = new GameObject("Automap");
        automapRenderer = go.AddComponent<AutomapRenderer>();
    }

    private void SetupCamera()
    {
        if (!followCamera || _player == null || automapRenderer == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        DungeonCameraFollow follow = cam.GetComponent<DungeonCameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<DungeonCameraFollow>();
        follow.viewSize = mapViewSize;
        follow.Setup(_player.transform, automapRenderer.FloorWorldRect());
    }

    /// <summary>
    /// 옛 방식은 타일맵에 바닥·벽·안개를 칠하고 타일 경계를 따라 벽선을 그렸다.
    /// 새 방식은 자동 지도 메시가 전부 그리므로 타일맵은 비우고(칸 좌표 기준으로만 사용) 옛 벽선은 끈다.
    /// </summary>
    private void HideLegacyMapVisuals()
    {
        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        if (fogTilemap != null) fogTilemap.ClearAllTiles();

        var legacyLines = FindFirstObjectByType<Genesis01.Dungeon.DungeonWallLineDrawer>();
        if (legacyLines != null)
        {
            legacyLines.ResetLines();
            legacyLines.gameObject.SetActive(false);
        }
    }

    private void LogFloor(int floor, bool restored)
    {
        int rooms = 0, gone = 0;
        foreach (var r in FloorData.rooms)
        {
            if (r.isGone) gone++;
            else rooms++;
        }
        FloorTableEntry cfg = CurrentFloorSettings;
        Debug.Log($"[MapManager] B{floor} {(restored ? "복원" : "생성")} — {FloorData.floorType}, " +
                  $"{cfg.sectionCols}x{cfg.sectionRows} 구획, {width}x{height}칸, 방 {rooms}개(빈 구획 {gone}), " +
                  $"문 {FloorData.doors.Count}개, 계단 {FloorData.stairsPos}" +
                  $"{(FloorData.hasTownGate ? $", 마을 입구 {FloorData.townGatePos}" : "")}, seed {DungeonPersistentData.currentSeed}");
        if (logFloorAscii)
            Debug.Log($"[MapManager] B{floor} 지도 (# 암반 . 방 , 통로 @ 시작 > 계단 T 마을 입구)\n{FloorData.ToAscii(null)}");
    }
}
