# 전투 시스템 점검 체크리스트

> 작성: 2026-03-30 | 최종 수정: 2026-03-31
> 범례: `[ ]` 미확인 | `[v]` 통과 | `[!]` 버그/문제 | `[?]` 런타임 확인 필요
> Unity 로그 경로: `C:\Users\User\AppData\Local\Unity\Editor\Editor.log`
> 메인 저장소: `E:\Dawi\My Programs\Game Maker\Unity\Projects\Abyssdawn 01`

---

## 작업 규칙 (다른 AI가 이어받을 때 필독)

1. **모든 코드 수정은 메인 저장소에 직접** — worktree(`upbeat-lalande`) 아님
2. **사용자 승인 없이 모든 작업 진행** — 시스템 파괴 수준 위험만 예외
3. **테스트 방법**: 사용자가 Unity에서 플레이 → "로그 읽어" 명령 → `Editor.log` 직접 읽어 분석
4. **체크 완료 즉시 이 파일 업데이트**

---

## 완료된 수정 사항 (이미 코드에 반영됨)

| 파일 | 수정 내용 | 완료 |
|------|-----------|------|
| `PlayerStats.cs` | `currentHP` getter `Debug.Log` 제거 (B-4) | ✓ |
| `PlayerStats.cs` | `currentHP/MP` setter `AssetDatabase.SaveAssets()` 제거 (B-5) — 씬 전환 무한로딩 원인이었음 | ✓ |
| `PlayerStats.cs` | `GetEquippedItemsList()` 헬퍼 추가 — `EquipmentManager` 없을 때 `statData`에서 직접 장비 읽기 | ✓ |
| `PlayerStats.cs` | 모든 `GetEquipment*Bonus()` 메서드가 헬퍼 사용하도록 리팩터 — 배틀씬 HP 105(장비보너스+5) 반영 | ✓ |
| `BattleManager.cs` | `ProcessAllIgniteDamage()` 적 DoT 이중 루프 제거 (B-1) | ✓ |
| `BattleManager.cs` | `ExecuteAllyResolution()` 스턴 체크 추가 (B-3) | ✓ |
| `BattleManager.cs` | `ExecuteEnemyTurn()` 스턴 체크 추가 (B-2) | ✓ |
| `BattleManager.cs` | `GameOverRoutine()` 추가 — 전멸 시 HP리셋+던전초기화+1층씬 로드 | ✓ |
| `BattleManager.cs` | `startDungeonScene` 필드 추가 (기본값: `"Abyssdawn_Dungeon_2D 07"`) | ✓ |
| `BattleManager.cs` | 단일 공격/듀얼 공격 데미지 메시지에서 괄호 공식 `(6+0)` 제거 | ✓ |
| `PlayerStats.cs` | `GetTraitBonus()` 추가 — 모든 스탯 computed property에 종의 특성 보너스 반영 | ✓ |
| `PlayerStats.cs` | `Awake()`에서 `CheckAndActivateTrait()` 호출 — 런타임 특성 발동 | ✓ |
| `PlayerStats.cs` | Last Stand 발동 로직 추가 — HP 0 시 Luck 기반 생존 (30%+Luck×2%, 최대 80%), 전투당 1회 | ✓ |
| `PlayerStats.cs` | `HasSpecialEffect()`, `ResetLastStand()` 헬퍼 추가 | ✓ |
| `BattleManager.cs` | `StartBattle()` 시 Last Stand 플래그 리셋 | ✓ |
| `BattleManager.cs` | `BuildTurnOrder()` — 민첩 기반 확률 정렬 (`Agility × Random(0.8~1.2)`) | ✓ |
| `BattleManager.cs` | `TryApplyWeaponCurse()` — `EquipmentManager` 없을 때 `statData` fallback | ✓ |
| `BattleManager.cs` | `TryBlock()` — `EquipmentManager` 없을 때 `statData` fallback | ✓ |
| `BattleManager.cs` | 적 공격에 `attackDebuff` 적용, 스킬 사용 시 `IsSilenced()` 체크 | ✓ |
| `BattleManager.cs` | 한글 전투 메시지 → 영어 전면 교체 | ✓ |
| `EnemyStats.cs` | 상태이상 부여/만료 시 `flatIcon` 아이콘 패널 표시/갱신 (수정 중) | ✓ |
| `Curse_Stun.asset` | `preventAction: 1`, `preventSkillUse: 1` 수정 (스턴 실제 발동) | ✓ |
| `ArmorBreak.asset` / `Block_Shield.asset` | 한글 effectName/description → 영어 | ✓ |

---

## 알려진 구조적 이슈 (수정 불필요 또는 설계 결정 필요)

| 코드 | 이슈 | 상태 |
|------|------|------|
| `DungeonEncounter.cs` | `FindFirstObjectByType<PlayerStats>()`가 `PlayerMarker`(name="None")를 먼저 찾음 — Hero 못 찾음 | 미수정 (SO 경유로 HP 영속은 작동) |
| `DungeonEncounter.cs` | `battleSceneName = "Abyysborn_Battle 01"` (구버전 씬 로드 중) | Inspector에서 확인 필요 |
| `PlayerStats.cs` | `PlayerMarker`, `PlayerArea` 오브젝트에 PlayerStats 컴포넌트 부착 — `playerName`이 "None"/""임 | 씬에서 정리 권장 |
| `BattleSystem.cs` | `PlayerAttack/EnemyAttack` 메서드 — `BattleManager`에서 호출 안 됨 (데드코드) | 설계 결정 필요 |
| `GameManager.cs` | `SaveFromPlayer()` — `statData != null`이면 조기 리턴, `staticPartyData` 미사용 | SO 경유로 작동하므로 당장 문제 없음 |

---

## 긴급 버그

- `[v]` **B-1** `ProcessAllIgniteDamage()` — 적 DoT 이중 루프 제거 ✓
- `[v]` **B-2** `ExecuteEnemyTurn()` — 스턴된 적 행동 차단 추가 ✓
- `[v]` **B-3** `ExecuteAllyResolution()` — 스턴된 아군 행동 차단 추가 ✓
- `[v]` **B-4** `PlayerStats.currentHP` getter — Debug.Log 제거 ✓
- `[v]` **B-5** `PlayerStats.currentHP/MP` setter — `AssetDatabase.SaveAssets()` 제거 ✓
- `[?]` **B-6** `BattleSystem.cs` — `PlayerAttack/EnemyAttack` 미호출 (데드코드 여부 설계 결정 필요)
- `[?]` **B-7** 동료 HP/MP 씬 전환 미보존 — Solo 모드에선 무관, Full Party 전환 시 문제

---

## 0단계 — 씬 영속성

- `[v]` **0-1** 던전 → 전투 씬 전환 시 HP/MP 유지 (SO 경유로 정상 영속) ✓
- `[v]` **0-2** 전투 종료 → 던전 복귀 시 HP/MP 올바르게 반영 (20/105 로그 확인) ✓
- `[v]` **0-3** SO `currentHP/MP` 저장/복원 정상 (`SO에서 로드한 HP 유지` 로그 확인) ✓
- `[v]` **0-4** 전멸 → `GameOverRoutine` 정상 동작 (HP 105/105 리셋 + 1층 씬 로드 확인) ✓
- `[v]` **0-5** 인카운터 반복 정상 — 쿨다운 3칸 + 랜덤 재시드 수정 후 롤 매번 다름 확인 ✓

> **0-4 런타임 테스트 방법**: 전투에서 일부러 전멸 → 1층 던전 씬(`Abyssdawn_Dungeon_2D 07`)으로 이동하는지, HP가 최대값으로 리셋됐는지 확인

---

## 1단계 — 캐릭터 스탯 계산

- `[v]` **1-1** 기본값 × `CharacterClass` 배율 계산 (computed property 확인)
- `[v]` **1-2** 장비 장착/해제 시 스탯 즉시 반영
- `[v]` **1-3** 패시브 보너스 스탯 반영
- `[v]` **1-4** `MemoryOfSpecies` 개별 보너스 반영
- `[v]` **1-5** `MemoryOfSpecies` 3세트 → `TraitsOfSpecies` 런타임 발동 확인 ✓ (`Last Stand` 활성화 로그 확인. 스탯 보너스는 0이 정상 — 특수효과 기반. 단 `LastStand` 전투 발동 로직 미구현)
- `[v]` **1-6** 레벨 성장치 누적 적용
- `[v]` **1-7** `maxHP/MP` computed property 전투 시작 시 정확도 (105 확인 ✓)
- `[v]` **1-8** 양손 무기 장착 시 왼손 자동 해제 후 스탯 반영

---

## 2단계 — 턴 구조

- `[v]` **2-1** Agility 내림차순으로 턴 순서 구성
- `[v]` **2-2** 동일 Agility 시 아군 먼저 (LINQ stable sort)
- `[v]` **2-3** 민첩 기반 확률 정렬 구현 — `Agility × Random(0.8~1.2)`. ±20% 범위 내에서만 역전 가능. 로그 확인 ✓
- `[v]` **2-4** 전열(Slot1~2) / 후열(Slot3~4) 슬롯 구조 정상
- `[v]` **2-5** 스턴 시 행동 불가 — B-2/B-3 수정으로 해결 ✓
- `[v]` **2-6** 캐릭터 사망 시 다음 라운드 턴 큐에서 제거
- `[v]` **2-7** 전투 종료 조건 (전원 사망) 발동
- `[v]` **2-8** `battleEnded` 플래그 중복 처리 방지

---

## 3단계 — 상태이상

- `[v]` **3-1** 부여 확률 `physicalApplyChance` 롤 체크
- `[v]` **3-2** 적 DoT 매 턴 처리 — 이중 루프 제거로 해결 ✓
- `[v]` **3-3** 아군 DoT 매 턴 처리 (아군 루프는 1번)
- `[v]` **3-4** 지속 턴 만료 시 자동 해제
- `[v]` **3-5** 중복 부여 시 긴 쪽 지속턴 유지 (갱신 방식)
- `[v]` **3-6** 스턴 행동 차단 — B-2/B-3 수정으로 해결 ✓
- `[v]` **3-7** 상태이상 아이콘 World-Space UI 표시 — 정상 작동 확인 ✓
- `[v]` **3-8** `flatIcon` 전투 화면 표시 — 정상 작동 확인 ✓

---

## 4단계 — 스킬

- `[v]` **4-1** MP 소모 스킬 사용 시 MP 차감
- `[v]` **4-2** MP 부족 시 스킬 차단
- `[v]` **4-3** 스킬 배율(`multiplier`) 데미지 반영
- `[v]` **4-4** `allowedCasterSlots` 포지션 제한
- `[v]` **4-5** `allowedTargetSlots` 타겟 제한
- `[v]` **4-6** 다중 타격 스킬 타수 — Quickhand 2타 로그 확인 (0.61×, 0.79× 각각 출력) ✓
- `[v]` **4-7** 듀얼 장착 시 0.35~0.45 배율 적용 (2타 각각 랜덤)
- `[v]` **4-8** 선행 스킬 미충족 시 잠금
- `[v]` **4-9** `DunBreak` 확률 적용
- `[v]` **4-10** 전투 중 소비 아이템 `ConsumableInventory.UseItem()` 호출 및 수량 차감
- `[v]` **4-11** 힐량 `hpRecoveryPercent × maxHP` 계산
- `[v]` **4-12** 아이템 버튼 Hero 전용 — 의도된 설계 확정 ✓
- `[v]` **4-13** `selectedConsumableItem` 인벤토리 클릭 시 BattleManager 자동 연동 완료 ✓

---

## 5단계 — 특수 시스템

- `[v]` **5-1** `ArmorBreak` 방어력 누적 감소
- `[v]` **5-2** 방어력 0 이하 방지
- `[v]` **5-3** `ArmorBreak` 크리티컬 1.5× 적용
- `[v]` **5-4** 블록 확률 계산
- `[v]` **5-5** 블록 발동 시 데미지 감소
- `[v]` **5-6** 방어 행동 `defenceReduction 0.5f` 적용
- `[v]` **5-7** 명중 계산 공식 확인 — `baseHit × agiModifier + Luck×0.002 + 패시브 + 장비` ✓
- `[v]` **5-8** 명중률 캡 — Clamp(0.2, 0.98) 코드 확인 (설계 10%/100%와 다르나 의도된 값으로 확정) ✓
- `[v]` **5-9** 크리티컬 공식 확인 — `roll(0~100) < criticalChance(25) + Luck` ✓
- `[-]` **5-10** `BattleRecorder` F6/F7/F8 — 핫키 기능, 현재 우선순위 제외
- `[v]` **5-11** `BattleSystem.cs` 데드코드 — 완전 미호출 확인, 파일 삭제 완료 ✓

---

## 6단계 — BalanceSimulator

- `[ ]` **6-1** `BalanceSimulator` `[ContextMenu]` 실행
- `[ ]` **6-2** 50층 클리어 결과 기록 (승률 / 평균 턴 수 / 생존 층)
- `[ ]` **6-3** 전사 파티 기준값 확정
- `[ ]` **6-4** 상태이상 없는 파티 vs 상태이상 파티 DPS 비교
- `[ ]` **6-5** 힐러 없는 파티 생존 한계 층 확인
- `[ ]` **6-6** 듀얼 빌드 vs 단일 무기 DPS 비교

**결과 기록란:**
| 항목 | 결과값 | 목표값 |
|------|--------|--------|
| 전사 파티 50층 승률 | — | — |
| 평균 전투 턴 수 | — | — |
| 전사×4 생존 한계 층 | — | — |
| 힐러 포함 50층 승률 | — | — |

---

## 7단계 — UI 연동

- `[ ]` **7-1** HP/MP 바 피해/회복 시 실시간 갱신
- `[ ]` **7-2** 상태이상 아이콘 부여/해제 표시
- `[ ]` **7-3** 데미지 숫자 표시
- `[ ]` **7-4** 턴 순서 / 현재 액션 캐릭터 포커스
- `[ ]` **7-5** 파티 상태 패널 파티원 수에 맞게 조정
- `[ ]` **7-6** StatRow ◆ 컬러 접두어 올바르게 표시
- `[v]` **7-7** `selectedConsumableItem` 인벤토리 선택 시 자동 설정 완료 ✓

---

## 8단계 — 미구현 시스템 (CLAUDE.md 13섹션 참고)

> ⚠️ Backlash → **Backflow(역류)** 로 명칭 정정

- `[v]` **8-1** `SkillData.backflowChance` 등 Backflow 필드 추가 ✓
- `[v]` **8-2** `EquipmentData.magicAmplify / backflowSuppression` 추가 ✓
- `[v]` **8-3** `PassiveData.backflowSuppression` 추가 ✓
- `[v]` **8-4** `PlayerStats.TotalBackflowSuppression / MagicAmplify` 계산 ✓
- `[v]` **8-5** BattleManager `TryApplyBackflow()` 역류 발동 로직 구현 ✓
- `[ ]` **8-6** Mana Stabilize SO + 버프 턴 관리
- `[ ]` **8-7** 마법 장비 SO 일괄 제작
- `[ ]` **8-8** Bow 견제사격 + 인터셉트 타이밍

---

## 다음 진행 순서

```
현재 위치: 3단계 진행 중
  [!] 3-7/3-8  상태이상 아이콘 — 출혈 적용 시 HP/MP UI 사라지는 버그 수정 필요
               flatIcon 패널 미표시 버그 수정 필요

완료 후:
  4단계 → 5단계 순으로 런타임 미확인 항목([?]/[ ]) 테스트
  6단계 BalanceSimulator는 전투 로직 확정 후 진행
  8단계 미구현 시스템은 CLAUDE.md 13섹션 명세 기반으로 구현

긴급 버그 (다음 세션 최우선):
  - EnemyStats CreateWorldSpaceUI() 재호출 시 statusIconPanel 초기화 누락
  - RefreshStatusIcons() 호출 타이밍 문제
```

---

## 빌드 프로파일 등록 씬 (2026-03-30 기준)

| 씬 이름 | 용도 |
|---------|------|
| `Scenes/SampleScene` | 테스트 씬 |
| `Abyssdawn_Dungeon_2D 07` | 현재 활성 던전 씬 (게임오버 시 로드 대상) |
| `Abyysborn_Battle 01` | 현재 활성 전투 씬 (오타 있는 구버전 씬명이 실제 사용 중) |

> ⚠️ `Abyssdawn_Battle 01` (신버전)은 빌드에 미등록 — 현재 `Abyysborn_Battle 01` 사용 중

---

*Unity 로그: `C:\Users\User\AppData\Local\Unity\Editor\Editor.log`*
*테스트: 사용자 플레이 후 "로그 읽어" → Editor.log 직접 분석*
