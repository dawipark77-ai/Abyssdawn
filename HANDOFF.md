# AI 인수인계 문서 — Abyssdawn 01 전투 시스템 점검

> 작성: 2026-03-30
> 이전 AI: Claude Sonnet 4.6
> 다음 AI에게: 이 문서와 `BATTLE_INSPECTION.md`를 먼저 읽고 작업을 이어가세요.

---

## 프로젝트 기본 정보

- **엔진**: Unity 6, URP 2D
- **메인 저장소**: `E:\Dawi\My Programs\Game Maker\Unity\Projects\Abyssdawn 01`
- **절대 규칙**: worktree(`upbeat-lalande`)가 아닌 **메인 저장소에 직접** 수정
- **테스트 방법**: 사용자가 Unity 플레이 → "로그 읽어" 명령 → `C:\Users\User\AppData\Local\Unity\Editor\Editor.log` 직접 읽어 분석
- **승인 없이 작업 진행** (시스템 파괴 수준만 예외)
- **CLAUDE.md** 반드시 참고 (프로젝트 전체 구조/패턴 설명)

---

## 빌드 프로파일 등록 씬 (현재 기준)

| 씬 이름 | 용도 |
|---------|------|
| `Abyssdawn_Dungeon_2D 07` | 현재 활성 던전 씬 |
| `Abyysborn_Battle 01` | 현재 활성 전투 씬 (오타 있는 구버전명이 실제 사용 중) |
| `Scenes/SampleScene` | 테스트 씬 |

---

## 이번 세션에서 완료한 수정

| 파일 | 수정 내용 |
|------|-----------|
| `PlayerStats.cs` | `currentHP` getter `Debug.Log` 제거 |
| `PlayerStats.cs` | `currentHP/MP` setter `AssetDatabase.SaveAssets()` 제거 → 무한로딩 원인 해결 |
| `PlayerStats.cs` | `GetEquippedItemsList()` 헬퍼 추가 — 배틀씬에 `EquipmentManager` 없어도 `statData`에서 장비 직접 읽기 → HP 105(+5 장비보너스) 정상 반영 |
| `BattleManager.cs` | `ProcessAllIgniteDamage()` 적 DoT 이중 루프 제거 |
| `BattleManager.cs` | `ExecuteAllyResolution()` / `ExecuteEnemyTurn()` 스턴 체크 추가 |
| `BattleManager.cs` | 공격 메시지 괄호 공식 `(6+0)` 제거 |
| `BattleManager.cs` | `GameOverRoutine()` 추가 — 전멸 시 HP리셋 + `DungeonPersistentData.ClearState()` + `GameManager.ClearAllData()` + 1층 씬 로드 |
| `BattleManager.cs` | `startDungeonScene` Inspector 필드 추가 (기본값: `"Abyssdawn_Dungeon_2D 07"`) |
| `BattleManager.cs` | `ReturnToDungeonRoutine()` — 복귀 시 `DungeonEncounter.justReturnedFromBattle = true` 플래그 설정 |
| `DungeonEncounter.cs` | 전투 복귀 후 **3칸 인카운터 쿨다운** 추가 (`postBattleCooldownSteps = 3`) |
| `MapManager.cs` | 던전 생성 완료 후 `Random.InitState(DateTime.Now.Ticks)` 재시드 — 인카운터 롤 고정 버그 해결 |

---

## 알려진 미해결 구조적 이슈 (건드리지 말 것 — 설계 결정 필요)

| 코드 | 이슈 |
|------|------|
| `DungeonEncounter.cs` | `FindFirstObjectByType<PlayerStats>()`가 `PlayerMarker`(name="None")를 먼저 찾음 — Hero 못 찾음. 하지만 SO 경유로 HP 영속은 작동하므로 당장 문제 없음 |
| `DungeonEncounter.cs` | `battleSceneName`이 `"Abyysborn_Battle 01"` (구버전). 이게 실제 사용 중인 씬이므로 건드리지 말 것 |
| `BattleSystem.cs` | `PlayerAttack/EnemyAttack` — `BattleManager`에서 호출 안 됨 (데드코드). 설계 결정 필요 |
| `GameManager.cs` | `SaveFromPlayer()` — `statData != null`이면 조기 리턴, `staticPartyData` 미사용. SO 경유로 작동하므로 당장 문제 없음 |
| 던전 씬 | `PlayerMarker`, `PlayerArea` 오브젝트에 `PlayerStats` 컴포넌트 부착, `playerName`이 "None"/""임 — 씬 청소 권장 |

---

## 다음 AI가 해야 할 작업 목록 (우선순위 순)

### 🔴 즉시 확인 (이전 세션 미확인 런타임 테스트)

- [ ] **0-5 확인**: 수정 후 인카운터 반복 테스트 — 전투 복귀 후 3칸 쿨다운 작동하는지, 롤이 매번 다른지 로그 확인
  - 테스트: 던전에서 전투 → 복귀 → 즉시 이동 3칸 (쿨다운 로그 뜨는지) → 4칸째부터 정상 확률인지

### 🟡 1단계 — 런타임 미확인 항목

- [ ] **1-5**: `MemoryOfSpecies` 동종 3세트 → `TraitsOfSpecies` 런타임 발동 여부 확인
  - 우려사항: `OnValidate()`가 에디터 전용이라 빌드/플레이 중 세트 효과 미발동 가능성

### 🟡 2단계

- [ ] **2-3**: 아군/적 턴 순서가 민첩 단일 정렬인지, 의도된 설계인지 사용자에게 확인

### 🟡 3단계

- [ ] **3-7**: 상태이상 아이콘 World-Space UI 런타임 표시 확인
- [ ] **3-8**: `flatIcon` 전투 화면 표시 확인

### 🟡 4단계

- [ ] **4-6**: 다중 타격 스킬 타수 런타임 확인
- [ ] **4-12**: 아이템 버튼 Hero 전용(`isHero`) — 의도된 설계인지 사용자 확인
- [ ] **4-13 수정**: `selectedConsumableItem` — 인벤토리 UI에서 선택 시 BattleManager에 자동 설정되도록 구현

### 🟡 5단계

- [ ] **5-7/5-8**: 명중 계산 공식 및 최소10%/최대100% 캡 런타임 확인
- [ ] **5-9**: 크리티컬 공식 실제 수치 런타임 확인
- [ ] **5-10**: `BattleRecorder` F6/F7/F8 동작 확인
- [ ] **5-11 결정**: `BattleSystem.cs` 데드코드 처리 방향 사용자에게 확인

### 🟢 6단계 — BalanceSimulator (전투 로직 확정 후)

- [ ] Inspector에서 `[ContextMenu] Simulate Balance` 실행
- [ ] 50층 클리어 결과 기록

### 🔵 8단계 — 미구현 시스템 구현 (CLAUDE.md 13섹션 참고)

우선순위 순서대로:
1. [ ] `SkillData`에 `backlashChance`, `BacklashType`, `backlashMagnitude`, `backlashStatusEffect` 필드 추가
2. [ ] `EquipmentData`에 `magicAmplify`, `backlashSuppression` 필드 추가
3. [ ] `PassiveData`에 `backlashSuppression` 필드 추가
4. [ ] `PlayerStats`에 `TotalBacklashSuppression` computed property 추가
5. [ ] `BattleSystem.cs` 또는 `BattleManager.cs`에 역효과 발동 로직 구현
6. [ ] `Mana Stabilize` 스킬 SO 생성 + BattleManager 버프 턴 관리
7. [ ] 마법 장비 SO 일괄 제작 (CLAUDE.md 13-B 표 참고)
8. [ ] `Suppression Shot` (Bow 견제사격) SO 생성 + 인터셉트 타이밍 구현

---

## 작업 시 참고할 핵심 파일

| 파일 | 경로 | 용도 |
|------|------|------|
| `BATTLE_INSPECTION.md` | 저장소 루트 | 전체 체크리스트 (항상 업데이트) |
| `CLAUDE.md` | 저장소 루트 | 프로젝트 구조/패턴/미구현 명세 전체 |
| `BattleManager.cs` | `Assets/Scripts/Battle/` | 전투 흐름 (~4600줄) |
| `PlayerStats.cs` | `Assets/Scripts/Battle/` | 스탯 computed property |
| `DungeonEncounter.cs` | `Assets/Scripts/Dungeon/` | 인카운터 로직 |
| `Editor.log` | `C:\Users\User\AppData\Local\Unity\Editor\` | 런타임 로그 |

---

## 체크리스트 업데이트 규칙

작업 완료 시마다 `BATTLE_INSPECTION.md`의 해당 항목을 업데이트:
- `[ ]` → `[x]` (통과)
- `[ ]` → `[!]` (버그 발견)
- `[ ]` → `[?]` (런타임 추가 확인 필요)

---

*이 문서는 Claude Sonnet 4.6이 작성했습니다. 2026-03-30*
