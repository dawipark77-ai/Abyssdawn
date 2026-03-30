using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using AbyssdawnBattle;
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
    public Button runButton;
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
    public bool playerTurn = true;

    [Header("RPG Percent Settings")]
    [Range(0, 100)] public float criticalChance = 25f; // 크리티컬 확률
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

    public enum PartyRole { Hero, Warrior, Rogue, Wizard }
    private PartyRole currentControlledRole = PartyRole.Hero;

    private List<PlayerStats> activePartyMembers = new List<PlayerStats>();
    private Dictionary<PartyRole, PlayerStats> allyInstances = new Dictionary<PartyRole, PlayerStats>();
    private PlayerStats currentControlledMember;

    private List<TextMeshProUGUI> playerStatusTexts = new List<TextMeshProUGUI>();
    private List<Image> playerStatusBackgrounds = new List<Image>();
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
            agility = p.Agility;
            isPlayer = true;
        }

        public BattleActor(EnemyStats e)
        {
            player = null;
            enemy = e;
            agility = e.Agility;
            isPlayer = false;
        }
    }

    private List<BattleActor> turnOrder = new List<BattleActor>();
    private bool turnInProgress = false;

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
    public EnemyDatabase enemyDatabase;
    public Transform spawnCenter;
    public Transform worldRoot;
    public Canvas canvas;
    public Transform enemyStatusPanel;
    public float spawnOffset = 1f;
    public float extraSpacingPerEnemy = 0.25f;

    private List<EnemyStats> activeEnemies = new List<EnemyStats>();
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

        // 1. 스킬 버튼 배치 (최대 7개)
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            
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
            // 버튼 배경은 어두운 색으로 유지
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            
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
            // 스킬 이름과 코스트를 줄바꿈으로 분리, 코스트는 더 작은 글씨로
            btnText.text = $"{skill.skillName}\n<size=16><color=#AAAAAA>{costText}</color></size>";
            // 메인 폰트 크기를 25pt로 조정
            btnText.fontSize = Mathf.Min(25, btnHeight * 0.55f); 
            btnText.alignment = TMPro.TextAlignmentOptions.Left;
            btnText.color = Color.white;
            
            // 리스너 추가
            SkillData capturedSkill = skill;
            btn.onClick.AddListener(() =>
            {
                UsePlayerSkill(capturedSkill);
            });
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
        startWithFullParty = false; // [Anti-Gravity] 강제 Solo 모드 설정 (인스펙터 값 무시)
        ForceDisableUIPanels();
        
        // EnemyDatabase 미리 로드 시도
        LoadEnemyDatabase();

        // 페이지네이션 UI 자동 연결 시도
        TryAutoAssignPaginationUI();
    }
    
    /// <summary>
    /// EnemyDatabase를 자동으로 로드하는 메서드
    /// </summary>
    void LoadEnemyDatabase()
    {
        if (enemyDatabase != null)
        {
            Debug.Log($"[BattleManager] EnemyDatabase already assigned: {enemyDatabase.name}");
            return;
        }
        
        Debug.Log("[BattleManager] EnemyDatabase not assigned. Attempting to auto-load...");
        
        // 방법 1: Editor 모드에서 AssetDatabase로 직접 찾기 (가장 확실한 방법)
        #if UNITY_EDITOR
        try
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:EnemyDatabase");
            Debug.Log($"[BattleManager] AssetDatabase.FindAssets found {guids.Length} EnemyDatabase asset(s)");
            
            if (guids.Length > 0)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    Debug.Log($"[BattleManager] Trying to load EnemyDatabase from path: {path}");
                    
                    EnemyDatabase db = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDatabase>(path);
                    if (db != null)
                    {
                        Debug.Log($"[BattleManager] ✓ Found EnemyDatabase via AssetDatabase: {path}");
                        Debug.Log($"[BattleManager] EnemyDatabase name: {db.name}, prefab count: {db.enemyPrefabs?.Count ?? 0}");
                        
                        // Editor 모드에서는 직접 할당 (플레이 모드에서도 작동)
                        enemyDatabase = db;
                        
                        // Resources 폴더에 있으면 Resources 경로로도 로드 시도 (확인용)
                        if (path.Contains("Resources"))
                        {
                            string resourcesPath = path.Substring(path.IndexOf("Resources/") + 10); // "Resources/" 이후 경로
                            resourcesPath = resourcesPath.Replace(".asset", ""); // 확장자 제거
                            Debug.Log($"[BattleManager] Resources path would be: {resourcesPath}");
                            
                            EnemyDatabase resourcesDb = Resources.Load<EnemyDatabase>(resourcesPath);
                            if (resourcesDb != null)
                            {
                                Debug.Log($"[BattleManager] ✓ Resources.Load also works: {resourcesPath}");
                            }
                            else
                            {
                                Debug.LogWarning($"[BattleManager] Resources.Load failed for path: {resourcesPath}");
                            }
                        }
                        
                        Debug.Log($"[BattleManager] ✓ EnemyDatabase assigned: {enemyDatabase.name}, prefabs: {enemyDatabase.enemyPrefabs?.Count ?? 0}");
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"[BattleManager] LoadAssetAtPath returned null for path: {path}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[BattleManager] AssetDatabase: No EnemyDatabase assets found in project!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BattleManager] Exception in Editor mode load: {ex.Message}\n{ex.StackTrace}");
        }
        #endif
        
        // 방법 2: 기본 Resources 경로로 로드 (빌드 시 사용)
        enemyDatabase = Resources.Load<EnemyDatabase>("EnemyDatabase");
        if (enemyDatabase != null)
        {
            Debug.Log($"[BattleManager] Successfully loaded EnemyDatabase from Resources: {enemyDatabase.name}");
            return;
        }
        else
        {
            Debug.LogWarning("[BattleManager] Resources.Load<EnemyDatabase>(\"EnemyDatabase\") returned null");
        }
        
        // 방법 3: Resources 폴더 전체에서 검색
        EnemyDatabase[] allDatabases = Resources.LoadAll<EnemyDatabase>("");
        if (allDatabases != null && allDatabases.Length > 0)
        {
            enemyDatabase = allDatabases[0];
            Debug.Log($"[BattleManager] Found EnemyDatabase via LoadAll: {enemyDatabase.name} (found {allDatabases.Length} total)");
            return;
        }
        else
        {
            Debug.LogWarning("[BattleManager] Resources.LoadAll<EnemyDatabase> returned null or empty");
        }
        
        Debug.LogError("[BattleManager] Failed to load EnemyDatabase! Please assign it manually in Inspector.");
    }

    void OnEnable()
    {
        ForceDisableUIPanels();
    }

    void Start()
    {
        ForceDisableUIPanels();
        StartCoroutine(EnsureUIPanelsClosedAfterFrames(3));

        // 버튼 리스너 초기화
        if (attackButton != null) attackButton.onClick.RemoveAllListeners();
        if (skillButton != null) skillButton.onClick.RemoveAllListeners();
        if (skillBackButton != null) skillBackButton.onClick.RemoveAllListeners();
        if (itemButton != null) itemButton.onClick.RemoveAllListeners();
        if (runButton != null) runButton.onClick.RemoveAllListeners();
        if (defendButton != null) defendButton.onClick.RemoveAllListeners();

        // Back 버튼 찾기 (Start에서도 찾기)
        FindAndConnectBackButton();

        // 페이지네이션 UI 자동 연결 시도
        TryAutoAssignPaginationUI();

        // 버튼 연결
        if (attackButton != null) attackButton.onClick.AddListener(OnAttackButton);
        if (skillButton != null) skillButton.onClick.AddListener(OnSkillButton);
        if (skillBackButton != null) skillBackButton.onClick.AddListener(OnSkillBack);
        if (itemButton != null) itemButton.onClick.AddListener(OnItemButton);
        if (runButton != null) runButton.onClick.AddListener(OnRunButton);
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
        foreach (var member in activePartyMembers)
        {
            if (member != null) 
            {
                Debug.Log("[PERSISTENCE_DEBUG] ReturnToDungeonRoutine: Saving " + member.playerName + " - HP: " + member.currentHP + "/" + member.maxHP);
                gm.SaveFromPlayer(member);
            }
        }

        yield return new WaitForSeconds(delay);
        
        string sceneToLoad = DungeonEncounter.lastDungeonScene;
        DungeonEncounter.justReturnedFromBattle = true;
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("[BattleManager] No last dungeon scene saved. Returning to 0.");
            SceneManager.LoadScene(0);
        }
        else
        {
            Debug.Log("[BattleManager] Returning to dungeon: " + sceneToLoad);
            SceneManager.LoadScene(sceneToLoad);
        }
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

        // GameManager 파티 데이터 초기화
        GameManager.EnsureInstance().ClearAllData();

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
            // 솔로 모드: Hero만 활성화
            activePartyMembers.Clear();
            activePartyMembers.Add(player);

            // 동료들 비활성화
            foreach (var kvp in allyInstances)
            {
                if (kvp.Value != null && kvp.Value != player)
                {
                    EnsureVisualForPartyMember(kvp.Value, false);
                }
            }
        }
        else
        {
            // 풀 파티 모드: Hero + 3명 동료
            activePartyMembers.Clear();
            activePartyMembers.Add(player);

            // Warrior
            var warrior = GetOrCreateAlly(PartyRole.Warrior);
            activePartyMembers.Add(warrior);

            // Rogue
            var rogue = GetOrCreateAlly(PartyRole.Rogue);
            activePartyMembers.Add(rogue);

            // Wizard
            var wizard = GetOrCreateAlly(PartyRole.Wizard);
            activePartyMembers.Add(wizard);

            // 동료들 위치 설정
            PositionPartyMembers();
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

        // Create a runtime PlayerStatData SO for this ally
        allyStats.statData = ScriptableObject.CreateInstance<PlayerStatData>();
        allyStats.statData.level = 1;
        allyStats.statData.exp = 0;

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
        Debug.Log("[BattleManager] StartBattle() called.");
        // 전투 상태 초기화
        battleEnded = false;
        currentPhase = BattlePhase.Command;
        playerTurn = false;
        turnInProgress = false;
        
        ForceDisableUIPanels();
        if (battleUIManager != null)
        {
            battleUIManager.ForceCloseMenus();
            // 전투 시작 시 메인 메뉴 표시
            battleUIManager.ShowMainMenu();
        }

        ClearSpawnedEnemies();
        activeEnemies.Clear();
        enemy = null;
        waitingForTargetSelection = false;
        pendingAction = "";
        pendingSkill = null;
        hoveredEnemy = null;

        // [중요] 먼저 GameManager에서 HP/MP 로드 (파티 구성 전에 로드하여 덮어쓰지 않도록)
        InitializeParty();
        
        // 파티 구성 보정 (InitializeParty 이후에 호출하여 로드된 HP/MP가 보존되도록)
        var desiredMode = startWithFullParty ? PartyMode.Full : PartyMode.Solo;
        ApplyPartyMode(desiredMode, force: true, restartCommands: false);
        
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

        // Awake()에서 이미 로드 시도했지만, 혹시 모를 경우를 대비해 다시 시도
        if (enemyDatabase == null)
        {
            Debug.LogWarning("[BattleManager] EnemyDatabase is still null in StartBattle(). Attempting to load again...");
            LoadEnemyDatabase();
        }

        // 적 스폰
        if (enemyDatabase == null)
        {
            Debug.LogError("[BattleManager] EnemyDatabase is null! Cannot spawn enemies.\n" +
                "Please assign EnemyDatabase in BattleManager Inspector or ensure it exists in Resources folder.");
            AddMessage("Error: Enemy Database not found! Please assign in Inspector.");
            ReturnToDungeon(3.0f); // Return to dungeon if we can't start battle
            return;
        }

        // EnemyDatabase 상태 확인
        if (enemyDatabase.enemyPrefabs == null || enemyDatabase.enemyPrefabs.Count == 0)
        {
            Debug.LogError($"[BattleManager] EnemyDatabase.enemyPrefabs is null or empty! (Count: {enemyDatabase.enemyPrefabs?.Count ?? 0})");
            AddMessage("Error: EnemyDatabase has no enemy prefabs assigned!");
            return;
        }

        // null 프리팹 체크
        int nullPrefabCount = 0;
        for (int i = 0; i < enemyDatabase.enemyPrefabs.Count; i++)
        {
            if (enemyDatabase.enemyPrefabs[i] == null)
            {
                nullPrefabCount++;
            }
        }
        if (nullPrefabCount > 0)
        {
            Debug.LogWarning($"[BattleManager] EnemyDatabase has {nullPrefabCount} null prefab(s) out of {enemyDatabase.enemyPrefabs.Count} total.");
        }

        // [User Request] 층별 적 등장 수 확률 조정
        int currentFloor = DungeonPersistentData.currentFloor;
        int count = 1;
        
        float roll = UnityEngine.Random.value; // 0.0 ~ 1.0

        if (currentFloor == 1)
        {
            // 1층: 1마리(90%), 2마리(10%)
            if (roll < 0.9f) count = 1;
            else count = 2;
        }
        else if (currentFloor == 2)
        {
            // 2층: 1마리(70%), 2마리(30%)
            if (roll < 0.7f) count = 1;
            else count = 2;
        }
        else if (currentFloor == 3)
        {
            // 3층: 1마리(50%), 2마리(50%)
            if (roll < 0.5f) count = 1;
            else count = 2;
        }
        else
        {
             // 4층 이상: 기본 랜덤 (1~3마리)
             count = UnityEngine.Random.Range(1, 4);
        }
        
        Debug.Log($"[BattleManager] Floor {currentFloor} Spawn Roll: {roll:F2} -> Count: {count}");

        // var picks = enemyDatabase.GetRandomEnemies(count);
        
        // 고블린 프리팹 찾기 (현재는 고블린만 나옴)
        var goblinPrefab = enemyDatabase.enemyPrefabs.Find(x => x.name.Contains("Goblin"));
        List<GameObject> picks = new List<GameObject>();
        if (goblinPrefab != null)
        {
            for (int k = 0; k < count; k++) picks.Add(goblinPrefab);
        }
        else
        {
            // 고블린이 없으면 그냥 랜덤 (오류 방지)
            picks = enemyDatabase.GetRandomEnemies(count);
        }

        Debug.Log($"[BattleManager] Requested {count} enemies, got {picks?.Count ?? 0} from database (Total prefabs: {enemyDatabase.enemyPrefabs.Count})");

        if (picks == null || picks.Count == 0)
        {
            Debug.LogError("[BattleManager] EnemyDatabase.GetRandomEnemies returned null or empty list!");
            AddMessage("Error: No enemy prefabs in database!");
            return;
        }

        // 적 정렬 (방어력/HP 기준, 탱커가 왼쪽)
        picks = picks.OrderByDescending(go => ScoreEnemyPrefab(go)).ToList();

        Vector3 center = spawnCenter != null ? spawnCenter.position : Vector3.zero;

        // Inspector에서 조절 가능한 간격 (spawnOffset + extraSpacingPerEnemy) 반영
        float spacing = spawnOffset;
        if (picks.Count > 1)
        {
            spacing += extraSpacingPerEnemy * (picks.Count - 1);
        }
        spacing = Mathf.Max(0.1f, spacing);

        // 적들의 Y 좌표를 통일 (발끝이 같은 선상에 위치)
        float baseY = center.y;
        List<GameObject> spawnedEnemies = new List<GameObject>();
        List<EnemyStats> spawnedEnemyStats = new List<EnemyStats>();

        // 1단계: 모든 적 스폰
        int spawnedCount = 0;
        int skippedNullCount = 0;
        for (int i = 0; i < picks.Count; i++)
        {
            try
            {
                GameObject enemyPrefab = picks[i];
                if (enemyPrefab == null)
                {
                    skippedNullCount++;
                    Debug.LogWarning($"[BattleManager] Skipping null prefab at index {i}");
                    continue;
                }

                float offset = (i - (picks.Count - 1) / 2f) * spacing;
                Vector3 spawnPos = new Vector3(center.x + offset, baseY, center.z);

                GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                if (enemyObj == null)
                {
                    Debug.LogError($"[BattleManager] Instantiate failed for prefab: {enemyPrefab.name}");
                    continue;
                }

                if (worldRoot != null)
                {
                    enemyObj.transform.SetParent(worldRoot, false);
                }

                EnemyStats enemyStats = enemyObj.GetComponent<EnemyStats>();
                if (enemyStats == null)
                {
                    enemyStats = enemyObj.AddComponent<EnemyStats>();
                }

                spawnedEnemies.Add(enemyObj);
                spawnedEnemyStats.Add(enemyStats);
                spawnedCount++;
                Debug.Log($"[BattleManager] Successfully spawned enemy {spawnedCount}: {enemyPrefab.name} at {spawnPos}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BattleManager] Exception while spawning enemy at index {i}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        Debug.Log($"[BattleManager] Spawn summary: {spawnedCount} spawned, {skippedNullCount} skipped (null prefabs)");

        // 4단계: activeEnemies에 추가
        foreach (EnemyStats enemyStats in spawnedEnemyStats)
        {
            activeEnemies.Add(enemyStats);

            // 적을 Recorder에 등록
            if (battleRecorder != null)
            {
                battleRecorder.RegisterTarget(enemyStats.transform);
            }
        }

        // 5단계: 모든 적의 발끝이 baseY에 오도록 위치 조정 (한 프레임 후 실행)
        if (spawnedEnemies.Count > 0)
        {
            StartCoroutine(AdjustEnemyPositions(spawnedEnemies, baseY));
        }

        if (activeEnemies.Count == 0)
        {
            Debug.LogError($"[BattleManager] No enemies spawned! Summary: {spawnedCount} spawned, {skippedNullCount} skipped. " +
                $"EnemyDatabase has {enemyDatabase.enemyPrefabs.Count} prefabs. Check console for details.");
            AddMessage("ERROR: No enemies spawned! Check console for details.");
            return;
        }

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
        for (int i = 0; i < activeEnemies.Count && i < 4; i++)
        {
            var e = activeEnemies[i];
            if (e == null) continue;
            int slotIndex = i + 1;
            e.currentSlot = (BattleSlot)slotIndex;
            enemyLine.AssignToSlot(e, slotIndex);
        }

        Debug.Log($"[BattleManager] BattleLine 초기화 완료. 플레이어: {playerLine}, 적: {enemyLine}");
    }

    private void RebuildEnemyStatusPanel()
    {
        if (enemyStatusPanel == null) return;

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

    private int ScoreEnemyPrefab(GameObject prefab)
    {
        EnemyStats stats = prefab.GetComponent<EnemyStats>();
        if (stats == null) return 0;
        return stats.defense * 10 + stats.maxHP;
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

        // 민첩 순으로 정렬 (내림차순)
        turnOrder = turnOrder.OrderByDescending(a => a.agility).ToList();
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
        
        // UI는 Fight 버튼을 눌러야만 나타남 (자동으로 활성화하지 않음)
        if (actionPanel != null) actionPanel.SetActive(false);
        if (skillPanel != null) skillPanel.SetActive(false);
        if (battleUIManager != null)
        {
            battleUIManager.ForceCloseMenus();
            // Command Phase 시작 시 메인 메뉴 표시
            battleUIManager.ShowMainMenu();
        }
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

        // 첫 번째 캐릭터가 아니면(이미 Fight를 선택한 상태라면) 자동으로 FightSubPanel 표시
        if (commandIndex > 0 && battleUIManager != null)
        {
            battleUIManager.ShowFightSubPanel();
        }

        AddMessage($"{currentControlledMember.playerName} is preparing an action.");
        UpdateStatusUI();
    }

    private void ConfigureActionUIForActor(PlayerStats actor)
    {
        Debug.Log($"[BattleManager] ConfigureActionUIForActor called with actor: {(actor != null ? actor.playerName : "null")}");
        
        bool isHero = actor != null && actor == player;
        PartyRole role = actor != null ? GetPartyRole(actor) : PartyRole.Hero;
        
        Debug.Log($"[BattleManager] isHero: {isHero}, role: {role}, player: {(player != null ? player.playerName : "null")}, actor == player: {(actor != null && player != null ? (actor == player).ToString() : "N/A")}");

        // 스킬 버튼 활성화 조건 (역할별 스킬 확인)
        bool hasSkills = false;
        if (actor != null)
        {
            PartyRole actorRole = GetPartyRole(actor);
            if (roleSkillCache.ContainsKey(actorRole))
            {
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
        
        if (runButton != null)
        {
            runButton.gameObject.SetActive(true);
            runButton.interactable = isHero;
            Debug.Log($"[BattleManager] Run button activated (interactable: {isHero})");
        }
        else
        {
            Debug.LogWarning("[BattleManager] runButton is null!");
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
        Debug.Log($"[BattleManager] RunButton: {(runButton != null ? $"Active={runButton.gameObject.activeSelf}, Interactable={runButton.interactable}, Visible={runButton.gameObject.activeInHierarchy}" : "NULL")}");
        
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
            AddMessage($"{cmd.actor.playerName} is stunned and cannot act!");
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
                        cmd.actor.Heal(cmd.itemHealAmount);
                        SyncPotionCount();
                        AddMessage($"{cmd.actor.playerName} used {cmd.usedItem.itemName} and recovered {cmd.itemHealAmount} HP!");
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
        string slotName = $"PartySlot_{warriorIndex}";
        Transform slotTransform = playerStatusPanel.transform.Find(slotName);
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

    private IEnumerator ExecuteEnemyTurn(EnemyStats enemy)
    {
        if (enemy == null || enemy.currentHP <= 0 || enemy.IsDead()) yield break;
        if (enemy.IsStunned())
        {
            AddMessage($"{enemy.enemyName} is stunned and cannot act!");
            yield break;
        }

        yield return new WaitForSeconds(actionDelay);

        // 적은 랜덤 파티 멤버 공격
        PlayerStats target = GetRandomAlivePartyMember();
        if (target != null && target.currentHP > 0)
        {
            if (CheckEvasion(target.Agility, enemy.Agility))
            {
                AddMessage($"{target.playerName} evaded {enemy.enemyName}'s attack!");
            }
            else
            {
                bool critical = CheckCritical(enemy.luck);

                // [DEBUG LOG] 적 공격 데미지 계산 전 스탯 확인
                Debug.Log($"[BattleLog] Enemy Attack: {enemy.attack}, Player Defense: {target.Defense} (base: {target.baseDefense} + bonus: {target.GetDefenseBonus()}), Critical: {critical}");

                int damage = CalculateDQDamage(enemy.attack, target.Defense, critical);

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

                target.TakeDamage(damage);
                AddMessage(critical ? $"{enemy.enemyName} critical hit! {target.playerName} took {damage} damage!" :
                                      $"{enemy.enemyName} attacked {target.playerName} and dealt {damage} damage!");
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
            if (member != null && member.activeStatusEffects.Count > 0 && member.currentHP > 0)
            {
                int hpBefore = member.currentHP;
                member.ProcessStatusEffectsEndOfTurn();
                if (member.currentHP < hpBefore)
                {
                    int damage = hpBefore - member.currentHP;
                    AddMessage($"{member.playerName}이(가) 상태이상으로 {damage} 피해를 입었습니다!");
                    ShakePlayerStatusUI(member);
                    anyCurseDamage = true;
                }
            }
        }

        // 적 상태이상 데미지 처리
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null && enemy.activeStatusEffects.Count > 0 && enemy.currentHP > 0 && !enemy.IsDead())
            {
                int hpBefore = enemy.currentHP;
                enemy.ProcessStatusEffectsEndOfTurn();
                if (enemy.currentHP < hpBefore)
                {
                    int damage = hpBefore - enemy.currentHP;
                    AddMessage($"{enemy.enemyName}이(가) 상태이상으로 {damage} 피해를 입었습니다!");
                    enemy.UpdateStatusUI();
                    anyCurseDamage = true;
                }
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
                    // UI 업데이트 (UI가 없으면 생성)
                    if (enemyStats.StatusUI == null)
                    {
                        enemyStats.CreateWorldSpaceUI();
                    }
                    else
                    {
                        enemyStats.UpdateStatusUI();
                    }
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

        if (CheckEvasion(target.Agility, attacker.Agility))
        {
            AddMessage($"{target.enemyName} evaded {attacker.playerName}!");
        }
        else
        {
            bool critical = CheckCritical(attacker.luck);

            // [DEBUG LOG] 데미지 계산 전 스탯 확인
            Debug.Log($"[BattleLog] Player Attack Stats - Base: {attacker.baseAttack}, Bonus: {attacker.GetAttackBonus()}, Final: {attacker.Attack}");
            Debug.Log($"[BattleLog] Enemy Defense: {target.defense}, Critical: {critical}");

            bool isDual = IsDualWielding(attacker);
            int shownTotal = 0;

            if (isDual)
            {
                // ───────── 쌍수 기본 공격 ─────────
                // 1) 한손 기준 깡뎀 합
                int singleBaseDamage = CalculateDQDamage(attacker.Attack, target.defense, false);
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

                    armorHit1 = Mathf.RoundToInt(armor1);
                    armorHit2 = Mathf.RoundToInt(armor2);
                }

                int hit1 = baseHit1 + armorHit1;
                int hit2 = baseHit2 + armorHit2;

                // 크리티컬이면 두 타 모두 동일 배율 적용
                if (critical)
                {
                    hit1 = Mathf.Max(1, Mathf.FloorToInt(hit1 * 1.5f));
                    hit2 = Mathf.Max(1, Mathf.FloorToInt(hit2 * 1.5f));
                }

                Debug.Log($"[BattleLog] Dual basic attack - base1:{baseHit1}, base2:{baseHit2}, armor1:{armorHit1}, armor2:{armorHit2}, f1:{f1:F2}, f2:{f2:F2}");

                int applied1 = target.TakeDamage(hit1, critical);
                int applied2 = 0;
                if (!target.IsDead())
                {
                    applied2 = target.TakeDamage(hit2, critical);
                }

                shownTotal = applied1 + applied2;
                AddMessage(critical
                    ? $"Critical! {attacker.playerName} struck twice for {shownTotal}!"
                    : $"{attacker.playerName} struck twice for {shownTotal} damage!");
            }
            else
            {
                // 한손/방패: fn = 1.0, 깡뎀 + 방어 파괴 (추가딜)
                int singleBase = CalculateDQDamage(attacker.Attack, target.defense, false);
                singleBase = Mathf.Max(singleBase, 1);

                float singleCoeff = GetArmorBreakCoefficient(attacker);
                int singleArmor = 0;
                if (singleCoeff > 0f && target.defense > 0)
                {
                    // fn = 1.0 (한손은 랜덤 배율 없음)
                    singleArmor = Mathf.RoundToInt(target.defense * singleCoeff);
                }

                if (critical)
                {
                    singleBase  = Mathf.Max(1, Mathf.FloorToInt(singleBase  * 1.5f));
                    singleArmor = Mathf.Max(0, Mathf.FloorToInt(singleArmor * 1.5f));
                }

                int singleTotal = singleBase + singleArmor;
                Debug.Log($"[BattleLog] Single-wield attack - base:{singleBase}, armorBreak:{singleArmor}, total:{singleTotal}");

                int applied1 = target.TakeDamage(singleBase, critical);
                int applied2 = 0;
                if (singleArmor > 0 && !target.IsDead())
                {
                    applied2 = target.TakeDamage(singleArmor, critical);
                }

                shownTotal = applied1 + applied2;
                AddMessage(critical
                    ? $"Critical! {attacker.playerName} dealt {shownTotal}!"
                    : $"{attacker.playerName} struck for {shownTotal} damage!");
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
        EquipmentManager em = attacker.GetComponent<EquipmentManager>();
        if (em == null) return;

        void CheckAndApply(EquipmentData weapon)
        {
            if (weapon == null || weapon.weaponCurses == null) return;
            foreach (var curse in weapon.weaponCurses)
            {
                if (curse == null) continue;
                bool applied = target.ApplyStatusEffect(curse);
                if (applied)
                {
                    AddMessage($"{weapon.equipmentName} inflicted {curse.effectType} on {target.enemyName}!");
                    Debug.Log($"[WeaponCurse] {weapon.equipmentName} → {curse.effectType} applied to {target.enemyName}");
                }
            }
        }

        CheckAndApply(em.rightHand);
        if (em.leftHand != em.rightHand)
            CheckAndApply(em.leftHand);
    }

    /// <summary>
    /// 방어자가 방패를 장착했고 블록 판정에 성공했는지 확인.
    /// 성공 시 피해 감소량을 out으로 반환합니다.
    /// </summary>
    private bool TryBlock(PlayerStats defender, out float damageReduction)
    {
        damageReduction = 0f;
        if (defender == null) return false;

        var em = defender.GetComponent<EquipmentManager>();
        if (em == null) return false;

        // 왼손(방패 슬롯) 또는 오른손에서 blockData 확인
        EquipmentData shield = em.leftHand ?? em.rightHand;
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
        var eq = attacker.GetComponent<EquipmentManager>();
        if (eq == null) return false;

        var right = eq.rightHand;
        var left = eq.leftHand;
        if (right == null || left == null) return false;

        // 둘 다 한손 무기이고, 양손 무기가 아니어야 함
        if (right.equipmentType != AbyssdawnBattle.EquipmentType.Hand) return false;
        if (left.equipmentType != AbyssdawnBattle.EquipmentType.Hand) return false;

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
        var eq = attacker.GetComponent<EquipmentManager>();
        if (eq == null) return 0f;

        float coeff = 0f;
        bool isDual = IsDualWielding(attacker);

        if (isDual)
        {
            if (eq.rightHand != null) coeff += eq.rightHand.GetArmorBreakCoefficient();
            if (eq.leftHand != null)  coeff += eq.leftHand.GetArmorBreakCoefficient();
        }
        else
        {
            if (eq.rightHand != null &&
                (eq.rightHand.equipmentType == AbyssdawnBattle.EquipmentType.Hand ||
                 eq.rightHand.equipmentType == AbyssdawnBattle.EquipmentType.TwoHanded))
            {
                coeff = eq.rightHand.GetArmorBreakCoefficient();
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
        var equipmentManager = attacker.GetComponent<EquipmentManager>();
        if (equipmentManager == null || equipmentManager.rightHand == null) return 0;

        var weapon = equipmentManager.rightHand;
        if (weapon.equipmentType != EquipmentType.Hand && weapon.equipmentType != EquipmentType.TwoHanded)
            return 0;

        float coeff = weapon.armorBreakCoefficient;
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
        if (target == null || skill == null || skill.Effects == null) return;

        foreach (var effect in skill.Effects)
        {
            if (effect == null || effect.statusEffect == null || effect.statusEffectChance <= 0f) continue;

            if (UnityEngine.Random.Range(0f, 100f) < effect.statusEffectChance)
            {
                bool applied = target.ApplyStatusEffect(effect.statusEffect);
                if (applied)
                    AddMessage($"{target.enemyName}에게 {effect.statusEffect.effectType} 상태이상 발생!");
            }
        }
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
            int hpCost = Mathf.FloorToInt(attacker.maxHP * (skill.hpCostPercent / 100f));
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

        // 스킬 사용 시 발동하는 패시브 처리 (Combat Breathing 등)
        ApplyOnSkillUsePassives(attacker, skill);

        Debug.Log("[BattleManager] ExecuteSkill finished. Updating status UI.");
        UpdateStatusUI();
        CheckBattleEnd();
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
            // 타격마다 명중률 체크
            float hitChance = CalculateHitChance(skill, attacker, target);
            float roll = UnityEngine.Random.Range(0f, 1f);
            
            if (roll >= hitChance)
            {
                // 회피됨
                evadedHits++;
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
            bool critical = CheckCritical(attacker.luck);
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

            // 크리티컬은 깡뎀 + 방어 파괴 합산에 배율 적용
            if (critical)
            {
                damage = Mathf.FloorToInt(damage * 1.5f);
            }

            damage = Mathf.Max(1, damage);

            Debug.Log($"[BattleLog] Skill Final Damage (with armor break): base={baseDamage}, armorBreak={armorBreakDamage}, total={damage}");

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
                attacker.ApplyStatusEffect(skill.selfStatusEffect);
                AddMessage($"{attacker.playerName}에게 {skill.selfStatusEffect.effectType} 역효과 발생!");
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

            float hitChance = CalculateHitChance(skill, attacker, target);
            float roll = UnityEngine.Random.Range(0f, 1f);

            if (roll >= hitChance)
            {
                AddMessage($"{target.enemyName} evaded {attacker.playerName}'s {skill.skillName}!");
                continue;
            }

            bool critical = CheckCritical(attacker.luck);
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

            int damage = Mathf.FloorToInt(baseStat * multiplier * scaleMultiplier);
            if (critical)
            {
                damage = Mathf.FloorToInt(damage * 1.5f);
            }
            damage = Mathf.Max(1, damage);

            // Sharp Edge 등 공격 패시브 보정
            damage = ApplyOffensivePassiveBonuses(attacker, skill, target, damage);

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

    // -------------------- Damage Calculation --------------------
    private int CalculateDQDamage(int atk, int def, bool isCritical)
    {
        float baseValue = (atk * 2f - def) / 2f;
        if (baseValue < 1f) baseValue = 1f;
        int damage = Mathf.FloorToInt(baseValue * UnityEngine.Random.Range(0.85f, 1.15f));
        if (isCritical) damage = Mathf.FloorToInt(damage * 1.5f);
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
    private bool CheckCritical(int luck)
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < criticalChance + luck;
    }

    private bool CheckEvasion(int targetAgility, int attackerAgility)
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < evasionChance + (targetAgility - attackerAgility) * 0.5f;
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

                // TODO: 전열/후열 시스템 정식 도입 시, PlayerStats.IsBackRow를 실제 위치 값으로 설정
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

    // -------------------- Hit Chance Calculation --------------------
    /// <summary>
    /// 명중률을 계산합니다.
    /// </summary>
    /// <param name="usedSkill">사용된 스킬 데이터</param>
    /// <param name="attacker">공격자 (PlayerStats)</param>
    /// <param name="defender">방어자 (PlayerStats 또는 EnemyStats)</param>
    /// <returns>명중 확률 (0.0 ~ 1.0)</returns>
    private float CalculateHitChance(SkillData usedSkill, PlayerStats attacker, PlayerStats defender)
    {
        // Step 1: 기본 명중률 가져오기
        float baseHit = usedSkill != null ? usedSkill.accuracy : 0.95f;

        // Step 2: AGI 보정 (장비 보정이 포함된 최종 AGI 사용)
        float attackerAGI = attacker.Agility; // 이미 GetEquipmentAgilityBonus()가 포함됨
        float defenderAGI = defender.Agility;
        float agiModifier = (attackerAGI * 1.5f) / (attackerAGI * 1.5f + defenderAGI);

        // Step 3: 최종 합산 (PassiveAccuracy + 장비 Accuracy 보정치 추가)
        float Luck = attacker.Luck;
        float passiveAccuracyBonus = attacker.GetPassiveAccuracyBonus();
        float itemAccuracyBonus = attacker.GetEquipmentAccuracyBonus(); // 장비 명중률 보정치
        float finalHitChance = baseHit * agiModifier + (Luck * 0.002f) + passiveAccuracyBonus + itemAccuracyBonus;

        // 최종값 제한
        return Mathf.Clamp(finalHitChance, 0.2f, 0.98f);
    }

    /// <summary>
    /// 명중률을 계산합니다 (EnemyStats 방어자용 오버로드).
    /// </summary>
    private float CalculateHitChance(SkillData usedSkill, PlayerStats attacker, EnemyStats defender)
    {
        // Step 1: 기본 명중률 가져오기
        float baseHit = usedSkill != null ? usedSkill.accuracy : 0.95f;

        // Step 2: AGI 보정 (장비 보정이 포함된 최종 AGI 사용)
        float attackerAGI = attacker.Agility; // 이미 GetEquipmentAgilityBonus()가 포함됨
        float defenderAGI = defender.Agility;
        float agiModifier = (attackerAGI * 1.5f) / (attackerAGI * 1.5f + defenderAGI);

        // Step 3: 최종 합산 (PassiveAccuracy + 장비 Accuracy 보정치 추가)
        float Luck = attacker.Luck;
        float passiveAccuracyBonus = attacker.GetPassiveAccuracyBonus();
        float itemAccuracyBonus = attacker.GetEquipmentAccuracyBonus(); // 장비 명중률 보정치
        float finalHitChance = baseHit * agiModifier + (Luck * 0.002f) + passiveAccuracyBonus + itemAccuracyBonus;

        // 최종값 제한
        return Mathf.Clamp(finalHitChance, 0.2f, 0.98f);
    }

    // -------------------- 메시지 --------------------
    private void AddMessage(string newMessage)
    {
        messageQueue.Enqueue(newMessage);
        if (messageQueue.Count > maxMessages) messageQueue.Dequeue();

        messageText.text = string.Join("\n", messageQueue.ToArray());
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
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

        // 기존 슬롯 제거
        foreach (Transform child in playerStatusPanel)
        {
            Destroy(child.gameObject);
        }
        playerStatusTexts.Clear();
        playerStatusBackgrounds.Clear();

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
        
        PartyRole role = GetPartyRole(currentControlledMember);
        if (roleSkillCache.ContainsKey(role))
        {
            return roleSkillCache[role];
        }
        
        return new List<SkillData>();
    }

    // ========== UI 업데이트 --------------------
    private void UpdateStatusUI()
    {
        string heroHpLine = player != null ? $"HP: {player.currentHP} / {player.maxHP}" : "HP: -";
        string heroMpLine = player != null ? $"MP: {player.currentMP} / {player.maxMP}" : "MP: -";

        bool hasPartyPanel = playerStatusPanel != null && playerStatusTexts.Count > 0;

        if (hasPartyPanel)
        {
            if (playerHPText != null) playerHPText.text = heroHpLine;
            if (playerMPText != null) playerMPText.text = heroMpLine;

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
                    // 드래곤 퀘스트 스타일: 이름, HP, MP (인덱스 제거)
                    tmp.text = $"{member.playerName}\nHP {member.currentHP}/{member.maxHP}\nMP {member.currentMP}/{member.maxMP}";
                    
                    bool isDead = member.currentHP <= 0;
                    tmp.color = isDead ? downedTextColor : aliveTextColor;

                    if (bg != null)
                    {
                        // 현재 턴인 캐릭터 강조 (선택적)
                        bool isCurrentTurn = !battleEnded && currentControlledMember == member && currentPhase == BattlePhase.Command;
                        bg.color = isCurrentTurn ? activeSlotColor : inactiveSlotColor;
                    }
                }
                else
                {
                    tmp.text = "";
                    if (bg != null) bg.color = inactiveSlotColor;
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

        // 동적 패널 업데이트 (적)
        if (enemyStatusPanel != null)
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
        foreach (var es in activeEnemies)
        {
            if (es != null)
            {
                es.UpdateStatusUI();
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
        if (!AnyPartyAlive())
        {
            if (player != null && player.currentHP < 0) player.currentHP = 0;
            UpdateStatusUI();
            AddMessage("Party was defeated...");
            if (actionPanel != null) actionPanel.SetActive(false);
            if (skillPanel != null) skillPanel.SetActive(false);
            battleEnded = true;
            StartCoroutine(GameOverRoutine(3.0f));
            return;
        }
        else if (AllEnemiesDefeated())
        {
            UpdateStatusUI();
            AddMessage("All enemies defeated!");

            // [Anti-Gravity] 경험치 정산 로직
            int totalExp = 0;
            foreach (var e in activeEnemies)
            {
                if (e != null) totalExp += e.expReward;
            }
            
            if (totalExp > 0)
            {
                AddMessage($"Victory! Party gained {totalExp} EXP!");
                foreach (var member in activePartyMembers)
                {
                    if (member != null && member.currentHP > 0)
                    {
                        member.AddExp(totalExp);
                    }
                }
            }

            if (actionPanel != null) actionPanel.SetActive(false);
            if (skillPanel != null) skillPanel.SetActive(false);
            battleEnded = true;
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


