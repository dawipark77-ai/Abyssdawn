using System;
using System.Collections.Generic;
using UnityEngine;
using Abyssdawn;

/// <summary>
/// 영입 동료(CompanionSO) 런타임 로스터 — 던전↔전투 씬 전환 간 유지.
/// 슬롯 1=Hero, 슬롯 2~4=activeRoster 순서(최대 3).
/// </summary>
public static class CompanionPartyPersistence
{
    [Serializable]
    public class Entry
    {
        public string resourcePath;
        public int currentHP;
        public int currentMP;
    }

    public const int MaxActive = 3;
    public const int MaxWaitlist = 3;

    public static readonly List<Entry> ActiveRoster = new List<Entry>(MaxActive);
    public static readonly List<string> WaitlistPaths = new List<string>(MaxWaitlist);

    public static void Clear()
    {
        ActiveRoster.Clear();
        WaitlistPaths.Clear();
    }

    /// <summary>Resources.Load 경로 (Assets/Resources/ 하위, 확장자 제외).</summary>
    public static string GetResourcePath(CompanionSO so)
    {
        if (so == null) return null;
        return $"Monsters/CompanionData/{so.name}";
    }

    public static CompanionSO LoadCompanion(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        return Resources.Load<CompanionSO>(resourcePath);
    }

    public static bool TryAddActive(CompanionSO data, int currentHP, int currentMP)
    {
        if (data == null) return false;
        string path = GetResourcePath(data);
        if (ActiveRoster.Count < MaxActive)
        {
            ActiveRoster.Add(new Entry { resourcePath = path, currentHP = currentHP, currentMP = currentMP });
            return true;
        }

        if (WaitlistPaths.Count < MaxWaitlist)
        {
            WaitlistPaths.Add(path);
            return false;
        }

        return false;
    }

    public static void SyncActiveFromInstances(IReadOnlyList<PlayerStats> companions)
    {
        ActiveRoster.Clear();
        if (companions == null) return;

        foreach (var stats in companions)
        {
            if (stats == null || stats.companionSource == null) continue;
            if (ActiveRoster.Count >= MaxActive) break;
            ActiveRoster.Add(new Entry
            {
                resourcePath = GetResourcePath(stats.companionSource),
                currentHP = stats.currentHP,
                currentMP = stats.currentMP
            });
        }
    }
}
