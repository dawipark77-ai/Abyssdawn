# 전열/후열 슬롯 시스템 설계 문서

## 📋 개요
다키스트 던전 스타일의 전열/후열 슬롯 시스템 구현 계획
- **1인칭 시점**: 플레이어는 항상 왼쪽, 적은 오른쪽
- **슬롯 번호**: 화면 기준 좌→우 순서로 1, 2, 3, 4
- **전열/후열**: 슬롯 1,2 = 전열(Front Row), 슬롯 3,4 = 후열(Back Row)

---

## 🎯 핵심 설계 원칙

### 1. 슬롯 기반 위치 시스템
```
[플레이어 측]          [적 측]
┌────┬────┬────┬────┐ ┌────┬────┬────┬────┐
│ 1  │ 2  │ 3  │ 4  │ │ 1  │ 2  │ 3  │ 4  │
│전열│전열│후열│후열│ │전열│전열│후열│후열│
└────┴────┴────┴────┘ └────┴────┴────┴────┘
```

### 2. 슬롯 번호 규칙
- **1번 슬롯**: 가장 왼쪽 (전열)
- **2번 슬롯**: 왼쪽에서 두 번째 (전열)
- **3번 슬롯**: 왼쪽에서 세 번째 (후열)
- **4번 슬롯**: 가장 오른쪽 (후열)

### 3. 전열/후열 구분
- **전열 (Front Row)**: 슬롯 1, 2
- **후열 (Back Row)**: 슬롯 3, 4

---

## 🏗️ 시스템 구조

### 1. 데이터 구조

#### `BattlePosition.cs` (새로 생성)
```csharp
public enum BattleSlot { None = 0, Slot1 = 1, Slot2 = 2, Slot3 = 3, Slot4 = 4 }

public static class BattlePositionHelper
{
    public static bool IsFrontRow(BattleSlot slot) => slot == BattleSlot.Slot1 || slot == BattleSlot.Slot2;
    public static bool IsBackRow(BattleSlot slot) => slot == BattleSlot.Slot3 || slot == BattleSlot.Slot4;
    public static int GetSlotIndex(BattleSlot slot) => (int)slot;
}
```

#### `PlayerStats.cs` 수정
```csharp
// 기존: public bool isFrontRow = true;
// 변경: 
[Header("5. 전투 포지션 (슬롯 시스템)")]
[Tooltip("현재 캐릭터가 위치한 슬롯 (1-4)")]
public BattleSlot currentSlot = BattleSlot.Slot1;

// 프로퍼티 추가
public bool IsFrontRow => BattlePositionHelper.IsFrontRow(currentSlot);
public bool IsBackRow => BattlePositionHelper.IsBackRow(currentSlot);
public int SlotIndex => BattlePositionHelper.GetSlotIndex(currentSlot);
```

#### `EnemyStats.cs` 수정
```csharp
[Header("Battle Position")]
[Tooltip("현재 적이 위치한 슬롯 (1-4)")]
public BattleSlot currentSlot = BattleSlot.Slot1;

// 프로퍼티 추가
public bool IsFrontRow => BattlePositionHelper.IsFrontRow(currentSlot);
public bool IsBackRow => BattlePositionHelper.IsBackRow(currentSlot);
public int SlotIndex => BattlePositionHelper.GetSlotIndex(currentSlot);
```

---

### 2. 슬롯 관리 시스템

#### `BattleSlotManager.cs` (새로 생성)
```csharp
public class BattleSlotManager : MonoBehaviour
{
    // 플레이어 슬롯 (4개 고정)
    private PlayerStats[] playerSlots = new PlayerStats[4];
    
    // 적 슬롯 (4개 고정, 동적 할당)
    private EnemyStats[] enemySlots = new EnemyStats[4];
    
    // 슬롯 위치 Transform (시각적 배치용)
    public Transform[] playerSlotPositions = new Transform[4];
    public Transform[] enemySlotPositions = new Transform[4];
    
    // 슬롯 할당/해제
    public bool AssignPlayerToSlot(PlayerStats player, BattleSlot slot);
    public bool AssignEnemyToSlot(EnemyStats enemy, BattleSlot slot);
    public void RemoveFromSlot(BattleSlot slot, bool isPlayer);
    
    // 슬롯 조회
    public PlayerStats GetPlayerAtSlot(BattleSlot slot);
    public EnemyStats GetEnemyAtSlot(BattleSlot slot);
    public List<PlayerStats> GetPlayersInFrontRow();
    public List<EnemyStats> GetEnemiesInFrontRow();
    
    // 위치 변경
    public bool SwapSlots(BattleSlot slot1, BattleSlot slot2, bool isPlayer);
    public bool MoveToSlot(BattleSlot fromSlot, BattleSlot toSlot, bool isPlayer);
}
```

---

### 3. 전투 시작 시 슬롯 초기화

#### `BattleManager.cs` 수정
```csharp
private BattleSlotManager slotManager;

void StartBattle()
{
    // ... 기존 코드 ...
    
    // 슬롯 시스템 초기화
    InitializeSlotSystem();
    
    // ... 기존 코드 ...
}

private void InitializeSlotSystem()
{
    // 플레이어 슬롯 할당 (순서대로 1,2,3,4)
    for (int i = 0; i < activePartyMembers.Count && i < 4; i++)
    {
        BattleSlot slot = (BattleSlot)(i + 1);
        slotManager.AssignPlayerToSlot(activePartyMembers[i], slot);
    }
    
    // 적 슬롯 할당 (스폰 순서대로 1,2,3,4)
    for (int i = 0; i < spawnedEnemyStats.Count && i < 4; i++)
    {
        BattleSlot slot = (BattleSlot)(i + 1);
        slotManager.AssignEnemyToSlot(spawnedEnemyStats[i], slot);
    }
}
```

---

### 4. 스킬 타겟팅 시스템

#### `SkillData.cs` 확장 (선택사항)
```csharp
[System.Serializable]
public class SkillTargeting
{
    [Header("타겟팅 타입")]
    public TargetType targetType = TargetType.SingleEnemy;
    
    [Header("슬롯 타겟팅")]
    [Tooltip("타겟 슬롯 범위 (예: 전열 1,2 = Slot1, Slot2)")]
    public List<BattleSlot> targetSlots = new List<BattleSlot>();
    
    [Tooltip("전열만 타겟 (자동으로 Slot1, Slot2 추가)")]
    public bool targetsFrontRowOnly = false;
    
    [Tooltip("후열만 타겟 (자동으로 Slot3, Slot4 추가)")]
    public bool targetsBackRowOnly = false;
}

public enum TargetType
{
    SingleEnemy,        // 단일 적
    MultipleEnemies,    // 다중 적 (슬롯 지정)
    Self,               // 자신
    Ally,               // 아군
    AllEnemies          // 모든 적
}
```

#### `BattleManager.cs` 스킬 실행 수정
```csharp
private IEnumerator ExecuteMandritto(PlayerStats attacker, SkillData skill)
{
    // 기존: List<EnemyStats> frontRowEnemies = ...
    // 변경:
    List<EnemyStats> targets = slotManager.GetEnemiesInSlots(
        new List<BattleSlot> { BattleSlot.Slot1, BattleSlot.Slot2 }
    );
    
    foreach (var target in targets)
    {
        yield return StartCoroutine(ExecuteSingleTargetSkill(attacker, skill, target));
    }
}
```

---

### 5. 위치 변경 시스템

#### 위치 변경 스킬 예시
```csharp
// 예: "Shuffle" 스킬 - 아군 위치를 무작위로 섞음
private IEnumerator ExecuteShuffle(PlayerStats caster, SkillData skill)
{
    List<BattleSlot> occupiedSlots = new List<BattleSlot>();
    List<PlayerStats> players = new List<PlayerStats>();
    
    // 현재 슬롯에 있는 플레이어 수집
    for (int i = 1; i <= 4; i++)
    {
        BattleSlot slot = (BattleSlot)i;
        PlayerStats player = slotManager.GetPlayerAtSlot(slot);
        if (player != null)
        {
            occupiedSlots.Add(slot);
            players.Add(player);
        }
    }
    
    // 무작위로 섞기
    for (int i = 0; i < players.Count; i++)
    {
        int randomIndex = Random.Range(0, occupiedSlots.Count);
        BattleSlot newSlot = occupiedSlots[randomIndex];
        occupiedSlots.RemoveAt(randomIndex);
        
        slotManager.MoveToSlot(players[i].currentSlot, newSlot, true);
    }
    
    yield return new WaitForSeconds(0.5f);
}
```

---

### 6. UI 표시

#### 슬롯 표시 UI (선택사항)
- 각 슬롯 위치에 번호 표시 (1, 2, 3, 4)
- 전열/후열 구분선 표시
- 현재 선택된 슬롯 하이라이트
- 빈 슬롯 표시 (회색 배경)

---

## 🔄 마이그레이션 계획

### Phase 1: 기본 구조 구축
1. ✅ `BattleSlot` enum 생성
2. ✅ `BattlePositionHelper` 유틸리티 클래스 생성
3. ✅ `BattleSlotManager` 기본 구조 생성
4. ✅ `PlayerStats`, `EnemyStats`에 `currentSlot` 필드 추가

### Phase 2: 슬롯 할당 시스템
1. ✅ `BattleManager`에서 전투 시작 시 슬롯 자동 할당
2. ✅ 기존 `PositionPartyMembers()`를 슬롯 기반으로 변경
3. ✅ 적 스폰 시 슬롯 할당

### Phase 3: 스킬 타겟팅 통합
1. ✅ `Mandritto` 스킬을 슬롯 기반 타겟팅으로 변경
2. ✅ 기존 `IsFrontRowEnemy()` 로직을 슬롯 기반으로 변경
3. ✅ 다른 전열/후열 스킬들 통합

### Phase 4: 위치 변경 기능
1. ✅ 위치 변경 스킬 구현 (Shuffle, Push, Pull 등)
2. ✅ 위치 변경 애니메이션
3. ✅ 위치 변경 시 스탯 보정 (예: 후열에서 전열로 이동 시)

### Phase 5: UI 개선
1. ✅ 슬롯 번호 표시
2. ✅ 전열/후열 구분선
3. ✅ 슬롯 선택 UI (스킬 사용 시)

---

## 📝 구현 우선순위

### 🔴 필수 (Phase 1-2)
- [ ] `BattleSlot` enum 및 헬퍼 클래스
- [ ] `BattleSlotManager` 기본 구조
- [ ] `PlayerStats`, `EnemyStats` 슬롯 필드 추가
- [ ] 전투 시작 시 슬롯 자동 할당
- [ ] 기존 `isFrontRow` 로직을 슬롯 기반으로 마이그레이션

### 🟡 중요 (Phase 3)
- [ ] `Mandritto` 스킬 슬롯 기반 타겟팅
- [ ] 전열/후열 스킬들 통합
- [ ] 슬롯 기반 타겟팅 헬퍼 메서드

### 🟢 선택 (Phase 4-5)
- [ ] 위치 변경 스킬 구현
- [ ] 슬롯 UI 표시
- [ ] 위치 변경 애니메이션

---

## 🎮 사용 예시

### 스킬 타겟팅 예시
```csharp
// Mandritto: 전열 적 1,2번 슬롯 동시 공격
List<EnemyStats> targets = slotManager.GetEnemiesInSlots(
    new List<BattleSlot> { BattleSlot.Slot1, BattleSlot.Slot2 }
);

// Backstab: 후열 적 3,4번 슬롯 공격
List<EnemyStats> targets = slotManager.GetEnemiesInSlots(
    new List<BattleSlot> { BattleSlot.Slot3, BattleSlot.Slot4 }
);

// Charge: 자신을 전열로 이동
if (attacker.IsBackRow)
{
    BattleSlot frontSlot = FindEmptyFrontSlot();
    slotManager.MoveToSlot(attacker.currentSlot, frontSlot, true);
}
```

---

## ⚠️ 주의사항

1. **기존 코드 호환성**: `isFrontRow` 플래그는 `currentSlot` 기반으로 자동 계산되도록 유지
2. **슬롯 제한**: 플레이어/적 모두 최대 4명까지만 슬롯 할당 가능
3. **빈 슬롯 처리**: 슬롯이 비어있을 때의 스킬 타겟팅 로직 고려
4. **위치 변경 제약**: 일부 스킬은 특정 위치에서만 사용 가능 (예: 전열에서만 사용 가능한 스킬)

---

## 📚 참고 자료
- 다키스트 던전 포지션 시스템
- 턴제 RPG 전투 시스템 설계 패턴





