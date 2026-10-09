using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [2026-10-09] 던전 메뉴의 야영(Camp) 버튼. 탐험 스킬 '야영(Make Camp)'을 배웠을 때만 보이고, 누르면 MapManager.TryMakeCamp.
/// 버튼 오브젝트(자식 그래픽)는 그대로 두고 이 컴포넌트가 켜고 끈다 — 비활성 상태에서도 알아야 하므로 부모(MenuPanel)가 아니라 버튼 자신의 그래픽만 숨긴다.
/// </summary>
[RequireComponent(typeof(Button))]
public class CampButton : MonoBehaviour
{
    private Button _button;
    private Graphic[] _graphics;
    private bool _shown = true;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _graphics = GetComponentsInChildren<Graphic>(true);
        _button.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        PlayerStats.OnStatusChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerStats.OnStatusChanged -= Refresh;
    }

    private void Update()
    {
        // 스킬을 배우는 순간(트리 창)에는 OnStatusChanged 가 안 올 수 있어 가볍게 확인
        if (Time.frameCount % 30 == 0) Refresh();
    }

    private void Refresh()
    {
        bool learned = FieldSkills.Rank(FieldSkills.MakeCamp) > 0;
        if (learned == _shown) return;
        _shown = learned;
        if (_graphics != null) foreach (var g in _graphics) if (g != null) g.enabled = learned;
        if (_button != null) _button.interactable = learned;
    }

    private void OnClick()
    {
        var map = FindFirstObjectByType<MapManager>();
        if (map != null) map.TryMakeCamp();
    }
}
