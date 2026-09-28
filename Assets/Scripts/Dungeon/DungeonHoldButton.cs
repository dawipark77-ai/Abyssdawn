using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 이동 패드 버튼 하나. 누르는 순간 한 칸, 계속 누르고 있으면 연속 이동 (모바일 기본 조작).
/// 손가락이 버튼 밖으로 나가거나 떼면 멈춘다. DungeonInputBinder 가 자동으로 붙인다.
/// </summary>
public class DungeonHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public DungeonGridPlayer player;
    public DungeonDirection direction;

    private bool _pressed;
    private Selectable _selectable;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (player == null) return;
        if (_selectable != null && !_selectable.IsInteractable()) return;
        _pressed = true;
        player.BeginHold(direction);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();
    public void OnPointerExit(PointerEventData eventData) => Release();
    private void OnDisable() => Release();

    private void Release()
    {
        if (!_pressed) return;
        _pressed = false;
        if (player != null) player.EndHold(direction);
    }
}
