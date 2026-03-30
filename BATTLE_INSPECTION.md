# 전투 시스템 점검 체크리스트

> 작성: 2026-03-30
> 범례: `[ ]` 미확인 | `[x]` 통과 | `[!]` 버그/문제 | `[?]` 런타임 확인 필요
> Unity 로그 경로: `C:\Users\User\AppData\Local\Unity\Editor\Editor.log`

---

## 긴급 버그 (수정 전 전투 정상 동작 불가)

- `[!]` **B-1** `ProcessAllIgniteDamage()` — 적 DoT가 매 라운드 **2번** 처리됨 (이중 루프)
- `[!]` **B-2** `ExecuteEnemyTurn()` — 스턴된 **적**이 그대로 행동함 (`IsStunned()` 체크 없음)
- `[!]` **B-3** `ExecuteAllyResolution()` — 스턴된 **아군**이 그대로 행동함 (`IsStunned()` 체크 없음)
- `[!]` **B-4** `PlayerStats.currentHP` getter — 매 HP 읽기마다 `Debug.Log` → 콘솔 폭주
- `[!]` **B-5** `PlayerStats.currentHP/MP` setter — 매 변경마다 `AssetDatabase.SaveAssets()` → 전투 중 히치
- `[?]` **B-6** `BattleSystem.cs` — `PlayerAttack/EnemyAttack` 미호출 (데드코드 여부 확인 필요)
- `[?]` **B-7** 동료 HP/MP 씬 전환 미보존 — Solo 모드에선 무관, Full Party 전환 시 문제

---

## 0단계 — 씬 영속성 (선행 필수)

- `[ ]` **0-1** 던전 → 전투 씬 전환 시 `staticPartyData` 유지되는가
- `[ ]` **0-2** 전투 종료 → 던전 복귀 시 HP/MP 올바르게 반영되는가
- `[ ]` **0-3** `DungeonPersistentData.currentHP/MP` 저장/복원 올바른가
- `[ ]` **0-4** 전투 중 전원 사망 시 씬 전환이 올바르게 처리되는가
- `[ ]` **0-5** 여러 번 인카운터 반복해도 데이터 누적 오염 없는가

---

## 1단계 — 캐릭터 스탯 계산

- `[x]` **1-1** 기본값 × `CharacterClass` 배율 계산 (computed property 확인)
- `[x]` **1-2** 장비 장착/해제 시 스탯 즉시 반영
- `[x]` **1-3** 패시브 보너스 스탯 반영 (단, GetPassiveBonus 내부 Debug.Log 다수 — B-4와 별개로 성능 주의)
- `[x]` **1-4** `MemoryOfSpecies` 개별 보너스 반영
- `[?]` **1-5** `MemoryOfSpecies` 동종 3세트 → `TraitsOfSpecies` 런타임 발동 여부 (`OnValidate` 에디터 전용 우려)
- `[x]` **1-6** 레벨 성장치 누적 적용
- `[x]` **1-7** `maxHP/MP` computed property 전투 시작 시 정확도
- `[x]` **1-8** 양손 무기 장착 시 왼손 자동 해제 후 스탯 반영

---

## 2단계 — 턴 구조

- `[x]` **2-1** Agility 내림차순으로 턴 순서 구성
- `[x]` **2-2** 동일 Agility 시 아군 먼저 (LINQ stable sort)
- `[?]` **2-3** 아군/적 교대 vs 민첩 단일 정렬 — **의도된 설계인지 확인 필요** (현재: 민첩 단일 정렬, 교대 없음)
- `[x]` **2-4** 전열(Slot1~2) / 후열(Slot3~4) 슬롯 구조 정상
- `[!]` **2-5** 스턴 시 행동 불가 — **미작동** → B-2/B-3
- `[x]` **2-6** 캐릭터 사망 시 다음 라운드 턴 큐에서 제거
- `[x]` **2-7** 전투 종료 조건 (전원 사망) 발동
- `[x]` **2-8** `battleEnded` 플래그 중복 처리 방지

---

## 3단계 — 상태이상

- `[x]` **3-1** 부여 확률 `physicalApplyChance` 롤 체크
- `[!]` **3-2** 적 DoT 매 턴 처리 — **2배 적용** → B-1
- `[x]` **3-3** 아군 DoT 매 턴 처리 (아군 루프는 1번)
- `[x]` **3-4** 지속 턴 만료 시 자동 해제
- `[x]` **3-5** 중복 부여 시 긴 쪽 지속턴 유지 (갱신 방식)
- `[!]` **3-6** 스턴 턴 만료 자동 해제는 OK, 실행 중 행동 차단 없음 → B-2/B-3
- `[ ]` **3-7** 상태이상 아이콘 World-Space UI 표시 (런타임 확인)
- `[ ]` **3-8** `flatIcon` 전투 화면에 올바르게 표시 (런타임 확인)

---

## 4단계 — 스킬

- `[x]` **4-1** MP 소모 스킬 사용 시 MP 차감
- `[x]` **4-2** MP 부족 시 스킬 차단
- `[x]` **4-3** 스킬 배율(`multiplier`) 데미지 반영
- `[x]` **4-4** `allowedCasterSlots` 포지션 제한
- `[x]` **4-5** `allowedTargetSlots` 타겟 제한
- `[ ]` **4-6** 다중 타격 스킬 타수 (런타임 확인)
- `[x]` **4-7** 듀얼 장착 시 0.35~0.45 배율 적용 (2타 각각 랜덤)
- `[x]` **4-8** 선행 스킬 미충족 시 잠금
- `[x]` **4-9** `DunBreak` 확률 적용
- `[x]` **4-10** 전투 중 소비 아이템 `ConsumableInventory.UseItem()` 호출 및 수량 차감
- `[x]` **4-11** 힐량 `hpRecoveryPercent × maxHP` 계산
- `[?]` **4-12** 아이템 버튼 Hero 전용 (`isHero`) — 동료 사용 불가, 의도된 설계인지 확인
- `[!]` **4-13** `selectedConsumableItem` Inspector 수동 연결 — 인벤토리 UI 자동화 미완성

---

## 5단계 — 특수 시스템

- `[x]` **5-1** `ArmorBreak` 방어력 누적 감소
- `[x]` **5-2** 방어력 0 이하 방지
- `[x]` **5-3** `ArmorBreak` 크리티컬 1.5× 적용
- `[x]` **5-4** 블록 확률 계산
- `[x]` **5-5** 블록 발동 시 데미지 감소
- `[x]` **5-6** 방어 행동 `defenceReduction 0.5f` 적용
- `[ ]` **5-7** 명중 계산 공식 실제 수치 (런타임 로그 확인)
- `[ ]` **5-8** 명중률 최소 10% / 최대 100% 캡 (런타임 확인)
- `[ ]` **5-9** 크리티컬 공식 실제 수치 (런타임 로그 확인)
- `[ ]` **5-10** `BattleRecorder` F6/F7/F8 동작 (런타임 확인)
- `[!]` **5-11** `BattleSystem.cs` 데드코드 — `PlayerAttack/EnemyAttack` 미호출 확인 필요

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

## 7단계 — UI 연동 (로직 완료 후 진행)

- `[ ]` **7-1** HP/MP 바 피해/회복 시 실시간 갱신
- `[ ]` **7-2** 상태이상 아이콘 부여/해제 표시
- `[ ]` **7-3** 데미지 숫자 표시
- `[ ]` **7-4** 턴 순서 / 현재 액션 캐릭터 포커스
- `[ ]` **7-5** 파티 상태 패널 파티원 수에 맞게 조정
- `[ ]` **7-6** StatRow ◆ 컬러 접두어 올바르게 표시
- `[!]` **7-7** `selectedConsumableItem` 인벤토리 선택 시 자동 설정 (미구현)

---

## 8단계 — 미구현 시스템

- `[ ]` **8-1** `SkillData.backlashChance` 등 Backlash 필드 추가
- `[ ]` **8-2** `EquipmentData.magicAmplify / backlashSuppression` 추가
- `[ ]` **8-3** `PassiveData.backlashSuppression` 추가
- `[ ]` **8-4** `PlayerStats.TotalBacklashSuppression` 계산
- `[ ]` **8-5** BattleSystem 역효과 발동 로직
- `[ ]` **8-6** Mana Stabilize SO + 버프 턴 관리
- `[ ]` **8-7** 마법 장비 SO 일괄 제작
- `[ ]` **8-8** Bow 견제사격 + 인터셉트 타이밍

---

## 수정 권장 순서

```
1순위 — 전투 동작 자체가 깨짐
  [!] B-1  DoT 이중처리 수정
  [!] B-2/3 스턴 체크 추가

2순위 — 에디터 성능 심각
  [!] B-4  currentHP getter Debug.Log 제거
  [!] B-5  HP setter AssetDatabase.SaveAssets() 제거

3순위 — 코드 정리
  [?] B-6  BattleSystem.cs 데드코드 정리
  [?] 1-5  MemoryOfSpecies 3세트 런타임 트리거 구현
```

---

*Unity 로그: `C:\Users\User\AppData\Local\Unity\Editor\Editor.log`*
*테스트 실행 후 로그 분석으로 `[ ]` 항목 자동 체크 진행*
