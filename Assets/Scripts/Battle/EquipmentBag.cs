using System;
using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle;

/// <summary>
/// 플레이어가 "가진" 장비 목록 (장착 중인 것 포함). static 이라 던전 ↔ 전투 씬 전환에도 유지된다.
/// 인벤토리 화면은 이 목록만 보여준다.
///
/// [2026-09-29] 이전에는 인벤토리가 Resources 의 모든 장비 에셋을 그대로 보여줘서,
///   얻지 않은 장비가 처음부터 전부 있는 것처럼 보였다. 획득 경로: 던전 보물상자 (MapManager).
/// 같은 장비 에셋은 하나만 가진다 (장착 여부를 에셋 참조로 판별하므로 중복이면 구분이 안 됨).
/// </summary>
public static class EquipmentBag
{
    private static readonly List<EquipmentData> _items = new List<EquipmentData>();

    /// <summary>목록이 바뀔 때 (인벤토리 화면 갱신용).</summary>
    public static event Action OnChanged;

    public static IReadOnlyList<EquipmentData> Items => _items;

    public static bool Contains(EquipmentData item) => item != null && _items.Contains(item);

    /// <summary>추가. 이미 가진 장비면 false.</summary>
    public static bool Add(EquipmentData item)
    {
        if (item == null || _items.Contains(item)) return false;
        _items.Add(item);
        OnChanged?.Invoke();
        return true;
    }

    public static bool Remove(EquipmentData item)
    {
        if (item == null || !_items.Remove(item)) return false;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>새 게임 / 게임 오버.</summary>
    public static void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        OnChanged?.Invoke();
    }

    /// <summary>테스트용: Resources 의 모든 장비 지급 (ConsumableInventory 의 F12 테스트 지급과 함께).</summary>
    public static int GrantAllForTest()
    {
        int added = 0;
        foreach (var e in Resources.LoadAll<EquipmentData>("Item_Equipments/Equipments"))
            if (e != null && !_items.Contains(e)) { _items.Add(e); added++; }
        if (added > 0) OnChanged?.Invoke();
        Debug.Log($"[EquipmentBag] 테스트 지급: 장비 {added}개");
        return added;
    }
}
