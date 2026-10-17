using System.Collections;
using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

/// <summary>
/// [2026-10-14] 모험의 서(새 게임 · 이어서 하기) → 던전 씬으로 넘길 내용.
/// 던전 씬이 열리면 MapManager 가 ApplyInDungeon 을 불러 처리한다 (주인공·소지품이 던전 씬에 있기 때문).
///   - 이어서 하기: 그 칸을 골라 SaveSystem.Load (저장한 씬을 다시 연다)
///   - 새 게임: 새 탐험 상태로 초기화 → 고른 종의 기억 3개 · 직업 · 성향 적용 → 그 칸에 바로 기록
/// </summary>
public static class GameStartRequest
{
    public enum Mode { None, NewGame, Continue }

    public static Mode mode = Mode.None;
    public static int slot;
    public static readonly List<MemoryOfSpeciesData> memories = new List<MemoryOfSpeciesData>();
    public static CharacterClass job;
    public static DispositionData disposition;
    public static OathBindingData oath;

    public static void Clear()
    {
        mode = Mode.None;
        memories.Clear();
        job = null;
        disposition = null;
        oath = null;
    }

    /// <summary>처리했으면 true (그러면 "이어서 할까요?" 질문은 하지 않는다).</summary>
    public static bool ApplyInDungeon(MonoBehaviour host)
    {
        if (mode == Mode.None) return false;
        Mode m = mode;
        int s = slot;
        SaveSystem.CurrentSlot = s;

        if (m == Mode.Continue)
        {
            Clear();
            string err;
            if (!SaveSystem.Load(out err))
            {
                Debug.LogWarning("[GameStartRequest] 모험의 서 불러오기 실패: " + err);
                if (DungeonHud.Instance != null) DungeonHud.Instance.Toast("<color=#FF6B6B>모험 기록을 불러오지 못했다.</color>\n<size=75%>" + err + "</size>");
            }
            return true;
        }

        // ── 새 게임 ──
        PlayerStats hero = FindHero();
        PlayerStatData sd = hero != null ? hero.statData : null;

        DungeonPersistentData.danger = 0f;
        DungeonTravelLog.Clear(); // [2026-10-15] 새 탐험 = 새 여행 로그
        if (sd != null && job != null) sd.currentJob = job; // 기초 장비가 직업을 보므로 먼저
        if (sd != null) sd.currentOath = oath;
        ConsumableInventory.ResetForNewRun();
        // [2026-10-18] 직업의 시작 소모품 (전사: 약초 3 · 붕대 2 · HP 포션 1 · 해독제 1)
        if (job != null && job.startingConsumables != null && ConsumableInventory.Instance != null)
            foreach (var it in job.startingConsumables)
                if (it != null && it.item != null && it.count > 0) ConsumableInventory.Instance.AddItem(it.item, it.count);
        EquipmentBag.ResetForNewRun(sd); // 주운 장비 비움 + 직업 기초 장비 장착
        // 던전의 EquipmentManager 는 이미 HeroData 를 읽어 둔 상태 — 새 장비로 다시 맞춤
        var em = hero != null ? hero.GetComponent<EquipmentManager>() : null;
        if (em != null && sd != null) { em.rightHand = sd.rightHand; em.leftHand = sd.leftHand; em.body = sd.body; em.accessory1 = sd.accessory1; em.accessory2 = sd.accessory2; }
        PlayerWallet.ResetForNewRun();
        BossEncounter.defeatedFloors.Clear();
        PlayerStats.PendingLevelUpNotes.Clear();
        if (sd != null)
        {
            sd.ResetSkillsForNewRun();
            if (job != null) sd.currentJob = job;
            if (memories.Count > 0) sd.memorySlot1 = memories[0];
            if (memories.Count > 1) sd.memorySlot2 = memories[1];
            if (memories.Count > 2) sd.memorySlot3 = memories[2];
            sd.CheckAndActivateTrait(); // 같은 종 3개면 종의 특성
        }
        if (hero != null)
        {
            hero.SyncJobFromData(); // [2026-10-17] 고른 직업을 주인공에게 바로 반영 (HP·스탯도 그 직업 기준으로)
            hero.currentHP = hero.maxHP;
            hero.currentMP = hero.maxMP;
            hero.NotifyStatusChanged();
        }
        SaveSystem.Disposition = disposition != null ? disposition.name : null;
        Disposition.PerfectionistTrapFloor = -1;
        SaveSystem.StartPlayClock(0f);
        Debug.Log($"[GameStartRequest] 새 게임 — 모험의 서 {s + 1}, 직업 {(job != null ? job.className : "-")}, 기억 {memories.Count}개, 성향 {(SaveSystem.Disposition ?? "-")}");
        Clear();

        // [2026-10-14] 눈을 뜨는 연출 (검게 → 흐릿 → 선명)
        WakeUpTransition.Play();

        // 던전·주인공이 다 자리 잡은 뒤 그 칸에 첫 기록 (드퀘처럼 모험의 서를 만들면 바로 남는다)
        host.StartCoroutine(FirstRecord(s));
        return true;
    }

    private static IEnumerator FirstRecord(int s)
    {
        yield return null;
        yield return null;
        SaveSystem.CurrentSlot = s;
        string err;
        bool ok = SaveSystem.Save(out err);
        yield return new WaitForSecondsRealtime(3.9f); // 눈 뜨는 연출이 끝난 뒤에 알림
        if (DungeonHud.Instance != null)
            DungeonHud.Instance.Toast(ok ? $"<color=#FFD24A>모험 기록 {s + 1}</color>을(를) 만들었다."
                                         : "<color=#FF6B6B>모험 기록을 만들지 못했다.</color>\n<size=75%>" + err + "</size>");
    }

    private static PlayerStats FindHero()
    {
        foreach (var p in Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            if (p != null && !p.IsRecruitedCompanion) return p;
        return null;
    }
}
