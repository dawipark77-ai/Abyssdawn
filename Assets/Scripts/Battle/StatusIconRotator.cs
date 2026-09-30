using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파티 카드의 상태이상 아이콘 줄 (StatusIconRow 에 자동으로 붙음 — PartyCardVisuals.ShowStatusIcons).
///  - 오른쪽 칸부터 채운다: 처음 걸린 상태이상 = 맨 오른쪽(StatusIcon_4), 다음 것 = 그 왼쪽 …
///  - 상태이상이 칸 수보다 많으면 rotateSeconds 마다 한 칸씩 밀려 전부 돌아가며 보인다
///    (예: 칸 4개에 5개 → 1234 → 2345 → 3451 …, 오른쪽 칸에서 하나가 빠지고 왼쪽 칸으로 새 것이 들어옴).
/// </summary>
public class StatusIconRotator : MonoBehaviour
{
    [Tooltip("상태이상이 칸보다 많을 때 한 칸씩 밀리는 간격 (초)")]
    public float rotateSeconds = 1.5f;

    private List<Image> _icons;
    private readonly List<Sprite> _sprites = new List<Sprite>();
    private int _offset;
    private float _nextRotate;

    /// <summary>표시할 칸들(왼쪽→오른쪽 순)과 상태이상 아이콘들(걸린 순서)을 넘긴다. 같은 목록이면 도는 위치 유지.</summary>
    public void Set(List<Image> icons, List<Sprite> sprites)
    {
        bool wasRotating = _icons != null && _sprites.Count > SlotCount;
        _icons = icons;
        _sprites.Clear();
        if (sprites != null) _sprites.AddRange(sprites);

        if (_sprites.Count <= SlotCount) _offset = 0;
        else
        {
            if (_offset >= _sprites.Count) _offset %= _sprites.Count;
            if (!wasRotating) _nextRotate = Time.unscaledTime + rotateSeconds; // 넘치기 시작한 순간부터 1.5초 뒤 첫 이동
        }
        Apply();
    }

    private int SlotCount => _icons != null ? _icons.Count : 0;

    private void Update()
    {
        if (_icons == null || _sprites.Count <= SlotCount) return;
        if (Time.unscaledTime < _nextRotate) return;
        _nextRotate = Time.unscaledTime + Mathf.Max(0.1f, rotateSeconds);
        _offset = (_offset + 1) % _sprites.Count;
        Apply();
    }

    private void Apply()
    {
        if (_icons == null) return;
        foreach (var img in _icons)
        {
            if (img == null) continue;
            img.enabled = false;
            img.sprite = null;
            img.color = new Color(1f, 1f, 1f, 0f);
        }

        int shown = Mathf.Min(SlotCount, _sprites.Count);
        for (int k = 0; k < shown; k++)
        {
            Image img = _icons[SlotCount - 1 - k]; // 오른쪽 칸부터
            if (img == null) continue;
            img.sprite = _sprites[(_offset + k) % _sprites.Count];
            img.enabled = true;
            img.color = Color.white;
        }
    }
}
