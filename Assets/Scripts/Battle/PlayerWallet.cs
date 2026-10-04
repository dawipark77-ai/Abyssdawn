using System;
using UnityEngine;

/// <summary>
/// 파티 소지금 (골드). static 이라 던전 ↔ 전투 씬 전환에도 유지되고, 저장 파일(SaveSystem)에 함께 저장된다.
///  - 새 게임 시작 금액: StartingGold (100G — 첫 마을에서 "조잡한 검(100G)이냐 포션이냐"를 고르게)
///  - 전투 승리: 쓰러뜨린 몬스터 골드 합 (도망친 몬스터는 0) — BattleManager
///  - 마을: 여관 요금, 상점 구매·판매 — MapManager / DungeonShop
///  - 게임 오버: 저장이 있으면 저장 당시 금액, 없으면 새 게임 금액
/// </summary>
public static class PlayerWallet
{
    public const int StartingGold = 100;
    public const int MaxGold = 999999;

    private static int _gold = StartingGold;

    /// <summary>금액이 바뀔 때 (Money 표시 갱신용).</summary>
    public static event Action<int> OnChanged;

    public static int Gold => _gold;

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Set(_gold + amount);
    }

    /// <summary>돈이 충분하면 빼고 true. 모자라면 아무것도 안 하고 false.</summary>
    public static bool TrySpend(int amount)
    {
        if (amount < 0) return false;
        if (_gold < amount) return false;
        Set(_gold - amount);
        return true;
    }

    public static void Set(int amount)
    {
        _gold = Mathf.Clamp(amount, 0, MaxGold);
        OnChanged?.Invoke(_gold);
    }

    /// <summary>새 게임 (저장 없이 게임 오버 포함).</summary>
    public static void ResetForNewRun() { Set(StartingGold); }
}
