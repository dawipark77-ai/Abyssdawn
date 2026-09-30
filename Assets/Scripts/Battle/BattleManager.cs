using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using AbyssdawnBattle;
using Abyssdawn;
using SkillData = AbyssdawnBattle.SkillData;

public class BattleManager : MonoBehaviour
{
    // 씬이 로드된 직후 실행 (Awake 이후, Start 이전)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        // 씬이 로드된 직후 skillPanel과 actionPanel 비활성화
        var skillPanel = GameObject.Find("SkillPanel");
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }
        var actionPanel = GameObject.Find("ActionPanel");
        if (actionPanel != null)
        {
            actionPanel.SetActive(false);
        }
    }

    [Header("UI Elements")]
    public ScrollRect scrollRect;
    public TextMeshProUGUI messageText;
    public GameObject actionPanel;

    [Header("Buttons")]
    public Button attackButton;
    public Button skillButton;
    public Button itemButton;
    [FormerlySerializedAs("runButton")]
    public Button fleeButton;
    public Button defendButton;
    public Button skillBackButton;

    [Header("Panels")]
    public GameObject skillPanel;

    [Header("Battle UI Manager")]
    public BattleUIManager battleUIManager;

    [Header("Status UI")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerMPText;
    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI enemyMPText;
    public TextMeshProUGUI potionCountText;
    public TextMeshProUGUI pageText;

    [Header("Battle Settings")]
    public int maxMessages = 50;

    [Header("Monster Recruit (1차 구현)")]
    [Tooltip("최대 활성 동료 수 (Hero 제외, 전투 슬롯 점유)")]
    [Range(1, 10)]
    public int maxActiveCompanions = 3;

    [Tooltip("최대 대기열(후보) 동료 수. 활성 슬롯 꽉 차면 여기에 보관")]
    [Range(0, 10)]
    public int maxCompanionWaitlist = 3;

    [Header("Recruit Dialog (영입 YES/NO UI)")]
    [Tooltip("직접 만든 영입 다이얼로그 패널 GameObject. 비워두면 런타임 자동 생성(회색 박스 + YES/NO).")]
    public GameObject customRecruitPanel;

    [Tooltip("커스텀 패널의 YES 버튼")]
    public Button customRecruitYesButton;

    [Tooltip("커스텀 패널의 NO 버튼")]
    public Button customRecruitNoButton;

    [Tooltip("적 sprite가 다시 등장할 때 페이드인 시간(초). 0이면 즉시 표시.")]
    [Range(0f, 3f)]
    public float recruitSpriteFadeInDuration = 0.8f;

    [Header("Sequential Message (Dragon Quest 스타일 레벨업 연출)")]
    [Tooltip("한 글자가 나타나는 간격(초). 작을수록 빠름. 권장 0.02~0.08")]
    [Range(0.005f, 0.3f)]
    public float messageTypingSpeed = 0.04f;

    [Tooltip("줄 완료 후 다음 줄로 진행할 키")]
    public KeyCode messageAdvanceKey = KeyCode.Space;

    [Tooltip("마우스 좌클릭으로도 다음 줄로 진행")]
    public bool messageAdvanceOnMouseClick = true;

    [Tooltip("타이핑 중 입력 시 해당 줄 전체 즉시 표시(스킵)")]
    public bool messageSkipOnInput = true;

    [Header("Advance Indicator (▼ 클릭 유도 — 텍스트 문자 방식)")]
    [Tooltip("▼ 표시 자체 ON/OFF. 끄면 시퀀스 진행에 영향 없이 ▼만 안 뜸")]
    public bool indicatorEnabled = true;

    [Tooltip("사용할 문자. 기본 '▼'. ▽ ▶ ▷ 등으로 바꿔도 됨")]
    public string indicatorChar = "▼";

    [Tooltip("깜빡임 1주기(초). 작을수록 빠르게 깜빡. on/off 토글 간격")]
    [Range(0.05f, 2f)]
    public float indicatorBlinkInterval = 0.4f;

    [Tooltip("위아래 움직임 ON/OFF. 끄면 제자리에서 깜빡임만")]
    public bool indicatorBobEnabled = true;

    [Tooltip("위아래 움직임 진폭(px). indicatorBobEnabled가 켜져 있을 때만 적용")]
    [Range(0f, 20f)]
    public float indicatorBobAmplitude = 4f;

    [Tooltip("위아래 움직임 속도. 클수록 빠르게 흔들림(라디안/초)")]
    [Range(0.5f, 10f)]
    public float indicatorBobSpeed = 3f;

    [Header("Post-Sequence Delay (시퀀스 종료 후 추가 대기)")]
    [Tooltip("레벨업 시퀀스 모든 줄 클릭 완료 후, 맵 복귀 전 추가 대기(초). 사용자 요청 '페이드아웃' 시간 역할")]
    [Range(0f, 5f)]
    public float postSequenceFadeDelay = 3.0f;

    [Header("Battle System")]
    public PlayerStats player;
    public EnemyStats enemy;
    public int potionCount = 3;
    public bool battleEnded = false;

    [Header("Scene Names")]
    [Tooltip("사망 시 리셋할 1층 던전 씬 이름")]
    public string startDungeonScene = "Abyssdawn_Dungeon_2D 07";

    [Header("Consumable Inventory")]
    [Tooltip("전투 중 사용할 아이템 SO (인벤토리 UI에서 선택 시 자동 설정).")]
    public ConsumableItemSO selectedConsumableItem;

    [Header("Replay System")]
    public BattleRecorder battleRecorder;

    [Header("Player Data")]
    public PlayerStatData playerStatData;

    private Queue<string> messageQueue = new Queue<string>();

    // [2026-05-24] 영입 시스템 (1차) — Warrior/Rogue/Wizard 시스템과 별개
    private List<PlayerStats> _companionInstances = new List<PlayerStats>();   // 활성 동료 (max maxActiveCompanions)
    private EnemyStats _lastDefeatedEnemy;                                     // EnemyStats.HandleDeath가 갱신
    public bool playerTurn = true;

    [Header("RPG Percent Settings")]
    // [2026-09-30] 25 → 5 (드퀘 수준). 25%는 적의 4타 중 1타가 1.5배라 운에 좌우되는 게임이 됨 (10층 밸런스 시뮬레이션)
    public const float DefaultCriticalChance = 5f;
    [Range(0, 100)] public float criticalChance = DefaultCriticalChance; // 크리티컬 기본 확률 (+ 행운 + 스킬/버프 보너스)
    [Range(0, 100)] public float evasionChance = 10f;  // 회피 확률

    // ========== 파티 시스템 ==========
    [Header("Party System")]
    public Transform playerPartyRoot;
    public Transform playerPartyCenter;
    public float playerPartySpacing = 1.5f;
    public bool startWithFullParty = false;
    public KeyCode soloPartyHotkey = KeyCode.F3;
    public KeyCode fullPartyHotkey = KeyCode.F4;

    [Header("Party Status UI")]
    public RectTransform playerStatusPanel;
    public float partyPanelHeight = 260f;
    public float partyPanelBottomMargin = 20f;
    public float partyPanelOuterPadding = 10f;
    public float partyPanelInnerPadding = 5f;
    public float partyPanelVerticalPadding = 10f;
    public float partySlotLeftMargin = 5f;
    public float partySlotTopMargin = 5f;
    public float partySlotRightMargin = 5f;
    public float partySlotBottomMargin = 5f;
    public float partySlotFontSize = 24f;
    public float partySlotLineSpacing = 28f;

    [Header("━━━━━━━━━━ 아군 파티 UI ━━━━━━━━━━")]
    [SerializeField] GameObject[] partySlots;           // PartySlot_1~4
    [SerializeField] TMP_Text[] partyNameTexts;
    [SerializeField] Image[] partyHPBars;
    [SerializeField] TMP_Text[] partyHPTexts;
    [SerializeField] Image[] partyMPBars;
    [SerializeField] TMP_Text[] partyMPTexts;
    [SerializeField] Transform[] partyStatusIconRows;
    // [2026-05-24] partyPortraitImages는 신규 필드 — null 시작이 SerializedObjectList NRE를
    // 유발할 가능성이 있어 명시적으로 빈 배열로 초기화. Inspector에서 비어 있어도 안전.
    [Tooltip("슬롯 1~4 초상화 (영입 동료 MonsterSO.sprite). 비어 있으면 이름·HP/MP만 갱신.")]
    [SerializeField] Image[] partyPortraitImages = new Image[0];

    // 파티 관련 구조체 및 열거형
    public enum PartyMode { Solo, Full }
    private PartyMode currentPartyMode = PartyMode.Full;

    [System.Serializable]
    public struct AllyPreset
    {
        public string name;
        public int maxHP;
        public int maxMP;
        public int attack;
        public int defense;
        public int magic;
        public int agility;
        public int luck;
        public Color color;
    }

    public enum PartyRole { Hero, Warrior, Rogue, Wizard, Companion }
    private PartyRole currentControlledRole = PartyRole.Hero;

    private List<PlayerStats> activePartyMembers = new List<PlayerStats>();
    private Dictionary<PartyRole, PlayerStats> allyInstances = new Dictionary<PartyRole, PlayerStats>();
    private PlayerStats currentControlledMember;

    private List<RectTransform> playerStatusSlots = new List<RectTransform>();
    private List<TextMeshProUGUI> playerStatusTexts = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> playerStatusNameTexts = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> playerStatusHPTexts = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> playerStatusMPTexts = new List<TextMeshProUGUI>();
    private List<Image> playerStatusBackgrounds = new List<Image>();
    private List<Transform> playerStatusIconRows = new List<Transform>();
    private List<List<Image>> playerStatusIconImages = new List<List<Image>>();
    private Dictionary<PlayerStats, Coroutine> playerShakeCoroutines = new Dictionary<PlayerStats, Coroutine>();

    // ========== 턴 시스템 ==========
    public enum BattlePhase { Command, Resolution }
    private BattlePhase currentPhase = BattlePhase.Command;

    internal class BattleActor
    {
        public PlayerStats player;
        public EnemyStats enemy;
        public int agility;
        public bool isPlayer;

        public BattleActor(PlayerStats p)
        {
            player = p;
            enemy = null;
            // [StatMod 5단계] Speed 배율 적용 후 턴 순서용 agility로 캡처.
            agility = Mathf.RoundToInt(p.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Speed, p.Agility));
            isPlayer = true;
        }

        public BattleActor(EnemyStats e)
        {
            player = null;
            enemy = e;
            // [StatMod 5단계] Speed 배율 적용 후 턴 순서용 agility로 캡처.
            agility = Mathf.RoundToInt(e.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Speed, e.Agility));
            isPlayer = false;
        }
    }

    private List<BattleActor> turnOrder = new List<BattleActor>();
    private bool turnInProgress = false;
    /// <summary>외부 UI(BattleItemSlot 등)가 행동 중복 방지용으로 읽는 read-only 프로퍼티.</summary>
    public bool IsTurnInProgress => turnInProgress;

    [System.Serializable]
    public class AllyCommand
    {
        public PlayerStats actor;
        public string actionType; // "attack", "skill", "item", "defend", "run"
        public EnemyStats targetEnemy;
        public SkillData skill;
        public int itemHealAmount;
        public bool consumesPotion;
        public ConsumableItemSO usedItem;
    }

    private List<AllyCommand> pendingCommands = new List<AllyCommand>();
    private int commandIndex = 0;
    private bool waitingForTargetSelection = false;
    private string pendingAction = "";
    private SkillData pendingSkill = null;
    private EnemyStats hoveredEnemy = null;

    // ========== 적 시스템 ==========
    [Header("Enemy System")]
    public Transform spawnCenter;
    public Transform worldRoot;
    public Canvas canvas;
    public Transform enemyStatusPanel;

    [Tooltip("적 상태창. 상단 적 카드 버튼 → OnEnemyStatusCardClicked(1~4)로 열림.")]
    [SerializeField] private EnemyStatusPanel enemyInspectPanel;

    [Header("━━━━━━━━━━ 적 진영 UI ━━━━━━━━━━")]
    [SerializeField] Image[] enemyMonsterImages;
    [SerializeField] TMP_Text[] enemyNameTexts;
    [SerializeField] Transform[] enemyStatusIconRows;

    public float spawnOffset = 1f;
    public float extraSpacingPerEnemy = 0.25f;

    [Header("몬스터 스폰")]
    [Tooltip("SlotPoint_Center — 단독 몬스터(Single) 배치용")]
    [SerializeField] private Transform slotPointCenter;
    [Tooltip("SlotPoint_1 ~ SlotPoint_4 (전열, 왼쪽부터)")]
    [SerializeField] private Transform[] slotPointsFront = new Transform[4];
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private float monsterScaleMultiplier = 1f;

    [Header("분단선 (고정 12|34)")]
    [Tooltip("항상 표시되는 단일 분단선 오브젝트")]
    [FormerlySerializedAs("divider_2")]
    public GameObject divider;

    /// <summary>
    /// 현재 전투의 분단선 위치 (1-based 슬롯 인덱스).
    /// 값이 K이면 Slot_K 뒤에 divider_K가 활성화 (Slot 1..K = 전열, Slot K+1..4 = 후열).
    /// 0 = 분단선 없음(전원 전열 또는 전원 후열 또는 단독).
    /// 스폰 시 SpawnPattern의 frontCount에서 파생됨.
    /// </summary>
    private int currentDividerSlotIdx = 0;

    [Tooltip("씬에 수동 배치하는 적 UI 앵커. 순번(1,2,3,...) 순서대로 몬스터 UI가 해당 앵커 위치로 고정됩니다.\n" +
             "비워두면 아래 자동 배치 설정이 사용됩니다.")]
    [SerializeField] private Transform[] enemyUIAnchors;

    [Header("Enemy UI Auto Layout (앵커 비어있을 때)")]
    [Tooltip("TRUE: 순번을 기준으로 코드가 자동 등간격 배치. FALSE: EnemyUIDisplay의 Fixed World Y / 스프라이트 추적 모드 사용")]
    [SerializeField] private bool useAutoUILayout = true;

    [Tooltip("UI 순번 1이 위치할 월드 좌표 (좌측 첫 UI 기준점)")]
    [SerializeField] private Vector3 uiLayoutOrigin = new Vector3(-3f, 3.5f, 0f);

    [Tooltip("UI 사이의 X 간격 (월드 단위)")]
    [SerializeField] private float uiLayoutSpacingX = 1.5f;

    private List<EnemyStats> activeEnemies = new List<EnemyStats>();
    private List<RectTransform> enemyStatusSlots = new List<RectTransform>();
    private List<Image> enemyStatusFrames = new List<Image>();
    private List<Image> enemyStatusMonsterImages = new List<Image>();
    private List<TextMeshProUGUI> enemyStatusNameTexts = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> enemyStatusHPTexts = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> enemyStatusMPTexts = new List<TextMeshProUGUI>();
    private List<List<Image>> enemyStatusIconImages = new List<List<Image>>();
    private Dictionary<int, Coroutine> enemySlotPulseCoroutines = new Dictionary<int, Coroutine>();
    private Dictionary<int, Coroutine> enemySlotShakeCoroutines = new Dictionary<int, Coroutine>();
    private Dictionary<EnemyStats, int> lastEnemyDisplayedHP = new Dictionary<EnemyStats, int>();
    private bool usingEnemyBarPortraitUI = false;
    private int currentTargetIndex = 0;

    // ========== 전열/후열 슬롯 시스템 ==========
    private BattleLine<PlayerStats> playerLine = new BattleLine<PlayerStats>();
    private BattleLine<EnemyStats> enemyLine = new BattleLine<EnemyStats>();

    // ========== 스킬 시스템 ==========
    [Header("Skill System")]
    public SkillDataList skillLibrary;
    public SkillData fireballSkill;
    private List<SkillData> heroSkillCache = new List<SkillData>(); // 히어로 스킬 목록 (Strong Slash, Fireball)
    private Dictionary<PartyRole, List<SkillData>> roleSkillCache = new Dictionary<PartyRole, List<SkillData>>(); // 역할별 스킬 캐시
    private readonly Dictionary<PlayerStats, List<SkillData>> companionSkillsByMember = new Dictionary<PlayerStats, List<SkillData>>();

    // ========== 액션 딜레이 ==========
    [Header("Action Delays")]
    public float turnDelay = 1.0f;
    public float actionDelay = 0.5f;

    // --- Skill UI Constants & State ---
    private const int MAX_SKILLS = 16;      // 전체 스킬 최대 보유량
    private const int SKILLS_PER_PAGE = 7;  // 한 페이지당 표시 스킬 수 (8번째 슬롯은 Back 버튼용)
    private int currentSkillPage = 0;       // 현재 스킬 페이지 인덱스
    private Button prevPageButton;
    private Button nextPageButton;

    // ... (중략) ...

    // 스킬 버튼 생성 (버튼이 없을 때)
    private void CreateSkillButtons(List<SkillData> skills)
    {
        if (skillPanel == null) return;
        
        // 기존 버튼 모두 제거 (SkillBackButton, Pagination 제외)
        foreach (Transform child in skillPanel.transform)
        {
            if (child.GetComponent<Button>() == skillBackButton || 
                child.GetComponent<Button>() == prevPageButton || 
                child.GetComponent<Button>() == nextPageButton) continue;
            
            Destroy(child.gameObject);
        }

        // 레이아웃 그룹 컴포넌트가 있다면 제거 (수동 배치를 위해)
        UnityEngine.UI.LayoutGroup[] layoutGroups = skillPanel.GetComponents<UnityEngine.UI.LayoutGroup>();
        foreach (var group in layoutGroups) DestroyImmediate(group);
        
        UnityEngine.UI.ContentSizeFitter fitter = skillPanel.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (fitter != null) DestroyImmediate(fitter);

        Debug.Log($"[BattleManager] Creating {skills.Count} skill buttons in SkillPanel (Layout: Adaptive 4x2)");
        
        // --- 동적 레이아웃 계산 ---
        RectTransform panelRT = skillPanel.GetComponent<RectTransform>();
        float panelWidth = panelRT.rect.width;
        float panelHeight = panelRT.rect.height;

        // 패딩 설정
        float paddingX = 20f;
        float paddingY = 20f;
        
        // 사용 가능한 공간
        float availableWidth = panelWidth - (paddingX * 2);
        float availableHeight = panelHeight - (paddingY * 2);
        
        // 셀 크기 (2열 4행)
        float cellWidth = availableWidth / 2f;
        float cellHeight = availableHeight / 4f;
        
        // 버튼 크기 (셀보다 약간 작게)
        float btnWidth = cellWidth * 0.9f;
        float btnHeight = cellHeight * 0.8f;

        // 시작 위치 (Top-Left 기준, Anchor가 Center이므로 좌표 변환 필요)
        // Anchor (0.5, 0.5) 기준:
        // Top-Left는 (-width/2, height/2)
        float startX = -panelWidth / 2f + paddingX + (cellWidth / 2f);
        float startY = panelHeight / 2f - paddingY - (cellHeight / 2f);

        // Determine current actor's equipped weapon category for skill restriction
        AbyssdawnBattle.WeaponCategory equippedCategory = AbyssdawnBattle.WeaponCategory.None;
        if (currentControlledMember != null && currentControlledMember.statData != null)
        {
            var rh = currentControlledMember.statData.rightHand;
            if (rh != null) equippedCategory = rh.weaponCategory;
        }

        // 1. 스킬 버튼 배치 (최대 7개)
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];

            // Weapon category restriction check
            // None on skill = universal (any weapon allowed)
            // Otherwise equipped weapon must match skill requirement
            bool weaponCompatible = skill.weaponCategory == AbyssdawnBattle.WeaponCategory.None
                                    || skill.weaponCategory == equippedCategory;

            // 버튼 생성
            GameObject btnObj = new GameObject($"SkillButton_{i}", typeof(RectTransform), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.Image));
            btnObj.transform.SetParent(skillPanel.transform, false);

            // 위치 계산
            // 0,1,2,3 -> 좌측 열 (Col 0)
            // 4,5,6   -> 우측 열 (Col 1)
            int col = i / 4;
            int row = i % 4;

            float x = startX + (col * cellWidth);
            float y = startY - (row * cellHeight);

            RectTransform btnRT = btnObj.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(btnWidth, btnHeight);
            btnRT.anchoredPosition = new Vector2(x, y);

            UnityEngine.UI.Button btn = btnObj.GetComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image btnImage = btnObj.GetComponent<UnityEngine.UI.Image>();

            if (weaponCompatible)
            {
                btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                btn.interactable = true;
            }
            else
            {
                // Grayed out — wrong weapon equipped
                btnImage.color = new Color(0.12f, 0.12f, 0.12f, 0.7f);
                btn.interactable = false;
            }

            // 아이콘 추가 (왼쪽에 배치)
            if (skill.skillIcon != null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                iconObj.transform.SetParent(btnObj.transform, false);
                RectTransform iconRT = iconObj.GetComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0, 0.5f);
                iconRT.anchorMax = new Vector2(0, 0.5f);
                iconRT.pivot = new Vector2(0, 0.5f);
                float iconSize = btnHeight * 0.7f; // 버튼 높이의 70%
                iconRT.sizeDelta = new Vector2(iconSize, iconSize);
                iconRT.anchoredPosition = new Vector2(10, 0); // 왼쪽에서 10px 떨어진 위치

                UnityEngine.UI.Image iconImage = iconObj.GetComponent<UnityEngine.UI.Image>();
                iconImage.sprite = skill.skillIcon;
                iconImage.preserveAspect = true;
                if (!weaponCompatible) iconImage.color = new Color(1f, 1f, 1f, 0.35f);
            }

            // 텍스트 추가 (아이콘 오른쪽에 배치)
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 0);
            textRT.anchorMax = new Vector2(1, 1);
            // 아이콘이 있으면 왼쪽 여백을 더 크게, 없으면 작게
            float leftMargin = skill.icon != null ? btnHeight * 0.7f + 15 : 10;
            textRT.offsetMin = new Vector2(leftMargin, 0);
            textRT.offsetMax = new Vector2(-10, 0);

            TMPro.TextMeshProUGUI btnText = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            string costText = skill.hpCostPercent > 0 ? $"HP {skill.hpCostPercent}%" : $"MP {skill.mpCost}";
            if (weaponCompatible)
            {
                btnText.text = $"{skill.skillName}\n<size=16><color=#AAAAAA>{costText}</color></size>";
                btnText.color = Color.white;
            }
            else
            {
                string reqLabel = skill.weaponCategory.ToString();
                btnText.text = $"{skill.skillName}\n<size=14><color=#FF6666>Requires: {reqLabel}</color></size>";
                btnText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            // 메인 폰트 크기를 25pt로 조정
            btnText.fontSize = Mathf.Min(25, btnHeight * 0.55f);
            btnText.alignment = TMPro.TextAlignmentOptions.Left;

            // 리스너 추가 (only for compatible skills)
            if (weaponCompatible)
            {
                SkillData capturedSkill = skill;
                btn.onClick.AddListener(() =>
                {
                    UsePlayerSkill(capturedSkill);
                });
            }
        }

        // 2. Back 버튼 배치 (우측 하단 고정: Col 1, Row 3)
        if (skillBackButton != null)
        {
            RectTransform backRT = skillBackButton.GetComponent<RectTransform>();
            if (backRT != null)
            {
                // 부모가 skillPanel이 아니면 옮김 (안전장치)
                if (skillBackButton.transform.parent != skillPanel.transform)
                {
                    skillBackButton.transform.SetParent(skillPanel.transform, false);
                }

                backRT.anchorMin = new Vector2(0.5f, 0.5f);
                backRT.anchorMax = new Vector2(0.5f, 0.5f);
                backRT.sizeDelta = new Vector2(btnWidth, btnHeight);
                
                // 우측 열(1), 마지막 행(3)
                float backX = startX + (1 * cellWidth);
                float backY = startY - (3 * cellHeight);
                backRT.anchoredPosition = new Vector2(backX, backY);
                
                // 텍스트가 있다면 "Back"으로 설정
                TMPro.TextMeshProUGUI backText = skillBackButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (backText != null)
                {
                    backText.text = "Back";
                    // Back 버튼 폰트도 동일하게 증가
                    backText.fontSize = Mathf.Min(28, btnHeight * 0.55f);
                }
                
                skillBackButton.gameObject.SetActive(true);
            }
        }
    }

    void Awake()
    {
        Debug.Log("[PERSISTENCE_DEBUG] BattleManager.Awake RUNNING");
        // [2026-05-25 고정 3칸] ActiveRoster.Count는 항상 3(null 포함). 잔존 판정은 CountActive()(실제 동료 수)로.
        Debug.Log($"[Recruit-DIAG] BattleManager.Awake — static 초기 상태 점검: CountActive={CompanionPartyPersistence.CountActive()}/{CompanionPartyPersistence.MaxActive}, CountWait={CompanionPartyPersistence.CountWait()}/{CompanionPartyPersistence.MaxWaitlist}");
        if (CompanionPartyPersistence.CountActive() > 0 || CompanionPartyPersistence.CountWait() > 0)
        {
            Debug.LogWarning($"[Recruit-DIAG] ⚠ Awake 시점 ActiveRoster/Waitlist에 이미 항목 있음 — Domain Reload 비활성화 또는 이전 세션 잔존 데이터 의심");
            for (int i = 0; i < CompanionPartyPersistence.ActiveRoster.Count; i++)
            {
                var e = CompanionPartyPersistence.ActiveRoster[i];
                if (e == null) continue;   // 빈 슬롯은 건너뜀
                Debug.LogWarning($"[Recruit-DIAG]   ActiveRoster[{i}]: id={e.id}, resourcePath='{e.resourcePath}', HP={e.currentHP}, MP={e.currentMP}");
            }
        }
        startWithFullParty = false; // [Anti-Gravity] 강제 Solo 모드 설정 (인스펙터 값 무시)
        ForceDisableUIPanels();

        AutoAssignSlotPoints();

        // 페이지네이션 UI 자동 연결 시도
        TryAutoAssignPaginationUI();
    }

    /// <summary>
    /// EnemySpawnRoot 하위에서 SlotPoint_Center, SlotPoint_1~7을 이름으로 자동 탐색.
    /// 이미 인스펙터로 할당된 슬롯은 덮어쓰지 않는다.
    /// </summary>
    private void AutoAssignSlotPoints()
    {
        if (worldRoot == null)
        {
            Debug.LogWarning("[SLOT_AUTO] worldRoot가 null이라 SlotPoint 자동 탐색 생략");
            return;
        }

        Transform enemySpawnRoot = null;
        foreach (Transform child in worldRoot)
        {
            if (child.name == "EnemySpawnRoot") { enemySpawnRoot = child; break; }
        }

        if (enemySpawnRoot == null)
        {
            Debug.LogWarning("[SLOT_AUTO] EnemySpawnRoot를 worldRoot 하위에서 찾지 못함");
            return;
        }

        if (slotPointCenter == null)
            slotPointCenter = enemySpawnRoot.Find("SlotPoint_Center");

        if (slotPointsFront == null || slotPointsFront.Length != 4)
            slotPointsFront = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            if (slotPointsFront[i] == null)
                slotPointsFront[i] = enemySpawnRoot.Find($"SlotPoint_{i + 1}");
        }

        Debug.Log($"[SLOT_AUTO] Center={slotPointCenter?.name}, Front=[{string.Join(",", System.Array.ConvertAll(slotPointsFront, t => t?.name ?? "null"))}]");
    }

    // ─────────────────────────────────────────────────────────────
    // AllowedSlots 기반 배치 알고리즘 (n명별 고정 템플릿)
    // MonsterSO: MonsterRowPreference(Either/Front/Back) + MonsterAllowedSlotMask(1~4). 몬스터 수로 템플릿을
    // 고르고, 각 MonsterSO.AllowedSlots(유효 마스크)로 슬롯을 채운다.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 4슬롯 시스템의 스폰 패턴.
    /// slots = 몬스터가 들어갈 슬롯 인덱스(1~4, 정렬된 오름차순). 0은 Center 특수값(n=1 전용).
    /// frontCount = slots[0..frontCount-1]이 전열, slots[frontCount..]가 후열.
    ///   · frontCount == 0 : 전원 후열 (분단선 없음)
    ///   · frontCount == slots.Length : 전원 전열 (분단선 없음)
    ///   · 그 외 : divider_{slots[frontCount-1]} 활성
    /// label = 디버그 출력용 포메이션 이름 (예: "2/2", "3/1").
    /// </summary>
    private readonly struct SpawnPattern
    {
        public readonly int[] slots;
        public readonly int frontCount;
        public readonly string label;

        public SpawnPattern(int[] s, int fc, string l) { slots = s; frontCount = fc; label = l; }
    }

    // [Phase 1.5 Step 1] SPAWN_PATTERNS 제거 — 사용처 0건의 죽은 코드였음.
    //   실제 스폰은 AssignSlotsByAllowedSlots()가 담당(슬롯 1~4, 최대 4마리).

    /// <summary>
    /// monsters 배열 순서대로 대응하는 Transform 슬롯을 반환한다.
    /// 규칙: 1명=Center, 2명=1·2, 3명=1·2·3, 4명=1·2·3·4.
    /// 배치 우선순위: Front → Back → Either.
    /// (일반 스폰은 슬롯을 1~4 순서로 채운다. Row는 순서 힌트이며 슬롯 강제는 하지 않는다.)
    /// 분단선은 시스템 규칙이 아니라 UI 기준선이므로 2/2 중앙(단일 divider)로 고정한다.
    /// </summary>
    private Transform[] AssignSlotsByAllowedSlots(MonsterSO[] monsters)
    {
        int n = monsters.Length;
        Transform[] result = new Transform[n];

        if (n <= 0) return result;

        if (n == 1)
        {
            currentDividerSlotIdx = 0;
            result[0] = slotPointCenter;
            if (result[0] == null)
                Debug.LogWarning("[SPAWN_WARN] slotPointCenter가 null입니다. EnemySpawnRoot 하위에 SlotPoint_Center를 배치하세요.");
            return result;
        }

        int[] slotOrder = n switch
        {
            2 => new[] { 1, 2 },
            3 => new[] { 1, 2, 3 },
            4 => new[] { 1, 2, 3, 4 },
            _ => null
        };
        if (slotOrder == null)
        {
            Debug.LogWarning($"[SPAWN_WARN] 지원 몬스터 수는 1~4입니다. n={n}");
            return result;
        }

        // 분단선은 항상 12|34 중앙 고정
        currentDividerSlotIdx = 2;

        List<int> front = new List<int>();
        List<int> back = new List<int>();
        List<int> either = new List<int>();

        for (int i = 0; i < n; i++)
        {
            MonsterSO m = monsters[i];
            if (m == null) { either.Add(i); continue; }

            switch (m.RowPreference)
            {
                case MonsterRowPreference.Front:
                    front.Add(i);
                    break;
                case MonsterRowPreference.Back:
                    back.Add(i);
                    break;
                default:
                    either.Add(i);
                    break;
            }
        }

        List<int> placementOrder = new List<int>(n);
        placementOrder.AddRange(front);
        placementOrder.AddRange(back);
        placementOrder.AddRange(either);

        List<int> remainingSlots = new List<int>(slotOrder);

        foreach (int mi in placementOrder)
        {
            if (remainingSlots.Count == 0) break;
            int chosenSlot = remainingSlots[0];
            result[mi] = SlotTransformByIndex(chosenSlot);
            remainingSlots.Remove(chosenSlot);
        }

        Debug.Log($"[SPAWN] 고정 배치 n={n}, slots=[{string.Join(",", slotOrder)}], divider(12|34 고정)");
        return result;
    }

    /// <summary>
    /// SpawnPattern으로부터 분단선 슬롯 인덱스를 계산.
    /// 전원 전열(frontCount==slots.Length) 또는 전원 후열(frontCount==0) → 0 (분단선 없음).
    /// 그 외 → 전열 마지막 몬스터가 자리한 슬롯 인덱스.
    /// </summary>
    private static int ComputeDividerSlotIdx(SpawnPattern pattern)
    {
        if (pattern.slots == null || pattern.slots.Length == 0) return 0;
        if (pattern.frontCount <= 0) return 0;
        if (pattern.frontCount >= pattern.slots.Length) return 0;
        // slots는 오름차순이라고 가정. frontCount-1번째가 전열 마지막 몬스터.
        return pattern.slots[pattern.frontCount - 1];
    }

    /// <summary>
    /// 주어진 템플릿에 MonsterSO 배열을 MRV 방식으로 배치 시도한다.
    /// placedCount에는 성공적으로 슬롯을 받은 몬스터 수가 담긴다.
    /// </summary>
    private Transform[] TryPlaceWithTemplate(MonsterSO[] monsters, int[] template, out int placedCount)
    {
        int n = monsters.Length;
        Transform[] result = new Transform[n];
        placedCount = 0;

        List<int> available = new List<int>(template);
        List<int> remaining = new List<int>();
        for (int i = 0; i < n; i++) remaining.Add(i);

        while (remaining.Count > 0)
        {
            int pickMonster = -1;
            int pickSlot = -1;
            int pickRemIdx = -1;
            int bestCount = int.MaxValue;

            for (int k = 0; k < remaining.Count; k++)
            {
                int i = remaining[k];
                SlotMask a = monsters[i].AllowedSlots;

                int validCount = 0;
                int firstValid = -1;
                for (int s = 0; s < available.Count; s++)
                {
                    int slotIdx = available[s];
                    if (SlotIsAllowed(slotIdx, a))
                    {
                        validCount++;
                        if (firstValid == -1) firstValid = slotIdx;
                    }
                }

                if (validCount > 0 && validCount < bestCount)
                {
                    bestCount = validCount;
                    pickMonster = i;
                    pickSlot = firstValid;
                    pickRemIdx = k;
                }
            }

            if (pickMonster == -1) break; // 더 이상 배치 불가

            result[pickMonster] = SlotTransformByIndex(pickSlot);
            available.Remove(pickSlot);
            remaining.RemoveAt(pickRemIdx);
            placedCount++;
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // ● 이동 시스템 — Phase 1: 조회 API (Read-only)
    //   상태 변경 없이 현재 포메이션을 조회한다.
    //   이 메서드들은 어떤 필드도 수정하지 않으며, 실패 시 null/false를
    //   반환할 뿐 부작용이 없다. 이동 로직 테스트의 진단용으로 사용.
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 지정된 슬롯에 있는 살아있는 몬스터를 반환. 없으면 null.
    /// currentSlot(BattleSlot enum) 기준 참조 매칭이라 월드 좌표 영향 없음.
    /// </summary>
    public EnemyStats GetMonsterAtSlot(BattleSlot slot)
    {
        if (slot == BattleSlot.None) return null;
        if (activeEnemies == null) return null;

        foreach (EnemyStats es in activeEnemies)
        {
            if (es == null) continue;
            if (es.IsDead()) continue;
            if (es.currentSlot == slot) return es;
        }
        return null;
    }

    /// <summary>
    /// UI 디스플레이 번호(1-based, AssignDisplayNumbers와 동일 규칙)로 몬스터 조회.
    /// 규칙: 살아있는 몬스터를 currentSlot 오름차순 정렬 후 n번째를 반환.
    /// 범위 밖이면 null.
    /// </summary>
    public EnemyStats GetMonsterByNumber(int displayNumber)
    {
        if (displayNumber < 1) return null;
        if (activeEnemies == null) return null;

        List<(EnemyStats stats, int slotKey)> ordered = new List<(EnemyStats, int)>();
        foreach (EnemyStats es in activeEnemies)
        {
            if (es == null) continue;
            if (es.IsDead()) continue;
            int key = (int)es.currentSlot;
            if (key == 0) key = int.MaxValue;
            ordered.Add((es, key));
        }
        ordered.Sort((a, b) => a.slotKey.CompareTo(b.slotKey));

        int idx = displayNumber - 1;
        if (idx >= ordered.Count) return null;
        return ordered[idx].stats;
    }

    /// <summary>
    /// 현재 포메이션을 콘솔에 출력한다. 디버그 전용, 상태 변경 없음.
    /// 4슬롯 시스템: 전/후열 구분은 currentDividerSlotIdx 기반.
    /// 출력 예:
    ///   [FORMATION] 총 4마리 / divider_2 (전열 2 : 후열 2)
    ///     Slot1=Rat(#1, HP 35/35) Slot2=Skel(#2) | Slot3=Wizard(#3) Slot4=Bat(#4)
    /// </summary>
    public void DebugPrintFormation()
    {
        if (activeEnemies == null || activeEnemies.Count == 0)
        {
            Debug.Log("[FORMATION] 활성 몬스터 없음");
            return;
        }

        // 살아있는 몬스터를 슬롯 오름차순으로 정렬
        List<(EnemyStats stats, int slotKey)> ordered = new List<(EnemyStats, int)>();
        foreach (EnemyStats es in activeEnemies)
        {
            if (es == null) continue;
            if (es.IsDead()) continue;
            int key = (int)es.currentSlot;
            if (key == 0) key = int.MaxValue;
            ordered.Add((es, key));
        }
        ordered.Sort((a, b) => a.slotKey.CompareTo(b.slotKey));

        // 슬롯 → (몬스터, 번호) 맵 구축 (디스플레이 번호는 AssignDisplayNumbers와 동일 규칙)
        Dictionary<BattleSlot, (EnemyStats stats, int number)> map = new Dictionary<BattleSlot, (EnemyStats, int)>();
        for (int i = 0; i < ordered.Count; i++)
        {
            map[ordered[i].stats.currentSlot] = (ordered[i].stats, i + 1);
        }

        // 4슬롯 한 줄 출력. currentDividerSlotIdx 위치에 '|' 표시로 전/후열 구분 시각화.
        System.Text.StringBuilder rowSb = new System.Text.StringBuilder("슬롯: ");
        int leftCount = 0, rightCount = 0;
        for (int i = 1; i <= 4; i++)
        {
            BattleSlot s = (BattleSlot)i;
            if (map.TryGetValue(s, out var pair))
            {
                rowSb.Append($"Slot{i}={pair.stats.enemyName}(#{pair.number}, HP {pair.stats.currentHP}/{pair.stats.maxHP}) ");
                if (i <= currentDividerSlotIdx) leftCount++; else rightCount++;
            }
            else
            {
                rowSb.Append($"Slot{i}=- ");
            }
            if (i == currentDividerSlotIdx && currentDividerSlotIdx > 0 && currentDividerSlotIdx < 4)
                rowSb.Append("| ");
        }

        // Center 슬롯(Solo)도 별도 표기
        if (map.TryGetValue(BattleSlot.Center, out var centerPair))
            rowSb.Append($"Center={centerPair.stats.enemyName}(#{centerPair.number}, HP {centerPair.stats.currentHP}/{centerPair.stats.maxHP})");

        string dividerLabel = (currentDividerSlotIdx >= 1 && currentDividerSlotIdx <= 3)
            ? $"divider_{currentDividerSlotIdx} (전열 {leftCount} : 후열 {rightCount})"
            : "분단선 없음";

        Debug.Log($"[FORMATION] 총 {ordered.Count}마리 / {dividerLabel}\n  {rowSb}");

        // ── 진단용 Raw Dump ──
        // activeEnemies 전체를 무가공 상태로 출력. F12 출력과 실제 스폰 로그/게임 뷰를
        // 교차 대조하기 위한 정보. currentSlot 내부값 vs 월드 좌표를 동시에 보여준다.
        System.Text.StringBuilder raw = new System.Text.StringBuilder("[FORMATION_RAW]\n");
        raw.Append($"  activeEnemies.Count = {activeEnemies.Count}\n");
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyStats es = activeEnemies[i];
            if (es == null)
            {
                raw.Append($"  [{i}] NULL 엔트리\n");
                continue;
            }
            Vector3 pos = es.transform != null ? es.transform.position : Vector3.zero;
            string row = SlotHelper.IsFrontRow(es.currentSlot) ? "FRONT" :
                         SlotHelper.IsBackRow(es.currentSlot) ? "BACK" :
                         es.currentSlot == BattleSlot.Center ? "CENTER" : "NONE";
            bool dead = es.IsDead();
            raw.Append($"  [{i}] {es.enemyName} | currentSlot={es.currentSlot}({(int)es.currentSlot}) [{row}] | world=({pos.x:F2},{pos.y:F2}) | HP={es.currentHP}/{es.maxHP} | Dead={dead}\n");
        }

        // ── 슬롯 배열 상태 진단 ──
        // slotPointsFront/Back/Center가 실제로 어떤 GameObject를 가리키는지 확인.
        // 두 배열이 같은 오브젝트를 가리키면 GetBattleSlotFromTransform이 오작동한다.
        raw.Append("\n  [Slot Arrays]\n");
        raw.Append($"  slotPointCenter = {(slotPointCenter == null ? "null" : slotPointCenter.name)}\n");
        raw.Append("  slotPointsFront = [");
        if (slotPointsFront != null)
        {
            for (int k = 0; k < slotPointsFront.Length; k++)
            {
                string nm = slotPointsFront[k] == null ? "null" : slotPointsFront[k].name;
                int iid = slotPointsFront[k] == null ? 0 : slotPointsFront[k].GetInstanceID();
                raw.Append($"{k}:{nm}(id={iid})");
                if (k < slotPointsFront.Length - 1) raw.Append(", ");
            }
        }
        raw.Append("]\n");

        // 교차 매핑 검증: 각 몬스터 Transform이 어느 slot 배열과 매칭되는지
        raw.Append("\n  [Transform 매칭 검증]\n");
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyStats es = activeEnemies[i];
            if (es == null || es.transform == null) continue;
            Vector3 p = es.transform.position;
            // 각 몬스터의 현재 월드 위치가 실제로 어느 SlotPoint 좌표에 가까운지
            string closest = "?";
            float bestDist = float.MaxValue;
            void Check(Transform t, string label)
            {
                if (t == null) return;
                float d = (t.position - p).sqrMagnitude;
                if (d < bestDist) { bestDist = d; closest = label; }
            }
            if (slotPointsFront != null)
                for (int k = 0; k < slotPointsFront.Length; k++) Check(slotPointsFront[k], $"Front[{k}]({slotPointsFront[k]?.name})");
            Check(slotPointCenter, $"Center({slotPointCenter?.name})");
            raw.Append($"  [{i}] {es.enemyName} world=({p.x:F2},{p.y:F2}) → 가장 가까운 SlotPoint: {closest} (dist²={bestDist:F3})\n");
        }

        Debug.Log(raw.ToString());
    }

    // ══════════════════════════════════════════════════════════════
    // ● 이동 시스템 — Phase 2: 데이터 레이어 (No Animation)
    //   currentSlot과 월드 위치를 즉시 갱신한다. 애니메이션/UI 추종은
    //   Phase 3에서 추가. 이동은 항상 AllowedSlots를 기본 검증하며,
    //   강제 이동(푸시/풀/셔플 등 스킬 유래)은 forced=true로 우회한다.
    //   이동 후 AssignDisplayNumbers + UpdateDivider + enemyLine 동기화.
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 이동 테스트용 선택 몬스터. 숫자키 1~7로 지정된 순간의 EnemyStats를 참조로 보관한다.
    /// 이후 이동으로 번호(#N)가 바뀌어도 같은 적을 계속 조작할 수 있다.
    /// null이거나 죽은 상태면 fallback으로 #1을 쓴다.
    /// </summary>
    private EnemyStats moveTestSelectedEnemy;

    /// <summary>
    /// 숫자키 1~7을 검사해 현재 디스플레이 번호 기준으로 moveTestSelectedEnemy를 갱신한다.
    /// </summary>
    private void HandleMoveSelectionKeys()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            List<EnemyStats> ordered = new List<EnemyStats>();
            if (activeEnemies != null)
            {
                foreach (EnemyStats es in activeEnemies)
                {
                    if (es == null || es.IsDead()) continue;
                    int idx = (int)es.currentSlot;
                    if (idx >= 1 && idx <= 4) ordered.Add(es);
                }
            }

            ordered.Sort((a, b) => ((int)a.currentSlot).CompareTo((int)b.currentSlot));
            if (ordered.Count == 0)
            {
                moveTestSelectedEnemy = null;
                Debug.Log("[MOVE_SELECT/F8] 선택 가능한 몬스터 없음");
                return;
            }

            int cur = -1;
            if (moveTestSelectedEnemy != null)
                cur = ordered.IndexOf(moveTestSelectedEnemy);
            int next = (cur + 1 + ordered.Count) % ordered.Count;
            moveTestSelectedEnemy = ordered[next];

            int displayNum = next + 1;
            Debug.Log($"[MOVE_SELECT/F8] 선택: #{displayNum} {moveTestSelectedEnemy.enemyName} (Slot{(int)moveTestSelectedEnemy.currentSlot})");
            return;
        }

        for (int n = 1; n <= 7; n++)
        {
            KeyCode key = KeyCode.Alpha0 + n;
            if (Input.GetKeyDown(key))
            {
                EnemyStats picked = GetMonsterByNumber(n);
                if (picked == null)
                {
                    Debug.Log($"[MOVE_SELECT] #{n} 몬스터 없음");
                }
                else
                {
                    moveTestSelectedEnemy = picked;
                    Debug.Log($"[MOVE_SELECT] #{n} {picked.enemyName} ({picked.currentSlot}) 선택됨");
                }
                return;
            }
        }
    }

    /// <summary>
    /// 현재 선택된 이동 테스트 대상. 없거나 죽었으면 #1로 fallback.
    /// </summary>
    private EnemyStats GetMoveTestTarget()
    {
        if (moveTestSelectedEnemy != null && !moveTestSelectedEnemy.IsDead())
            return moveTestSelectedEnemy;
        return GetMonsterByNumber(1);
    }

    /// <summary>
    /// F10(좌) / F9(우) / F11(셔플) 이동 액션 핫키 처리.
    /// 4슬롯 1~4 한 줄에서 한 칸씩. 분단선·AllowedSlots 무시(forced + 분단선 인덱스 초기화).
    /// </summary>
    private void HandleMoveActionKeys()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            EnemyStats e = GetMoveTestTarget();
            if (e == null) { Debug.Log("[MOVE_TEST/F10] 대상 몬스터 없음"); }
            else
            {
                BattleSlot left = GetHorizontalNeighbor4(e.currentSlot, -1);
                if (left == BattleSlot.None)
                    Debug.Log($"[MOVE_TEST/F10] {e.enemyName} {e.currentSlot}의 왼쪽 이웃 없음 (끝)");
                else
                    MoveToSlot(e, left, forced: true, clearFormationDivider: true);
            }
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            EnemyStats e = GetMoveTestTarget();
            if (e == null) { Debug.Log("[MOVE_TEST/F9] 대상 몬스터 없음"); }
            else
            {
                BattleSlot right = GetHorizontalNeighbor4(e.currentSlot, 1);
                if (right == BattleSlot.None)
                    Debug.Log($"[MOVE_TEST/F9] {e.enemyName} {e.currentSlot}의 오른쪽 이웃 없음 (끝)");
                else
                    MoveToSlot(e, right, forced: true, clearFormationDivider: true);
            }
        }

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && Input.GetKeyDown(KeyCode.F3))
        {
            ShuffleAllEnemies();
        }
    }

    /// <summary>
    /// 슬롯별 결정적 sortingOrder.
    /// 후열(Slot5~7) = 10..12, 전열(Slot1~4) = 20..23.
    /// 같은 행 내에서도 슬롯 번호가 클수록(오른쪽일수록) 상위에 그려져 스프라이트 겹침이 결정적이 된다.
    /// 이동·셔플 이후에도 이 규칙만 적용하면 시각적 정렬이 흐트러지지 않는다.
    /// </summary>
    private int GetSortingOrderForSlot(BattleSlot slot)
    {
        int idx = (int)slot;
        if (idx >= 5 && idx <= 7) return 10 + (idx - 5); // 10, 11, 12
        if (idx >= 1 && idx <= 4) return 20 + (idx - 1); // 20, 21, 22, 23
        return 20;
    }

    /// <summary>
    /// 이동 API 결과 코드. 실패 사유를 명확히 구분한다.
    /// </summary>
    public enum MoveResult
    {
        Success_Moved,      // 빈 슬롯으로 이동
        Success_Swapped,    // 점유 슬롯과 스왑
        Fail_InvalidArgs,   // mover null / 죽음 / target None
        Fail_SameSlot,      // 이미 해당 슬롯
        Fail_AllowedSlots,  // AllowedSlots 위반 (forced=false일 때)
        Fail_NoTransform,   // 목표 슬롯 Transform이 씬에 없음
    }

    // [Phase 1.5 Step 1] GetHorizontalNeighbor(비4, 5~7 처리) 제거 — 호출자 0건의 죽은 코드.
    //   살아있는 수평 이동은 아래 GetHorizontalNeighbor4(1~4 전용)가 담당.

    /// <summary>
    /// 4슬롯 전용(1~4) 수평 이웃. 분단선·전후열 UI와 무관하게 한 줄로만 이어진다.
    /// </summary>
    private static BattleSlot GetHorizontalNeighbor4(BattleSlot slot, int direction)
    {
        int idx = (int)slot;
        if (direction != -1 && direction != 1) return BattleSlot.None;
        if (idx < 1 || idx > 4) return BattleSlot.None;
        int next = idx + direction;
        if (next >= 1 && next <= 4) return (BattleSlot)next;
        return BattleSlot.None;
    }

    // [Phase 1.5 Step 1] GetNearestSlotInOppositeRow 제거 — 호출자 0건의 죽은 코드.
    //   슬롯 5~7을 목표로 하던 전↔후열 밀치기/당기기 매핑(7슬롯 잔재).

    /// <summary>
    /// 핵심 이동 API. 빈 슬롯이면 Move, 점유 슬롯이면 Swap.
    /// forced=false(기본): AllowedSlots 엄격 검증. 실패 시 상태 변경 없이 Fail_AllowedSlots.
    /// forced=true: 푸시/셔플 등 스킬 강제 이동. AllowedSlots 무시.
    /// clearFormationDivider=true: 성공 시 스폰 분단선 인덱스를 0으로(한 줄 자유 이동용).
    /// 성공 시 currentSlot + 월드 위치 갱신, 전/후열 sortingOrder 재설정,
    /// AssignDisplayNumbers + UpdateDivider + enemyLine 동기화.
    /// 애니메이션 없음 (Phase 3에서 추가).
    /// </summary>
    public MoveResult MoveToSlot(EnemyStats mover, BattleSlot target, bool forced = false, bool clearFormationDivider = false)
    {
        if (mover == null || mover.IsDead()) return MoveResult.Fail_InvalidArgs;
        if (target == BattleSlot.None) return MoveResult.Fail_InvalidArgs;
        if (mover.currentSlot == target) return MoveResult.Fail_SameSlot;

        int targetIdx = (int)target;
        if (targetIdx < 1 || targetIdx > 7) return MoveResult.Fail_InvalidArgs;

        Transform targetTransform = SlotTransformByIndex(targetIdx);
        if (targetTransform == null) return MoveResult.Fail_NoTransform;

        // 이동자의 AllowedSlots 검증 (엄격 모드)
        if (!forced && !SlotIsAllowed(targetIdx, mover.allowedSlots))
        {
            Debug.LogWarning($"[MOVE_FAIL] {mover.enemyName} {mover.currentSlot} → {target} 차단됨 (AllowedSlots 위반)");
            return MoveResult.Fail_AllowedSlots;
        }

        EnemyStats occupant = GetMonsterAtSlot(target);

        // 빈 슬롯 → 단순 이동
        if (occupant == null)
        {
            BattleSlot from = mover.currentSlot;
            ApplySlotAssignment(mover, target, targetTransform);
            if (clearFormationDivider) currentDividerSlotIdx = 2;
            PostMoveRefresh();
            Debug.Log($"[MOVE] {mover.enemyName} {from} → {target} (이동)" + (forced ? " [forced]" : ""));
            return MoveResult.Success_Moved;
        }

        // 점유 슬롯 → 스왑. 스왑은 상대의 AllowedSlots도 검증.
        if (!forced && !SlotIsAllowed((int)mover.currentSlot, occupant.allowedSlots))
        {
            Debug.LogWarning($"[MOVE_FAIL] {mover.enemyName} ↔ {occupant.enemyName} 스왑 차단됨 (상대 AllowedSlots 위반)");
            return MoveResult.Fail_AllowedSlots;
        }

        BattleSlot moverFrom = mover.currentSlot;
        Transform moverOriginTransform = SlotTransformByIndex((int)moverFrom);
        ApplySlotAssignment(mover, target, targetTransform);
        ApplySlotAssignment(occupant, moverFrom, moverOriginTransform);
        if (clearFormationDivider) currentDividerSlotIdx = 2;
        PostMoveRefresh();
        Debug.Log($"[MOVE] {mover.enemyName} {moverFrom} → {target} (swap with {occupant.enemyName})" + (forced ? " [forced]" : ""));
        return MoveResult.Success_Swapped;
    }

    /// <summary>아군(PlayerStats)을 지정 슬롯으로 이동. currentSlot 갱신 + 리스트 순서 교체 + UI 갱신.</summary>
    private bool MovePlayerToSlot(PlayerStats mover, BattleSlot targetSlot)
    {
        if (mover == null || mover.currentHP <= 0) return false;
        if (targetSlot == BattleSlot.None) return false;
        if (mover.currentSlot == targetSlot) return false;

        // 목표 슬롯에 있는 다른 파티원 찾기 (스왑용)
        PlayerStats occupant = null;
        foreach (var m in activePartyMembers)
        {
            if (m != null && m != mover && m.currentSlot == targetSlot)
            {
                occupant = m;
                break;
            }
        }

        BattleSlot fromSlot = mover.currentSlot;

        if (occupant == null)
        {
            // 빈 슬롯: 단순 이동
            mover.currentSlot = targetSlot;
        }
        else
        {
            // 점유 슬롯: 스왑
            mover.currentSlot = targetSlot;
            occupant.currentSlot = fromSlot;
        }

        // activePartyMembers를 currentSlot 순서로 정렬 후 위치 갱신
        activePartyMembers.Sort((a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;
            return ((int)a.currentSlot).CompareTo((int)b.currentSlot);
        });
        PositionPartyMembers();
        UpdateStatusUI();

        Debug.Log($"[PlayerMove] {mover.playerName} {fromSlot} → {targetSlot}" +
                  (occupant != null ? $" (swap with {occupant.playerName})" : ""));
        return true;
    }

    /// <summary>
    /// currentSlot + 월드 위치 + 렌더링 순서를 한 번에 갱신한다.
    /// HoverOffsetY는 (현재 월드 Y - 이전 슬롯 Y)로 역산해 새 슬롯에서도 유지된다.
    /// 이전 슬롯 Transform이 없으면 HoverY=0으로 처리.
    /// </summary>
    private void ApplySlotAssignment(EnemyStats es, BattleSlot newSlot, Transform newSlotTransform)
    {
        if (es == null || newSlotTransform == null) return;

        float hoverY = 0f;
        Transform prevSlot = SlotTransformByIndex((int)es.currentSlot);
        if (prevSlot != null && es.transform != null)
        {
            hoverY = es.transform.position.y - prevSlot.position.y;
        }

        es.currentSlot = newSlot;
        if (es.transform != null)
        {
            Vector3 pos = newSlotTransform.position;
            pos.y += hoverY;
            es.transform.position = pos;
        }

        // 슬롯별 결정적 sortingOrder (겹침 순서 고정)
        SpriteRenderer sr = es.GetComponent<SpriteRenderer>() ?? es.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = GetSortingOrderForSlot(newSlot);
        }
    }

    /// <summary>
    /// 이동 연산 후 부수 갱신: 번호 재배정 + 분단선 갱신 + enemyLine(전열 4슬롯) 재동기화.
    /// Phase 2에서는 즉시 스냅 방식이라 애니 중 추종 처리는 없다.
    /// </summary>
    private void PostMoveRefresh()
    {
        AssignDisplayNumbers();
        UpdateDividerFromActualLayout();

        // 레거시 enemyLine은 전열(Slot1~4)만 보관. 후열은 activeEnemies에서만 조회.
        enemyLine.Clear();
        foreach (var e in activeEnemies)
        {
            if (e == null) continue;
            int idx = (int)e.currentSlot;
            if (idx >= 1 && idx <= 4) enemyLine.AssignToSlot(e, idx);
        }
    }

    /// <summary>
    /// 적 전체를 현재 점유 중인 슬롯 집합 안에서 무작위로 재배치한다.
    /// 슬롯 집합은 보존·몬스터만 섞음. 스폰 분단선 인덱스는 0으로 초기화(한 줄 셔플).
    /// AllowedSlots는 강제로 무시(셔플은 본질적으로 forced 이동).
    /// Phase 3에서 페이드/슬라이드 연출 추가 예정.
    /// </summary>
    public void ShuffleAllEnemies()
    {
        List<EnemyStats> alive = new List<EnemyStats>();
        List<BattleSlot> slots = new List<BattleSlot>();
        foreach (var e in activeEnemies)
        {
            if (e == null || e.IsDead()) continue;
            BattleSlot s = e.currentSlot;
            if (s == BattleSlot.None || s == BattleSlot.Center) continue;
            alive.Add(e);
            slots.Add(s);
        }

        if (alive.Count < 2)
        {
            Debug.Log("[SHUFFLE] 대상이 2마리 미만이라 섞을 필요 없음");
            return;
        }

        // Fisher–Yates 셔플 (alive 리스트 순서만)
        for (int i = alive.Count - 1; i > 0; i--)
        {
            int r = UnityEngine.Random.Range(0, i + 1);
            (alive[i], alive[r]) = (alive[r], alive[i]);
        }

        // alive[i] → slots[i] 매핑으로 재배치. currentSlot을 먼저 전원 None으로 초기화해
        // GetMonsterAtSlot 충돌을 방지한다.
        // HoverY 역산은 이 시점에서 손실되므로(prevSlot이 None) HoverY=0으로 snap.
        // 셔플은 연출용이라 Phase 3 애니에서 hoverY 별도 복원 예정.
        foreach (var e in alive) e.currentSlot = BattleSlot.None;

        for (int i = 0; i < alive.Count; i++)
        {
            EnemyStats e = alive[i];
            BattleSlot target = slots[i];
            Transform t = SlotTransformByIndex((int)target);
            if (t != null && e.transform != null)
            {
                e.transform.position = t.position;
            }
            e.currentSlot = target;
            SpriteRenderer sr = e.GetComponent<SpriteRenderer>() ?? e.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = GetSortingOrderForSlot(target);
        }

        currentDividerSlotIdx = 2;
        PostMoveRefresh();
        Debug.Log($"[SHUFFLE] 적 {alive.Count}마리 재배치 완료");
    }

    /// <summary>
    /// 전열(1~4) 좌→우, 후열(5~7) 좌→우 순서로 활성 몬스터에게 순번(1부터)을 부여하고
    /// EnemyUIDisplay.SetNumber를 통해 UI Number 텍스트를 갱신한다.
    /// 정렬 키는 EnemyStats.currentSlot(BattleSlot enum)이며, 스폰 시점에 세팅된 값을 사용한다.
    /// (HoverOffsetY 때문에 위치가 슬롯 좌표와 어긋나도 영향을 받지 않는다.)
    /// </summary>
    private void AssignDisplayNumbers()
    {
        if (activeEnemies == null || activeEnemies.Count == 0) return;

        // 슬롯 번호(BattleSlot enum 정수값)로 정렬하기 위한 리스트 구성
        // Slot1=1, Slot2=2, ..., Slot7=7, Center=8 → 단순 오름차순
        List<(EnemyStats stats, int slotKey)> ordered = new List<(EnemyStats, int)>();
        foreach (EnemyStats es in activeEnemies)
        {
            if (es == null) continue;
            int key = (int)es.currentSlot;
            // None(0)인 경우는 맨 뒤로 (정상이면 발생하지 않음)
            if (key == 0) key = int.MaxValue;
            ordered.Add((es, key));
        }

        ordered.Sort((a, b) => a.slotKey.CompareTo(b.slotKey));

        for (int n = 0; n < ordered.Count; n++)
        {
            EnemyStats es = ordered[n].stats;
            if (es == null) continue;
            EnemyUIDisplay ui = es.GetComponent<EnemyUIDisplay>()
                               ?? es.GetComponentInChildren<EnemyUIDisplay>();
            if (ui == null) continue;

            ui.SetNumber(n + 1);

            // UI 위치 결정: 씬 앵커 우선 → 자동 레이아웃 → 아무것도 없으면 EnemyUIDisplay 기본 모드
            if (enemyUIAnchors != null && n < enemyUIAnchors.Length && enemyUIAnchors[n] != null)
            {
                ui.SetUIAnchor(enemyUIAnchors[n]);
            }
            else if (useAutoUILayout)
            {
                Vector3 pos = uiLayoutOrigin + Vector3.right * (n * uiLayoutSpacingX);
                ui.SetUIAnchorPosition(pos);
            }
        }
    }

    /// <summary>
    /// 슬롯 Transform을 참조 비교(== 연산자)로 대조해 BattleSlot enum 값을 반환한다.
    /// 위치 비교가 아니므로 HoverOffsetY 등으로 몬스터 월드 좌표가 흔들려도 정확하다.
    /// 매칭 실패 시 BattleSlot.None 반환.
    /// </summary>
    private BattleSlot GetBattleSlotFromTransform(Transform slot)
    {
        if (slot == null) return BattleSlot.None;

        if (slotPointsFront != null)
        {
            for (int k = 0; k < slotPointsFront.Length; k++)
            {
                if (slotPointsFront[k] == slot) return (BattleSlot)(k + 1); // Slot1~Slot4
            }
        }
        if (slotPointCenter == slot) return BattleSlot.Center;
        return BattleSlot.None;
    }

    /// <summary>
    /// 주어진 Transform이 어느 SlotPoint에 배치되어 있는지 1-based 번호로 반환.
    /// 전열=1~4, 후열=5~7, Center=0, 알 수 없음=0.
    /// Transform 자체가 아닌 위치 기반 비교로 동작한다.
    /// </summary>
    private int GetSlotIndexFromPosition(Transform target)
    {
        if (target == null) return 0;
        const float eps = 0.01f;

        if (slotPointsFront != null)
        {
            for (int k = 0; k < slotPointsFront.Length; k++)
            {
                if (slotPointsFront[k] == null) continue;
                if ((slotPointsFront[k].position - target.position).sqrMagnitude < eps)
                    return k + 1; // 1~4
            }
        }
        if (slotPointCenter != null &&
            (slotPointCenter.position - target.position).sqrMagnitude < eps)
            return 0; // Center
        return 0;
    }

    /// <summary>
    /// 1-based 슬롯 인덱스(1~4)가 주어진 SlotMask에 허용되는지 확인.
    /// 마스크에 Slot1~4 비트가 없으면 1~4 전체 허용(구 데이터·동료 등 방어).
    /// </summary>
    private bool SlotIsAllowed(int slotIdx1Based, SlotMask mask)
    {
        SlotMask bit = slotIdx1Based switch
        {
            1 => SlotMask.Slot1,
            2 => SlotMask.Slot2,
            3 => SlotMask.Slot3,
            4 => SlotMask.Slot4,
            _ => SlotMask.None
        };
        if (bit == SlotMask.None) return false;

        const SlotMask FOUR_SLOT_MASK = SlotMask.Slot1 | SlotMask.Slot2 | SlotMask.Slot3 | SlotMask.Slot4;
        if ((mask & FOUR_SLOT_MASK) == 0) return true;

        return (mask & bit) != 0;
    }

    /// <summary>
    /// 리스트에서 used[]가 false인 첫 번째 슬롯 인덱스를 꺼내 사용 처리하고 반환. 없으면 0.
    /// </summary>
    private int TakeFirstFree(List<int> indexList, bool[] used)
    {
        for (int k = 0; k < indexList.Count; k++)
        {
            if (!used[k])
            {
                used[k] = true;
                return indexList[k];
            }
        }
        return 0;
    }

    /// <summary>
    /// 후열 슬롯(5,6,7)만 체크되어 있고 전열/Center 비트는 없는지 확인
    /// </summary>
    private bool IsBackOnly(SlotMask a)
    {
        return (a & SlotMask.Back) != 0 && (a & SlotMask.Front) == 0;
    }

    /// <summary>
    /// 전열 슬롯(1,2,3,4)만 체크되어 있고 후열 비트는 없는지 확인
    /// </summary>
    private bool IsFrontOnly(SlotMask a)
    {
        return (a & SlotMask.Front) != 0 && (a & SlotMask.Back) == 0;
    }

    /// <summary>
    /// 1-based 슬롯 인덱스(1~7)를 실제 Transform으로 변환. Center는 제외.
    /// </summary>
    private Transform SlotTransformByIndex(int idx1Based)
    {
        if (idx1Based >= 1 && idx1Based <= 4 && slotPointsFront != null && slotPointsFront.Length >= 4)
            return slotPointsFront[idx1Based - 1];
        return null;
    }

/// <summary>
    /// 분단선은 항상 12|34 중앙 고정.
    /// 시스템 판정에는 관여하지 않고, 전열/후열 시각 가이드로만 사용한다.
    /// </summary>
    private void UpdateDividerFromActualLayout()
    {
        currentDividerSlotIdx = 2;

        if (divider != null)
        {
            divider.SetActive(true);
            return;
        }

        Debug.LogWarning("[DIVIDER] divider(단일 분단선)가 Inspector에 연결되지 않았습니다.");
    }

    // [Phase 1.5 Step 4] GetSlotsForFormation 제거 — 호출자 0건의 죽은 레거시 메서드(slotPointsBack 의존).
    //   실제 스폰은 AssignSlotsByAllowedSlots()가 담당. FormationType enum은 보존(다른 곳에서 쓰일 수 있음).


    void OnEnable()
    {
        ForceDisableUIPanels();
    }

    void Start()
    {
        ForceDisableUIPanels();
        StartCoroutine(EnsureUIPanelsClosedAfterFrames(3));

        // 버튼 리스너 초기화 — BattleUIManager가 있으면 Item·Skill 메인 버튼은 UI 매니저가 전담 (토글·패널 연동)
        BattleUIManager battleUIMgr =
            battleUIManager != null ? battleUIManager : FindFirstObjectByType<BattleUIManager>();

        if (attackButton != null) attackButton.onClick.RemoveAllListeners();
        if (skillButton != null && battleUIMgr == null) skillButton.onClick.RemoveAllListeners();
        if (skillBackButton != null) skillBackButton.onClick.RemoveAllListeners();
        if (itemButton != null && battleUIMgr == null) itemButton.onClick.RemoveAllListeners();
        if (fleeButton != null) fleeButton.onClick.RemoveAllListeners();
        if (defendButton != null) defendButton.onClick.RemoveAllListeners();

        // Back 버튼 찾기 (Start에서도 찾기)
        FindAndConnectBackButton();

        // 페이지네이션 UI 자동 연결 시도
        TryAutoAssignPaginationUI();

        // 버튼 연결
        if (attackButton != null) attackButton.onClick.AddListener(OnAttackButton);
        if (skillButton != null && battleUIMgr == null) skillButton.onClick.AddListener(OnSkillButton);
        if (skillBackButton != null) skillBackButton.onClick.AddListener(OnSkillBack);
        if (itemButton != null && battleUIMgr == null) itemButton.onClick.AddListener(OnItemButton);
        if (fleeButton != null) fleeButton.onClick.AddListener(OnRunButton);
        if (defendButton != null) defendButton.onClick.AddListener(OnDefendButton);

        // 파티 초기화
        InitializeParty();

        // 전투 시작 시 potionCount를 인벤토리와 동기화
        SyncPotionCount();

        // 스킬 캐시 초기화
        CacheHeroSkills();

        // BattleRecorder 초기화
        if (battleRecorder == null)
        {
            GameObject recorderObj = new GameObject("BattleRecorder");
            battleRecorder = recorderObj.AddComponent<BattleRecorder>();
        }
        
        // 로그 UI 등록
        if (battleRecorder != null && messageText != null)
        {
            battleRecorder.RegisterLogUI(messageText);
        }
        
        StartBattle();
    }

    void Update()
    {
        if (Input.GetKeyDown(soloPartyHotkey)) SetPartyMode(PartyMode.Solo);
        if (Input.GetKeyDown(fullPartyHotkey)) SetPartyMode(PartyMode.Full);

        // ── 이동 시스템 디버그 (Phase 1) ──
        // F12: 현재 포메이션을 콘솔에 출력 (읽기 전용, 상태 변경 없음)
        if (Input.GetKeyDown(KeyCode.F12))
        {
            DebugPrintFormation();
        }

        // ── 이동 시스템 테스트 핫키 (4슬롯 좌우 전용) ──
        // 숫자키 1~4: 현재 #N 몬스터를 "이동 선택" 상태로 고정
        // F10: 선택 몬스터를 왼쪽으로 한 칸 (분단선·AllowedSlots 무시)
        // F9 : 선택 몬스터를 오른쪽으로 한 칸 (동일)
        // F11: 적 전체 랜덤 셔플 (forced)
        HandleMoveSelectionKeys();
        HandleMoveActionKeys();

        // 타겟 전환(좌우 방향키)
        if (!battleEnded && activeEnemies.Count > 1 && !waitingForTargetSelection)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ChangeTarget(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeTarget(1);
        }

        // -------------------- 취소/Back 기능 --------------------
        // 타겟 선택 중일 때 취소 (ESC 키)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelButton();
        }

        // 우클릭 취소 (개발자용)
        if (Input.GetMouseButtonDown(1))
        {
            OnCancelButton();
        }

        // 타겟 선택 모드 처리
        if (waitingForTargetSelection)
        {
            bool targetSelected = HandleTargetSelection();
            HandleEnemyHover(); // 타겟 선택 모드일 때만 하이라이트

            // 타겟을 선택하지 않았고, 빈 공간을 클릭했다면 취소
            if (!targetSelected && Input.GetMouseButtonDown(0))
            {
                if (battleUIManager != null && battleUIManager.IsTouchingBackground())
                {
                    OnCancelButton();
                }
            }
        }
        else
        {
            // 타겟 선택 모드가 아닐 때 빈 공간 터치 취소 (메뉴 닫기 등)
            if (Input.GetMouseButtonDown(0))
            {
                if (battleUIManager != null && battleUIManager.fightSubPanel != null && battleUIManager.IsTouchingBackground())
                {
                    if (battleUIManager.fightSubPanel.activeSelf)
                    {
                        OnCancelButton();
                    }
                }
            }

            // 타겟 선택 모드가 아니면 하이라이트 제거
            if (hoveredEnemy != null)
            {
                hoveredEnemy.SetHighlight(false);
                hoveredEnemy = null;
            }
        }
    }

    private bool HandleTargetSelection()
    {
        // 마우스 클릭으로 타겟 선택
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            if (Camera.main == null) return false;

            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;

            Collider2D[] colliders = Physics2D.OverlapPointAll(mousePos);
            EnemyStats clickedEnemy = null;

            foreach (var col in colliders)
            {
                clickedEnemy = col.GetComponent<EnemyStats>();
                if (clickedEnemy != null && clickedEnemy.currentHP > 0) break;
            }

            if (clickedEnemy != null)
            {
                if (hoveredEnemy != null && hoveredEnemy != clickedEnemy)
                {
                    hoveredEnemy.SetHighlight(false);
                }

                hoveredEnemy = clickedEnemy;
                hoveredEnemy.SetHighlight(true);

                // 타겟 선택 완료
                if (pendingAction == "attack")
                {
                    QueueAllyCommand(currentControlledMember, "attack", clickedEnemy);
                }
                else if (pendingAction == "skill" && pendingSkill != null)
                {
                    QueueAllyCommand(currentControlledMember, "skill", clickedEnemy, pendingSkill);
                }

                waitingForTargetSelection = false;
                pendingAction = "";
                pendingSkill = null;
                return true; // 타겟 선택 성공
            }
        }
        return false; // 타겟 선택 안함
    }

    public void OnCancelButton()
    {
        Debug.Log("[BattleManager] OnCancelButton called");

        // 1. 타겟 선택 중일 때 -> Fight 메뉴로 복귀
        if (waitingForTargetSelection)
        {
            waitingForTargetSelection = false;
            pendingAction = "";
            pendingSkill = null;
            if (hoveredEnemy != null)
            {
                hoveredEnemy.SetHighlight(false);
                hoveredEnemy = null;
            }
            
            AddMessage("Target selection cancelled.");
            
            // Fight 메뉴 다시 표시
            if (battleUIManager != null)
            {
                battleUIManager.ShowFightSubPanel();
                battleUIManager.ShowBackButton(false); // 메뉴에서는 Back 버튼 숨김 (선택 사항)
            }
            return;
        }

        // 2. Fight 메뉴가 열려있을 때 -> 이전 단계로 복귀 (Main Menu 또는 이전 캐릭터)
        // 현재는 Main Menu로 돌아가는 것으로 구현
        if (battleUIManager != null && battleUIManager.fightSubPanel != null && battleUIManager.fightSubPanel.activeSelf)
        {
            // 첫 번째 캐릭터라면 Main Menu로
            if (commandIndex == 0)
            {
                battleUIManager.ForceCloseMenus();
                battleUIManager.ShowMainMenu();
            }
            else
            {
                // 이전 캐릭터로 돌아가기 (구현 복잡도에 따라 선택)
                // 현재는 그냥 메뉴 닫기만 수행하거나 아무것도 안함
                // 여기서는 일단 아무것도 안함 (Fight 메뉴 유지)
            }
            battleUIManager.ShowBackButton(false);
        }
    }

    // 타겟 선택 시작 시 Back 버튼 표시
    private void ShowBackButton()
    {
        if (battleUIManager != null)
        {
            battleUIManager.ShowBackButton(true);
        }
    }

    private void HideBackButton()
    {
        if (battleUIManager != null)
        {
            battleUIManager.ShowBackButton(false);
        }
    }

    private void ReturnToDungeon(float delay = 2.0f)
    {
        StartCoroutine(ReturnToDungeonRoutine(delay));
    }

    private IEnumerator ReturnToDungeonRoutine(float delay)
    {
        var gm = GameManager.EnsureInstance();
        Debug.Log($"[BM:DIAG] ReturnToDungeonRoutine START | activePartyMembers.Count={activePartyMembers.Count} | GM={(gm != null ? "exists" : "NULL")} | delay={delay}, IsPlayingMessageSequence={IsPlayingMessageSequence}");
        foreach (var member in activePartyMembers)
        {
            if (member != null)
            {
                Debug.Log($"[BM:DIAG] SaveFromPlayer ABOUT TO CALL (initial) | location=ReturnToDungeonRoutine, line=1866 | member='{member.playerName}', InstanceID={member.GetInstanceID()}, EXP={member.exp}, HP={member.currentHP}, Lv={member.level}");
                Debug.Log("[PERSISTENCE_DEBUG] ReturnToDungeonRoutine: Saving " + member.playerName + " - HP: " + member.currentHP + "/" + member.maxHP);
                gm.SaveFromPlayer(member);
            }
            else
            {
                Debug.LogWarning("[BM:DIAG] ReturnToDungeonRoutine: skipped NULL member");
            }
        }

        // [2026-05-24] 복귀 타이머 분기:
        //   - 레벨업 메시지 시퀀스 진행 중 → 자동 delay 타이머 무시, 시퀀스 끝까지 대기
        //     → 시퀀스 종료 후 postSequenceFadeDelay 초 추가 대기(페이드아웃 시간 대용) → 씬 전환
        //   - 시퀀스 없으면 → 기존대로 delay 초 대기 후 씬 전환
        bool wasPlayingSequence = IsPlayingMessageSequence;

        // [2026-05-24] 영입 다이얼로그 분기 — WaitForSeconds(delay) 전에 굴림.
        // 굴림 성공 + 슬롯 여유 → 적 sprite 켜고 메시지 + YES/NO 다이얼로그.
        // 굴림 실패 또는 슬롯 부족 → 기존 흐름(시퀀스 대기 또는 일반 delay).
        bool recruitFlow = TryInitiateRecruitDialog(out Abyssdawn.MonsterSO recruitData);

        if (recruitFlow)
        {
            Debug.Log($"[Recruit] 영입 다이얼로그 흐름 활성 — '{recruitData.MonsterName}' (기존 {delay}s 자동 딜레이 무시)");

            // [2026-05-25 BUGFIX] wasPlayingSequence 스냅샷 의존 제거 → 무조건 가드로 일원화.
            // 이전 결함: 시퀀스 시작이 이 코루틴 첫 프레임보다 늦거나 멀티 레벨업으로
            //   _sequenceRoutine이 재시작되면 스냅샷이 빗나가 가드가 무력화됨 → 로그 끝나기 전 영입 시작.
            // 수정: RecruitDialogRoutine 직전에 조건과 무관하게 시퀀스 완전 종료까지 대기.
            //   멀티 레벨업도 안전 — PlayMessageSequence가 새 시퀀스를 set하면 _sequenceRoutine != null이
            //   다시 true가 되어 마지막 레벨업 시퀀스까지 끝나야 루프를 빠져나감.
            yield return StartCoroutine(WaitForActiveMessageSequence("영입 전 시퀀스 대기"));
            if (postSequenceFadeDelay > 0f) yield return new WaitForSeconds(postSequenceFadeDelay);

            yield return StartCoroutine(RecruitDialogRoutine(recruitData));
        }
        else if (wasPlayingSequence)
        {
            Debug.Log("[BattleManager] 레벨업 시퀀스 진행 중 — 자동 복귀 타이머 무시, 시퀀스 완료까지 대기");
            while (IsPlayingMessageSequence)
            {
                yield return null;
            }
            Debug.Log($"[BattleManager] 시퀀스 완료 감지 — postSequenceFadeDelay {postSequenceFadeDelay}s 추가 대기");
            if (postSequenceFadeDelay > 0f)
            {
                yield return new WaitForSeconds(postSequenceFadeDelay);
            }
            Debug.Log("[BattleManager] 추가 대기 종료 — 씬 전환 진행");
        }
        else
        {
            Debug.Log($"[BattleManager] 시퀀스 없음 — 기존대로 {delay}s 자동 대기");
            yield return new WaitForSeconds(delay);
        }

        // [SAFETY NET 2026-05-11] 씬 전환 직전 강제 저장 — delay 사이 EXP/HP가 외부 코드로 변경됐을 가능성 보완
        Debug.Log($"[BM:DIAG] FORCE SAVE before scene transition (after delay={delay}s)");
        foreach (var member in activePartyMembers)
        {
            if (member != null)
            {
                Debug.Log($"[BM:DIAG] FORCE SAVE | member='{member.playerName}', InstanceID={member.GetInstanceID()}, EXP={member.exp}, HP={member.currentHP}, Lv={member.level}");
                gm.SaveFromPlayer(member);
            }
        }

        SyncCompanionPersistenceBeforeSceneLeave();

        string sceneToLoad = DungeonEncounter.lastDungeonScene;
        DungeonEncounter.justReturnedFromBattle = true;
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("[BattleManager] No last dungeon scene saved. Returning to 0.");
            Debug.Log("[BM:DIAG] LoadScene(0) — sceneToLoad empty");
            SceneManager.LoadScene(0);
        }
        else
        {
            Debug.Log("[BattleManager] Returning to dungeon: " + sceneToLoad);
            Debug.Log($"[BM:DIAG] LoadScene('{sceneToLoad}') — dict count BEFORE LoadScene={GameManager.staticPartyData.Count}");
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    /// <summary>
    /// [2026-05-25] 진행 중인 메시지 시퀀스(레벨업 등)가 완전히 끝날 때까지 대기.
    /// 멀티 레벨업으로 시퀀스가 StopCoroutine 후 재시작돼도, _sequenceRoutine != null인 동안 계속 대기.
    /// 안전장치: maxWaitSeconds 초과 시(시퀀스가 비정상으로 영영 안 끝나는 경우) 강제 탈출 + 경고.
    /// 정상 시퀀스는 사용자 클릭으로 종료되므로 타임아웃은 비상용. 넉넉하게 잡아 일반 플레이를 방해하지 않음.
    /// </summary>
    private IEnumerator WaitForActiveMessageSequence(string reason, float maxWaitSeconds = 120f)
    {
        if (!IsPlayingMessageSequence)
        {
            Debug.Log($"[BattleManager] WaitForActiveMessageSequence({reason}): 진행 중 시퀀스 없음 — 즉시 통과");
            yield break;
        }

        Debug.Log($"[BattleManager] WaitForActiveMessageSequence({reason}): 시퀀스 완료까지 대기 시작");
        float startUnscaled = Time.unscaledTime;
        while (IsPlayingMessageSequence)
        {
            if (Time.unscaledTime - startUnscaled > maxWaitSeconds)
            {
                Debug.LogWarning($"[BattleManager] WaitForActiveMessageSequence({reason}): {maxWaitSeconds}s 타임아웃 — 시퀀스가 비정상으로 종료 안 됨. 강제 진행.");
                break;
            }
            yield return null;
        }
        Debug.Log($"[BattleManager] WaitForActiveMessageSequence({reason}): 대기 종료 (시퀀스 {(IsPlayingMessageSequence ? "타임아웃 강제" : "정상")} 완료)");
    }

    private IEnumerator GameOverRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // HP/MP 최대값으로 리셋 (SO에 직접 기록)
        foreach (var member in activePartyMembers)
        {
            if (member != null)
            {
                member.currentHP = member.maxHP;
                member.currentMP = member.maxMP;
            }
        }

        // 던전 영속 데이터 전체 초기화 (층수, 안개, 위치, 상태이상)
        DungeonPersistentData.ClearState();

        GameManager.EnsureInstance().ClearAllData();
        CompanionPartyPersistence.Clear();
        DestroyAllCompanionInstances();

        // [2026-09-30] 아이템·장비도 새 탐험 상태로 (새벽의 잔 최대 충전, 주운 아이템·장비 초기화, 장착 장비 = 시작 장비)
        ConsumableInventory.ResetForNewRun();
        EquipmentBag.ResetForNewRun(playerStatData);
        PlayerStats.PendingLevelUpNotes.Clear();

        Debug.Log("[BattleManager] Game Over — resetting to floor 1.");
        DungeonEncounter.justReturnedFromBattle = false; // 게임오버는 새 시작이므로 쿨다운 없음
        SceneManager.LoadScene(startDungeonScene);
    }

    private void ForceDisableUIPanels()
    {
        if (actionPanel != null) actionPanel.SetActive(false);
        if (skillPanel != null) skillPanel.SetActive(false);
    }

    private IEnumerator EnsureUIPanelsClosedAfterFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            yield return null;
            ForceDisableUIPanels();
        }
    }

    // ========== 파티 시스템 ==========
    private void InitializeParty()
    {
        Debug.Log("[PERSISTENCE_DEBUG] BattleManager.InitializeParty RUNNING");
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerStats>();
            if (player != null)
            {
                Debug.Log("[PERSISTENCE_DEBUG] [BattleManager] PlayerStats found automatically: " + player.name);
            }
            else
            {
                Debug.LogError("[BattleManager] Player is not assigned and could not be found in the scene!");
                return;
            }
        }

        activePartyMembers.Clear();
        activePartyMembers.Add(player);

        var gm = GameManager.EnsureInstance();
        
        if (gm.hasPlayerSnapshot)
        {
            foreach (var member in activePartyMembers)
            {
                if (member != null)
                {
                    gm.ApplyToPlayer(member);
                    Debug.Log("[BattleManager] Applied persistent stats to " + member.playerName + ": HP " + member.currentHP + "/" + member.maxHP + ", MP " + member.currentMP + "/" + member.maxMP);
                }
            }
        }
        else
        {
            // 스냅샷이 없으면 HP/MP를 최대값으로 초기화한 후 저장
            Debug.Log("[BattleManager] No player snapshot found. Initializing HP/MP to max values.");
            foreach (var member in activePartyMembers)
            {
                if (member != null)
                {
                    // HP/MP가 0이거나 비정상적인 값이면 최대값으로 초기화
                    if (member.currentHP <= 0 || member.currentHP > member.maxHP)
                    {
                        member.currentHP = member.maxHP;
                        Debug.Log("[BattleManager] Initialized " + member.playerName + " HP to " + member.maxHP);
                    }
                    if (member.currentMP <= 0 || member.currentMP > member.maxMP)
                    {
                        member.currentMP = member.maxMP;
                        Debug.Log("[BattleManager] Initialized " + member.playerName + " MP to " + member.maxMP);
                    }
                    
                    gm.SaveFromPlayer(member);
                    Debug.Log("[BattleManager] Saved " + member.playerName + " to GameManager: HP " + member.currentHP + "/" + member.maxHP + ", MP " + member.currentMP + "/" + member.maxMP);
                }
            }
        }

        RebuildPlayerStatusPanel();
        UpdateStatusUI(); // 중요: 데이터 로드 후 UI 강제 업데이트
        Debug.Log("[BattleManager] InitializeParty complete. UI Updated.");
    }

    /// <summary>
    /// 슬롯1=Hero, 슬롯2~4=영입 동료(CompanionPartyPersistence, 최대 3).
    /// </summary>
    private void BuildBattlePartyForEncounter(bool includePresetAlliesIfNoCompanions = false)
    {
        Debug.Log($"[Recruit-DIAG] === BuildBattlePartyForEncounter 진입 === includePreset={includePresetAlliesIfNoCompanions} | ActiveRoster.Count(전)={CompanionPartyPersistence.ActiveRoster.Count}, WaitlistPaths.Count(전)={CompanionPartyPersistence.WaitlistPaths.Count}");
        RestoreCompanionInstancesFromPersistence();

        activePartyMembers.Clear();
        companionSkillsByMember.Clear();

        // [Hero 이동 Step 2] MainLine 4칸 occupancy 기반 조립.
        //   슬롯 s == heroSlotIndex면 Hero, 그 외엔 ActiveRoster 동료(오름차순). 빈 칸은 null 보존.
        //   heroSlotIndex=0이면 occupancy=[Hero, ActiveRoster0, 1, 2]로 기존(압축 제거)과 완전히 동일.
        //   매칭: token.entry.id == ally.companionId (RestoreCompanion에서 심음) — 같은 종 2마리도 정확히 구분.
        var occupancy = CompanionPartyPersistence.BuildMainLineOccupancy();
        for (int s = 0; s < occupancy.Length; s++)
        {
            var token = occupancy[s];
            if (token.IsHero)
            {
                activePartyMembers.Add(player);   // 이 슬롯 = Hero
                continue;
            }

            PlayerStats matched = null;
            if (token.IsCompanion && token.entry != null)
            {
                foreach (var c in _companionInstances)
                {
                    if (c != null && c != player && c.companionId == token.entry.id)
                    {
                        matched = c;
                        break;
                    }
                }
            }

            activePartyMembers.Add(matched);   // 인스턴스 또는 null(빈 슬롯 보존)
            if (matched != null)
            {
                RegisterCompanionSkills(matched);
                EnsureVisualForPartyMember(matched, false);
            }
        }

        // 끝쪽 빈 칸만 제거(중간 빈 칸은 슬롯 보존 위해 유지). 프리셋 디버그 경로의 append 호환 + 불필요한 trailing null 정리.
        for (int i = activePartyMembers.Count - 1; i >= 1; i--)
        {
            if (activePartyMembers[i] == null) activePartyMembers.RemoveAt(i);
            else break;
        }

        if (includePresetAlliesIfNoCompanions && _companionInstances.Count == 0)
        {
            var warrior = GetOrCreateAlly(PartyRole.Warrior);
            var rogue = GetOrCreateAlly(PartyRole.Rogue);
            var wizard = GetOrCreateAlly(PartyRole.Wizard);
            activePartyMembers.Add(warrior);
            activePartyMembers.Add(rogue);
            activePartyMembers.Add(wizard);
        }

        PositionPartyMembers();
        Debug.Log($"[CompanionParty] BuildBattleParty — members={activePartyMembers.Count} (companions={_companionInstances.Count})");
    }

    private void DestroyAllCompanionInstances()
    {
        foreach (var c in _companionInstances)
        {
            if (c != null)
                Destroy(c.gameObject);
        }
        _companionInstances.Clear();
        companionSkillsByMember.Clear();
    }

    private void RestoreCompanionInstancesFromPersistence()
    {
        Debug.Log($"[Recruit-DIAG] === RestoreCompanionInstancesFromPersistence 진입 === ActiveRoster.Count={CompanionPartyPersistence.ActiveRoster.Count}");
        DestroyAllCompanionInstances();

        int restored = 0;
        foreach (var entry in CompanionPartyPersistence.ActiveRoster)
        {
            if (entry == null) continue;   // [2026-05-25 고정 3칸] 빈 슬롯(null)은 건너뜀
            if (_companionInstances.Count >= maxActiveCompanions) break;
            var so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
            if (so == null)
            {
                Debug.LogWarning($"[CompanionParty] 로드 실패: '{entry.resourcePath}' (Resources 경로 확인 필요)");
                continue;
            }

            var ally = CreateAllyFromCompanion(so);
            if (ally == null) continue;

            ally.currentHP = Mathf.Clamp(entry.currentHP, 0, ally.maxHP);
            ally.currentMP = Mathf.Clamp(entry.currentMP, 0, ally.maxMP);
            ally.companionId = entry.id;   // [2026-05-25 ID 3단계] entry.id를 인스턴스로 전달 (Sync id 매칭용)
            _companionInstances.Add(ally);
            restored++;
        }
        Debug.Log($"[Recruit-DIAG] RestoreCompanionInstances 완료 — restored={restored}, _companionInstances.Count={_companionInstances.Count}");
    }

    private void RegisterCompanionSkills(PlayerStats member)
    {
        if (member == null || member.companionSource == null) return;

        var list = new List<SkillData>();
        var skills = member.companionSource.ActiveSkills;
        if (skills != null)
        {
            foreach (var sk in skills)
            {
                if (sk != null) list.Add(sk);
            }
        }
        companionSkillsByMember[member] = list;
    }

    private void SyncCompanionPersistenceBeforeSceneLeave()
    {
        CompanionPartyPersistence.SyncActiveFromInstances(_companionInstances);
        Debug.Log($"[CompanionParty] Saved roster count={CompanionPartyPersistence.ActiveRoster.Count}");
    }

    private void SetPartyMode(PartyMode mode)
    {
        if (currentPartyMode == mode) return;
        currentPartyMode = mode;
        ApplyPartyMode(mode, force: true, restartCommands: false);
    }

    private void ApplyPartyMode(PartyMode mode, bool force = false, bool restartCommands = true)
    {
        if (!force && currentPartyMode == mode) return;

        currentPartyMode = mode;

        if (mode == PartyMode.Solo)
        {
            activePartyMembers.Clear();
            activePartyMembers.Add(player);

            foreach (var kvp in allyInstances)
            {
                if (kvp.Value != null && kvp.Value != player)
                    EnsureVisualForPartyMember(kvp.Value, false);
            }
            foreach (var c in _companionInstances)
            {
                if (c != null && c != player)
                    EnsureVisualForPartyMember(c, false);
            }
        }
        else
        {
            // Hero(슬롯1) + 영입 동료(슬롯2~4) 우선, 없으면 프리셋 3인(디버그)
            BuildBattlePartyForEncounter(includePresetAlliesIfNoCompanions: true);
        }

        RebuildPlayerStatusPanel();
        
        var gm = GameManager.Instance;
        if (gm != null && gm.hasPlayerSnapshot)
        {
            foreach (var member in activePartyMembers)
            {
                if (member != null && gm.partyData.ContainsKey(member.playerName))
                {
                    gm.ApplyToPlayer(member);
                    Debug.Log("[BattleManager] Applied stats to " + member.playerName + " after ApplyPartyMode: HP " + member.currentHP + "/" + member.maxHP + ", MP " + member.currentMP + "/" + member.maxMP);
                }
            }
        }
        
        UpdateStatusUI();

        if (restartCommands && currentPhase == BattlePhase.Command)
        {
            commandIndex = 0;
            pendingCommands.Clear();
            PrepareNextCommand();
        }
    }

    private PlayerStats GetOrCreateAlly(PartyRole role)
    {
        if (allyInstances.ContainsKey(role) && allyInstances[role] != null)
        {
            return allyInstances[role];
        }

        return CreateAllyFromPreset(role);
    }

    private PlayerStats CreateAllyFromPreset(PartyRole role)
    {
        AllyPreset preset = GetAllyPreset(role);

        GameObject allyObj = new GameObject(preset.name);
        if (playerPartyRoot != null)
        {
            allyObj.transform.SetParent(playerPartyRoot, false);
        }

        PlayerStats allyStats = allyObj.AddComponent<PlayerStats>();
        allyStats.playerName = preset.name;

        // Create a runtime PlayerStatData SO for this ally (정적 스탯만 보관)
        allyStats.statData = ScriptableObject.CreateInstance<PlayerStatData>();
        // [2026-05-07] level/exp는 PlayerStats 컴포넌트가 직접 보유 (PlayerStatData에서 분리됨)
        allyStats.level = 1;
        allyStats.exp = 0;

        // Set base stats (instead of final calculated stats, which are now read-only)
        allyStats.baseHP = preset.maxHP;
        allyStats.baseMP = preset.maxMP;
        allyStats.baseAttack = preset.attack;
        allyStats.baseDefense = preset.defense;
        allyStats.baseMagic = preset.magic;
        allyStats.baseAgility = preset.agility;
        allyStats.baseLuck = preset.luck;

        // characterClass is null, so maxHP/maxMP/stats will use base values directly

        var gm = GameManager.Instance;
        if (gm != null && gm.partyData.ContainsKey(allyStats.playerName))
        {
            gm.ApplyToPlayer(allyStats);
            Debug.Log("[BattleManager] Loaded " + allyStats.playerName + " stats from GameManager: HP " + allyStats.currentHP + "/" + allyStats.maxHP + ", MP " + allyStats.currentMP + "/" + allyStats.maxMP);
        }
        else
        {
            // GameManager에 데이터가 없으면 풀피로 초기화
            allyStats.currentHP = allyStats.maxHP;
            allyStats.currentMP = allyStats.maxMP;
            // GameManager에 저장
            if (gm != null)
            {
                gm.SaveFromPlayer(allyStats);
            }
        }

        // 동료는 월드에 시각적 표현 없음 (스테이터스만 표시)
        EnsureVisualForPartyMember(allyStats, false);

        allyInstances[role] = allyStats;
        return allyStats;
    }

    private AllyPreset GetAllyPreset(PartyRole role)
    {
        switch (role)
        {
            case PartyRole.Warrior:
                return new AllyPreset
                {
                    name = "Warrior",
                    maxHP = 120,
                    maxMP = 10,
                    attack = 22,
                    defense = 12,
                    magic = 3,
                    agility = 8,
                    luck = 2,
                    color = Color.red
                };
            case PartyRole.Rogue:
                return new AllyPreset
                {
                    name = "Rogue",
                    maxHP = 80,
                    maxMP = 15,
                    attack = 20,
                    defense = 6,
                    magic = 5,
                    agility = 18,
                    luck = 8,
                    color = Color.yellow
                };
            case PartyRole.Wizard:
                return new AllyPreset
                {
                    name = "Wizard",
                    maxHP = 70,
                    maxMP = 50,
                    attack = 8,
                    defense = 4,
                    magic = 25,
                    agility = 10,
                    luck = 4,
                    color = Color.cyan
                };
            default:
                return new AllyPreset();
        }
    }

    private void PositionPartyMembers()
    {
        if (playerPartyCenter == null) return;

        int count = activePartyMembers.Count;
        for (int i = 0; i < count; i++)
        {
            if (activePartyMembers[i] == null) continue;

            float offset = (i - (count - 1) / 2f) * playerPartySpacing;
            Vector3 pos = playerPartyCenter.position + Vector3.right * offset;
            activePartyMembers[i].transform.position = pos;
        }
    }

    private void EnsureVisualForPartyMember(PlayerStats member, bool showVisual)
    {
        if (member == null) return;

        SpriteRenderer sr = member.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = showVisual;

        Collider2D col = member.GetComponent<Collider2D>();
        if (col != null) col.enabled = showVisual;

        // Hero는 월드에 표시, 동료는 표시하지 않음
        if (member == player)
        {
            if (sr != null) sr.enabled = true;
            if (col != null) col.enabled = true;
        }
        else
        {
            if (sr != null) sr.enabled = false;
            if (col != null) col.enabled = false;
        }
    }

    private PartyRole GetPartyRole(PlayerStats member)
    {
        if (member == player) return PartyRole.Hero;
        if (member != null && member.IsRecruitedCompanion) return PartyRole.Companion;
        if (allyInstances.ContainsValue(member))
        {
            foreach (var kvp in allyInstances)
            {
                if (kvp.Value == member) return kvp.Key;
            }
        }
        return PartyRole.Hero;
    }

    public void StartBattle()
    {
        Debug.Log("[BATTLE_DEBUG] StartBattle() 시작");
        Debug.Log("[BattleManager] StartBattle() called.");
        // 전투 상태 초기화
        battleEnded = false;

        // [LastStand] 전투 시작 시 모든 파티 멤버의 Last Stand 플래그 리셋
        foreach (var member in activePartyMembers)
        {
            if (member != null) member.ResetLastStand();
        }
        currentPhase = BattlePhase.Command;
        playerTurn = false;
        turnInProgress = false;
        
        ForceDisableUIPanels();
        if (battleUIManager != null)
        {
            // 초기화가 끝난 뒤 StartCommandPhase()에서 FightSubPanel을 연다
            battleUIManager.ForceCloseMenus();
        }

        ClearSpawnedEnemies();
        activeEnemies.Clear();
        enemy = null;
        waitingForTargetSelection = false;
        pendingAction = "";
        pendingSkill = null;
        hoveredEnemy = null;

        InitializeParty();

        foreach (var member in activePartyMembers)
        {
            if (member != null) member.ResetLastStand();
        }

        // Hero + 영입 동료(슬롯2~4). 프리셋 3인은 영입이 없고 startWithFullParty일 때만.
        BuildBattlePartyForEncounter(includePresetAlliesIfNoCompanions: startWithFullParty);
        
        // ApplyPartyMode 후 다시 한 번 GameManager에서 로드 (파티 구성이 덮어쓰지 않도록)
        var gm = GameManager.EnsureInstance();
        if (gm != null && gm.hasPlayerSnapshot)
        {
            foreach (var member in activePartyMembers)
            {
                if (member != null && GameManager.staticPartyData.ContainsKey(member.playerName))
                {
                    var savedData = GameManager.staticPartyData[member.playerName];
                    member.currentHP = Mathf.Clamp(savedData.currentHP, 0, member.maxHP);
                    member.currentMP = Mathf.Clamp(savedData.currentMP, 0, member.maxMP);
                    Debug.Log("[BattleManager] Force re-applied HP/MP after ApplyPartyMode: " + member.playerName + " - HP " + member.currentHP + "/" + member.maxHP + ", MP " + member.currentMP + "/" + member.maxMP);
                }
            }
            UpdateStatusUI();
        }
        
        Debug.Log("[BattleManager] === Final HP/MP Status After StartBattle ===");
        foreach (var member in activePartyMembers)
        {
            if (member != null)
            {
                Debug.Log("[BattleManager] " + member.playerName + ": HP " + member.currentHP + "/" + member.maxHP + ", MP " + member.currentMP + "/" + member.maxMP);
            }
        }
        
        // [User Request] Start with Solo only. Disable auto-fill for now.
        // if (activePartyMembers.Count <= 1)
        // {
        //     ApplyPartyMode(PartyMode.Full, force: true, restartCommands: false);
        //     desiredMode = PartyMode.Full;
        // }
        
        // 전투 시작 시 모든 파티 멤버의 HP/MP를 최대값으로 초기화 (주석 처리: GameManager 연동을 위해)
        // InitializePartyHPMP(); // InitializeParty()에서 이미 GameManager에서 로드함

        // 파티 멤버를 Recorder에 등록
        if (battleRecorder != null)
        {
            battleRecorder.ClearTargets(); // 기존 타겟 초기화
            foreach (var member in activePartyMembers)
            {
                if (member != null) battleRecorder.RegisterTarget(member.transform);
            }
        }

        int currentFloor = DungeonPersistentData.currentFloor;
        MonsterSO[] monsters = LoadMonsterSOsForFloor(currentFloor);

        if (monsters == null || monsters.Length == 0)
        {
            Debug.LogError("[BattleManager] LoadMonsterSOsForFloor returned empty. Cannot spawn enemies.");
            AddMessage("ERROR: No monsters available for this floor!");
            return;
        }

        if (monsterPrefab == null)
        {
            Debug.LogError("[BattleManager] monsterPrefab is not assigned in Inspector!");
            AddMessage("ERROR: monsterPrefab not assigned!");
            return;
        }

        // ── AllowedSlots 기반 배치 알고리즘 ──
        // 각 몬스터의 AllowedSlots를 기반으로 슬롯 자동 배정.
        // Back 전용 → Front 전용 → 혼합/Any 순으로 슬롯 선점.
        Transform[] formationSlots = AssignSlotsByAllowedSlots(monsters);
        int monsterCount = monsters.Length;

        Debug.Log($"[SPAWN_DEBUG] monsters 수: {monsterCount} (AllowedSlots 기반 배치)");
        Debug.Log($"[SPAWN_DEBUG] monsterPrefab: {monsterPrefab}");
        for (int dbg = 0; dbg < monsterCount; dbg++)
        {
            string name = monsters[dbg]?.MonsterName ?? "null";
            SlotMask a = monsters[dbg]?.AllowedSlots ?? SlotMask.None;
            string slotName = formationSlots[dbg]?.name ?? "null";
            Debug.Log($"[SPAWN_DEBUG] {dbg}번 {name} (AllowedSlots={a}) → {slotName}");
        }

        for (int i = 0; i < monsterCount; i++)
        {
            Transform slot = formationSlots[i];

            if (slot == null)
            {
                Debug.LogWarning($"[SPAWN_WARN] {monsters[i].MonsterName}에 할당 가능한 빈 슬롯이 없어 스폰 누락 (AllowedSlots={monsters[i].AllowedSlots}).");
                continue;
            }

            Debug.Log($"[SPAWN_DEBUG] {i}번 몬스터 {monsters[i].MonsterName} → 슬롯 이름: {slot.name}, 위치: {slot.position}");

            // MonsterSO.HoverOffsetY 만큼 월드 Y로 띄워서 스폰 (공중 부양 효과)
            Vector3 spawnPos = slot.position + Vector3.up * monsters[i].HoverOffsetY;
            GameObject obj = Instantiate(monsterPrefab,
                spawnPos, Quaternion.identity, worldRoot);
            Debug.Log($"[SPAWN] {i}번 슬롯에 소환: {monsters[i].MonsterName}, 스프라이트: {monsters[i].Sprite?.name}, HoverY: {monsters[i].HoverOffsetY}");

            EnemyStats stats = obj.GetComponent<EnemyStats>();
            if (stats != null)
            {
                stats.Init(monsters[i]);
                // 스폰된 슬롯 Transform을 참조 비교로 BattleSlot enum에 저장.
                // AssignDisplayNumbers() 등 이후 로직이 이 값을 정렬 키로 쓴다.
                stats.currentSlot = GetBattleSlotFromTransform(slot);
            }

            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>()
                             ?? obj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = monsters[i].Sprite;
                sr.sortingLayerName = "Default";
                // 슬롯별 결정적 sortingOrder (전열 20~23, 후열 10~12)
                sr.sortingOrder = GetSortingOrderForSlot(GetBattleSlotFromTransform(slot));

                if (sr.sprite == null)
                    Debug.LogWarning($"[SPRITE_DEBUG] 스프라이트 NULL: {monsters[i].MonsterName} — MonsterSO의 Sprite 필드를 확인하세요.");
                else
                    Debug.Log($"[SPRITE_DEBUG] 주입 완료: {monsters[i].MonsterName}, 스프라이트: {sr.sprite.name}");
            }
            else
            {
                Debug.LogWarning($"[SPRITE_DEBUG] SpriteRenderer 없음: {monsters[i].MonsterName}");
            }

            // World Space Canvas (이름/HP/MP UI) 정렬. SpriteRenderer와 같은 sortingOrder 규칙 적용.
            // 단, 몬스터 스프라이트보다 UI가 항상 앞에 오도록 +5 오프셋을 둔다.
            Canvas[] canvases = obj.GetComponentsInChildren<Canvas>(includeInactive: true);
            int canvasOrder = 25;
            foreach (Canvas cv in canvases)
            {
                if (cv == null) continue;
                cv.overrideSorting = true;
                cv.sortingLayerName = "Default";
                cv.sortingOrder = canvasOrder;
            }

            // 스프라이트 크기를 슬롯 크기에 맞게 자동 계산
            if (sr != null && sr.sprite != null)
            {
                // 카메라가 보는 월드 높이 계산
                Camera cam = Camera.main;
                float worldHeight = cam.orthographicSize * 2f;
                float worldWidth = worldHeight * cam.aspect;

                // 슬롯 하나의 크기 = 전체 너비 / 4
                float slotWorldWidth = worldWidth / 4f;
                float slotWorldHeight = worldHeight / 3f;

                // 스프라이트 원본 크기
                float spriteWidth = sr.sprite.bounds.size.x;
                float spriteHeight = sr.sprite.bounds.size.y;

                // 슬롯에 맞는 스케일
                float scaleX = slotWorldWidth / spriteWidth;
                float scaleY = slotWorldHeight / spriteHeight;
                float scale = Mathf.Min(scaleX, scaleY);

                float finalScale = scale * monsterScaleMultiplier * monsters[i].ScaleMultiplier;
                obj.transform.localScale = new Vector3(finalScale, finalScale, 1f);
                Debug.Log($"[SCALE_DEBUG] {monsters[i].MonsterName} 최종 스케일: {finalScale}");
            }

            // 스프라이트·스케일 확정 후 BoxCollider2D를 스프라이트 크기에 맞게 설정
            if (sr != null && sr.sprite != null)
            {
                BoxCollider2D col = obj.GetComponent<BoxCollider2D>();
                if (col != null)
                    col.size = sr.sprite.bounds.size;
            }

            if (stats != null)
            {
                activeEnemies.Add(stats);
                if (battleRecorder != null) battleRecorder.RegisterTarget(stats.transform);
            }
            Debug.Log($"[BattleManager] Spawned {monsters[i].MonsterName} at slot {slot.name}");
        }

        if (activeEnemies.Count == 0)
        {
            Debug.LogError("[BattleManager] No enemies spawned after MonsterSO spawn loop. Check console for details.");
            AddMessage("ERROR: No enemies spawned! Check console for details.");
            return;
        }

        // 슬롯 순번(Number UI) 부여: 전열(Slot1~4) 좌→우 먼저, 후열(Slot5~7) 좌→우 나중.
        // 실제 사용 중인 슬롯 번호(2,3,5,6 등)와 무관하게 1부터 연속 증가한다.
        AssignDisplayNumbers();

        // 전/후열 분단선 토글 — 실제 배치된 currentSlot 집계 기반
        // (반드시 activeEnemies 채워지고 currentSlot이 세팅된 뒤 호출)
        UpdateDividerFromActualLayout();

        Debug.Log($"[BattleManager] Battle started successfully with {activeEnemies.Count} enemy/enemies.");

        currentTargetIndex = 0;
        enemy = GetCurrentTarget();

        if (enemy == null && activeEnemies.Count > 0)
        {
            enemy = activeEnemies[0];
        }

        // 적 상태 UI 패널 재구성
        RebuildEnemyStatusPanel();

        // 슬롯 초기화 (BattleLine 기반)
        InitializeBattleLines();

        // 턴 순서 구성
        BuildTurnOrder();

        // 커맨드 페이즈 시작
        StartCommandPhase();
    }

    /// <summary>
    /// 플레이어 파티와 적을 BattleLine에 슬롯 순서대로 배치합니다.
    /// 슬롯 1,2 = 전열, 슬롯 3,4 = 후열
    /// </summary>
    private void InitializeBattleLines()
    {
        playerLine.Clear();
        for (int i = 0; i < activePartyMembers.Count && i < 4; i++)
        {
            var member = activePartyMembers[i];
            if (member == null) continue;
            int slotIndex = i + 1;
            member.currentSlot = (BattleSlot)slotIndex;
            playerLine.AssignToSlot(member, slotIndex);
        }

        enemyLine.Clear();

        // 적의 currentSlot은 이미 스폰 단계(AssignSlotsByAllowedSlots + GetBattleSlotFromTransform)에서
        // 7슬롯 체계로 정확히 세팅되어 있으므로 절대 덮어쓰지 않는다.
        // BattleLine은 레거시 4슬롯 컨테이너라 후열(Slot5~7)을 표현하지 못하므로,
        // 전열(Slot1~4)에 있는 적만 동기화해 기존 GetAt/GetCharactersInMask 호출을 유지한다.
        foreach (var e in activeEnemies)
        {
            if (e == null) continue;
            int slotIdx = (int)e.currentSlot;
            if (slotIdx >= 1 && slotIdx <= 4)
            {
                enemyLine.AssignToSlot(e, slotIdx);
            }
        }

        Debug.Log($"[BattleManager] BattleLine 초기화 완료. 플레이어: {playerLine}, 적(전열 전용): {enemyLine}");
    }

    private void RebuildEnemyStatusPanel()
    {
        EnsureEnemyStatusPanelReference();
        if (enemyStatusPanel == null) return;

        enemyStatusSlots.Clear();
        enemyStatusFrames.Clear();
        enemyStatusMonsterImages.Clear();
        enemyStatusNameTexts.Clear();
        enemyStatusHPTexts.Clear();
        enemyStatusMPTexts.Clear();
        enemyStatusIconImages.Clear();
        StopAllEnemySlotEffects();
        lastEnemyDisplayedHP.Clear();
        usingEnemyBarPortraitUI = false;

        if (TryBindExistingEnemySlots())
        {
            usingEnemyBarPortraitUI = true;
            return;
        }

        // 기존 슬롯 제거
        for (int i = enemyStatusPanel.childCount - 1; i >= 0; i--)
        {
            Destroy(enemyStatusPanel.GetChild(i).gameObject);
        }

        float rowH = 26f;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            int captured = i;
            var slotGO = new GameObject($"EnemySlot_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            slotGO.transform.SetParent(enemyStatusPanel, false);
            var rt = slotGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(520f, rowH);
            rt.anchoredPosition = new Vector2(0f, -i * (rowH + 4f));

            var img = slotGO.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.08f);

            var btn = slotGO.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (waitingForTargetSelection && activeEnemies.Count > captured && activeEnemies[captured] != null)
                {
                    EnemyStats clickedEnemy = activeEnemies[captured];
                    if (clickedEnemy.currentHP > 0)
                    {
                        if (hoveredEnemy != null && hoveredEnemy != clickedEnemy)
                        {
                            hoveredEnemy.SetHighlight(false);
                        }

                        hoveredEnemy = clickedEnemy;
                        hoveredEnemy.SetHighlight(true);

                        if (pendingAction == "attack")
                        {
                            QueueAllyCommand(currentControlledMember, "attack", clickedEnemy);
                        }
                        else if (pendingAction == "skill" && pendingSkill != null)
                        {
                            QueueAllyCommand(currentControlledMember, "skill", clickedEnemy, pendingSkill);
                        }

                        waitingForTargetSelection = false;
                        pendingAction = "";
                        pendingSkill = null;
                    }
                }
            });

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(slotGO.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10f, 0f);
            textRT.offsetMax = new Vector2(-10f, 0f);

            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.text = "";
            text.fontSize = 14;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
        }
    }

    private bool TryBindExistingEnemySlots()
    {
        Transform slotRoot = FindExistingEnemyBarRoot();
        if (slotRoot == null) return false;

        List<Transform> foundSlots = new List<Transform>();
        for (int i = 0; i < 4; i++)
        {
            Transform slot = FindEnemySlot(slotRoot, i);
            if (slot == null)
            {
                return false;
            }

            foundSlots.Add(slot);
        }

        foreach (Transform slot in foundSlots)
        {
            enemyStatusSlots.Add(slot as RectTransform);

            Image frameImage = FindImageInSlot(slot, "Frame");
            if (frameImage == null)
            {
                frameImage = slot.GetComponent<Image>();
            }

            Image monsterImage = FindImageInSlot(slot, "MonsterImage");
            TextMeshProUGUI nameText = FindTextInSlot(slot, "Name", "NameText", "Nametext");
            TextMeshProUGUI hpText = FindTextInSlot(slot, "HPText", "HP");
            TextMeshProUGUI mpText = FindTextInSlot(slot, "MPText", "MP");
            Transform statusIconRow = FindNamedDescendantRecursive(slot, "StatusIconRow");

            enemyStatusFrames.Add(frameImage);
            enemyStatusMonsterImages.Add(monsterImage);
            enemyStatusNameTexts.Add(nameText);
            enemyStatusHPTexts.Add(hpText);
            enemyStatusMPTexts.Add(mpText);
            enemyStatusIconImages.Add(BindStatusIcons(statusIconRow));

            Button slotButton = slot.GetComponent<Button>();
            if (slotButton == null)
            {
                slotButton = slot.gameObject.AddComponent<Button>();
            }

            int capturedSlotNumber = enemyStatusSlots.Count; // 1~4 (바인딩 순서 = 슬롯 번호)
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() =>
            {
                if (!waitingForTargetSelection) return;

                // 해당 슬롯 번호에 배정된 몬스터를 enemyLine에서 찾기
                EnemyStats clickedEnemy = enemyLine.GetAt(capturedSlotNumber) as EnemyStats;
                if (clickedEnemy == null || clickedEnemy.currentHP <= 0) return;

                if (hoveredEnemy != null && hoveredEnemy != clickedEnemy)
                    hoveredEnemy.SetHighlight(false);

                hoveredEnemy = clickedEnemy;
                hoveredEnemy.SetHighlight(true);

                if (pendingAction == "attack")
                    QueueAllyCommand(currentControlledMember, "attack", clickedEnemy);
                else if (pendingAction == "skill" && pendingSkill != null)
                    QueueAllyCommand(currentControlledMember, "skill", clickedEnemy, pendingSkill);

                waitingForTargetSelection = false;
                pendingAction = "";
                pendingSkill = null;
            });

            Debug.Log($"[BattleManager] Enemy slot bind - {slot.name} | MonsterImage: {(monsterImage != null ? monsterImage.name : "NULL")} | Name: {(nameText != null ? nameText.name : "NULL")} | HP: {(hpText != null ? hpText.name : "NULL")} | MP: {(mpText != null ? mpText.name : "NULL")}");
        }

        Debug.Log($"[BattleManager] Bound existing EnemyBar slots: {enemyStatusSlots.Count}");
        return enemyStatusSlots.Count == 4;
    }

    private int ScoreEnemyPrefab(GameObject prefab)
    {
        EnemyStats stats = prefab.GetComponent<EnemyStats>();
        if (stats == null) return 0;
        return stats.defense * 10 + stats.maxHP;
    }

    /// <summary>
    /// 지정 층에 등장 가능한 MonsterSO에서 Rat만 스폰합니다(임시). 스폰 수 1마리.
    /// Resources/Monsters, CanSpawnOnFloor, Rat 강제 폴백.
    /// </summary>
    private MonsterSO[] LoadMonsterSOsForFloor(int floor)
    {
        Debug.Log("[BATTLE_DEBUG] LoadMonsterSOsForFloor 진입");
        Debug.Log($"[FLOOR_DEBUG] LoadMonsterSOsForFloor 호출, floor: {floor}");
        MonsterSO[] all = Resources.LoadAll<MonsterSO>("Monsters");
        Debug.Log($"[FLOOR_DEBUG] 로드된 SO 수: {all?.Length ?? 0}");
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning("[BattleManager] Resources.LoadAll<MonsterSO>(\"Monsters\") returned empty.");
            return new MonsterSO[0];
        }

        // 층 조건 필터
        var candidates = all.Where(so => so != null && so.CanSpawnOnFloor(floor)).ToList();
        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[BattleManager] No MonsterSO passes CanSpawnOnFloor({floor}). Falling back to all loaded SOs.");
            candidates = all.Where(so => so != null).ToList();
        }

        if (candidates.Count == 0)
            return new MonsterSO[0];

        // (임시) 스폰 풀: Rat만 — 다른 MonsterSO는 제외
        candidates = candidates
            .Where(so => string.Equals(so.MonsterName, "Rat", System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            MonsterSO ratSo = all.FirstOrDefault(so =>
                so != null && string.Equals(so.MonsterName, "Rat", System.StringComparison.OrdinalIgnoreCase));
            if (ratSo == null)
            {
                Debug.LogError("[BattleManager] Rat MonsterSO가 없습니다. Resources/Monsters/Rat 확인.");
                return new MonsterSO[0];
            }
            candidates = new List<MonsterSO> { ratSo };
            Debug.LogWarning("[BattleManager] 층 조건에 맞는 Rat가 없어 Rat SO를 강제 사용합니다(테스트).");
        }

        // SpawnWeight 합산
        float totalWeight = candidates.Sum(so => Mathf.Max(0f, so.SpawnWeight));
        if (totalWeight <= 0f)
            totalWeight = candidates.Count; // 가중치가 모두 0이면 균등 분배

        // 스폰 수: (임시) 플레이 테스트용 — 항상 1마리. 복원 시: Mathf.Clamp(Random.Range(1,5),1,4)
        int count = 1;
        Debug.Log($"[BattleManager] LoadMonsterSOsForFloor({floor}) → candidates: {candidates.Count}, count: {count}");

        // 중복 허용: 같은 몬스터가 여러 번 뽑힐 수 있도록 pool에서 제거하지 않는다.
        // 예) 4마리 전투에서 Rat × 4 같은 조합도 가능.
        List<MonsterSO> result = new List<MonsterSO>();
        for (int k = 0; k < count; k++)
        {
            float r = UnityEngine.Random.Range(0f, totalWeight);
            float acc = 0f;
            MonsterSO picked = candidates[0];
            foreach (var so in candidates)
            {
                acc += Mathf.Max(0f, so.SpawnWeight);
                if (r <= acc) { picked = so; break; }
            }
            result.Add(picked);
        }

        return result.ToArray();
    }

    private EnemyStats GetCurrentTarget()
    {
        if (activeEnemies.Count == 0) return null;
        currentTargetIndex = Mathf.Clamp(currentTargetIndex, 0, activeEnemies.Count - 1);
        int safety = 0;
        while (activeEnemies[currentTargetIndex] == null || activeEnemies[currentTargetIndex].currentHP <= 0)
        {
            currentTargetIndex = (currentTargetIndex + 1) % activeEnemies.Count;
            if (++safety > activeEnemies.Count) break;
        }
        return activeEnemies[currentTargetIndex];
    }

    private void ChangeTarget(int dir)
    {
        if (activeEnemies.Count == 0) return;
        currentTargetIndex = (currentTargetIndex + dir + activeEnemies.Count) % activeEnemies.Count;
        enemy = GetCurrentTarget();
        UpdateStatusUI();
    }

    private void ClearSpawnedEnemies()
    {
        StopAllEnemySlotEffects();
        lastEnemyDisplayedHP.Clear();

        foreach (var e in activeEnemies)
        {
            if (e != null) Destroy(e.gameObject);
        }
        activeEnemies.Clear();
    }

    // ========== 턴 시스템 ==========
    private void BuildTurnOrder()
    {
        turnOrder.Clear();

        // 파티 멤버 추가
        foreach (var member in activePartyMembers)
        {
            if (member != null && member.currentHP > 0)
            {
                turnOrder.Add(new BattleActor(member));
            }
        }

        // 적 추가
        foreach (var e in activeEnemies)
        {
            if (e != null && e.currentHP > 0)
            {
                turnOrder.Add(new BattleActor(e));
            }
        }

        // 민첩 기반 확률 정렬: speedScore = Agility × Random(0.8~1.2)
        // ±20% 범위 내 민첩 차이만 역전 가능, 그 이상 차이는 절대 역전 불가
        turnOrder = turnOrder
            .OrderByDescending(a => a.agility * UnityEngine.Random.Range(0.8f, 1.2f))
            .ToList();
        Debug.Log("[TurnOrder] " + string.Join(" > ", turnOrder.Select(a => $"{(a.isPlayer ? a.player.playerName : a.enemy.enemyName)}(Agi:{a.agility})")));
    }

    private void StartCommandPhase()
    {
        Debug.Log("[BattleManager] StartCommandPhase() - Starting command input phase.");
        currentPhase = BattlePhase.Command;
        pendingCommands.Clear();
        commandIndex = 0;
        playerTurn = false;
        turnInProgress = false;
        waitingForTargetSelection = false;
        pendingAction = "";
        pendingSkill = null;
        if (hoveredEnemy != null)
        {
            hoveredEnemy.SetHighlight(false);
            hoveredEnemy = null;
        }
        
        // 살아있는 파티 멤버가 있는지 확인
        bool hasAliveMember = false;
        foreach (var member in activePartyMembers)
        {
            if (member != null && member.currentHP > 0)
            {
                hasAliveMember = true;
                break;
            }
        }
        
        if (!hasAliveMember)
        {
            Debug.LogWarning("[BattleManager] No alive party members! Cannot start command phase.");
            CheckBattleEnd();
            return;
        }

        // 커맨드 페이즈: Main 대신 PrepareNextCommand()에서 FightSubPanel 연다
        if (actionPanel != null) actionPanel.SetActive(false);
        if (skillPanel != null) skillPanel.SetActive(false);
        if (battleUIManager != null)
            battleUIManager.ForceCloseMenus();
        PrepareNextCommand();
    }

    private void PrepareNextCommand()
    {
        if (battleEnded) return;

        // 살아있는 다음 아군 찾기
        while (commandIndex < activePartyMembers.Count && (activePartyMembers[commandIndex] == null || activePartyMembers[commandIndex].currentHP <= 0))
        {
            commandIndex++;
        }

        if (commandIndex >= activePartyMembers.Count)
        {
            // 모든 파티 멤버가 죽었는지 확인
            bool allDead = true;
            foreach (var member in activePartyMembers)
            {
                if (member != null && member.currentHP > 0)
                {
                    allDead = false;
                    break;
                }
            }
            
            if (allDead)
            {
                Debug.LogWarning("[BattleManager] All party members are dead. Ending battle.");
                CheckBattleEnd();
                return;
            }
            
            BeginResolutionPhase();
            return;
        }

        currentPhase = BattlePhase.Command;
        currentControlledMember = activePartyMembers[commandIndex];
        
        // 안전장치: currentControlledMember가 null이거나 HP가 0이면 다음으로
        if (currentControlledMember == null || currentControlledMember.currentHP <= 0)
        {
            commandIndex++;
            PrepareNextCommand();
            return;
        }
        
        currentControlledRole = GetPartyRole(currentControlledMember);
        playerTurn = true;
        turnInProgress = false;

        if (skillPanel != null) skillPanel.SetActive(false);
        ConfigureActionUIForActor(currentControlledMember);

        // actionPanel은 Fight 버튼을 눌러야만 활성화됨 (자동으로 활성화하지 않음)
        // if (actionPanel != null) actionPanel.SetActive(true);
        if (skillPanel != null) skillPanel.SetActive(false);

        // 매 커맨드 턴마다 FightSubPanel을 동일하게 연다 (첫 파티원 포함)
        if (battleUIManager != null)
            battleUIManager.ShowFightSubPanel();

        AddMessage($"{currentControlledMember.playerName} is preparing an action.");
        UpdateStatusUI();
    }

    private void ConfigureActionUIForActor(PlayerStats actor)
    {
        Debug.Log($"[BattleManager] ConfigureActionUIForActor called with actor: {(actor != null ? actor.playerName : "null")}");
        
        bool isHero = actor != null && actor == player;
        PartyRole role = actor != null ? GetPartyRole(actor) : PartyRole.Hero;
        
        Debug.Log($"[BattleManager] isHero: {isHero}, role: {role}, player: {(player != null ? player.playerName : "null")}, actor == player: {(actor != null && player != null ? (actor == player).ToString() : "N/A")}");

        bool hasSkills = false;
        if (actor != null)
        {
            if (companionSkillsByMember.TryGetValue(actor, out var compSkills))
                hasSkills = compSkills.Count > 0;
            else
            {
                PartyRole actorRole = GetPartyRole(actor);
                if (roleSkillCache.ContainsKey(actorRole))
                    hasSkills = roleSkillCache[actorRole].Count > 0;
            }
        }

        // 4가지 버튼 활성화 (Attack, Skill, Item, Defend)
        if (attackButton != null)
        {
            attackButton.gameObject.SetActive(true);
            attackButton.interactable = true;
            Debug.Log("[BattleManager] Attack button activated");
        }
        else
        {
            Debug.LogWarning("[BattleManager] attackButton is null!");
        }
        
        if (skillButton != null)
        {
            skillButton.gameObject.SetActive(true);
            skillButton.interactable = hasSkills;
            Debug.Log($"[BattleManager] Skill button activated (interactable: {hasSkills})");
        }
        else
        {
            Debug.LogWarning("[BattleManager] skillButton is null!");
        }
        
        if (itemButton != null)
        {
            itemButton.gameObject.SetActive(true);
            itemButton.interactable = isHero;
            Debug.Log($"[BattleManager] Item button activated (interactable: {isHero})");
        }
        else
        {
            Debug.LogWarning("[BattleManager] itemButton is null!");
        }
        
        if (defendButton != null)
        {
            defendButton.gameObject.SetActive(true);
            defendButton.interactable = true;
            Debug.Log("[BattleManager] Defend button activated");
        }
        else
        {
            Debug.LogWarning("[BattleManager] defendButton is null!");
        }
        
        if (fleeButton != null)
        {
            fleeButton.gameObject.SetActive(true);
            fleeButton.interactable = isHero;
            Debug.Log($"[BattleManager] Run button activated (interactable: {isHero})");
        }
        else
        {
            Debug.LogWarning("[BattleManager] fleeButton is null!");
        }
    }

    private void QueueAllyCommand(PlayerStats actor, string actionType, EnemyStats targetEnemy = null, SkillData skill = null, int itemHeal = 0, bool consumesPotion = false, ConsumableItemSO usedItem = null)
    {
        if (actor == null) return;

        var command = new AllyCommand
        {
            actor = actor,
            actionType = actionType,
            targetEnemy = targetEnemy,
            skill = skill,
            itemHealAmount = itemHeal,
            consumesPotion = consumesPotion,
            usedItem = usedItem
        };

        pendingCommands.Add(command);

        waitingForTargetSelection = false;
        pendingAction = "";
        pendingSkill = null;
        if (hoveredEnemy != null)
        {
            hoveredEnemy.SetHighlight(false);
            hoveredEnemy = null;
        }

        // skillPanel만 닫고 actionPanel은 유지 (다음 동료 턴을 위해)
        HideCommandPanels(hideMainPanel: false, closeFightMenu: false);

        commandIndex++;
        PrepareNextCommand();
    }

    private void HideCommandPanels(bool hideMainPanel = true, bool closeFightMenu = true)
    {
        if (hideMainPanel && actionPanel != null && actionPanel.activeSelf)
        {
            actionPanel.SetActive(false);
        }
        if (skillPanel != null && skillPanel.activeSelf)
        {
            skillPanel.SetActive(false);
        }
        if (closeFightMenu && battleUIManager != null)
        {
            battleUIManager.ForceCloseMenus();
        }
    }

    // ActionPanel 표시 (외부에서 호출 가능)
    public void ShowActionPanel()
    {
        Debug.Log("[BattleManager] ShowActionPanel() called");
        Debug.Log($"[BattleManager] Current Phase: {currentPhase}, Battle Ended: {battleEnded}, Turn In Progress: {turnInProgress}");
        Debug.Log($"[BattleManager] activeEnemies count: {activeEnemies.Count}, activePartyMembers count: {activePartyMembers.Count}");
        
        // 전투가 시작되지 않았으면 전투 시작
        if (activeEnemies.Count == 0)
        {
            Debug.Log("[BattleManager] No enemies found. Starting battle...");
            StartBattle();
            // StartBattle 후에도 여전히 적이 없으면 경고
            if (activeEnemies.Count == 0)
            {
                Debug.LogWarning("[BattleManager] Battle started but no enemies spawned. ActionPanel will still be shown.");
            }
        }
        
        // battleEnded가 true이고 적도 없으면 새 전투 시작
        if (battleEnded && activeEnemies.Count == 0)
        {
            Debug.Log("[BattleManager] Battle ended but no enemies. Starting new battle...");
            StartBattle();
        }
        
        // Battle이 종료되었고 적도 모두 죽었을 때만 차단
        if (battleEnded && activeEnemies.Count > 0)
        {
            bool allEnemiesDead = true;
            foreach (var e in activeEnemies)
            {
                if (e != null && e.currentHP > 0)
                {
                    allEnemiesDead = false;
                    break;
                }
            }
            
            if (allEnemiesDead)
            {
                Debug.LogWarning("[BattleManager] All enemies are dead. Cannot show action panel.");
                return;
            }
            else
            {
                // 적이 살아있으면 전투 계속
                Debug.Log("[BattleManager] Enemies still alive. Continuing battle...");
                battleEnded = false;
            }
        }
        
        // Command Phase가 아니면 Command Phase로 전환 시도
        if (currentPhase != BattlePhase.Command && !battleEnded)
        {
            Debug.Log("[BattleManager] Not in Command Phase. Starting Command Phase...");
            StartCommandPhase();
        }
        
        if (actionPanel == null)
        {
            Debug.LogError("[BattleManager] actionPanel is null! Cannot show action panel.");
            // ActionPanel을 자동으로 찾기 시도
            GameObject foundPanel = GameObject.Find("ActionPanel");
            if (foundPanel != null)
            {
                actionPanel = foundPanel;
                Debug.Log("[BattleManager] Found ActionPanel automatically");
            }
            else
            {
                Debug.LogError("[BattleManager] ActionPanel not found in scene! Searching all GameObjects...");
                // 모든 GameObject에서 ActionPanel 찾기
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.Contains("Action") || obj.name.Contains("Panel"))
                    {
                        Debug.Log("[BattleManager] Found potential panel: " + obj.name);
                    }
                }
                return;
            }
        }

        Debug.Log($"[BattleManager] Activating ActionPanel: {actionPanel.name}");
        Debug.Log($"[BattleManager] ActionPanel active before: {actionPanel.activeSelf}");
        Debug.Log($"[BattleManager] ActionPanel activeInHierarchy before: {actionPanel.activeInHierarchy}");
        
        // 부모가 비활성화되어 있으면 활성화
        Transform parent = actionPanel.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                Debug.LogWarning($"[BattleManager] Parent {parent.name} is inactive. Activating it.");
                parent.gameObject.SetActive(true);
            }
            parent = parent.parent;
        }
        
        actionPanel.SetActive(true);
        Debug.Log($"[BattleManager] ActionPanel active after: {actionPanel.activeSelf}");
        Debug.Log($"[BattleManager] ActionPanel activeInHierarchy after: {actionPanel.activeInHierarchy}");
        
        // Canvas 확인 (메서드 레벨에서 선언하여 재사용)
        Canvas canvas = actionPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"[BattleManager] Found Canvas: {canvas.name}, enabled: {canvas.enabled}, renderMode: {canvas.renderMode}");
            
            // Canvas가 비활성화되어 있으면 활성화
            if (!canvas.gameObject.activeSelf)
            {
                Debug.LogWarning($"[BattleManager] Canvas {canvas.name} is inactive. Activating it.");
                canvas.gameObject.SetActive(true);
            }
            
            // Canvas 컴포넌트가 비활성화되어 있으면 활성화
            if (!canvas.enabled)
            {
                Debug.LogWarning($"[BattleManager] Canvas component {canvas.name} is disabled. Enabling it.");
                canvas.enabled = true;
            }
        }
        else
        {
            Debug.LogWarning("[BattleManager] No Canvas found in ActionPanel's parent hierarchy!");
        }
        
        // ActionPanel의 RectTransform 확인 및 수정
        RectTransform actionPanelRT = actionPanel.GetComponent<RectTransform>();
        if (actionPanelRT != null)
        {
            Debug.Log($"[BattleManager] ActionPanel RectTransform BEFORE - size: {actionPanelRT.sizeDelta}, position: {actionPanelRT.position}, anchoredPosition: {actionPanelRT.anchoredPosition}, localScale: {actionPanelRT.localScale}");
            
            // ActionPanel이 보이도록 보장 (크기가 0이면 기본값 설정)
            if (actionPanelRT.sizeDelta.x <= 0 || actionPanelRT.sizeDelta.y <= 0)
            {
                Debug.LogWarning($"[BattleManager] ActionPanel size is too small: {actionPanelRT.sizeDelta}. Setting default size.");
                actionPanelRT.sizeDelta = new Vector2(200, 100);
            }
            
            // Scale이 0이면 기본값 설정
            if (actionPanelRT.localScale.x <= 0 || actionPanelRT.localScale.y <= 0)
            {
                Debug.LogWarning($"[BattleManager] ActionPanel scale is zero: {actionPanelRT.localScale}. Setting to (1,1,1).");
                actionPanelRT.localScale = Vector3.one;
            }
            
            Debug.Log($"[BattleManager] ActionPanel RectTransform AFTER - size: {actionPanelRT.sizeDelta}, position: {actionPanelRT.position}, anchoredPosition: {actionPanelRT.anchoredPosition}, localScale: {actionPanelRT.localScale}");
        }
        else
        {
            Debug.LogError("[BattleManager] ActionPanel has no RectTransform component!");
        }
        
        // ActionPanel의 모든 자식 활성화
        foreach (Transform child in actionPanel.transform)
        {
            if (!child.gameObject.activeSelf)
            {
                Debug.LogWarning($"[BattleManager] Child {child.name} of ActionPanel is inactive. Activating it.");
                child.gameObject.SetActive(true);
            }
        }
        
        if (skillPanel != null)
        {
            skillPanel.SetActive(false);
        }

        // 현재 컨트롤 중인 캐릭터에 맞게 버튼 상태 설정
        if (currentControlledMember != null)
        {
            Debug.Log($"[BattleManager] Configuring UI for: {currentControlledMember.playerName} (isHero: {currentControlledMember == player})");
            ConfigureActionUIForActor(currentControlledMember);
        }
        else if (player != null)
        {
            Debug.Log($"[BattleManager] Configuring UI for player: {player.playerName}");
            // 현재 컨트롤 중인 캐릭터가 없으면 플레이어 기준으로 설정
            ConfigureActionUIForActor(player);
        }
        else
        {
            Debug.LogWarning("[BattleManager] No player or currentControlledMember found. Configuring UI with defaults.");
            ConfigureActionUIForActor(null);
        }
        
        // 버튼 상태 최종 확인
        Debug.Log($"[BattleManager] === Button Status After Activation ===");
        Debug.Log($"[BattleManager] AttackButton: {(attackButton != null ? $"Active={attackButton.gameObject.activeSelf}, Interactable={attackButton.interactable}, Visible={attackButton.gameObject.activeInHierarchy}" : "NULL")}");
        Debug.Log($"[BattleManager] SkillButton: {(skillButton != null ? $"Active={skillButton.gameObject.activeSelf}, Interactable={skillButton.interactable}, Visible={skillButton.gameObject.activeInHierarchy}" : "NULL")}");
        Debug.Log($"[BattleManager] ItemButton: {(itemButton != null ? $"Active={itemButton.gameObject.activeSelf}, Interactable={itemButton.interactable}, Visible={itemButton.gameObject.activeInHierarchy}" : "NULL")}");
        Debug.Log($"[BattleManager] DefendButton: {(defendButton != null ? $"Active={defendButton.gameObject.activeSelf}, Interactable={defendButton.interactable}, Visible={defendButton.gameObject.activeInHierarchy}" : "NULL")}");
        Debug.Log($"[BattleManager] RunButton: {(fleeButton != null ? $"Active={fleeButton.gameObject.activeSelf}, Interactable={fleeButton.interactable}, Visible={fleeButton.gameObject.activeInHierarchy}" : "NULL")}");
        
        // Canvas 상태 최종 확인 (이미 선언된 변수 재사용)
        if (canvas != null)
        {
            Debug.Log($"[BattleManager] Canvas Status - Name: {canvas.name}, Active: {canvas.gameObject.activeSelf}, Enabled: {canvas.enabled}, RenderMode: {canvas.renderMode}");
        }
        
        Debug.Log($"[BattleManager] ActionPanel Final Status - Active: {actionPanel.activeSelf}, ActiveInHierarchy: {actionPanel.activeInHierarchy}");
        Debug.Log($"[BattleManager] =======================================");
    }

    private void BeginResolutionPhase()
    {
        if (battleEnded) return;

        currentPhase = BattlePhase.Resolution;
        playerTurn = false;
        HideCommandPanels();

        // 해결 단계 시작
        StartCoroutine(ExecuteResolutionQueue());
    }

    private IEnumerator ExecuteResolutionQueue()
    {
        // 턴 순서 다시 구성 (민첩 순)
        // 드퀘·진여신전생식: 적·아군 모두 행동을 정한 뒤 동시에 시작, 순서는 AGI × 랜덤(0.8~1.2).
        // AGI 차이가 크면(10 vs 3) 항상 높은 쪽이 먼저, 작으면(10 vs 8) 낮은 쪽이 먼저 움직일 수도 있다.
        BuildTurnOrder();

        Debug.Log($"[BattleManager] Starting resolution queue. Order size: {turnOrder.Count}");
        foreach (var actor in turnOrder)
        {
            if (battleEnded)
            {
                Debug.Log("[BattleManager] Battle ended during resolution. Breaking queue.");
                yield break;
            }

            if (actor.isPlayer)
            {
                Debug.Log($"[BattleManager] Executing player turn: {actor.player?.playerName}");
                // 플레이어 액션 실행
                AllyCommand cmd = pendingCommands.Find(c => c.actor == actor.player);
                if (cmd != null)
                {
                    yield return StartCoroutine(ExecuteAllyResolution(cmd));
                }
            }
            else
            {
                Debug.Log($"[BattleManager] Executing enemy turn: {actor.enemy?.enemyName}");
                // 적 액션 실행
                yield return StartCoroutine(ExecuteEnemyTurn(actor.enemy));
            }

            yield return new WaitForSeconds(turnDelay);
        }

        // 모든 행동이 끝난 후 점화 데미지 처리 (가장 마지막)
        yield return StartCoroutine(ProcessAllIgniteDamage());

        // 모든 액션 완료 후 상태 체크
        CheckBattleEnd();

        if (!battleEnded)
        {
            // 다음 라운드 시작
            StartCommandPhase();
        }
    }

    private IEnumerator ExecuteAllyResolution(AllyCommand cmd)
    {
        if (cmd.actor == null || cmd.actor.currentHP <= 0) yield break;
        if (cmd.actor.IsStunned())
        {
            AddMessage($"{cmd.actor.playerName} is <color=#FFD700>stunned</color> and cannot act!");
            yield break;
        }

        yield return new WaitForSeconds(actionDelay);

        Debug.Log($"[ExecuteAllyResolution] Executing action: {cmd.actionType} for {cmd.actor.playerName}");

        switch (cmd.actionType)
        {
            case "attack":
                if (cmd.targetEnemy != null && cmd.targetEnemy.currentHP > 0)
                {
                    ExecuteAttack(cmd.actor, cmd.targetEnemy);
                }
                break;
            case "skill":
                if (cmd.skill != null)
                {
                    // Silence 체크 — 스킬 사용 불가 상태이상
                    if (cmd.actor.IsSilenced())
                    {
                        AddMessage($"{cmd.actor.playerName} is silenced and cannot use skills!");
                        break;
                    }
                    // 타겟이 필요한 스킬인데 타겟이 없거나 죽었으면 실행 불가 (단, 회복/방어 스킬은 타겟 없이(본인) 실행 가능)
                    bool isSelfSkill = IsSelfTargetSkill(cmd.skill);
                    if (isSelfSkill || (cmd.targetEnemy != null && cmd.targetEnemy.currentHP > 0))
                    {
                        yield return StartCoroutine(ExecuteSkill(cmd.actor, cmd.skill, cmd.targetEnemy));
                    }
                }
                break;
            case "item":
                if (cmd.consumesPotion && cmd.usedItem != null)
                {
                    bool used = ConsumableInventory.Instance != null
                        ? ConsumableInventory.Instance.UseItem(cmd.usedItem)
                        : potionCount-- > 0;
                    if (used)
                    {
                        // 통합 헬퍼: HP/MP 회복 + 상태이상 해제 + MP 페널티 일괄 적용
                        // (cmd.itemHealAmount는 더 이상 사용하지 않음 — SO 필드에서 직접 계산)
                        var fx = ConsumableEffectApplier.ApplyEffects(cmd.actor, cmd.usedItem);

                        SyncPotionCount();

                        string msg = $"{cmd.actor.playerName} used {cmd.usedItem.itemName}";
                        if (fx.hpHealed > 0) msg += $" — recovered {fx.hpHealed} HP";
                        if (fx.mpHealed > 0) msg += $" — recovered {fx.mpHealed} MP";
                        if (fx.mpLost   > 0) msg += $" — lost {fx.mpLost} MP";
                        if (fx.curesApplied != null && fx.curesApplied.Count > 0)
                            msg += $" — cured {string.Join(", ", fx.curesApplied)}";
                        AddMessage(msg + "!");
                    }
                }
                break;
            case "defend":
                Debug.Log($"[ExecuteAllyResolution] DEFEND case triggered for {cmd.actor.playerName}");
                cmd.actor.Defend();
                
                // 워리어의 Shield Wall 패시브 적용
                PartyRole role = GetPartyRole(cmd.actor);
                Debug.Log($"[ExecuteAllyResolution] Actor role: {role}");
                
                if (role == PartyRole.Warrior)
                {
                    Debug.Log("[ExecuteAllyResolution] Applying Shield Wall effect");
                    // Shield Wall 효과: 방어력 +20%
                    cmd.actor.defenseBuffAmount = 20f;
                    AddMessage($"{cmd.actor.playerName} is defending with Shield Wall!");
                }
                else
                {
                    Debug.Log($"[ExecuteAllyResolution] Not a warrior, role is {role}");
                    AddMessage($"{cmd.actor.playerName} is defending!");
                }
                break;
            default:
                Debug.LogWarning($"[ExecuteAllyResolution] Unknown action type: {cmd.actionType}");
                break;
        }

        UpdateStatusUI();
        UpdatePotionUI();
    }

    // Shield Wall 시각 효과 (워리어 UI 반짝임)
    private IEnumerator ShieldWallVisualEffect(PlayerStats warrior)
    {
        Debug.Log($"[ShieldWall] Effect started for {warrior.playerName}");
        
        // 파티 상태 패널에서 워리어의 UI 찾기
        if (playerStatusPanel == null)
        {
            Debug.LogWarning("[ShieldWall] playerStatusPanel is null!");
            yield break;
        }
        
        Debug.Log($"[ShieldWall] playerStatusPanel found: {playerStatusPanel.name}");
        
        // 워리어의 인덱스 찾기
        int warriorIndex = activePartyMembers.IndexOf(warrior);
        Debug.Log($"[ShieldWall] Warrior index: {warriorIndex}, Active party count: {activePartyMembers.Count}");
        
        if (warriorIndex < 0)
        {
            Debug.LogWarning("[ShieldWall] Warrior not found in active party!");
            yield break;
        }
        
        // 워리어의 슬롯 찾기
        string slotName = $"PartySlot_{warriorIndex + 1}";
        Transform slotTransform = GetPartySlotTransform(warriorIndex);
        Debug.Log($"[ShieldWall] Looking for slot: {slotName}, Found: {slotTransform != null}");
        
        if (slotTransform == null)
        {
            Debug.LogWarning($"[ShieldWall] Slot {slotName} not found!");
            yield break;
        }
        
        Debug.Log($"[ShieldWall] Creating shield effect background");
        
        // 슬롯의 기존 Image는 투명하므로, 새로운 배경 이미지를 생성
        GameObject effectBg = new GameObject("ShieldEffect", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        effectBg.transform.SetParent(slotTransform, false);
        
        RectTransform effectRT = effectBg.GetComponent<RectTransform>();
        effectRT.anchorMin = Vector2.zero;
        effectRT.anchorMax = Vector2.one;
        effectRT.offsetMin = Vector2.zero;
        effectRT.offsetMax = Vector2.zero;
        effectRT.SetAsFirstSibling(); // 텍스트 뒤에 배치
        
        UnityEngine.UI.Image effectImage = effectBg.GetComponent<UnityEngine.UI.Image>();
        
        // 철갑 효과: 밝은 은색/파란색 반짝임 (3회)
        Color shieldColor = new Color(0.6f, 0.8f, 1f, 0.4f); // 밝은 청은색
        Color transparentColor = new Color(0.6f, 0.8f, 1f, 0f);
        
        Debug.Log("[ShieldWall] Starting blink animation (3 cycles)");
        
        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"[ShieldWall] Blink cycle {i + 1}/3");
            
            // 밝게 (페이드 인)
            float elapsed = 0f;
            float duration = 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                effectImage.color = Color.Lerp(transparentColor, shieldColor, t);
                yield return null;
            }
            effectImage.color = shieldColor;
            
            // 어둡게 (페이드 아웃)
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                effectImage.color = Color.Lerp(shieldColor, transparentColor, t);
                yield return null;
            }
            effectImage.color = transparentColor;
        }
        
        Debug.Log("[ShieldWall] Effect complete, destroying effect object");
        
        // 효과 오브젝트 제거
        Destroy(effectBg);
    }

    // ─────────────────────────────────────────────────────────────────
    // [Enemy AI] 매 턴 행동 "선택" 레이어 (decision layer)
    //   PlanNeo(가드/Fallback) → Arc(Delta 후보·Sigma 가중치·Omega 리스크)
    //   → Zeta(역전) → Quantum(노이즈) 순으로 가중 랜덤 선택.
    //   ※ 이번 단계는 "선택"만 구현한다. Skill/Defend/Flee의 실제 실행 경로는
    //     아직 없으므로 ExecuteEnemyTurn에서 평타로 fallback(PlanNeo)하며,
    //     선택 결과는 로그로만 남긴다. 실행 경로는 다음 단계에서 연결.
    // ─────────────────────────────────────────────────────────────────
    private enum EnemyActionType { Attack, Skill, Defend, Flee }

    private struct EnemyActionDecision
    {
        public EnemyActionType type;
        public SkillData skill;   // type == Skill일 때만 유효
        public PlayerStats target;   // 타겟 지능이 선택한 공격 대상 (null이면 랜덤)
    }

    /// <summary>AIPattern별 타겟 선택. null 반환 시 ExecuteEnemyTurn이 랜덤으로 폴백.</summary>
    private PlayerStats SelectEnemyTarget(EnemyStats enemy, Abyssdawn.AIPattern pattern)
    {
        List<PlayerStats> alive = activePartyMembers
            .Where(p => p != null && p.currentHP > 0)
            .ToList();
        if (alive.Count == 0) return null;

        switch (pattern)
        {
            case Abyssdawn.AIPattern.Aggressive:
            {
                // HP 낮을수록 가중치 높음 (역수 방식) — 다키스트 던전 참고
                float totalWeight = 0f;
                var weights = new float[alive.Count];
                for (int i = 0; i < alive.Count; i++)
                {
                    float hpRatio = (float)alive[i].currentHP / Mathf.Max(1, alive[i].maxHP);
                    // HP 비율 역수. 최소 0.1 보장 (HP 0 방지)
                    weights[i] = 1f / Mathf.Max(0.1f, hpRatio);
                    totalWeight += weights[i];
                }
                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cumulative = 0f;
                for (int i = 0; i < alive.Count; i++)
                {
                    cumulative += weights[i];
                    if (roll <= cumulative)
                        return alive[i];
                }
                return alive[alive.Count - 1];
            }

            case Abyssdawn.AIPattern.Defensive:
                // 방어 중인 아군 제외, 후열 우선 (안전한 타겟)
                var nonDefending = alive.Where(p => !p.isDefending).ToList();
                var pool = nonDefending.Count > 0 ? nonDefending : alive;
                var backRow = pool.Where(p => SlotHelper.IsBackRow(p.currentSlot)).ToList();
                return backRow.Count > 0
                    ? backRow[UnityEngine.Random.Range(0, backRow.Count)]
                    : pool[UnityEngine.Random.Range(0, pool.Count)];

            case Abyssdawn.AIPattern.Support:
                // 후열 아군 우선 (힐러/마법사 견제)
                var back = alive.Where(p => SlotHelper.IsBackRow(p.currentSlot)).ToList();
                return back.Count > 0
                    ? back[UnityEngine.Random.Range(0, back.Count)]
                    : alive[UnityEngine.Random.Range(0, alive.Count)];

            default:
                return GetRandomAlivePartyMember();
        }
    }

    private EnemyActionDecision SelectEnemyAction(EnemyStats enemy)
    {
        var fallback = new EnemyActionDecision { type = EnemyActionType.Attack, skill = null };
        if (enemy == null) return fallback;

        Abyssdawn.AIPattern pattern = (enemy.sourceMonster != null)
            ? enemy.sourceMonster.AIPattern
            : Abyssdawn.AIPattern.Aggressive;

        // fallback 반환에도 타겟 지능 적용 (pattern 확정 후 설정)
        fallback.target = SelectEnemyTarget(enemy, pattern);

        float hpRatio = (enemy.maxHP > 0) ? (float)enemy.currentHP / enemy.maxHP : 1f;

        // [Phase] MonsterSO 전환점 기준으로 페이즈 판정
        float desperateThreshold = (enemy.sourceMonster != null) ? enemy.sourceMonster.DesperatePhaseThreshold : 0.5f;
        float criticalThreshold  = (enemy.sourceMonster != null) ? enemy.sourceMonster.CriticalPhaseThreshold  : 0.2f;

        bool isDesperatePhase = hpRatio <= desperateThreshold;
        bool isCriticalPhase  = hpRatio <= criticalThreshold;

        // [Arc/Delta] 행동 후보 + [Sigma] 패턴 기본 가중치 + [Omega] 리스크 조정
        var candidates = new List<(EnemyActionType type, SkillData skill, float weight)>();

        // 공격(평타) — 항상 후보
        candidates.Add((EnemyActionType.Attack, null, AttackBaseWeight(pattern)));

        // 스킬 — 시전 슬롯 조건 + MP 충족분만 후보. [Zeta]용 최강 스킬도 함께 추적.
        SkillData strongestCastable = null;
        float strongestPower = -1f;
        Debug.Log($"[EnemyAI-Skill] sourceMonster={enemy.sourceMonster?.MonsterName ?? "NULL"}, ActiveSkills={enemy.sourceMonster?.ActiveSkills?.Count ?? -1}");
        if (enemy.sourceMonster != null && enemy.sourceMonster.ActiveSkills != null)
        {
            foreach (var skill in enemy.sourceMonster.ActiveSkills)
            {
                if (skill == null || !CanEnemyCastSkill(enemy, skill)) continue;
                Debug.Log($"[EnemyAI-Skill-Check] skill={skill.skillName}, CanCast=True, w={SkillBaseWeight(pattern, skill):F2}");

                float w = SkillBaseWeight(pattern, skill);
                // [Omega] HP 소모 스킬 = 고위험 → 가중치 감소
                if (skill.hpCostPercent > 0f) w *= 0.6f;
                // [Omega] 명중률 낮을수록 소폭 감소 (accuracy 0~1 → 0.7~1.0 배)
                w *= Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(skill.accuracy));
                candidates.Add((EnemyActionType.Skill, skill, Mathf.Max(0.01f, w)));

                if (skill.maxMult > strongestPower)
                {
                    strongestPower = skill.maxMult;
                    strongestCastable = skill;
                }
            }
        }

        // [Phase] 방어 — Desperate 페이즈에서 후보 추가 (가중치 ×2)
        if (isDesperatePhase)
            candidates.Add((EnemyActionType.Defend, null, DefendBaseWeight(pattern) * 2f));

        // [Phase] 도망 — Critical 페이즈에서 후보 추가 (가중치 ×3)
        if (isCriticalPhase)
            candidates.Add((EnemyActionType.Flee, null, FleeBaseWeight(pattern) * 3f));

        // [Zeta] 역전 시나리오 — Critical 페이즈에서 15% 확률로 최강 스킬 강제 선택
        if (isCriticalPhase && strongestCastable != null && Random.value < 0.15f)
        {
            Debug.Log($"[EnemyAI/Zeta] {enemy.enemyName} 역전 발동 → 최강 스킬 '{strongestCastable.skillName}'");
            return new EnemyActionDecision { type = EnemyActionType.Skill, skill = strongestCastable, target = SelectEnemyTarget(enemy, pattern) };
        }

        // [Quantum] 최종 가중치에 ±10% 노이즈
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            float noisy = candidates[i].weight * Random.Range(0.9f, 1.1f);
            candidates[i] = (candidates[i].type, candidates[i].skill, noisy);
            totalWeight += noisy;
        }
        if (totalWeight <= 0f) return fallback;   // [PlanNeo] 안전

        // 가중 랜덤 선택
        float roll = Random.value * totalWeight;
        foreach (var c in candidates)
        {
            roll -= c.weight;
            if (roll <= 0f)
                return new EnemyActionDecision { type = c.type, skill = c.skill, target = SelectEnemyTarget(enemy, pattern) };
        }

        return fallback;   // [PlanNeo] Fallback
    }

    /// <summary>적이 지금 이 스킬을 시전할 수 있는지 — 시전 슬롯 조건 + MP 충족.</summary>
    private bool CanEnemyCastSkill(EnemyStats enemy, SkillData skill)
    {
        if (skill == null || skill.targeting == null) return false;
        if (enemy.currentMP < skill.mpCost) return false;

        // 회복 스킬: HP가 이미 최대치면 사용 불가
        if (skill is MonsterSkillData msd && msd.Category == MonsterSkillCategory.Heal)
        {
            if (enemy.currentHP >= enemy.maxHP) return false;
        }

        if (skill.hpCostPercent > 0f)
        {
            int hpCost = Mathf.RoundToInt(enemy.maxHP * skill.hpCostPercent / 100f);
            if (enemy.currentHP <= hpCost)
                return false;
        }

        // Center/None은 단독 배치 슬롯 — Slot1 기준으로 판정
        BattleSlot checkSlot = enemy.currentSlot;
        if (checkSlot == BattleSlot.None || checkSlot == BattleSlot.Center)
            checkSlot = BattleSlot.Slot1;

        return skill.targeting.CanCastFrom(checkSlot);
    }

    // [Sigma] 패턴별 기본 가중치 ─────────────────────────────────────────
    private float AttackBaseWeight(Abyssdawn.AIPattern p) => p switch
    {
        Abyssdawn.AIPattern.Aggressive => 1.0f,
        Abyssdawn.AIPattern.Defensive  => 0.6f,
        Abyssdawn.AIPattern.Support    => 0.5f,
        _ => 1.0f
    };

    private float SkillBaseWeight(Abyssdawn.AIPattern p, SkillData skill)
    {
        // 지원/버프 스킬 = 타겟이 적이 아닌 것(Ally/Self)
        bool isSupportSkill = skill.targeting != null &&
                              (skill.targeting.targetFaction == TargetFaction.Ally ||
                               skill.targeting.targetFaction == TargetFaction.Self);
        switch (p)
        {
            case Abyssdawn.AIPattern.Aggressive: return isSupportSkill ? 0.4f : 1.0f;
            case Abyssdawn.AIPattern.Defensive:  return isSupportSkill ? 0.7f : 0.6f;
            case Abyssdawn.AIPattern.Support:    return isSupportSkill ? 1.0f : 0.5f;
            default: return 1.0f;
        }
    }

    private float DefendBaseWeight(Abyssdawn.AIPattern p) => p switch
    {
        Abyssdawn.AIPattern.Aggressive => 0.3f,
        Abyssdawn.AIPattern.Defensive  => 1.0f,
        Abyssdawn.AIPattern.Support    => 0.7f,
        _ => 0.3f
    };

    private float FleeBaseWeight(Abyssdawn.AIPattern p) => p switch
    {
        Abyssdawn.AIPattern.Aggressive => 0.5f,
        Abyssdawn.AIPattern.Defensive  => 0.7f,
        Abyssdawn.AIPattern.Support    => 0.6f,
        _ => 0.5f
    };

    // [Enemy AI] 도망(Flee) 실행 — 리스트 제거 없이(인덱스 안정) 처치 처리 + EXP 0.
    //   ※ EnemyStats.isDead/spriteRenderer/statusUI는 private이라 접근 불가 →
    //     currentHP=0(IsDead()/AllEnemiesDefeated가 '처치됨'으로 인식) + GetComponent + StatusUI 프로퍼티로 보정.
    private IEnumerator HandleFlee(EnemyStats enemy)
    {
        // 1. 메시지
        AddMessage($"{enemy.enemyName} fled from battle!");
        yield return new WaitForSeconds(actionDelay);

        // 2. 상태 처리 (리스트 제거 없이 — 인덱스 안정성 보장)
        enemy.currentHP = 0;    // IsDead()/AllEnemiesDefeated가 '처치됨'으로 인식 (isDead는 private이라 대체)
        enemy.expReward = 0;    // EXP 없음
        enemy.activeStatusEffects.Clear();

        // 3. 시각 처리 (spriteRenderer/statusUI는 private → 접근 가능한 경로로)
        var sr = enemy.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        Collider2D col = enemy.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (enemy.StatusUI != null) enemy.StatusUI.SetActive(false);

        // 4. 전투 종료 체크 — 기존 AllEnemiesDefeated() 경로 활용
        UpdateStatusUI();
        CheckBattleEnd();
    }

    private IEnumerator ExecuteEnemyTurn(EnemyStats enemy)
    {
        if (enemy == null || enemy.currentHP <= 0 || enemy.IsDead()) yield break;
        if (enemy.IsStunned())
        {
            AddMessage($"{enemy.enemyName} is <color=#FFD700>stunned</color> and cannot act!");
            // 모으던 힘이 스턴으로 흩어진다 (모아 치기를 끊는 방법)
            if (enemy.pendingChargeSkill != null)
            {
                enemy.pendingChargeSkill = null;
                enemy.pendingChargeTarget = null;
                BattleFx.ClearIntent(enemy);
                AddMessage($"{enemy.enemyName}'s gathered power fades away!");
            }
            yield break;
        }

        yield return new WaitForSeconds(actionDelay);

        // [모아 치기] 지난 턴에 예고했던 스킬을 지금 발동
        if (enemy.pendingChargeSkill != null)
        {
            SkillData charged = enemy.pendingChargeSkill;
            PlayerStats chargedTarget = enemy.pendingChargeTarget;
            enemy.pendingChargeSkill = null;
            enemy.pendingChargeTarget = null;
            BattleFx.ClearIntent(enemy);
            if (chargedTarget == null || chargedTarget.currentHP <= 0) chargedTarget = GetRandomAlivePartyMember();
            if (chargedTarget != null)
            {
                AddMessage($"<color=#FF7A30>{enemy.enemyName} unleashes {charged.skillName}!</color>");
                yield return StartCoroutine(ExecuteEnemySkill(enemy, chargedTarget, charged));
            }
            yield break;
        }

        // [Enemy AI] 행동 선택 레이어 — 이번 단계는 선택만. Skill/Defend/Flee 실행 경로 미구현 → 평타 fallback.
        EnemyActionDecision decision = SelectEnemyAction(enemy);
        Debug.Log($"[EnemyAI] {enemy.enemyName} (AI={(enemy.sourceMonster != null ? enemy.sourceMonster.AIPattern.ToString() : "?")}) → 선택: {decision.type}{(decision.skill != null ? $" ('{decision.skill.skillName}')" : "")}");
        if (decision.type == EnemyActionType.Defend)
        {
            enemy.isDefending = true;
            AddMessage($"{enemy.enemyName} takes a defensive stance!");
            yield return new WaitForSeconds(actionDelay);
            UpdateStatusUI();
            yield break;
        }
        if (decision.type == EnemyActionType.Flee)
        {
            yield return StartCoroutine(HandleFlee(enemy));
            yield break;
        }
        if (decision.type == EnemyActionType.Skill && decision.skill != null)
        {
            // Self 타겟 스킬(버프 등)은 플레이어 타겟 불필요
            if (decision.skill is MonsterSkillData msd &&
                msd.targeting.targetFaction == TargetFaction.Self)
            {
                yield return StartCoroutine(ExecuteEnemySkill(enemy, null, decision.skill));
                yield break;
            }

            PlayerStats skillTarget = decision.target ?? GetRandomAlivePartyMember();
            if (skillTarget != null && skillTarget.currentHP > 0)
            {
                // [모아 치기] 이번 턴은 예고만 — 다음 자기 턴에 발동 (그사이 방어·선제 처치·스턴으로 대응)
                if (decision.skill is MonsterSkillData chargeSkill && chargeSkill.chargeTurns > 0)
                {
                    enemy.pendingChargeSkill = chargeSkill;
                    enemy.pendingChargeTarget = skillTarget;
                    string msg = string.IsNullOrEmpty(chargeSkill.chargeMessage)
                        ? $"{enemy.enemyName} is gathering power!"
                        : string.Format(chargeSkill.chargeMessage, enemy.enemyName);
                    AddMessage($"<color=#FF7A30>{msg}</color>");
                    BattleFx.SetIntent(enemy, chargeSkill.chargeIntentLabel);
                    Debug.Log($"[EnemyAI] {enemy.enemyName} 모아 치기 예고: {chargeSkill.skillName} → 다음 턴 {skillTarget.playerName}");
                    yield return new WaitForSeconds(actionDelay);
                    yield break;
                }
                yield return StartCoroutine(ExecuteEnemySkill(enemy, skillTarget, decision.skill));
                yield break;
            }
        }

        // 적은 랜덤 파티 멤버 공격 (타겟 지능이 고른 대상 우선, 없으면 랜덤)
        PlayerStats target = decision.target ?? GetRandomAlivePartyMember();

        // [Bite] 기본공격 override는 MonsterSO.basicAttackOverride로 직접 지정 (Race 자동 분기·Resources.Load 폐기).
        MonsterSkillData biteOverride = (enemy != null && enemy.sourceMonster != null)
            ? enemy.sourceMonster.BasicAttackOverride
            : null;

        if (target != null && target.currentHP > 0)
        {
            if (!RollPhysicalHit_EnemyVsPlayer(enemy, target, biteOverride))
            {
                AddMessage($"{target.playerName} evaded {enemy.enemyName}'s attack!");
                BattleFx.AllyMiss(target);
            }
            else
            {
                // [StatMod 5단계] CritChance는 Flat(퍼센트포인트 가산) 전용 설계 — baseValue=0f는 가산 전용 사용.
                bool critical = CheckCritical(enemy.luck, (biteOverride != null && biteOverride.isBasicAttackOverride ? biteOverride.critBonusPercent : 0f)
                    + enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance, 0f));

                // [StatMod 4단계] 적 공격력에 상태이상/버프 배율 적용.
                int effectiveAttack = Mathf.FloorToInt(
                    enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack, enemy.attack));

                Debug.Log($"[BattleLog] Enemy Attack: {effectiveAttack}, Player Defense: {target.Defense} (base: {target.baseDefense} + bonus: {target.GetDefenseBonus()}), Critical: {critical}");

                // [Bite] Beast 평타 대체 시 공격력 보정
                if (biteOverride != null && biteOverride.isBasicAttackOverride)
                {
                    effectiveAttack = Mathf.RoundToInt(effectiveAttack * biteOverride.atkMultiplierOverride);
                }

                int damage = CalculateDQDamage(
                    effectiveAttack,
                    Mathf.FloorToInt(target.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Defense, target.Defense)),
                    critical,
                    (biteOverride != null && biteOverride.isBasicAttackOverride) ? biteOverride.randomRollMaxOverride : 1.15f,
                    enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage, 1.5f)
                );
                damage = ApplySlotDamageToTarget(damage, target.currentSlot, target.playerName);

                if (target.isDefending)
                {
                    damage = Mathf.FloorToInt(damage * (1f - target.defenceReduction));
                    target.isDefending = false;
                }

                // 방패 블록 판정
                if (TryBlock(target, out float blockReduction))
                {
                    damage = Mathf.Max(0, Mathf.FloorToInt(damage - blockReduction));
                    AddMessage($"{target.playerName} blocked the attack! (DR {blockReduction:F0})");
                    if (damage == 0)
                    {
                        AddMessage($"Fully blocked!");
                        UpdateStatusUI();
                        CheckBattleEnd();
                        yield break;
                    }
                }

                Debug.Log($"[BattleLog] Enemy Final Damage: {damage}");

                int hpBefore = target.currentHP;
                if (critical) BattleFx.MarkAllyCritical();
                target.TakeDamage(damage);
                AddMessage(critical ? $"{enemy.enemyName} critical hit! {target.playerName} took {damage} damage!" :
                                      $"{enemy.enemyName} attacked {target.playerName} and dealt {damage} damage!");
                // [LastStand] HP가 0이 아닌 1로 유지됐다면 불굴 발동
                if (hpBefore > 1 && target.currentHP == 1 && target.HasSpecialEffect("LastStand"))
                {
                    AddMessage($"★ {target.playerName}'s Last Stand activates! Death avoided!");
                }
                // 아군이 데미지를 받으면 UI 흔들림
                ShakePlayerStatusUI(target);
            }
        }

        UpdateStatusUI();
    }

    // 모든 저주 효과 처리 (턴 종료 시)
    private IEnumerator ProcessAllIgniteDamage()
    {
        bool anyCurseDamage = false;

        // 아군 상태이상 데미지 처리
        foreach (var member in activePartyMembers)
        {
            if (member != null && member.currentHP > 0)
            {
                if (member.activeStatusEffects.Count > 0)
                {
                    // 이번 턴 DoT를 발생시킬 효과 미리 스냅샷 (appliedThisTurn=false && DoT > 0)
                    var dotEffects = member.activeStatusEffects
                        .Where(e => !e.appliedThisTurn && e.data.physicalDamagePerTurn > 0f)
                        .ToList();

                    int hpBefore = member.currentHP;
                    member.ProcessStatusEffectsEndOfTurn();

                    if (member.currentHP < hpBefore)
                    {
                        foreach (var se in dotEffects)
                        {
                            int dmg = Mathf.Max(1, Mathf.FloorToInt(member.maxHP * se.data.physicalDamagePerTurn));
                            string col = GetStatusColor(se.data.effectType);
                            AddMessage($"{member.playerName} suffered <color={col}>{dmg} damage</color> from <color={col}>{se.data.effectType}</color>!");
                        }
                        ShakePlayerStatusUI(member);
                        anyCurseDamage = true;
                    }
                }
                member.TickStatModifiers();   // ← 조건 밖으로 이동 (activeStatusEffects가 없어도 항상 호출)
            }
        }

        // 적 상태이상 데미지 처리
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null && enemy.currentHP > 0 && !enemy.IsDead())
            {
                if (enemy.activeStatusEffects.Count > 0)
                {
                    var dotEffects = enemy.activeStatusEffects
                        .Where(e => !e.appliedThisTurn && e.data.physicalDamagePerTurn > 0f)
                        .ToList();

                    int hpBefore = enemy.currentHP;
                    enemy.ProcessStatusEffectsEndOfTurn();

                    if (enemy.currentHP < hpBefore)
                    {
                        foreach (var se in dotEffects)
                        {
                            int dmg = Mathf.Max(1, Mathf.FloorToInt(enemy.maxHP * se.data.physicalDamagePerTurn));
                            string col = GetStatusColor(se.data.effectType);
                            AddMessage($"{enemy.enemyName} suffered <color={col}>{dmg} damage</color> from <color={col}>{se.data.effectType}</color>!");
                        }
                        enemy.UpdateStatusUI();
                        anyCurseDamage = true;
                    }
                }
                enemy.TickStatModifiers();   // ← 조건 밖으로 이동 (activeStatusEffects가 없어도 항상 호출)
            }
        }

        // 저주 데미지가 있었으면 UI 업데이트 및 딜레이
        if (anyCurseDamage)
        {
            UpdateStatusUI();
            yield return new WaitForSeconds(actionDelay);
        }
    }
    
    // 아군 UI 흔들림 효과
    /// <summary>아군 상태 카드(배경)의 RectTransform — 피해 숫자 팝업 위치용 (BattleFx).</summary>
    public RectTransform GetPlayerCardRect(PlayerStats player)
    {
        int i = activePartyMembers.IndexOf(player);
        if (i < 0) return null;
        if (i < playerStatusBackgrounds.Count && playerStatusBackgrounds[i] != null) return playerStatusBackgrounds[i].rectTransform;
        if (i < playerStatusTexts.Count && playerStatusTexts[i] != null) return playerStatusTexts[i].rectTransform;
        return null;
    }

    public void ShakePlayerStatusUI(PlayerStats player)
    {
        if (player == null || playerStatusTexts.Count == 0) return;
        
        // 해당 플레이어의 인덱스 찾기
        int playerIndex = activePartyMembers.IndexOf(player);
        if (playerIndex < 0 || playerIndex >= playerStatusTexts.Count) return;
        
        // 기존 흔들림 코루틴이 있으면 중지
        if (playerShakeCoroutines.ContainsKey(player) && playerShakeCoroutines[player] != null)
        {
            StopCoroutine(playerShakeCoroutines[player]);
        }
        
        // 새로운 흔들림 코루틴 시작
        playerShakeCoroutines[player] = StartCoroutine(ShakePlayerStatusUIRoutine(playerIndex));
    }
    
    // 아군 UI 흔들림 코루틴
    private IEnumerator ShakePlayerStatusUIRoutine(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= playerStatusTexts.Count) yield break;
        if (playerIndex >= playerStatusBackgrounds.Count) yield break;
        
        TextMeshProUGUI text = playerStatusTexts[playerIndex];
        Image background = playerStatusBackgrounds[playerIndex];
        
        if (text == null && background == null) yield break;
        
        // 원본 위치 저장
        Vector2 textOriginalPos = text != null ? text.rectTransform.anchoredPosition : Vector2.zero;
        Vector2 bgOriginalPos = background != null ? background.rectTransform.anchoredPosition : Vector2.zero;
        
        float duration = 0.2f;
        float magnitude = 5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            if (text == null && background == null) yield break;
            
            float offsetX = UnityEngine.Random.Range(-magnitude, magnitude);
            float offsetY = UnityEngine.Random.Range(-magnitude, magnitude);
            
            if (text != null)
            {
                text.rectTransform.anchoredPosition = textOriginalPos + new Vector2(offsetX, offsetY);
            }
            if (background != null)
            {
                background.rectTransform.anchoredPosition = bgOriginalPos + new Vector2(offsetX, offsetY);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 원본 위치로 복원
        if (text != null)
        {
            text.rectTransform.anchoredPosition = textOriginalPos;
        }
        if (background != null)
        {
            background.rectTransform.anchoredPosition = bgOriginalPos;
        }
    }

    // MP 회복 시각 효과 (초록색 빛)
    private IEnumerator MPRecoveryGlowEffect(PlayerStats player)
    {
        if (player == null || playerStatusBackgrounds.Count == 0) yield break;
        
        // 해당 플레이어의 인덱스 찾기
        int playerIndex = activePartyMembers.IndexOf(player);
        if (playerIndex < 0 || playerIndex >= playerStatusBackgrounds.Count) yield break;
        
        Image background = playerStatusBackgrounds[playerIndex];
        if (background == null) yield break;
        
        // 초록색 빛 설정
        Color greenGlow = new Color(0f, 1f, 0f, 0.5f); // 밝은 초록색, 50% 투명도
        Color transparent = new Color(0f, 1f, 0f, 0f); // 완전 투명
        
        // 3번 펄스 효과
        int pulseCount = 3;
        float pulseDuration = 0.3f; // 각 펄스 지속 시간
        
        for (int i = 0; i < pulseCount; i++)
        {
            // 페이드 인 (투명 -> 초록색)
            float elapsed = 0f;
            while (elapsed < pulseDuration / 2f)
            {
                if (background == null) yield break;
                
                float t = elapsed / (pulseDuration / 2f);
                background.color = Color.Lerp(transparent, greenGlow, t);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 페이드 아웃 (초록색 -> 투명)
            elapsed = 0f;
            while (elapsed < pulseDuration / 2f)
            {
                if (background == null) yield break;
                
                float t = elapsed / (pulseDuration / 2f);
                background.color = Color.Lerp(greenGlow, transparent, t);
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 펄스 사이 짧은 대기
            if (i < pulseCount - 1)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // 최종적으로 투명으로 복원
        if (background != null)
        {
            background.color = transparent;
        }
    }


    // 적 위치 조정 코루틴 (발끝을 같은 선상에 맞춤)
    private IEnumerator AdjustEnemyPositions(List<GameObject> enemyObjects, float targetY)
    {
        // 한 프레임 대기하여 bounds가 제대로 계산되도록 함
        yield return null;
        yield return new WaitForSeconds(0.1f); // UI 생성 대기

        foreach (GameObject enemyObj in enemyObjects)
        {
            if (enemyObj == null) continue;

            SpriteRenderer sr = enemyObj.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null && sr.enabled)
            {
                // 발끝(bounds.min.y)이 targetY에 오도록 조정
                float currentBottom = sr.bounds.min.y;
                float adjustment = targetY - currentBottom;
                Vector3 currentPos = enemyObj.transform.position;
                Vector3 newPos = new Vector3(currentPos.x, currentPos.y + adjustment, currentPos.z);
                enemyObj.transform.position = newPos;

                // EnemyStats의 originalPosition도 업데이트
                EnemyStats enemyStats = enemyObj.GetComponent<EnemyStats>();
                if (enemyStats != null)
                {
                    enemyStats.SetOriginalPosition(newPos);
                    enemyStats.SetWorldSpaceStatusUIEnabled(false);
                }
            }
        }
        
        // 모든 적의 UI가 생성될 때까지 추가 대기
        yield return new WaitForSeconds(0.2f);
        
        // 모든 적의 UI 상태 업데이트
        foreach (GameObject enemyObj in enemyObjects)
        {
            if (enemyObj == null) continue;
            EnemyStats enemyStats = enemyObj.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.UpdateStatusUI();
            }
        }
    }

    /// <summary>디버그/외부 조회용 — activePartyMembers는 private이라 직접 접근 불가.</summary>
    public List<PlayerStats> GetActivePartyMembers() => activePartyMembers;

    /// <summary>디버그/외부 조회용 — activeEnemies는 private이라 직접 접근 불가.</summary>
    public List<EnemyStats> GetActiveEnemies() => activeEnemies;

    private PlayerStats GetRandomAlivePartyMember()
    {
        List<PlayerStats> alive = activePartyMembers.Where(p => p != null && p.currentHP > 0).ToList();
        if (alive.Count == 0) return null;

        // Hero와 Warrior가 더 많이 공격받도록 가중치 적용
        List<PlayerStats> weighted = new List<PlayerStats>();
        foreach (var member in alive)
        {
            int weight = GetPartySlotWeight(member);
            for (int i = 0; i < weight; i++)
            {
                weighted.Add(member);
            }
        }

        return weighted[UnityEngine.Random.Range(0, weighted.Count)];
    }

    private int GetPartySlotWeight(PlayerStats member)
    {
        PartyRole role = GetPartyRole(member);
        switch (role)
        {
            case PartyRole.Hero:
            case PartyRole.Warrior:
                return 3; // 높은 가중치
            default:
                return 1; // 낮은 가중치
        }
    }

    // -------------------- 버튼 이벤트 --------------------
    /// <summary>
    /// 상단 적 카드 버튼용. slotNumber = 1~4 (카드 순서 = 카드에 표시된 번호).
    /// 카드 배치와 같은 규칙(GetEnemyByCardNumber)으로 누른 순간의 적을 찾으므로,
    /// 몬스터가 자리이동해도 항상 그 카드에 지금 표시된 적의 상태창이 열린다.
    /// </summary>
    public void OnEnemyStatusCardClicked(int slotNumber)
    {
        EnemyStats es = GetEnemyByCardNumber(slotNumber);

        if (enemyInspectPanel == null)
        {
            Debug.LogWarning("[BattleManager] enemyInspectPanel이 연결되지 않았습니다.");
            return;
        }

        if (es == null || es.IsDead()) return;

        enemyInspectPanel.Show(es);
    }

    /// <summary>
    /// 상단 카드 n번(1-based)에 표시 중인 적. AssignDisplayNumbers와 똑같은 규칙:
    /// 죽은 적 포함 전체를 currentSlot 오름차순(Slot1~7 → Center)으로 정렬한 n번째.
    /// (enemyLine은 Slot1~4만 담아 단독 배치(Center)를 못 찾고,
    ///  GetMonsterByNumber는 죽은 적을 빼고 세어 사망 후 카드와 번호가 어긋나므로 쓰지 않는다)
    /// </summary>
    private EnemyStats GetEnemyByCardNumber(int cardNumber)
    {
        if (cardNumber < 1 || activeEnemies == null) return null;

        var ordered = new List<(EnemyStats stats, int slotKey)>();
        foreach (EnemyStats es in activeEnemies)
        {
            if (es == null) continue;
            int key = (int)es.currentSlot;
            if (key == 0) key = int.MaxValue;
            ordered.Add((es, key));
        }
        ordered.Sort((a, b) => a.slotKey.CompareTo(b.slotKey));

        int idx = cardNumber - 1;
        return idx < ordered.Count ? ordered[idx].stats : null;
    }

    public void OnAttackButton()
    {
        if (currentPhase != BattlePhase.Command || !playerTurn || battleEnded || waitingForTargetSelection || turnInProgress) return;
        
        // FightSubPanel 강제 종료
        if (battleUIManager != null && battleUIManager.fightSubPanel != null)
        {
            battleUIManager.fightSubPanel.SetActive(false);
        }

        if (activeEnemies.Count == 0)
        {
            AddMessage("No enemies to attack!");
            return;
        }
        if (currentControlledMember == null)
        {
            AddMessage("No active ally to command.");
            return;
        }

        waitingForTargetSelection = true;
        pendingAction = "attack";
        pendingSkill = null;
        // actionPanel은 타겟 선택 모드에서도 유지 (타겟 선택 취소 시 다시 보이도록)
        AddMessage("Select target to attack!");
        ShowBackButton();
    }

    public void UsePlayerSkill(SkillData skill)
    {
        if (currentPhase != BattlePhase.Command || !playerTurn || battleEnded || waitingForTargetSelection || turnInProgress) return;
        
        // FightSubPanel 강제 종료
        if (battleUIManager != null && battleUIManager.fightSubPanel != null)
        {
            battleUIManager.fightSubPanel.SetActive(false);
        }

        if (currentControlledMember == null)
        {
            AddMessage("No active ally to command.");
            return;
        }

        if (skill == null) return;

        // Weapon category check (safety guard — button should already be disabled)
        if (skill.weaponCategory != AbyssdawnBattle.WeaponCategory.None)
        {
            AbyssdawnBattle.WeaponCategory equipped = AbyssdawnBattle.WeaponCategory.None;
            if (currentControlledMember.statData != null && currentControlledMember.statData.rightHand != null)
                equipped = currentControlledMember.statData.rightHand.weaponCategory;
            if (equipped != skill.weaponCategory)
            {
                AddMessage($"Requires {skill.weaponCategory} equipped!");
                return;
            }
        }

        // MP 체크
        if (skill.mpCost > 0 && currentControlledMember.currentMP < skill.mpCost)
        {
            AddMessage("Not enough MP!");
            return;
        }

        // HP 체크 (HP 코스트가 있을 경우)
        if (skill.hpCostPercent > 0)
        {
            int hpCost = Mathf.FloorToInt(currentControlledMember.maxHP * (skill.hpCostPercent / 100f));
            if (currentControlledMember.currentHP <= hpCost)
            {
                AddMessage("Not enough HP!");
                return;
            }
        }

        // 즉시 발동 스킬 (회복, 방어 등 본인 대상)
        if (IsSelfTargetSkill(skill))
        {
            QueueAllyCommand(currentControlledMember, "skill", null, skill);
            return;
        }

        waitingForTargetSelection = true;
        pendingAction = "skill";
        pendingSkill = skill;
        if (skillPanel != null) skillPanel.SetActive(false);
        if (actionPanel != null) actionPanel.SetActive(false);
        AddMessage($"Select target for {skill.skillName}!");
        ShowBackButton();
    }

    /// <summary>
    /// 외부(BattleItemSlot 등)에서 직접 호출하는 아이템 사용 API.
    /// turnInProgress 락 + QueueAllyCommand("item") + Back 버튼 정리.
    /// </summary>
    public void UseItemInBattle(PlayerStats user, ConsumableItemSO item, int healAmount)
    {
        turnInProgress = true;
        QueueAllyCommand(user, "item", null, null, healAmount, true, item);
        HideBackButton();
    }

    public void OnItemButton()
    {
        if (currentPhase != BattlePhase.Command || !playerTurn || battleEnded || turnInProgress) return;

        // FightSubPanel 강제 종료
        if (battleUIManager != null && battleUIManager.fightSubPanel != null)
        {
            battleUIManager.fightSubPanel.SetActive(false);
        }

        if (currentControlledMember == null || currentControlledMember != player)
        {
            AddMessage("Only the Hero can use items.");
            return;
        }

        if (selectedConsumableItem == null)
        {
            AddMessage("No item selected!");
            return;
        }

        if (!HasPotionAvailable())
        {
            AddMessage("No potions left!");
            return;
        }

        turnInProgress = true;
        int heal = Mathf.RoundToInt(selectedConsumableItem.hpRecoveryPercent * player.maxHP);
        QueueAllyCommand(player, "item", null, null, heal, true, selectedConsumableItem);
        HideBackButton();
    }

    public void OnRunButton()
    {
        if (currentPhase != BattlePhase.Command || !playerTurn || battleEnded || turnInProgress) return;
        
        // FightSubPanel 강제 종료
        if (battleUIManager != null && battleUIManager.fightSubPanel != null)
        {
            battleUIManager.fightSubPanel.SetActive(false);
        }

        if (currentControlledMember == null || currentControlledMember != player)
        {
            AddMessage("Only the Hero can run.");
            return;
        }

        turnInProgress = true;
          AddMessage("You ran away!");
          HideCommandPanels();
          battleEnded = true;
          HideBackButton();
          ReturnToDungeon(1.5f);
      }

    public void OnDefendButton()
    {
        if (currentPhase != BattlePhase.Command || !playerTurn || battleEnded || waitingForTargetSelection || turnInProgress) return;
        
        // FightSubPanel 강제 종료
        if (battleUIManager != null && battleUIManager.fightSubPanel != null)
        {
            battleUIManager.fightSubPanel.SetActive(false);
        }

        if (currentControlledMember == null)
        {
            AddMessage("No active ally to command.");
            return;
        }

        // 워리어인 경우 즉시 Shield Wall 시각 효과 표시
        PartyRole role = GetPartyRole(currentControlledMember);
        if (role == PartyRole.Warrior)
        {
            Debug.Log("[OnDefendButton] Warrior defending - showing Shield Wall effect immediately");
            StartCoroutine(ShieldWallVisualEffect(currentControlledMember));
        }

        turnInProgress = true;
        QueueAllyCommand(currentControlledMember, "defend");
        HideBackButton();
    }

    // -------------------- Skill 메뉴 --------------------
    public void OnSkillButton()
    {
        Debug.Log("[BattleManager] OnSkillButton() called");
        
        if (currentPhase != BattlePhase.Command || !playerTurn || battleEnded)
        {
            Debug.LogWarning($"[BattleManager] Cannot show skill panel. Phase: {currentPhase}, playerTurn: {playerTurn}, battleEnded: {battleEnded}");
            return;
        }
        
        // 현재 캐릭터의 스킬 목록 가져오기
        List<SkillData> currentSkills = GetCurrentActorSkills();
        Debug.Log($"[BattleManager] Current actor skills count: {currentSkills.Count}");
        
        if (currentSkills.Count == 0)
        {
            AddMessage("No skills available!");
            Debug.LogWarning("[BattleManager] No skills available for current actor!");
            return;
        }
        
        // fightSubPanel 닫기 (BattleUIManager에서 처리하지만 여기서도 확인)
        if (battleUIManager != null && battleUIManager.fightSubPanel != null)
        {
            battleUIManager.fightSubPanel.SetActive(false);
            Debug.Log("[BattleManager] Closed fightSubPanel");
        }
        
        // actionPanel 닫기
        if (actionPanel != null)
        {
            actionPanel.SetActive(false);
            Debug.Log("[BattleManager] Closed actionPanel");
        }
        
        // 스킬 패널 표시 및 스킬 버튼 업데이트
        if (skillPanel != null)
        {
            Debug.Log($"[BattleManager] Activating skillPanel. Current state: activeSelf={skillPanel.activeSelf}, activeInHierarchy={skillPanel.activeInHierarchy}");
            
            // 스킬 패널의 모든 부모 활성화
            Transform parent = skillPanel.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    Debug.Log($"[BattleManager] Activating parent: {parent.name}");
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            // 스킬 패널 활성화
            skillPanel.SetActive(true);
            Debug.Log($"[BattleManager] skillPanel activated. New state: activeSelf={skillPanel.activeSelf}, activeInHierarchy={skillPanel.activeInHierarchy}");
            
            // Back 버튼 찾기 및 연결
            FindAndConnectBackButton();
            
            // 스킬 버튼 업데이트
            UpdateSkillPanelButtons(currentSkills);
            
            // Back 버튼이 보이도록 확실히 활성화 (스킬 버튼 업데이트 후)
            StartCoroutine(EnsureBackButtonVisible());
        }
        else
        {
            Debug.LogError("[BattleManager] skillPanel is null! Cannot show skill panel.");
        }
    }
    
    // 스킬 패널의 버튼 업데이트
    private void UpdateSkillPanelButtons(List<SkillData> skills)
    {
        // SkillPanel 내의 버튼 찾기
        if (skillPanel == null)
        {
            Debug.LogError("[BattleManager] skillPanel is null! Cannot update skill buttons.");
            return;
        }
        
        Debug.Log($"[BattleManager] UpdateSkillPanelButtons called. Found {skills.Count} skills.");
        Debug.Log($"[BattleManager] SkillPanel active: {skillPanel.activeSelf}, activeInHierarchy: {skillPanel.activeInHierarchy}");
        
        // 스킬 패널이 활성화되어 있는지 확인
        if (!skillPanel.activeInHierarchy)
        {
            Debug.LogWarning("[BattleManager] SkillPanel is not active in hierarchy! Activating and waiting...");
            skillPanel.SetActive(true);
            
            // 무한 루프 방지: 이미 코루틴이 실행 중인지 체크하거나 횟수 제한을 둘 수 있으나,
            // 여기서는 단순 활성화 후 한 프레임 대기 루틴만 실행
            StartCoroutine(DelayedUpdateSkillButtons(skills));
            return;
        }
        
        // 스킬 패널 내의 모든 버튼 찾기 (비활성화된 것도 포함)
        Button[] skillButtons = skillPanel.GetComponentsInChildren<Button>(true);
        List<Button> availableSkillButtons = new List<Button>();
        
        Debug.Log($"[BattleManager] Found {skillButtons.Length} total buttons in SkillPanel");
        
        foreach (Button btn in skillButtons)
        {
            // SkillBackButton은 제외
            if (btn == skillBackButton)
            {
                Debug.Log($"[BattleManager] Skipping SkillBackButton: {btn.name}");
                continue;
            }
            
            // 모든 버튼 추가 (SkillBackButton 제외)
            availableSkillButtons.Add(btn);
            Debug.Log($"[BattleManager] Found skill button: {btn.name}, Active: {btn.gameObject.activeSelf}, Interactable: {btn.interactable}");
        }
        
        Debug.Log($"[BattleManager] Available skill buttons (excluding back): {availableSkillButtons.Count}");
        
        // 1. 스킬 정렬 제거: 획득 순서 유지

        // 2. 페이지 계산
        int totalPages = Mathf.CeilToInt((float)skills.Count / SKILLS_PER_PAGE);
        if (currentSkillPage >= totalPages) currentSkillPage = Mathf.Max(0, totalPages - 1);
        
        int startIndex = currentSkillPage * SKILLS_PER_PAGE;
        int count = Mathf.Min(SKILLS_PER_PAGE, skills.Count - startIndex);
        
        // 현재 페이지의 스킬들만 추출
        List<SkillData> pageSkills = skills.GetRange(startIndex, count);
        
        Debug.Log($"[BattleManager] Displaying Page {currentSkillPage + 1}/{totalPages}, Skills: {count}");

        // 3. 버튼 생성 및 배치 (수동 레이아웃)
        CreateSkillButtons(pageSkills); // 기존 버튼 재사용 로직이 복잡하므로 재생성 방식 사용 (최적화 가능하지만 안전하게)

        // 4. 페이지 버튼 업데이트
        UpdatePaginationButtons(totalPages);
    }

    private void UpdatePaginationButtons(int totalPages)
    {
        if (skillPanel == null) return;

        // 버튼이 없으면 생성
        if (prevPageButton == null || nextPageButton == null)
        {
            CreatePaginationButtons();
        }

        if (prevPageButton != null)
        {
            prevPageButton.gameObject.SetActive(totalPages > 1);
            prevPageButton.interactable = currentSkillPage > 0;
        }

        if (nextPageButton != null)
        {
            nextPageButton.gameObject.SetActive(totalPages > 1);
            nextPageButton.interactable = currentSkillPage < totalPages - 1;
        }
    }

    private void CreatePaginationButtons()
    {
        TryAutoAssignPaginationUI();
        if (skillPanel == null)
        {
            Debug.LogWarning("[BattleManager] CreatePaginationButtons aborted: skillPanel is null.");
            return;
        }
        if (skillBackButton == null)
        {
            Debug.LogWarning("[BattleManager] CreatePaginationButtons aborted: skillBackButton is null. Assign it or name it 'BackButton'/'Back'.");
            return;
        }

        // Prev Button
        if (prevPageButton == null)
        {
            GameObject btnObj = new GameObject("PrevPageButton", typeof(RectTransform), typeof(Button), typeof(Image));
            btnObj.transform.SetParent(skillPanel.transform, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(40, 60);
            rt.anchoredPosition = new Vector2(25, 0);

            prevPageButton = btnObj.GetComponent<Button>();
            if (prevPageButton.image != null)
            {
                prevPageButton.image.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            }
            else
            {
                Debug.LogWarning("[BattleManager] PrevPageButton has no Image component.");
            }
            
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            TMPro.TextMeshProUGUI text = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            text.text = "<";
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 24;
            text.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            text.GetComponent<RectTransform>().sizeDelta = rt.sizeDelta;

            prevPageButton.onClick.AddListener(() => {
                if (currentSkillPage > 0)
                {
                    currentSkillPage--;
                    OnSkillButton(); // Refresh
                }
            });
        }

        // Next Button
        if (nextPageButton == null)
        {
            GameObject btnObj = new GameObject("NextPageButton", typeof(RectTransform), typeof(Button), typeof(Image));
            btnObj.transform.SetParent(skillPanel.transform, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(40, 60);
            rt.anchoredPosition = new Vector2(-25, 0);

            nextPageButton = btnObj.GetComponent<Button>();
            if (nextPageButton.image != null)
            {
                nextPageButton.image.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            }
            else
            {
                Debug.LogWarning("[BattleManager] NextPageButton has no Image component.");
            }

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            TMPro.TextMeshProUGUI text = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            text.text = ">";
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 24;
            text.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            text.GetComponent<RectTransform>().sizeDelta = rt.sizeDelta;

            nextPageButton.onClick.AddListener(() => {
                currentSkillPage++;
                OnSkillButton(); // Refresh
            });
        }
    }
    
    // 스킬 버튼 업데이트를 지연시키는 코루틴
    private IEnumerator DelayedUpdateSkillButtons(List<SkillData> skills)
    {
        Debug.Log("[BattleManager] DelayedUpdateSkillButtons started. Waiting for end of frame...");
        yield return new WaitForEndOfFrame();
        yield return null;
        
        if (skillPanel != null && skillPanel.activeInHierarchy)
        {
            Debug.Log("[BattleManager] SkillPanel is now active. Updating buttons...");
            UpdateSkillPanelButtons(skills);
        }
        else
        {
            Debug.LogWarning("[BattleManager] DelayedUpdateSkillButtons: SkillPanel still not active or became null.");
        }
    }
    


    // 스킬 습득 메서드
    public void LearnSkill(SkillData newSkill)
    {
        if (player == null) return;
        
        // 현재 스킬 목록 가져오기 (Hero 기준)
        List<SkillData> currentSkills = heroSkillCache; // Hero 스킬만 관리한다고 가정
        
        // 이미 배운 스킬인지 확인
        if (currentSkills.Exists(s => s.skillID == newSkill.skillID))
        {
            AddMessage($"Already learned {newSkill.skillName}!");
            return;
        }

        // 용량 확인
        if (currentSkills.Count >= MAX_SKILLS)
        {
            // 꽉 찼을 때: 확인 팝업
            if (battleUIManager != null)
            {
                battleUIManager.ShowConfirmationDialog(
                    $"Skill inventory is full.\nDiscard new skill '{newSkill.skillName}'?",
                    () => {
                        // Yes: 새 스킬 버림
                        AddMessage($"Discarded {newSkill.skillName}.");
                    },
                    () => {
                        // No: 새 스킬 배우고, 가장 비싼 스킬 버림
                        // 1. 정렬 (가장 비싼게 마지막)
                        currentSkills.Sort();
                        SkillData removedSkill = currentSkills[currentSkills.Count - 1];
                        currentSkills.RemoveAt(currentSkills.Count - 1);
                        currentSkills.Add(newSkill);
                        currentSkills.Sort(); // 다시 정렬
                        
                        AddMessage($"Learned {newSkill.skillName}!");
                        AddMessage($"Forgot {removedSkill.skillName} to make space.");
                    }
                );
            }
            else
            {
                Debug.LogError("[BattleManager] BattleUIManager not found for confirmation dialog!");
            }
        }
        else
        {
            // 공간 있음: 그냥 배움
            currentSkills.Add(newSkill);
            currentSkills.Sort();
            AddMessage($"Learned {newSkill.skillName}!");
        }
    }

    // Back 버튼을 찾아서 연결하는 메서드
    private void FindAndConnectBackButton()
    {
        Button foundBackButton = null;
        
        // 1. Inspector에 할당된 버튼 확인
        if (skillBackButton != null)
        {
            foundBackButton = skillBackButton;
            Debug.Log($"[BattleManager] Using Inspector-assigned skillBackButton: {foundBackButton.name}");
        }
        
        // 2. 스킬 패널에서 Back 버튼 찾기
        if (foundBackButton == null && skillPanel != null)
        {
            Button[] buttons = skillPanel.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string btnName = btn.name.ToLower();
                if (btn != null && btn.name == "BackButton")
                {
                    foundBackButton = btn;
                    skillBackButton = btn;
                    Debug.Log("[BattleManager] Found skillBackButton in panel: " + btn.name);
                    break;
                }
            }
        }
        
        // 3. 씬 전체에서 Back 버튼 찾기 (스킬 패널이 없을 수도 있음)
        if (foundBackButton == null)
        {
            Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (Button btn in allButtons)
            {
                string btnName = btn.name.ToLower();
                if (btnName.Contains("back") || btnName == "backbutton" || btnName == "backbutt")
                {
                    // 스킬 패널의 자식인지 확인
                    if (skillPanel != null && btn.transform.IsChildOf(skillPanel.transform))
                    {
                        foundBackButton = btn;
                        skillBackButton = btn;
                        Debug.Log($"[BattleManager] Found skillBackButton in scene: {btn.name}");
                        break;
                    }
                }
            }
        }
        
        // 4. Back 버튼 강제로 연결 (Inspector 설정 무시하고 코드에서 연결)
        if (foundBackButton != null)
        {
            // 모든 기존 리스너 제거 (Inspector에서 설정된 것도 포함)
            foundBackButton.onClick.RemoveAllListeners();
            // 코드에서 리스너 추가
            foundBackButton.onClick.AddListener(OnSkillBack);
            Debug.Log($"[BattleManager] skillBackButton '{foundBackButton.name}' listener FORCE CONNECTED to OnSkillBack()");
        }
        else
        {
            Debug.LogWarning("[BattleManager] skillBackButton not found! Please assign it in Inspector or name it 'Back'.");
        }
    }

    private void TryAutoAssignPaginationUI()
    {
        if (skillPanel == null)
        {
            GameObject foundPanel = GameObject.Find("SkillPanel");
            if (foundPanel != null)
            {
                skillPanel = foundPanel;
                Debug.Log("[BattleManager] Auto-assigned skillPanel by name: SkillPanel");
            }
        }

        if (skillPanel == null) return;

        if (skillBackButton == null)
        {
            Button[] buttons = skillPanel.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string btnName = btn.name.ToLower();
                if (btnName == "backbutton" || btnName == "back")
                {
                    skillBackButton = btn;
                    Debug.Log($"[BattleManager] Auto-assigned skillBackButton: {btn.name}");
                    break;
                }
            }
        }

        if (prevPageButton == null || nextPageButton == null)
        {
            Button[] buttons = skillPanel.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string btnName = btn.name.ToLower();
                if (prevPageButton == null && (btnName == "prevpagebutton" || btnName == "prevpage" || btnName == "prev"))
                {
                    prevPageButton = btn;
                    Debug.Log($"[BattleManager] Auto-assigned prevPageButton: {btn.name}");
                }
                if (nextPageButton == null && (btnName == "nextpagebutton" || btnName == "nextpage" || btnName == "next"))
                {
                    nextPageButton = btn;
                    Debug.Log($"[BattleManager] Auto-assigned nextPageButton: {btn.name}");
                }
            }
        }

        if (pageText == null)
        {
            TextMeshProUGUI[] texts = skillPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                string textName = text.name.ToLower();
                if (textName == "pagetext" || textName.Contains("page"))
                {
                    pageText = text;
                    Debug.Log($"[BattleManager] Auto-assigned pageText: {text.name}");
                    break;
                }
            }
        }
    }
    
    // Back 버튼 클릭 시 호출되는 메서드 (public으로 Inspector에서 연결 가능)
    public void OnSkillBack()
    {
        Debug.Log("[BattleManager] ========== OnSkillBack() CALLED ==========");
        
        // 1. 스킬 패널 닫기 (모든 방법으로 확실히 닫기)
        if (skillPanel != null)
        {
            Debug.Log($"[BattleManager] Before closing - skillPanel activeSelf: {skillPanel.activeSelf}, activeInHierarchy: {skillPanel.activeInHierarchy}");
            
            // skillPanel 변수로 직접 닫기
            skillPanel.SetActive(false);
            
            // GameObject.Find로도 찾아서 닫기 (혹시 다른 인스턴스가 있을 수 있음)
            GameObject foundPanel = GameObject.Find(skillPanel.name);
            if (foundPanel != null && foundPanel != skillPanel)
            {
                Debug.Log($"[BattleManager] Found another instance of skillPanel, closing it: {foundPanel.name}");
                foundPanel.SetActive(false);
            }
            
            Debug.Log($"[BattleManager] After closing - skillPanel activeSelf: {skillPanel.activeSelf}, activeInHierarchy: {skillPanel.activeInHierarchy}");
            Debug.Log($"[BattleManager] ✓ SkillPanel CLOSED successfully!");
        }
        else
        {
            Debug.LogWarning("[BattleManager] skillPanel is null! Trying to find it by name...");
            
            // skillPanel이 null이면 이름으로 찾기 시도
            GameObject found = GameObject.Find("SkillPanel");
            if (found != null)
            {
                Debug.Log($"[BattleManager] Found SkillPanel by name, closing: {found.name}");
                found.SetActive(false);
            }
            else
            {
                Debug.LogError("[BattleManager] Cannot find SkillPanel! Please assign it in Inspector.");
            }
        }
        
        // 2. FightSubPanel 열기
        if (battleUIManager != null)
        {
            Debug.Log("[BattleManager] Opening FightSubPanel...");
            battleUIManager.ShowFightSubPanel();
            Debug.Log("[BattleManager] ✓ FightSubPanel OPENED successfully!");
        }
        else
        {
            Debug.LogWarning("[BattleManager] battleUIManager is null! Cannot open FightSubPanel.");
        }
        
        Debug.Log("[BattleManager] ========== OnSkillBack() COMPLETE ==========");
    }

    // Back 버튼이 확실히 보이도록 하는 코루틴
    private IEnumerator EnsureBackButtonVisible()
    {
        // 한 프레임 대기 (스킬 패널이 완전히 활성화되도록)
        yield return null;
        
        if (skillBackButton == null)
        {
            Debug.LogWarning("[BattleManager] skillBackButton is null in EnsureBackButtonVisible!");
            yield break;
        }
        
        Debug.Log($"[BattleManager] ========== Ensuring Back Button Visible ==========");
        Debug.Log($"[BattleManager] Back button name: {skillBackButton.name}");
        Debug.Log($"[BattleManager] Before - activeSelf: {skillBackButton.gameObject.activeSelf}, activeInHierarchy: {skillBackButton.gameObject.activeInHierarchy}");
        
        // 1. Back 버튼의 모든 부모 활성화 (skillPanel까지)
        Transform backParent = skillBackButton.transform.parent;
        while (backParent != null)
        {
            if (!backParent.gameObject.activeSelf)
            {
                backParent.gameObject.SetActive(true);
                Debug.Log($"[BattleManager] Activated Back button parent: {backParent.name}");
            }
            backParent = backParent.parent;
        }
        
        // 2. Back 버튼 자체 활성화
        skillBackButton.gameObject.SetActive(true);
        skillBackButton.interactable = true;
        
        // 3. Back 버튼의 RectTransform 확인 및 수정
        RectTransform backRect = skillBackButton.GetComponent<RectTransform>();
        if (backRect != null)
        {
            // 크기가 0이면 기본 크기 설정
            if (backRect.sizeDelta.x <= 0 || backRect.sizeDelta.y <= 0)
            {
                Debug.LogWarning($"[BattleManager] Back button size is invalid: {backRect.sizeDelta}. Setting default size.");
                backRect.sizeDelta = new Vector2(100f, 30f);
            }
            
            // 위치 확인
            Debug.Log($"[BattleManager] Back button position: {backRect.anchoredPosition}, size: {backRect.sizeDelta}");
        }
        
        // 4. Back 버튼의 Image 컴포넌트 확인
        UnityEngine.UI.Image backImage = skillBackButton.GetComponent<UnityEngine.UI.Image>();
        if (backImage != null)
        {
            if (backImage.color.a <= 0)
            {
                Debug.LogWarning("[BattleManager] Back button image is transparent! Setting to visible.");
                Color c = backImage.color;
                c.a = 1f;
                backImage.color = c;
            }
        }
        else
        {
            Debug.LogWarning("[BattleManager] Back button has no Image component!");
        }
        
        // 5. Back 버튼의 텍스트 확인
        TMPro.TextMeshProUGUI backTextTMP = skillBackButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (backTextTMP != null)
        {
            backTextTMP.gameObject.SetActive(true);
            if (string.IsNullOrEmpty(backTextTMP.text))
            {
                backTextTMP.text = "Back";
            }
            Debug.Log($"[BattleManager] Back button text: {backTextTMP.text}");
        }
        else
        {
            UnityEngine.UI.Text backText = skillBackButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (backText != null)
            {
                backText.gameObject.SetActive(true);
                if (string.IsNullOrEmpty(backText.text))
                {
                    backText.text = "Back";
                }
                Debug.Log($"[BattleManager] Back button text: {backText.text}");
            }
        }
        
        Debug.Log($"[BattleManager] After - activeSelf: {skillBackButton.gameObject.activeSelf}, activeInHierarchy: {skillBackButton.gameObject.activeInHierarchy}");
        Debug.Log($"[BattleManager] ========== Back Button Visibility Check Complete ==========");
    }
    
    private void ExecuteAttack(PlayerStats attacker, EnemyStats target)
    {
        if (attacker == null || target == null) return;

        // [Bite] 동료의 원본 MonsterSO에 기본공격 override가 있으면 평타 대체.
        //   Hero는 companionSource == null → biteOverride = null → 아래 4곳 전부 기존과 동일 동작.
        MonsterSkillData biteOverride = (attacker.companionSource != null)
            ? attacker.companionSource.BasicAttackOverride
            : null;

        if (!RollPhysicalHit_PlayerVsEnemy(attacker, target, biteOverride))
        {
            AddMessage($"{target.enemyName} evaded {attacker.playerName}!");
            BattleFx.EnemyMiss(target);
        }
        else
        {
            // [StatMod 5단계] CritChance는 Flat(퍼센트포인트 가산) 전용 설계 — baseValue=0f는 가산 전용 사용.
            bool critical = CheckCritical(attacker.luck, (biteOverride != null && biteOverride.isBasicAttackOverride ? biteOverride.critBonusPercent : 0f)
                + attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance, 0f));

            // [Bite] ATK 보정용 로컬 (attacker.Attack은 읽기전용 computed라 로컬에 받아 곱함)
            // [StatMod 4단계] 상태이상/버프 배율 적용 후 Bite 배율 적용.
            int effectiveAttack = Mathf.FloorToInt(
                attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack, attacker.Attack));
            if (biteOverride != null && biteOverride.isBasicAttackOverride)
                effectiveAttack = Mathf.RoundToInt(effectiveAttack * biteOverride.atkMultiplierOverride);

            // [DEBUG LOG] 데미지 계산 전 스탯 확인
            Debug.Log($"[BattleLog] Player Attack Stats - Base: {attacker.baseAttack}, Bonus: {attacker.GetAttackBonus()}, Final: {attacker.Attack}");
            Debug.Log($"[BattleLog] Enemy Defense: {target.defense}, Critical: {critical}");

            bool isDual = IsDualWielding(attacker);
            int shownTotal = 0;

            if (isDual)
            {
                // ───────── 쌍수 기본 공격 ─────────
                // 1) 한손 기준 깡뎀 합
                int singleBaseDamage = CalculateDQDamage(effectiveAttack,
                    Mathf.FloorToInt(target.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Defense, target.defense)), false,
                    biteOverride != null && biteOverride.isBasicAttackOverride ? biteOverride.randomRollMaxOverride : 1.15f);
                singleBaseDamage = Mathf.Max(singleBaseDamage, 1);

                // 2) 쌍수 방어 파괴 공식
                // 타격당 방어 파괴 = 적 현재 방어력 × 계수 × 0.35~0.45
                // 2타 합산: 1타 후 남은 방어력으로 2타 계산
                float coeff = GetArmorBreakCoefficient(attacker);
                float remainingDef = target.defense;

                float f1 = UnityEngine.Random.Range(0.35f, 0.45f);
                float f2 = UnityEngine.Random.Range(0.35f, 0.45f);

                // 깡뎀 분배 (총합 × 0.35~0.45 두 번 = 0.7~0.9)
                int baseHit1 = Mathf.Max(1, Mathf.FloorToInt(singleBaseDamage * f1));
                int baseHit2 = Mathf.Max(1, Mathf.FloorToInt(singleBaseDamage * f2));

                int armorHit1 = 0;
                int armorHit2 = 0;

                if (coeff > 0f && remainingDef > 0f)
                {
                    float armor1 = remainingDef * coeff * f1;
                    remainingDef -= armor1;
                    float armor2 = Mathf.Max(0f, remainingDef) * coeff * f2;

                    armorHit1 = Mathf.Max(1, Mathf.RoundToInt(armor1));
                    armorHit2 = Mathf.Max(1, Mathf.RoundToInt(armor2));

                    // 방어력 누적 감소 (실제 target.defense 영구 감소)
                    int totalDefReduced = Mathf.Min(armorHit1 + armorHit2, target.defense);
                    target.defense = Mathf.Max(0, target.defense - totalDefReduced);
                    Debug.Log($"[ArmorBreak] Dual — defense reduced by {totalDefReduced}. Remaining: {target.defense}");
                }

                int hit1 = baseHit1 + armorHit1;
                int hit2 = baseHit2 + armorHit2;

                // 크리티컬이면 두 타 모두 동일 배율 적용
                if (critical)
                {
                    // [StatMod 5단계] 공격자 CritDamage 배율을 1회 계산해 재사용.
                    float critDmgMult = attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage, 1.5f);
                    hit1 = Mathf.Max(1, Mathf.FloorToInt(hit1 * critDmgMult));
                    hit2 = Mathf.Max(1, Mathf.FloorToInt(hit2 * critDmgMult));
                }

                Debug.Log($"[BattleLog] Dual basic attack - base1:{baseHit1}, base2:{baseHit2}, armor1:{armorHit1}, armor2:{armorHit2}, f1:{f1:F2}, f2:{f2:F2}");

                int hit1Slot = ApplySlotDamageToTarget(hit1, target.currentSlot, target.enemyName);
                int hit2Slot = ApplySlotDamageToTarget(hit2, target.currentSlot, target.enemyName);
                if (target.isDefending)
                {
                    hit1Slot = Mathf.FloorToInt(hit1Slot * (1f - target.defenceReduction));
                    hit2Slot = Mathf.FloorToInt(hit2Slot * (1f - target.defenceReduction));
                    target.isDefending = false;
                    AddMessage($"{target.enemyName} defended and reduced the damage!");
                }
                hit1Slot = ApplyPhysResist(hit1Slot, target);
                hit2Slot = ApplyPhysResist(hit2Slot, target);
                int applied1 = target.TakeDamage(hit1Slot, critical);
                int applied2 = 0;
                if (!target.IsDead())
                {
                    applied2 = target.TakeDamage(hit2Slot, critical);
                }

                shownTotal = applied1 + applied2;
                AddMessage(critical
                    ? $"Critical! {attacker.playerName} struck twice for {shownTotal}!"
                    : $"{attacker.playerName} struck twice for {shownTotal} damage!");
            }
            else
            {
                // 한손/방패: fn = 1.0, 깡뎀 + 방어 파괴 (추가딜)
                int singleBase = CalculateDQDamage(effectiveAttack,
                    Mathf.FloorToInt(target.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Defense, target.defense)), false,
                    biteOverride != null && biteOverride.isBasicAttackOverride ? biteOverride.randomRollMaxOverride : 1.15f);
                singleBase = Mathf.Max(singleBase, 1);

                float singleCoeff = GetArmorBreakCoefficient(attacker);
                int singleArmor = 0;
                int defenseReduced = 0;
                if (singleCoeff > 0f && target.defense > 0)
                {
                    // 방어구 파괴: 적 방어력의 X% — 최소 1 보장
                    singleArmor = Mathf.Max(1, Mathf.RoundToInt(target.defense * singleCoeff));
                    // 방어력 누적 감소 (실제로 target.defense를 영구 감소)
                    defenseReduced = Mathf.Min(singleArmor, target.defense);
                    target.defense = Mathf.Max(0, target.defense - defenseReduced);
                }

                if (critical)
                {
                    // [StatMod 5단계] 공격자 CritDamage 배율을 1회 계산해 재사용.
                    float critDmgMult = attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage, 1.5f);
                    singleBase  = Mathf.Max(1, Mathf.FloorToInt(singleBase  * critDmgMult));
                    singleArmor = Mathf.Max(0, Mathf.FloorToInt(singleArmor * critDmgMult));
                }

                int singleTotal = singleBase + singleArmor;
                Debug.Log($"[BattleLog] Single-wield attack - base:{singleBase}, armorBreak:{singleArmor}, defReduced:{defenseReduced}, total:{singleTotal}");

                int singleBaseSlot = ApplySlotDamageToTarget(singleBase, target.currentSlot, target.enemyName);
                int singleArmorSlot = ApplySlotDamageToTarget(singleArmor, target.currentSlot, target.enemyName);
                if (target.isDefending)
                {
                    singleBaseSlot = Mathf.FloorToInt(singleBaseSlot * (1f - target.defenceReduction));
                    singleArmorSlot = Mathf.FloorToInt(singleArmorSlot * (1f - target.defenceReduction));
                    target.isDefending = false;
                    AddMessage($"{target.enemyName} defended and reduced the damage!");
                }
                singleBaseSlot = ApplyPhysResist(singleBaseSlot, target);
                if (singleArmorSlot > 0) singleArmorSlot = ApplyPhysResist(singleArmorSlot, target);
                int applied1 = target.TakeDamage(singleBaseSlot, critical);
                int applied2 = 0;
                if (singleArmor > 0 && !target.IsDead())
                {
                    applied2 = target.TakeDamage(singleArmorSlot, false);
                }

                shownTotal = applied1 + applied2;
                string armorMsg = defenseReduced > 0 ? $" [Armor Break: DEF -{defenseReduced}]" : "";
                AddMessage(critical
                    ? $"Critical! {attacker.playerName} dealt {shownTotal}!{armorMsg}"
                    : $"{attacker.playerName} struck for {shownTotal} damage!{armorMsg}");
            }

            // ───────── 무기 저주 부여 ─────────
            if (!target.IsDead())
                TryApplyWeaponCurse(attacker, target);
        }

        UpdateStatusUI();
        CheckBattleEnd();
    }

    /// <summary>
    /// 장착 무기의 weaponCurse(StatusEffectSO)를 확인해 적에게 상태이상을 부여합니다.
    /// 부여 확률은 SO 내부의 physicalApplyChance를 사용합니다.
    /// 쌍수일 경우 양손 모두 독립적으로 확률 체크합니다.
    /// </summary>
    private void TryApplyWeaponCurse(PlayerStats attacker, EnemyStats target)
    {
        EquipmentData rightHand = null, leftHand = null;
        EquipmentManager em = attacker.GetComponent<EquipmentManager>();
        if (em != null)
        {
            rightHand = em.rightHand;
            leftHand  = em.leftHand;
        }
        else if (attacker.statData != null)
        {
            rightHand = attacker.statData.rightHand;
            leftHand  = attacker.statData.leftHand;
        }

        void CheckAndApply(EquipmentData weapon)
        {
            if (weapon == null || weapon.weaponCurses == null) return;
            foreach (var curse in weapon.weaponCurses)
            {
                if (curse == null) continue;
                bool applied = target.ApplyStatusEffect(curse);
                if (applied)
                {
                    string wCol = GetStatusColor(curse.effectType);
                    AddMessage($"{weapon.equipmentName} inflicted <color={wCol}>{curse.effectType}</color> on {target.enemyName}!");
                    Debug.Log($"[WeaponCurse] {weapon.equipmentName} → {curse.effectType} applied to {target.enemyName}");
                }
            }
        }

        CheckAndApply(rightHand);
        if (leftHand != rightHand)
            CheckAndApply(leftHand);
    }

    /// <summary>
    /// 방어자가 방패를 장착했고 블록 판정에 성공했는지 확인.
    /// 성공 시 피해 감소량을 out으로 반환합니다.
    /// </summary>
    private bool TryBlock(PlayerStats defender, out float damageReduction)
    {
        damageReduction = 0f;
        if (defender == null) return false;

        EquipmentData leftHand = null, rightHand = null;
        var em = defender.GetComponent<EquipmentManager>();
        if (em != null)
        {
            leftHand  = em.leftHand;
            rightHand = em.rightHand;
        }
        else if (defender.statData != null)
        {
            leftHand  = defender.statData.leftHand;
            rightHand = defender.statData.rightHand;
        }

        // 왼손(방패 슬롯) 또는 오른손에서 blockData 확인
        EquipmentData shield = leftHand ?? rightHand;
        if (shield == null || shield.blockData == null) return false;

        bool blocked = shield.blockData.RollBlock(defender.Defense);
        if (blocked)
        {
            damageReduction = shield.blockData.GetDamageReduction(defender.Defense);
            Debug.Log($"[Block] {defender.playerName} blocked! DR={damageReduction:F1} (chance={shield.blockData.GetBlockChance(defender.Defense)*100f:F1}%)");
        }
        return blocked;
    }

    /// <summary>
    /// 현재 공격자가 한손 무기 2개를 듀얼로 장착 중인지 판별
    /// </summary>
    private bool IsDualWielding(PlayerStats attacker)
    {
        if (attacker == null) return false;

        AbyssdawnBattle.EquipmentData right = null;
        AbyssdawnBattle.EquipmentData left = null;

        var eq = attacker.GetComponent<EquipmentManager>();
        if (eq != null) { right = eq.rightHand; left = eq.leftHand; }
        else if (attacker.statData != null) { right = attacker.statData.rightHand; left = attacker.statData.leftHand; }

        if (right == null || left == null) return false;
        if (right.equipmentType != AbyssdawnBattle.EquipmentType.Hand) return false;
        if (left.equipmentType != AbyssdawnBattle.EquipmentType.Hand) return false;
        // 방패(Shield)는 무기가 아니므로 쌍수에서 제외
        if (right.weaponCategory == AbyssdawnBattle.WeaponCategory.Shield) return false;
        if (left.weaponCategory == AbyssdawnBattle.WeaponCategory.Shield) return false;

        return true;
    }

    /// <summary>
    /// 현재 공격자가 사용하는 방어구 파괴 계수 합산
    /// - 한손/방패: 오른손 무기 계수만 사용
    /// - 쌍수: 양손 한손 무기의 계수를 합산
    /// </summary>
    private float GetArmorBreakCoefficient(PlayerStats attacker)
    {
        if (attacker == null) return 0f;

        AbyssdawnBattle.EquipmentData rightHand = null;
        AbyssdawnBattle.EquipmentData leftHand = null;

        var eq = attacker.GetComponent<EquipmentManager>();
        if (eq != null) { rightHand = eq.rightHand; leftHand = eq.leftHand; }
        else if (attacker.statData != null) { rightHand = attacker.statData.rightHand; leftHand = attacker.statData.leftHand; }

        if (rightHand == null) return 0f;

        float coeff = 0f;
        bool isDual = IsDualWielding(attacker);

        if (isDual)
        {
            coeff += rightHand.GetArmorBreakCoefficient();
            if (leftHand != null) coeff += leftHand.GetArmorBreakCoefficient();
        }
        else
        {
            if (rightHand.equipmentType == AbyssdawnBattle.EquipmentType.Hand ||
                rightHand.equipmentType == AbyssdawnBattle.EquipmentType.TwoHanded)
            {
                coeff = rightHand.GetArmorBreakCoefficient();
            }
        }

        return Mathf.Max(0f, coeff);
    }

    /// <summary>
    /// 무기 SO의 방어구 파괴 계수로 계산한 방어 퍼뎀 (적 방어력의 X% 고정 피해).
    /// 기본 공식(한손): 적 방어력 × armorBreakCoefficient.
    /// </summary>
    private int GetArmorBreakDamage(PlayerStats attacker, int targetDefense)
    {
        if (attacker == null || targetDefense <= 0) return 0;

        AbyssdawnBattle.EquipmentData weapon = null;
        var eq = attacker.GetComponent<EquipmentManager>();
        if (eq != null) weapon = eq.rightHand;
        else if (attacker.statData != null) weapon = attacker.statData.rightHand;

        if (weapon == null) return 0;
        if (weapon.equipmentType != AbyssdawnBattle.EquipmentType.Hand &&
            weapon.equipmentType != AbyssdawnBattle.EquipmentType.TwoHanded) return 0;

        float coeff = weapon.GetArmorBreakCoefficient();
        if (coeff <= 0f) return 0;

        float percent = attacker.Attack * coeff;
        float raw = targetDefense * percent;
        return Mathf.RoundToInt(raw);
    }

    private static bool IsSelfTargetEffect(EffectType type)
    {
        switch (type)
        {
            case EffectType.Recovery:
            case EffectType.BuffAttack:
            case EffectType.BuffDefense:
            case EffectType.BuffAgility:
            case EffectType.BuffMagic:
            case EffectType.BuffLuck:
            case EffectType.PassiveAttack:
            case EffectType.PassiveDefense:
            case EffectType.PassiveAgility:
            case EffectType.PassiveMagic:
            case EffectType.PassiveLuck:
                return true;
            default:
                return false;
        }
    }

    private bool IsSelfTargetSkill(SkillData skill)
    {
        if (skill == null) return false;

        // targeting.targetFaction이 Self면 무조건 Self 스킬
        if (skill.targeting != null &&
            skill.targeting.targetFaction == TargetFaction.Self)
            return true;

        if (skill.Effects == null || skill.Effects.Count == 0)
        {
            return skill.isRecovery || skill.isDefensive;
        }

        foreach (var effect in skill.Effects)
        {
            if (effect != null && IsSelfTargetEffect(effect.effectType)) return true;
        }

        return false;
    }

    private void ApplySelfEffects(PlayerStats attacker, SkillData skill)
    {
        if (attacker == null || skill == null || skill.Effects == null) return;

        foreach (var effect in skill.Effects)
        {
            if (effect == null) continue;

            switch (effect.effectType)
            {
                case EffectType.Recovery:
                    ApplyRecoveryEffect(attacker, skill, effect);
                    break;
                case EffectType.BuffDefense:
                    if (effect.effectAmount > 0)
                    {
                        attacker.defenseBuffAmount = effect.effectAmount;
                        AddMessage($"{attacker.playerName} used {skill.skillName}! Defense increased!");
                    }
                    break;
            }
        }

        // [MonsterSkillData Buff 분기] curseEffect.statModifiers 기반 Self 버프
        if (skill is MonsterSkillData msd && msd.Category == MonsterSkillCategory.Buff)
        {
            if (msd.curseEffect != null && msd.curseApplyChance > 0f &&
                UnityEngine.Random.value <= msd.curseApplyChance)
            {
                // 같은 source 스킬이 이미 있으면 제거 후 재추가 (타이머 갱신)
                attacker.RemoveStatModifiersFromSource(skill);
                foreach (var mod in msd.curseEffect.statModifiers)
                {
                    attacker.AddStatModifier(mod, skill, msd.curseEffect.physicalDuration);
                }
                AddMessage($"{attacker.playerName} uses {skill.skillName}!");
            }
        }
    }

    private void ApplyRecoveryEffect(PlayerStats attacker, SkillData skill, SkillEffect effect)
    {
        if (effect.effectAmount <= 0f) return;

        bool isMeditation = skill.skillName == "Meditation";
        if (effect.recoveryTarget == RecoveryTarget.MP || effect.recoveryTarget == RecoveryTarget.Both)
        {
            int recoverAmount = Mathf.FloorToInt(attacker.maxMP * (effect.effectAmount / 100f));
            attacker.currentMP = Mathf.Min(attacker.currentMP + recoverAmount, attacker.maxMP);
            AddMessage(isMeditation
                ? $"{attacker.playerName} meditated and recovered {recoverAmount} MP!"
                : $"{attacker.playerName} recovered {recoverAmount} MP!");

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SaveFromPlayer(attacker);
            }

            StartCoroutine(MPRecoveryGlowEffect(attacker));
        }

        if (effect.recoveryTarget == RecoveryTarget.HP || effect.recoveryTarget == RecoveryTarget.Both)
        {
            int recoverAmount = Mathf.FloorToInt(attacker.maxHP * (effect.effectAmount / 100f));
            attacker.Heal(recoverAmount);
            AddMessage($"{attacker.playerName} recovered {recoverAmount} HP!");
        }
    }

    private void ApplyCurseEffects(EnemyStats target, SkillData skill)
    {
        if (target == null || skill == null) return;

        // ── 신규 시스템: skill.curseEffect + skill.curseApplyChance ──
        // 확률은 스킬 단위에서만 1번 굴림 (EnemyStats 내부 이중 롤 없음)
        if (skill.curseEffect != null && skill.curseApplyChance > 0f)
        {
            if (UnityEngine.Random.value < skill.curseApplyChance)
            {
                // [분기] statModifiers가 있으면 activeStatModifiers 경로
                if (skill.curseEffect.statModifiers != null && skill.curseEffect.statModifiers.Count > 0)
                {
                    bool isMagic = skill.damageType == AbyssdawnBattle.DamageType.Magic;
                    int dur = isMagic ? skill.curseEffect.magicalDuration : skill.curseEffect.physicalDuration;
                    if (dur > 0)
                    {
                        int currentStacks = target.activeStatModifiers
                            .Count(m => m.source is SkillData sd && sd.skillID == skill.skillID);

                        if (currentStacks == 0)
                        {
                            for (int s = 0; s < 2; s++)
                                foreach (var mod in skill.curseEffect.statModifiers)
                                    target.AddStatModifier(mod, skill, dur);
                            AddMessage($"{target.enemyName} Accuracy reduced by 10%!");
                        }
                        else if (currentStacks < 4)
                        {
                            foreach (var mod in skill.curseEffect.statModifiers)
                                target.AddStatModifier(mod, skill, dur);
                            foreach (var am in target.activeStatModifiers
                                .Where(m => m.source is SkillData sd && sd.skillID == skill.skillID))
                                am.remainingTurns = dur;
                            AddMessage($"{target.enemyName} Accuracy further reduced! Stack {currentStacks + 1}");
                        }
                        else
                        {
                            foreach (var am in target.activeStatModifiers
                                .Where(m => m.source is SkillData sd && sd.skillID == skill.skillID))
                                am.remainingTurns = dur;
                            AddMessage($"{target.enemyName} Accuracy debuff timer refreshed!");
                        }
                    }
                }
                else
                {
                    // 기존 경로: activeStatusEffects
                    bool isMagic = skill.damageType == AbyssdawnBattle.DamageType.Magic;
                    int dur = isMagic ? skill.curseEffect.magicalDuration : skill.curseEffect.physicalDuration;
                    bool applied = target.ApplyStatusEffectDirect(skill.curseEffect, dur);
                    if (applied)
                    {
                        string cCol = GetStatusColor(skill.curseEffect.effectType);
                        AddMessage($"{target.enemyName} is afflicted with <color={cCol}>{skill.curseEffect.variantId}</color>!");
                    }
                }
            }
        }

        // ── 구버전 시스템: skill.Effects 리스트 (무기 장비 저주 등) ──
        if (skill.Effects == null) return;
        foreach (var effect in skill.Effects)
        {
            if (effect == null || effect.statusEffect == null || effect.statusEffectChance <= 0f) continue;

            if (UnityEngine.Random.Range(0f, 100f) < effect.statusEffectChance)
            {
                // 구버전은 EnemyStats 내부 physicalApplyChance 롤 유지 (하위 호환)
                bool applied = target.ApplyStatusEffect(effect.statusEffect);
                if (applied)
                {
                    string eCol = GetStatusColor(effect.statusEffect.effectType);
                    AddMessage($"{target.enemyName} is afflicted with <color={eCol}>{effect.statusEffect.effectType}</color>!");
                }
            }
        }
    }

    /// <summary>적 스킬이 플레이어(아군)에게 상태이상을 거는 경로</summary>
    private void ApplyCurseEffectsToPlayer(PlayerStats target, SkillData skill)
    {
        if (skill.curseEffect == null || skill.curseApplyChance <= 0f) return;

        // [StatMod] 대상의 StatusResist 배율 적용
        float chance = skill.curseApplyChance *
            target.ApplyStatModifiers(AbyssdawnBattle.ModStatType.StatusResist, 1f);
        if (UnityEngine.Random.value >= chance) return;

        // [분기] curseEffect에 statModifiers가 있으면 activeStatModifiers 경로
        if (skill.curseEffect.statModifiers != null && skill.curseEffect.statModifiers.Count > 0)
        {
            int dur = skill.damageType == DamageType.Physical
                ? skill.curseEffect.physicalDuration
                : skill.curseEffect.magicalDuration;
            if (dur <= 0) return;

            // 현재 같은 source(skill) 스택 수 확인
            int currentStacks = target.activeStatModifiers
                .Count(m => m.source is SkillData sd && sd.skillID == skill.skillID);

            if (currentStacks == 0)
            {
                // 첫 적용: 2스택 (-0.10)
                for (int s = 0; s < 2; s++)
                    foreach (var mod in skill.curseEffect.statModifiers)
                        target.AddStatModifier(mod, skill, dur);
                AddMessage($"{target.playerName} is afflicted! Accuracy reduced by 10%!");
            }
            else if (currentStacks < 4)
            {
                // 추가 중첩: 1스택 + 타이머 갱신
                foreach (var mod in skill.curseEffect.statModifiers)
                    target.AddStatModifier(mod, skill, dur);
                foreach (var am in target.activeStatModifiers
                    .Where(m => m.source is SkillData sd && sd.skillID == skill.skillID))
                    am.remainingTurns = dur;
                AddMessage($"{target.playerName} Accuracy further reduced! Stack {currentStacks + 1}");
            }
            else
            {
                // 최대 스택: 타이머만 갱신
                foreach (var am in target.activeStatModifiers
                    .Where(m => m.source is SkillData sd && sd.skillID == skill.skillID))
                    am.remainingTurns = dur;
                AddMessage($"{target.playerName} Accuracy debuff timer refreshed!");
            }
            return;
        }

        // [기존 경로] statModifiers 없으면 activeStatusEffects 경로 유지
        int duration = skill.damageType == DamageType.Physical
            ? skill.curseEffect.physicalDuration
            : skill.curseEffect.magicalDuration;
        if (duration > 0)
        {
            target.ApplyStatusEffect(skill.curseEffect, duration);
            AddMessage($"{target.playerName} is afflicted with {skill.curseEffect.effectType}!");
        }
    }

    /// <summary>적 스킬용 스탯 스케일 값 — EnemyStats에 GetScaleValue가 없어 직접 분기.</summary>
    private int GetEnemyScaleValue(EnemyStats enemy, ScaleStat stat)
    {
        return stat switch
        {
            ScaleStat.Attack  => enemy.attack,
            ScaleStat.Defense => enemy.defense,
            ScaleStat.Magic   => enemy.magic,
            ScaleStat.Agility => enemy.Agility,
            ScaleStat.Luck    => enemy.luck,
            _                 => enemy.attack   // None 또는 미정의 → attack
        };
    }

    /// <summary>적이 SkillData 기반 스킬을 플레이어에게 시전. (평타와 별개 경로)</summary>
    private IEnumerator ExecuteEnemySkill(EnemyStats enemy, PlayerStats target, SkillData skill)
    {
        // 1. MP 차감
        enemy.currentMP = Mathf.Max(0, enemy.currentMP - skill.mpCost);

        // 2. HP 코스트
        if (skill.hpCostPercent > 0f)
        {
            int hpCost = Mathf.RoundToInt(enemy.maxHP * skill.hpCostPercent / 100f);
            enemy.currentHP = Mathf.Max(1, enemy.currentHP - hpCost);
        }

        // [Heal 분기] 회복 스킬이면 데미지 루프 진입하지 않고 즉시 처리 후 종료
        if (skill is MonsterSkillData healSkill && healSkill.Category == MonsterSkillCategory.Heal)
        {
            // 회복량 = effects의 Recovery 항목 effectAmount(% of maxHP). 없으면 5f 폴백.
            float healPercent = 5f;
            var recEffect = healSkill.Effects?.FirstOrDefault(e => e != null && e.effectType == EffectType.Recovery);
            if (recEffect != null) healPercent = recEffect.effectAmount;

            int healAmount = Mathf.RoundToInt(enemy.maxHP * healPercent / 100f);
            enemy.Heal(healAmount);
            AddMessage($"{enemy.enemyName} uses {skill.skillName} and recovers {healAmount} HP!");
            yield return new WaitForSeconds(actionDelay);
            UpdateStatusUI();
            yield break;
        }

        // [Buff 분기]
        if (skill is MonsterSkillData buffSkill && buffSkill.Category == MonsterSkillCategory.Buff)
        {
            if (skill.curseEffect != null && skill.curseApplyChance > 0f &&
                UnityEngine.Random.value <= skill.curseApplyChance)
            {
                // 같은 source 스킬이 이미 있으면 제거 후 재추가 (타이머 갱신)
                enemy.RemoveStatModifiersFromSource(skill);
                foreach (var mod in skill.curseEffect.statModifiers)
                {
                    enemy.AddStatModifier(mod, skill, skill.curseEffect.physicalDuration);
                }
                AddMessage($"{enemy.enemyName} uses {skill.skillName}!");
            }
            yield return new WaitForSeconds(actionDelay);
            UpdateStatusUI();
            yield break;
        }

        // [Debuff 분기 — 순수 디버프 스킬만 (Physical+Debuff는 데미지 루프로)]
        if (skill is MonsterSkillData debuffSkill && debuffSkill.Category == MonsterSkillCategory.Debuff)
        {
            if (target != null && skill.curseEffect != null)
            {
                float effectiveChance = skill.curseApplyChance *
                    target.ApplyStatModifiers(AbyssdawnBattle.ModStatType.StatusResist, 1f);
                if (UnityEngine.Random.value <= effectiveChance)
                {
                int currentStacks = target.activeStatModifiers
                    .Count(m => m.source is SkillData sd && sd.skillID == skill.skillID);

                if (currentStacks == 0)
                {
                    // 첫 적용: 2스택 (-0.10)
                    for (int s = 0; s < 2; s++)
                        foreach (var mod in skill.curseEffect.statModifiers)
                            target.AddStatModifier(mod, skill, skill.curseEffect.physicalDuration);
                    AddMessage($"{enemy.enemyName} uses {skill.skillName}! " +
                              $"{target.playerName}'s accuracy is reduced!");
                }
                else if (currentStacks < 4)
                {
                    // 추가 중첩: 1스택 + 전체 타이머 갱신
                    foreach (var mod in skill.curseEffect.statModifiers)
                        target.AddStatModifier(mod, skill, skill.curseEffect.physicalDuration);
                    foreach (var am in target.activeStatModifiers
                        .Where(m => m.source is SkillData sd && sd.skillID == skill.skillID))
                        am.remainingTurns = skill.curseEffect.physicalDuration;
                    AddMessage($"{enemy.enemyName} uses {skill.skillName}! " +
                              $"Effect refreshed and stacked!");
                }
                else
                {
                    // 최대 스택 도달: 타이머만 갱신
                    foreach (var am in target.activeStatModifiers
                        .Where(m => m.source is SkillData sd && sd.skillID == skill.skillID))
                        am.remainingTurns = skill.curseEffect.physicalDuration;
                    AddMessage($"{enemy.enemyName} uses {skill.skillName}! " +
                              $"Effect timer refreshed!");
                }
                }
            }
            yield return new WaitForSeconds(actionDelay);
            UpdateStatusUI();
            yield break;
        }

        int hits = Mathf.Max(1, skill.hitCount);
        int totalDamage = 0;

        for (int i = 0; i < hits; i++)
        {
            // 3. 타격마다 독립 명중 판정
            if (!RollPhysicalHit_EnemyVsPlayer(enemy, target, skill))
            {
                AddMessage($"{enemy.enemyName}'s {skill.skillName} missed!");
                BattleFx.AllyMiss(target);
                continue;
            }

            // 4. 크리티컬
            // [StatMod 5단계] CritChance는 Flat(퍼센트포인트 가산) 전용 설계.
            bool critical = CheckCritical(enemy.luck, skill.critBonusPercent
                + enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance, 0f));

            // 5. 데미지 계산
            int baseStat = GetEnemyScaleValue(enemy, skill.scalingStat);
            // Attack/Magic 배율 적용
            if (skill.scalingStat == ScaleStat.Magic)
                baseStat = Mathf.FloorToInt(
                    enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Magic, baseStat));
            else
                baseStat = Mathf.FloorToInt(
                    enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack, baseStat));
            float mult = UnityEngine.Random.Range(skill.minMult, skill.maxMult);
            float defValue;
            if (skill is MonsterSkillData msdDef && msdDef.Category == MonsterSkillCategory.MagicAttack)
            {
                // 마법 스킬: MagicDefense DQ 공식
                float mdef = target.ApplyStatModifiers(
                    AbyssdawnBattle.ModStatType.Defense, target.MagicDefense);
                defValue = mdef;
            }
            else
            {
                // 물리 스킬: 기존 물리 Defense
                defValue = target.ApplyStatModifiers(
                    AbyssdawnBattle.ModStatType.Defense, target.Defense);
            }
            float baseValue = (baseStat * 2f - defValue) / 2f;
            if (baseValue < 1f) baseValue = 1f;
            int damage = Mathf.FloorToInt(baseValue * mult);
            if (critical) damage = Mathf.FloorToInt(damage * enemy.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage, 1.5f));
            damage = Mathf.Max(1, damage);

            // 6. 슬롯 보정
            damage = ApplySlotDamageToTarget(damage, target.currentSlot, target.playerName);

            // 7. 적 방어 경감 (isDefending)
            if (target.isDefending)
            {
                damage = Mathf.FloorToInt(damage * (1f - target.defenceReduction));
                target.isDefending = false;
                AddMessage($"{target.playerName} defended and reduced the damage!");
            }

            // 8. 방패 블록 — TryBlock(PlayerStats, out float) 시그니처에 맞춤
            if (TryBlock(target, out float blockReduction))
            {
                damage = Mathf.Max(0, Mathf.FloorToInt(damage - blockReduction));
                if (damage <= 0)
                {
                    AddMessage($"{target.playerName} blocked {skill.skillName} completely!");
                    continue;
                }
                AddMessage($"{target.playerName} blocked part of {skill.skillName}! (DR {blockReduction:F0})");
            }

            // 9. 데미지 적용
            if (critical) BattleFx.MarkAllyCritical();
            target.TakeDamage(damage);
            totalDamage += damage;
            AddMessage($"{enemy.enemyName} uses {skill.skillName}! {target.playerName} takes {damage} damage{(critical ? " (Critical!)" : "")}.");

            // 10. 상태이상 (타격마다)
            ApplyCurseEffectsToPlayer(target, skill);

            // 11. 밀기/당기기
            if (skill is MonsterSkillData pushSkill && pushSkill.slotShiftAmount != 0
                && UnityEngine.Random.value <= pushSkill.slotShiftChance)
            {
                int direction = pushSkill.slotShiftAmount > 0 ? 1 : -1;
                int steps = Mathf.Abs(pushSkill.slotShiftAmount);
                BattleSlot currentSlot = target.currentSlot;
                for (int step = 0; step < steps; step++)
                {
                    BattleSlot nextSlot = GetHorizontalNeighbor4(currentSlot, direction);
                    if (nextSlot == BattleSlot.None) break;
                    currentSlot = nextSlot;
                }
                if (currentSlot != target.currentSlot)
                {
                    bool pushed = MovePlayerToSlot(target, currentSlot);
                    if (pushed)
                        AddMessage($"{target.playerName} is knocked back!");
                }
            }

            if (target.currentHP <= 0) break;

            if (hits > 1)
                yield return new WaitForSeconds(0.3f);
        }

        // 11. UI 갱신
        ShakePlayerStatusUI(target);
        UpdateStatusUI();
        CheckBattleEnd();
    }

    private IEnumerator ExecuteSkill(PlayerStats attacker, SkillData skill, EnemyStats target)
    {
        if (attacker == null || skill == null) yield break;
        // 타겟이 필요한 스킬인데 타겟이 없으면 리턴 (회복/방어 스킬 제외)
        if (target == null && !IsSelfTargetSkill(skill)) yield break;

        // MP 소모
        if (skill.mpCost > 0)
        {
            attacker.currentMP -= (int)skill.mpCost;
            attacker.currentMP = Mathf.Max(0, attacker.currentMP);
            // MP 변경 시 GameManager에 저장
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SaveFromPlayer(attacker);
                Debug.Log($"[BattleManager] {attacker.playerName} used {skill.mpCost} MP. Current MP: {attacker.currentMP}/{attacker.maxMP}. Saved to GameManager.");
            }
        }

        // HP 소모
        if (skill.hpCostPercent > 0)
        {
            int hpCost = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHP * (skill.hpCostPercent / 100f)));
            attacker.currentHP -= hpCost;
            attacker.currentHP = Mathf.Max(0, attacker.currentHP);
            Debug.Log($"[BattleManager] {attacker.playerName} used {skill.skillName} and lost {hpCost} HP. Current HP: {attacker.currentHP}/{attacker.maxHP}");
            ShakePlayerStatusUI(attacker);
            
            // HP 변경 시 GameManager에 저장
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SaveFromPlayer(attacker);
            }
            
            if (attacker.currentHP <= 0)
            {
                attacker.currentHP = 0;
                Debug.Log($"[BattleManager] {attacker.playerName} defeated by skill cost!");
            }
        }

        bool isSelfSkill = IsSelfTargetSkill(skill);

        // 1. 회복/버프 스킬
        if (isSelfSkill)
        {
            ApplySelfEffects(attacker, skill);
        }
        // 2. 공격 스킬
        else if (target != null)
        {
            // Mandritto: 전열 2명 동시 공격 (멀티 타겟)
            if (skill.skillName == "Mandritto")
            {
                yield return StartCoroutine(ExecuteMandritto(attacker, skill));
            }
            else
            {
                yield return StartCoroutine(ExecuteSingleTargetSkill(attacker, skill, target));
            }
        }

        // 역류(Backflow) 처리
        TryApplyBackflow(attacker, skill);

        // 스킬 사용 시 발동하는 패시브 처리 (Combat Breathing 등)
        ApplyOnSkillUsePassives(attacker, skill);

        Debug.Log("[BattleManager] ExecuteSkill finished. Updating status UI.");
        UpdateStatusUI();
        CheckBattleEnd();
    }

    /// <summary>
    /// 스킬 사용 후 역류(Backflow) 발동을 시도합니다.
    /// backflowChance > 0인 스킬만 대상. 억제율은 장비+패시브 합산.
    /// </summary>
    private void TryApplyBackflow(PlayerStats attacker, SkillData skill)
    {
        if (skill == null || skill.backflowChance <= 0f) return;
        if (attacker == null) return;

        float suppression = attacker.TotalBackflowSuppression;
        float finalChance = skill.backflowChance * (1f - suppression);
        finalChance = Mathf.Max(0f, finalChance);

        if (Random.value > finalChance) return;

        // 역류 발동
        Debug.Log($"[Backflow] {attacker.playerName} — Backflow triggered! Chance={finalChance:P0} (raw={skill.backflowChance:P0}, suppression={suppression:P0}) Type={skill.backflowType}");

        switch (skill.backflowType)
        {
            case BackflowType.HPLoss:
            {
                int loss = Mathf.Max(1, Mathf.FloorToInt(attacker.maxHP * 0.1f));
                attacker.currentHP = Mathf.Max(1, attacker.currentHP - loss);
                AddMessage($"<color=red>[Backflow] {attacker.playerName} loses {loss} HP from magical recoil!</color>");
                ShakePlayerStatusUI(attacker);
                break;
            }
            case BackflowType.MPLoss:
            {
                int loss = Mathf.Max(1, Mathf.FloorToInt(attacker.maxMP * 0.1f));
                attacker.currentMP = Mathf.Max(0, attacker.currentMP - loss);
                AddMessage($"<color=blue>[Backflow] {attacker.playerName} loses {loss} MP from magical recoil!</color>");
                break;
            }
            case BackflowType.StatusEffect:
            {
                if (skill.backflowStatusEffect != null)
                {
                    attacker.ApplyStatusEffect(skill.backflowStatusEffect);
                    AddMessage($"<color=orange>[Backflow] {attacker.playerName} is afflicted with {skill.backflowStatusEffect.variantId}!</color>");
                }
                break;
            }
            case BackflowType.Stun:
            {
                // 스턴: 간이 구현 — 다음 행동 취소 (statusEffect 없이 플래그로)
                AddMessage($"<color=yellow>[Backflow] {attacker.playerName} is stunned by magical recoil!</color>");
                // StatusEffect 방식으로 적용: Curse_Stun 사용 (있을 경우)
                var stunEffect = skill.backflowStatusEffect;
                if (stunEffect != null)
                    attacker.ApplyStatusEffect(stunEffect);
                break;
            }
            case BackflowType.HPAndMP:
            {
                int hpLoss = Mathf.Max(1, Mathf.FloorToInt(attacker.maxHP * 0.1f));
                int mpLoss = Mathf.Max(1, Mathf.FloorToInt(attacker.maxMP * 0.1f));
                attacker.currentHP = Mathf.Max(1, attacker.currentHP - hpLoss);
                attacker.currentMP = Mathf.Max(0, attacker.currentMP - mpLoss);
                AddMessage($"<color=red>[Backflow] {attacker.playerName} loses {hpLoss} HP and {mpLoss} MP from magical recoil!</color>");
                ShakePlayerStatusUI(attacker);
                break;
            }
        }

        var gmSave = GameManager.Instance;
        if (gmSave != null) gmSave.SaveFromPlayer(attacker);
    }

    /// <summary>
    /// 일반 단일 타겟 공격 스킬 처리
    /// </summary>
    private IEnumerator ExecuteSingleTargetSkill(PlayerStats attacker, SkillData skill, EnemyStats target)
    {
        if (attacker == null || skill == null || target == null) yield break;

        int hits = skill.hitCount > 0 ? skill.hitCount : 1;
        bool isDual = IsDualWielding(attacker);
        if (isDual)
        {
            // 쌍수: 스킬 타수 ×2
            hits *= 2;
        }

        // 방어구 파괴 누적 계산용 남은 방어력 (스킬 전체 동안만 사용하는 가상 값)
        float remainingDefense = target.defense;
        float armorCoeff = GetArmorBreakCoefficient(attacker);
        int totalDamage = 0;
        int successfulHits = 0;
        int evadedHits = 0;

        // 각 타격마다 독립적으로 명중률/크리티컬 계산 + 딜레이
        for (int i = 0; i < hits; i++)
        {
            // 타격마다 명중률 체크 (스킬 accuracy × 슬롯 보정 × AGI 등)
            var (hitChance, logSkillAcc, logSlotAcc) = EvaluateHitChance(skill, attacker, target);
            float roll = UnityEngine.Random.Range(0f, 1f);
            bool missed = roll >= hitChance;
            Debug.Log($"[HIT] {attacker.playerName} → {target.enemyName}(슬롯{(int)target.currentSlot}): 스킬accuracy={logSkillAcc}, 슬롯보정={logSlotAcc}, 최종={hitChance}, 결과={(missed ? "miss" : "hit")}");
            if (missed)
            {
                // 회피됨
                evadedHits++;
                BattleFx.EnemyMiss(target);
                if (hits > 1)
                {
                    AddMessage($"Hit {i + 1}: {target.enemyName} evaded!");
                }
                else
                {
                    AddMessage($"{target.enemyName} evaded {attacker.playerName}'s {skill.skillName}!");
                }
                
                // 회피해도 약간의 딜레이
                if (i < hits - 1) yield return new WaitForSeconds(0.3f);
                continue; // 이 타격은 회피됨, 다음 타격으로
            }

            // 회피하지 않았으면 데미지 계산
            // [StatMod 5단계] CritChance는 Flat(퍼센트포인트 가산) 전용 설계.
            bool critical = CheckCritical(attacker.luck, attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance, 0f));
            float multiplier = UnityEngine.Random.Range(skill.minMultiplier, skill.maxMultiplier);

            // 스탯 스케일링
            float baseStat = attacker.GetScaleValue(skill.scalingStat);
            float scaleMultiplier = 1f;
            if (skill.scalingStat == ScaleStat.CurrentHPPercent ||
                skill.scalingStat == ScaleStat.CurrentMPPercent)
            {
                scaleMultiplier = baseStat;
                baseStat = attacker.Attack;
            }
            else if (skill.scalingStat == ScaleStat.None)
            {
                baseStat = attacker.Attack;
            }

            // [StatMod 4단계] 실제 사용된 스탯 종류에 따라 배율 적용.
            if (skill.scalingStat == ScaleStat.Magic)
            {
                baseStat = Mathf.FloorToInt(attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Magic, baseStat));
            }
            else if (skill.scalingStat == ScaleStat.Attack || skill.scalingStat == ScaleStat.None ||
                     skill.scalingStat == ScaleStat.CurrentHPPercent || skill.scalingStat == ScaleStat.CurrentMPPercent)
            {
                baseStat = Mathf.FloorToInt(attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack, baseStat));
            }

            // [DEBUG LOG] 스킬 데미지 계산 정보
            Debug.Log($"[BattleLog] Skill {skill.skillName} - Base Stat: {baseStat}, Multiplier: {multiplier:F2}, Critical: {critical}, Scale: {scaleMultiplier:F2}");

            int baseDamage = Mathf.FloorToInt(baseStat * multiplier * scaleMultiplier);
            baseDamage = Mathf.Max(1, baseDamage);

            // Sharp Edge 등 공격 패시브로 인한 추가 보정 (깡뎀에만 적용)
            baseDamage = ApplyOffensivePassiveBonuses(attacker, skill, target, baseDamage);

            // --- 방어구 파괴 계산 (타수 일반화) ---
            int armorBreakDamage = 0;
            if (armorCoeff > 0f && remainingDefense > 0f && skill.damageType == DamageType.Physical)
            {
                float fn = isDual ? UnityEngine.Random.Range(0.35f, 0.45f) : 1f;
                float rawArmor = remainingDefense * armorCoeff * fn;
                remainingDefense = Mathf.Max(0f, remainingDefense - rawArmor);
                armorBreakDamage = Mathf.RoundToInt(rawArmor);
            }

            int damage = baseDamage + armorBreakDamage;

            // 방어력/마방 적용 (DQ 표준)
            if (skill.damageType == DamageType.Physical)
            {
                // 물리: DQ 공식 — (damage×2 - defense) / 2
                float defValue = target.ApplyStatModifiers(
                    AbyssdawnBattle.ModStatType.Defense, target.defense);
                float reduced = (damage * 2f - defValue) / 2f;
                damage = Mathf.Max(1, Mathf.FloorToInt(reduced));
                damage = ApplyPhysResist(damage, target);
            }
            else if (skill.damageType == DamageType.Magic)
            {
                // 마법: magResist 배율 곱셈
                damage = Mathf.Max(1, Mathf.FloorToInt(damage * target.magResist));
            }

            // 크리티컬은 깡뎀 + 방어 파괴 합산에 배율 적용
            if (critical)
            {
                damage = Mathf.FloorToInt(damage * attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage, 1.5f));
            }

            damage = Mathf.Max(1, damage);

            Debug.Log($"[BattleLog] Skill Final Damage (with armor break): base={baseDamage}, armorBreak={armorBreakDamage}, total={damage}");

            damage = ApplySlotDamageToTarget(damage, target.currentSlot, target.enemyName);
            if (target.isDefending)
            {
                damage = Mathf.FloorToInt(damage * (1f - target.defenceReduction));
                target.isDefending = false;
                AddMessage($"{target.enemyName} defended and reduced the damage!");
            }
            // 데미지 적용 (적이 흔들림) - 크리티컬 여부 전달
            target.TakeDamage(damage, critical);
            totalDamage += damage;
            successfulHits++;
            
            // 각 타격마다 개별 데미지 메시지 표시
            if (hits > 1)
            {
                string critMsg = critical ? " Critical!" : "";
                AddMessage($"Hit {i + 1}: {damage} damage{critMsg}");
            }
            else
            {
                string critMsg = critical ? " Critical hit!" : "";
                AddMessage($"{attacker.playerName} used {skill.skillName}! {damage} damage{critMsg}");
            }
            
            // 저주 적용 (각 타격마다 curseChance 확률로 적용)
            ApplyCurseEffects(target, skill);
            
            // 다음 타격 전 딜레이 (마지막 타격 후에는 딜레이 없음)
            if (i < hits - 1)
            {
                yield return new WaitForSeconds(0.4f); // 타격 간 딜레이
            }
        }

        // 다중 타격일 경우 총합 메시지 추가
        if (hits > 1 && successfulHits > 0)
        {
            AddMessage($"Total: {totalDamage} damage ({successfulHits}/{hits} hits)");
        }

        // 본인 피해 (Magic Bolt, Fireball) - 스킬 사용 자체에 대한 확률이므로 타격 성공 여부와 무관
        if (skill.selfDmgChance > 0 && UnityEngine.Random.Range(0f, 100f) < skill.selfDmgChance)
        {
            int selfDmg = 0;
            if (skill.selfDmgPercent > 0)
            {
                selfDmg = Mathf.FloorToInt(attacker.maxHP * (skill.selfDmgPercent / 100f));
            }

            if (selfDmg > 0)
            {
                attacker.currentHP -= selfDmg;
                attacker.currentHP = Mathf.Max(0, attacker.currentHP);
                AddMessage($"{attacker.playerName} took {selfDmg} recoil damage!");
                ShakePlayerStatusUI(attacker);
            }
        }

        // 본인 상태이상 적용 (selfStatusEffect)
        if (skill.selfStatusEffect != null && skill.selfStatusEffectChance > 0)
        {
            if (UnityEngine.Random.Range(0f, 100f) < skill.selfStatusEffectChance)
            {
                // 마법 스킬이면 magicalDuration 사용
                bool isMagic = skill.damageType == AbyssdawnBattle.DamageType.Magic;
                int dur = isMagic ? skill.selfStatusEffect.magicalDuration : skill.selfStatusEffect.physicalDuration;
                attacker.ApplyStatusEffect(skill.selfStatusEffect, dur);
                string sCol = GetStatusColor(skill.selfStatusEffect.effectType);
                AddMessage($"{attacker.playerName} is <color={sCol}>self-afflicted</color> with <color={sCol}>{skill.selfStatusEffect.variantId}</color>!");
            }
        }
    }

    /// <summary>
    /// Mandritto: 전열(앞열) 적 2명 동시 타격
    /// 현재는 activeEnemies 리스트의 앞에서부터 살아있는 최대 2명을 전열로 간주
    /// </summary>
    private IEnumerator ExecuteMandritto(PlayerStats attacker, SkillData skill)
    {
        if (attacker == null || skill == null) yield break;

        // SlotMask 기반으로 타겟 슬롯 결정 (스킬의 allowedTargetSlots 활용)
        SlotMask targetMask = (skill.targeting != null) ? skill.targeting.allowedTargetSlots : SlotMask.Front;
        List<EnemyStats> frontRowEnemies = enemyLine.GetCharactersInMask(targetMask)
            .Where(e => e != null && e.currentHP > 0)
            .ToList();

        if (frontRowEnemies.Count == 0) yield break;

        int totalDamage = 0;

        foreach (var target in frontRowEnemies)
        {
            if (target == null || target.currentHP <= 0) continue;

            var (hitChance, logSkillAcc2, logSlotAcc2) = EvaluateHitChance(skill, attacker, target);
            float roll = UnityEngine.Random.Range(0f, 1f);
            bool missed2 = roll >= hitChance;
            Debug.Log($"[HIT] {attacker.playerName} → {target.enemyName}(슬롯{(int)target.currentSlot}): 스킬accuracy={logSkillAcc2}, 슬롯보정={logSlotAcc2}, 최종={hitChance}, 결과={(missed2 ? "miss" : "hit")}");
            if (missed2)
            {
                AddMessage($"{target.enemyName} evaded {attacker.playerName}'s {skill.skillName}!");
                BattleFx.EnemyMiss(target);
                continue;
            }

            // [StatMod 5단계] CritChance는 Flat(퍼센트포인트 가산) 전용 설계.
            bool critical = CheckCritical(attacker.luck, attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritChance, 0f));
            float multiplier = UnityEngine.Random.Range(skill.minMultiplier, skill.maxMultiplier);

            float baseStat = attacker.GetScaleValue(skill.scalingStat);
            float scaleMultiplier = 1f;
            if (skill.scalingStat == ScaleStat.CurrentHPPercent ||
                skill.scalingStat == ScaleStat.CurrentMPPercent)
            {
                scaleMultiplier = baseStat;
                baseStat = attacker.Attack;
            }
            else if (skill.scalingStat == ScaleStat.None)
            {
                baseStat = attacker.Attack;
            }

            // [StatMod 4단계] 실제 사용된 스탯 종류에 따라 배율 적용.
            if (skill.scalingStat == ScaleStat.Magic)
            {
                baseStat = Mathf.FloorToInt(attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Magic, baseStat));
            }
            else if (skill.scalingStat == ScaleStat.Attack || skill.scalingStat == ScaleStat.None ||
                     skill.scalingStat == ScaleStat.CurrentHPPercent || skill.scalingStat == ScaleStat.CurrentMPPercent)
            {
                baseStat = Mathf.FloorToInt(attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Attack, baseStat));
            }

            int damage = Mathf.FloorToInt(baseStat * multiplier * scaleMultiplier);
            if (critical)
            {
                damage = Mathf.FloorToInt(damage * attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.CritDamage, 1.5f));
            }
            damage = Mathf.Max(1, damage);

            // Sharp Edge 등 공격 패시브 보정
            damage = ApplyOffensivePassiveBonuses(attacker, skill, target, damage);

            damage = ApplySlotDamageToTarget(damage, target.currentSlot, target.enemyName);
            if (target.isDefending)
            {
                damage = Mathf.FloorToInt(damage * (1f - target.defenceReduction));
                target.isDefending = false;
                AddMessage($"{target.enemyName} defended and reduced the damage!");
            }

            // 방어력/마방 적용 (DQ 표준)
            if (skill.damageType == DamageType.Physical)
            {
                // 물리: DQ 공식 — (damage×2 - defense) / 2
                float defValue = target.ApplyStatModifiers(
                    AbyssdawnBattle.ModStatType.Defense, target.defense);
                float reduced = (damage * 2f - defValue) / 2f;
                damage = Mathf.Max(1, Mathf.FloorToInt(reduced));
                damage = ApplyPhysResist(damage, target);
            }
            else if (skill.damageType == DamageType.Magic)
            {
                // 마법: magResist 배율 곱셈
                damage = Mathf.Max(1, Mathf.FloorToInt(damage * target.magResist));
            }

            target.TakeDamage(damage, critical);
            totalDamage += damage;

            string critMsg = critical ? " Critical hit!" : "";
            AddMessage($"{attacker.playerName} used {skill.skillName} on {target.enemyName}! {damage} damage{critMsg}");

            ApplyCurseEffects(target, skill);

            // 각 타겟 사이에 약간의 딜레이
            yield return new WaitForSeconds(0.3f);
        }

        if (totalDamage > 0)
        {
            AddMessage($"Mandritto total damage: {totalDamage}");
        }
    }

    /// <summary>
    /// 적의 물리 저항 적용 (MonsterSO.physResist: 1 = 정상 피해, 0.8 = 20% 덜 받음, 0 = 면역). 최소 1.
    /// [2026-09-30] 이전에는 physResist 를 읽기만 하고 피해 계산에 쓰지 않았다 (마법의 magResist 만 적용).
    /// </summary>
    private static int ApplyPhysResist(int damage, EnemyStats target)
    {
        if (target == null || damage <= 0) return damage;
        float r = Mathf.Clamp01(target.physResist);
        return Mathf.Max(1, Mathf.FloorToInt(damage * r));
    }

    // -------------------- Damage Calculation --------------------
    private int CalculateDQDamage(int atk, int def, bool isCritical, float randomMax = 1.15f, float critDamageMultiplier = 1.5f)
    {
        float baseValue = (atk * 2f - def) / 2f;
        if (baseValue < 1f) baseValue = 1f;
        int damage = Mathf.FloorToInt(baseValue * UnityEngine.Random.Range(0.85f, randomMax));
        if (isCritical) damage = Mathf.FloorToInt(damage * critDamageMultiplier);
        return Mathf.Max(damage, 1);
    }

    /// <summary>
    /// 공격 패시브(Sharp Edge 등)에 의한 추가 데미지 보정
    /// </summary>
    private int ApplyOffensivePassiveBonuses(PlayerStats attacker, SkillData usedSkill, EnemyStats target, int baseDamage)
    {
        if (attacker == null || attacker.statData == null || attacker.statData.equippedPassives == null)
            return baseDamage;

        int result = baseDamage;

        foreach (var passive in attacker.statData.equippedPassives)
        {
            if (passive == null || !passive.IsPassive) continue;

            // Sharp Edge: 방어 관통 효과를 "추가 피해 %"로 단순 모델링
            if (passive.skillName == "Sharp Edge")
            {
                float penPercent = 0.08f; // 기본 8%

                // 무기 공격력 보정치 × 0.1% 만큼 추가 관통 → 총 공격력 보정치를 사용
                int equipAtk = attacker.GetEquipmentAttackBonus();
                penPercent += equipAtk * 0.001f; // 예: +10 공격 → +1% 추가

                // 앞열 적 대상이면 추가 +5%
                if (IsFrontRowEnemy(target))
                {
                    penPercent += 0.05f;
                }

                result = Mathf.FloorToInt(result * (1f + penPercent));
            }
        }

        return Mathf.Max(result, 1);
    }

    /// <summary>
    /// 적이 전열(슬롯 1,2)에 있는지 확인합니다. currentSlot 기반으로 판단합니다.
    /// </summary>
    private bool IsFrontRowEnemy(EnemyStats enemy)
    {
        if (enemy == null) return false;
        return enemy.IsFrontRow;
    }

    // -------------------- Critical / Evasion --------------------
    private bool CheckCritical(int luck, float bonusPercent = 0f)
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < criticalChance + luck + bonusPercent;
    }

    /// <summary>
    /// 스킬 사용 시 발동하는 패시브 처리 (Combat Breathing 등)
    /// </summary>
    private void ApplyOnSkillUsePassives(PlayerStats attacker, SkillData usedSkill)
    {
        if (attacker == null || attacker.statData == null || attacker.statData.equippedPassives == null) return;

        foreach (var passive in attacker.statData.equippedPassives)
        {
            if (passive == null || !passive.IsPassive) continue;

            // Combat Breathing: 스킬 사용 시 HP 5% 회복, 후열이면 +2% 추가
            if (passive.skillName == "Combat Breathing")
            {
                float healPercent = 5f;

                // [슬롯 표준화] 아군 후열 판정(SlotHelper 모델 B). 슬롯 3,4에서 후열 보너스(+2%) 발동.
                if (attacker.IsBackRow)
                {
                    healPercent += 2f;
                }

                int healAmount = Mathf.FloorToInt(attacker.maxHP * (healPercent / 100f));
                if (healAmount > 0)
                {
                    attacker.Heal(healAmount);
                    AddMessage($"{attacker.playerName}'s Combat Breathing restores {healAmount} HP!");
                }
            }
        }
    }

    // -------------------- Slot damage / Hit chance (슬롯 보정) --------------------
    /// <summary>
    /// 대상 슬롯에 따른 피해량 배율을 적용합니다. (최소 1)
    /// </summary>
    private int ApplySlotDamageToTarget(int baseDamage, BattleSlot slot, string targetDisplayName)
    {
        if (baseDamage <= 0)
            return 0;
        float mult = SlotBalanceTable.GetDamageMultiplier(slot);
        int final = Mathf.Max(1, Mathf.FloorToInt(baseDamage * mult));
        Debug.Log($"[DMG] {targetDisplayName}(슬롯{(int)slot}): 기본={baseDamage}, 배율={mult}, 최종={final}");
        return final;
    }

    /// <summary>
    /// 최종 명중률 = (스킬 accuracy × 슬롯 명중 보정) × AGI 보정 + Luck/패시브/장비 보정, 이후 클램프.
    /// </summary>
    private static (float finalChance, float skillAcc, float slotAcc) ComputeHitChanceCore(
        SkillData usedSkill,
        float attackerAgility,
        float defenderAgility,
        float attackerLuck,
        float passiveAccuracyBonus,
        float itemAccuracyBonus,
        BattleSlot defenderSlot,
        float accuracyModMultiplier = 1f,
        float evasionModMultiplier = 0f)
    {
        float skillAcc = usedSkill != null ? usedSkill.accuracy : 1f;
        // [StatMod 4단계] 공격자 상태이상/버프의 명중 배율 적용.
        skillAcc *= accuracyModMultiplier;
        float slotAcc = SlotBalanceTable.GetHitChanceMultiplier(defenderSlot);
        float combinedAcc = skillAcc * slotAcc;

        float agiModifier = BattleSimCombatMath.ComputeAgilityHitModifier(attackerAgility, defenderAgility);
        // [StatMod 6단계] 방어자 회피율 적용 — 0=회피 없음, 최대 0.8(80% 명중 감소)로 클램프.
        evasionModMultiplier = Mathf.Clamp(evasionModMultiplier, 0f, 0.8f);
        float finalHitChance = combinedAcc * agiModifier * (1f - evasionModMultiplier)
            + (attackerLuck * 0.002f) + passiveAccuracyBonus + itemAccuracyBonus;
        float clamped = Mathf.Clamp(finalHitChance, 0.2f, 0.98f);
        return (clamped, skillAcc, slotAcc);
    }

    private (float finalChance, float skillAcc, float slotAcc) EvaluateHitChance(SkillData usedSkill, PlayerStats attacker, EnemyStats defender)
    {
        if (attacker == null || defender == null)
            return (0.2f, 1f, SlotBalanceTable.GetHitChanceMultiplier(BattleSlot.Slot1));
        float evasionMod = Mathf.Clamp(
            defender.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Evasion, 0f), 0f, 0.8f);
        return ComputeHitChanceCore(
            usedSkill,
            attacker.Agility,
            defender.Agility,
            attacker.Luck,
            attacker.GetPassiveAccuracyBonus(),
            attacker.GetEquipmentAccuracyBonus(),
            defender.currentSlot,
            attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Accuracy, 1f),
            evasionMod);
    }

    private (float finalChance, float skillAcc, float slotAcc) EvaluateHitChance(SkillData usedSkill, PlayerStats attacker, PlayerStats defender)
    {
        if (attacker == null || defender == null)
            return (0.2f, 1f, SlotBalanceTable.GetHitChanceMultiplier(BattleSlot.Slot1));
        return ComputeHitChanceCore(
            usedSkill,
            attacker.Agility,
            defender.Agility,
            attacker.Luck,
            attacker.GetPassiveAccuracyBonus(),
            attacker.GetEquipmentAccuracyBonus(),
            defender.currentSlot);
    }

    private (float finalChance, float skillAcc, float slotAcc) EvaluateHitChance(SkillData usedSkill, EnemyStats attacker, PlayerStats defender)
    {
        if (attacker == null || defender == null)
            return (0.2f, 1f, SlotBalanceTable.GetHitChanceMultiplier(BattleSlot.Slot1));
        float evasionMod = Mathf.Clamp(
            defender.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Evasion, 0f), 0f, 0.8f);
        return ComputeHitChanceCore(
            usedSkill,
            attacker.Agility,
            defender.Agility,
            attacker.luck,
            0f,
            0f,
            defender.currentSlot,
            attacker.ApplyStatModifiers(AbyssdawnBattle.ModStatType.Accuracy, 1f),
            evasionMod);
    }

    private bool RollPhysicalHit_PlayerVsEnemy(PlayerStats attacker, EnemyStats target, SkillData skillOrNull)
    {
        var (final, sa, sl) = EvaluateHitChance(skillOrNull, attacker, target);
        float roll = UnityEngine.Random.Range(0f, 1f);
        bool hit = roll < final;
        Debug.Log($"[HIT] {attacker.playerName} → {target.enemyName}(슬롯{(int)target.currentSlot}): 스킬accuracy={sa}, 슬롯보정={sl}, 최종={final}, 결과={(hit ? "hit" : "miss")}");
        return hit;
    }

    private bool RollPhysicalHit_EnemyVsPlayer(EnemyStats attacker, PlayerStats target, SkillData skillOrNull)
    {
        var (final, sa, sl) = EvaluateHitChance(skillOrNull, attacker, target);
        float roll = UnityEngine.Random.Range(0f, 1f);
        bool hit = roll < final;
        Debug.Log($"[HIT] {attacker.enemyName} → {target.playerName}(슬롯{(int)target.currentSlot}): 스킬accuracy={sa}, 슬롯보정={sl}, 최종={final}, 결과={(hit ? "hit" : "miss")}");
        return hit;
    }

    // -------------------- 영입 시스템 (1차) --------------------
    // 정책: 활성 동료 max maxActiveCompanions(=3), 대기열 max maxCompanionWaitlist(=3).
    // 후보: 마지막에 죽은 적 1마리. EnemyStats.HandleDeath → NotifyEnemyDied가 갱신.
    // 동료 레벨업 없음 (CompanionSO 고정 스탯). UI 없음 — 콘솔 로그만.
    // Warrior/Rogue/Wizard 하드코딩 동료 시스템과 별개 컬렉션.

    /// <summary>EnemyStats.HandleDeath가 호출. 마지막 사망 적 갱신.</summary>
    public void NotifyEnemyDied(EnemyStats enemy)
    {
        _lastDefeatedEnemy = enemy;
        Debug.Log($"[Recruit] 마지막 사망 적 갱신: '{(enemy != null ? enemy.enemyName : "NULL")}' (sourceMonster={(enemy != null && enemy.sourceMonster != null ? enemy.sourceMonster.MonsterName : "NULL")})");
    }

    // ──────────────────────────────────────────────────────────────────
    // [2026-05-24] 영입 다이얼로그 흐름 (ReturnToDungeonRoutine 내부에서 사용)
    // - TryInitiateRecruitDialog: 굴림 + 슬롯 체크. 가능하면 CompanionSO 반환.
    // - RecruitDialogRoutine: 적 sprite 켜기 → 메시지 → YES/NO → 분기 처리
    // - YES 선택 시 CompanionPartyPersistence.TryAddActive만 호출 (활성 파티 슬롯/_companionInstances는 건드리지 않음 — 사용자 명시)
    // - WaitForMessageSequence: PlayMessageSequence 동기 대기 헬퍼
    // - BuildRecruitYesNoPanel / MakeRecruitDialogButton: 런타임 UI 생성
    // ──────────────────────────────────────────────────────────────────

    /// <summary>같은 종 동료를 이미 데리고 있을 때 영입 확률 배율 (드퀘5식 2마리째 감소).</summary>
    public const float DuplicateCompanionChanceMultiplier = 0.25f;

    /// <summary>마지막 적의 영입 굴림 + 슬롯 여유 체크. 가능하면 recruitData를 반환.</summary>
    private bool TryInitiateRecruitDialog(out Abyssdawn.MonsterSO recruitData)
    {
        recruitData = null;
        if (_lastDefeatedEnemy == null)
        {
            Debug.Log("[Recruit] TryInitiateRecruitDialog: _lastDefeatedEnemy == NULL → 다이얼로그 스킵");
            return false;
        }
        var so = _lastDefeatedEnemy.sourceMonster;
        if (so == null)
        {
            Debug.LogWarning($"[Recruit] '{_lastDefeatedEnemy.enemyName}' sourceMonster == NULL → 다이얼로그 스킵");
            return false;
        }
        if (!so.CanBecomCompanion)
        {
            Debug.Log($"[Recruit] '{so.MonsterName}' 영입 불가 (CanBecomCompanion={so.CanBecomCompanion}, companionChance={so.CompanionChance:F2})");
            return false;
        }

        // [2026-09-30] 드퀘5식: 같은 종을 이미 데리고 있으면 2마리째부터 확률 1/4 (같은 종으로 파티가 채워지는 것 방지)
        int owned = CompanionPartyPersistence.CountOwned(so);
        float chance = so.CompanionChance * (owned > 0 ? DuplicateCompanionChanceMultiplier : 1f);

        float roll = Random.value;
        bool rollSuccess = roll <= chance;
        if (!rollSuccess)
        {
            Debug.Log($"[Recruit] ❌ 굴림 실패 — '{so.MonsterName}' (확률 {chance:P1}{(owned > 0 ? $", 같은 종 {owned}마리 보유로 1/4" : "")}, 굴림 {roll:F3})");
            return false;
        }

        // [2026-05-25 고정 3칸] ActiveRoster/WaitlistPaths.Count는 항상 3이므로 CountActive()/CountWait()(실제 수)로 판정.
        bool hasActiveSlot = CompanionPartyPersistence.CountActive() < CompanionPartyPersistence.MaxActive;
        bool hasWaitSlot = CompanionPartyPersistence.CountWait() < CompanionPartyPersistence.MaxWaitlist;
        if (!hasActiveSlot && !hasWaitSlot)
        {
            Debug.LogWarning($"[Recruit] ⚠ 굴림은 성공했지만 활성+대기 슬롯 모두 가득 — '{so.MonsterName}' 다이얼로그 스킵");
            return false;
        }

        Debug.Log($"[Recruit] ✅ 굴림 성공 + 슬롯 여유 — '{so.MonsterName}' 다이얼로그 진입 (확률 {so.CompanionChance:P0}, 굴림 {roll:F3}, active {CompanionPartyPersistence.ActiveRoster.Count}/{CompanionPartyPersistence.MaxActive}, wait {CompanionPartyPersistence.WaitlistPaths.Count}/{CompanionPartyPersistence.MaxWaitlist})");
        recruitData = so;
        return true;
    }

    /// <summary>영입 다이얼로그 — 적 재활성화(페이드인), 메시지, YES/NO 버튼, 분기 처리.</summary>
    private IEnumerator RecruitDialogRoutine(Abyssdawn.MonsterSO data)
    {
        // 1) 마지막에 죽은 적의 sprite를 화면 중앙으로 옮기고 alpha 0 → 1 페이드인 (자연스러운 재등장)
        SpriteRenderer recruitSpriteRef = null;
        Color recruitSpriteOriginalColor = Color.white;
        if (_lastDefeatedEnemy != null)
        {
            recruitSpriteRef = _lastDefeatedEnemy.GetComponent<SpriteRenderer>();
            if (recruitSpriteRef != null)
            {
                // 화면 중앙으로 위치 이동 (메인 카메라 기준 월드 중앙).
                // z는 기존 sprite의 z 유지(카메라와의 거리), 카메라 정면 중앙에 배치.
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 spritePos = recruitSpriteRef.transform.position;
                    Vector3 centerWorld = cam.transform.position
                        + cam.transform.forward * Mathf.Abs(spritePos.z - cam.transform.position.z);
                    centerWorld.z = spritePos.z; // 2D 정렬 유지
                    recruitSpriteRef.transform.position = centerWorld;
                    Debug.Log($"[Recruit] 다이얼로그: '{_lastDefeatedEnemy.enemyName}' 화면 중앙으로 이동 {spritePos} → {centerWorld}");
                }

                recruitSpriteOriginalColor = recruitSpriteRef.color;
                recruitSpriteRef.color = new Color(recruitSpriteOriginalColor.r, recruitSpriteOriginalColor.g, recruitSpriteOriginalColor.b, 0f);
                recruitSpriteRef.enabled = true;
                Debug.Log($"[Recruit] 다이얼로그: '{_lastDefeatedEnemy.enemyName}' 스프라이트 재활성 (alpha=0에서 시작, 페이드인 {recruitSpriteFadeInDuration:F2}s)");

                if (recruitSpriteFadeInDuration > 0f)
                {
                    yield return StartCoroutine(FadeInSpriteRoutine(recruitSpriteRef, recruitSpriteOriginalColor, recruitSpriteFadeInDuration));
                }
                else
                {
                    recruitSpriteRef.color = recruitSpriteOriginalColor; // 즉시 표시
                }
            }
        }

        // 2) 영입 권유 문구 — CompanionRecruitPhrases에서 랜덤 선택 ({0}에 동료 이름 삽입).
        string invitePhrase = Abyssdawn.CompanionRecruitPhrases.GetRandomPhrase($"<color=#FFD700>{data.MonsterName}</color>");
        Debug.Log($"[Recruit] 랜덤 권유 문구: \"{invitePhrase}\"");
        yield return StartCoroutine(WaitForMessageSequence(new System.Collections.Generic.List<string>
        {
            invitePhrase
        }));

        // 3) YES/NO 패널 — 커스텀 우선, 없으면 런타임 생성
        GameObject panel;
        Button yesBtn;
        Button noBtn;
        bool isCustomPanel = customRecruitPanel != null && customRecruitYesButton != null && customRecruitNoButton != null;

        if (isCustomPanel)
        {
            panel = customRecruitPanel;
            yesBtn = customRecruitYesButton;
            noBtn = customRecruitNoButton;
            panel.SetActive(true);
            Debug.Log($"[Recruit] 커스텀 영입 패널 사용: '{panel.name}'");
        }
        else
        {
            panel = BuildRecruitYesNoPanel(out yesBtn, out noBtn);
            Debug.Log("[Recruit] 커스텀 패널 미연결 — 런타임 자동 생성된 회색 패널 사용");
        }

        // listener를 변수에 캡처해 우리 액션만 add/remove (Inspector에서 설정한 다른 onClick 보존)
        int? choice = null; // null=대기, 0=NO, 1=YES
        UnityEngine.Events.UnityAction yesAction = () => { if (choice == null) choice = 1; };
        UnityEngine.Events.UnityAction noAction  = () => { if (choice == null) choice = 0; };
        yesBtn.onClick.AddListener(yesAction);
        noBtn.onClick.AddListener(noAction);
        Debug.Log("[Recruit] YES/NO listener 등록, 사용자 입력 대기");

        // 4) 사용자 선택 대기
        while (choice == null) yield return null;

        // 우리 listener만 제거 (다른 listener 보존)
        yesBtn.onClick.RemoveListener(yesAction);
        noBtn.onClick.RemoveListener(noAction);

        if (isCustomPanel)
        {
            panel.SetActive(false);
        }
        else
        {
            Destroy(panel);
        }
        Debug.Log($"[Recruit] 사용자 선택: {(choice == 1 ? "YES" : "NO")}");

        // 5) 분기
        if (choice == 1)
        {
            // YES — 영속화 등록만. 활성 파티 슬롯/_companionInstances는 건드리지 않음 (사용자 명시)
            // 다음 씬에서 RestoreCompanionInstancesFromPersistence가 ActiveRoster 기반으로 동료 인스턴스화.
            bool added = CompanionPartyPersistence.TryAddActive(data, data.HP, data.MP);
            Debug.Log($"[Recruit] ✅ YES → CompanionPartyPersistence.TryAddActive 호출. 반환={added}, ActiveRoster.Count={CompanionPartyPersistence.ActiveRoster.Count}, WaitlistPaths.Count={CompanionPartyPersistence.WaitlistPaths.Count}");

            // "동료가 되었다" 메시지 (영어)
            yield return StartCoroutine(WaitForMessageSequence(new System.Collections.Generic.List<string>
            {
                $"<color=#90EE90>{data.MonsterName}</color> joined your party!"
            }));

            yield return new WaitForSeconds(3f);
        }
        else
        {
            Debug.Log($"[Recruit] NO → '{data.MonsterName}' 영입 거절, 즉시 맵 복귀");
        }
    }

    /// <summary>SpriteRenderer alpha를 0에서 originalColor의 alpha까지 duration초 동안 보간.</summary>
    private IEnumerator FadeInSpriteRoutine(SpriteRenderer sr, Color targetColor, float duration)
    {
        if (sr == null) yield break;
        float elapsed = 0f;
        float targetAlpha = targetColor.a;
        while (elapsed < duration)
        {
            if (sr == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float a = Mathf.Lerp(0f, targetAlpha, t);
            sr.color = new Color(targetColor.r, targetColor.g, targetColor.b, a);
            yield return null;
        }
        if (sr != null) sr.color = targetColor;
    }

    /// <summary>PlayMessageSequence를 동기적으로 대기 (시퀀스 종료까지 yield).</summary>
    private IEnumerator WaitForMessageSequence(System.Collections.Generic.IList<string> lines)
    {
        PlayMessageSequence(lines);
        yield return null; // 시퀀스 코루틴이 시작될 한 프레임 확보
        while (IsPlayingMessageSequence) yield return null;
    }

    /// <summary>YES/NO 패널을 Canvas 자식으로 런타임 생성. 최소 디자인.</summary>
    private GameObject BuildRecruitYesNoPanel(out Button yesBtn, out Button noBtn)
    {
        Canvas targetCanvas = this.canvas;
        if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();

        GameObject panel = new GameObject("RecruitYesNoPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(targetCanvas != null ? targetCanvas.transform : null, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420, 160);
        rt.anchoredPosition = Vector2.zero;

        var img = panel.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.85f);

        yesBtn = MakeRecruitDialogButton("YES", panel.transform, new Vector2(-95, 0));
        noBtn  = MakeRecruitDialogButton("NO",  panel.transform, new Vector2( 95, 0));

        panel.transform.SetAsLastSibling(); // 다른 UI 위로
        return panel;
    }

    private Button MakeRecruitDialogButton(string label, Transform parent, Vector2 anchoredPos)
    {
        GameObject go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150, 80);
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        GameObject txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        var txtRt = tmp.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go.GetComponent<Button>();
    }

    /// <summary>MonsterSO 고정 스탯/스킬로 PlayerStats 인스턴스 동적 생성.
    /// SetActive 패턴 — 비활성 상태에서 statData를 포함한 전체 셋업을 마친 뒤 활성화하여
    /// PlayerStats.Awake가 statData null인 상태로 실행되는 false positive 에러 차단.
    /// </summary>
    private PlayerStats CreateAllyFromCompanion(Abyssdawn.MonsterSO data)
    {
        if (data == null) return null;

        // ─────────────────────────────────────────────────────────────
        // STEP 1: GameObject 생성 + 즉시 비활성화 (Awake 차단)
        // Unity는 비활성 GameObject에 부착된 컴포넌트의 Awake를 호출하지 않음.
        // 표준 패턴 — "fully construct, then activate".
        // ─────────────────────────────────────────────────────────────
        GameObject allyObj = new GameObject($"Companion_{data.MonsterName}");
        allyObj.SetActive(false);
        Debug.Log($"[Recruit] CreateAllyFromCompanion: GameObject '{allyObj.name}' 생성 + 비활성화 (Awake 차단 모드)");

        if (playerPartyRoot != null)
        {
            allyObj.transform.SetParent(playerPartyRoot, false);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 2: PlayerStats 컴포넌트 부착 (비활성이라 Awake 안 됨)
        // ─────────────────────────────────────────────────────────────
        PlayerStats allyStats = allyObj.AddComponent<PlayerStats>();
        allyStats.playerName = data.MonsterName;
        allyStats.companionSource = data;

        // ─────────────────────────────────────────────────────────────
        // STEP 3: statData를 Awake 호출 전에 미리 할당 (핵심 — 에러 근원 제거)
        // CompanionSO는 동료 시스템 전용 데이터, PlayerStatData는 PlayerStats 작동에 필요
        // ─────────────────────────────────────────────────────────────
        allyStats.statData = ScriptableObject.CreateInstance<PlayerStatData>();

        // [고정 스탯] 동료는 레벨업 없음 (사용자 정책)
        allyStats.level = 1;
        allyStats.exp = 0;
        allyStats.maxExp = int.MaxValue; // 사실상 레벨업 차단

        // 7스탯은 MonsterSO에서 직접 읽음.
        allyStats.baseHP      = data.HP;
        allyStats.baseMP      = data.MP;
        allyStats.baseAttack  = data.ATK;
        allyStats.baseDefense = data.DEF;
        allyStats.baseMagic   = data.MAG;
        allyStats.baseAgility = data.AGI;
        allyStats.baseLuck    = data.LUK;

        // characterClass = null (CompanionSO는 직업 없음, base 스탯이 그대로 최종)

        // 스킬셋 복사 (PlayerStatData의 equippedSkills/equippedPassives는 List<SkillData>)
        int activeSkillCount = 0;
        if (data.ActiveSkills != null)
        {
            allyStats.statData.equippedSkills = new List<AbyssdawnBattle.SkillData>(data.ActiveSkills);
            activeSkillCount = allyStats.statData.equippedSkills.Count;
        }
        if (data.PassiveSkills != null && data.PassiveSkills.Count > 0)
        {
            // PassiveData는 별도 타입. equippedPassives는 List<SkillData>이므로 직접 호환 안 됨.
            // 1차에서는 패시브 스킬 효과 미적용 — 로그만 남기고 빈 리스트로.
            // (PassiveData → SkillData 변환 또는 별도 적용 경로는 2차에서 처리)
            Debug.LogWarning($"[Recruit] '{data.MonsterName}' PassiveSkills {data.PassiveSkills.Count}개 — 1차에서는 미적용 (2차 작업 대기)");
        }

        // 풀피로 초기화
        allyStats.currentHP = allyStats.maxHP;
        allyStats.currentMP = allyStats.maxMP;

        // ─────────────────────────────────────────────────────────────
        // STEP 4: 모든 셋업 완료 — 이제 활성화. Awake가 statData가 있는 상태로 실행됨.
        // ─────────────────────────────────────────────────────────────
        allyObj.SetActive(true);

        // 시각적 표현 없음 (Warrior/Rogue 패턴 동일)
        EnsureVisualForPartyMember(allyStats, false);

        // ─────────────────────────────────────────────────────────────
        // STEP 5: 검증 로그 — statData/스킬/스탯이 제대로 들어갔는지 가시화
        // ─────────────────────────────────────────────────────────────
        bool statDataOK = allyStats.statData != null;
        Debug.Log($"[Recruit] CreateAllyFromCompanion: '{data.MonsterName}' 생성 완료");
        Debug.Log($"[Recruit]   ├─ statData={(statDataOK ? "OK" : "NULL!")} (InstanceID={(statDataOK ? allyStats.statData.GetInstanceID() : 0)})");
        Debug.Log($"[Recruit]   ├─ HP {allyStats.currentHP}/{allyStats.maxHP}, MP {allyStats.currentMP}/{allyStats.maxMP}");
        Debug.Log($"[Recruit]   ├─ ATK {allyStats.Attack}, DEF {allyStats.Defense}, MAG {allyStats.Magic}, AGI {allyStats.Agility}, LUK {allyStats.Luck}");
        Debug.Log($"[Recruit]   └─ equippedSkills.Count={activeSkillCount}{(activeSkillCount > 0 ? $" (첫 스킬: '{allyStats.statData.equippedSkills[0]?.skillName}')" : " (액티브 스킬 없음)")}");

        // [Recruit-DIAG] HP 분해 추적 — CompanionSO.HP와 실제 maxHP가 다르면 어디서 +가 생겼는지 식별
        // 의심 1순위: PlayerStats.Awake가 statData.currentJob == null이면 SetClass("Warrior")로 자동 할당.
        var cc = allyStats.characterClass;
        int classHpBonus = cc != null ? cc.hpBonus : 0;
        float classHpMult = cc != null ? cc.hpMultiplier : 1f;
        int equipHp = allyStats.GetEquipmentHPBonus();
        int totalHpBonus = allyStats.GetHPBonus(); // maxHP - baseHP
        Debug.Log($"[Recruit-DIAG] HP 분해 — '{data.MonsterName}'");
        Debug.Log($"[Recruit-DIAG]   ├─ CompanionSO.HP={data.HP} → baseHP={allyStats.baseHP} (그대로 복사)");
        Debug.Log($"[Recruit-DIAG]   ├─ characterClass={(cc == null ? "NULL ✓" : $"'{cc.className}' ⚠ (자동 할당된 듯 — Awake가 SetClass 실행)")}");
        Debug.Log($"[Recruit-DIAG]   ├─ class hpMultiplier={classHpMult:F2}, hpBonus={classHpBonus}");
        Debug.Log($"[Recruit-DIAG]   ├─ equipmentHPBonus={equipHp} (장비 미장착이면 0)");
        Debug.Log($"[Recruit-DIAG]   ├─ totalHpBonus (maxHP - baseHP) = {totalHpBonus}");
        Debug.Log($"[Recruit-DIAG]   └─ maxHP({allyStats.maxHP}) = baseHP({allyStats.baseHP}) × mult({classHpMult:F2}) + classBonus({classHpBonus}) + 기타({totalHpBonus - classHpBonus})");
        if (cc != null)
        {
            Debug.LogWarning($"[Recruit-DIAG] ⚠ 동료에 직업이 할당됨: '{cc.className}'. CompanionSO는 직업 없이 고정 스탯이어야 함. PlayerStats.Awake의 fallback 자동 Warrior 할당이 원인으로 추정.");
        }

        return allyStats;
    }

    // -------------------- 메시지 --------------------
    // [PUBLIC 2026-05-13] PlayerStats.LevelUp 등 외부에서 호출 가능하도록 public 승격.
    public void AddMessage(string newMessage)
    {
        if (messageText == null) return; // 전투씬 외 호출 안전 가드
        // [SeqMsg-DIAG] 시퀀스 진행 중 다른 메시지가 AddMessage로 들어오면
        // messageText.text가 덮어쓰기되어 시퀀스 출력이 깨짐. 경고로 가시화.
        if (IsPlayingMessageSequence)
        {
            Debug.LogWarning($"[SeqMsg-DIAG] ⚠ AddMessage 호출됨 (시퀀스 진행 중!): \"{newMessage}\" — 시퀀스 텍스트 충돌 가능");
        }
        messageQueue.Enqueue(newMessage);
        if (messageQueue.Count > maxMessages) messageQueue.Dequeue();

        messageText.text = string.Join("\n", messageQueue.ToArray());
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 메시지 박스 전체 비움. messageQueue와 messageText를 모두 클리어.
    /// 레벨업 시퀀스가 빈 화면에서 시작되도록 호출.
    /// </summary>
    public void ClearMessages()
    {
        messageQueue.Clear();
        if (messageText != null)
        {
            messageText.text = "";
            messageText.maxVisibleCharacters = int.MaxValue;
        }
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        Debug.Log("[BattleManager] ClearMessages: 메시지 박스 비움");
    }

    // -------------------- 순차 메시지 (Dragon Quest 스타일) --------------------
    // 줄별 타이핑 효과 + 한 줄 끝나면 클릭/키 대기 → 다음 줄.
    // 모든 줄 완료 시 onComplete 콜백 호출. messageText 컴포넌트 재사용.
    // 타이핑은 TMP의 maxVisibleCharacters로 처리하여 컬러 태그(<color>, <b>)에 안전.

    private Coroutine _sequenceRoutine;

    /// <summary>현재 순차 메시지 출력 중인지.</summary>
    public bool IsPlayingMessageSequence => _sequenceRoutine != null;

    /// <summary>
    /// 여러 줄을 한 줄씩 타이핑 + 클릭 대기로 출력. 호출 즉시 반환(코루틴 시작만).
    /// onComplete: 모든 줄이 끝난 직후 호출 (전투 결과 닫기, 다음 단계 진행 등).
    /// </summary>
    public void PlayMessageSequence(IList<string> lines, System.Action onComplete = null)
    {
        Debug.Log($"[SeqMsg-DIAG] === PlayMessageSequence 진입 === lines.Count={(lines == null ? "(null)" : lines.Count.ToString())}, messageText={(messageText != null ? messageText.name : "NULL")}, gameObject.activeInHierarchy={gameObject.activeInHierarchy}");
        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                Debug.Log($"[SeqMsg-DIAG]   line[{i}] (len={(lines[i] == null ? 0 : lines[i].Length)}) = \"{lines[i]}\"");
            }
        }

        if (messageText == null)
        {
            Debug.LogWarning("[BattleManager] PlayMessageSequence: messageText가 null입니다. 콜백만 즉시 호출.");
            onComplete?.Invoke();
            return;
        }
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning("[SeqMsg-DIAG] lines 비어있음 → 즉시 onComplete");
            onComplete?.Invoke();
            return;
        }

        if (_sequenceRoutine != null)
        {
            // 이전 시퀀스가 진행 중이면 강제 종료 후 새로 시작
            Debug.LogWarning("[SeqMsg-DIAG] 이전 시퀀스가 진행 중 — StopCoroutine 후 재시작");
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }
        _sequenceRoutine = StartCoroutine(SequenceRoutine(lines, onComplete));
        Debug.Log("[SeqMsg-DIAG] StartCoroutine 완료. 코루틴 핸들 보존됨.");
    }

    /// <summary>현재 시퀀스를 강제로 끝내고 onComplete를 즉시 호출하지는 않는다.</summary>
    public void StopMessageSequence()
    {
        if (_sequenceRoutine != null)
        {
            Debug.LogWarning($"[SeqMsg-DIAG] !! StopMessageSequence 호출됨 (시퀀스 강제 중단). StackTrace:\n{System.Environment.StackTrace}");
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
            if (messageText != null) messageText.maxVisibleCharacters = int.MaxValue;
            HideAdvanceIndicator();
        }
    }

    // -------------------- Advance Indicator (▼ 텍스트 문자 방식) --------------------
    // 별도 GameObject 없이 messageText의 끝에 ▼ 글자를 매 프레임 append.
    // bob 움직임은 TMP의 <voffset> 태그로 처리. 깜빡임은 글자 추가/제거 토글.
    // 본문은 _indicatorBaseText에 보존되어 매 프레임 복원되므로 시퀀스 텍스트 충돌 없음.
    private Coroutine _indicatorRoutine;
    private string _indicatorBaseText = "";       // 인디케이터 표시 전 본문 텍스트(매 프레임 이 위에 ▼ 덧붙임)
    private int _indicatorBaseMaxVisible = int.MaxValue;
    private bool _indicatorActive = false;

    /// <summary>인디케이터 표시 + 깜빡임/bob 애니메이션 시작. 현재 messageText 본문을 base로 캐시.</summary>
    private void ShowAdvanceIndicator()
    {
        if (!indicatorEnabled || messageText == null) return;
        if (_indicatorActive) HideAdvanceIndicator(); // 중복 시작 방지

        _indicatorBaseText = messageText.text;
        _indicatorBaseMaxVisible = messageText.maxVisibleCharacters;
        // 인디케이터 표시 중엔 본문 + ▼ 둘 다 보여야 하므로 가시 제한 해제
        messageText.maxVisibleCharacters = int.MaxValue;
        _indicatorActive = true;
        if (_indicatorRoutine != null) StopCoroutine(_indicatorRoutine);
        _indicatorRoutine = StartCoroutine(IndicatorAnimationRoutine());
    }

    /// <summary>인디케이터 숨김 + 애니메이션 중단 + 본문 복원.</summary>
    private void HideAdvanceIndicator()
    {
        if (_indicatorRoutine != null)
        {
            StopCoroutine(_indicatorRoutine);
            _indicatorRoutine = null;
        }
        if (_indicatorActive && messageText != null)
        {
            messageText.text = _indicatorBaseText;
            messageText.maxVisibleCharacters = _indicatorBaseMaxVisible;
        }
        _indicatorActive = false;
    }

    /// <summary>매 프레임 본문 + ▼(옵션: voffset bob)을 다시 그려 깜빡임+움직임 효과.</summary>
    private System.Collections.IEnumerator IndicatorAnimationRoutine()
    {
        float startTime = Time.unscaledTime;
        float blinkTimer = 0f;
        bool visiblePhase = true;

        while (true)
        {
            float elapsed = Time.unscaledTime - startTime;

            // visible phase일 때만 ▼ 표시. 비가시 phase는 본문만.
            string suffix = "";
            if (visiblePhase)
            {
                if (indicatorBobEnabled && indicatorBobAmplitude > 0f)
                {
                    float bob = Mathf.Sin(elapsed * indicatorBobSpeed) * indicatorBobAmplitude;
                    // TMP voffset는 em 단위가 아닌 픽셀(혹은 폰트 크기 단위) — 일반적으로 px 작동
                    suffix = $"  <voffset={bob:0.00}>{indicatorChar}</voffset>";
                }
                else
                {
                    suffix = $"  {indicatorChar}";
                }
            }

            if (messageText != null) messageText.text = _indicatorBaseText + suffix;

            blinkTimer += Time.unscaledDeltaTime;
            if (blinkTimer >= indicatorBlinkInterval)
            {
                blinkTimer = 0f;
                visiblePhase = !visiblePhase;
            }

            yield return null;
        }
    }

    private System.Collections.IEnumerator SequenceRoutine(IList<string> lines, System.Action onComplete)
    {
        Debug.Log($"[SeqMsg-DIAG] SequenceRoutine 진입. 처리할 줄 수={lines.Count}");

        // [2026-05-24] 흐름 재구성:
        //   1) 기존 누적 메시지(승리/EXP 등)를 그대로 보여준 채 ▼ 표시 + 클릭 대기 1회
        //   2) 클릭 → ClearMessages → 빈 화면에서 시퀀스 시작
        //   3) 줄별 타이핑 + 클릭 대기 (기존)
        // 이전 누적(append) 모드는 폐기 — 사용자 요청: 시퀀스는 항상 빈 화면 첫 줄부터.

        HideAdvanceIndicator();
        messageText.maxVisibleCharacters = int.MaxValue;

        // ──────── 단계 1: 사전 메시지 확인 클릭 대기 ────────
        // 화면에 이미 표시된 메시지("All enemies defeated!" / "Victory! ...")를
        // 사용자가 충분히 본 뒤 클릭으로 넘기게 함.
        bool hasPriorMessages = !string.IsNullOrEmpty(messageText.text);
        if (hasPriorMessages)
        {
            Debug.Log("[SeqMsg-DIAG] 사전 메시지 존재 — ▼ 표시 + 클릭 대기 (clear 직전)");
            yield return null; // 직전 입력 잔향 회피
            ShowAdvanceIndicator();
            int preClearWait = 0;
            while (!IsAdvanceInputPressedThisFrame())
            {
                preClearWait++;
                yield return null;
            }
            HideAdvanceIndicator();
            Debug.Log($"[SeqMsg-DIAG] 사전 메시지 확인 클릭 감지 ({preClearWait} 프레임 대기)");
        }
        else
        {
            Debug.Log("[SeqMsg-DIAG] 사전 메시지 없음 — clear 단계 스킵");
        }

        // ──────── 단계 2: 메시지 박스 비움 ────────
        ClearMessages();
        messageText.maxVisibleCharacters = int.MaxValue;

        // accumulated는 빈 문자열에서 시작 → 시퀀스 첫 줄이 화면 맨 위에 옴
        string accumulated = "";

        for (int li = 0; li < lines.Count; li++)
        {
            string line = lines[li];
            if (string.IsNullOrEmpty(line))
            {
                Debug.LogWarning($"[SeqMsg-DIAG] ▶ 줄 #{li + 1} 스킵 (빈 문자열)");
                continue;
            }
            Debug.Log($"[SeqMsg-DIAG] ▶ 줄 #{li + 1}/{lines.Count} 시작: \"{line}\"");

            // 누적 + 새 줄(개행 포함) 전체를 텍스트에 박아두고 maxVisibleCharacters로 가린다.
            string full = string.IsNullOrEmpty(accumulated) ? line : (accumulated + "\n" + line);
            messageText.text = full;

            // ForceMeshUpdate로 textInfo.characterCount 동기화 (TMP 필수)
            messageText.ForceMeshUpdate();
            int totalVisibleChars = messageText.textInfo.characterCount;

            // 이전 줄까지의 보이는 글자 수 = accumulated 본문 + 개행 1자
            int startVisible = 0;
            if (!string.IsNullOrEmpty(accumulated))
            {
                messageText.text = accumulated;
                messageText.ForceMeshUpdate();
                startVisible = messageText.textInfo.characterCount + 1; // +1 for \n
                messageText.text = full;
                messageText.ForceMeshUpdate();
            }
            messageText.maxVisibleCharacters = startVisible;
            Debug.Log($"[SeqMsg-DIAG] 줄 #{li + 1} 타이핑 준비: startVisible={startVisible}, totalVisible={totalVisibleChars}, 타이핑할 글자수={totalVisibleChars - startVisible}");

            // 스크롤 맨 아래로
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;

            // 타이핑 효과
            bool skipped = false;
            for (int v = startVisible + 1; v <= totalVisibleChars; v++)
            {
                if (messageSkipOnInput && IsAdvanceInputPressedThisFrame())
                {
                    Debug.Log($"[SeqMsg-DIAG] 줄 #{li + 1} 타이핑 중 입력 감지 → 스킵 (v={v}/{totalVisibleChars})");
                    messageText.maxVisibleCharacters = totalVisibleChars;
                    skipped = true;
                    // 스킵 입력이 곧바로 다음 줄로 넘어가는 신호로도 작동하지 않도록 한 프레임 흘림
                    yield return null;
                    break;
                }
                messageText.maxVisibleCharacters = v;
                yield return new WaitForSecondsRealtime(messageTypingSpeed);
            }

            messageText.maxVisibleCharacters = totalVisibleChars;
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
            Debug.Log($"[SeqMsg-DIAG] 줄 #{li + 1} 타이핑 완료. messageText.text(첫 80자)=\"{(messageText.text.Length > 80 ? messageText.text.Substring(0, 80) + "..." : messageText.text)}\"");

            // 줄 완료 — 마지막 줄이면 마지막 확인 대기, 아니면 다음 줄 진행 대기
            bool isLastLine = (li == lines.Count - 1);
            if (!isLastLine)
            {
                Debug.Log($"[SeqMsg-DIAG] 줄 #{li + 1} → 다음 줄 진행 입력 대기 시작 (Key={messageAdvanceKey}, MouseClick={messageAdvanceOnMouseClick})");
                // 직전 스킵 입력의 잔향을 피해 한 프레임 대기
                if (skipped) yield return null;
                ShowAdvanceIndicator(); // ▼ 표시 시작
                int waitFrames = 0;
                while (!IsAdvanceInputPressedThisFrame())
                {
                    waitFrames++;
                    yield return null;
                }
                HideAdvanceIndicator(); // 입력 감지 → ▼ 즉시 숨김
                Debug.Log($"[SeqMsg-DIAG] 줄 #{li + 1} 입력 감지! {waitFrames} 프레임 대기 후 다음 줄로 진행");
            }
            else
            {
                Debug.Log($"[SeqMsg-DIAG] 줄 #{li + 1} 마지막 줄 — for 루프 종료");
            }

            accumulated = full;
        }

        // 마지막 줄도 클릭 한 번 더 받아야 닫히도록 (드퀘식 — 마지막 화면 확인 대기)
        Debug.Log("[SeqMsg-DIAG] 모든 줄 출력 완료. 최종 확인 입력 대기 시작");
        // 직전 스킵/진행 입력의 잔향을 피해 한 프레임 대기
        yield return null;
        ShowAdvanceIndicator(); // 마지막 줄에도 ▼ 표시
        int finalWait = 0;
        while (!IsAdvanceInputPressedThisFrame())
        {
            finalWait++;
            yield return null;
        }
        HideAdvanceIndicator();
        Debug.Log($"[SeqMsg-DIAG] 최종 입력 감지! {finalWait} 프레임 대기");

        // 시퀀스 출력은 그대로 화면에 남음 (사용자가 마지막 클릭 직후 페이드아웃 대기 동안 메시지 확인).
        // messageText.text는 모든 시퀀스 줄이 포함된 상태(=accumulated). 가시 제한만 해제.
        messageText.maxVisibleCharacters = int.MaxValue;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;

        _sequenceRoutine = null;
        Debug.Log("[SeqMsg-DIAG] 시퀀스 정상 종료. onComplete 호출");
        onComplete?.Invoke();
    }

    /// <summary>이번 프레임에 진행 입력(키 또는 마우스 좌클릭)이 들어왔는지.</summary>
    private bool IsAdvanceInputPressedThisFrame()
    {
        if (Input.GetKeyDown(messageAdvanceKey)) return true;
        if (messageAdvanceOnMouseClick && Input.GetMouseButtonDown(0)) return true;
        return false;
    }

    /// <summary>
    /// 상태이상 타입에 대응하는 TMP 색상 hex 코드를 반환합니다.
    /// </summary>
    private static string GetStatusColor(AbyssdawnBattle.StatusEffectType type)
    {
        return type switch
        {
            AbyssdawnBattle.StatusEffectType.Ignite  => "#FF6600",
            AbyssdawnBattle.StatusEffectType.Bleed   => "#8B0000",
            AbyssdawnBattle.StatusEffectType.Stun    => "#FFD700",
            AbyssdawnBattle.StatusEffectType.Poison  => "#4B6B2A",
            _                                        => "#FFFFFF",
        };
    }



    private void HandleEnemyHover()
    {
        // 타겟 선택 모드일 때만 하이라이트
        if (!waitingForTargetSelection || Camera.main == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        Collider2D[] colliders = Physics2D.OverlapPointAll(mousePos);
        EnemyStats hovered = null;

        foreach (var col in colliders)
        {
            hovered = col.GetComponent<EnemyStats>();
            if (hovered != null && hovered.currentHP > 0) break;
        }

        if (hovered != hoveredEnemy)
        {
            if (hoveredEnemy != null)
            {
                hoveredEnemy.SetHighlight(false);
            }

            hoveredEnemy = hovered;

            if (hoveredEnemy != null)
            {
                hoveredEnemy.SetHighlight(true);
            }
        }
    }

    // ========== 파티 상태 UI ==========
    private void RebuildPlayerStatusPanel()
    {
        // 패널이 없으면 찾거나 생성
        if (playerStatusPanel == null)
        {
            GameObject panelObj = GameObject.Find("PlayerStatusPanel");
            if (panelObj == null)
            {
                // Canvas 찾기 (Inspector에 할당된 Canvas 우선 사용)
                Canvas targetCanvas = this.canvas;
                if (targetCanvas == null) targetCanvas = FindAnyObjectByType<Canvas>();
                
                if (targetCanvas != null)
                {
                    panelObj = new GameObject("PlayerStatusPanel", typeof(RectTransform));
                    panelObj.transform.SetParent(targetCanvas.transform, false);
                    Debug.Log($"[BattleManager] Created PlayerStatusPanel automatically on Canvas: {targetCanvas.name}");
                }
            }
            
            if (panelObj != null)
            {
                playerStatusPanel = panelObj.GetComponent<RectTransform>();
            }
            else
            {
                Debug.LogError("[BattleManager] Could not find or create PlayerStatusPanel! No Canvas found.");
                return;
            }
        }

        // 패널 활성화 보장
        if (!playerStatusPanel.gameObject.activeSelf)
        {
            playerStatusPanel.gameObject.SetActive(true);
            Debug.Log("[BattleManager] Activated PlayerStatusPanel");
        }

        // 최상단에 그리기 (다른 UI에 가려지지 않도록)
        playerStatusPanel.SetAsLastSibling();
        
        // 레이어 설정 (UI)
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) playerStatusPanel.gameObject.layer = uiLayer;

        // 기존 바인딩 초기화
        playerStatusSlots.Clear();
        playerStatusNameTexts.Clear();
        playerStatusHPTexts.Clear();
        playerStatusMPTexts.Clear();
        playerStatusTexts.Clear();
        playerStatusBackgrounds.Clear();
        playerStatusIconRows.Clear();
        playerStatusIconImages.Clear();

        // 씬에 이미 만든 PartyBar/PartySlot_1~4 구조가 있으면 그대로 사용
        if (TryBindExistingPartySlots())
        {
            ApplyPlayerStatusFontSettings();
            return;
        }

        foreach (Transform child in playerStatusPanel)
        {
            Destroy(child.gameObject);
        }

        // 패널 설정 (화면 하단)
        RectTransform panelRT = playerStatusPanel;
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.offsetMin = new Vector2(0f, partyPanelBottomMargin);
        panelRT.offsetMax = new Vector2(0f, partyPanelBottomMargin + partyPanelHeight);
        panelRT.localScale = Vector3.one; // 스케일 초기화

        // 슬롯 크기 계산
        float panelWidth = Screen.width;
        // CanvasScaler가 있을 경우 Screen.width가 아니라 Canvas의 크기를 기준으로 해야 함
        Canvas rootCanvas = playerStatusPanel.GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            panelWidth = canvasRT.rect.width;
            Debug.Log($"[BattleManager] Using Canvas width: {panelWidth} (Canvas: {rootCanvas.name})");
        }
        else
        {
            Debug.LogWarning("[BattleManager] PlayerStatusPanel has no parent Canvas!");
        }

        // 여백을 좀 더 넉넉하게
        float totalPadding = partyPanelOuterPadding * 2 + partyPanelInnerPadding * 3;
        float slotWidth = (panelWidth - totalPadding) / 4f;
        float slotHeight = partyPanelHeight - partyPanelVerticalPadding * 2;

        Debug.Log($"[BattleManager] Rebuilding Party UI. Panel: {playerStatusPanel.name}, Active: {playerStatusPanel.gameObject.activeInHierarchy}, Pos: {panelRT.anchoredPosition}, Size: {panelRT.rect.size}");

        // 4개 슬롯 생성
        for (int i = 0; i < 4; i++)
        {
            // 1. 슬롯 컨테이너 (투명)
            GameObject slotObj = new GameObject($"PartySlot_{i}", typeof(RectTransform), typeof(Image));
            slotObj.transform.SetParent(playerStatusPanel, false);

            RectTransform slotRT = slotObj.GetComponent<RectTransform>();
            float xPos = partyPanelOuterPadding + (slotWidth + partyPanelInnerPadding) * i;
            
            // 앵커를 좌하단 기준으로 설정하여 위치 고정
            slotRT.anchorMin = new Vector2(0f, 0f);
            slotRT.anchorMax = new Vector2(0f, 0f);
            slotRT.pivot = new Vector2(0f, 0f);
            slotRT.anchoredPosition = new Vector2(xPos, partyPanelVerticalPadding);
            slotRT.sizeDelta = new Vector2(slotWidth, slotHeight);

            // 컨테이너는 투명 (Raycast Target은 유지하여 클릭 가능성 열어둠)
            Image slotImg = slotObj.GetComponent<Image>();
            slotImg.color = new Color(0f, 0f, 0f, 0f);

            // 2. 테두리 생성 (4개의 라인)
            float borderThickness = 2f;
            Color borderColor = Color.white;

            // Top Border
            CreateBorderLine(slotObj.transform, "TopBorder", borderColor, 
                new Vector2(0, 1), new Vector2(1, 1), 
                new Vector2(0, -borderThickness), new Vector2(0, 0));

            // Bottom Border
            CreateBorderLine(slotObj.transform, "BottomBorder", borderColor, 
                new Vector2(0, 0), new Vector2(1, 0), 
                new Vector2(0, 0), new Vector2(0, borderThickness));

            // Left Border
            CreateBorderLine(slotObj.transform, "LeftBorder", borderColor, 
                new Vector2(0, 0), new Vector2(0, 1), 
                new Vector2(0, 0), new Vector2(borderThickness, 0));

            // Right Border
            CreateBorderLine(slotObj.transform, "RightBorder", borderColor, 
                new Vector2(1, 0), new Vector2(1, 1), 
                new Vector2(-borderThickness, 0), new Vector2(0, 0));

            // 3. 내부 배경 (투명, 턴 활성화 시 색상 변경용)
            GameObject innerObj = new GameObject("InnerBackground", typeof(RectTransform), typeof(Image));
            innerObj.transform.SetParent(slotObj.transform, false);
            
            RectTransform innerRT = innerObj.GetComponent<RectTransform>();
            innerRT.anchorMin = new Vector2(0f, 0f);
            innerRT.anchorMax = new Vector2(1f, 1f);
            // 테두리와 겹치지 않게 약간 안쪽으로
            innerRT.offsetMin = new Vector2(borderThickness, borderThickness);
            innerRT.offsetMax = new Vector2(-borderThickness, -borderThickness);
            
            Image innerImg = innerObj.GetComponent<Image>();
            innerImg.color = new Color(0f, 0f, 0f, 0f); // 기본 투명
            playerStatusBackgrounds.Add(innerImg); // 배경색 변경을 위해 저장

            // 4. 텍스트 (내부 배경의 자식으로)
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(innerObj.transform, false);

            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0f);
            textRT.anchorMax = new Vector2(1f, 1f);
            textRT.offsetMin = new Vector2(partySlotLeftMargin, partySlotBottomMargin);
            textRT.offsetMax = new Vector2(-partySlotRightMargin, -partySlotTopMargin);

            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color(1f, 1f, 1f, 1f); // 완전 불투명 흰색
            text.fontSize = partySlotFontSize;
            
            playerStatusTexts.Add(text);
        }

        ApplyPlayerStatusFontSettings();
    }

    private bool TryBindExistingPartySlots()
    {
        Transform slotRoot = FindExistingPartyBarRoot();
        if (slotRoot == null) return false;

        List<Transform> foundSlots = new List<Transform>();
        for (int i = 0; i < 4; i++)
        {
            Transform slot = FindPartySlot(slotRoot, i);
            if (slot == null)
            {
                return false;
            }
            foundSlots.Add(slot);
        }

        playerStatusTexts.Clear();
        playerStatusBackgrounds.Clear();

        foreach (Transform slot in foundSlots)
        {
            playerStatusSlots.Add(slot as RectTransform);

            Image bg = slot.GetComponent<Image>();
            if (bg == null)
            {
                Transform inner = slot.Find("InnerBackground");
                if (inner != null) bg = inner.GetComponent<Image>();
            }
            playerStatusBackgrounds.Add(bg);

            TextMeshProUGUI nameText = FindTextInSlot(slot, "Nametext", "NameText");
            TextMeshProUGUI hpText = FindTextInSlot(slot, "HPText");
            TextMeshProUGUI mpText = FindTextInSlot(slot, "MPText");

            Debug.Log($"[BattleManager] Slot bind - {slot.name} | Name: {(nameText != null ? nameText.name : "NULL")} | HP: {(hpText != null ? hpText.name : "NULL")} | MP: {(mpText != null ? mpText.name : "NULL")}");

            playerStatusNameTexts.Add(nameText);
            playerStatusHPTexts.Add(hpText);
            playerStatusMPTexts.Add(mpText);

            Transform statusIconRow = FindNamedDescendantRecursive(slot, "StatusIconRow");
            playerStatusIconRows.Add(statusIconRow);
            playerStatusIconImages.Add(BindStatusIcons(statusIconRow));

            // 흔들림/기존 폴백 처리를 위해 대표 텍스트 하나를 유지
            playerStatusTexts.Add(hpText != null ? hpText : (nameText != null ? nameText : mpText));
        }

        Debug.Log($"[BattleManager] Bound existing PartyBar slots: {playerStatusSlots.Count}");
        return playerStatusSlots.Count == 4;
    }

    private Transform FindExistingPartyBarRoot()
    {
        if (playerStatusPanel != null)
        {
            Transform nested = playerStatusPanel.transform.Find("PartyBar");
            if (nested != null) return nested;
        }

        GameObject globalPartyBar = GameObject.Find("PartyBar");
        if (globalPartyBar != null) return globalPartyBar.transform;

        if (playerStatusPanel != null)
        {
            Transform directSlot = FindPartySlot(playerStatusPanel.transform, 0);
            if (directSlot != null) return playerStatusPanel.transform;
        }

        return null;
    }

    private Transform GetPartySlotTransform(int index)
    {
        Transform slotRoot = FindExistingPartyBarRoot();

        return FindPartySlot(slotRoot, index);
    }

    private Transform FindPartySlot(Transform slotRoot, int index)
    {
        if (slotRoot == null) return null;

        string[] candidateNames =
        {
            $"PartySlot_{index + 1}",
            $"PartySlot{index + 1}",
            $"PartySlot_{index}",
            $"PartySlot{index}"
        };

        foreach (string candidate in candidateNames)
        {
            Transform found = slotRoot.Find(candidate);
            if (found != null) return found;
        }

        return null;
    }

    private Transform FindExistingEnemyBarRoot()
    {
        if (enemyStatusPanel != null)
        {
            if (FindEnemySlot(enemyStatusPanel, 0) != null) return enemyStatusPanel;

            Transform nested = enemyStatusPanel.Find("EnemyBar");
            if (nested != null) return nested;
        }

        GameObject globalEnemyBar = GameObject.Find("EnemyBar");
        if (globalEnemyBar != null) return globalEnemyBar.transform;

        if (enemyStatusPanel != null)
        {
            Transform directSlot = FindEnemySlot(enemyStatusPanel, 0);
            if (directSlot != null) return enemyStatusPanel;
        }

        return null;
    }

    private void EnsureEnemyStatusPanelReference()
    {
        Transform existingEnemyBar = FindExistingEnemyBarRoot();
        if (existingEnemyBar != null)
        {
            enemyStatusPanel = existingEnemyBar;
            return;
        }

        GameObject globalEnemyBar = GameObject.Find("EnemyBar");
        if (globalEnemyBar != null)
        {
            enemyStatusPanel = globalEnemyBar.transform;
        }
    }

    private Transform FindEnemySlot(Transform slotRoot, int index)
    {
        if (slotRoot == null) return null;

        string[] candidateNames =
        {
            $"EnemySlot_{index + 1}",
            $"EnemySlot{index + 1}",
            $"EnemySlot_{index}",
            $"EnemySlot{index}"
        };

        foreach (string candidate in candidateNames)
        {
            Transform found = slotRoot.Find(candidate);
            if (found != null) return found;
        }

        return null;
    }

    private TextMeshProUGUI FindTextInSlot(Transform slot, params string[] names)
    {
        if (slot == null) return null;

        foreach (string name in names)
        {
            Transform child = FindNamedDescendantRecursive(slot, name);
            if (child != null)
            {
                TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    Debug.Log($"[BattleManager] Found text '{name}' directly on {child.name} in slot {slot.name}");
                    return tmp;
                }

                tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                    Debug.Log($"[BattleManager] Found text '{name}' under child {child.name} -> TMP {tmp.name} in slot {slot.name}");
                    return tmp;
                }
            }
        }

        foreach (Transform child in slot)
        {
            TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null) return tmp;
        }

        TextMeshProUGUI fallback = slot.GetComponentInChildren<TextMeshProUGUI>(true);
        if (fallback == null)
        {
            Debug.LogWarning($"[BattleManager] No TMP text found in slot {slot.name}");
        }
        else
        {
            Debug.LogWarning($"[BattleManager] Fallback TMP used for slot {slot.name}: {fallback.name}");
        }

        return fallback;
    }

    private Image FindImageInSlot(Transform slot, params string[] names)
    {
        if (slot == null) return null;

        foreach (string name in names)
        {
            Transform child = FindNamedDescendantRecursive(slot, name);
            if (child == null) continue;

            Image img = child.GetComponent<Image>();
            if (img != null)
            {
                return img;
            }

            img = child.GetComponentInChildren<Image>(true);
            if (img != null)
            {
                return img;
            }
        }

        return null;
    }

    private List<Image> BindStatusIcons(Transform statusIconRow)
    {
        List<Image> icons = new List<Image>();
        if (statusIconRow == null) return icons;

        for (int i = 1; i <= 4; i++)
        {
            Transform iconTransform = FindNamedDescendantRecursive(statusIconRow, $"StatusIcon_{i}");
            if (iconTransform == null)
            {
                iconTransform = FindNamedDescendantRecursive(statusIconRow, $"StatusIcon{i}");
            }

            Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (iconImage != null)
            {
                icons.Add(iconImage);
            }
        }

        return icons;
    }

    private void UpdatePlayerStatusIcons(int index, PlayerStats member)
    {
        if (index < 0 || index >= playerStatusIconImages.Count) return;

        List<Image> icons = playerStatusIconImages[index];
        if (icons == null || icons.Count == 0) return;

        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;
            icons[i].enabled = false;
            icons[i].sprite = null;
            icons[i].color = new Color(1f, 1f, 1f, 0f);
        }

        if (member == null || member.activeStatusEffects == null) return;

        int shown = 0;
        foreach (var status in member.activeStatusEffects)
        {
            if (shown >= icons.Count) break;
            if (status == null || status.data == null) continue;

            Sprite iconSprite = status.data.flatIcon != null ? status.data.flatIcon : status.data.itemIcon;
            if (iconSprite == null) continue;

            Image iconImage = icons[shown];
            if (iconImage == null) continue;

            iconImage.sprite = iconSprite;
            iconImage.enabled = true;
            iconImage.color = Color.white;
            shown++;
        }
    }

    private void UpdateEnemyStatusIcons(int index, EnemyStats enemyStats)
    {
        if (index < 0 || index >= enemyStatusIconImages.Count) return;

        List<Image> icons = enemyStatusIconImages[index];
        if (icons == null || icons.Count == 0) return;

        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;
            icons[i].enabled = false;
            icons[i].sprite = null;
            icons[i].color = new Color(1f, 1f, 1f, 0f);
        }

        if (enemyStats == null || enemyStats.activeStatusEffects == null) return;

        int shown = 0;
        foreach (var status in enemyStats.activeStatusEffects)
        {
            if (shown >= icons.Count) break;
            if (status == null || status.data == null) continue;

            Sprite iconSprite = status.data.flatIcon != null ? status.data.flatIcon : status.data.itemIcon;
            if (iconSprite == null) continue;

            Image iconImage = icons[shown];
            if (iconImage == null) continue;

            iconImage.sprite = iconSprite;
            iconImage.enabled = true;
            iconImage.color = Color.white;
            shown++;
        }
    }

    private void StopAllEnemySlotEffects()
    {
        foreach (var pair in enemySlotPulseCoroutines)
        {
            if (pair.Value != null) StopCoroutine(pair.Value);
        }
        enemySlotPulseCoroutines.Clear();

        foreach (var pair in enemySlotShakeCoroutines)
        {
            if (pair.Value != null) StopCoroutine(pair.Value);
        }
        enemySlotShakeCoroutines.Clear();

        for (int i = 0; i < enemyStatusMonsterImages.Count; i++)
        {
            ResetEnemySlotVisuals(i);
        }
    }

    private void ResetEnemySlotVisuals(int index)
    {
        if (index >= 0 && index < enemyStatusMonsterImages.Count && enemyStatusMonsterImages[index] != null)
        {
            enemyStatusMonsterImages[index].color = Color.white;
            enemyStatusMonsterImages[index].rectTransform.localScale = Vector3.one;
        }

        if (index >= 0 && index < enemyStatusFrames.Count && enemyStatusFrames[index] != null)
        {
            Color c = enemyStatusFrames[index].color;
            c.a = 1f;
            enemyStatusFrames[index].color = c;
        }

        if (index >= 0 && index < enemyStatusNameTexts.Count && enemyStatusNameTexts[index] != null)
        {
            enemyStatusNameTexts[index].color = Color.white;
            enemyStatusNameTexts[index].rectTransform.localScale = Vector3.one;
        }
    }

    private void SetEnemySlotPulse(int index, bool enabled)
    {
        if (enabled)
        {
            if (!enemySlotPulseCoroutines.ContainsKey(index))
            {
                enemySlotPulseCoroutines[index] = StartCoroutine(EnemySlotPulseRoutine(index));
            }
            return;
        }

        if (enemySlotPulseCoroutines.TryGetValue(index, out Coroutine routine) && routine != null)
        {
            StopCoroutine(routine);
        }
        enemySlotPulseCoroutines.Remove(index);
        ResetEnemySlotVisuals(index);
    }

    private IEnumerator EnemySlotPulseRoutine(int index)
    {
        while (index >= 0 && index < enemyStatusNameTexts.Count)
        {
            TextMeshProUGUI nameText = enemyStatusNameTexts[index];
            if (nameText == null) yield break;

            float t = (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.45f, 1f, t);
            float scale = Mathf.Lerp(1f, 1.05f, t);

            nameText.color = new Color(1f, 1f, 1f, alpha);
            nameText.rectTransform.localScale = Vector3.one * scale;

            yield return null;
        }
    }

    private void TriggerEnemySlotShake(int index)
    {
        if (index < 0 || index >= enemyStatusMonsterImages.Count) return;
        Image monsterImage = enemyStatusMonsterImages[index];
        if (monsterImage == null) return;

        if (enemySlotShakeCoroutines.TryGetValue(index, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }

        enemySlotShakeCoroutines[index] = StartCoroutine(EnemySlotShakeRoutine(index));
    }

    private IEnumerator EnemySlotShakeRoutine(int index)
    {
        if (index < 0 || index >= enemyStatusMonsterImages.Count) yield break;

        Image monsterImage = enemyStatusMonsterImages[index];
        if (monsterImage == null) yield break;

        RectTransform rt = monsterImage.rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        float duration = 0.15f;
        float magnitude = 8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = UnityEngine.Random.Range(-magnitude, magnitude);
            float offsetY = UnityEngine.Random.Range(-magnitude * 0.35f, magnitude * 0.35f);
            rt.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = basePos;
        enemySlotShakeCoroutines.Remove(index);
    }

    private Transform FindNamedDescendantRecursive(Transform root, string targetName)
    {
        if (root == null) return null;

        foreach (Transform child in root)
        {
            if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindNamedDescendantRecursive(child, targetName);
            if (nested != null) return nested;
        }

        return null;
    }

    private void CreateBorderLine(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject lineObj = new GameObject(name, typeof(RectTransform), typeof(Image));
        lineObj.transform.SetParent(parent, false);
        
        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        
        Image img = lineObj.GetComponent<Image>();
        img.color = color;
    }

    private void ApplyPlayerStatusFontSettings()
    {
        foreach (var text in playerStatusTexts)
        {
            if (text == null) continue;
            text.fontSize = partySlotFontSize;
            text.lineSpacing = partySlotLineSpacing;
            text.color = new Color(1f, 1f, 1f, 1f); // 완전 불투명 흰색
        }

        foreach (var text in playerStatusNameTexts)
        {
            if (text == null) continue;
            text.fontSize = partySlotFontSize;
            text.color = new Color(1f, 1f, 1f, 1f);
        }

        foreach (var text in playerStatusHPTexts)
        {
            if (text == null) continue;
            text.fontSize = partySlotFontSize;
            text.color = new Color(1f, 1f, 1f, 1f);
        }

        foreach (var text in playerStatusMPTexts)
        {
            if (text == null) continue;
            text.fontSize = partySlotFontSize;
            text.color = new Color(1f, 1f, 1f, 1f);
        }
    }

    // ========== 스킬 시스템 ==========
private void CacheHeroSkills()
{
    heroSkillCache.Clear();
    roleSkillCache.Clear();

    // Load Hero skills from PlayerStatData
    if (playerStatData != null && playerStatData.equippedSkills != null)
    {
        foreach (var skill in playerStatData.equippedSkills)
        {
            if (skill != null && skill.IsActive)  // Active 스킬만
            {
                heroSkillCache.Add(skill);
                Debug.Log($"[BattleManager] Loaded equipped skill: {skill.skillName} (ID: {skill.skillID})");

                // Keep fireball reference for legacy magic usage
                if (skill.skillID == "03")
                {
                    fireballSkill = skill;
                }
            }
        }

        // 역할별 스킬 캐시 설정
        roleSkillCache[PartyRole.Hero] = new List<SkillData>(heroSkillCache);
    }
    else
    {
        Debug.LogWarning("[BattleManager] playerStatData or equippedSkills is null!");
        roleSkillCache[PartyRole.Hero] = new List<SkillData>();
    }

    // Load skills for other roles from skillLibrary (legacy system, will be improved later)
    if (skillLibrary != null)
    {
        // 워리어: Strong Slash (Shield Wall은 패시브)
        var strongSlash = skillLibrary.GetSkillByID("01");
        if (strongSlash != null)
        {
            roleSkillCache[PartyRole.Warrior] = new List<SkillData> { strongSlash };
            Debug.Log($"[BattleManager] Warrior skill added: {strongSlash.skillName}");
        }
        else
        {
            roleSkillCache[PartyRole.Warrior] = new List<SkillData>();
        }

        // 로그: Quickhand
        var quickhand = skillLibrary.GetSkillByID("06");
        if (quickhand != null)
        {
            roleSkillCache[PartyRole.Rogue] = new List<SkillData> { quickhand };
            Debug.Log($"[BattleManager] Rogue skill added: {quickhand.skillName}");
        }
        else
        {
            roleSkillCache[PartyRole.Rogue] = new List<SkillData>();
        }

        // 마법사: Meditation, Magic Bolt, Fireball
        var meditation = skillLibrary.GetSkillByID("04");
        var magicBolt = skillLibrary.GetSkillByID("05");
        var fireball = skillLibrary.GetSkillByID("02");

        roleSkillCache[PartyRole.Wizard] = new List<SkillData>();

        if (meditation != null)
        {
            roleSkillCache[PartyRole.Wizard].Add(meditation);
            Debug.Log($"[BattleManager] Wizard skill added: {meditation.skillName}");
        }

        if (magicBolt != null)
        {
            roleSkillCache[PartyRole.Wizard].Add(magicBolt);
            Debug.Log($"[BattleManager] Wizard skill added: {magicBolt.skillName}");
        }

        if (fireball != null)
        {
            roleSkillCache[PartyRole.Wizard].Add(fireball);
            Debug.Log($"[BattleManager] Wizard skill added: {fireball.skillName}");
        }
    }
    else
    {
        Debug.LogWarning("[BattleManager] skillLibrary is null! Other party roles will have no skills.");
        roleSkillCache[PartyRole.Warrior] = new List<SkillData>();
        roleSkillCache[PartyRole.Rogue] = new List<SkillData>();
        roleSkillCache[PartyRole.Wizard] = new List<SkillData>();
    }

    // 로그 출력
    Debug.Log($"[BattleManager] Skill cache initialized - Hero: {roleSkillCache[PartyRole.Hero].Count} skills, Warrior: {roleSkillCache[PartyRole.Warrior].Count} skills, Rogue: {roleSkillCache[PartyRole.Rogue].Count} skills, Wizard: {roleSkillCache[PartyRole.Wizard].Count} skills");
}
    
    // 현재 캐릭터의 스킬 목록 가져오기
    public List<SkillData> GetCurrentActorSkills()
    {
        if (currentControlledMember == null) return new List<SkillData>();

        if (companionSkillsByMember.TryGetValue(currentControlledMember, out var compSkills))
            return compSkills;

        PartyRole role = GetPartyRole(currentControlledMember);
        if (roleSkillCache.ContainsKey(role))
            return roleSkillCache[role];

        return new List<SkillData>();
    }

    // ========== UI 업데이트 --------------------
    private const string DeathSkullResource = "UI/DeathSkull";
    private const string DeathSkullObjectName = "DeathSkull";
    private static Sprite _deathSkullSprite;

    /// <summary>
    /// 쓰러진 아군 카드에 해골 그림을 깐다 (카드 배경 위, 이름·HP 글자 아래). 살아나면 숨긴다.
    /// 그림: Resources/UI/DeathSkull.png — 없으면 아무것도 하지 않음.
    /// </summary>
    private static void SetDeathSkull(Transform card, bool dead)
    {
        if (card == null) return;
        Transform existing = card.Find(DeathSkullObjectName);
        if (!dead)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }
        if (existing == null)
        {
            if (_deathSkullSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>(DeathSkullResource);
                if (tex == null)
                {
                    Debug.LogWarning($"[BattleManager] 사망 해골 그림 'Resources/{DeathSkullResource}' 을(를) 찾지 못했습니다.");
                    return;
                }
                _deathSkullSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            var go = new GameObject(DeathSkullObjectName, typeof(RectTransform));
            go.transform.SetParent(card, false);
            go.transform.SetAsFirstSibling(); // 카드 배경 위, 글자 아래
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
            var img = go.AddComponent<Image>();
            img.sprite = _deathSkullSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.9f);
            existing = go.transform;
        }
        existing.gameObject.SetActive(true);
    }

    private void UpdateStatusUI()
    {
        string heroHpLine = player != null ? $"HP: {player.currentHP} / {player.maxHP}" : "HP: -";
        string heroMpLine = player != null ? $"MP: {player.currentMP} / {player.maxMP}" : "MP: -";

        bool hasPartyPanel = playerStatusSlots.Count > 0 &&
            playerStatusNameTexts.Count > 0 &&
            playerStatusHPTexts.Count > 0 &&
            playerStatusMPTexts.Count > 0;

        if (hasPartyPanel)
        {
            if (playerHPText != null) playerHPText.text = "";
            if (playerMPText != null) playerMPText.text = "";

            // 스타일 정의 (투명 배경, 흰색 테두리, 흰색 텍스트)
            Color activeSlotColor = new Color(1f, 1f, 1f, 0.1f); // 활성 턴: 아주 연한 흰색 (10%)
            Color inactiveSlotColor = new Color(0f, 0f, 0f, 0f);       // 비활성: 완전 투명 (0%)
            Color aliveTextColor = Color.white;
            Color downedTextColor = new Color(1f, 0.5f, 0.5f, 1f);   // 기절: 붉은색 틴트

            for (int i = 0; i < playerStatusTexts.Count; i++)
            {
                var tmp = playerStatusTexts[i];
                if (tmp == null) continue;

                // 텍스트 부모의 부모가 슬롯(외곽선)이고, 텍스트 부모가 내부 배경임
                Transform innerBgTrans = tmp.transform.parent;
                Image bg = innerBgTrans != null ? innerBgTrans.GetComponent<Image>() : null;
                
                if (bg == null && innerBgTrans != null)
                {
                     bg = innerBgTrans.GetComponentInParent<Image>();
                }

                bool hasMember = i < activePartyMembers.Count && activePartyMembers[i] != null;
                if (hasMember)
                {
                    var member = activePartyMembers[i];
                    bool hasDedicatedSlotTexts =
                        i < playerStatusNameTexts.Count && i < playerStatusHPTexts.Count && i < playerStatusMPTexts.Count &&
                        playerStatusNameTexts[i] != null && playerStatusHPTexts[i] != null && playerStatusMPTexts[i] != null;

                    if (hasDedicatedSlotTexts)
                    {
                        playerStatusNameTexts[i].text = member.playerName;
                        playerStatusHPTexts[i].text = $"HP {member.currentHP}/{member.maxHP}";
                        playerStatusMPTexts[i].text = $"MP {member.currentMP}/{member.maxMP}";
                    }
                    else
                    {
                        // 드래곤 퀘스트 스타일: 이름, HP, MP (인덱스 제거)
                        tmp.text = $"{member.playerName}\nHP {member.currentHP}/{member.maxHP}\nMP {member.currentMP}/{member.maxMP}";
                    }
                    
                    bool isDead = member.currentHP <= 0;
                    tmp.color = isDead ? downedTextColor : aliveTextColor;
                    if (i < playerStatusNameTexts.Count && playerStatusNameTexts[i] != null)
                        playerStatusNameTexts[i].color = isDead ? downedTextColor : aliveTextColor;
                    if (i < playerStatusHPTexts.Count && playerStatusHPTexts[i] != null)
                        playerStatusHPTexts[i].color = isDead ? downedTextColor : aliveTextColor;
                    if (i < playerStatusMPTexts.Count && playerStatusMPTexts[i] != null)
                        playerStatusMPTexts[i].color = isDead ? downedTextColor : aliveTextColor;

                    SetDeathSkull(innerBgTrans, isDead);

                    if (bg != null)
                    {
                        // 현재 턴인 캐릭터 강조 (선택적)
                        bool isCurrentTurn = !battleEnded && currentControlledMember == member && currentPhase == BattlePhase.Command;
                        bg.color = isCurrentTurn ? activeSlotColor : inactiveSlotColor;
                    }

                    UpdatePlayerStatusIcons(i, member);
                }
                else
                {
                    tmp.text = "";
                    if (i < playerStatusNameTexts.Count && playerStatusNameTexts[i] != null)
                        playerStatusNameTexts[i].text = "";
                    if (i < playerStatusHPTexts.Count && playerStatusHPTexts[i] != null)
                        playerStatusHPTexts[i].text = "";
                    if (i < playerStatusMPTexts.Count && playerStatusMPTexts[i] != null)
                        playerStatusMPTexts[i].text = "";
                    if (bg != null) bg.color = inactiveSlotColor;
                    SetDeathSkull(innerBgTrans, false);
                    UpdatePlayerStatusIcons(i, null);
                }
            }
        }
        else
        {
            List<string> partyLines = new List<string>(activePartyMembers.Count);
            for (int i = 0; i < activePartyMembers.Count; i++)
            {
                var member = activePartyMembers[i];
                if (member == null) continue;
                partyLines.Add($"{member.playerName}  HP {member.currentHP}/{member.maxHP}  MP {member.currentMP}/{member.maxMP}");
            }

            if (playerHPText != null)
            {
                playerHPText.text = partyLines.Count > 0 ? string.Join("\n", partyLines) : heroHpLine;
            }
            if (playerMPText != null)
            {
                playerMPText.text = heroMpLine;
            }
        }

        var t = GetCurrentTarget();
        if (t != null)
        {
            if (enemyHPText != null) enemyHPText.text = $"HP: {t.currentHP} / {t.maxHP}";
            if (enemyMPText != null) enemyMPText.text = $"MP: {t.currentMP} / {t.maxMP}";
        }

        bool hasEnemyBarPanel =
            enemyStatusSlots.Count > 0 &&
            enemyStatusNameTexts.Count > 0 &&
            enemyStatusHPTexts.Count > 0 &&
            enemyStatusMPTexts.Count > 0;

        // 기존 하이라키 EnemyBar 업데이트
        if (hasEnemyBarPanel)
        {
            Color enemyTargetColor = new Color(1f, 1f, 1f, 1f);
            Color enemyInactiveColor = new Color(1f, 1f, 1f, 0.7f);
            Color enemyDownColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
            Color emptySlotColor = new Color(1f, 1f, 1f, 0.2f);

            for (int i = 0; i < enemyStatusSlots.Count; i++)
            {
                int slotNumber = i + 1; // UI 슬롯 번호 (1~4)
                EnemyStats es = enemyLine.GetAt(slotNumber) as EnemyStats;
                bool hasEnemy = es != null && !es.IsDead();

                Image frame = i < enemyStatusFrames.Count ? enemyStatusFrames[i] : null;
                Image monsterImage = i < enemyStatusMonsterImages.Count ? enemyStatusMonsterImages[i] : null;
                TextMeshProUGUI nameText = i < enemyStatusNameTexts.Count ? enemyStatusNameTexts[i] : null;
                TextMeshProUGUI hpText = i < enemyStatusHPTexts.Count ? enemyStatusHPTexts[i] : null;
                TextMeshProUGUI mpText = i < enemyStatusMPTexts.Count ? enemyStatusMPTexts[i] : null;

                if (hasEnemy)
                {
                    bool isDead = es.currentHP <= 0;
                    bool isCurrentTarget = enemy == es && !isDead;
                    bool shouldPulse = !isDead && waitingForTargetSelection && hoveredEnemy == es;

                    if (lastEnemyDisplayedHP.TryGetValue(es, out int previousHP))
                    {
                        if (es.currentHP < previousHP)
                            TriggerEnemySlotShake(i);
                    }
                    lastEnemyDisplayedHP[es] = es.currentHP;

                    if (nameText != null) nameText.text = es.enemyName;
                    if (hpText != null) hpText.text = $"HP {es.currentHP}/{es.maxHP}";
                    if (mpText != null) mpText.text = $"MP {es.currentMP}/{es.maxMP}";

                    if (nameText != null) nameText.color = isDead ? enemyDownColor : Color.white;
                    if (hpText != null) hpText.color = isDead ? enemyDownColor : Color.white;
                    if (mpText != null) mpText.color = isDead ? enemyDownColor : Color.white;

                    if (monsterImage != null)
                    {
                        SpriteRenderer sr = es.GetComponent<SpriteRenderer>();
                        monsterImage.sprite = sr != null ? sr.sprite : null;
                        monsterImage.enabled = monsterImage.sprite != null;
                        monsterImage.color = isDead ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                        monsterImage.preserveAspect = true;
                    }

                    if (frame != null)
                    {
                        frame.color = isDead
                            ? enemyDownColor
                            : (isCurrentTarget ? enemyTargetColor : enemyInactiveColor);
                    }

                    UpdateEnemyStatusIcons(i, es);
                    SetEnemySlotPulse(i, shouldPulse);

                    es.UpdateStatusUI();
                }
                else
                {
                    if (nameText != null) nameText.text = "";
                    if (hpText != null) hpText.text = "";
                    if (mpText != null) mpText.text = "";
                    if (monsterImage != null)
                    {
                        monsterImage.sprite = null;
                        monsterImage.enabled = false;
                        monsterImage.color = Color.white;
                    }
                    if (frame != null) frame.color = emptySlotColor;

                    UpdateEnemyStatusIcons(i, null);
                    SetEnemySlotPulse(i, false);
                }
            }
        }
        // 동적 패널 업데이트 (기존 폴백)
        else if (enemyStatusPanel != null)
        {
            int childCount = enemyStatusPanel.childCount;
            Color enemyActiveColor = new Color(1f, 1f, 1f, 0.22f);
            Color enemyInactiveColor = new Color(1f, 1f, 1f, 0.08f);
            Color enemyDownColor = new Color(0.8f, 0.6f, 0.6f, 1f);

            for (int i = 0; i < childCount && i < activeEnemies.Count; i++)
            {
                var es = activeEnemies[i];
                if (es == null) continue;
                Transform slot = enemyStatusPanel.GetChild(i);
                var text = slot.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{es.enemyName}  HP {es.currentHP}/{es.maxHP}  MP {es.currentMP}/{es.maxMP}";
                    text.color = es.currentHP > 0 ? Color.white : enemyDownColor;
                }

                var bg = slot.GetComponent<Image>();
                if (bg != null)
                {
                    bool isCurrentTarget = enemy == es && es.currentHP > 0;
                    bg.color = isCurrentTarget ? enemyActiveColor : enemyInactiveColor;
                }
            }
        }

        // 월드 스페이스 UI 업데이트
        if (!usingEnemyBarPortraitUI)
        {
            foreach (var es in activeEnemies)
            {
                if (es != null)
                {
                    es.SetWorldSpaceStatusUIEnabled(true);
                    SpriteRenderer worldSprite = es.GetComponent<SpriteRenderer>();
                    if (worldSprite != null && es.currentHP > 0)
                    {
                        worldSprite.enabled = true;
                    }
                    es.UpdateStatusUI();
                }
            }
        }

        // SerializeField 파티/적 UI 동기화
        UpdatePartyUI();
    }

    // ========== 파티/적 진영 SerializeField UI 업데이트 ==========
    private void UpdatePartyUI()
    {
        // ── 아군 슬롯 ──
        if (partySlots != null)
        {
            for (int i = 0; i < partySlots.Length; i++)
            {
                bool hasMember = i < activePartyMembers.Count && activePartyMembers[i] != null;

                if (partySlots[i] != null)
                    partySlots[i].SetActive(hasMember);

                if (!hasMember) continue;

                PlayerStats m = activePartyMembers[i];
                bool isDead = m.currentHP <= 0;
                Color textColor = isDead ? new Color(1f, 0.5f, 0.5f, 1f) : Color.white;

                if (partyNameTexts != null && i < partyNameTexts.Length && partyNameTexts[i] != null)
                {
                    partyNameTexts[i].text = m.playerName;
                    partyNameTexts[i].color = textColor;
                }

                if (partyHPBars != null && i < partyHPBars.Length && partyHPBars[i] != null)
                    partyHPBars[i].fillAmount = m.maxHP > 0 ? Mathf.Clamp01((float)m.currentHP / m.maxHP) : 0f;

                if (partyHPTexts != null && i < partyHPTexts.Length && partyHPTexts[i] != null)
                {
                    partyHPTexts[i].text = $"{m.currentHP}/{m.maxHP}";
                    partyHPTexts[i].color = textColor;
                }

                if (partyMPBars != null && i < partyMPBars.Length && partyMPBars[i] != null)
                    partyMPBars[i].fillAmount = m.maxMP > 0 ? Mathf.Clamp01((float)m.currentMP / m.maxMP) : 0f;

                if (partyMPTexts != null && i < partyMPTexts.Length && partyMPTexts[i] != null)
                {
                    partyMPTexts[i].text = $"{m.currentMP}/{m.maxMP}";
                    partyMPTexts[i].color = textColor;
                }

                if (partyPortraitImages != null && i < partyPortraitImages.Length && partyPortraitImages[i] != null)
                {
                    Sprite portrait = m.IsRecruitedCompanion && m.companionSource != null
                        ? m.companionSource.sprite
                        : null;
                    partyPortraitImages[i].sprite = portrait;
                    partyPortraitImages[i].enabled = portrait != null;
                    partyPortraitImages[i].color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                }

                // 상태이상 아이콘 행
                if (partyStatusIconRows != null && i < partyStatusIconRows.Length && partyStatusIconRows[i] != null)
                {
                    Image[] icons = partyStatusIconRows[i].GetComponentsInChildren<Image>(true);
                    var effects = m.activeStatusEffects;
                    for (int k = 0; k < icons.Length; k++)
                    {
                        if (k < effects.Count && effects[k] != null && effects[k].data != null)
                        {
                            icons[k].sprite = effects[k].data.flatIcon;
                            icons[k].gameObject.SetActive(true);
                        }
                        else
                        {
                            icons[k].gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        // currentSlot 기준으로 이름/이미지/아이콘 업데이트
        foreach (var es in activeEnemies)
        {
            if (es == null) continue;
            int slotIndex = (int)es.currentSlot - 1;
            if (slotIndex < 0) continue;

            if (enemyNameTexts != null && slotIndex < enemyNameTexts.Length && enemyNameTexts[slotIndex] != null)
                enemyNameTexts[slotIndex].text = es.enemyName;

            if (enemyMonsterImages != null && slotIndex < enemyMonsterImages.Length && enemyMonsterImages[slotIndex] != null)
            {
                SpriteRenderer sr = es.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    enemyMonsterImages[slotIndex].sprite = sr.sprite;
                    enemyMonsterImages[slotIndex].color = es.currentHP > 0 ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.65f);
                }
            }

            // 상태이상 아이콘 행
            if (enemyStatusIconRows != null && slotIndex < enemyStatusIconRows.Length && enemyStatusIconRows[slotIndex] != null)
            {
                Image[] icons = enemyStatusIconRows[slotIndex].GetComponentsInChildren<Image>(true);
                var effects = es.activeStatusEffects;
                for (int k = 0; k < icons.Length; k++)
                {
                    if (k < effects.Count && effects[k] != null && effects[k].data != null)
                    {
                        icons[k].sprite = effects[k].data.flatIcon;
                        icons[k].gameObject.SetActive(true);
                    }
                    else
                    {
                        icons[k].gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    // 전투 시작 시 모든 파티 멤버의 HP/MP를 최대값으로 초기화
    private void InitializePartyHPMP()
    {
        // 주인공 HP/MP 초기화
        if (player != null)
        {
            player.currentHP = player.maxHP;
            player.currentMP = player.maxMP;
            Debug.Log($"[BattleManager] Hero HP/MP initialized: {player.currentHP}/{player.maxHP} HP, {player.currentMP}/{player.maxMP} MP");
        }
        
        // 모든 파티 멤버의 HP/MP 초기화
        foreach (var member in activePartyMembers)
        {
            if (member != null && member != player) // 주인공은 이미 초기화했으므로 제외
            {
                member.currentHP = member.maxHP;
                member.currentMP = member.maxMP;
                Debug.Log($"[BattleManager] {member.playerName} HP/MP initialized: {member.currentHP}/{member.maxHP} HP, {member.currentMP}/{member.maxMP} MP");
            }
        }
        
        // UI 업데이트
        UpdateStatusUI();
    }
    
    private void SyncPotionCount()
    {
        if (ConsumableInventory.Instance != null && selectedConsumableItem != null)
            potionCount = ConsumableInventory.Instance.GetQuantity(selectedConsumableItem);
    }

    private bool HasPotionAvailable()
    {
        if (ConsumableInventory.Instance != null && selectedConsumableItem != null)
            return ConsumableInventory.Instance.HasItem(selectedConsumableItem);
        return potionCount > 0;
    }

    private void UpdatePotionUI()
    {
        SyncPotionCount();
        if (potionCountText != null)
            potionCountText.text = $"Potions: {potionCount}";
    }



    // -------------------- 전투 종료 체크 --------------------
    private void CheckBattleEnd()
    {
        // [DUPLICATE GUARD 2026-05-11] 이미 종료된 전투는 다시 처리하지 않음
        // CheckBattleEnd가 같은 프레임/턴에 여러 곳에서 호출되어 Victory 보상 중복 지급 방지
        string _diagCaller = "?";
#if UNITY_EDITOR
        try { _diagCaller = new System.Diagnostics.StackTrace(1).GetFrame(0)?.GetMethod()?.Name ?? "?"; }
        catch { _diagCaller = "?"; }
#endif
        Debug.Log($"[BM:DIAG] CheckBattleEnd ENTRY | battleEnded={battleEnded} | caller={_diagCaller}");

        if (battleEnded)
        {
            Debug.Log($"[BM:DIAG] CheckBattleEnd SKIPPED (already ended) | caller={_diagCaller}");
            return;
        }

        if (!AnyPartyAlive())
        {
            // ★ battleEnded를 분기 시작 즉시 설정 — 분기 끝에서 설정하면 그 사이 재진입 가능
            battleEnded = true;

            if (player != null && player.currentHP < 0) player.currentHP = 0;
            UpdateStatusUI();
            AddMessage("Party was defeated...");
            if (actionPanel != null) actionPanel.SetActive(false);
            if (skillPanel != null) skillPanel.SetActive(false);
            StartCoroutine(GameOverRoutine(3.0f));
            return;
        }
        else if (AllEnemiesDefeated())
        {
            // ★ battleEnded를 분기 시작 즉시 설정 — AddExp/Save 전에 가드 활성화
            battleEnded = true;

            UpdateStatusUI();
            AddMessage("All enemies defeated!");

            // [2026-05-24] 영입 굴림은 ReturnToDungeonRoutine으로 이동.
            // 사용자 YES/NO 다이얼로그 + 메시지 시퀀스가 맵 복귀 전 단계에 통합됨.

            // [Anti-Gravity] 경험치 정산 로직
            int totalExp = 0;
            foreach (var e in activeEnemies)
            {
                if (e != null) totalExp += e.expReward;
            }

            if (totalExp > 0)
            {
                AddMessage($"Victory! Party gained {totalExp} EXP!");
                Debug.Log($"[BM:DIAG] Victory EXP distribution | totalExp={totalExp} | activePartyMembers.Count={activePartyMembers.Count}");
                foreach (var member in activePartyMembers)
                {
                    if (member != null && member.currentHP > 0)
                    {
                        Debug.Log($"[BM:DIAG] AddExp ABOUT TO CALL | member='{member.playerName}', InstanceID={member.GetInstanceID()}, Before EXP={member.exp}");
                        member.AddExp(totalExp);
                        Debug.Log($"[BM:DIAG] AddExp returned | member='{member.playerName}', After EXP={member.exp}, Lv={member.level}");
                    }
                    else
                    {
                        Debug.LogWarning($"[BM:DIAG] AddExp SKIPPED | member={(member == null ? "NULL" : member.playerName)}, currentHP={(member != null ? member.currentHP.ToString() : "?")} (dead or null)");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[BM:DIAG] Victory but totalExp=0 — AddExp not called");
            }

            if (actionPanel != null) actionPanel.SetActive(false);
            if (skillPanel != null) skillPanel.SetActive(false);
            ReturnToDungeon(3.0f);
        }
    }

    private bool AnyPartyAlive()
    {
        Debug.Log("[BattleManager] Checking if any party member is alive.");
        foreach (var member in activePartyMembers)
        {
            if (member != null && member.currentHP > 0) return true;
        }
        return false;
    }

    private bool AllEnemiesDefeated()
    {
        foreach (var e in activeEnemies)
        {
            if (e != null && e.currentHP > 0) return false;
        }
        return activeEnemies.Count > 0;
    }
}


