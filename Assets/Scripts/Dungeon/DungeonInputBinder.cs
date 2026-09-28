using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 화면 이동 패드(MovePad)의 버튼을 DungeonGridPlayer 의 4방향 이동에 연결한다.
/// MapManager 가 던전 시작 시 BindMovePad 를 자동 호출하므로 씬에 따로 붙일 필요는 없다.
///
/// [2026-09-29] 예전 버전은 씬의 "모든" 버튼 중 이름에 Back/Left 등이 들어간 것을 찾아 기존 기능을 지우고
///   이동을 연결해서, 메뉴의 뒤로가기 버튼까지 망가뜨릴 수 있었다. 이제 MovePad 안의 버튼만, 정확한 이름으로 찾는다.
///   또 MovePad 버튼들은 이미 삭제된 PlayerMover 를 가리키고 있어 모바일에서 이동이 불가능했다.
/// </summary>
public class DungeonInputBinder : MonoBehaviour
{
    public const string DefaultPadName = "MovePad";

    // 같은 패드를 두 번 연결하면 한 번 눌러 두 칸씩 움직이므로, 패드에 붙은 이 컴포넌트에 표시해 둔다.
    private bool _bound;

    void Start()
    {
        var player = FindFirstObjectByType<DungeonGridPlayer>();
        if (player != null) BindMovePad(player);
    }

    /// <summary>MovePad 안의 버튼을 이름으로 찾아 연결. 연결한 버튼 수를 반환.</summary>
    public static int BindMovePad(DungeonGridPlayer player, string padName = DefaultPadName)
    {
        if (player == null) return 0;

        GameObject pad = GameObject.Find(padName);
        if (pad == null)
        {
            Debug.LogWarning($"[DungeonInputBinder] '{padName}' 오브젝트를 찾지 못해 화면 이동 버튼을 연결하지 못했습니다.");
            return 0;
        }
        var marker = pad.GetComponent<DungeonInputBinder>();
        if (marker == null) marker = pad.AddComponent<DungeonInputBinder>();
        if (marker._bound) return 0;
        marker._bound = true;

        int count = 0;
        foreach (var button in pad.GetComponentsInChildren<Button>(true))
        {
            UnityAction action = ActionFor(player, button.name);
            if (action == null) continue;
            button.onClick.AddListener(action);
            count++;
        }
        Debug.Log($"[DungeonInputBinder] '{padName}' 버튼 {count}개를 4방향 이동에 연결했습니다.");
        return count;
    }

    /// <summary>
    /// 탑다운 절대 방향 이동: 위=북, 아래=남, 왼쪽=서, 오른쪽=동.
    /// L Side / R Side(옛 1인칭 게걸음 버튼)는 왼쪽/오른쪽과 같게 둔다.
    /// </summary>
    private static UnityAction ActionFor(DungeonGridPlayer player, string buttonName)
    {
        switch (buttonName.Trim().ToLowerInvariant())
        {
            case "up":
            case "forward":
            case "btn_forward":
                return player.MoveNorth;
            case "down":
            case "back":
            case "backward":
                return player.MoveSouth;
            case "left":
            case "l side":
            case "btn_left":
                return player.MoveWest;
            case "right":
            case "r side":
            case "btn_right":
                return player.MoveEast;
            default:
                return null;
        }
    }
}
