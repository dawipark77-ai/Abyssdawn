using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 카메라가 플레이어를 따라가게 한다. 층 가장자리 밖은 보이지 않게 범위를 제한하고,
/// 층이 화면보다 작은 방향은 층 가운데에 고정한다.
///
/// 화면 전체를 덮는 월드 배경 그림(예: 던전 배경 스프라이트)은 카메라에 붙여 함께 움직인다
/// — 카메라가 움직이면 배경이 화면 밖으로 밀려나는 것을 막기 위함.
/// 확대/축소할 때는 배경도 같은 비율로 키우거나 줄여 항상 화면을 꽉 채운다.
///
/// 전체 지도 모드(SetOverview)에서는 층 전체가 한 화면에 들어오도록 축소하고 층 가운데를 비춘다.
/// MapManager 가 실행 시 자동으로 붙이고 설정하므로 씬에 따로 배치하지 않아도 된다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DungeonCameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("따라가는 부드러움 (초). 0이면 즉시")]
    public float smoothTime = 0.12f;
    public bool clampToFloor = true;

    [Header("확대")]
    [Tooltip("평소 지도 확대 정도 (카메라 Orthographic Size). 작을수록 지도가 크게 보인다. 0이면 씬 설정 그대로")]
    public float viewSize = 8f;
    [Tooltip("전체 지도 때 층 가장자리 바깥 여백 (월드 단위)")]
    public float overviewMargin = 1f;

    [Header("여백 (월드 단위) — UI에 가려지는 쪽을 늘리면 된다")]
    public float paddingTop = 0.5f;
    public float paddingBottom = 0.5f;
    public float paddingSide = 0.5f;

    [Header("배경")]
    [Tooltip("화면 전체를 덮는 월드 배경 스프라이트를 찾아 카메라에 붙인다")]
    public bool carryFullscreenBackground = true;

    private Camera _cam;
    private Rect _floorRect;
    private bool _hasBounds;
    private Vector3 _velocity;
    private bool _initialized;
    private bool _overview;

    // 카메라에 붙인 배경과, 붙일 때의 카메라 크기·배경 크기·위치 (확대/축소 비율 계산용)
    private float _sceneSize;
    private readonly List<Transform> _backgrounds = new List<Transform>();
    private readonly List<Vector3> _bgScales = new List<Vector3>();
    private readonly List<Vector3> _bgPositions = new List<Vector3>();

    public bool IsOverview => _overview;

    /// <summary>현재 카메라 크기 ÷ 평소 크기. 전체 지도에서 1보다 크다 (선 굵기 보정용).</summary>
    public float ZoomRatio => _cam != null ? _cam.orthographicSize / NormalSize() : 1f;

    /// <summary>MapManager 가 층을 만들 때마다 호출.</summary>
    public void Setup(Transform followTarget, Rect floorWorldRect)
    {
        Initialize();
        target = followTarget;
        _floorRect = floorWorldRect;
        _hasBounds = floorWorldRect.width > 0f && floorWorldRect.height > 0f;
        ApplySize();
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = DesiredPosition();
        _velocity = Vector3.zero;
    }

    /// <summary>전체 지도 켜기/끄기. 켜면 층 전체가 보이게 축소, 끄면 플레이어 위치로 즉시 복귀.</summary>
    public void SetOverview(bool on)
    {
        Initialize();
        _overview = on;
        ApplySize();
        if (on) transform.position = OverviewPosition();
        else SnapToTarget();
    }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _cam = GetComponent<Camera>();
        _sceneSize = _cam.orthographicSize;
        // 배경은 카메라가 움직이기 전(원래 배치 상태)에 붙여야 화면 속 위치가 유지된다.
        if (carryFullscreenBackground) CarryFullscreenBackground();
    }

    private void LateUpdate()
    {
        if (_cam == null) return;
        ApplySize(); // 인스펙터에서 viewSize 를 바꾸면 바로 반영

        if (_overview)
        {
            transform.position = OverviewPosition();
            return;
        }
        if (target == null) return;
        Vector3 desired = DesiredPosition();
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime)
            : desired;
    }

    private float NormalSize()
    {
        return viewSize > 0f ? viewSize : _sceneSize;
    }

    /// <summary>층 전체 + 여백이 화면에 들어오는 크기. 평소보다 더 확대되지는 않는다.</summary>
    private float OverviewSize()
    {
        if (!_hasBounds || _cam == null) return NormalSize();
        float halfH = _floorRect.height * 0.5f + overviewMargin;
        float halfW = _floorRect.width * 0.5f + overviewMargin;
        return Mathf.Max(NormalSize(), halfH, halfW / _cam.aspect);
    }

    private Vector3 OverviewPosition()
    {
        Vector2 c = _hasBounds ? _floorRect.center : (Vector2)transform.position;
        return new Vector3(c.x, c.y, transform.position.z);
    }

    private void ApplySize()
    {
        if (_cam == null || !_cam.orthographic) return;
        float size = _overview ? OverviewSize() : NormalSize();
        if (Mathf.Approximately(_cam.orthographicSize, size)) return;
        _cam.orthographicSize = size;
        ScaleBackgrounds();
    }

    /// <summary>배경을 카메라 크기에 비례해 키워 화면에 보이는 모습을 그대로 유지한다.</summary>
    private void ScaleBackgrounds()
    {
        if (_sceneSize <= 0f) return;
        float ratio = _cam.orthographicSize / _sceneSize;
        for (int i = 0; i < _backgrounds.Count; i++)
        {
            Transform bg = _backgrounds[i];
            if (bg == null) continue;
            Vector3 p = _bgPositions[i];
            bg.localScale = _bgScales[i] * ratio;
            bg.localPosition = new Vector3(p.x * ratio, p.y * ratio, p.z);
        }
    }

    private Vector3 DesiredPosition()
    {
        Vector3 p = target.position;
        p.z = transform.position.z;
        if (!clampToFloor || !_hasBounds || _cam == null || !_cam.orthographic) return p;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float minX = _floorRect.xMin - paddingSide + halfW;
        float maxX = _floorRect.xMax + paddingSide - halfW;
        p.x = minX > maxX ? _floorRect.center.x : Mathf.Clamp(p.x, minX, maxX);

        float minY = _floorRect.yMin - paddingBottom + halfH;
        float maxY = _floorRect.yMax + paddingTop - halfH;
        p.y = minY > maxY ? _floorRect.center.y : Mathf.Clamp(p.y, minY, maxY);
        return p;
    }

    private void CarryFullscreenBackground()
    {
        if (_cam == null || !_cam.orthographic) return;
        float viewH = _cam.orthographicSize * 2f;
        float viewW = viewH * _cam.aspect;
        Vector3 camPos = transform.position;

        var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var sr in renderers)
        {
            if (sr.transform.IsChildOf(transform)) continue;
            if (sr.GetComponentInParent<DungeonGridPlayer>() != null) continue;

            Bounds b = sr.bounds;
            bool coversView = b.size.x >= viewW * 0.9f && b.size.y >= viewH * 0.9f;
            bool centered = Mathf.Abs(b.center.x - camPos.x) < viewW * 0.25f && Mathf.Abs(b.center.y - camPos.y) < viewH * 0.25f;
            if (!coversView || !centered) continue;

            sr.transform.SetParent(transform, true);
            _backgrounds.Add(sr.transform);
            _bgScales.Add(sr.transform.localScale);
            _bgPositions.Add(sr.transform.localPosition);
            Debug.Log($"[DungeonCameraFollow] 화면 배경 '{sr.name}'을(를) 카메라에 붙였습니다 (카메라와 함께 이동).");
        }
    }
}
