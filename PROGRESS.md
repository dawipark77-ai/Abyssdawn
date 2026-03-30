# Abyssdawn 01 — 개발 진행상황 전체 기록

> 최종 업데이트: 2026-03-30

---

## 목차

1. [프로젝트 기본 정보](#1-프로젝트-기본-정보)
2. [전체 커밋 이력](#2-전체-커밋-이력)
3. [구현 완료된 시스템](#3-구현-완료된-시스템)
4. [데이터 구조 & 스탯 계산](#4-데이터-구조--스탯-계산)
5. [ScriptableObject 목록](#5-scriptableobject-목록)
6. [씬 구성 & 전환 흐름](#6-씬-구성--전환-흐름)
7. [에디터 툴 목록](#7-에디터-툴-목록)
8. [소비 아이템 SO 목록](#8-소비-아이템-so-목록)
9. [장비 SO 목록 (Crude 티어)](#9-장비-so-목록-crude-티어)
10. [기획 확정 / 미구현 시스템](#10-기획-확정--미구현-시스템)
11. [작업 규칙](#11-작업-규칙)

---

## 1. 프로젝트 기본 정보

| 항목 | 내용 |
|------|------|
| 엔진 | Unity 6 |
| 렌더 파이프라인 | URP 2D (Universal Render Pipeline 17.2.0) |
| 장르 | 턴제 RPG 던전 크롤러 |
| 해상도 | 1920 × 1080 |
| 언어 | C# |
| 주요 네임스페이스 | `AbyssdawnBattle`, `Genesis01` |
| 메인 저장소 | `E:\Dawi\My Programs\Game Maker\Unity\Projects\Abyssdawn 01` |
| 현재 브랜치 | `main` |

**기본 게임 루프:**
```
던전 격자 이동 → 이동 시 15% 랜덤 인카운터 → 턴제 전투 → 던전 복귀
```

---

## 2. 전체 커밋 이력

| 커밋 해시 | 내용 |
|-----------|------|
| `d4f6c29` | Lore 카테고리 UI 스크롤/탭 시스템 추가 + CLAUDE.md 업데이트 |
| `4a1980e` | 새 SkillTree UI 시스템 추가 (NewSkillTreeUI, NewSkillTreeNode, NewSkillDetailPopup, 에디터 셋업) |
| `418944b` | ConsumableInventory → 전투 포션 시스템 연동 |
| `d24481a` | InventoryUIManager 무기 상세 UI 정비 |
| `0cdaf31` | ArmorBreakDataSO / BlockDataSO에 itemIcon/flatIcon 필드 추가, Flaticon 이미지 에셋 추가 |
| `b9b12fe` | StatusEffect 프리팹 이미지 로드 오류 수정 (자식 이름 오타 수정) |
| `4c9e082` | 에셋 업데이트, Curse SO → Resources 마이그레이션, 인벤토리 UI 정비 |
| `a0777fe` | 백업: 던전 씬, 장비 에셋, 프리팹, 신규 이미지 리소스 |
| `45ab0a2` | 백업: InventoryUIManager 스탯 비교 UI + StatusEffect 프리팹 수정 |
| `f5d408b` | Merge claude/reverent-chaum: Charges/Economy 섹션 충돌 해소 |
| `7023b20` | MainStatText, StatusEffectRow, PriceText UI 추가 + InventoryUISetupTool |
| `abd9054` | CLAUDE.md 추가, 소비 아이템 충전 시스템, 영어 UI 로컬라이제이션 |
| `693037f` | StatRow 프리팹, CrudeSword 아이콘, 인벤토리 UI 업데이트, 씬, 폰트 폴백 |
| `1e2437e` | 씬, 폰트 폴백, 에디터 스크립트 메타, 작업 노트 업데이트 |
| `1f38088` | 인벤토리 시스템: Resources.LoadAll 마이그레이션, UI 수정, 영어 UI 로컬라이제이션 전체 |
| `a96023f` | 인벤토리 UI 시스템 및 소비 아이템 SO 데이터 추가 |
| `26b7642` | StatusEffectSO 아이콘을 itemIcon/flatIcon으로 분리 |
| `afe4abb` | CurseData → StatusEffectSO 마이그레이션, 레거시 참조 모두 수정 |
| `c45621b` | ArmorBreak SO, Block SO, 무기 상태이상 목록, 장비 에디터 추가 |
| `85ce2a0` | 전투 시스템 확장: 방어구 파괴, 이중 무장, 상태이상, 스킬 SO |

---

## 3. 구현 완료된 시스템

### 3-1. 전투 시스템

| 스크립트 | 상태 | 내용 |
|----------|------|------|
| `BattleManager.cs` | 완성 (~4600줄) | 턴제 전투 전체 흐름, 파티 슬롯(전열/후열), 스킬 실행, 턴 관리, 씬 복귀 |
| `BattleSystem.cs` | 완성 | 명중률(Agility), 크리티컬(Luck), 방어구 파괴(ArmorBreak), 블록 계산 |
| `BattleUIManager.cs` | 완성 | MainMenu ↔ FightSubPanel 전환, EventSystem 자동 생성, 확인 다이얼로그 동적 생성 |
| `BattleRecorder.cs` | 완성 | F6/F7/F8 리플레이 시스템 (최대 3600프레임 원형 버퍼) |
| `BattleRecorder.cs` | 완성 | F8 일시정지/재개, F6/F7 프레임 단위 되감기 |
| `BalanceSimulator.cs` | 완성 | `[ContextMenu]` 50층 밸런스 시뮬레이션 |
| `Character.cs` | 완성 | 전투 내 캐릭터 데이터 래퍼 |

### 3-2. 상태이상(저주/Curse) 시스템

| 항목 | 상태 | 내용 |
|------|------|------|
| `StatusEffectSO` | 완성 | 물리/마법 별도 지속턴·데미지·부여확률 |
| Ignite | 완성 | 화염 지속 데미지 |
| Poison | 완성 | 독 지속 데미지 |
| Bleed (Dagger) | 완성 | 단검 출혈 |
| Bleed (Katana) | 완성 | 카타나 출혈 |
| Stun | 완성 | 행동 불가 |
| 아이콘 구조 | 완성 | `itemIcon` (인벤토리용) / `flatIcon` (전투 UI용) 이중 구조 |

### 3-3. 장비 & 인벤토리 시스템

| 스크립트 | 상태 | 내용 |
|----------|------|------|
| `EquipmentManager.cs` | 완성 | 장착/해제, 양손 무기 장착 시 왼손 슬롯 자동 해제 |
| `InventoryUIManager.cs` | 완성 | 탭(All/Equipment/Consumable), 슬라이드 애니메이션 |
| `InventoryUIManager.cs` | 완성 | StatRow ◆ 컬러 접두어 (일반=회색, 특수=골드) |
| `InventoryUIManager.cs` | 완성 | StatusEffectRow 오렌지 ◆ 접두어 |
| `InventoryUIManager.cs` | 완성 | ArmorBreak/Block SO `itemIcon`/`flatIcon` 이중 아이콘 표시 |
| `InventorySlot.cs` | 완성 | 레어 아이템 골드 테두리, Dawn Chalice 파란 테두리 |
| `ConsumableInventory.cs` | 완성 | 싱글톤, 소비 아이템 수량 관리, stackGroup 합산 제한 |
| `ConsumableInventory.cs` | 완성 | Dawn Chalice 특수 충전 처리, `OnInventoryChanged` 이벤트 |

### 3-4. 소비 아이템 → 전투 연동

| 항목 | 상태 | 내용 |
|------|------|------|
| `ConsumableInventory.Instance.UseItem(item)` | 완성 | 전투 중 아이템 사용 |
| `BattleManager.selectedConsumableItem` | 완성 | 선택된 아이템 필드 |
| `AllyCommand.usedItem` | 완성 | 커맨드에 아이템 포함 |
| 힐량 계산 | 완성 | SO의 `hpRecoveryPercent × maxHP` 기반 |
| Inspector 수동 연결 → 자동화 | **미완성** | 전투 중 아이템 선택 UI 자동 설정 필요 |

### 3-5. 스킬 시스템

| 항목 | 상태 | 내용 |
|------|------|------|
| `SkillData SO` | 완성 | targeting(SlotMask), 비용, 배율, effects 리스트, DunBreak 확률, 선행 스킬 |
| `SkillTreeNode.cs` | 완성 | Locked/Available/Learned 상태, 선행 스킬 체크 |
| `SwordSkillTreeManager.cs` | 완성 | Sword Lore 트리 전체 관리, LP 관리 |
| `NewSkillTreeUI.cs` | 완성 | 카테고리 페이저 방식, `PlayerStatData` 직접 읽기/쓰기 |
| `NewSkillTreeNode.cs` | 완성 | 새 UI용 노드 |
| `NewSkillDetailPopup.cs` | 완성 | 스킬 상세 팝업, Learn 버튼, 배경 클릭/ESC 닫기 |
| 다른 Lore/Path 트리 | **미완성** | Sword 외 나머지 트리 미구현 |

### 3-6. Lore 카테고리 UI 시스템

| 스크립트 | 상태 | 내용 |
|----------|------|------|
| `LoreCategoryScroller.cs` | 완성 | 카테고리 아이콘 띠 BtnTreePrev/BtnTreeNext 한 칸씩 스크롤 |
| `LoreCategoryScroller.cs` | 완성 | Viewport+Content+Mask 구조, 첫/마지막 아이콘 양끝 정렬 |
| `LoreCategoryTabController.cs` | 완성 | WEAPONARY/UTILITY/Arcane Mystery 탭 전환 |
| `LoreCategoryTabController.cs` | 완성 | 클릭 시 해당 Viewport만 열고 나머지 닫음, 토글 지원 |
| `LoreTreePanelController.cs` | 완성 | Lore 트리 패널 닫기 버튼 + ESC 키 |

### 3-7. 캐릭터 빌드 시스템

| 항목 | 상태 | 내용 |
|------|------|------|
| `CharacterClass SO` | 완성 | 스탯 배율, HP/MP 배율, 레벨당 성장치 |
| `MemoryOfSpeciesData SO` | 완성 | Human/Elf/Dwarf/Orc/Halfling, 3세트 발동 → Traits 활성화 |
| `TraitsOfSpeciesData SO` | 완성 | 동종 기억 3세트 발동 보너스 |
| `OathBindingData SO` | 완성 | 서약/제약 (Weak/Medium/Strong) |
| `PathSkillData SO` | 완성 | Mage/Monk/Sister/Thief/Warrior 패스 스킬 |
| `WeaponLoreSkillData SO` | 완성 | 무기별 Lore 스킬 |
| `PlayerStats.cs` | 완성 | computed property 실시간 계산, `static event OnStatusChanged` |
| `PlayerStatData.cs` | 완성 | SO 저장 금고, `OnValidate` 세트 효과 자동 처리 |

### 3-8. 던전 시스템

| 스크립트 | 상태 | 내용 |
|----------|------|------|
| `MapManager.cs` | 완성 | 타일맵 기반 절차적 생성 (방+미로 통로+출구+안개) |
| `DungeonGridPlayer.cs` | 완성 | WASD/화살표 격자 이동, 벽 충돌, 안개 해제, 미니맵 커서, 출구 → 다음 층 |
| `DungeonEncounter.cs` | 완성 (싱글톤) | 이동 시 15% 랜덤 인카운터, 현재 씬/위치 저장 후 전투 씬 로드 |
| `DungeonPersistentData.cs` | 완성 (static) | 씬 전환 간 그리드 좌표·안개 영역·HP/MP 영속 |
| `GeminiAPIManager.cs` | 완성 | Google Gemini AI API 연동 (던전 이벤트/텍스트 생성) |
| 던전 이벤트 (전투 외) | **미완성** | 상점, 휴식, 트랩 등 특수 이벤트 미구현 |
| 보스 층 배치 | **미정** | 설계 미정 |

### 3-9. 전역 매니저

| 스크립트 | 상태 | 내용 |
|----------|------|------|
| `GameManager.cs` | 완성 | 싱글톤, `staticPartyData` 씬 전환 간 유지, `DontDestroyOnLoad` |
| `EncounterManager.cs` | 완성 | 싱글톤, 던전 → 전투 씬 전환 트리거 |

### 3-10. 기타 구현 항목

| 항목 | 상태 | 내용 |
|------|------|------|
| `EnemyStats.cs` | 완성 | World-Space Canvas HP/MP UI 동적 생성, 히트 쉐이크 코루틴, 상태이상 인스턴스 관리 |
| `BillboardSprite.cs` | 완성 | 카메라 방향 정렬 컴포넌트 |
| 저장/불러오기 | **미완성** | `PlayerStatData` SO가 런타임 저장소이나 파일 저장 없음 |

---

## 4. 데이터 구조 & 스탯 계산

### 스탯 계산 레이어

```
최종 스탯 = 기본값 × CharacterClass 배율
          + Passive 보너스
          + Equipment 보너스
          + MemoryOfSpecies 보너스
          + TraitsOfSpecies 보너스  ← 동종 기억 3세트 발동 시
```

### SlotMask (전열/후열 타겟팅)

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

### 씬 전환 흐름

```
던전 씬 (Dungeon 2D 02~08)
  → [DungeonEncounter: 15% 인카운터]
  → 전투 씬 (Battle 01)
  → [BattleManager: 전투 종료]
  → 원래 던전 씬 복귀
```

### 씬 간 데이터 영속 구조

```csharp
// DungeonPersistentData (static)
playerGridPos, revealedTiles, currentHP, currentMP, currentFloor

// GameManager (DontDestroyOnLoad)
staticPartyData: Dictionary<string, PartyMemberData>
```

---

## 5. ScriptableObject 목록

| SO 클래스 | CreateAssetMenu 경로 | 저장 위치 | 역할 |
|-----------|----------------------|-----------|------|
| `SkillData` | `Abyssdawn/Skill Data` | `Resources/Skills/` | 스킬 전체 정의 |
| `StatusEffectSO` | `Abyssdawn/Status Effect` | `Resources/StatusEffects/` | 상태이상(저주) 데이터 |
| `EquipmentData` | `Abyssdawn/Equipment Data` | `Resources/Item_Equipments/Equipments/` | 장비 정의 |
| `ConsumableItemSO` | — | `Resources/Item_Equipments/Items/` | 소비 아이템 정의 |
| `CharacterClass` | `Game/Character Class` | `Resources/Classes/` | 직업 데이터 |
| `MemoryOfSpeciesData` | `Abyssdawn/Memory of Species` | — | 종의 기억 |
| `TraitsOfSpeciesData` | `Abyssdawn/Traits Of Species Data` | `Resources/Traits/` | 종의 특성 (세트 발동) |
| `OathBindingData` | `Abyssdawn/Oath & Binding` | `Scripts/Battle/Data/Oath&Path/Oath&Binding/` | 서약/제약 |
| `PathSkillData` | — | `Scripts/Battle/Data/Oath&Path/Path/` | 패스 스킬 |
| `WeaponLoreSkillData` | `Abyssdawn/Weapon Lore Skill` | — | 무기 Lore 스킬 |
| `ArmorBreakDataSO` | — | `Resources/EquipmentsSkills/` | 방어구 파괴 계수 |
| `BlockDataSO` | — | `Resources/EquipmentsSkills/` | 방패 블록 데이터 |
| `PassiveData` | — | — | 패시브 스킬 데이터 |
| `PlayerStatData` | — | — | 플레이어 런타임 저장 금고 |
| `EnemyDatabase` | `Battle/EnemyDatabase` | `Resources/` | 적 프리팹 풀 |
| `CharacterClassDatabase` | — | `Resources/` | 직업 DB |

---

## 6. 씬 구성 & 전환 흐름

| 씬 파일 | 용도 |
|---------|------|
| `Abyssdawn_Dungeon_2D 02~08.unity` | 던전 씬 (총 7개, 층별) |
| `Abyssdawn_Battle 01.unity` | 현재 전투 씬 |
| `Abyysborn_Battle 01.unity` | 구버전 전투 씬 (보존) |
| `Scenes/SampleScene.unity` | 기본 테스트 씬 |
| `Settings/Scenes/URP2DSceneTemplate.unity` | URP 씬 템플릿 |
| `_Recovery/0.unity`, `0 (1).unity` | 복구/백업 씬 |

---

## 7. 에디터 툴 목록

| 툴 | 위치 | 기능 |
|----|------|------|
| `SkillAssetGenerator` | `Scripts/Battle/Editor/` | SkillData SO 일괄 생성 |
| `EquipmentDataEditor` | `Scripts/Battle/Editor/` | 장비 데이터 커스텀 인스펙터 |
| `InventoryUIBuilder` | `Scripts/Battle/Editor/` | 인벤토리 UI 자동 구성 |
| `ConsumableItemGenerator` | `Scripts/Battle/Editor/` | 소비 아이템 SO 일괄 생성 |
| `NewSkillTreeSetupTool` | `Scripts/Battle/Editor/` | 새 스킬 트리 UI 씬 자동 셋업 |
| `CharacterClassSetupTool` | `Scripts/Editor/` | CharacterClass 데이터 셋업 |
| `DungeonSetupTool` | `Scripts/Dungeon/Editor/` | 던전 씬 자동 셋업 |
| `DungeonScene03Fixer` | `Scripts/Dungeon/Editor/` | 던전 씬 수동 수정 툴 |
| `BalanceSimulator` | `Scripts/Battle/` (ContextMenu) | 전투 밸런스 시뮬레이션 |

---

## 8. 소비 아이템 SO 목록

| SO 파일 | 효과 | 특이사항 |
|---------|------|---------|
| `HP_Potion` | HP 회복 | — |
| `Mana_Potion` | MP 회복 | — |
| `Antidote` | 독 해제 | — |
| `Bandage` | 출혈 해제 | — |
| `Coolant` | 화염(Ignite) 해제 | — |
| `Purification_Water` | 저주 해제 | — |
| `Smoke_Bomb` | 도망 보조 | — |
| `Stimulant` | 공격 버프 | — |
| `Whetstone` | 공격력 버프 | — |
| `Dawn_Chalice` | 특수 회복 | 최대 3회 충전, 파란 테두리 특수 처리 |

---

## 9. 장비 SO 목록 (Crude 티어)

| SO 파일 | 타입 | 특이사항 |
|---------|------|---------|
| `CrudeSword` | 한손 무기 (Sword) | — |
| `Crude_Dagger` | 한손 무기 (Dagger) | Bleed 상태이상 |
| `Crude_Katana` | 한손 무기 (Katana) | Bleed 상태이상 |
| `Crude_Axe` | 한손 무기 (Axe) | — |
| `Crude_Hammer` | 한손 무기 (Hammer) | — |
| `Crude_Greatsword` | 양손 무기 (Sword) | — |
| `Crude_Polearm` | 양손 무기 (Polearm) | — |
| `Crude_Spear` | 양손 무기 (Spear) | — |
| `Crude_Bow` | 원거리 (Bow) | Back 슬롯 |
| `Crude_Crossbow` | 원거리 (Crossbow) | Back 슬롯 |
| `Crude_Staff` | 양손 마법 (Staff) | — |
| `Crude_Wand` | 한손 마법 (Wand) | — |
| `CrudeShield` | 방패 (Shield) | Block 장착 스킬 |
| `Crude_Buckler` | 방패 (Buckler) | Block 장착 스킬 |
| `Crude_Greatshield` | 방패 (Greatshield) | Block 장착 스킬 |
| `CrudeArmor` | 갑옷 (Heavy) | — |
| `CrudeBoots` | 신발 | — |
| `CrudeBracelet` | 액세서리 | — |

---

## 10. 기획 확정 / 미구현 시스템

> 이 섹션의 항목들은 기획이 확정되었으므로, 향후 반드시 구현해야 합니다.
> 상세 코드 스니펫은 `CLAUDE.md` 섹션 13(13-A~13-F)에 기록되어 있습니다.

### 10-A. 마법 역효과 (Backlash) 시스템

강력한 마법 스킬에 성공해도 시전자에게 **역효과** 발생 가능.

**`SkillData`에 추가할 필드:**
- `backlashChance` (float 0~1): 역효과 발생 기본 확률
- `backlashType` (BacklashType enum): HPLoss / MPLoss / StatusEffect / Stun / HPAndMP
- `backlashMagnitude` (float 0~1): 역효과 강도 (최대HP 대비 비율)
- `backlashStatusEffect` (StatusEffectSO): 역효과로 부여되는 상태이상

**최종 역효과 확률 계산:**
```
최종 확률 = backlashChance × (1 - TotalBacklashSuppression)
```

---

### 10-B. 마법 관련 장비 (역효과 억제 & 마법 증폭)

**`EquipmentData`에 추가할 필드:**
- `magicAmplify` (float 1~3, 기본 1.0): 마법 데미지 배율 (여러 장비 곱셈 합산)
- `backlashSuppression` (float 0~1, 기본 0.0): 역효과 억제율 (여러 장비 가산, 최대 캡 0.8)

**예정 장비 목록:**

| 장비명 | 타입 | magicAmplify | backlashSuppression |
|--------|------|:---:|:---:|
| Arcane Staff | 양손 무기 | 1.4 | 0.0 |
| Spellblade | 한손 무기 | 1.2 | 0.1 |
| Null-Weave Robe | 천 갑옷 | 1.1 | 0.3 |
| Runed Leather | 가죽 갑옷 | 1.0 | 0.2 |
| Mana Talisman | 액세서리 | 1.15 | 0.15 |
| Backlash Ward | 액세서리 | 1.0 | 0.4 |
| Amplification Gem | 액세서리 | 1.25 | 0.0 |

---

### 10-C. 마법사 패시브 — 역효과 억제 (Backlash Resilience)

**`PassiveData`에 추가할 필드:**
- `backlashSuppression` (float 0~1): 마법 역효과 확률 감소율

**티어 설계:**

| 티어 | 패시브명 | 억제율 |
|------|---------|:---:|
| T1 | Backlash Resilience I | 0.10 |
| T2 | Backlash Resilience II | 0.20 |
| T3 | Backlash Resilience III | 0.30 |

**`PlayerStats.TotalBacklashSuppression` 추가 필요:**
```csharp
public float TotalBacklashSuppression =>
    Mathf.Min(0.8f, GetEquipmentBacklashSuppression() + GetPassiveBacklashSuppression());
```

---

### 10-D. 마법사 액티브 — Mana Stabilize (마나 안정화)

이번 턴 역효과 확률 −70% (1턴 한정 소모형 버프).

| 항목 | 값 |
|------|-----|
| MP 비용 | 15~20 |
| 대상 | Self (자신) |
| 지속 | 1턴 |
| 억제율 | −70% |
| 쿨다운 | 3턴 |

---

### 10-E. 활(Bow) 견제사격 (Suppression Shot)

**핵심 규칙 (변경 불가):**
1. 적 행동이 **캔슬되지 않음** — 적은 자기 턴에 정상 행동
2. 적 행동 시작 **직전** 타이밍에 자동 先타격 삽입
3. 크리티컬 **적용** 가능 (Luck 기반)
4. 先타격으로 HP 0 → 자연 사망 처리 (행동 못하는 건 OK, 취소 아님)

| 항목 | 값 |
|------|-----|
| 무기 | 활(Bow) 전용, Back 슬롯 |
| 공격 배율 | 0.7x |
| MP 비용 | 0 또는 소량 |
| 턴 타이밍 | 적 행동 직전 인터셉트 |

**턴 타이밍 흐름:**
```
[아군 A] → [아군 B] → [적 X 행동 시작 직전]
  ↳ 견제사격 先타격 (크리티컬 가능)
→ [적 X 행동 진행] (HP 0이면 사망으로 자연 종료)
```

---

### 10-F. 구현 우선순위

| 순위 | 항목 | 관련 파일 |
|:---:|------|------|
| 1 | `SkillData` backlash 필드 추가 | `SkillData.cs` |
| 2 | `EquipmentData` magicAmplify/backlashSuppression 추가 | `EquipmentData.cs` |
| 3 | `PassiveData` backlashSuppression 추가 | `PassiveData.cs` |
| 4 | `PlayerStats.TotalBacklashSuppression` 계산 | `PlayerStats.cs` |
| 5 | BattleSystem 역효과 발동 로직 | `BattleSystem.cs` |
| 6 | Mana Stabilize 스킬 SO + 버프 턴 관리 | `BattleManager.cs` |
| 7 | 마법 장비 SO 일괄 제작 | `EquipmentData` SO 파일들 |
| 8 | Bow 견제사격 SO + 인터셉트 타이밍 | `BattleManager.cs` |

---

### 10-G. 그 외 미완성 항목

| 항목 | 상태 | 비고 |
|------|------|------|
| 전투 중 아이템 선택 UI 자동화 | 미완성 | `selectedConsumableItem` 수동 연결 → 자동 설정 필요 |
| Sword 외 Lore/Path 스킬 트리 | 미완성 | Sword Lore 트리만 구현됨 |
| 던전 이벤트 (전투 외) | 미완성 | 상점, 휴식, 트랩 등 특수 이벤트 |
| 보스 층 배치 | 미정 | 총 층수/보스 배치 설계 미정 |
| 저장/불러오기 (파일) | 미완성 | PlayerStatData SO는 런타임 저장소이나 파일 저장 없음 |
| 상점/휴식 타일 | 미완성 | 설계 미정 |

---

## 11. 작업 규칙

- **모든 파일 수정은 메인 저장소에 직접 적용**
  - 경로: `E:\Dawi\My Programs\Game Maker\Unity\Projects\Abyssdawn 01\`
  - Unity Editor가 메인 저장소를 열고 있으므로 worktree 변경은 Unity에 반영 안 됨
- `FindObjectOfType` 대신 `FindFirstObjectByType` 사용 (Unity 6)
- 새 싱글톤 추가 시 기존 패턴(`Awake` + `DontDestroyOnLoad`) 따름
- 새 SO 타입 추가 시 대응하는 에디터 Generator 툴도 함께 작성 권장
- 에디터 전용 코드는 `Assets/Scripts/*/Editor/` 폴더 또는 `#if UNITY_EDITOR` 블록

---

*이 문서는 Claude Code가 자동 생성 및 관리합니다.*
*CLAUDE.md와 함께 참조하면 프로젝트 전체 구조를 빠르게 파악할 수 있습니다.*
