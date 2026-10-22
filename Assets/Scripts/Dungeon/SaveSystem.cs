using System;
using System.Collections.Generic;
using System.IO;
using AbyssdawnBattle;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 저장 / 불러오기 (한 칸). 마을의 "Save" 에서 저장하고, 게임을 켜서 던전에 처음 들어올 때 이어서 할지 묻는다.
/// 파일: Application.persistentDataPath/abyssdawn_save.json (JSON)
///
/// 저장하는 것 (지금 게임이 상태를 들고 있는 곳 전부):
///  - 던전: 지금 층, 위치·방향, 층별 기록(시드·드러난 지도·연 상자·아는 함정·쓴 샘·탐험 EXP 받음·들어가 본 방), 위험도, 빛
///  - 주인공 수치: GameManager.PartyMemberData (레벨·EXP·HP/MP·기본 스탯·분배 포인트·LP)
///  - 주인공 장비·스킬·종의 기억·직업 (HeroData — 에셋 이름으로)
///  - 동료: 참전 3칸(빈칸 포함)·대기 3칸·주인공 자리·다음 ID
///  - 소비 아이템·새벽의 잔 충전, 가진 장비 목록, 소지금
/// 저장하지 않는 것: 전투 중 상태이상 (마을에서만 저장하므로 없음), 아직 보여주지 않은 레벨업 기록.
/// </summary>
public static class SaveSystem
{
    public const int Version = 1;

    // [2026-10-14] 모험의 서 여러 칸 (드퀘식). 0번 칸 = 예전 한 칸짜리 파일 이름 그대로 (기존 저장 유지)
    public const int SlotCount = 3;
    /// <summary>지금 쓰는 칸 (0부터). 저장·불러오기·"이어서 하기" 질문이 모두 이 칸을 쓴다.</summary>
    public static int CurrentSlot;
    public static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, slot <= 0 ? "abyssdawn_save.json" : $"abyssdawn_save_{slot + 1}.json");
    public static string FilePath => PathFor(CurrentSlot);
    public static bool HasSave => File.Exists(FilePath);
    public static bool HasSaveIn(int slot) => File.Exists(PathFor(slot));

    /// <summary>모험의 서 지우기.</summary>
    public static void Delete(int slot)
    {
        try { if (File.Exists(PathFor(slot))) File.Delete(PathFor(slot)); Debug.Log($"[SaveSystem] 모험의 서 {slot + 1} 삭제"); }
        catch (Exception ex) { Debug.LogWarning("[SaveSystem] 지우기 실패: " + ex.Message); }
    }

    // [2026-10-14] 플레이 시간 — 불러온/새로 시작한 때부터 실제 시간으로 잰다 (저장 때 합쳐서 기록)
    private static float _playBase, _playMark;
    public static float PlaySeconds => _playBase + Mathf.Max(0f, Time.realtimeSinceStartup - _playMark);
    public static void StartPlayClock(float fromSeconds) { _playBase = Mathf.Max(0f, fromSeconds); _playMark = Time.realtimeSinceStartup; }
    public static string FormatPlayTime(float seconds) { int m = Mathf.FloorToInt(seconds / 60f); return $"{m / 60}:{m % 60:00}"; }

    /// <summary>[2026-10-14] 새 게임에서 고른 성향 (DispositionData 에셋 이름, 아직 효과 없음).</summary>
    public static string Disposition;

    /// <summary>이번 실행에서 "이어서 하기" 질문을 이미 했는지 (게임 오버 후 다시 묻지 않게).</summary>
    public static bool LaunchChecked;

    /// <summary>불러온 뒤 던전에 들어가면 한 번 띄울 알림 (게임 오버 후 "마지막 저장에서 깨어남" 등). MapManager 가 보여주고 지운다.</summary>
    public static string PendingNotice;

    // ─────────────────────────────────────────
    // 파일 형식 (JsonUtility 용 — 전부 public 필드)
    // ─────────────────────────────────────────

    [Serializable] public class V2 { public int x, y; public V2() { } public V2(Vector2Int v) { x = v.x; y = v.y; } public Vector2Int ToV() { return new Vector2Int(x, y); } }

    [Serializable]
    public class FloorSave
    {
        public int floor, seed;
        public List<V2> revealed = new List<V2>(), openedChests = new List<V2>(), knownTraps = new List<V2>(), usedSprings = new List<V2>();
        public List<V2> sprungTraps = new List<V2>(), disarmedTraps = new List<V2>(); // [2026-10-07]
        public bool clearRewarded;
        public List<int> enteredRooms = new List<int>();
        public List<int> openedDoors = new List<int>(); // [2026-10-17]
    }

    [Serializable] public class CompanionSave { public bool empty = true; public int id; public string resourcePath; public int currentHP, currentMP; public bool hpKnown; } // hpKnown: 대기 명단 HP 기록 여부 (예전 저장 = false → 가득)
    [Serializable] public class ItemSave { public string name; public int quantity; }

    [Serializable]
    public class SaveData
    {
        public int version = Version;
        public string savedAt;
        public string sceneName;
        // 던전
        public int currentFloor;
        public V2 playerPos;
        public int facing;
        public float danger;
        public int lightSteps;
        public int lastTownFloor; // [2026-10-22] 귀향의 거울 목적지 (0 = 없음)
        public List<DungeonFieldStatus.SaveEntry> fieldStatuses = new List<DungeonFieldStatus.SaveEntry>(); // 함정 상태이상 [2026-10-07]
        public List<FloorSave> floors = new List<FloorSave>();
        // 주인공
        public GameManager.PartyMemberData hero;
        public string heroKey;
        public string job, rightHand, leftHand, body, accessory1, accessory2, memory1, memory2, memory3;
        public string oath; // [2026-10-14]
        public List<string> pathSkills = new List<string>(); // [2026-10-14] 배운 직업 패스
        public List<string> learnedSkills = new List<string>(), equippedSkills = new List<string>(), equippedPassives = new List<string>();
        // 동료
        public List<CompanionSave> active = new List<CompanionSave>(), waitlist = new List<CompanionSave>();
        public int heroSlotIndex, nextCompanionId;
        // 소지품
        public List<ItemSave> consumables = new List<ItemSave>();
        public int chaliceCharges;
        public int chaliceLevel; // [2026-10-18] 새벽의 잔 강화 단계
        public List<string> equipmentBag = new List<string>();
        public int gold = PlayerWallet.StartingGold;
        public List<int> bossesDefeated = new List<int>(); // [2026-10-09] 보스를 쓰러뜨린 층
        public float playSeconds;   // [2026-10-14] 모험의 서에 보여줄 플레이 시간
        public string disposition;  // [2026-10-14] 성향 (에셋 이름)
        // 이어서 하기 창에 보여줄 요약
        public int heroLevel;
    }

    // ─────────────────────────────────────────
    // 저장
    // ─────────────────────────────────────────

    public static bool Save(out string error)
    {
        error = null;
        try
        {
            PlayerStats hero = FindHero();
            if (hero == null) { error = "No hero"; return false; }
            var player = UnityEngine.Object.FindFirstObjectByType<DungeonGridPlayer>();

            GameManager.EnsureInstance().SaveFromPlayer(hero); // 최신 수치를 기록에 반영
            var d = new SaveData
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                sceneName = SceneManager.GetActiveScene().name,
                currentFloor = DungeonPersistentData.currentFloor,
                playerPos = new V2(player != null ? player.gridPos : DungeonPersistentData.lastPlayerGridPos),
                facing = (int)(player != null ? player.facing : DungeonPersistentData.lastPlayerFacing),
                danger = DungeonPersistentData.danger,
                lightSteps = DungeonPersistentData.playerLightSteps,
                lastTownFloor = DungeonPersistentData.lastTownFloor,
                fieldStatuses = DungeonFieldStatus.ToSave(),
                heroKey = hero.playerName,
                heroLevel = hero.level,
                heroSlotIndex = CompanionPartyPersistence.heroSlotIndex,
                nextCompanionId = CompanionPartyPersistence.NextCompanionId,
            };
            GameManager.PartyMemberData pmd;
            if (GameManager.staticPartyData.TryGetValue(hero.playerName, out pmd)) d.hero = pmd;

            foreach (var kv in DungeonPersistentData.floors)
            {
                var s = kv.Value;
                var fs = new FloorSave { floor = s.floor, seed = s.seed, clearRewarded = s.clearRewarded };
                foreach (var p in s.revealed) fs.revealed.Add(new V2(p));
                foreach (var p in s.openedChests) fs.openedChests.Add(new V2(p));
                foreach (var p in s.knownTraps) fs.knownTraps.Add(new V2(p));
                foreach (var p in s.usedSprings) fs.usedSprings.Add(new V2(p));
                foreach (var p in s.sprungTraps) fs.sprungTraps.Add(new V2(p));
                foreach (var p in s.disarmedTraps) fs.disarmedTraps.Add(new V2(p));
                fs.enteredRooms.AddRange(s.enteredRooms);
                fs.openedDoors.AddRange(s.openedDoors);
                d.floors.Add(fs);
            }

            PlayerStatData sd = hero.statData;
            if (sd != null)
            {
                d.job = NameOf(sd.currentJob);
                d.oath = NameOf(sd.currentOath);
                if (sd.learnedPathSkills != null) foreach (var p in sd.learnedPathSkills) if (p != null) d.pathSkills.Add(p.name);
                d.rightHand = NameOf(sd.rightHand); d.leftHand = NameOf(sd.leftHand); d.body = NameOf(sd.body);
                d.accessory1 = NameOf(sd.accessory1); d.accessory2 = NameOf(sd.accessory2);
                d.memory1 = NameOf(sd.memorySlot1); d.memory2 = NameOf(sd.memorySlot2); d.memory3 = NameOf(sd.memorySlot3);
                foreach (var s in sd.learnedSkills) if (s != null) d.learnedSkills.Add(s.name);
                // 장착 슬롯은 빈 칸도 "" 로 남겨 슬롯 위치를 보존
                foreach (var s in sd.equippedSkills) d.equippedSkills.Add(s != null ? s.name : "");
                foreach (var s in sd.equippedPassives) d.equippedPassives.Add(s != null ? s.name : "");
            }

            foreach (var e in CompanionPartyPersistence.ActiveRoster)
                d.active.Add(e == null ? new CompanionSave() : new CompanionSave { empty = false, id = e.id, resourcePath = e.resourcePath, currentHP = e.currentHP, currentMP = e.currentMP });
            foreach (var w in CompanionPartyPersistence.WaitlistPaths)
                d.waitlist.Add(w == null ? new CompanionSave() : new CompanionSave { empty = false, id = w.id, resourcePath = w.resourcePath, currentHP = w.currentHP, currentMP = w.currentMP, hpKnown = true });

            var inv = ConsumableInventory.Instance;
            if (inv != null)
            {
                foreach (var s in inv.slots) if (s != null && s.item != null) d.consumables.Add(new ItemSave { name = s.item.name, quantity = s.quantity });
                d.chaliceCharges = inv.dawnChaliceCharges;
                d.chaliceLevel = inv.dawnChaliceLevel;
            }
            foreach (var e in EquipmentBag.Items) if (e != null) d.equipmentBag.Add(e.name);
            d.gold = PlayerWallet.Gold;
            d.bossesDefeated.AddRange(BossEncounter.defeatedFloors);
            d.playSeconds = PlaySeconds;
            d.disposition = Disposition;

            File.WriteAllText(FilePath, JsonUtility.ToJson(d, true));
            Debug.Log($"[SaveSystem] 저장 완료 — B{d.currentFloor} Lv{d.heroLevel} → {FilePath}");
            PlayLog.Saved(); // [2026-10-22]
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogError("[SaveSystem] 저장 실패: " + ex);
            return false;
        }
    }

    // ─────────────────────────────────────────
    // 불러오기
    // ─────────────────────────────────────────

    public static SaveData Peek() => Peek(CurrentSlot);

    public static SaveData Peek(int slot)
    {
        try { return HasSaveIn(slot) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot))) : null; }
        catch (Exception ex) { Debug.LogWarning("[SaveSystem] 저장 파일을 읽지 못했습니다: " + ex.Message); return null; }
    }

    /// <summary>
    /// 저장 파일 내용을 게임의 보관소(static·HeroData)에 채운 뒤 저장한 던전 씬을 다시 연다.
    /// 씬이 열리면 기존 복원 경로(GameManager → PlayerStats, DungeonPersistentData → MapManager)가 그대로 적용된다.
    /// </summary>
    public static bool Load(out string error)
    {
        error = null;
        SaveData d = Peek();
        if (d == null) { error = "No save file"; return false; }
        try
        {
            // 던전
            DungeonPersistentData.ClearState();
            DungeonPersistentData.currentFloor = Mathf.Max(1, d.currentFloor);
            foreach (var fs in d.floors)
            {
                var s = new DungeonFloorState(fs.floor, fs.seed) { clearRewarded = fs.clearRewarded };
                foreach (var p in fs.revealed) s.revealed.Add(p.ToV());
                foreach (var p in fs.openedChests) s.openedChests.Add(p.ToV());
                foreach (var p in fs.knownTraps) s.knownTraps.Add(p.ToV());
                foreach (var p in fs.usedSprings) s.usedSprings.Add(p.ToV());
                if (fs.sprungTraps != null) foreach (var p in fs.sprungTraps) s.sprungTraps.Add(p.ToV());
                else foreach (var p in fs.knownTraps) s.sprungTraps.Add(p.ToV()); // 예전 저장: 아는 함정 = 밟은 함정으로 간주
                if (fs.disarmedTraps != null) foreach (var p in fs.disarmedTraps) s.disarmedTraps.Add(p.ToV());
                foreach (var r in fs.enteredRooms) s.enteredRooms.Add(r);
                if (fs.openedDoors != null) foreach (var r in fs.openedDoors) s.openedDoors.Add(r); // 예전 저장엔 없음 → 문은 모두 닫힘
                DungeonPersistentData.floors[fs.floor] = s;
            }
            DungeonFloorState cur;
            if (DungeonPersistentData.floors.TryGetValue(DungeonPersistentData.currentFloor, out cur))
            {
                DungeonPersistentData.currentSeed = cur.seed;
                DungeonPersistentData.revealedTiles = cur.revealed;
            }
            DungeonPersistentData.hasSavedState = true;
            DungeonPersistentData.lastPlayerGridPos = d.playerPos != null ? d.playerPos.ToV() : Vector2Int.zero;
            DungeonPersistentData.lastPlayerFacing = (DungeonDirection)d.facing;
            DungeonPersistentData.danger = d.danger;
            DungeonPersistentData.playerLightSteps = d.lightSteps;
            DungeonPersistentData.lastTownFloor = d.lastTownFloor;
            DungeonFieldStatus.FromSave(d.fieldStatuses);

            // 주인공 수치
            var gm = GameManager.EnsureInstance();
            gm.ClearAllData();
            if (d.hero != null && !string.IsNullOrEmpty(d.heroKey)) GameManager.staticPartyData[d.heroKey] = d.hero;

            // 주인공 장비·스킬·기억·직업 (HeroData 에셋 — 씬이 바뀌어도 같은 에셋). 전투 씬에서 불러올 때도 주인공 이름으로 찾는다
            PlayerStats hero = FindHero(d.heroKey);
            PlayerStatData sd = hero != null ? hero.statData : null;
            if (sd != null)
            {
                // 같은 이름이면 지금 직업 유지 — Class_Warrior 가 두 곳(Resources/Classes, Scripts/.../Classes)에 있어
                //   이름 검색이 다른 쪽을 집으면 HeroData 가 쓸데없이 바뀌었다 (내용은 동일)
                if (sd.currentJob == null || sd.currentJob.name != d.job)
                    sd.currentJob = Find<CharacterClass>(d.job) ?? sd.currentJob;
                if (hero != null) hero.SyncJobFromData(); // [2026-10-17] 불러온 직업을 주인공에게도
                sd.currentOath = string.IsNullOrEmpty(d.oath) ? null : Find<OathBindingData>(d.oath);
                if (sd.learnedPathSkills == null) sd.learnedPathSkills = new List<PathSkillData>();
                sd.learnedPathSkills.Clear();
                if (d.pathSkills != null)
                    foreach (var n in d.pathSkills) { var p = Find<PathSkillData>(n); if (p != null) sd.learnedPathSkills.Add(p); }
                sd.rightHand = Find<EquipmentData>(d.rightHand); sd.leftHand = Find<EquipmentData>(d.leftHand); sd.body = Find<EquipmentData>(d.body);
                sd.accessory1 = Find<EquipmentData>(d.accessory1); sd.accessory2 = Find<EquipmentData>(d.accessory2);
                sd.memorySlot1 = Find<MemoryOfSpeciesData>(d.memory1); sd.memorySlot2 = Find<MemoryOfSpeciesData>(d.memory2); sd.memorySlot3 = Find<MemoryOfSpeciesData>(d.memory3);
                sd.CheckAndActivateTrait(); // [2026-10-14] 불러온 기억으로 종의 특성 다시 계산 (안 하면 이전 판의 특성이 남음)
                FillList(sd.learnedSkills, d.learnedSkills);
                FillSlots(sd.equippedSkills, d.equippedSkills, 6);
                FillSlots(sd.equippedPassives, d.equippedPassives, 3);
            }

            // 동료
            CompanionPartyPersistence.Clear();
            for (int i = 0; i < CompanionPartyPersistence.ActiveRoster.Count && i < d.active.Count; i++)
            {
                var c = d.active[i];
                CompanionPartyPersistence.ActiveRoster[i] = c.empty ? null : new CompanionPartyPersistence.Entry { id = c.id, resourcePath = c.resourcePath, currentHP = c.currentHP, currentMP = c.currentMP };
            }
            for (int i = 0; i < CompanionPartyPersistence.WaitlistPaths.Count && i < d.waitlist.Count; i++)
            {
                var c = d.waitlist[i];
                CompanionPartyPersistence.WaitlistPaths[i] = c.empty ? null : new CompanionPartyPersistence.WaitEntry { id = c.id, resourcePath = c.resourcePath,
                    currentHP = c.hpKnown ? c.currentHP : -1, currentMP = c.hpKnown ? c.currentMP : -1 }; // [2026-10-22] 예전 저장은 가득
            }
            CompanionPartyPersistence.heroSlotIndex = d.heroSlotIndex;
            CompanionPartyPersistence.NextCompanionId = d.nextCompanionId;

            // 소지품
            var slots = new List<ConsumableInventory.ConsumableSlot>();
            foreach (var it in d.consumables)
            {
                var so = Find<ConsumableItemSO>(it.name);
                if (so != null && !so.isDawnChalice) slots.Add(new ConsumableInventory.ConsumableSlot(so, it.quantity));
            }
            if (ConsumableInventory.Instance != null) { ConsumableInventory.Instance.dawnChaliceLevel = d.chaliceLevel; ConsumableInventory.Instance.RestoreFromSave(slots, d.chaliceCharges); }
            EquipmentBag.Clear();
            foreach (var n in d.equipmentBag) { var e = Find<EquipmentData>(n); if (e != null) EquipmentBag.AddLoaded(e); }
            PlayerWallet.Set(d.gold);
            PlayLog.Loaded(d.gold); // [2026-10-22] 불러온 골드는 번 돈으로 치지 않음
            BossEncounter.defeatedFloors.Clear();
            if (d.bossesDefeated != null) foreach (int f in d.bossesDefeated) BossEncounter.defeatedFloors.Add(f);
            PlayerStats.PendingLevelUpNotes.Clear();
            StartPlayClock(d.playSeconds);
            Disposition = d.disposition;
            global::Disposition.PerfectionistTrapFloor = -1;

            Debug.Log($"[SaveSystem] 불러오기 — B{d.currentFloor} Lv{d.heroLevel} ({d.savedAt}), 씬 '{d.sceneName}'");
            string scene = string.IsNullOrEmpty(d.sceneName) ? SceneManager.GetActiveScene().name : d.sceneName;
            EncounterTransition.LoadSceneWithFadeQueued(scene); // [2026-10-21] 타이틀→던전 전환 중이어도 버려지지 않게
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogError("[SaveSystem] 불러오기 실패: " + ex);
            return false;
        }
    }

    // ─────────────────────────────────────────
    // 도우미
    // ─────────────────────────────────────────

    private static PlayerStats FindHero(string heroKey = null)
    {
        var all = UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        if (!string.IsNullOrEmpty(heroKey))
            foreach (var p in all)
                if (p != null && !p.IsRecruitedCompanion && string.Equals(p.playerName, heroKey, StringComparison.OrdinalIgnoreCase)) return p;
        foreach (var p in all)
            if (p != null && !p.IsRecruitedCompanion) return p;
        return null;
    }

    private static string NameOf(UnityEngine.Object o) { return o != null ? o.name : null; }
    private static readonly Dictionary<string, UnityEngine.Object> _cache = new Dictionary<string, UnityEngine.Object>();

    /// <summary>에셋 이름으로 찾기 — Resources 전체 + 이미 불러온 에셋(씬이 참조하는 스킬 등).</summary>
    private static T Find<T>(string name) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(name)) return null;
        string key = typeof(T).Name + "/" + name;
        UnityEngine.Object hit;
        if (_cache.TryGetValue(key, out hit) && hit != null) return hit as T;
        foreach (var o in Resources.LoadAll<T>("")) if (o != null && o.name == name) { _cache[key] = o; return o; }
        foreach (var o in Resources.FindObjectsOfTypeAll<T>()) if (o != null && o.name == name) { _cache[key] = o; return o; }
        var reg = SaveAssetRegistry.Find<T>(name); // [2026-10-21] Resources 밖 에셋 (종의 기억 등)
        if (reg != null) { _cache[key] = reg; return reg; }
        Debug.LogWarning($"[SaveSystem] '{name}' ({typeof(T).Name}) 에셋을 찾지 못했습니다.");
        return null;
    }

    private static void FillList(List<SkillData> target, List<string> names)
    {
        if (target == null) return;
        target.Clear();
        if (names == null) return;
        foreach (var n in names) { var s = Find<SkillData>(n); if (s != null) target.Add(s); }
    }

    /// <summary>장착 슬롯 복원: "" 은 빈 칸(null). 최소 minCount 칸 유지.</summary>
    private static void FillSlots(List<SkillData> target, List<string> names, int minCount)
    {
        if (target == null) return;
        target.Clear();
        if (names != null)
            foreach (var n in names) target.Add(string.IsNullOrEmpty(n) ? null : Find<SkillData>(n));
        while (target.Count < minCount) target.Add(null);
    }
}
