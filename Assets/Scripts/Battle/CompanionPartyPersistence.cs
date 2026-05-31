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

    /// <summary>
    /// [2026-05-25 방식①] 전투 참전 동료 인스턴스의 현재 HP/MP를 ActiveRoster에 반영.
    ///
    /// 이전 버그: ActiveRoster.Clear() 후 _companionInstances로 전부 재구성 →
    ///   다이얼로그(YES)로 ActiveRoster에 직접 추가한 신규 동료가 Clear에서 소실됨.
    ///
    /// 수정: ActiveRoster를 진실의 소스로 유지(Clear 제거). _companionInstances를 순회하며
    ///   - resourcePath가 일치하는 entry를 찾으면 그 HP/MP만 갱신(전투 중 데미지 유지).
    ///   - 못 찾으면(과거 경로 등으로 ActiveRoster에 없는 인스턴스) MaxActive 한도 내에서 Add.
    ///
    /// 매칭은 resourcePath 기준 — 같은 종류 동료 2마리의 정확한 구분은 이번 범위 밖(첫 매칭 갱신).
    /// 전투 사망 동료 제거는 이번 범위 밖(별도 작업).
    /// </summary>
    public static void SyncActiveFromInstances(IReadOnlyList<PlayerStats> companions)
    {
        if (companions == null) return;

        foreach (var stats in companions)
        {
            if (stats == null || stats.companionSource == null) continue;

            string path = GetResourcePath(stats.companionSource);

            // ActiveRoster에서 같은 resourcePath의 첫 entry 찾기
            Entry match = null;
            for (int i = 0; i < ActiveRoster.Count; i++)
            {
                if (ActiveRoster[i] != null && ActiveRoster[i].resourcePath == path)
                {
                    match = ActiveRoster[i];
                    break;
                }
            }

            if (match != null)
            {
                // 찾으면 HP/MP만 최신화 (전투 중 입은 데미지를 다음 전투까지 유지)
                match.currentHP = stats.currentHP;
                match.currentMP = stats.currentMP;
            }
            else
            {
                // 못 찾으면 신규 Add (단 MaxActive 초과 금지)
                if (ActiveRoster.Count < MaxActive)
                {
                    ActiveRoster.Add(new Entry
                    {
                        resourcePath = path,
                        currentHP = stats.currentHP,
                        currentMP = stats.currentMP
                    });
                }
            }
        }
    }
}
