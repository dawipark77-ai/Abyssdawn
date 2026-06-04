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
        // [2026-05-25 ID 1단계] 영입 개체별 고유 ID. 같은 종(resourcePath 동일)이라도 개체 구분용.
        // 나중에 개체 이름 부여, 정확한 방출/순서 이동에 사용. 세션 내 유일성만 보장(영속화는 별도).
        public int id;
        public string resourcePath;
        public int currentHP;
        public int currentMP;
    }

    // [2026-05-25 ID 2단계] 대기열 전용 경량 항목. id + resourcePath만 보관.
    // 대기 동료는 전투 참전을 안 하므로 currentHP/MP 변동이 없음 → Entry의 HP/MP 필드 불필요.
    // 의미 있는 필드만 둬서 데이터 의도를 명확히 함. (활성 승격 시 HP/MP는 CompanionSO.HP로 채우면 됨)
    [Serializable]
    public class WaitEntry
    {
        public int id;
        public string resourcePath;
    }

    // [2026-05-25 ID 1단계] 동료 개체 ID 순차 카운터. 발급 시마다 ++.
    // 활성·대기 전체에서 공유 → 전역 유일. static이라 Domain Reload 시 1로 리셋
    // (세션 내 유일성만 보장, 저장/로드는 이번 범위 아님).
    private static int _nextCompanionId = 1;

    public const int MaxActive = 3;
    public const int MaxWaitlist = 3;

    // [2026-05-25 고정 3칸] ActiveRoster는 항상 Count==MaxActive(3) 고정. 각 칸은 Entry 또는 null.
    //   - ActiveRoster[0]=슬롯0(Companion_SLot2), [1]=슬롯1(Slot3), [2]=슬롯2(Slot4) — 인덱스=슬롯 고정.
    //   - 방출 시 그 칸을 null로 (당겨오기 없음, 빈 자리 유지). RemoveAt 금지.
    //   - "실제 동료 수"는 CountActive()로 (Count는 항상 3이므로 의미 없음).
    // static 생성자에서 null 3칸으로 초기화.
    public static readonly List<Entry> ActiveRoster = new List<Entry>(MaxActive);
    // [2026-05-25 ID 2단계] List<string> → List<WaitEntry> (id 포함). 이름은 호환 위해 WaitlistPaths 유지.
    // (이번 1차 작업에서 WaitlistPaths는 가변 리스트 그대로 — 고정 칸 변경은 별도 작업)
    public static readonly List<WaitEntry> WaitlistPaths = new List<WaitEntry>(MaxWaitlist);

    static CompanionPartyPersistence()
    {
        EnsureActiveRosterFixedSize();
    }

    /// <summary>ActiveRoster를 항상 MaxActive(3)칸으로 보장. 부족하면 null로 채움.</summary>
    private static void EnsureActiveRosterFixedSize()
    {
        while (ActiveRoster.Count < MaxActive) ActiveRoster.Add(null);
        // 혹시 초과분이 있으면 잘라냄(방어적 — 정상 흐름에선 발생 안 함)
        while (ActiveRoster.Count > MaxActive) ActiveRoster.RemoveAt(ActiveRoster.Count - 1);
    }

    /// <summary>활성 동료 실제 수 (null 아닌 칸 수). 영입 가드는 이 값으로 판정.</summary>
    public static int CountActive()
    {
        int n = 0;
        for (int i = 0; i < ActiveRoster.Count; i++)
            if (ActiveRoster[i] != null) n++;
        return n;
    }

    /// <summary>대기열 실제 수 (null 아닌 항목). WaitlistPaths는 가변이라 보통 Count와 같지만 일관성 위해 제공.</summary>
    public static int CountWait()
    {
        int n = 0;
        for (int i = 0; i < WaitlistPaths.Count; i++)
            if (WaitlistPaths[i] != null) n++;
        return n;
    }

    public static void Clear()
    {
        // [2026-05-25 고정 3칸] Clear 후에도 ActiveRoster는 3칸 null 유지.
        ActiveRoster.Clear();
        EnsureActiveRosterFixedSize();
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
        EnsureActiveRosterFixedSize();
        string path = GetResourcePath(data);

        // [2026-05-25 고정 3칸] 빈 null 칸을 앞에서부터 찾아 채움 (Add 대신).
        int slot = FindFirstEmptyActiveSlot();
        if (slot >= 0)
        {
            int newId = _nextCompanionId++;
            ActiveRoster[slot] = new Entry { id = newId, resourcePath = path, currentHP = currentHP, currentMP = currentMP };
            Debug.Log($"[Companion-ID] 발급: resourcePath='{path}', id={newId} (TryAddActive, slot={slot})");
            return true;
        }

        // 빈 칸 없음(3칸 다 참) → 대기열로
        if (WaitlistPaths.Count < MaxWaitlist)
        {
            // [2단계] 대기열도 id 발급 (_nextCompanionId 공유 → 활성·대기 전역 유일)
            int waitId = _nextCompanionId++;
            WaitlistPaths.Add(new WaitEntry { id = waitId, resourcePath = path });
            Debug.Log($"[Companion-ID] 발급(대기열): resourcePath='{path}', id={waitId} (TryAddActive 대기열)");
            return false;
        }

        return false;
    }

    /// <summary>ActiveRoster에서 가장 앞의 빈(null) 칸 인덱스. 없으면 -1.</summary>
    private static int FindFirstEmptyActiveSlot()
    {
        for (int i = 0; i < ActiveRoster.Count; i++)
            if (ActiveRoster[i] == null) return i;
        return -1;
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
        EnsureActiveRosterFixedSize();

        foreach (var stats in companions)
        {
            if (stats == null || stats.companionSource == null) continue;

            string path = GetResourcePath(stats.companionSource);

            // [2026-05-25 ID 3단계] 매칭 우선순위:
            //   1) stats.companionId != 0 → entry.id == companionId 정확 매칭 (같은 종 2마리 구분)
            //   2) companionId == 0(미할당) → resourcePath 첫 매칭 폴백 (안전장치)
            //   3) 어느 쪽도 못 찾음 → 신규 Add (새 id 발급)
            Entry match = null;

            if (stats.companionId != 0)
            {
                for (int i = 0; i < ActiveRoster.Count; i++)
                {
                    if (ActiveRoster[i] != null && ActiveRoster[i].id == stats.companionId)
                    {
                        match = ActiveRoster[i];
                        break;
                    }
                }
                if (match != null)
                    Debug.Log($"[Sync-DIAG] 인스턴스 companionId={stats.companionId} → entry.id={match.id} 매칭, HP/MP 갱신 ({stats.currentHP}/{stats.currentMP})");
                else
                    Debug.LogWarning($"[Sync-DIAG] 인스턴스 companionId={stats.companionId} → 해당 id의 entry 없음. 신규 Add로 처리");
            }
            else
            {
                // id=0 → resourcePath 첫 매칭 폴백
                for (int i = 0; i < ActiveRoster.Count; i++)
                {
                    if (ActiveRoster[i] != null && ActiveRoster[i].resourcePath == path)
                    {
                        match = ActiveRoster[i];
                        break;
                    }
                }
                if (match != null)
                    Debug.Log($"[Sync-DIAG] 인스턴스 companionId=0 → resourcePath 폴백 매칭 (entry.id={match.id}, path='{path}'), HP/MP 갱신");
                else
                    Debug.Log($"[Sync-DIAG] 인스턴스 companionId=0 → resourcePath 폴백도 실패 ('{path}'). 신규 Add로 처리");
            }

            if (match != null)
            {
                // 찾으면 HP/MP만 최신화 (전투 중 입은 데미지를 다음 전투까지 유지)
                match.currentHP = stats.currentHP;
                match.currentMP = stats.currentMP;
            }
            else
            {
                // 못 찾으면 빈 null 칸을 찾아 채움 (고정 3칸 — Add 대신)
                int slot = FindFirstEmptyActiveSlot();
                if (slot >= 0)
                {
                    int newId = _nextCompanionId++;
                    ActiveRoster[slot] = new Entry
                    {
                        id = newId,
                        resourcePath = path,
                        currentHP = stats.currentHP,
                        currentMP = stats.currentMP
                    };
                    Debug.Log($"[Companion-ID] 발급: resourcePath='{path}', id={newId} (SyncActiveFromInstances 신규분, slot={slot})");
                }
            }
        }
    }
}
