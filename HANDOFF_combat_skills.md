# Abyssdawn 01 — 포지션/전투 시스템 인수인계
### (다음 섹션: 몬스터 스킬 · 몬스터 AI · 스킬 기반 자리이동 · 버프/디버프)

> **현재 상태 한 줄 요약:** 슬롯 모델이 **모델 B(전열=슬롯1,2 / 후열=슬롯3,4)로 적·아군 통일** 완료, 파티 **압축 제거(슬롯 고정·빈칸 null 보존)** 완료, **Hero를 MainLine 4칸 어디든 이동(전투 반영)** 완료. **적 AI는 "랜덤 단일 기본공격"만 존재하며 적 스킬 사용·슬롯 타겟팅·자리이동은 미구현.**
>
> **기준 커밋:** `05c9e51` (`05c9e5127c22a7c9074a2f6ee8ea12cb9666d79c`) — "feat: Hero move Step 5 - disabled-slot tint"
> **원격:** https://github.com/dawipark77-ai/Abyssdawn (main)
>
> ⚠️ 이 문서의 모든 줄 번호는 위 커밋 기준. 코드 수정 시 즉시 어긋날 수 있으니, 작업 전 해당 함수명을 grep으로 재확인할 것. "미확인"으로 표기한 항목은 코드 근거를 끝까지 확인하지 못한 것.

---

> ## ★다음 작업자가 가장 먼저 알아야 할 핵심 2가지★
>
> ### (가) 적·아군 자리이동은 비대칭 — 적은 쉽고 아군은 새 작업
> - **적 이동: 이미 됨.** `MoveToSlot(EnemyStats, BattleSlot, ...)` — **BattleManager.cs:1167** — 이 빈슬롯→이동/점유→스왑 + `currentSlot`·월드위치 갱신까지 처리. **스킬 실행에서 호출만 하면 됨.** (현재 호출처는 디버그 키뿐.)
> - **아군 전투 중 이동: 미구현.** `SwapMainLine` — **CompanionPartyPersistence.cs:463** — 은 **던전 Tactics 화면 전용**(static 데이터 = 씬 전환 간 유지)이라 **전투 중엔 부적합**. 전투 중 아군 위치의 진실의 소스는 `currentSlot`(InitializeBattleLines:2604가 세팅) + `activePartyMembers` 순서 + `playerLine`. **전투 중 아군 자리이동은 이 셋을 동기화하는 함수를 새로 설계해야 함.**
> - → **"스킬로 자리 옮기기"는 적은 `MoveToSlot` 재사용으로 쉽고, 아군은 전투용 이동 함수부터 새로 만들어야 한다.**
>
> ### (나) 슬롯 시스템은 "읽을 준비만 됐고, 아직 안 읽힌다"
> - **토대는 완성:** `SlotHelper`(모델 B, SkillData.cs:92)·`SlotMask`(:73)·`heroSlotIndex`(CompanionPartyPersistence.cs:59)·occupancy 헬퍼 등 — 슬롯/포지션 데이터는 정확히 갖춰짐.
> - **그러나 런타임 스킬이 슬롯 제한을 강제하는 배선은 0:** `CanCastFrom`(시전 슬롯) 호출 **0건**, `allowedTargetSlots`(타겟 슬롯)는 `ExecuteMandritto`(BattleManager.cs:5529) **1곳뿐**. 즉 SkillData의 슬롯 필드는 대부분 런타임에 **읽히지 않음**.
> - → **"슬롯 기반 스킬"(특정 슬롯에서만 시전 / 특정 슬롯만 타겟)을 만들려면, SkillData의 `allowedCasterSlots`/`allowedTargetSlots`를 실제 발동·타겟팅 경로(`ExecuteSkill`:5211 등)에 연결하는 배선부터 깔아야 한다.** 토대는 있으나 자동 적용되지 않는다.

---

## ① 슬롯 모델 (스킬/AI가 슬롯을 읽을 때)

### 진실의 소스: `SlotHelper` (모델 B, 적·아군 공용)
- 파일: `Assets/Scripts/Battle/Data/SkillData.cs`
- **전열 = 슬롯 1,2 / 후열 = 슬롯 3,4** 로 통일됨 (Phase 1.5 작업 결과).
- `IsFrontRow(int)` — **SkillData.cs:119** → `slotIndex 1~2`
- `IsFrontRow(BattleSlot)` — **SkillData.cs:127** → `Slot1 || Slot2`
- `IsBackRow(int)` — **SkillData.cs:136** → `slotIndex 3~4`
- `IsBackRow(BattleSlot)` — **SkillData.cs:144** → `Slot3 || Slot4`
- `GetRow(int)` — **SkillData.cs:101**, `GetRow(BattleSlot)` — **SkillData.cs:109** (Center는 Front 간주)
- `ToSlotMask` / `ContainsSlot` — **SkillData.cs:153 / 175** (모델 무관 비트 유틸 — 행 정의와 별개)

### `BattleSlot` enum — **SkillData.cs:53-64**
```
None=0, Slot1=1, Slot2=2, Slot3=3, Slot4=4, Slot5=5, Slot6=6, Slot7=7, Center=8
```
- **Slot5~7은 죽은 잔재** (실제 전투는 1~4만 사용). 주석에도 "Step 5에서 제거 예정"이라 적혀 있으나 **아직 제거 안 됨** (⑥ 참고).

### `SlotMask` (Flags) — **SkillData.cs:73-87**
- 개별 비트: `Slot1=1, Slot2=2, Slot3=4, Slot4=8, Slot5=16, Slot6=32, Slot7=64, Center=128`
- **`Front = Slot1|Slot2` (=3)** — SkillData.cs:84
- **`Back = Slot3|Slot4` (=12)** — SkillData.cs:85
- **`Any = Slot1|Slot2|Slot3|Slot4` (=15)** — SkillData.cs:86
- ⚠️ 에셋은 마스크를 **원시 정수**로 저장. 코드는 전부 **비트 AND**로 읽음 (등치 비교 `== SlotMask.Front` 없음). 즉 마스크 상수를 바꿔도 에셋 저장값은 불변, 소비는 비트 단위.

### 적·아군 공용 확인
- `EnemyStats.currentSlot` — **EnemyStats.cs:51**, `IsFrontRow/IsBackRow` — **EnemyStats.cs:56/57** → `SlotHelper` 호출
- `PlayerStats.currentSlot` — **PlayerStats.cs:300**, `IsFrontRow/IsBackRow` — **PlayerStats.cs:307/308** → `SlotHelper` 호출
- → **둘 다 동일한 `SlotHelper`(모델 B)를 봄.** 행 판정은 완전 공용.

---

## ② 슬롯 ↔ 데이터 매핑 (자리이동 구현의 핵심)

### 아군 측 데이터: `CompanionPartyPersistence` (static, 씬 전환 간 유지)
- 파일: `Assets/Scripts/Battle/CompanionPartyPersistence.cs`
- `ActiveRoster` (List<Entry>, **고정 3칸 null 허용**) — **CompanionPartyPersistence.cs:46**. 동료만 담음 (Hero 없음).
- `WaitlistPaths` (List<WaitEntry>, 고정 3칸) — **:50**. 대기열(Reserves).
- `MainLineSlots = 4` (Hero 1 + 동료 3) — **:53**
- **`heroSlotIndex` (0~3, 0=슬롯1)** — **:59**. Hero가 MainLine 어느 칸에 있는지. `Clear()`(:108)에서 0 리셋.

### MainLine 4칸 occupancy 모델 (Hero+동료 통합 표현)
- **`BuildMainLineOccupancy()`** — **CompanionPartyPersistence.cs:411** → 길이 4 토큰 배열. 슬롯 s==heroSlotIndex면 Hero, 그 외엔 ActiveRoster를 오름차순으로 채움(빈칸은 Empty).
- **`MainLineSlotToRosterIndex(slot)`** — **:438** → MainLine 슬롯(0~3) → ActiveRoster 인덱스(0~2). Hero 슬롯이면 -1. (`slot<hero ? slot : slot-1`)
- **`RosterIndexToMainLineSlot(k)`** — **:450** → 역변환.
- **`SwapMainLine(a, b)`** — **:463** → 두 MainLine 슬롯 점유자 교환. occupancy 라운드트립으로 `heroSlotIndex`+`ActiveRoster` 재도출. **Hero/동료 대칭** (Hero 끼면 heroSlotIndex 갱신, 동료끼리면 ActiveRoster만). **검증 완료(Step 1~4).**
- 레거시 동료 전용 swap: `SwapActive(:217)`, `SwapWait(:234)`, `SwapActiveWait(:256)` — TacticsView가 동료↔대기 교환에 사용.

### 전투 파티 조립: `BuildBattlePartyForEncounter` — **BattleManager.cs:1991**
- `BuildMainLineOccupancy()`로 4칸 토큰을 받아 슬롯 s=0..3 순회:
  - Hero 토큰 → `activePartyMembers.Add(player)`
  - Companion 토큰 → `token.entry.id == ally.companionId`로 `_companionInstances` 매칭 → Add (없으면 null)
  - Empty → null Add
- **압축 없음. 빈 칸은 null로 보존** ("보이는 자리 = 전투 자리"). 끝쪽 trailing null만 정리.
- ⚠️ **프리셋 디버그 경로**(동료 0마리 시 Warrior/Rogue/Wizard append) — **BattleManager.cs:~2041** — 는 **아직 압축(append) 방식, heroSlotIndex 미반영** (⑥ 참고, 디버그 전용).

### 전투 중 슬롯 확정: `InitializeBattleLines` — **BattleManager.cs:2604**
- 아군: `activePartyMembers`의 **리스트 인덱스 i → `member.currentSlot = (BattleSlot)(i+1)`** (:2612). null은 건너뜀(:2610). 즉 인덱스 0=슬롯1 … 3=슬롯4.
- 적: `currentSlot`은 스폰 단계에서 이미 세팅됨(덮어쓰지 않음). 슬롯 1~4 적만 `enemyLine`에 동기화(:2622-2628).
- `playerLine` / `enemyLine` = `BattleLine<T>` — **BattleManager.cs:348 / 349**. `BattleLine` 클래스는 `BattleLine.cs:10` (Swap:115, Move:130 보유 — 단 4슬롯 레거시 컨테이너).
- 월드 배치: `PositionPartyMembers` — **BattleManager.cs:2290** (인덱스 기반 가로 배치, null 스킵).

---

## ③ 스킬 시스템 현황 (몬스터 스킬 제작 시)

### `SkillData` (ScriptableObject) — **SkillData.cs:263**
주요 필드:
| 필드 | 위치 | 의미 |
|---|---|---|
| `usageType` (UsageType.Active/Passive) | :273 | 액티브/패시브. `IsActive`(:421)/`IsPassive`(:422) |
| `damageType` (Physical/Magic 등) | :274 | 데미지 타입 |
| `scalingStat` | :276 | 스케일 스탯 |
| `weaponCategory` | :279 | 필요 무기 (None=공용) |
| `targeting` (SkillTargeting) | :288 | 타겟팅 설정 (아래) |
| `mpCost` / `hpCostPercent` | :293 / :292 | 코스트 |
| `minMult`/`maxMult`/`hitCount`/`accuracy` | :296-301 | 위력/명중 |
| `selfDmgPercent`/`selfStatusEffect` 등 | :303-312 | 자해/역효과 |
| `effects` (List<SkillEffect>) | :315 | 효과 리스트 (`SkillEffect`:251 — effectType/recoveryTarget/effectAmount/statusEffect/statusEffectChance) |
| `curseEffect`/`curseApplyChance` | :340/:343 | 상태이상 부여 |
| `backflowChance`/`backflowType` | :347/:348 | 역류 |
| `dunbreakChance` | :332 | 던브레이크 |

### `SkillTargeting` — **SkillData.cs:185**
- `targetFaction` (Enemy/Ally/Self/All) — **:190** (enum TargetFaction:20)
- `allowedCasterSlots` (SlotMask, 기본 Any) — **:197** — 어느 슬롯에서 시전 가능한지
- `allowedTargetSlots` (SlotMask, 기본 Slot1) — **:205** — 어느 슬롯을 타겟하는지
- `CanCastFrom(slot)` — **:234** (`allowedCasterSlots & casterMask != 0`)
- `CanTarget(slot)` — **:243**, `GetTargetSlots()` — **:215**
- 슬롯 제한 읽는 방식: **전부 비트 AND** (등치 비교 아님).

### ⚠️ 런타임 슬롯 제한 적용 현황 (중요)
- **`CanCastFrom`은 실제 전투(BattleManager)에서 호출 0건** — grep 확인. 즉 **시전 슬롯 제한이 런타임에 강제되지 않음** (시뮬레이터에서만 사용).
- **`allowedTargetSlots`는 BattleManager에서 단 1곳** — **BattleManager.cs:5529** (`ExecuteMandritto`의 폴백). 대다수 스킬은 타겟 슬롯 제한이 런타임에 강제되지 않음.
- → **스킬 슬롯 타겟팅을 제대로 쓰려면 실행 경로에 `CanCastFrom`/`allowedTargetSlots` 적용을 새로 배선해야 함.**

### 플레이어 스킬 실행 경로
- **`ExecuteSkill(PlayerStats, SkillData, EnemyStats)`** — **BattleManager.cs:5211** (단일 대상 스킬 메인)
- **`ExecuteMandritto`** — **BattleManager.cs:5524** (전열 다중 타격 예시. `allowedTargetSlots`+`enemyLine.GetCharactersInMask`로 슬롯 기반 타겟 — :5528-5529)
- 데미지: `CalculateDQDamage` — **:5598**, 슬롯 보정 `ApplySlotDamageToTarget` — **:5695** (`SlotBalanceTable.GetDamageMultiplier`)
- 명중: `EvaluateHitChance` (오버로드 3개) — **:5727 / :5741 / :5755**, 코어 `ComputeHitChanceCore` — **:5708** (`SlotBalanceTable.GetHitChanceMultiplier`)

### 현재 스킬 종류 (전부 **플레이어** 무기 Lore + Warfare. `.asset` 개수)
경로: `Assets/Scripts/Battle/Data/Skills/<폴더>/`
- Axe_Lore: 10, Warfare: 10, Combat Arts: 7, Devine_Lore: 7, Fire_Lore: 7
- Bow_Lore: 6, Dagger_Lore: 6, Dual_Lore: 6, Polearm_Lore: 6, Spear_Lore: 6
- Katana_Lore: 5, Sword_Lore: 5
- **빈 폴더(0개): Mace_Lore, Two_Handed_Axe, Two_Handed_Sword**
- **몬스터 전용 스킬 폴더는 없음.** (MonsterSO에 스킬 연결 필드는 있으나 — ④ 참고 — 실제 할당된 몬스터 스킬 에셋 존재 여부는 **미확인**)

### ★Warfare 스킬 5종 = 미확정 컨셉 단계★
- `Warfare_ReadingTheTide`, `Warfare_PrincelyTerror`, `Warfare_FrictionOfWar`, `Warfare_FormationShift`, `Warfare_PrincelyRuthlessness`
- 자리이동/지휘 버프·디버프 컨셉으로 보이나 **확정 아님** (예: `Warfare_PrincelyRuthlessness` description에 "사용 설명 추가 예정" 명시). `FormationShift`는 "아군 전열·후열 포진 변경" 컨셉이나 미구현. **다음 작업(자리이동/버프)과 직접 연관 — 컨셉 확정부터 필요.**

---

## ④ 전투 흐름 / AI (몬스터 AI 제작 시)

### 턴 순서: `BuildTurnOrder` — **BattleManager.cs:2919**
- 아군 + 적 전원을 한 리스트에 넣고 **Agility × Random(0.8~1.2)** 내림차순 정렬 (:2943). 슬롯 무관.

### ★적 행동/AI: `ExecuteEnemyTurn` — **BattleManager.cs:3633**★
- 현재 적은 **`GetRandomAlivePartyMember()`(:3645)로 랜덤 파티원 1명에게 기본 공격만** 함.
- **스킬 사용 없음. AI 패턴(전열 우선/약한 적 노리기 등) 없음. 슬롯 기반 타겟팅 없음.**
- 타겟 가중치 `GetPartySlotWeight` — **:3969** — 는 **역할 기반**(Hero/Warrior 가중치 ↑), **슬롯 무관**.
- 데미지 처리: `CalculateDQDamage`(:3664) → `ApplySlotDamageToTarget`(:3665, 슬롯 보정) → 방어/블록.
- → **적 AI·적 스킬 사용은 사실상 백지 상태.** 몬스터 AI 작업 = 이 함수를 슬롯/스킬 인지 방식으로 확장하는 것.

### 적 스킬 사용
- **미구현.** ExecuteEnemyTurn에 스킬 분기 없음. `MonsterSO.ActiveSkills`(데이터)는 존재하나 런타임에서 호출되지 않음.

---

## ⑤ 자리이동 기반 자산 (스킬로 적·아군 옮길 때 재사용)

### 아군 포지션 이동 — **재사용 가능**
- **`SwapMainLine(a, b)`** — **CompanionPartyPersistence.cs:463** — 이 아군 MainLine 자리이동의 검증된 기반. 데이터(heroSlotIndex+ActiveRoster) 갱신 → 다음 전투에 반영.
- 단 **현재는 던전 Tactics 화면(전투 밖)에서만 호출됨** (`TacticsView.cs`). **전투 중 자리이동 스킬은 이 데이터 모델이 아니라 `activePartyMembers`/`currentSlot`을 직접 조작해야 할 수 있음** — 전투 중에는 `currentSlot`(InitializeBattleLines가 세팅)이 진실의 소스. 전투 중 아군 자리 교환 = `activePartyMembers` 순서 교체 + `currentSlot` 갱신 + `playerLine` 갱신 필요 (미구현, 설계 필요).

### 적 슬롯 관리 / 적 자리이동 — **부분적 토대 있음**
- 적은 `ActiveRoster` 없음. **`EnemyStats.currentSlot`(EnemyStats.cs:51)이 적 위치의 진실의 소스.** 스폰 시 `AssignSlotsByAllowedSlots`+`GetBattleSlotFromTransform`로 세팅(BattleManager, 슬롯 1~4).
- **`MoveToSlot(EnemyStats mover, BattleSlot target, ...)`** — **BattleManager.cs:1167** — 적 1마리를 목표 슬롯으로 이동/스왑하는 함수가 **이미 존재**. 빈 슬롯→이동, 점유→스왑, currentSlot+월드위치 갱신. **적 자리이동 스킬의 기반으로 재사용 가능.**
  - 단 이 함수의 호출처는 현재 디버그 키(F9/F10 좌우 이동)뿐 — 스킬에서 호출하도록 배선 필요.
- `enemyLine`(BattleLine<EnemyStats>, BattleManager.cs:349)은 슬롯 1~4만 담는 레거시 미러. `Swap`/`Move`(BattleLine.cs:115/130) 보유하나 currentSlot과 별개라 직접 쓰면 불일치 위험.

### 후열 타격 / 끌어당기기·밀치기
- **후열 타격:** 가능. `allowedTargetSlots = Back(=Slot3|Slot4)`로 스킬 설정 후 `enemyLine.GetCharactersInMask(Back)`로 후열 적만 타겟 (ExecuteMandritto:5524 패턴 참고). 단 런타임 적용은 Mandritto류만 — 일반 스킬은 배선 필요(③).
- **끌어당기기/밀치기:** 적은 `MoveToSlot`로 가능(토대 있음). 아군은 전투 중 이동 함수 미구현(설계 필요). ⚠️ 과거 7슬롯용 전↔후열 밀치기 함수(`GetNearestSlotInOppositeRow`)는 **이미 삭제됨**(Phase 1.5 Step 1) — 4슬롯 기준으로 새로 만들어야 함.

---

## ⑥ ★주의사항 / 미완성·미테스트 항목★ (함정)

1. **프리셋 디버그 경로 = 압축 방식** — `BuildBattlePartyForEncounter` 내 동료 0마리 시 Warrior/Rogue/Wizard를 append (BattleManager.cs:~2041). heroSlotIndex 미반영. **디버그 전용**이라 일반 플레이엔 영향 없으나, 디버그로 테스트 시 슬롯이 다르게 나올 수 있음.
2. **`BattleSlot.Slot5/6/7` enum 잔재** — SkillData.cs:60-62. 기능 무관(실전 미사용)이나 코드에 남아 있음. `SlotBalanceTable`/`BattleSimCombatMath`의 `case Slot5/6/7`가 이 enum에 의존 → enum 제거하려면 그 case들 먼저 정리해야 컴파일 깨지지 않음.
3. **같은 종 동료 2마리 + Hero 이동 겹칠 때 id 매칭** — `BuildBattlePartyForEncounter`가 `companionId`로 매칭(BattleManager.cs:~2013). 로직상 1:1이나 **2마리 동종 + Hero 이동 동시 케이스는 실측 미완료(미확인).**
4. **★`SlotBalanceTable` = 슬롯별 명중/피해 배율의 진짜 소스 (SlotHelper와 별개)★** — `Assets/Scripts/Battle/SlotBalanceTable.cs`.
   - 명중 배율 `HitChanceByBalanceIndex` — **SlotBalanceTable.cs:10** (슬롯1=0.95 / 2=0.90 / 3=0.75 / 4=0.65)
   - 피해 배율 `DamageMultiplierByBalanceIndex` — **:19** (슬롯1,2=1.10 / 3,4=0.90)
   - 슬롯→인덱스 변환 `ResolveBalanceSlotIndex` — **:32** (BattleSlot enum을 직접 1~4로 매핑, **SlotHelper 미사용**. Slot5/6/7은 3/4로 매핑 — 잔재)
   - 조회 `GetHitChanceMultiplier` — **:53**, `GetDamageMultiplier` — **:61**
   - **라이브 전투에 실제 적용됨:** `ApplySlotDamageToTarget`(BattleManager.cs:5695)·`ComputeHitChanceCore`(:5708)가 호출.
   - **→ 다음 섹션이 "전투 밸런스"를 다룰 때, 슬롯별 명중/피해를 바꾸려면 `SlotHelper`가 아니라 이 `SlotBalanceTable`의 두 배열(:10, :19)을 수정해야 한다.** (SlotHelper는 전열/후열 *판정*만, 배율 *수치*는 이 표.)
   - **→ 자리이동 스킬로 적/아군이 슬롯을 옮기면, 옮긴 자리의 명중/피해 배율이 자동 적용된다** (currentSlot이 바뀌면 이 표가 그 슬롯 값으로 계산). 후열로 이동 = 맞기 어렵고 덜 아픔, 전열로 이동 = 맞기 쉽고 더 아픔. **이게 의도된 동작인지 다음 작업자가 반드시 인지할 것.**
5. **Button Transition = Color Tint** — Tactics MainLine/Reserve 슬롯 Button들. 선택 강조(노랑)·비활성(검정) 틴트가 버튼 색 전이와 충돌해 호버 시 깜빡일 수 있음. 문제 시 해당 Button Transition을 None으로.
6. **런타임 슬롯 제한 미적용** — `CanCastFrom`(시전 슬롯) 호출 0건, `allowedTargetSlots`(타겟 슬롯)는 Mandritto류만. 스킬 슬롯 규칙을 실제로 강제하려면 실행 경로에 배선 필요(③).
7. **TacticsView 레거시 필드** — `heroSlot`/`activeSlots`(HideInInspector)가 `mainlineSlots`로 자동 이관용으로 남아 있음(TacticsView.cs). 기능 정상이나 추후 정리 대상.
8. **전투 중 아군 자리이동 미구현** — `SwapMainLine`은 던전 Tactics용(데이터 모델). 전투 중에는 `currentSlot`/`activePartyMembers`/`playerLine`을 직접 다뤄야 함 — 설계 필요(⑤).
9. **확인된 TODO 표기:** 코드 전수 TODO 스캔은 **미수행**. 위 항목 외 추가 TODO가 있을 수 있음(미확인).

---

## ⑦ 핵심 파일 경로 목록 (줄 번호)

| 파일 | 핵심 위치 |
|---|---|
| `Assets/Scripts/Battle/Data/SkillData.cs` | BattleSlot enum:53 / SlotMask:73(Front:84,Back:85,Any:86) / SlotHelper:92(IsFrontRow:119,127 IsBackRow:136,144 GetRow:101,109 ToSlotMask:153) / SkillTargeting:185(targetFaction:190, allowedCasterSlots:197, allowedTargetSlots:205, CanCastFrom:234, CanTarget:243) / SkillData:263(usageType:273, targeting:288, effects:315) |
| `Assets/Scripts/Battle/CompanionPartyPersistence.cs` | ActiveRoster:46 / WaitlistPaths:50 / MainLineSlots:53 / **heroSlotIndex:59** / Clear:108 / SwapActive:217 / SwapWait:234 / SwapActiveWait:256 / **BuildMainLineOccupancy:411 / MainLineSlotToRosterIndex:438 / RosterIndexToMainLineSlot:450 / SwapMainLine:463** |
| `Assets/Scripts/Battle/BattleManager.cs` | MoveToSlot(적):1167 / playerLine:348 enemyLine:349 / **BuildBattlePartyForEncounter:1991** / PositionPartyMembers:2290 / **InitializeBattleLines:2604** / BuildTurnOrder:2919 / **ExecuteEnemyTurn:3633** / GetRandomAlivePartyMember:3950 / GetPartySlotWeight:3969 / ExecuteSkill:5211 / ExecuteMandritto:5524(allowedTargetSlots:5529) / CalculateDQDamage:5598 / IsFrontRowEnemy:5646 / ApplySlotDamageToTarget:5695 / ComputeHitChanceCore:5708 / EvaluateHitChance:5727,5741,5755 |
| `Assets/Scripts/Battle/EnemyStats.cs` | currentSlot:51 / allowedSlots:54 / IsFrontRow:56 IsBackRow:57 |
| `Assets/Scripts/Battle/PlayerStats.cs` | currentSlot:300 / IsFrontRow:307 IsBackRow:308 |
| `Assets/Scripts/Battle/Data/Monsters/MonsterSO.cs` | FormationType:39 / MonsterRowPreference:66 / **ActiveSkills:303 / PassiveSkills:306** / RowPreference:314 / AllowedSlots:317 |
| `Assets/Scripts/Battle/SlotBalanceTable.cs` | ResolveBalanceSlotIndex:32 / GetHitChanceMultiplier:53 / GetDamageMultiplier:61 (명중/피해 배율 하드코딩) |
| `Assets/Scripts/Battle/BattleLine.cs` | BattleLine<T>:10 / Swap:115 / Move:130 (4슬롯 레거시 컨테이너) |
| `Assets/Scripts/Dungeon/TacticsView.cs` | MainLine swap UI (SwapMainLine 호출처). 던전 Tactics 화면 전용 |

---

## 다음 작업 시작점 제안 (어디부터 보면 되는지)

1. **몬스터 전용 스킬 + 몬스터 AI:**
   시작점 = **`ExecuteEnemyTurn` (BattleManager.cs:3633)**. 현재 "랜덤 단일 기본공격"을 → "MonsterSO.ActiveSkills(MonsterSO.cs:303)에서 스킬 선택 → 슬롯/타겟 판정 → ExecuteSkill 유사 경로로 실행"으로 확장. 먼저 적이 스킬을 가질 수 있게 MonsterSO 에셋에 스킬 할당 여부(미확인) 확인부터.

2. **스킬에 따른 적·아군 자리이동:**
   - 적 이동: **`MoveToSlot` (BattleManager.cs:1167)** 재사용 — 스킬 실행에서 호출하도록 배선.
   - 아군 이동(전투 중): 신규 설계 필요. `currentSlot`+`activePartyMembers`+`playerLine` 동기화 함수부터 만들 것 (`SwapMainLine`은 던전용이라 전투 중엔 부적합).
   - 4번(SlotBalanceTable) 밸런스 영향 함께 고려.

3. **스킬 버프/디버프:**
   시작점 = **`SkillEffect`(SkillData.cs:251) + effects 리스트(:315)** 와 상태이상(`StatusEffectSO`, `curseEffect`:340). 적용 경로는 `ExecuteSkill`(5211) 내부. Warfare 5종(③)의 컨셉 확정이 선행되어야 함.

4. **선결 과제 (위 작업 공통 전제):**
   - 런타임 슬롯 제한 배선(③ — CanCastFrom/allowedTargetSlots를 ExecuteSkill 경로에 적용)
   - 적 스킬 실행 경로 신설(현재 적은 ExecuteSkill을 안 탐)

---

*작성 기준 커밋 `05c9e51`. 줄 번호는 작업 중 변동되므로 함수명 grep으로 재확인 권장.*
