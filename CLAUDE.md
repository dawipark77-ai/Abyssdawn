# CLAUDE.md — Abyssdawn 01

이 문서는 Claude(AI 코딩 어시스턴트)가 이 프로젝트를 이해하고 올바르게 지원하기 위한 컨텍스트 문서입니다.
코드 생성·수정 시 여기 정의된 패턴과 컨벤션을 따르세요.

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [게임 설계 — 핵심 콘셉트](#2-게임-설계--핵심-콘셉트)
3. [게임 설계 — 전투 시스템](#3-게임-설계--전투-시스템)
4. [게임 설계 — 캐릭터 빌드](#4-게임-설계--캐릭터-빌드)
5. [게임 설계 — 던전 구조](#5-게임-설계--던전-구조)
6. [게임 설계 — 기타 시스템](#6-게임-설계--기타-시스템)
7. [프로젝트 구조](#7-프로젝트-구조)
8. [주요 스크립트 역할](#8-주요-스크립트-역할)
9. [ScriptableObject 데이터 계층](#9-scriptableobject-데이터-계층)
10. [코딩 패턴 & 컨벤션](#10-코딩-패턴--컨벤션)
11. [씬 구성](#11-씬-구성)
12. [에디터 툴](#12-에디터-툴)

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 엔진 | Unity 6 |
| 렌더 파이프라인 | URP 2D (Universal Render Pipeline 17.2.0) |
| 장르 | 턴제 RPG 던전 크롤러 |
| 해상도 | 1920 × 1080 |
| 언어 | C# |
| 주요 namespace | `AbyssdawnBattle`, `Genesis01` |

**기본 게임 루프:** 던전 탐색(격자 이동) → 랜덤 인카운터(15%) → 턴제 전투 → 던전 복귀

---

## 2. 게임 설계 — 핵심 콘셉트

> **[이 섹션을 직접 채워주세요]**
>
> 예시 항목:
> - 게임의 핵심 테마/분위기
> - 목표 플레이타임 / 진행 구조 (몇 층 던전인지 등)
> - 게임의 차별점 / 핵심 재미 요소
> - 타겟 플레이어층

---

## 3. 게임 설계 — 전투 시스템

> **[이 섹션을 직접 채워주세요]**
>
> 현재 코드에서 파악된 내용 (확인 후 수정/보완):
> - 턴제 전투, 파티 vs 적
> - 전열(Front: Slot1~2) / 후열(Back: Slot3~4) 슬롯 시스템 (`SlotMask` Flags Enum)
> - 명중률: Agility 기반 계산
> - 크리티컬: Luck 기반 계산
> - 방어구 파괴(ArmorBreak) 시스템
> - 방패 블록(Block) 시스템
> - 상태이상(저주/Curse) 시스템 — `StatusEffectSO`
>
> 추가로 설명할 내용:
> - 턴 순서 결정 방식
> - 행동 종류(공격/스킬/방어/포션/도망 외 추가 행동)
> - 파티 최대 인원 및 구성 방식
> - 적 AI 패턴
> - 전투 보상 구조

---

## 4. 게임 설계 — 캐릭터 빌드

> **[이 섹션을 직접 채워주세요]**
>
> 현재 코드에서 파악된 내용 (확인 후 수정/보완):
>
> **스탯 계산 레이어 (스택 순서):**
> ```
> 최종 스탯 = 기본값 × 직업(CharacterClass) 배율
>           + 패시브(Passive) 보너스
>           + 장비(Equipment) 보너스
>           + 종의 기억(MemoryOfSpecies) 보너스
>           + 종의 특성(TraitsOfSpecies) 보너스  ← 3세트 발동 시
> ```
>
> **빌드 축:**
> - **직업(CharacterClass):** 스탯 배율 + HP/MP 배율 + 레벨당 성장치
> - **무기 Lore:** Sword/Dagger/Bow/Katana/Spear/Polearm/Dual/Combat Arts/Divine/Warfare
> - **Path:** Mage/Monk/Sister/Thief/Warrior
> - **Oath & Binding:** 서약/제약 (강도: Weak/Medium/Strong)
> - **종의 기억(MemoryOfSpecies):** Human/Elf/Dwarf/Orc/Halfling — 3개 세트 시 특성 발동
>
> 추가로 설명할 내용:
> - 직업 목록 및 각 직업 특성
> - 레벨업 방식
> - Oath & Binding의 실제 게임플레이 효과
> - 스킬 트리 전체 구조 (LP 소모 방식)
> - 종의 기억 세트 보너스 상세

---

## 5. 게임 설계 — 던전 구조

> **[이 섹션을 직접 채워주세요]**
>
> 현재 코드에서 파악된 내용 (확인 후 수정/보완):
> - 격자(Grid) 기반 이동 (WASD / 화살표키)
> - 절차적 생성 — 방(Room) + 미로 통로 + 안개
> - 다층 구조 (`DungeonPersistentData.currentFloor`)
> - 이동 시 15% 확률 랜덤 인카운터
> - 출구 도달 → 다음 층 생성
>
> 추가로 설명할 내용:
> - 총 층수 / 보스 층 배치
> - 던전 이벤트 종류 (전투 외 이벤트)
> - 상점, 휴식 지점, 트랩 등 특수 타일
> - 안개 탐색 규칙 (시야 범위 등)
> - Gemini AI가 생성하는 던전 이벤트/텍스트 범위

---

## 6. 게임 설계 — 기타 시스템

> **[이 섹션을 직접 채워주세요]**
>
> 항목 예시:
> - 인벤토리 / 장비 시스템 규칙
> - 소비 아이템(Consumable) 목록 및 효과
> - 저장/불러오기 방식
> - Dawn Chalice 아이템 (파란 테두리)의 특수 역할
> - 전투 리플레이 시스템 활용 방식 (F6/F7/F8)
> - 밸런스 시뮬레이터 활용 방침

---

## 7. 프로젝트 구조

```
Assets/
├── _Recovery/                      # 복구/백업 씬
├── Battle/Curse/                   # 저주 이미지 에셋
├── Data/                           # 기타 데이터
├── Editor/                         # 에디터 전용 스크립트 (최상위)
├── Enemies/                        # 적 이미지
├── Genesis 01/                     # 구 프로토타입 (보존)
│   └── Assets/Scripts/Dungeon/     # 초기 던전 프로토타입
├── Icons/                          # 아이템/장비 아이콘
├── Images/                         # 배경/UI/스킬/적 이미지
├── Items/Consumables/              # 소비 아이템 에셋
├── Prefab/
│   ├── Dungeon/                    # WallLinePrefab 등
│   ├── SkillListItem.prefab        # 스킬 목록 슬롯 UI
│   ├── StatRow_Prefab.prefab       # 스탯 비교 행 UI
│   └── TilePrefab.prefab           # 던전 타일
├── Resources/                      # Runtime 로드용 에셋
│   ├── Classes/                    # CharacterClass SO
│   ├── Item_Equipments/
│   │   ├── Equipments/Crude/       # 장비 SO
│   │   └── Items/                  # 아이템 SO
│   ├── Skills/                     # SkillData SO
│   ├── StatusEffects/              # StatusEffectSO (저주 등)
│   └── Traits/                     # TraitsOfSpeciesData SO
├── Scripts/
│   ├── Battle/
│   │   ├── Data/                   # SO 정의 스크립트
│   │   │   ├── Classes/
│   │   │   ├── CombatData/
│   │   │   ├── Curse/
│   │   │   ├── Enemies/
│   │   │   ├── EquipmentsSkills/
│   │   │   ├── MemoryofSpecies/    # Human/Elf/Dwarf/Orc/Halfling
│   │   │   ├── Oath&Path/          # OathBindingData, PathSkillData
│   │   │   ├── Skills/             # 무기 Lore별 스킬 데이터
│   │   │   └── TraitsofSpecies/
│   │   ├── Editor/                 # 전투 관련 에디터 툴
│   │   ├── Enemyimages/
│   │   ├── Images/
│   │   └── Prefabs/
│   ├── Dungeon/
│   │   ├── Editor/                 # 던전 에디터 툴
│   │   └── Prefab/
│   └── Editor/                     # 공용 에디터 툴
├── Settings/Scenes/                # URP 씬 템플릿
└── TextMesh Pro/
```

---

## 8. 주요 스크립트 역할

### 전역 매니저

| 스크립트 | 역할 |
|----------|------|
| `GameManager.cs` | 싱글톤. 파티 데이터(`staticPartyData`)를 씬 전환 간 유지. `DontDestroyOnLoad` |
| `DungeonPersistentData.cs` | static 클래스. 던전 씬 전환 시 플레이어 그리드 좌표, 안개 탐색 영역, HP/MP 보존 |
| `EncounterManager.cs` | 싱글톤. 던전 → 전투 씬 전환 트리거 |
| `GeminiAPIManager.cs` | 싱글톤 (`namespace Genesis01`). Google Gemini AI API 연동. `UnityWebRequest` 콜백 패턴 |

### 전투 시스템

| 스크립트 | 역할 |
|----------|------|
| `BattleManager.cs` | 전투 전체 흐름 제어. 파티 시스템, 스킬 실행, 턴 관리, 씬 복귀. (~4600줄) |
| `BattleSystem.cs` | 공격/방어/포션 계산 로직. 명중률(Agility), 크리티컬(Luck), 방어구 파괴 데미지 |
| `BattleUIManager.cs` | 전투 UI 패널 제어. MainMenu ↔ FightSubPanel. EventSystem 자동 생성. 확인 다이얼로그 동적 생성 |
| `BattleRecorder.cs` | 리플레이 시스템. F8 일시정지/재개, F6/F7 프레임 단위 되감기. 최대 3600프레임 원형 버퍼 |
| `BalanceSimulator.cs` | `[ContextMenu]` 밸런스 시뮬레이터. 50층 클리어·보스 난이도·승률 시뮬레이션 |

### 플레이어 스탯

| 스크립트 | 역할 |
|----------|------|
| `PlayerStats.cs` | 핵심 스탯 클래스. 스탯이 computed property로 실시간 계산. `static event OnStatusChanged` 발행 |
| `PlayerStatData.cs` | ScriptableObject 저장 금고. HP/MP/레벨/장비/스킬/종의 기억/특성 모두 보존. `OnValidate`에서 세트 효과 자동 처리 |
| `EnemyStats.cs` | 적 스탯. World-Space Canvas HP/MP UI 동적 생성. 히트 쉐이크 코루틴. 상태이상 인스턴스 관리 |

### 장비 & 인벤토리

| 스크립트 | 역할 |
|----------|------|
| `EquipmentManager.cs` | 장착/해제 로직. `PlayerStatData` 실시간 저장. 양손 무기 장착 시 왼손 슬롯 자동 해제 |
| `InventoryUIManager.cs` | 탭형 인벤토리 UI (All/Equipment/Consumable). 슬라이드 애니메이션. 장비 스탯 비교 행 동적 생성 |
| `InventorySlot.cs` | 슬롯 UI. 레어 아이템 골드 테두리. Dawn Chalice 파란 테두리 특수 처리 |
| `ConsumableInventory.cs` | 소비 아이템 인벤토리. `OnInventoryChanged` 이벤트 |

### 던전 시스템

| 스크립트 | 역할 |
|----------|------|
| `MapManager.cs` | 타일맵 기반 절차적 던전 생성. 방·미로 통로·출구·안개. `currentFloor` 기반 다층 생성 |
| `DungeonGridPlayer.cs` | 격자 이동(WASD/화살표). 벽 충돌. `revealedTiles` 안개 해제. 미니맵 커서. 출구 → 다음 층 생성 |
| `DungeonEncounter.cs` | 싱글톤. 이동마다 15% 확률 랜덤 인카운터. 현재 씬/위치 저장 후 전투 씬 로드 |
| `DungeonRoomGenerator.cs` | static 유틸리티. 방 내부 타일 클리어 |
| `DungeonMenuController.cs` | Status 패널 토글 |

### 스킬 트리

| 스크립트 | 역할 |
|----------|------|
| `SkillTreeNode.cs` | 스킬 트리 노드 1개. Locked/Available/Learned 상태 관리. `SkillData.prerequisiteSkills` 기반 선행 스킬 체크 |
| `SwordSkillTreeManager.cs` | Sword Lore 트리 매니저. LP 관리. 에디터 플레이모드 백업/복원 |
| `SkillDetailPopup.cs` | 스킬 상세 팝업. Learn 버튼. 배경 클릭/ESC 닫기 |
| `LoreTreePanelController.cs` | Lore 트리 패널 닫기 버튼 + ESC 키 |

---

## 9. ScriptableObject 데이터 계층

모든 게임 데이터는 ScriptableObject로 정의되며 `Resources/` 하위에 저장됩니다.
런타임에는 `Resources.LoadAll<T>()` 로 자동 로드합니다.

| SO 클래스 | CreateAssetMenu 경로 | 역할 |
|-----------|----------------------|------|
| `SkillData` | `Abyssdawn/Skill Data` | 스킬 전체 정의. targeting(SlotMask), 비용, 배율, effects 리스트, DunBreak 확률, 선행 스킬 트리 |
| `StatusEffectSO` | `Abyssdawn/Status Effect` | 상태이상(저주) 데이터. 물리/마법 별도 지속턴·데미지·부여확률 |
| `EquipmentData` | `Abyssdawn/Equipment Data` | 장비 정의. ArmorBreak, Block, weaponCurses, 방어구 카테고리(Light/Heavy) |
| `CharacterClass` | `Game/Character Class` | 직업. 스탯 보정, HP/MP 배율, 레벨당 성장치 |
| `MemoryOfSpeciesData` | `Abyssdawn/Memory of Species` | 종의 기억. 종족별 스탯 보정 + 레벨당 성장치 |
| `TraitsOfSpeciesData` | `Abyssdawn/Traits Of Species Data` | 종의 특성. 동종 기억 3개 세트 발동 보너스 |
| `OathBindingData` | `Abyssdawn/Oath & Binding` | 서약/제약. 직업별 강도(Weak/Medium/Strong) |
| `WeaponLoreSkillData` | `Abyssdawn/Weapon Lore Skill` | 무기 전용 Lore 스킬. 무기 카테고리별 분류 |
| `EnemyDatabase` | `Battle/EnemyDatabase` | 적 프리팹 풀. 랜덤 선택 |
| `ArmorBreakDataSO` | — | 방어구 파괴 계수 |
| `BlockDataSO` | — | 방패 블록 데이터 |
| `PassiveData` | — | 패시브 스킬 데이터 |
| `PlayerStatData` | — | 플레이어 런타임 저장 금고 (HP/MP/레벨/장비/스킬/기억/특성) |

---

## 10. 코딩 패턴 & 컨벤션

### ① 싱글톤 패턴

`GameManager`, `EncounterManager`, `DungeonEncounter`, `GeminiAPIManager` 모두 동일 패턴:

```csharp
private static T _instance;
public static T Instance
{
    get
    {
        if (_instance == null)
            _instance = FindFirstObjectByType<T>();
        return _instance;
    }
}

void Awake()
{
    if (_instance != null && _instance != this) { Destroy(gameObject); return; }
    _instance = this;
    DontDestroyOnLoad(gameObject); // GameManager만 해당
}
```

새 싱글톤을 만들 때 이 패턴을 따르세요. `FindObjectOfType` 대신 `FindFirstObjectByType`를 사용합니다(Unity 6).

### ② ScriptableObject 데이터 드리븐

- 모든 게임 데이터는 SO로 정의
- `Resources/` 하위에 위치시키고 `Resources.LoadAll<T>()` 로 런타임 로드
- `PlayerStatData` SO는 런타임 상태 저장소로도 활용 (HP/MP setter에서 SO에 직접 기록)
- `OnValidate()`에서 데이터 무결성 체크 및 자동 세팅

### ③ static 이벤트 패턴

```csharp
// PlayerStats.cs 패턴
public static event Action OnStatusChanged;
public void NotifyStatusChanged() => OnStatusChanged?.Invoke();
```

HP/MP/스탯 변경 시 UI 등 구독자에 방송. 새 UI 업데이트는 이 이벤트를 구독하세요.

### ④ Computed Property 스탯 계산

스탯을 필드로 저장하지 않고 property getter에서 실시간 계산합니다:

```csharp
public int maxHP => characterClass.GetFinalMaxHP(baseHP) + GetPassiveHPBonus() + GetEquipmentHPBonus();
public int Attack => characterClass.GetFinalAttack(baseAttack + allocatedPoints) + passiveBonus + equipmentBonus;
```

스탯 수정 시 반드시 `NotifyStatusChanged()`를 호출해 UI를 갱신하세요.

### ⑤ Flags Enum 비트 마스크 (슬롯 타겟팅)

```csharp
[System.Flags]
public enum SlotMask
{
    Slot1 = 1, Slot2 = 2, Slot3 = 4, Slot4 = 8,
    Front = Slot1 | Slot2,
    Back  = Slot3 | Slot4,
    Any   = Front | Back
}
```

스킬 타겟팅은 `allowedCasterSlots`, `allowedTargetSlots` 비트 연산으로 처리합니다.

### ⑥ static 전역 데이터 (씬 간 영속성)

씬 전환 시 오브젝트가 파괴되어도 static 필드는 메모리에 유지됩니다:

```csharp
// DungeonPersistentData.cs
public static class DungeonPersistentData
{
    public static Vector2Int playerGridPos;
    public static HashSet<Vector2Int> revealedTiles;
    public static int currentHP, currentMP, currentFloor;
}

// GameManager.cs
public static Dictionary<string, PartyMemberData> staticPartyData;
```

씬 전환 전에 반드시 여기에 저장, 씬 로드 후 여기서 복원하세요.

### ⑦ 레거시 마이그레이션 패턴

구버전 SO 필드를 신버전으로 자동 마이그레이션:

```csharp
[FormerlySerializedAs("effectType"), HideInInspector, SerializeField]
private EffectType legacyEffectType;

private void OnValidate()
{
    MigrateLegacyEffects(); // 구버전 단일 effect → List<SkillEffect>
}
```

SO 구조 변경 시 이 패턴으로 하위 호환성을 유지하세요.

### ⑧ World-Space UI 동적 생성

`EnemyStats.CreateWorldSpaceUI()`처럼 코드로 월드 공간 Canvas를 직접 생성합니다.
카메라 방향 정렬은 `BillboardSprite.cs` 컴포넌트로 처리합니다.

### ⑨ 에디터 전용 코드 분리

에디터 전용 로직은 반드시 `Assets/Scripts/*/Editor/` 폴더에 두거나 `#if UNITY_EDITOR` 블록으로 감싸세요:

```csharp
#if UNITY_EDITOR
[ContextMenu("Simulate Balance")]
private void SimulateBalance() { ... }
#endif
```

### ⑩ 세트 효과 패턴 (종의 기억)

`PlayerStatData.OnValidate()`에서 장착된 `MemoryOfSpeciesData`의 종족을 집계하고,
같은 종족 3개 시 해당 `TraitsOfSpeciesData`를 자동 활성화합니다.
세트 효과 로직을 추가할 때 이 패턴을 따르세요.

---

## 11. 씬 구성

| 씬 파일 | 용도 |
|---------|------|
| `Abyssdawn_Dungeon_2D 02~08.unity` | 던전 씬 (총 7개, 층별) |
| `Abyssdawn_Battle 01.unity` | 현재 전투 씬 |
| `Abyysborn_Battle 01.unity` | 구버전 전투 씬 (보존) |
| `Scenes/SampleScene.unity` | 기본 테스트 씬 |
| `Settings/Scenes/URP2DSceneTemplate.unity` | URP 씬 템플릿 |
| `_Recovery/0.unity`, `0 (1).unity` | 복구/백업 씬 |

**씬 전환 흐름:**
```
던전 씬 (Dungeon 2D 02~) 
  → [DungeonEncounter: 15% 인카운터]
  → 전투 씬 (Battle 01)
  → [BattleManager: 전투 종료]
  → 원래 던전 씬 복귀
```

---

## 12. 에디터 툴

프로젝트에는 반복 작업 자동화를 위한 에디터 전용 도구가 다수 있습니다.

| 툴 | 위치 | 기능 |
|----|------|------|
| `SkillAssetGenerator` | `Scripts/Battle/Editor/` | SkillData SO 일괄 생성 |
| `EquipmentDataEditor` | `Scripts/Battle/Editor/` | 장비 데이터 커스텀 인스펙터 |
| `InventoryUIBuilder` | `Scripts/Battle/Editor/` | 인벤토리 UI 자동 구성 |
| `ConsumableItemGenerator` | `Scripts/Battle/Editor/` | 소비 아이템 SO 일괄 생성 |
| `CharacterClassSetupTool` | `Scripts/Editor/` | CharacterClass 데이터 셋업 |
| `DungeonSetupTool` | `Scripts/Dungeon/Editor/` | 던전 씬 자동 셋업 |
| `DungeonScene03Fixer` | `Scripts/Dungeon/Editor/` | 던전 씬 수동 수정 툴 |
| `BalanceSimulator` | `Scripts/Battle/` | `[ContextMenu]` 전투 밸런스 시뮬레이션 |

새 SO 타입을 추가할 때는 대응하는 에디터 Generator 툴도 함께 만드는 것을 권장합니다.

---

## 사용 중인 Unity 패키지

| 패키지 | 버전 | 용도 |
|--------|------|------|
| `com.unity.render-pipelines.universal` | 17.2.0 | URP 렌더링 |
| `com.unity.inputsystem` | 1.14.2 | 새 입력 시스템 |
| `com.unity.ugui` | 2.0.0 | UI Toolkit |
| `com.unity.2d.animation` | 12.0.2 | 2D 애니메이션 |
| `com.unity.2d.tilemap` + extras | 1.0.0 / 5.0.1 | 던전 타일맵 |
| `com.unity.2d.aseprite` | 2.0.2 | Aseprite 임포트 |
| `com.unity.timeline` | 1.8.9 | 타임라인 |
| `com.unity.visualscripting` | 1.9.8 | 비주얼 스크립팅 |
| `com.unity.test-framework` | 1.6.0 | 유닛 테스트 |

---

*최종 수정: 2026-03-21*
