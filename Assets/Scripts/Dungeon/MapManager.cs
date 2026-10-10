using System.Collections;
using System.Collections.Generic;
using System.Text;
using AbyssdawnBattle;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 던전 층 지휘자.
///   층 설정표 → RogueFloorGenerator(로그식 생성) → DungeonFloorData(층 데이터, 유일한 기준)
///   → AutomapRenderer(위저드리풍 자동 지도)로 그리고, 플레이어 배치·지도 공개·탐험 요소를 처리한다.
///
/// 탐험 요소 (세계수의 미궁 · 위저드리 · 이상한 던전에서 가져온 것):
///  - 계단: 밟으면 확인창. 내려가는 계단 / 올라가는 계단(2층부터, 도착 자리) — 위층으로 돌아갈 수 있다
///  - 층 기억: 한 번 간 층은 지도·상자·함정·샘 상태가 그대로 남는다 (DungeonFloorState)
///  - 보물상자: 밟으면 장비(아직 없는 것, 30%) 또는 소비 아이템 획득 (가방이 가득 차면 닫힌 채로 남음)
///  - 숨겨진 함정: 밟기 전엔 안 보임. 가시(피해) / 전송(층 안 랜덤 방) / 경보(즉시 전투) / 구멍(아래층 추락)
///  - 회복의 샘: 확인 후 HP·MP 회복 (층마다 한 번). 5의 배수 층의 샘은 새벽의 잔도 충전
///  - 탐험률: 전체 지도에만 표시 (100% 는 별도 알림 없이 "Mapped 100%")
///  - 새 방에 들어서면 연속 이동이 멈춘다
///  - 계단·마을 입구·상자·샘은 문 바로 앞에 생기지 않는다 (RogueFloorGenerator)
///
/// 같은 층은 (층 번호, 시드)로 항상 똑같이 재생성되므로, 전투·마을에서 돌아오면 떠났던 상태 그대로 복원된다.
///
/// [2026-09-29] 방 + 최근접 복도 + 남은 공간 미로 채우기 방식에서 로그식 구획 생성으로 교체.
///              이전 버전은 git 태그 backup/pre-map-rework 에 있다.
/// </summary>
public class MapManager : MonoBehaviour
{
    /// <summary>층에 도착하는 자리.</summary>
    public enum Arrival { Start, SavedPosition, DownStairs, RandomRoom }

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
    [Tooltip("화면 아래 UI(파티 카드 + 메뉴 버튼)가 덮는 높이 비율. 지도·플레이어는 그 위 빈 곳 가운데에 보인다. 키우면 지도가 위로 올라감. 플레이 중 바꾸면 바로 반영")]
    [Range(0f, 0.8f)] public float mapBottomUi = 0.28f;
    [Tooltip("화면 위 UI(ENTROPY·층 표시)가 덮는 높이 비율")]
    [Range(0f, 0.5f)] public float mapTopUi = 0.06f;
    [Tooltip("화면 배경 스프라이트 (월드의 Sprite 오브젝트). 넣기만 하면 실행 시 카메라 화면에 정확히 맞춰진다 — 위치·크기 조정 불필요. " +
             "Order in Layer 는 지도(5)·플레이어(10)보다 작게")]
    public SpriteRenderer screenBackground;

    [Header("Full Map")]
    [Tooltip("전체 지도를 여닫는 버튼 오브젝트 이름 (실행 시 이름으로 찾아 자동 연결)")]
    public string fullMapButtonName = DungeonFullMap.DefaultButtonName;

    [Header("Town Economy")]
    [Tooltip("여관 요금 — 첫 마을 (B1)")]
    public int innPriceFirstTown = 8;
    [Tooltip("여관 요금 — 두 번째 마을 (B5)")]
    public int innPriceSecondTown = 20;
    [Tooltip("소지금을 표시할 글자 오브젝트 이름 (그 오브젝트나 자식의 TextMeshPro). 실행 시 자동 연결")]
    public string moneyLabelName = "Money";

    [Header("Search")]
    [Tooltip("탐색 버튼 오브젝트 이름 (실행 시 이름으로 찾아 자동 연결). 반경·턴 소모·연출은 실행 중 붙는 DungeonSearch 에서")]
    public string searchButtonName = DungeonSearch.DefaultButtonName;

    [Header("UI")]
    [Tooltip("창들(상태·인벤토리 등)이 들어 있는 Canvas 이름. 창은 한 번에 하나만 열린다 (DungeonPanelGroup)")]
    public string uiCanvasName = "Canvas";

    [Header("Town")]
    [Tooltip("마을 입구 타일을 밟았을 때 불러올 씬 이름. 빌드 설정에 없으면 이동하지 않고 경고만 남긴다")]
    public string townSceneName = "Town";

    [Header("Exploration")]
    [Tooltip("회복의 샘 회복량 (최대 HP·MP 비율)")]
    [Range(0f, 1f)] public float springHealPercent = 0.5f;
    [Tooltip("경보 함정 문구를 보여준 뒤 전투가 시작되기까지 (초)")]
    public float alarmDelay = 0.8f;
    [Tooltip("보물상자에서 장비가 나올 확률 (아직 없는 장비 중에서). 나머지는 소비 아이템")]
    [Range(0f, 1f)] public float chestEquipmentChance = 0.3f;
    [Tooltip("[2026-10-08] 허접 무기가 나오는 층(장비마다 dropMaxFloor)에서 보물상자가 허접 무기를 줄 확률 (아직 없는 것 중에서)")]
    [Range(0f, 1f)] public float chestJunkWeaponChance = 0.4f;
    [Tooltip("보물상자에서 '지식의 서'(스킬 포인트 LP +1)가 나올 확률 — 스톤샤드식 전투 외 성장 수단")]
    [Range(0f, 1f)] public float chestTomeChance = 0.12f;

    [Header("Sight (안개 시야)")]
    [Tooltip("기본 시야 반경 (칸)")]
    public int baseSightRadius = 3;
    [Tooltip("어두운 층의 기본 시야 반경")]
    public int darkSightRadius = 1;
    [Tooltip("불(화로) 있는 방 안이거나 플레이어가 빛을 들고 있을 때 더해지는 반경")]
    public int lightSightBonus = 2;
    [Tooltip("테스트용: 지금 층을 어두운 층으로 취급")]
    public bool forceDarkFloor = false;

    [Header("Growth (전투 외 성장 — 스톤샤드식)")]
    [Tooltip("걸을 때 자연 회복: 이 걸음 수마다 HP 1 (0 이면 끔)")]
    public int stepsPerRegenHP = 12;
    [Tooltip("층을 계단으로 처음 내려갈 때 받는 탐험 EXP = 값 × 층 번호")]
    public int floorClearExpPerFloor = 20;
    [Tooltip("그 층 지도를 100% 채우고 내려가면 추가로 받는 EXP = 값 × 층 번호")]
    public int mapCompleteExpPerFloor = 20;

    [HideInInspector] public int width;
    [HideInInspector] public int height;

    public DungeonFloorData FloorData { get; private set; }
    public DungeonFloorState FloorState { get; private set; }
    public FloorTableEntry CurrentFloorSettings { get; private set; }

    /// <summary>다음 층으로 내려가는 계단 위치 (기존 API 호환).</summary>
    public Vector2Int ExitPos => FloorData != null ? FloorData.stairsPos : new Vector2Int(-1, -1);

    /// <summary>이 층에서 드러난 칸 비율 (0~1).</summary>
    public float ExploredRatio => FloorState != null && _walkableCount > 0
        ? Mathf.Clamp01((float)FloorState.revealed.Count / _walkableCount)
        : 0f;

    private DungeonGridPlayer _player;
    private DungeonHud _hud;
    private float _sceneEncounterChance = -1f;
    private int _walkableCount;
    private int _regenSteps;
    private List<ConsumableItemSO> _loot;

    void Start()
    {
        if (DungeonPersistentData.currentFloor < 1) DungeonPersistentData.currentFloor = 1;

        bool restoring = DungeonPersistentData.hasSavedState;
        if (!restoring)
        {
            // 새 탐험: 층 기억·위험도·빛·함정 상태이상 초기화
            DungeonPersistentData.floors.Clear();
            DungeonPersistentData.danger = 0f;
            DungeonPersistentData.playerLightSteps = 0;
            DungeonFieldStatus.Clear();
        }

        _player = FindFirstObjectByType<DungeonGridPlayer>();
        if (DungeonEncounter.Instance != null) _sceneEncounterChance = DungeonEncounter.Instance.encounterChance;
        _hud = DungeonHud.Ensure();

        HideLegacyMapVisuals();
        BuildCurrentFloor(restoring ? Arrival.SavedPosition : Arrival.Start);

        if (_player != null) DungeonInputBinder.BindMovePad(_player);

        DungeonFullMap fullMap = GetComponent<DungeonFullMap>();
        if (fullMap == null) fullMap = gameObject.AddComponent<DungeonFullMap>();
        fullMap.Setup(this, fullMapButtonName);

        // 탐색 (서치 버튼 — 주변의 숨겨진 함정 찾기)
        DungeonSearch search = GetComponent<DungeonSearch>();
        if (search == null) search = gameObject.AddComponent<DungeonSearch>();
        search.Setup(this, searchButtonName);

        // 소지금 표시 (화면의 "Money" 글자)
        MoneyLabel.Bind(moneyLabelName);

        // 화면 아래 파티 카드 (전투의 PartyBar 를 그대로 배치하면 자동 연결)
        if (FindFirstObjectByType<DungeonPartyBar>() == null) gameObject.AddComponent<DungeonPartyBar>();

        // 창(상태·인벤토리·Oath & Path 등)은 한 번에 하나만 열리게
        Transform canvas = DungeonPanelGroup.FindPanelCanvas(uiCanvasName);
        if (canvas != null) DungeonPanelGroup.Setup(canvas);
        else Debug.LogWarning($"[MapManager] 창들이 들어 있는 '{uiCanvasName}' 을(를) 찾지 못해 창 겹침 방지를 켜지 못했습니다.");

        // 게임을 켜고 처음 들어왔으면: 저장이 있으면 이어서 할지 묻는다 (게임 오버·전투 복귀 때는 묻지 않음)
        if (!SaveSystem.LaunchChecked)
        {
            SaveSystem.LaunchChecked = true;
            if (!restoring && SaveSystem.HasSave) AskContinueFromSave();
        }
        if (!string.IsNullOrEmpty(SaveSystem.PendingNotice))
        {
            Toast(SaveSystem.PendingNotice); // 게임 오버 후 마지막 저장에서 깨어남 등
            SaveSystem.PendingNotice = null;
        }
    }

    private void OnValidate()
    {
        // 플레이 중 인스펙터에서 확대 값을 바꾸면 바로 카메라에 반영 (플레이를 끝내면 값은 되돌아가니 기억해 두고 다시 입력)
        if (!Application.isPlaying || Camera.main == null) return;
        DungeonCameraFollow follow = Camera.main.GetComponent<DungeonCameraFollow>();
        if (follow != null) ApplyCameraSettings(follow);
    }

    private void ApplyCameraSettings(DungeonCameraFollow follow)
    {
        follow.viewSize = mapViewSize;
        follow.bottomUiFraction = mapBottomUi;
        follow.topUiFraction = mapTopUi;
    }

    /// <summary>다음 층으로 (기존 API 호환). 위층에서 내려온 자리 = 올라가는 계단 위에 도착.</summary>
    public void GenerateNextFloor()
    {
        ChangeFloor(DungeonPersistentData.currentFloor + 1, Arrival.Start);
    }

    /// <summary>층 이동. 처음 가는 층이면 새로 만들고, 가 본 층이면 기억대로 복원한다.</summary>
    public void ChangeFloor(int floor, Arrival arrival)
    {
        if (floor < 1) return;
        DungeonPersistentData.currentFloor = floor;
        DungeonPersistentData.hasSavedState = false;
        DungeonPersistentData.lastPlayerGridPos = Vector2Int.zero;
        DungeonPersistentData.lastPlayerFacing = DungeonDirection.North;
        BuildCurrentFloor(arrival);
    }

    // ─────────────────────────────────────────
    // 한 걸음마다
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어가 pos 칸에 들어왔을 때 DungeonGridPlayer 가 호출.
    /// 지도를 공개하고 계단·보물·함정·샘을 처리한다. 무슨 일이 있었으면 true (이번 걸음 인카운터 판정 생략).
    /// </summary>
    public bool OnPlayerEntered(Vector2Int pos)
    {
        if (FloorData == null) return false;

        TickWalkRegen();
        TickPlayerLight();
        TickFieldStatus();
        RevealAt(pos);
        PassiveTrapSense(pos); // 탐험 스킬: 함정 감지
        FloorRoom room = FloorData.GetRoomAt(pos);
        if (room != null && !room.isGone && FloorState.enteredRooms.Add(room.id))
        {
            // 처음 들어선 방: 연속 이동을 멈춘다 (안개 속 방을 둘러볼 수 있게. 알림 문구는 띄우지 않음)
            if (_player != null) _player.InterruptHold();
        }

        FloorCell cell = FloorData.GetCell(pos);
        switch (cell.feature)
        {
            case FloorFeature.StairsDown:
                AskDescend();
                return true;
            case FloorFeature.StairsUp:
                AskAscend();
                return true;
            case FloorFeature.TownGate:
                AskTown();
                return true;
            case FloorFeature.Chest:
                if (FloorState.openedChests.Contains(pos)) return false;
                OpenChest(pos);
                return true;
            case FloorFeature.Spring:
                if (FloorState.usedSprings.Contains(pos)) return false;
                AskSpring(pos);
                return true;
            case FloorFeature.Trap:
                return TriggerTrap(pos, cell.trap);
        }
        return false;
    }

    /// <summary>
    /// 지금 시야 반경 (칸). 기본 3 / 어두운 층 1, 불 있는 방 안이거나 빛을 들고 있으면 +2.
    ///   보통 층: 3 (불 있으면 5) · 어두운 층: 1 (불 있으면 3)
    /// </summary>
    public int SightRadiusAt(Vector2Int pos)
    {
        bool dark = forceDarkFloor || (CurrentFloorSettings != null && CurrentFloorSettings.darkFloor);
        int r = dark ? darkSightRadius : baseSightRadius;
        FloorRoom room = FloorData != null ? FloorData.GetRoomAt(pos) : null;
        // [2026-10-08] 불 있는 방(벽 화로)의 시야 확대는 횃불(주변 2칸 밝힘)로 바뀜 — 빛을 들고 있을 때만 +2
        bool carryingLight = DungeonPersistentData.playerLightSteps > 0;
        if (carryingLight) r += lightSightBonus;
        if (DungeonFieldStatus.Has(AbyssdawnBattle.StatusEffectType.Blind)) r -= DungeonFieldStatus.BlindSightPenalty; // 실명 가스
        return Mathf.Max(1, r);
    }

    /// <summary>
    /// 플레이어가 steps 걸음 동안 빛을 든다 (시야 +2). 횃불·새벽불 등 빛 아이템이 정해지면 여기에 연결.
    /// 탐험 스킬 '생존 전문가'가 있으면 ×1.5.
    /// </summary>
    public void AddPlayerLight(int steps)
    {
        // [2026-10-11] 생존 전문가의 빛 +50% 는 보급술 문서에 없어 뺐다
        DungeonPersistentData.playerLightSteps = Mathf.Max(0, DungeonPersistentData.playerLightSteps + steps);
        if (_player != null) RevealAt(_player.gridPos);
        Debug.Log($"[MapManager] 빛 {DungeonPersistentData.playerLightSteps}걸음 (시야 {SightRadiusAt(_player != null ? _player.gridPos : Vector2Int.zero)}칸)");
    }

    private void TickPlayerLight()
    {
        if (DungeonPersistentData.playerLightSteps <= 0) return;
        DungeonPersistentData.playerLightSteps--;
        if (DungeonPersistentData.playerLightSteps == 0) Toast("<color=#AAAAAA>Your light fades...</color>");
    }

    private void Update()
    {
        HandleTrapClick();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugKeys();
#endif
    }

    // ─────────────────────────────────────────
    // [2026-10-08] 함정 클릭 — 바로 앞(상하좌우 1칸)의 아는 함정을 누르면 해제할지 묻는다. 성공하면 소량의 EXP.
    // ─────────────────────────────────────────
    [Header("Trap Click (2026-10-08)")]
    [Tooltip("함정 해제 성공 EXP = 기본 + 층 × 층당")]
    public int disarmExpBase = 3;
    public int disarmExpPerFloor = 2;

    private void HandleTrapClick()
    {
        if (!Input.GetMouseButtonDown(0) || _player == null || FloorData == null || FloorState == null) return;
        if (_player.IsInputLocked) return; // 창이 떠 있거나 이동 중 잠김
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return; // UI 를 누름
        Camera cam = Camera.main;
        if (cam == null || floorTilemap == null) return;
        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int c = floorTilemap.WorldToCell(world);
        Vector2Int p = new Vector2Int(c.x, -c.y); // 격자 y 는 아래로 증가 (AutomapRenderer.CellCenter 와 반대 변환)
        Vector2Int d = p - _player.gridPos;
        if (Mathf.Abs(d.x) + Mathf.Abs(d.y) != 1) return; // 바로 앞 칸만
        AskDisarm(p, null);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 테스트용: Ctrl+L = 빛 30걸음, Ctrl+K = 어두운 층 켜고 끄기
    private void DebugKeys()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrl || _player == null) return;
        if (Input.GetKeyDown(KeyCode.L)) AddPlayerLight(30);
        if (Input.GetKeyDown(KeyCode.K))
        {
            forceDarkFloor = !forceDarkFloor;
            Debug.Log($"[MapManager] (테스트) 어두운 층 {(forceDarkFloor ? "켬" : "끔")} — 시야 {SightRadiusAt(_player.gridPos)}칸");
        }
    }
#endif

    // 지금 보이는 칸 (매 걸음 새로 계산). 자동 지도가 이 밖의 기억된 칸을 흐리게 그린다
    private readonly HashSet<Vector2Int> _visible = new HashSet<Vector2Int>();

    // ─────────────────────────────────────────
    // [2026-10-08] 밝기 — 주인공 주변은 거리에 따라 흐려지고, 횃불은 주변 2칸을 밝힌다. 나머지는 어둠.
    //   [2026-10-08 2차 — 어두운 밤의 간접 조명 느낌으로 더 어둡게]
    //   주인공: 자기 칸·1칸 100% · 2칸 50% · 3칸 20% (빛을 들어 시야가 넓으면 그 밖은 15%)
    //   횃불: 횃불 칸 100% · 1칸 70% · 2칸 40% — 벽 너머는 밝히지 않음. 같은 방/통로 안에서 벽에 막히지 않으면 멀리서도 보인다
    //   가 본 곳(지금 안 보임) 20% · 안 본 곳 0% — AutomapRenderer 가 어둠을 덮는다
    // ─────────────────────────────────────────
    [Header("Light (2026-10-08)")]
    [Tooltip("주인공에서 거리 0·1·2·3칸의 밝기 (그 밖은 마지막 값 다음 Far 값)")]
    public float[] heroLightByDistance = { 1f, 1f, 0.5f, 0.2f };
    [Tooltip("시야가 넓을 때(빛을 들었을 때) 3칸 너머의 밝기")]
    [Range(0f, 1f)] public float heroLightFar = 0.15f;
    [Tooltip("횃불에서 거리 0·1·2칸의 밝기 (길이 = 밝히는 범위)")]
    public float[] torchLightByDistance = { 1f, 0.7f, 0.4f };
    [Tooltip("멀리 있는 횃불이 보이는 최대 거리 (같은 방/통로 안)")]
    public int torchViewRange = 14;

    private readonly Dictionary<Vector2Int, float> _light = new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> _farView = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _torchArea = new HashSet<Vector2Int>();

    /// <summary>칸마다 밝기를 계산해 _light 에 넣는다. 횃불로 밝혀진 먼 칸도 보이는 칸·기억에 더함. 새로 기억된 칸이 있으면 true.</summary>
    private bool ComputeLighting(Vector2Int pos)
    {
        _light.Clear();
        bool changed = false;
        foreach (Vector2Int p in _visible)
        {
            int d = Mathf.Max(Mathf.Abs(p.x - pos.x), Mathf.Abs(p.y - pos.y));
            float b = d < heroLightByDistance.Length ? heroLightByDistance[d] : heroLightFar;
            if (b > 0f) _light[p] = b;
        }
        if (FloorData.torches.Count == 0) return false;

        FloorVisibility.ComputeVisible(FloorData, pos, torchViewRange, _farView);
        int range = torchLightByDistance.Length - 1;
        foreach (FloorTorch t in FloorData.torches)
        {
            // 횃불 빛이 닿는 칸 (횃불 기준 시야 — 벽 너머는 안 밝힘)
            FloorVisibility.ComputeVisible(FloorData, t.cell, range, _torchArea);
            foreach (Vector2Int p in _torchArea)
            {
                if (!_farView.Contains(p)) continue; // 주인공 쪽에서 보이지 않으면 모름
                int d = Mathf.Max(Mathf.Abs(p.x - t.cell.x), Mathf.Abs(p.y - t.cell.y));
                if (d > range) continue;
                float b = torchLightByDistance[d];
                float cur;
                if (!_light.TryGetValue(p, out cur) || cur < b) _light[p] = b;
                _visible.Add(p);
                if (FloorState.revealed.Add(p)) changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// pos 기준으로 시야를 계산해 지도를 공개하고, 보이는 칸이 바뀌었으니 자동 지도를 다시 그린다.
    /// 새로 기억된 칸이 있으면 true.
    /// </summary>
    public bool RevealAt(Vector2Int pos)
    {
        if (FloorData == null || FloorState == null) return false;
        bool changed = FloorVisibility.RevealAround(FloorData, pos, FloorState.revealed, SightRadiusAt(pos), _visible);
        changed |= ComputeLighting(pos);
        if (automapRenderer != null)
        {
            automapRenderer.SetVisible(_visible);
            automapRenderer.SetLighting(_light);
            automapRenderer.Rebuild();
        }
        // 지도 완성(100%)은 알림 없이 전체 지도의 탐험률로만 보여준다 (DungeonFullMap)
        return changed;
    }

    // ─────────────────────────────────────────
    // 탐색 (DungeonSearch — 서치 버튼)
    // ─────────────────────────────────────────

    /// <summary>
    /// pos 주변 radius 칸 안의 숨겨진 것(지금은 아직 모르는 함정)을 찾아 found 에 담는다. 아직 드러내지는 않는다.
    /// 범위 규칙은 시야와 같다: 둥근 반경, 벽 너머·문 너머는 찾지 못함 (FloorVisibility).
    /// 숨겨진 문 등이 생기면 여기에 추가.
    /// </summary>
    public void FindHiddenAround(Vector2Int pos, int radius, List<Vector2Int> found)
    {
        found.Clear();
        if (FloorData == null || FloorState == null) return;
        var area = new HashSet<Vector2Int>();
        FloorVisibility.ComputeVisible(FloorData, pos, radius, area);
        foreach (Vector2Int p in FloorData.traps)
            if (area.Contains(p) && !FloorState.knownTraps.Contains(p)) found.Add(p);
    }

    /// <summary>찾은 함정을 드러낸다 — 이후 자동 지도에 함정 표시(X)가 그려지고 층 기억에 남는다.</summary>
    public void RevealFound(List<Vector2Int> found)
    {
        if (FloorState == null || found == null || found.Count == 0) return;
        foreach (Vector2Int p in found) FloorState.knownTraps.Add(p);
        if (automapRenderer != null) automapRenderer.Rebuild();
    }

    /// <summary>제자리에서 한 턴이 지남 (탐색 등): 걸음 회복·빛 소모가 한 걸음만큼 진행된다.</summary>
    public void PassTurnInPlace()
    {
        TickWalkRegen();
        TickPlayerLight();
        TickFieldStatus();
    }

    /// <summary>함정 상태이상(독·출혈·화상 등) 한 걸음 진행. 피해가 있으면 화면이 살짝 붉어지고 알림.</summary>
    private void TickFieldStatus()
    {
        if (!DungeonFieldStatus.Any) return;
        string msg = DungeonFieldStatus.TickStep(GetHeroStats());
        if (string.IsNullOrEmpty(msg)) return;
        if (msg.Contains(" -")) _hud.Flash(new Color(0.6f, 0.1f, 0.4f, 0.3f), 0.25f);
        Toast(msg);
        CheckHeroDeath();
    }

    // ─────────────────────────────────────────
    // 던전 사망 (함정·함정 상태이상) → 게임 오버 [2026-10-08]
    // ─────────────────────────────────────────

    [Tooltip("던전에서 쓰러진 뒤 게임 오버 창이 뜨기까지 (초)")]
    public float dungeonDeathDelay = 2f;
    private bool _heroDead;

    /// <summary>주인공 HP 가 0 이면 이동을 막고 게임 오버 창을 띄운다 (한 번만).</summary>
    private void CheckHeroDeath()
    {
        if (_heroDead) return;
        PlayerStats hero = GetHeroStats();
        if (hero == null || hero.currentHP > 0) return;
        _heroDead = true;
        if (_player != null) _player.LockInput(this);
        _hud.Flash(new Color(0.5f, 0f, 0f, 0.8f), 1.2f);
        Toast("<color=#FF4A4A>You have fallen...</color>");
        Debug.Log($"[MapManager] B{(FloorData != null ? FloorData.floorNumber : 0)} 주인공 사망 (던전) → 게임 오버");
        StartCoroutine(DungeonGameOverRoutine(hero));
    }

    private IEnumerator DungeonGameOverRoutine(PlayerStats hero)
    {
        yield return new WaitForSeconds(dungeonDeathDelay);
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        var party = new List<PlayerStats> { hero };
        GameOverFlow.ShowScreen(
            () => GameOverFlow.Restart(false, scene, party, hero.statData, null),
            () => GameOverFlow.Restart(true, scene, party, hero.statData, null));
    }

    // [2026-10-08] 함정 자동 감지 — 누구나, 걸을 때마다 1~3칸 안(벽 너머 제외)의 숨은 함정을 확률로 알아챈다. 민첩 기반.
    //   1칸 확률 = 기본 10% + 민첩 1당 0.25%p (+ 탐험 스킬 '함정 감지' 25%p), 최대 50%
    //   2칸 = 1칸 × 0.5,  3칸 = 1칸 × 0.25   (함정마다 따로, 걸음마다 다시 굴림)
    //   예) 민첩 5: 11.3% / 5.6% / 2.8%  ·  민첩 50: 22.5% / 11.3% / 5.6%  ·  민첩 99: 34.8% / 17.4% / 8.7%
    [Header("Trap Sense (함정 자동 감지)")]
    [Tooltip("1칸 거리 기본 감지 확률")]
    [Range(0f, 1f)] public float trapSenseBaseChance = 0.10f;
    [Tooltip("민첩 1당 1칸 거리 감지 확률 증가 (0.0025 = 0.25%p, 민첩 99 → +24.75%p)")]
    [Range(0f, 0.02f)] public float trapSensePerAgi = 0.0025f;
    [Tooltip("1칸 거리 감지 확률 상한")]
    [Range(0f, 1f)] public float trapSenseMaxChance = 0.50f;
    [Tooltip("거리별 배율 — [0] = 1칸, [1] = 2칸, [2] = 3칸. 길이가 감지 범위")]
    public float[] trapSenseDistanceMult = { 1f, 0.5f, 0.25f };

    /// <summary>지금 주인공의 1칸 거리 감지 확률 (거리 배율 적용 전).</summary>
    public float TrapSenseChance()
    {
        PlayerStats hero = GetHeroStats();
        float agi = hero != null ? hero.Agility : 0f;
        float c = trapSenseBaseChance + agi * trapSensePerAgi;
        if (FieldSkills.Has(FieldSkills.TrapDetection)) c += FieldSkills.TrapDetectPassiveChance;
        return Mathf.Clamp(c, 0f, trapSenseMaxChance);
    }

    private readonly HashSet<Vector2Int> _senseArea = new HashSet<Vector2Int>();

    private void PassiveTrapSense(Vector2Int pos)
    {
        if (FloorData == null || FloorState == null || trapSenseDistanceMult == null || trapSenseDistanceMult.Length == 0) return;
        int range = trapSenseDistanceMult.Length;
        FloorVisibility.ComputeVisible(FloorData, pos, range, _senseArea); // 시야와 같은 규칙: 벽·문 너머는 못 느낌
        float near = TrapSenseChance();
        var found = new List<Vector2Int>();
        foreach (Vector2Int p in FloorData.traps)
        {
            if (FloorState.knownTraps.Contains(p) || p == pos || !_senseArea.Contains(p)) continue;
            int d = Mathf.Max(Mathf.Abs(p.x - pos.x), Mathf.Abs(p.y - pos.y));
            if (d < 1 || d > range) continue;
            float chance = near * trapSenseDistanceMult[d - 1];
            if (Random.value < chance)
            {
                found.Add(p);
                Debug.Log($"[MapManager] 함정 감지 {p} ({d}칸, {chance:P1}): {FloorData.GetCell(p).trap}");
            }
        }
        if (found.Count == 0) return;
        RevealFound(found);
        if (_player != null) _player.InterruptHold();
        Toast(found.Count == 1
            ? $"<color=#FF8A70>You sense a {TrapLabel(FloorData.GetCell(found[0]).trap)} nearby!</color>"
            : $"<color=#FF8A70>You sense {found.Count} traps nearby!</color>");
    }

    // ─────────────────────────────────────────
    // 걸음 직전 — 함정 감지(멈춤) · 함정 해제 (DungeonGridPlayer 가 이동 직전에 묻는다)
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어가 next 칸으로 들어가려 할 때. 이동을 막았으면 true.
    ///  - 숨은 함정 + '함정 감지': 50% 확률로 직전에 알아채고 멈춘다 (함정이 지도에 드러남)
    ///  - 아는(작동 중인) 함정 + '함정 해제': 해제 / 그냥 지나가기 / 물러서기 를 묻는다. 지나가기를 고르면 step() 으로 이동
    /// </summary>
    public bool BeforeStep(Vector2Int next, System.Action step)
    {
        if (FloorData == null || FloorState == null) return false;
        FloorCell cell = FloorData.GetCell(next);
        if (cell.feature != FloorFeature.Trap || !FloorState.IsTrapArmed(next, cell.trap)) return false;

        bool known = FloorState.knownTraps.Contains(next);
        if (!known)
        {
            if (!FieldSkills.Has(FieldSkills.TrapDetection) || Random.value >= FieldSkills.TrapDetectStopChance) return false;
            FloorState.knownTraps.Add(next);
            if (automapRenderer != null) automapRenderer.Rebuild();
            Toast($"<color=#FF8A70>You stop short — a hidden {TrapLabel(cell.trap)}!</color>");
            return true;
        }

        if (!FieldSkills.Has(FieldSkills.TrapDisarm)) return false;
        return AskDisarm(next, step);
    }

    /// <summary>
    /// [2026-10-08] 아는 함정의 해제 여부를 묻는다 (걸어 들어가려 할 때 = 탐험 스킬 '함정 해제', 또는 바로 앞 함정을 눌렀을 때 = 누구나).
    /// step 이 있으면 "그냥 밟고 지나가기" 도 고를 수 있다. 물었으면 true.
    /// </summary>
    private bool AskDisarm(Vector2Int pos, System.Action step)
    {
        if (!FloorData.InBounds(pos)) return false;
        FloorCell cell = FloorData.GetCell(pos);
        if (cell.feature != FloorFeature.Trap || !FloorState.IsTrapArmed(pos, cell.trap) || !FloorState.knownTraps.Contains(pos)) return false;
        int chance = Mathf.RoundToInt(DisarmChance() * 100f);
        string[] options = step != null
            ? new[] { $"Disarm <color=#9FC8FF>({chance}%)</color>", "Walk over it" }
            : new[] { $"Disarm <color=#9FC8FF>({chance}%)</color>" };
        _hud.Choose($"A <b>{TrapLabel(cell.trap)}</b> lies ahead.\n<size=80%><color=#AAAAAA>If disarming fails, it goes off (half damage).</color></size>",
            options, step != null ? "Back" : "Leave it", false, choice =>
            {
                if (choice == 0) TryDisarm(pos);
                else if (choice == 1 && step != null) step();
            });
        return true;
    }

    /// <summary>해제 확률: 탐험 스킬 '함정 해제'가 있으면 55% + 민첩, 없으면 30% + 민첩 (민첩 1당 0.5%p, 최대 95%).</summary>
    private float DisarmChance()
    {
        PlayerStats hero = GetHeroStats();
        float agi = hero != null ? hero.Agility : 0f;
        float baseChance = FieldSkills.Has(FieldSkills.TrapDisarm) ? FieldSkills.DisarmBaseChance : FieldSkills.DisarmNoSkillChance;
        return Mathf.Clamp(baseChance + agi * FieldSkills.DisarmPerAgi, 0f, FieldSkills.DisarmMaxChance);
    }

    /// <summary>함정 해제 시도. 성공: 함정 제거 + 함정 부품 1개. 실패: 그 자리에서 발동 (피해 절반).</summary>
    private void TryDisarm(Vector2Int pos)
    {
        FloorCell cell = FloorData.GetCell(pos);
        if (Random.value < DisarmChance())
        {
            FloorState.disarmedTraps.Add(pos);
            if (automapRenderer != null) automapRenderer.Rebuild();
            string loot = "";
            var parts = Resources.Load<ConsumableItemSO>(TrapPartsResource);
            if (parts != null && ConsumableInventory.Instance != null && ConsumableInventory.Instance.AddItem(parts, 1) > 0)
                loot = $"\n<size=80%>Salvaged <color=#FFD24A>{parts.itemName}</color>.</size>";
            int exp = Mathf.Max(0, disarmExpBase + disarmExpPerFloor * FloorData.floorNumber);
            PlayerStats hero = GetHeroStats();
            if (hero != null && exp > 0) hero.AddExp(exp);
            Toast($"<color=#9FFF9F>You disarm the {TrapLabel(cell.trap)}.</color> <color=#FFD24A>+{exp} EXP</color>{loot}");
            Debug.Log($"[MapManager] 함정 해제 성공 {pos}: {cell.trap} (EXP +{exp})");
        }
        else
        {
            Toast("<color=#FF6B6B>Your hand slips!</color>");
            Debug.Log($"[MapManager] 함정 해제 실패 {pos}: {cell.trap}");
            TriggerTrap(pos, cell.trap, FieldSkills.DisarmFailDamageMult, true);
            CheckHeroDeath();
        }
    }

    public const string TrapPartsResource = "Item_Equipments/Items/Trap_Parts";

    public string TrapName(Vector2Int pos)
    {
        return FloorData != null ? FloorData.GetCell(pos).trap.ToString() : "";
    }

    // ─────────────────────────────────────────
    // 계단 · 마을 입구
    // ─────────────────────────────────────────

    // ─────────────────────────────────────────
    // [2026-10-09] 야영 (탐험 스킬 Make Camp, 1~5등급) — 던전 메뉴의 Camp 버튼
    // ─────────────────────────────────────────

    /// <summary>[2026-10-11] 이번 층에서 계단(내려가는 쪽 또는 올라가는 쪽)을 이미 발견했는지 — 야전술 '퇴로 확보'.</summary>
    public bool StairsKnown()
    {
        if (FloorData == null || FloorState == null) return false;
        if (FloorState.revealed.Contains(FloorData.stairsPos)) return true;
        return FloorData.hasStairsUp && FloorState.revealed.Contains(FloorData.stairsUpPos);
    }

    /// <summary>야영을 지금 할 수 없는 이유 (할 수 있으면 null).</summary>
    public string CampBlockReason()
    {
        if (FieldSkills.Rank(FieldSkills.MakeCamp) <= 0) return "You don't know how to make camp.";
        if (FloorData == null || FloorState == null) return "Not here.";
        if (CurrentFloorSettings != null && CurrentFloorSettings.floorType == FloorType.Town) return "You can't make camp on a town floor.";
        if (FloorState.campUsed) return "You have already camped on this floor.";
        return null;
    }

    public void TryMakeCamp()
    {
        if (_hud == null) return;
        string why = CampBlockReason();
        if (why != null) { Toast($"<color=#AAAAAA>{why}</color>"); return; }
        int rank = Mathf.Clamp(FieldSkills.Rank(FieldSkills.MakeCamp), 1, FieldSkills.MakeCampMaxRank);
        float restore = FieldSkills.CampRestore[rank - 1];
        float ambush = Mathf.Min(DungeonPersistentData.danger, FieldSkills.CampAmbushMax) * FieldSkills.CampAmbushMult[rank - 1];
        var cures = FieldSkills.CampCures(rank);
        var cureNames = new List<string>();
        foreach (var c in cures) cureNames.Add(DungeonFieldStatus.NameOf(c));
        string info = $"Rest here to recover <b>{Mathf.RoundToInt(restore * 100f)}%</b> HP·MP." +
                      (cureNames.Count > 0 ? $"\nCures: {string.Join(", ", cureNames)}." : "") +
                      (rank >= FieldSkills.CampLightFromRank ? $"\nRekindle your light (+{FieldSkills.CampLightSteps} steps)." : "") +
                      $"\n<size=80%><color=#FF8A70>Ambush risk {Mathf.RoundToInt(ambush * 100f)}%. Danger +{Mathf.RoundToInt(FieldSkills.CampDangerAfter[rank - 1] * 100f)}% afterwards.</color></size>";
        _hud.Confirm($"<b>Make Camp</b> <size=80%>(Lv {rank})</size>\n{info}", "Rest", "Not now", ok =>
        {
            if (!ok) return;
            MakeCamp(rank, restore, ambush, cures);
        });
    }

    private void MakeCamp(int rank, float restore, float ambushChance, AbyssdawnBattle.StatusEffectType[] cures)
    {
        FloorState.campUsed = true;
        PlayerStats hero = GetHeroStats();
        if (hero != null)
        {
            hero.currentHP = Mathf.Min(hero.maxHP, hero.currentHP + Mathf.Max(1, Mathf.RoundToInt(hero.maxHP * restore)));
            if (hero.maxMP > 0) hero.currentMP = Mathf.Min(hero.maxMP, hero.currentMP + Mathf.RoundToInt(hero.maxMP * restore));
            hero.NotifyStatusChanged();
        }
        foreach (var c in cures) if (DungeonFieldStatus.Has(c)) DungeonFieldStatus.Remove(c);
        if (rank >= FieldSkills.CampLightFromRank) AddPlayerLight(FieldSkills.CampLightSteps);
        DungeonEncounter.SetDanger(DungeonPersistentData.danger + FieldSkills.CampDangerAfter[rank - 1]);
        Debug.Log($"[MapManager] B{FloorData.floorNumber} 야영 Lv{rank}: 회복 {restore:P0}, 습격 확률 {ambushChance:P0}");

        if (Random.value < ambushChance)
        {
            Toast("<color=#FF6B6B>Something was watching your fire... Ambush!</color>");
            Debug.Log($"[MapManager] 야영 중 습격");
            EncounterPlan.ambush = true;
            var enc = DungeonEncounter.Instance != null ? DungeonEncounter.Instance : FindFirstObjectByType<DungeonEncounter>();
            if (enc != null) enc.ForceEncounter();
            else EncounterPlan.ambush = false;
            return;
        }
        Toast($"<color=#9FFF9F>You rest by the fire.</color> <size=80%>HP·MP +{Mathf.RoundToInt(restore * 100f)}%</size>");
    }

    private void AskDescend()
    {
        int next = FloorData.floorNumber + 1;
        // [2026-10-09] B10 마지막 계단: 스켈레톤 나이트가 지키고 있다 (이기기 전에는 내려갈 수 없음)
        if (BossEncounter.HasBoss(FloorData.floorNumber))
        {
            _hud.Confirm("A cold presence bars the stairs.\n<b>The Skeleton Knight</b> rises to face you.\n<size=80%><color=#FF8A70>You cannot flee from this fight.</color></size>",
                "Fight", "Back away", ok =>
                {
                    if (!ok) return;
                    var boss = BossEncounter.LoadBoss();
                    var enc = DungeonEncounter.Instance != null ? DungeonEncounter.Instance : FindFirstObjectByType<DungeonEncounter>();
                    if (boss == null || enc == null) { Debug.LogError("[MapManager] 보스전을 시작할 수 없습니다 (보스 SO 또는 DungeonEncounter 없음)."); return; }
                    enc.StartPresetEncounter(new[] { boss });
                });
            return;
        }
        _hud.Confirm($"Stairs lead down.\nDescend to <b>B{next}</b>?", "Descend", "Stay", ok =>
        {
            if (!ok) return;
            string reward = GrantFloorClearExp();
            ChangeFloor(next, Arrival.Start);
            // [2026-10-11] 단골 손님: 여관에서 쉬고 내려온 층의 첫 3번 전투
            if (DungeonPersistentData.innVigorPending)
            {
                DungeonPersistentData.innVigorPending = false;
                DungeonPersistentData.innVigorBattlesLeft = FieldSkills.RegularInnVigorBattles;
            }
            Toast($"You descend to B{next}." + reward);
        });
    }

    /// <summary>
    /// 걸을 때 자연 회복 (스톤샤드식). stepsPerRegenHP 걸음마다 HP 1.
    /// 전투 사이 회복 수단이 포션·샘뿐이면 층이 길수록 버틸 수 없어서 넣었다 (10층 시뮬레이션).
    /// </summary>
    private void TickWalkRegen()
    {
        if (stepsPerRegenHP <= 0) return;
        if (++_regenSteps < stepsPerRegenHP) return;
        _regenSteps = 0;
        PlayerStats hero = GetHeroStats();
        if (hero != null && hero.currentHP > 0 && hero.currentHP < hero.maxHP) hero.currentHP += 1;
    }

    /// <summary>
    /// 층을 계단으로 처음 내려갈 때 탐험 EXP (+ 지도를 다 채웠으면 추가). 알림에 붙일 문구를 돌려준다.
    /// 같은 층은 한 번만 (위층에서 다시 내려와도 없음).
    /// </summary>
    private string GrantFloorClearExp()
    {
        if (FloorState == null || FloorState.clearRewarded) return "";
        FloorState.clearRewarded = true;

        int floor = FloorData.floorNumber;
        int exp = Mathf.Max(0, floorClearExpPerFloor) * floor;
        bool mapped = ExploredRatio >= 0.9999f;
        int mapExp = mapped ? Mathf.Max(0, mapCompleteExpPerFloor) * floor : 0;
        int total = exp + mapExp;
        PlayerStats hero = GetHeroStats();
        if (hero == null || total <= 0) return "";

        hero.AddExp(total);
        Debug.Log($"[MapManager] B{floor} 탐험 EXP +{total} (클리어 {exp}{(mapped ? $", 지도 완성 {mapExp}" : "")})");
        return mapped
            ? $"\n<size=80%><color=#FFD24A>Explored B{floor}: +{exp} EXP  ·  Map complete: +{mapExp} EXP</color></size>"
            : $"\n<size=80%><color=#FFD24A>Explored B{floor}: +{exp} EXP</color></size>";
    }

    private void AskAscend()
    {
        int prev = FloorData.floorNumber - 1;
        _hud.Confirm($"Stairs lead up.\nReturn to <b>B{prev}</b>?", "Ascend", "Stay", ok =>
        {
            if (!ok) return;
            ChangeFloor(prev, Arrival.DownStairs);
            Toast($"You climb back to B{prev}.");
        });
    }

    private void AskTown()
    {
        _hud.Confirm("The town gate.\nEnter the town?", "Enter", "Stay", ok =>
        {
            if (ok) OpenTown();
        });
    }

    // ─────────────────────────────────────────
    // 마을 (메뉴식): 여관 · 상점 · 저장
    // ─────────────────────────────────────────

    private void OpenTown()
    {
        _hud.ShowTown(AskInn, _hud.ShowShop, AskSave, null);
    }

    /// <summary>여관 요금: 첫 마을(B1~4) / 두 번째 마을(B5~).</summary>
    private int InnPrice
    {
        get
        {
            int p = FloorData != null && FloorData.floorNumber >= 5 ? innPriceSecondTown : innPriceFirstTown;
            // [2026-10-11] 보급술 '단골 손님' 1~5등급: 10/15/25/35/50% 할인 (최소 1G 할인)
            int rank = FieldSkills.Rank(FieldSkills.RegularCustomer);
            if (rank > 0 && p > 0)
            {
                int off = Mathf.Max(1, Mathf.RoundToInt(p * FieldSkills.RegularInnDiscount[Mathf.Clamp(rank, 1, 5) - 1]));
                p = Mathf.Max(0, p - off);
            }
            return p;
        }
    }

    private void AskInn()
    {
        int price = InnPrice;
        _hud.Confirm($"Rest at the inn for <color=#FFD24A>{price} G</color>?\n<size=75%><color=#9FC8FF>Fully restores HP and MP for the whole party.\nYou have {PlayerWallet.Gold} G.</color></size>", "Rest", "Cancel", ok =>
        {
            if (!ok) return;
            if (!PlayerWallet.TrySpend(price)) { Toast("<color=#FF6B6B>Not enough gold.</color>"); return; }
            RestAtInn();
        });
    }

    /// <summary>여관: 주인공·참전 동료 전원 HP/MP 완전 회복 + 주인공 상태이상 해제.</summary>
    private void RestAtInn()
    {
        // [2026-10-11] 단골 손님: 다음 층 첫 3번의 전투 동안 최대 HP·MP 증가 (다음 층으로 내려갈 때 시작)
        int regular = FieldSkills.Rank(FieldSkills.RegularCustomer);
        if (regular > 0)
        {
            DungeonPersistentData.innVigorPercent = FieldSkills.RegularInnVigor[Mathf.Clamp(regular, 1, 5) - 1];
            DungeonPersistentData.innVigorPending = true;
            DungeonPersistentData.innVigorBattlesLeft = 0;
        }
        PlayerStats hero = GetHeroStats();
        DungeonFieldStatus.Clear(); // 함정 상태이상도 낫는다
        if (hero != null)
        {
            hero.RemoveAllStatusEffects();
            hero.currentHP = hero.maxHP;
            hero.currentMP = hero.maxMP;
            GameManager.EnsureInstance().SaveFromPlayer(hero);
        }
        foreach (var entry in CompanionPartyPersistence.ActiveRoster)
        {
            if (entry == null) continue;
            var so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
            if (so == null) continue;
            entry.currentHP = so.HP;
            entry.currentMP = so.MP;
        }
        _hud.Flash(new Color(1f, 0.85f, 0.5f, 0.45f), 0.6f);
        Toast("<color=#FFD24A>You rest at the inn.</color>\n<size=80%>The whole party is fully restored.</size>");
        Debug.Log("[MapManager] 여관 — 파티 전원 HP/MP 완전 회복");
    }

    private void AskSave()
    {
        _hud.Confirm("Record your journey?\n<size=75%><color=#9FC8FF>Your previous save will be overwritten.</color></size>", "Save", "Cancel", ok =>
        {
            if (!ok) return;
            string err;
            if (SaveSystem.Save(out err)) Toast("<color=#FFD24A>Your journey has been recorded.</color>");
            else Toast("<color=#FF6B6B>Save failed.</color>\n<size=75%>" + err + "</size>");
        });
    }

    /// <summary>게임을 켜고 던전에 처음 들어왔을 때, 저장이 있으면 이어서 할지 묻는다 (게임 오버 후에는 묻지 않음).</summary>
    private void AskContinueFromSave()
    {
        SaveSystem.SaveData d = SaveSystem.Peek();
        if (d == null) return;
        _hud.Confirm($"Continue your journey?\n<size=75%><color=#9FC8FF>B{d.currentFloor}  ·  Lv {d.heroLevel}  ·  saved {d.savedAt}</color></size>", "Continue", "New Game", ok =>
        {
            if (!ok) return;
            string err;
            if (!SaveSystem.Load(out err)) Toast("<color=#FF6B6B>Could not load the save.</color>\n<size=75%>" + err + "</size>");
        });
    }

    // ─────────────────────────────────────────
    // 보물상자 · 샘
    // ─────────────────────────────────────────

    private void OpenChest(Vector2Int pos)
    {
        if (_player != null) _player.InterruptHold();

        // 지식의 서: 스킬 포인트(LP) +1 — 전투 외 성장 수단
        if (Random.value < chestTomeChance)
        {
            PlayerStats hero = GetHeroStats();
            if (hero != null)
            {
                hero.skillPoints += 1;
                FloorState.openedChests.Add(pos);
                Toast("Opened a chest: <color=#B98CFF>Tome of Lore</color>!\n<size=75%>+1 LP (skill point)</size>");
                Debug.Log($"[MapManager] B{FloorData.floorNumber} 보물상자 {pos} → 지식의 서 (LP {hero.skillPoints})");
                if (automapRenderer != null) automapRenderer.Rebuild();
                return;
            }
        }

        // [2026-10-08] 초반 층: 허접 무기 (스킬을 쓰기 위한 무기). [2026-10-09] 같은 장비도 다시 나옴 (30개까지 — 쌍수용)
        EquipmentData junk = Random.value < chestJunkWeaponChance ? RollEquipment(true) : null;
        // 장비
        if (junk != null || Random.value < chestEquipmentChance)
        {
            EquipmentData gear = junk != null ? junk : RollEquipment(false);
            if (gear != null && EquipmentBag.Add(gear))
            {
                FloorState.openedChests.Add(pos);
                // [2026-10-09] 자동 장착 없음 — 가방에만 넣는다 (장착은 아이템 창에서)
                Toast($"Opened a chest: <color=#FFB347>{gear.equipmentName}</color>!\n<size=75%>Added to your pack.</size>");
                Debug.Log($"[MapManager] B{FloorData.floorNumber} 보물상자 {pos} → 장비 {gear.equipmentName}");
                if (automapRenderer != null) automapRenderer.Rebuild();
                return;
            }
        }

        ConsumableItemSO item = RollLoot();
        if (item == null)
        {
            FloorState.openedChests.Add(pos);
            Toast("The chest is empty.");
        }
        else
        {
            int added = ConsumableInventory.Instance != null ? ConsumableInventory.Instance.AddItem(item, 1) : 0;
            if (added <= 0)
            {
                // 가방이 가득 참: 상자는 닫힌 채로 남겨 나중에 다시 열 수 있게
                Toast($"A chest holds <color=#FFD24A>{item.itemName}</color>,\nbut you cannot carry more.");
                return;
            }
            FloorState.openedChests.Add(pos);
            // [2026-10-11] 보급술 '전리품 수습': 25% 확률로 소비 아이템 1개 더
            if (FieldSkills.Has(FieldSkills.LootSalvage) && Random.value < FieldSkills.LootChestExtraChance)
            {
                ConsumableItemSO extra = RollLoot();
                if (extra != null && ConsumableInventory.Instance != null && ConsumableInventory.Instance.AddItem(extra, 1) > 0)
                    Toast($"<color=#9FFF9F>Salvaged an extra {extra.itemName}.</color>");
            }
            Toast($"Opened a chest: <color=#FFD24A>{item.itemName}</color>!");
            Debug.Log($"[MapManager] B{FloorData.floorNumber} 보물상자 {pos} → {item.itemName}");
        }
        if (automapRenderer != null) automapRenderer.Rebuild();
    }

    /// <summary>
    /// 상자 내용물: Resources/Item_Equipments/Items 의 소비 아이템 중 가중치 추첨 (새벽의 잔 제외).
    /// 회복약이 가장 흔하고 특수 아이템이 가장 드물다.
    /// </summary>
    private ConsumableItemSO RollLoot()
    {
        if (_loot == null)
        {
            _loot = new List<ConsumableItemSO>();
            foreach (var so in Resources.LoadAll<ConsumableItemSO>("Item_Equipments/Items"))
                if (so != null && !so.isDawnChalice && so.maxStack > 0 && !so.excludeFromChests) _loot.Add(so);
        }
        if (_loot.Count == 0) return null;

        float total = 0f;
        foreach (var so in _loot) total += LootWeight(so);
        float roll = Random.value * total;
        foreach (var so in _loot)
        {
            roll -= LootWeight(so);
            if (roll <= 0f) return so;
        }
        return _loot[_loot.Count - 1];
    }

    private List<EquipmentData> _gearPool;

    /// <summary>
    /// Resources/Item_Equipments/Equipments 중 아직 가지지 않은 장비 하나 (이 층에서 나올 수 있는 것만). 없으면 null.
    /// junkOnly = 허접 무기만, false = 허접 무기를 뺀 나머지.
    /// </summary>
    private EquipmentData RollEquipment(bool junkOnly)
    {
        if (_gearPool == null)
            _gearPool = new List<EquipmentData>(Resources.LoadAll<EquipmentData>("Item_Equipments/Equipments"));
        int floor = FloorData != null ? FloorData.floorNumber : 1;
        var candidates = _gearPool.FindAll(e => e != null && EquipmentBag.CanAdd(e) && e.isJunkWeapon == junkOnly && e.DropsOnFloor(floor));
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    private static float LootWeight(ConsumableItemSO so)
    {
        switch (so.itemCategory)
        {
            case ItemCategory.HpRecovery: return 4f;
            case ItemCategory.MpRecovery: return 3f;
            case ItemCategory.StatusCure: return 2f;
            case ItemCategory.BattleSupport: return 2f;
            default: return 1f;
        }
    }

    private void AskSpring(Vector2Int pos)
    {
        int percent = Mathf.RoundToInt(springHealPercent * 100f);
        _hud.Confirm($"A clear spring bubbles here.\nDrink from it?\n<size=75%><color=#9FC8FF>Restores {percent}% HP and MP</color></size>", "Drink", "Leave", ok =>
        {
            if (!ok) return;
            FloorState.usedSprings.Add(pos);
            PlayerStats stats = GetHeroStats();
            if (stats != null)
            {
                int hpGain = Mathf.Min(stats.maxHP - stats.currentHP, Mathf.RoundToInt(stats.maxHP * springHealPercent));
                int mpGain = Mathf.Min(stats.maxMP - stats.currentMP, Mathf.RoundToInt(stats.maxMP * springHealPercent));
                stats.currentHP += Mathf.Max(0, hpGain);
                stats.currentMP += Mathf.Max(0, mpGain);
                Toast($"You feel refreshed. <color=#8CFF8C>+{Mathf.Max(0, hpGain)} HP</color>  <color=#8CC8FF>+{Mathf.Max(0, mpGain)} MP</color>");
            }
            if (ConsumableInventory.IsRefillFloor(FloorData.floorNumber) && ConsumableInventory.Instance != null)
            {
                ConsumableInventory.Instance.RefillDawnChalice();
                Toast("<color=#9FD8FF>The Dawn Chalice is refilled.</color>");
            }
            _hud.Flash(new Color(0.4f, 0.75f, 1f, 0.35f));
            if (automapRenderer != null) automapRenderer.Rebuild();
        });
    }

    // ─────────────────────────────────────────
    // 함정
    // ─────────────────────────────────────────

    /// <summary>화면 표시용 함정 이름.</summary>
    public static string TrapLabel(FloorTrapType type)
    {
        switch (type)
        {
            case FloorTrapType.Spike: return "spike trap";
            case FloorTrapType.Teleport: return "teleport trap";
            case FloorTrapType.Alarm: return "alarm trap";
            case FloorTrapType.Pitfall: return "pitfall";
            case FloorTrapType.PoisonDart: return "poison dart trap";
            case FloorTrapType.Blade: return "blade trap";
            case FloorTrapType.FlameVent: return "flame vent";
            case FloorTrapType.BlindingGas: return "gas trap";
            case FloorTrapType.Rockfall: return "rockfall trap";
            case FloorTrapType.Net: return "net trap";
            case FloorTrapType.ManaDrain: return "mana-drain rune";
            case FloorTrapType.Crossbow: return "crossbow trap";
            default: return "trap";
        }
    }

    // [2026-10-08] 함정 피해 20~30% + 출혈 3턴 (한 걸음 = 1턴, 매 턴 4%). 함정·함정 상태이상으로도 죽는다 → 게임 오버.
    //   기본 20%, 칼날·화염 25%, 낙석·석궁 30%, 가시는 층마다 +1% (최대 30%). 해제 실패는 피해 절반.
    [Header("Traps (2026-10-07 개편)")]
    [Tooltip("모든 함정의 기본 피해 (최대 HP 비율)")]
    [Range(0f, 1f)] public float trapBaseDamagePercent = 0.20f;
    [Tooltip("모든 함정이 거는 출혈 지속 턴 (한 걸음 = 1턴, 매 턴 최대 HP 4%)")]
    public int trapBleedTurns = 3;
    [Tooltip("무거운 함정(칼날·화염)의 추가 피해")]
    [Range(0f, 1f)] public float trapHeavyBonusPercent = 0.05f;
    [Tooltip("가장 무거운 함정(낙석·석궁)의 추가 피해")]
    [Range(0f, 1f)] public float trapCrushBonusPercent = 0.10f;
    [Tooltip("가시 함정: 층마다 더해지는 피해 (최대 = 가장 무거운 함정 추가 피해만큼)")]
    [Range(0f, 0.05f)] public float spikePerFloorPercent = 0.01f;
    [Tooltip("마나 흡수 룬: 잃는 MP (최대 MP 비율)")]
    [Range(0f, 1f)] public float manaDrainPercent = 0.5f;
    [Tooltip("그물 함정: 위험도 게이지 상승량 (0~1). 가득 차면 즉시 전투")]
    [Range(0f, 1f)] public float netDangerAdd = 0.35f;

    // [2026-10-08] 함정 회피 — 밟아도 확률로 피한다 (민첩 기반, 감지와 같은 틀).
    //   회피 확률 = 기본 10% + 민첩 1당 0.25%p, 최대 50%.  예) 민첩 5: 11.3% · 50: 22.5% · 99: 34.8%
    //   피하면 피해·효과 없이 함정이 드러난다 (함정은 그대로 남아 다시 밟으면 또 굴림). 해제 실패로 터진 함정은 못 피함.
    [Header("Trap Dodge (함정 회피)")]
    [Tooltip("함정을 밟았을 때 기본 회피 확률")]
    [Range(0f, 1f)] public float trapDodgeBaseChance = 0.10f;
    [Tooltip("민첩 1당 회피 확률 증가 (0.0025 = 0.25%p, 민첩 99 → +24.75%p)")]
    [Range(0f, 0.02f)] public float trapDodgePerAgi = 0.0025f;
    [Tooltip("회피 확률 상한")]
    [Range(0f, 1f)] public float trapDodgeMaxChance = 0.50f;

    /// <summary>지금 주인공의 함정 회피 확률.</summary>
    public float TrapDodgeChance()
    {
        PlayerStats hero = GetHeroStats();
        float agi = hero != null ? hero.Agility : 0f;
        return Mathf.Clamp(trapDodgeBaseChance + agi * trapDodgePerAgi, 0f, trapDodgeMaxChance);
    }

    /// <summary>종류별 피해 비율 (기본 20% + 추가).</summary>
    private float TrapDamagePercent(FloorTrapType type)
    {
        float p = trapBaseDamagePercent;
        switch (type)
        {
            case FloorTrapType.Spike: p += Mathf.Min(trapCrushBonusPercent, spikePerFloorPercent * Mathf.Max(0, FloorData.floorNumber - 1)); break;
            case FloorTrapType.Blade:
            case FloorTrapType.FlameVent: p += trapHeavyBonusPercent; break;
            case FloorTrapType.Rockfall:
            case FloorTrapType.Crossbow: p += trapCrushBonusPercent; break;
        }
        return Mathf.Clamp01(p);
    }

    /// <summary>함정 발동. 해제됐거나 다 쓴 함정은 아무 일도 없다. 무슨 일이 있었으면 true.</summary>
    private bool TriggerTrap(Vector2Int pos, FloorTrapType type)
    {
        // 회피 판정 (작동하는 함정일 때만)
        if (FloorState.IsTrapArmed(pos, type))
        {
            float dodge = TrapDodgeChance();
            if (Random.value < dodge)
            {
                if (_player != null) _player.InterruptHold();
                FloorState.knownTraps.Add(pos); // 드러나기만 하고 발동하지 않음
                if (automapRenderer != null) automapRenderer.Rebuild();
                Debug.Log($"[MapManager] B{FloorData.floorNumber} 함정 회피 {pos}: {type} ({dodge:P1})");
                Toast($"<color=#9FFF9F>You leap aside!</color>\n<size=80%>You felt a {TrapLabel(type)} click under your foot just in time.</size>");
                return true;
            }
        }
        bool hit = TriggerTrap(pos, type, 1f, false);
        CheckHeroDeath();
        return hit;
    }

    /// <summary>damageMult: 피해 배율 (해제 실패 = 0.5). fromDisarm: 해제 실패로 발동 (제자리에서 — 이동 없음).</summary>
    private bool TriggerTrap(Vector2Int pos, FloorTrapType type, float damageMult, bool fromDisarm)
    {
        if (!FloorState.IsTrapArmed(pos, type)) return false;

        if (_player != null) _player.InterruptHold();
        FloorState.knownTraps.Add(pos);
        FloorState.sprungTraps.Add(pos);
        if (automapRenderer != null) automapRenderer.Rebuild();

        // 공통: 기본 피해 + 출혈 3턴
        float percent = TrapDamagePercent(type) * damageMult;
        int dmg = DamageHero(percent);
        DungeonFieldStatus.Add(AbyssdawnBattle.StatusEffectType.Bleed, trapBleedTurns, 1);
        string hit = $"<color=#FF6B6B>-{dmg} HP</color>  <color=#FF5A5A>Bleeding {trapBleedTurns}</color>";
        Debug.Log($"[MapManager] B{FloorData.floorNumber} 함정 발동 {pos}: {type}{(fromDisarm ? " (해제 실패)" : "")} 피해 {dmg} ({percent:P0}) + 출혈 {trapBleedTurns}턴");

        switch (type)
        {
            case FloorTrapType.PoisonDart:
                DungeonFieldStatus.Add(AbyssdawnBattle.StatusEffectType.Poison);
                _hud.Flash(new Color(0.3f, 0.9f, 0.2f, 0.45f));
                Toast($"<color=#7CFC7C>A poison dart!</color> {hit}\n<size=80%>You are <color=#7CFC7C>poisoned</color>. (Antidote cures it)</size>");
                return true;
            case FloorTrapType.Blade:
                _hud.Flash(new Color(1f, 0.05f, 0.05f, 0.55f));
                Toast($"<color=#FF5A5A>Hidden blades!</color> {hit}\n<size=80%>(Bandage stops the bleeding)</size>");
                return true;
            case FloorTrapType.FlameVent:
                DungeonFieldStatus.Add(AbyssdawnBattle.StatusEffectType.Ignite);
                _hud.Flash(new Color(1f, 0.45f, 0f, 0.55f));
                Toast($"<color=#FF9A3C>A jet of flame!</color> {hit}\n<size=80%>You are <color=#FF9A3C>burning</color>. (Coolant puts it out)</size>");
                return true;
            case FloorTrapType.BlindingGas:
                DungeonFieldStatus.Add(AbyssdawnBattle.StatusEffectType.Blind);
                if (_player != null) RevealAt(_player.gridPos); // 시야가 바로 좁아진다
                _hud.Flash(new Color(0.55f, 0.55f, 0.75f, 0.6f), 0.6f);
                Toast($"<color=#B0B0FF>Choking gas!</color> {hit}\n<size=80%>Your eyes sting — you can barely see. (Purification Water clears it)</size>");
                return true;
            case FloorTrapType.Rockfall:
                DungeonFieldStatus.Add(AbyssdawnBattle.StatusEffectType.Stun);
                _hud.Flash(new Color(0.5f, 0.4f, 0.3f, 0.7f), 0.5f);
                Toast($"<color=#D9C2A0>Rocks crash down!</color> {hit}\n<size=80%>You are <color=#FFD700>concussed</color> — you'll be stunned when the next fight starts.</size>");
                return true;
            case FloorTrapType.Crossbow:
                _hud.Flash(new Color(1f, 0.1f, 0.1f, 0.6f), 0.4f);
                Toast($"<color=#FF7A5A>Crossbow bolts fly from the walls!</color> {hit}");
                return true;
            case FloorTrapType.ManaDrain:
            {
                PlayerStats stats = GetHeroStats();
                int lostMp = 0;
                if (stats != null)
                {
                    int before = stats.currentMP;
                    stats.currentMP = Mathf.Max(0, before - Mathf.RoundToInt(stats.maxMP * manaDrainPercent * damageMult));
                    lostMp = before - stats.currentMP;
                }
                _hud.Flash(new Color(0.3f, 0.5f, 1f, 0.55f));
                Toast($"<color=#7FA8FF>A mana-drain rune!</color> {hit}  <color=#7FA8FF>-{lostMp} MP</color>");
                return true;
            }
            case FloorTrapType.Net:
            {
                float add = netDangerAdd * damageMult;
                _hud.Flash(new Color(0.9f, 0.85f, 0.5f, 0.45f));
                Toast($"<color=#E6D98C>A barbed net drops on you!</color> {hit}\n<size=80%>You struggle free... something heard that.</size>");
                if (DungeonEncounter.Instance != null) DungeonEncounter.Instance.AddDanger(add);
                return true;
            }
            case FloorTrapType.Teleport:
            {
                if (fromDisarm) { Toast($"<color=#D19BFF>The rune flares and burns you!</color> {hit}"); return true; }
                _hud.Flash(new Color(0.75f, 0.4f, 1f, 0.5f));
                FloorRoom here = FloorData.GetRoomAt(pos);
                Vector2Int target = RandomRoomCell(here != null ? here.id : -1);
                if (_player != null) _player.Teleport(target);
                RevealAt(target);
                SnapCamera();
                Toast($"<color=#D19BFF>A teleport trap!</color> {hit}\n<size=80%>You are torn away...</size>");
                return true;
            }
            case FloorTrapType.Alarm:
                _hud.Flash(new Color(1f, 0.55f, 0.15f, 0.5f));
                Toast($"<color=#FFA040>An alarm trap!</color> {hit}\n<size=80%>Monsters close in!</size>");
                StartCoroutine(AlarmRoutine());
                return true;
            case FloorTrapType.Pitfall:
            {
                if (fromDisarm) { Toast($"<color=#FF5A8C>The floor gives way — you scramble back, cut and bruised.</color> {hit}"); return true; }
                int next = FloorData.floorNumber + 1;
                _hud.Flash(new Color(0f, 0f, 0f, 0.8f), 0.6f);
                ChangeFloor(next, Arrival.RandomRoom);
                Toast($"<color=#FF5A8C>A pitfall!</color> You fall to B{next}. {hit}");
                return true;
            }
            default: // Spike
                _hud.Flash(new Color(1f, 0.15f, 0.1f, 0.5f));
                Toast($"<color=#FF6B6B>Spike trap!</color> {hit}");
                return true;
        }
    }

    private IEnumerator AlarmRoutine()
    {
        if (_player != null) _player.LockInput(this);
        yield return new WaitForSeconds(alarmDelay);
        if (_player != null) _player.UnlockInput(this);
        if (DungeonEncounter.Instance != null) DungeonEncounter.Instance.ForceEncounter();
    }

    /// <summary>최대 HP × percent 피해. [2026-10-08] 함정으로도 쓰러진다 (HP 0 → CheckHeroDeath). 실제 피해량 반환.</summary>
    private int DamageHero(float percent)
    {
        PlayerStats stats = GetHeroStats();
        if (stats == null || percent <= 0f) return 0;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(stats.maxHP * percent));
        int before = stats.currentHP;
        stats.currentHP = Mathf.Max(0, before - dmg);
        return before - stats.currentHP;
    }

    private PlayerStats GetHeroStats()
    {
        PlayerStats stats = _player != null ? _player.GetComponent<PlayerStats>() : null;
        if (stats == null) stats = FindFirstObjectByType<PlayerStats>();
        return stats;
    }

    /// <summary>방(빈 구획 제외) 안의 아무 요소도 없는 칸을 랜덤으로. excludeRoomId 방은 가능하면 피한다.</summary>
    private Vector2Int RandomRoomCell(int excludeRoomId)
    {
        var cells = new List<Vector2Int>();
        for (int pass = 0; pass < 2 && cells.Count == 0; pass++)
        {
            foreach (FloorRoom r in FloorData.rooms)
            {
                if (r.isGone || (pass == 0 && r.id == excludeRoomId)) continue;
                for (int x = r.bounds.x; x < r.bounds.xMax; x++)
                    for (int y = r.bounds.y; y < r.bounds.yMax; y++)
                    {
                        var p = new Vector2Int(x, y);
                        FloorCell c = FloorData.GetCell(p);
                        if (c.roomId == r.id && c.feature == FloorFeature.None) cells.Add(p); // 기둥·깎인 모서리 제외
                    }
            }
        }
        return cells.Count > 0 ? cells[Random.Range(0, cells.Count)] : FloorData.startPos;
    }

    private void Toast(string message)
    {
        if (_hud != null) _hud.Toast(message);
    }

    // ─────────────────────────────────────────
    // 층 만들기
    // ─────────────────────────────────────────

    /// <summary>[2026-10-08] 실험용(에디터 자동 플레이) 층 설정표 덮어쓰기. null 이면 평소대로. 게임 중에는 쓰지 않음.</summary>
    public static FloorTable DevFloorTableOverride;

    private void BuildCurrentFloor(Arrival arrival)
    {
        int floor = DungeonPersistentData.currentFloor;
        FloorTable table = DevFloorTableOverride != null ? DevFloorTableOverride : floorTable;
        CurrentFloorSettings = table != null && table.entries != null && table.entries.Count > 0
            ? table.GetEntry(floor)
            : FloorTableDefaults.Find(null, floor);

        // 층 기억: 가 본 층이면 같은 시드·지도·상자·함정 상태 그대로
        FloorState = DungeonPersistentData.GetOrCreateFloor(floor);
        DungeonPersistentData.currentSeed = FloorState.seed;
        DungeonPersistentData.revealedTiles = FloorState.revealed;

        FloorData = RogueFloorGenerator.Generate(floor, FloorState.seed, CurrentFloorSettings);
        width = FloorData.width;
        height = FloorData.height;
        _walkableCount = FloorData.CountWalkable();

        ApplyEncounterChance();

        EnsureAutomapRenderer();
        automapRenderer.Bind(FloorData, FloorState, floorTilemap);

        // 도착 자리
        if (_player == null) _player = FindFirstObjectByType<DungeonGridPlayer>();
        Vector2Int spawn = FloorData.startPos;
        DungeonDirection facing = DungeonDirection.North;
        switch (arrival)
        {
            case Arrival.SavedPosition:
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
                break;
            case Arrival.DownStairs:
                spawn = FloorData.stairsPos;
                break;
            case Arrival.RandomRoom:
                spawn = RandomRoomCell(-1);
                break;
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

        if (_hud != null)
        {
            _hud.SetFloor(floor);
            _hud.SetDangerVisible(DungeonEncounter.Instance != null && DungeonEncounter.Instance.encounterChance > 0f);
        }
        LogFloor(floor, arrival);
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
        ApplyCameraSettings(follow);
        if (screenBackground != null) follow.screenBackground = screenBackground;
        follow.Setup(_player.transform, automapRenderer.FloorWorldRect());
    }

    private void SnapCamera()
    {
        Camera cam = Camera.main;
        DungeonCameraFollow follow = cam != null ? cam.GetComponent<DungeonCameraFollow>() : null;
        if (follow != null) follow.SnapToTarget();
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

    private void LogFloor(int floor, Arrival arrival)
    {
        int rooms = 0, gone = 0;
        foreach (var r in FloorData.rooms)
        {
            if (r.isGone) gone++;
            else rooms++;
        }
        var traps = new StringBuilder();
        foreach (var p in FloorData.traps) traps.Append(FloorData.GetCell(p).trap).Append(' ');

        FloorTableEntry cfg = CurrentFloorSettings;
        Debug.Log($"[MapManager] B{floor} {arrival} — {FloorData.floorType}, " +
                  $"{cfg.sectionCols}x{cfg.sectionRows} 구획, {width}x{height}칸, 방 {rooms}개(빈 구획 {gone}), " +
                  $"문 {FloorData.doors.Count}개, 계단 {FloorData.stairsPos}" +
                  $"{(FloorData.hasStairsUp ? $", 올라가는 계단 {FloorData.stairsUpPos}" : "")}" +
                  $"{(FloorData.hasTownGate ? $", 마을 입구 {FloorData.townGatePos}" : "")}" +
                  $", 상자 {FloorData.chests.Count}, 샘 {FloorData.springs.Count}, 함정 {FloorData.traps.Count}({traps.ToString().Trim()})" +
                  $", seed {FloorState.seed}, 탐험 {Mathf.RoundToInt(ExploredRatio * 100f)}%");
        if (logFloorAscii)
            Debug.Log($"[MapManager] B{floor} 지도 (# 암반 . 방 , 통로 @ 시작 > 내려감 < 올라감 T 마을 $ 상자 ^ 함정 ~ 샘)\n{FloorData.ToAscii(null)}");
    }
}
