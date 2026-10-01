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
    [Tooltip("가시 함정 피해 (최대 HP 비율). HP 는 1 아래로 내려가지 않는다")]
    [Range(0f, 1f)] public float spikeDamagePercent = 0.1f;
    [Tooltip("구멍 함정으로 떨어질 때 피해 (최대 HP 비율)")]
    [Range(0f, 1f)] public float pitfallDamagePercent = 0.05f;
    [Tooltip("회복의 샘 회복량 (최대 HP·MP 비율)")]
    [Range(0f, 1f)] public float springHealPercent = 0.5f;
    [Tooltip("경보 함정 문구를 보여준 뒤 전투가 시작되기까지 (초)")]
    public float alarmDelay = 0.8f;
    [Tooltip("보물상자에서 장비가 나올 확률 (아직 없는 장비 중에서). 나머지는 소비 아이템")]
    [Range(0f, 1f)] public float chestEquipmentChance = 0.3f;
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
            // 새 탐험: 층 기억·위험도·빛 초기화
            DungeonPersistentData.floors.Clear();
            DungeonPersistentData.danger = 0f;
            DungeonPersistentData.playerLightSteps = 0;
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

        // 화면 아래 파티 카드 (전투의 PartyBar 를 그대로 배치하면 자동 연결)
        if (FindFirstObjectByType<DungeonPartyBar>() == null) gameObject.AddComponent<DungeonPartyBar>();

        // 창(상태·인벤토리·Oath & Path 등)은 한 번에 하나만 열리게
        Transform canvas = DungeonPanelGroup.FindPanelCanvas(uiCanvasName);
        if (canvas != null) DungeonPanelGroup.Setup(canvas);
        else Debug.LogWarning($"[MapManager] 창들이 들어 있는 '{uiCanvasName}' 을(를) 찾지 못해 창 겹침 방지를 켜지 못했습니다.");
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
        RevealAt(pos);
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
        bool litRoom = room != null && room.lit;
        bool carryingLight = DungeonPersistentData.playerLightSteps > 0;
        if (litRoom || carryingLight) r += lightSightBonus;
        return Mathf.Max(1, r);
    }

    /// <summary>
    /// 플레이어가 steps 걸음 동안 빛을 든다 (시야 +2). 횃불·새벽불 등 빛 아이템이 정해지면 여기에 연결.
    /// </summary>
    public void AddPlayerLight(int steps)
    {
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 테스트용: Ctrl+L = 빛 30걸음, Ctrl+K = 어두운 층 켜고 끄기
    private void Update()
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

    /// <summary>
    /// pos 기준으로 시야를 계산해 지도를 공개하고, 보이는 칸이 바뀌었으니 자동 지도를 다시 그린다.
    /// 새로 기억된 칸이 있으면 true.
    /// </summary>
    public bool RevealAt(Vector2Int pos)
    {
        if (FloorData == null || FloorState == null) return false;
        bool changed = FloorVisibility.RevealAround(FloorData, pos, FloorState.revealed, SightRadiusAt(pos), _visible);
        if (automapRenderer != null)
        {
            automapRenderer.SetVisible(_visible);
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
    }

    public string TrapName(Vector2Int pos)
    {
        return FloorData != null ? FloorData.GetCell(pos).trap.ToString() : "";
    }

    // ─────────────────────────────────────────
    // 계단 · 마을 입구
    // ─────────────────────────────────────────

    private void AskDescend()
    {
        int next = FloorData.floorNumber + 1;
        _hud.Confirm($"Stairs lead down.\nDescend to <b>B{next}</b>?", "Descend", "Stay", ok =>
        {
            if (!ok) return;
            string reward = GrantFloorClearExp();
            ChangeFloor(next, Arrival.Start);
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
            if (!ok) return;
            if (!DungeonTownGate.Enter(townSceneName, _player))
                Toast("The town is not open yet.");
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

        // 장비 (아직 없는 것 중에서) — 장비를 얻는 유일한 경로
        if (Random.value < chestEquipmentChance)
        {
            EquipmentData gear = RollEquipment();
            if (gear != null && EquipmentBag.Add(gear))
            {
                FloorState.openedChests.Add(pos);
                Toast($"Opened a chest: <color=#FFB347>{gear.equipmentName}</color>!\n<size=75%>New equipment added to your pack.</size>");
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
                if (so != null && !so.isDawnChalice && so.maxStack > 0) _loot.Add(so);
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

    /// <summary>Resources/Item_Equipments/Equipments 중 아직 가지지 않은 장비 하나. 다 가졌으면 null.</summary>
    private EquipmentData RollEquipment()
    {
        if (_gearPool == null)
            _gearPool = new List<EquipmentData>(Resources.LoadAll<EquipmentData>("Item_Equipments/Equipments"));
        var candidates = _gearPool.FindAll(e => e != null && !EquipmentBag.Contains(e));
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

    /// <summary>함정 발동. 다 쓴 함정(가시 외)은 아무 일도 없다. 무슨 일이 있었으면 true.</summary>
    private bool TriggerTrap(Vector2Int pos, FloorTrapType type)
    {
        bool known = FloorState.knownTraps.Contains(pos);
        if (known && AutomapRenderer.IsTrapSpent(type)) return false;

        if (_player != null) _player.InterruptHold();
        FloorState.knownTraps.Add(pos);
        if (automapRenderer != null) automapRenderer.Rebuild();
        Debug.Log($"[MapManager] B{FloorData.floorNumber} 함정 발동 {pos}: {type}");

        switch (type)
        {
            case FloorTrapType.Teleport:
            {
                _hud.Flash(new Color(0.75f, 0.4f, 1f, 0.5f));
                FloorRoom here = FloorData.GetRoomAt(pos);
                Vector2Int target = RandomRoomCell(here != null ? here.id : -1);
                if (_player != null) _player.Teleport(target);
                RevealAt(target);
                SnapCamera();
                Toast("<color=#D19BFF>A teleport trap!</color> You are whisked away...");
                return true;
            }
            case FloorTrapType.Alarm:
                _hud.Flash(new Color(1f, 0.55f, 0.15f, 0.5f));
                Toast("<color=#FFA040>An alarm trap!</color> Monsters close in!");
                StartCoroutine(AlarmRoutine());
                return true;
            case FloorTrapType.Pitfall:
            {
                int next = FloorData.floorNumber + 1;
                int dmg = DamageHero(pitfallDamagePercent);
                _hud.Flash(new Color(0f, 0f, 0f, 0.8f), 0.6f);
                ChangeFloor(next, Arrival.RandomRoom);
                Toast($"<color=#FF5A8C>A pitfall!</color> You fall to B{next}. <color=#FF6B6B>-{dmg} HP</color>");
                return true;
            }
            default: // Spike
            {
                int dmg = DamageHero(spikeDamagePercent);
                _hud.Flash(new Color(1f, 0.15f, 0.1f, 0.5f));
                Toast($"<color=#FF6B6B>Spike trap! -{dmg} HP</color>");
                return true;
            }
        }
    }

    private IEnumerator AlarmRoutine()
    {
        if (_player != null) _player.LockInput(this);
        yield return new WaitForSeconds(alarmDelay);
        if (_player != null) _player.UnlockInput(this);
        if (DungeonEncounter.Instance != null) DungeonEncounter.Instance.ForceEncounter();
    }

    /// <summary>최대 HP × percent 피해. 함정으로는 쓰러지지 않는다 (HP 최소 1). 실제 피해량 반환.</summary>
    private int DamageHero(float percent)
    {
        PlayerStats stats = GetHeroStats();
        if (stats == null || percent <= 0f) return 0;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(stats.maxHP * percent));
        int before = stats.currentHP;
        stats.currentHP = Mathf.Max(1, before - dmg);
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

    private void BuildCurrentFloor(Arrival arrival)
    {
        int floor = DungeonPersistentData.currentFloor;
        CurrentFloorSettings = floorTable != null && floorTable.entries != null && floorTable.entries.Count > 0
            ? floorTable.GetEntry(floor)
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
