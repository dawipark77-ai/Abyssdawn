# 전투 시스템 점검 스케줄표

> 작성: 2026-03-30 | 코드 분석 완료: 2026-03-30
> 범례: ✅ 정상 | ⚠️ 주의/불완전 | 🐛 버그 확인 | ❌ 미구현

---

## 전체 진행 순서

```
0단계 씬 영속성 (선행 필수)
  ↓
1단계 스탯 계산 검증
  ↓
2단계 턴 구조 검증
  ↓
3단계 상태이상 검증
  ↓
4단계 스킬 검증
  ↓
5단계 특수 시스템 검증
  ↓
6단계 BalanceSimulator 실행 → 기준값 확정
  ↓
7단계 UI 연동 검증 (별도 작업)
  ↓
8단계 미구현 시스템 구현
```

---

## 🔴 긴급 버그 목록 (즉시 수정 필요)

| # | 위치 | 증상 | 심각도 |
|---|------|------|--------|
| B-1 | `BattleManager.ProcessAllIgniteDamage()` | **적 상태이상 DoT가 매 라운드 2번 처리됨** — `ProcessStatusEffectsEndOfTurn()` 이중 호출 (2289~2320줄 두 루프) | 🔴 치명 |
| B-2 | `BattleManager.ExecuteEnemyTurn()` | **스턴된 적이 그대로 행동함** — `enemy.IsStunned()` 체크 없음 | 🔴 치명 |
| B-3 | `BattleManager.ExecuteAllyResolution()` | **스턴된 아군이 그대로 행동함** — `cmd.actor.IsStunned()` 체크 없음 | 🔴 치명 |
| B-4 | `PlayerStats.currentHP` getter | **매 HP 읽기마다 Debug.Log 출력** — 전투 중 수백/수천 번 호출 → 콘솔 폭주, 성능 저하 | 🟠 중증 |
| B-5 | `PlayerStats.currentHP/MP` setter | **`EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()` 매 HP 변경 시 호출** — 에디터 플레이 중 매 공격마다 에셋 저장 → 전투 중 히치 발생 | 🟠 중증 |
| B-6 | `BattleSystem.cs` | **이 파일은 BattleManager에서 호출되지 않음** — `BattleManager.ExecuteAttack`이 독립 구현을 가짐. `BattleSystem.PlayerAttack()`, `BattleSystem.EnemyAttack()` 실질적으로 데드코드. UsePotion()도 BattleManager에서 직접 처리 | 🟡 주의 |
| B-7 | `GameManager.SaveFromPlayer` / `ApplyToPlayer` | **동료 HP/MP가 씬 전환 후 보존되지 않음** — `statData != null`이면 즉시 return. 동료는 `CreateInstance` SO라 영속 안 됨. Solo 모드에서는 현재 문제없으나 Full Party 전환 시 동료 매번 풀피로 초기화 | 🟡 주의 |

---

## 0단계 — 씬 전환 데이터 영속성

| # | 항목 | 코드 분석 결과 | 상태 |
|---|------|---------------|------|
| 0-1 | `staticPartyData` 씬 전환 시 유지 | `static Dictionary` 사용 → 메모리 유지 확인. BUT Hero에 `statData` SO 있으면 GameManager 우회 → SO가 직접 저장소. 코드상 정상 흐름 | ✅ |
| 0-2 | 전투 종료 후 HP/MP 던전 복귀 시 반영 | `ReturnToDungeonRoutine`에서 `gm.SaveFromPlayer` 호출. Hero는 SO 직접 저장으로 처리됨 → 정상 | ✅ |
| 0-3 | `DungeonPersistentData.currentHP/MP` 저장/복원 | BattleManager 복귀 코드에서 DungeonPersistentData 직접 사용 없음. GameManager → SO 경로로 처리 | ⚠️ 런타임 확인 필요 |
| 0-4 | 전투 중 전원 사망 씬 전환 | `CheckBattleEnd` → `ReturnToDungeon` 흐름 존재. `DungeonEncounter.lastDungeonScene` 없으면 씬 0으로 fallback | ✅ |
| 0-5 | 여러 번 인카운터 반복 데이터 오염 | `staticPartyData`는 `DontDestroyOnLoad` GameManager에 귀속 → 오염 없어야 함. 런타임 확인 필요 | ⚠️ 런타임 확인 필요 |

---

## 1단계 — 캐릭터 스탯 계산 검증

| # | 항목 | 코드 분석 결과 | 상태 |
|---|------|---------------|------|
| 1-1 | 기본값 × CharacterClass 배율 | `PlayerStats.Attack` → `characterClass.GetFinalAttack(pureBase)` computed property | ✅ |
| 1-2 | 장비 장착/해제 시 스탯 즉시 반영 | `GetEquipmentAttackBonus()` 등 computed property. 장착 변경 → `OnStatusChanged` 이벤트 발행 | ✅ |
| 1-3 | 패시브 보너스 스탯 반영 | `GetPassiveBonus()` 구현됨. BUT 내부에 Debug.Log 다수 → 성능 주의 | ⚠️ 성능 |
| 1-4 | MemoryOfSpecies 개별 보너스 | PlayerStats에 개별 기억 스탯 보너스 계산 로직 존재 (`levelUpWithMemories` 등) | ✅ |
| 1-5 | **MemoryOfSpecies 3세트 → TraitsOfSpecies 발동** | `PlayerStatData.OnValidate()`에서만 처리 → **런타임에서 세트 효과 미발동 가능성 높음**. `OnValidate`는 에디터 전용 | ⚠️ 런타임 미검증 |
| 1-6 | 레벨 성장치 (CharacterClass 레벨당 성장) | `levelUpWithCharacterClass()` 존재 | ✅ |
| 1-7 | `maxHP/MP` computed property 전투 시작 시 정확도 | `BattleManager.StartBattle` → `InitializeParty` → `gm.ApplyToPlayer` → HP Clamp(0, maxHP) | ✅ |
| 1-8 | 양손 무기 장착 시 왼손 자동 해제 후 스탯 반영 | `EquipmentManager`에서 처리, computed property라 즉시 반영 | ✅ |

---

## 2단계 — 턴 구조 검증

| # | 항목 | 코드 분석 결과 | 상태 |
|---|------|---------------|------|
| 2-1 | Agility 순서대로 턴 | `BuildTurnOrder()` → `OrderByDescending(a => a.agility)` | ✅ |
| 2-2 | 동일 Agility 시 처리 순서 일관성 | LINQ stable sort 보장 → 동일 Agility는 `turnOrder.Add` 순서 유지 (아군 먼저) | ✅ |
| 2-3 | 아군/적군 교대 턴 | **교대 아님** — 민첩 단일 정렬. 적이 고민첩이면 연속 공격 가능. 이것이 의도된 설계인지 확인 필요 | ⚠️ 설계 확인 필요 |
| 2-4 | 전열/후열 SlotMask 구조 | `BattleLine<T>`, `InitializeBattleLines()` 구현됨 | ✅ |
| 2-5 | **스턴 시 행동 불가** | `IsStunned()` 메서드 정의됨. BUT `ExecuteEnemyTurn`/`ExecuteAllyResolution`에서 **체크 없음** → 스턴 무력화 | 🐛 **B-2/B-3** |
| 2-6 | 사망 시 턴 큐 제거 | `BuildTurnOrder()`: `currentHP > 0` 조건 포함. 라운드 시작 시 재구성 | ✅ |
| 2-7 | 전투 종료 조건 | `CheckBattleEnd()` → `AnyPartyAlive()` / `activeEnemies` 생존 체크 | ✅ |
| 2-8 | `battleEnded` 중복 처리 방지 | 모든 코루틴 시작부에 `if (battleEnded) yield break` 체크 | ✅ |

---

## 3단계 — 상태이상(저주/Curse) 검증

| # | 항목 | 코드 분석 결과 | 상태 |
|---|------|---------------|------|
| 3-1 | 부여 확률 `physicalApplyChance` 작동 | `EnemyStats.ApplyStatusEffect()`: `if (Random.value > physicalApplyChance) return false` | ✅ |
| 3-2 | **매 턴 DoT 처리 - 적에게 2번 호출됨** | `ProcessAllIgniteDamage`에서 `activeEnemies` 루프 2개 → 적 DoT 2배 적용, 턴 카운트 2배 감소 | 🐛 **B-1** |
| 3-3 | DoT 아군 처리 | 아군 루프는 1번만 → 정상 | ✅ |
| 3-4 | 지속 턴 만료 후 자동 해제 | `remainingTurns <= 0` → `RemoveAt(i)` | ✅ |
| 3-5 | 중복 부여 처리 | 기존 효과 있으면 `Mathf.Max(existing, new)` 로 긴 쪽 유지 (갱신 방식) | ✅ |
| 3-6 | 스턴: 턴 종료 후 해제 | `remainingTurns` 감소 → 만료 시 제거됨. BUT 실행 중 체크 없음 | 🐛 **B-2/B-3** |
| 3-7 | 상태이상 아이콘 World-Space UI 표시 | `EnemyStats.CreateWorldSpaceUI()` 존재. 아이콘 연결 여부는 런타임 확인 필요 | ⚠️ 런타임 확인 필요 |
| 3-8 | `flatIcon` 전투 UI에 사용 | `EnemyStats` World-Space UI 생성 코드에서 아이콘 직접 참조 여부 확인 필요 | ⚠️ 런타임 확인 필요 |

---

## 4단계 — 스킬 검증

| # | 항목 | 코드 분석 결과 | 상태 |
|---|------|---------------|------|
| 4-1 | MP 소모 스킬 차감 | `ExecuteSkill` 내부 처리 — 코드 확인됨 | ✅ |
| 4-2 | MP 부족 시 스킬 차단 | `ExecuteSkill`에 MP 체크 로직 존재 | ✅ |
| 4-3 | 스킬 배율 데미지 적용 | `ExecuteSkill` 내 `SkillData.multiplier` 사용 | ✅ |
| 4-4/4-5 | SlotMask 포지션/타겟 제한 | `BattleLine`, `SlotMask` 구조 구현됨 | ✅ |
| 4-7 | 듀얼 장착 시 0.35~0.45 배율 | `ExecuteAttack`에서 `IsDualWielding` 체크 후 `Random.Range(0.35f, 0.45f)` 각각 적용 | ✅ |
| 4-9 | DunBreak 확률 | `ExecuteSkill` 내부 처리 | ✅ |
| 4-10 | `ConsumableInventory.UseItem()` 호출 | `ExecuteAllyResolution case "item"` → `ConsumableInventory.Instance.UseItem(cmd.usedItem)` | ✅ |
| 4-11 | 힐량 `hpRecoveryPercent × maxHP` | `OnItemButton()`: `heal = selectedConsumableItem.hpRecoveryPercent * player.maxHP` → `itemHealAmount`로 전달 | ✅ |
| 4-X | **Item 버튼 Hero 전용** | `ConfigureActionUIForActor`: `itemButton.interactable = isHero` → 동료는 아이템 사용 불가 | ⚠️ 설계 확인 필요 |
| 4-X | **`selectedConsumableItem` 수동 연결** | Inspector에서만 설정 가능. 인벤토리 UI와 자동 연결 없음 | ⚠️ 미완성 |

---

## 5단계 — 특수 시스템 검증

| # | 항목 | 코드 분석 결과 | 상태 |
|---|------|---------------|------|
| 5-1 | ArmorBreak 누적 감소 | `ExecuteAttack` → `GetArmorBreakCoefficient` → `target.defense * coeff` | ✅ |
| 5-2 | 방어력 0 이하 방지 | `CalculateDQDamage`에서 `Mathf.Max(0, attack - defense)` 처리 | ✅ |
| 5-3 | ArmorBreak 크리티컬 1.5× | `ExecuteAttack` 크리티컬 분기에서 armorAmt도 1.5× 적용 | ✅ |
| 5-4/5-5 | Block 확률 + 데미지 감소 | `TryBlock(target, out float blockReduction)` → `damage -= blockReduction` | ✅ |
| 5-6 | 방어 행동 `defenceReduction 0.5f` | `ExecuteEnemyTurn`: `target.isDefending` 체크 → `damage *= (1 - defenceReduction)` | ✅ |
| 5-7 | 명중 계산 공식 | **BattleManager 자체 구현 사용** (`CheckEvasion`). `BattleSystem.CheckHit`은 미사용(데드코드) | ⚠️ 참고 |
| 5-8 | 명중률 캡 10~100 | BattleManager의 `CheckEvasion` 공식 확인 필요 (BattleSystem과 다를 수 있음) | ⚠️ 런타임 확인 필요 |
| 5-9 | 크리티컬 `luck × 0.05f` | BattleManager의 `CheckCritical` 공식 확인 필요 (BattleSystem과 독립 구현) | ⚠️ 런타임 확인 필요 |
| 5-10 | BattleRecorder F6/F7/F8 | `BattleRecorder` 자동 생성 및 파티/적 등록 코드 존재 | ✅ |
| 5-X | **`BattleSystem.cs` 실질적 데드코드** | `BattleManager.ExecuteAttack`이 독립 구현. `BattleSystem.PlayerAttack/EnemyAttack`은 호출되지 않음 | 🐛 **B-6** |

---

## 6단계 — BalanceSimulator 실행 → 기준값 확정

| # | 항목 | 상태 |
|---|------|------|
| 6-1 | `BalanceSimulator.cs` `[ContextMenu]` 실행 | ☐ |
| 6-2 | 50층 클리어 시뮬레이션 결과 기록 | ☐ |
| 6-3 | 기본 전사 파티 승률 기준값 확정 | ☐ |
| 6-4 | 상태이상 없는 파티 vs 상태이상 파티 비교 | ☐ |
| 6-5 | 힐러 없는 파티 생존 한계 층 | ☐ |
| 6-6 | 듀얼 vs 단일 무기 DPS 비교 | ☐ |

**시뮬레이션 결과 기록란:**

| 항목 | 결과값 | 목표값 |
|------|--------|--------|
| 전사 파티 50층 승률 | — | — |
| 평균 전투 턴 수 | — | — |
| 전사×4 생존 한계 층 | — | — |
| 힐러 포함 50층 승률 | — | — |

> ⚠️ **B-1 (DoT 이중 처리) 수정 후 실행해야 의미 있는 결과 나옴**

---

## 7단계 — UI 연동 검증 (전투 로직 완료 후 진행)

| # | 항목 | 상태 |
|---|------|------|
| 7-1 | HP/MP 바 실시간 갱신 | ☐ |
| 7-2 | 상태이상 아이콘 부여/해제 표시 | ☐ |
| 7-3 | 데미지 숫자 표시 | ☐ |
| 7-4 | 턴 순서 / 액션 캐릭터 포커스 | ☐ |
| 7-5 | 파티 상태 패널 파티원 수 반응 | ☐ |
| 7-6 | StatRow ◆ 컬러 접두어 | ☐ |
| 7-7 | `selectedConsumableItem` 자동 연결 | ☐ 미완성 — 별도 구현 필요 |

---

## 8단계 — 미구현 시스템 구현

| # | 항목 | 우선순위 | 상태 |
|---|------|:---:|------|
| 8-1 | `SkillData.backlashChance` 등 필드 추가 | 1 | ☐ |
| 8-2 | `EquipmentData.magicAmplify / backlashSuppression` | 2 | ☐ |
| 8-3 | `PassiveData.backlashSuppression` | 3 | ☐ |
| 8-4 | `PlayerStats.TotalBacklashSuppression` | 4 | ☐ |
| 8-5 | BattleSystem 역효과 발동 로직 | 5 | ☐ |
| 8-6 | Mana Stabilize SO + 버프 턴 관리 | 6 | ☐ |
| 8-7 | 마법 장비 SO 일괄 제작 | 7 | ☐ |
| 8-8 | Bow 견제사격 + 인터셉트 타이밍 | 8 | ☐ |

---

## 발견된 버그/이슈 상세 기록

| 발견일 | 단계 | 버그 | 위치 | 수정 여부 |
|--------|------|------|------|-----------|
| 2026-03-30 | 3단계 | **DoT 이중 처리** — 적 상태이상 `ProcessStatusEffectsEndOfTurn` 2회 호출 | `BattleManager.cs` ProcessAllIgniteDamage() | ☐ 미수정 |
| 2026-03-30 | 2단계 | **스턴 무력화** — `ExecuteEnemyTurn`/`ExecuteAllyResolution`에 `IsStunned()` 체크 없음 | `BattleManager.cs` | ☐ 미수정 |
| 2026-03-30 | 성능 | **HP getter Debug.Log** — 매 HP 읽기마다 로그 출력 | `PlayerStats.currentHP` getter | ☐ 미수정 |
| 2026-03-30 | 성능 | **에디터 플레이 중 매 HP 변경마다 AssetDatabase.SaveAssets()** | `PlayerStats.currentHP/MP` setter | ☐ 미수정 |
| 2026-03-30 | 5단계 | **BattleSystem.cs 데드코드** — PlayerAttack/EnemyAttack 미사용 | `BattleSystem.cs` | ☐ 검토 필요 |
| 2026-03-30 | 0단계 | **동료 HP/MP 씬 전환 미보존** — statData 런타임 SO는 씬 파괴 시 소멸 | `GameManager.SaveFromPlayer` | ☐ Solo 모드에선 무관 |

---

## 수정 권장 우선순위

```
1순위 (전투 불가능 수준)
  → B-1: DoT 이중 처리 수정 (ProcessAllIgniteDamage 중복 루프 제거)
  → B-2/B-3: 스턴 체크 추가 (ExecuteEnemyTurn / ExecuteAllyResolution)

2순위 (전투 중 심각한 성능 저하)
  → B-4: currentHP getter Debug.Log 제거
  → B-5: HP setter AssetDatabase.SaveAssets() 에디터 플레이 중 호출 제거

3순위 (코드 정리)
  → B-6: BattleSystem.cs 용도 결정 (제거 또는 BattleManager로 통합)
  → 1-5: MemoryOfSpecies 3세트 런타임 트리거 구현
```

---

*코드 정적 분석 완료. 런타임 확인 항목은 Unity 에디터 플레이 모드에서 직접 검증 필요.*
*CLAUDE.md + PROGRESS.md와 함께 참조하세요.*
