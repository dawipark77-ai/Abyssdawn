using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle; // SlotMask, SkillData, ConsumableItemSO 참조

namespace Abyssdawn
{
    // ──────────────────────────────────────────
    // 몬스터 분류 열거형
    // ──────────────────────────────────────────

    /// <summary>
    /// 몬스터 등급. Normal = 일반, Elite = 강화 개체, Boss = 보스
    /// </summary>
    public enum MonsterType
    {
        Normal,
        Elite,
        Boss,
        Beast,
        Undead
    }

    /// <summary>
    /// 적 AI 행동 패턴.
    /// Aggressive = 공격 우선, Defensive = 방어/회복 우선, Support = 아군 보조 우선
    /// </summary>
    public enum AIPattern
    {
        Aggressive,
        Defensive,
        Support
    }

    /// <summary>
    /// 적 파티의 전투 진형(Formation).
    /// 이 몬스터가 대표(Boss > Elite > 첫 번째)일 때 전투 배치가 결정됩니다.
    /// Single = 단독 1마리 / All_Front = 4·0 / Three_Front = 3·1 / Two_Two = 2·2 / One_Front = 1·3
    /// </summary>
    public enum FormationType
    {
        Single,
        All_Front,
        Three_Front,
        Two_Two,
        One_Front
    }

    // ──────────────────────────────────────────
    // 드롭 엔트리 (직렬화 가능 내부 클래스)
    // ──────────────────────────────────────────

    /// <summary>
    /// 몬스터 드롭 테이블의 항목 하나.
    /// </summary>
    [System.Serializable]
    public class DropEntry
    {
        [Tooltip("드롭될 아이템 (ConsumableItemSO 또는 EquipmentData 모두 할당 가능)")]
        public BaseItemSO item;

        [Tooltip("드롭 확률 (0.0 = 0%, 1.0 = 100%)")]
        [Range(0f, 1f)]
        public float dropChance = 0.1f;
    }

    // ──────────────────────────────────────────
    // MonsterSO
    // ──────────────────────────────────────────

    /// <summary>
    /// 적 몬스터의 전투 데이터 SO.
    /// 런타임 로직 없음. 순수 데이터만 보유.
    /// </summary>
    [CreateAssetMenu(menuName = "Abyssdawn/MonsterSO", fileName = "NewMonster")]
    public class MonsterSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 식별 정보
        // ──────────────────────────────────────────
        [Header("식별 정보")]

        [Tooltip("몬스터 이름")]
        [SerializeField] private string monsterName;

        [Tooltip("전투 화면에 표시할 스프라이트")]
        public Sprite sprite;

        [Header("비주얼")]
        [SerializeField] private float scaleMultiplier = 1f;

        [Tooltip("몬스터 등급 (Normal / Elite / Boss)")]
        [SerializeField] private MonsterType type = MonsterType.Normal;

        // ──────────────────────────────────────────
        // 전투 스탯
        // ──────────────────────────────────────────
        [Header("전투 스탯")]

        [Tooltip("최대 HP")]
        [SerializeField] private int hp;

        [Tooltip("최대 MP")]
        [SerializeField] private int mp;

        [Tooltip("물리 공격력")]
        [SerializeField] private int atk;

        [Tooltip("물리 방어력")]
        [SerializeField] private int def;

        [Tooltip("마법 공격력")]
        [SerializeField] private int mag;

        [Tooltip("민첩성 (명중률/회피 계산에 사용)")]
        [SerializeField] private int agi;

        [Tooltip("행운 (크리티컬 확률 계산에 사용)")]
        [SerializeField] private int luk;

        // ──────────────────────────────────────────
        // 저항 (0 = 완전 면역, 1 = 정상 피해)
        // ──────────────────────────────────────────
        [Header("저항 (0 = 완전 면역 / 1 = 정상 피해)")]

        [Tooltip("물리 피해 저항")]
        [Range(0f, 1f)]
        [SerializeField] private float physResist = 1f;

        [Tooltip("마법 피해 저항")]
        [Range(0f, 1f)]
        [SerializeField] private float magResist = 1f;

        [Tooltip("상태이상 저항 (저주 등 부여 확률 배율)")]
        [Range(0f, 1f)]
        [SerializeField] private float statusResist = 1f;

        // ──────────────────────────────────────────
        // 스킬
        // ──────────────────────────────────────────
        [Header("스킬")]

        [Tooltip("사용 가능한 액티브 스킬 목록")]
        [SerializeField] private List<SkillData> activeSkills = new();

        [Tooltip("상시 발동하는 패시브 스킬 목록")]
        [SerializeField] private List<PassiveData> passiveSkills = new();

        // ──────────────────────────────────────────
        // AI 행동 패턴
        // ──────────────────────────────────────────
        [Header("AI 행동 패턴")]

        [Tooltip("AI가 우선시하는 전투 전략 (Aggressive / Defensive / Support)")]
        [SerializeField] private AIPattern aiPattern = AIPattern.Aggressive;

        // ──────────────────────────────────────────
        // 파티 배치
        // ──────────────────────────────────────────
        [Header("파티 배치")]

        [Tooltip("이 몬스터가 배치될 수 있는 슬롯 (SlotMask 비트 마스크)")]
        [SerializeField] private SlotMask allowedSlots = SlotMask.Any;

        [Tooltip("이 몬스터가 대표(Boss/Elite/첫 번째)일 때 전투 파티에 적용할 진형")]
        [SerializeField] private FormationType formation = FormationType.Two_Two;

        // ──────────────────────────────────────────
        // 등장 조건
        // ──────────────────────────────────────────
        [Header("등장 조건")]

        [Tooltip("등장 최소 던전 층")]
        [SerializeField] private int minFloor = 1;

        [Tooltip("등장 최대 던전 층 (0 = 제한 없음)")]
        [SerializeField] private int maxFloor = 0;

        [Tooltip("같은 테이블 내 상대적 등장 가중치 (높을수록 자주 등장)")]
        [Min(0f)]
        [SerializeField] private float spawnWeight = 1f;

        // ──────────────────────────────────────────
        // 보상
        // ──────────────────────────────────────────
        [Header("보상")]

        [Tooltip("처치 시 획득 경험치")]
        [SerializeField] private int expReward;

        [Tooltip("처치 시 획득 골드")]
        [SerializeField] private int goldReward;

        [Tooltip("드롭 테이블 (아이템 + 드롭 확률 목록)")]
        [SerializeField] private List<DropEntry> dropTable = new();

        // ──────────────────────────────────────────
        // 동료화
        // ──────────────────────────────────────────
        [Header("동료화")]

        [Tooltip("전투 종료 후 동료 합류 확률 (0 = 동료화 불가)")]
        [Range(0f, 1f)]
        [SerializeField] private float companionChance = 0f;

        [Tooltip("동료화 시 사용할 CompanionSO (null = 동료화 불가)")]
        [SerializeField] private CompanionSO companionData;

        // ──────────────────────────────────────────
        // 프로퍼티 (읽기 전용)
        // ──────────────────────────────────────────

        // 식별
        /// <summary>몬스터 이름</summary>
        public string MonsterName => monsterName;

        /// <summary>전투 스프라이트</summary>
        public Sprite Sprite => sprite;

        /// <summary>스케일 배율</summary>
        public float ScaleMultiplier => scaleMultiplier;

        /// <summary>몬스터 등급</summary>
        public MonsterType Type => type;

        // 스탯
        /// <summary>최대 HP</summary>
        public int HP => hp;

        /// <summary>최대 MP</summary>
        public int MP => mp;

        /// <summary>물리 공격력</summary>
        public int ATK => atk;

        /// <summary>물리 방어력</summary>
        public int DEF => def;

        /// <summary>마법 공격력</summary>
        public int MAG => mag;

        /// <summary>민첩성</summary>
        public int AGI => agi;

        /// <summary>행운</summary>
        public int LUK => luk;

        // 저항
        /// <summary>물리 피해 저항 (0~1)</summary>
        public float PhysResist => physResist;

        /// <summary>마법 피해 저항 (0~1)</summary>
        public float MagResist => magResist;

        /// <summary>상태이상 저항 (0~1)</summary>
        public float StatusResist => statusResist;

        // 스킬
        /// <summary>액티브 스킬 목록</summary>
        public IReadOnlyList<SkillData> ActiveSkills => activeSkills;

        /// <summary>패시브 스킬 목록</summary>
        public IReadOnlyList<PassiveData> PassiveSkills => passiveSkills;

        // AI
        /// <summary>AI 행동 패턴</summary>
        public AIPattern AIPattern => aiPattern;

        // 배치
        /// <summary>배치 가능 슬롯 마스크</summary>
        public SlotMask AllowedSlots => allowedSlots;

        /// <summary>대표 몬스터일 때 적용될 전투 진형</summary>
        public FormationType Formation => formation;

        // 등장 조건
        /// <summary>등장 최소 층</summary>
        public int MinFloor => minFloor;

        /// <summary>등장 최대 층 (0 = 제한 없음)</summary>
        public int MaxFloor => maxFloor;

        /// <summary>등장 가중치</summary>
        public float SpawnWeight => spawnWeight;

        // 보상
        /// <summary>획득 경험치</summary>
        public int ExpReward => expReward;

        /// <summary>획득 골드</summary>
        public int GoldReward => goldReward;

        /// <summary>드롭 테이블</summary>
        public IReadOnlyList<DropEntry> DropTable => dropTable;

        // 동료화
        /// <summary>동료 합류 확률 (0 = 불가)</summary>
        public float CompanionChance => companionChance;

        /// <summary>동료화 데이터 (null = 불가)</summary>
        public CompanionSO CompanionData => companionData;

        // ──────────────────────────────────────────
        // 편의 메서드
        // ──────────────────────────────────────────

        /// <summary>
        /// 지정 층에서 이 몬스터가 등장할 수 있는지 확인
        /// </summary>
        public bool CanSpawnOnFloor(int floor)
        {
            if (floor < minFloor) return false;
            if (maxFloor > 0 && floor > maxFloor) return false;
            return true;
        }

        /// <summary>
        /// 동료화 가능 여부 (확률 > 0이고 companionData가 있어야 함)
        /// </summary>
        public bool CanBecomCompanion => companionChance > 0f && companionData != null;
    }
}
