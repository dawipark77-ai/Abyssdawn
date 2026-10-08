using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 지도. 화면 오른쪽 위 MapButton(모바일) 또는 M 키로 켜고 끈다.
/// 켜져 있는 동안:
///  - 카메라가 층 전체가 한 화면에 들어오게 축소 (DungeonCameraFollow.SetOverview)
///  - 벽선을 굵게 해서 축소해도 잘 보이게 (AutomapRenderer.SetLineScale)
///  - 이동 잠금 + 이동 패드 숨김 (지도를 보다 실수로 움직여 전투가 시작되는 것 방지)
///  - 층 번호 옆에 탐험률 표시 (B3 · Mapped 42%)
/// 지도 내용은 평소와 같다 — 가본 곳만 보인다.
/// MapManager 가 실행 시 자동으로 붙이고 버튼을 이름으로 찾아 연결하므로 씬에서 따로 연결할 필요 없다.
/// </summary>
public class DungeonFullMap : MonoBehaviour
{
    public const string DefaultButtonName = "MapButton";

    public KeyCode toggleKey = KeyCode.M;

    public bool IsOpen { get; private set; }

    private MapManager _map;
    private GameObject _movePad;
    private bool _buttonBound;

    public void Setup(MapManager map, string buttonName = DefaultButtonName)
    {
        _map = map;
        if (_buttonBound) return;

        GameObject go = GameObject.Find(buttonName);
        Button button = go != null ? go.GetComponent<Button>() : null;
        if (button == null)
        {
            Debug.LogWarning($"[DungeonFullMap] '{buttonName}' 버튼을 찾지 못했습니다 (켜져 있는 오브젝트에 Button 컴포넌트 필요). M 키로만 열 수 있습니다.");
            return;
        }
        button.onClick.AddListener(Toggle);
        _buttonBound = true;
        Debug.Log($"[DungeonFullMap] '{buttonName}' 버튼을 전체 지도에 연결했습니다.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();
    }

    public void Toggle()
    {
        // 확인창(계단·샘 등)이 떠 있는 동안에는 열지 않는다
        if (!IsOpen && DungeonHud.Instance != null && DungeonHud.Instance.IsDialogOpen) return;
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (open == IsOpen) return;

        Camera cam = Camera.main;
        DungeonCameraFollow follow = cam != null ? cam.GetComponent<DungeonCameraFollow>() : null;
        if (follow == null)
        {
            Debug.LogWarning("[DungeonFullMap] 카메라에 DungeonCameraFollow 가 없어 전체 지도를 열 수 없습니다.");
            return;
        }
        IsOpen = open;

        // 전체 지도를 열면 떠 있던 창(상태·인벤토리 등)은 닫는다
        if (open && DungeonPanelGroup.Instance != null) DungeonPanelGroup.Instance.CloseAll();

        follow.SetOverview(open);

        AutomapRenderer automap = _map != null ? _map.automapRenderer : null;
        // 축소 비율의 제곱근만큼 굵게 — 그대로 곱하면 큰 층에서 선이 칸을 덮을 만큼 두꺼워진다.
        if (automap != null) automap.SetLineScale(open ? Mathf.Sqrt(follow.ZoomRatio) : 1f);
        if (automap != null) automap.SetOverview(open); // [2026-10-08] 전체 지도는 어둠 없이 (가 본 곳 밝게)

        DungeonGridPlayer player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null)
        {
            if (open) player.LockInput(this);
            else player.UnlockInput(this);
        }

        // 층 번호 옆에 탐험률 (세계수의 미궁식 지도 완성도)
        DungeonHud hud = DungeonHud.Instance;
        if (hud != null) hud.SetFloor(DungeonPersistentData.currentFloor, open && _map != null ? _map.ExploredRatio : -1f);

        // GameObject.Find 는 켜진 오브젝트만 찾으므로, 숨기기 전에 찾아 둔다.
        if (_movePad == null) _movePad = GameObject.Find(DungeonInputBinder.DefaultPadName);
        if (_movePad != null) _movePad.SetActive(!open);

        Debug.Log($"[DungeonFullMap] 전체 지도 {(open ? "열기" : "닫기")} (B{DungeonPersistentData.currentFloor})");
    }

    private void OnDisable()
    {
        // 씬을 떠날 때 열린 채였다면 이동 패드를 되돌려 둔다 (다음 씬 로드 시엔 어차피 새로 만들어짐).
        if (IsOpen && _movePad != null) _movePad.SetActive(true);
    }
}
