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
    // [2026-05-25 방출 작업] WaitlistPaths도 ActiveRoster처럼 고정 3칸(null 허용)으로 전환.
    //   - WaitlistPaths[0]=Reserves1, [1]=Reserves2, [2]=Reserves3 — 인덱스=슬롯 고정.
    //   - 방출 시 그 칸을 null로 (당겨오기 없음, 빈 자리 유지). 활성과 일관.
    public static readonly List<WaitEntry> WaitlistPaths = new List<WaitEntry>(MaxWaitlist);

    // [2026-06-15 Hero 이동 Step 1] MainLine 슬롯 수 = Hero 1 + 동료 MaxActive(3) = 4.
    public const int MainLineSlots = MaxActive + 1;

    // [2026-06-15 Hero 이동 Step 1] Hero가 위치한 MainLine 슬롯 인덱스 (0=슬롯1 ~ 3=슬롯4).
    //   ActiveRoster[0,1,2]는 "Hero를 뺀 나머지 슬롯을 오름차순으로" 채운다.
    //   ActiveRoster처럼 static — 씬 전환 간 유지, Domain Reload 시 0으로 리셋. Clear()에서도 0.
    //   기본값 0이면 Hero=슬롯1로 기존 동작과 완전히 동일.
    public static int heroSlotIndex = 0;

    static CompanionPartyPersistence()
    {
        EnsureActiveRosterFixedSize();
        EnsureWaitlistFixedSize();
    }

    /// <summary>ActiveRoster를 항상 MaxActive(3)칸으로 보장. 부족하면 null로 채움.</summary>
    private static void EnsureActiveRosterFixedSize()
    {
        while (ActiveRoster.Count < MaxActive) ActiveRoster.Add(null);
        // 혹시 초과분이 있으면 잘라냄(방어적 — 정상 흐름에선 발생 안 함)
        while (ActiveRoster.Count > MaxActive) ActiveRoster.RemoveAt(ActiveRoster.Count - 1);
    }

    /// <summary>WaitlistPaths를 항상 MaxWaitlist(3)칸으로 보장. 부족하면 null로 채움.</summary>
    private static void EnsureWaitlistFixedSize()
    {
        while (WaitlistPaths.Count < MaxWaitlist) WaitlistPaths.Add(null);
        while (WaitlistPaths.Count > MaxWaitlist) WaitlistPaths.RemoveAt(WaitlistPaths.Count - 1);
    }

    /// <summary>WaitlistPaths에서 가장 앞의 빈(null) 칸 인덱스. 없으면 -1.</summary>
    private static int FindFirstEmptyWaitSlot()
    {
        for (int i = 0; i < WaitlistPaths.Count; i++)
            if (WaitlistPaths[i] == null) return i;
        return -1;
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

    /// <summary>같은 종(MonsterSO)의 동료를 몇 마리 데리고 있는지 (참전 + 대기).</summary>
    public static int CountOwned(MonsterSO so)
    {
        string path = GetResourcePath(so);
        if (path == null) return 0;
        int n = 0;
        for (int i = 0; i < ActiveRoster.Count; i++)
            if (ActiveRoster[i] != null && ActiveRoster[i].resourcePath == path) n++;
        for (int i = 0; i < WaitlistPaths.Count; i++)
            if (WaitlistPaths[i] != null && WaitlistPaths[i].resourcePath == path) n++;
        return n;
    }

    public static void Clear()
    {
        // [2026-05-25 고정 3칸] Clear 후에도 ActiveRoster·WaitlistPaths는 3칸 null 유지.
        ActiveRoster.Clear();
        EnsureActiveRosterFixedSize();
        WaitlistPaths.Clear();
        EnsureWaitlistFixedSize();
        heroSlotIndex = 0;   // [2026-06-15 Hero 이동 Step 1] Hero 위치도 기본(슬롯1)으로 리셋
    }

    /// <summary>Resources.Load 경로 (Assets/Resources/ 하위, 확장자 제외).</summary>
    public static string GetResourcePath(MonsterSO so)
    {
        if (so == null) return null;
        return $"Monsters/{so.name}";
    }

    public static MonsterSO LoadCompanion(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        return Resources.Load<MonsterSO>(resourcePath);
    }

    public static bool TryAddActive(MonsterSO data, int currentHP, int currentMP)
    {
        if (data == null) return false;
        EnsureActiveRosterFixedSize();
        EnsureWaitlistFixedSize();
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

        // 빈 칸 없음(3칸 다 참) → 대기열로 (대기열도 고정 3칸 빈 칸 채우기)
        int waitSlot = FindFirstEmptyWaitSlot();
        if (waitSlot >= 0)
        {
            // [2단계] 대기열도 id 발급 (_nextCompanionId 공유 → 활성·대기 전역 유일)
            int waitId = _nextCompanionId++;
            WaitlistPaths[waitSlot] = new WaitEntry { id = waitId, resourcePath = path };
            Debug.Log($"[Companion-ID] 발급(대기열): resourcePath='{path}', id={waitId} (TryAddActive 대기열, slot={waitSlot})");
            return false;
        }

        return false;
    }

    /// <summary>활성 슬롯 방출 — 해당 칸을 null로 (빈 자리 유지, 당겨오기 없음).</summary>
    public static bool ReleaseActive(int slotIndex)
    {
        EnsureActiveRosterFixedSize();
        if (slotIndex < 0 || slotIndex >= ActiveRoster.Count)
        {
            Debug.LogWarning($"[Release] ReleaseActive: 잘못된 slotIndex={slotIndex} (범위 0~{ActiveRoster.Count - 1})");
            return false;
        }
        var e = ActiveRoster[slotIndex];
        if (e == null)
        {
            Debug.LogWarning($"[Release] ReleaseActive: slot {slotIndex} 이미 비어 있음");
            return false;
        }
        Debug.Log($"[Release] 활성 동료 방출 — slot={slotIndex}, id={e.id}, path='{e.resourcePath}' → null");
        ActiveRoster[slotIndex] = null;
        return true;
    }

    /// <summary>대기열 슬롯 방출 — 해당 칸을 null로 (빈 자리 유지, 당겨오기 없음).</summary>
    public static bool ReleaseWait(int slotIndex)
    {
        EnsureWaitlistFixedSize();
        if (slotIndex < 0 || slotIndex >= WaitlistPaths.Count)
        {
            Debug.LogWarning($"[Release] ReleaseWait: 잘못된 slotIndex={slotIndex} (범위 0~{WaitlistPaths.Count - 1})");
            return false;
        }
        var w = WaitlistPaths[slotIndex];
        if (w == null)
        {
            Debug.LogWarning($"[Release] ReleaseWait: slot {slotIndex} 이미 비어 있음");
            return false;
        }
        Debug.Log($"[Release] 대기 동료 방출 — slot={slotIndex}, id={w.id}, path='{w.resourcePath}' → null");
        WaitlistPaths[slotIndex] = null;
        return true;
    }

    /// <summary>ActiveRoster에서 가장 앞의 빈(null) 칸 인덱스. 없으면 -1.</summary>
    private static int FindFirstEmptyActiveSlot()
    {
        for (int i = 0; i < ActiveRoster.Count; i++)
            if (ActiveRoster[i] == null) return i;
        return -1;
    }

    // ─────────────────────────────────────────────────────────────────
    // [2026-06-13 Tactics swap] 슬롯 자리 바꾸기 — "동료끼리만".
    //   Hero 이동·빈 칸 압축 문제는 이번 범위 밖(다음 작업).
    //   세 함수 모두 인덱스=슬롯 고정 원칙 유지(당겨오기 없음, 빈 칸은 그대로 자리 교환).
    // ─────────────────────────────────────────────────────────────────

    /// <summary>활성 ↔ 활성: ActiveRoster[i]와 [j]의 Entry 참조 맞바꿈 (null 포함 가능).</summary>
    public static bool SwapActive(int i, int j)
    {
        EnsureActiveRosterFixedSize();
        if (i < 0 || i >= ActiveRoster.Count || j < 0 || j >= ActiveRoster.Count)
        {
            Debug.LogWarning($"[Swap] SwapActive: 잘못된 인덱스 i={i}, j={j} (범위 0~{ActiveRoster.Count - 1})");
            return false;
        }
        if (i == j) return false;
        var tmp = ActiveRoster[i];
        ActiveRoster[i] = ActiveRoster[j];
        ActiveRoster[j] = tmp;
        Debug.Log($"[Swap] 활성↔활성 — ActiveRoster[{i}] ↔ ActiveRoster[{j}]");
        return true;
    }

    /// <summary>대기 ↔ 대기: WaitlistPaths[i]와 [j]의 WaitEntry 참조 맞바꿈 (null 포함 가능).</summary>
    public static bool SwapWait(int i, int j)
    {
        EnsureWaitlistFixedSize();
        if (i < 0 || i >= WaitlistPaths.Count || j < 0 || j >= WaitlistPaths.Count)
        {
            Debug.LogWarning($"[Swap] SwapWait: 잘못된 인덱스 i={i}, j={j} (범위 0~{WaitlistPaths.Count - 1})");
            return false;
        }
        if (i == j) return false;
        var tmp = WaitlistPaths[i];
        WaitlistPaths[i] = WaitlistPaths[j];
        WaitlistPaths[j] = tmp;
        Debug.Log($"[Swap] 대기↔대기 — WaitlistPaths[{i}] ↔ WaitlistPaths[{j}]");
        return true;
    }

    /// <summary>
    /// 활성 ↔ 대기: 타입이 달라(Entry vs WaitEntry) 변환하며 맞바꿈.
    ///   - 활성→대기: 새 WaitEntry{ id, resourcePath } 생성. currentHP/MP는 버림(WaitEntry에 자리 없음).
    ///   - 대기→활성: 새 Entry 생성, HP/MP는 CompanionSO.HP/MP로 풀피 시작.
    ///   - 한쪽이 null이면 이동만 (반대편 칸은 null이 됨).
    /// </summary>
    public static bool SwapActiveWait(int activeIndex, int waitIndex)
    {
        EnsureActiveRosterFixedSize();
        EnsureWaitlistFixedSize();
        if (activeIndex < 0 || activeIndex >= ActiveRoster.Count ||
            waitIndex < 0 || waitIndex >= WaitlistPaths.Count)
        {
            Debug.LogWarning($"[Swap] SwapActiveWait: 잘못된 인덱스 active={activeIndex}, wait={waitIndex}");
            return false;
        }

        Entry a = ActiveRoster[activeIndex];      // Entry 또는 null
        WaitEntry w = WaitlistPaths[waitIndex];   // WaitEntry 또는 null

        if (a == null && w == null) return false; // 둘 다 비어 있으면 할 일 없음

        // 활성→대기: Entry → WaitEntry (HP/MP 버림)
        WaitEntry newWait = (a != null)
            ? new WaitEntry { id = a.id, resourcePath = a.resourcePath }
            : null;

        // 대기→활성: WaitEntry → Entry (풀피)
        Entry newActive = null;
        if (w != null)
        {
            var so = LoadCompanion(w.resourcePath);
            int hp = (so != null) ? so.HP : 0;
            int mp = (so != null) ? so.MP : 0;
            if (so == null)
                Debug.LogWarning($"[Swap] SwapActiveWait: CompanionSO 로드 실패 '{w.resourcePath}' → HP/MP 0으로 승격");
            newActive = new Entry { id = w.id, resourcePath = w.resourcePath, currentHP = hp, currentMP = mp };
        }

        ActiveRoster[activeIndex] = newActive;
        WaitlistPaths[waitIndex] = newWait;
        Debug.Log($"[Swap] 활성↔대기 — ActiveRoster[{activeIndex}] ↔ WaitlistPaths[{waitIndex}] (활성→대기 HP버림, 대기→활성 풀피)");
        return true;
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

    // ═════════════════════════════════════════════════════════════════
    // [2026-06-15 Hero 이동 Step 1] MainLine 4칸 occupancy 토대.
    //   아직 아무 데서도 호출하지 않음(죽은 코드). Step 2~4에서 사용.
    //   진실의 소스는 (heroSlotIndex + ActiveRoster). occupancy는 그 파생 표현.
    // ═════════════════════════════════════════════════════════════════

    /// <summary>MainLine 한 칸이 무엇인지: 빈칸 / Hero / 동료(Entry).</summary>
    public enum MainLineKind { Empty, Hero, Companion }

    /// <summary>MainLine 4칸 표현 토큰. kind==Companion일 때만 entry 유효.</summary>
    public struct MainLineToken
    {
        public MainLineKind kind;
        public Entry entry;   // Companion일 때만 유효

        public bool IsHero => kind == MainLineKind.Hero;
        public bool IsCompanion => kind == MainLineKind.Companion;
        public bool IsEmpty => kind == MainLineKind.Empty;

        public static MainLineToken HeroToken => new MainLineToken { kind = MainLineKind.Hero, entry = null };
        public static MainLineToken EmptyToken => new MainLineToken { kind = MainLineKind.Empty, entry = null };
        public static MainLineToken Companion(Entry e) => new MainLineToken { kind = MainLineKind.Companion, entry = e };
    }

    /// <summary>
    /// 현재 (heroSlotIndex + ActiveRoster)로부터 MainLine 4칸 occupancy를 만든다.
    ///   슬롯 s == heroSlotIndex → Hero. 그 외 → ActiveRoster를 오름차순으로 하나씩(빈칸은 Empty).
    /// </summary>
    public static MainLineToken[] BuildMainLineOccupancy()
    {
        EnsureActiveRosterFixedSize();
        int h = Mathf.Clamp(heroSlotIndex, 0, MainLineSlots - 1);

        var occ = new MainLineToken[MainLineSlots];
        int ci = 0;   // ActiveRoster 진행 인덱스 (비-Hero 슬롯마다 증가)
        for (int s = 0; s < MainLineSlots; s++)
        {
            if (s == h)
            {
                occ[s] = MainLineToken.HeroToken;
            }
            else
            {
                Entry e = (ci < ActiveRoster.Count) ? ActiveRoster[ci] : null;
                ci++;
                occ[s] = (e != null) ? MainLineToken.Companion(e) : MainLineToken.EmptyToken;
            }
        }
        return occ;
    }

    /// <summary>
    /// MainLine 슬롯 인덱스(0~3) → ActiveRoster 인덱스(0~2). Hero 슬롯이면 -1.
    ///   비-Hero 슬롯이 "Hero 제외 오름차순"에서 몇 번째인지: s &lt; hero면 s, s &gt; hero면 s-1.
    /// </summary>
    public static int MainLineSlotToRosterIndex(int slot)
    {
        if (slot < 0 || slot >= MainLineSlots) return -1;
        int h = Mathf.Clamp(heroSlotIndex, 0, MainLineSlots - 1);
        if (slot == h) return -1;   // Hero 슬롯은 ActiveRoster에 대응 없음
        return (slot < h) ? slot : slot - 1;
    }

    /// <summary>
    /// ActiveRoster 인덱스(0~2) → MainLine 슬롯 인덱스(0~3). (MainLineSlotToRosterIndex의 역)
    ///   Hero 슬롯을 건너뛴 오름차순이므로: rosterIndex &lt; hero면 그대로, 아니면 +1.
    /// </summary>
    public static int RosterIndexToMainLineSlot(int rosterIndex)
    {
        if (rosterIndex < 0 || rosterIndex >= MaxActive) return -1;
        int h = Mathf.Clamp(heroSlotIndex, 0, MainLineSlots - 1);
        return (rosterIndex < h) ? rosterIndex : rosterIndex + 1;
    }

    /// <summary>
    /// 두 MainLine 슬롯(0~3)의 점유자를 맞바꾼다. occupancy 라운드트립으로 (heroSlotIndex, ActiveRoster) 재도출.
    ///   - 한쪽이 Hero면 heroSlotIndex가 갱신됨 (Hero 이동).
    ///   - 둘 다 동료면 heroSlotIndex 불변 + ActiveRoster만 교환 (동료끼리 swap도 이 헬퍼로 통일 가능).
    ///   - 빈칸과의 교환은 "이동"으로 자연 처리됨.
    /// </summary>
    public static bool SwapMainLine(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= MainLineSlots || slotB < 0 || slotB >= MainLineSlots)
        {
            Debug.LogWarning($"[HeroMove] SwapMainLine: 잘못된 슬롯 a={slotA}, b={slotB} (범위 0~{MainLineSlots - 1})");
            return false;
        }
        if (slotA == slotB) return false;

        EnsureActiveRosterFixedSize();
        var occ = BuildMainLineOccupancy();

        // 두 칸 교환
        var tmp = occ[slotA];
        occ[slotA] = occ[slotB];
        occ[slotB] = tmp;

        // 역산 ① 새 Hero 슬롯 = Hero 토큰 위치
        int newHero = heroSlotIndex;
        for (int s = 0; s < MainLineSlots; s++)
        {
            if (occ[s].IsHero) { newHero = s; break; }
        }

        // 역산 ② ActiveRoster = 비-Hero 슬롯을 오름차순으로 (Companion이면 entry, 빈칸은 null)
        var newRoster = new Entry[MaxActive];
        int ci = 0;
        for (int s = 0; s < MainLineSlots; s++)
        {
            if (s == newHero) continue;
            if (ci < MaxActive)
                newRoster[ci] = occ[s].IsCompanion ? occ[s].entry : null;
            ci++;
        }

        // 반영
        heroSlotIndex = newHero;
        for (int k = 0; k < MaxActive; k++)
            ActiveRoster[k] = newRoster[k];

        Debug.Log($"[HeroMove] SwapMainLine({slotA},{slotB}) → heroSlotIndex={heroSlotIndex}, ActiveRoster=[{DescribeRoster()}]");
        return true;
    }

    /// <summary>디버그용 ActiveRoster 요약 문자열.</summary>
    private static string DescribeRoster()
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < ActiveRoster.Count; k++)
        {
            if (k > 0) sb.Append(", ");
            var e = ActiveRoster[k];
            sb.Append(e != null ? $"id{e.id}" : "null");
        }
        return sb.ToString();
    }
}
