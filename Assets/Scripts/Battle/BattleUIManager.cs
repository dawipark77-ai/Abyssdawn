using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class BattleUIManager : MonoBehaviour
{
    [Header("메인 메뉴 버튼 (FightPanel 안의 Attack / Skill / Item / Defend / Flee)")]
    public Button attackButton;
    public Button skillButton;
    public Button itemButton;
    public Button defendButton;
    public Button fleeButton;

    [Header("메인 패널")]
    public GameObject mainMenuPanel;

    [Header("서브 패널 (Skill/Item용 — 인스펙터에서 SubPanels 등 지정, 없으면 비워 둠)")]
    public GameObject fightSubPanel;
    [Tooltip("SubPanels/ItemPanel 오브젝트를 넣으세요. 비우면 SubPanels/ItemPanel 자동 검색. (FightPanel의 Item 버튼을 넣으면 안 됩니다)")]
    public GameObject battleItemPanel; // SubPanels/ItemPanel 등
    public Button attackSubButton;      // 레거시: 별도 서브 바에 둔 Attack (없으면 null)
    public Button skillSubButton;       // 레거시
    public Button itemSubButton;        // 레거시
    public Button defendSubButton;      // 레거시

    [Header("BattleManager Reference")]
    public BattleManager battleManager;

    void Awake()
    {
        Debug.Log("[BattleUIManager] Awake() called");
        EnsureEventSystem();
    }

    void Start()
    {
        Debug.Log("[BattleUIManager] Start() called");
        
        // BattleManager 자동 찾기 (할당되지 않은 경우)
        if (battleManager == null)
        {
            battleManager = Object.FindFirstObjectByType<BattleManager>();
            if (battleManager != null)
            {
                Debug.Log("[BattleUIManager] Found BattleManager automatically in Start()");
            }
            else
            {
                Debug.LogWarning("[BattleUIManager] BattleManager not found. Please assign it in the Inspector.");
            }
        }
        
        // 메인 메뉴 패널 찾기
        FindMainMenuPanel();

        // Item 패널 참조
        FindBattleItemPanelIfNeeded();

        // (선택) 별도 FightSubPanel 오브젝트가 있을 때만 하위 서브버튼 연결
        FindFightSubPanel();

        // 메인 메뉴 Attack / Skill / Item / Defend / Flee 연결
        FindAndConnectMainMenuButtons();

        // ItemPanel / Back
        SetupItemPanelBackButton();
    }

    // 메인 메뉴 패널 찾기 — FightPanel을 우선 사용, 없으면 레거시 Main 계열 fallback
    void FindMainMenuPanel()
    {
        if (mainMenuPanel != null) return;

        // 새 디자인: FightPanel을 메인 메뉴로 사용 (transform.Find는 비활성 자식도 검색)
        Transform fightPanel = transform.Find("FightPanel");
        if (fightPanel != null)
        {
            mainMenuPanel = fightPanel.gameObject;
            Debug.Log("[BattleUIManager] mainMenuPanel 자동 할당: FightPanel");
            return;
        }

        // 하위 호환: 기존 Main/MainMenuPanel 등 찾기 (없어도 됨)
        string[] possibleNames = { "MainMenuPanel", "MainMenu", "MainPanel", "MenuPanel", "BattleMenu", "Main" };
        foreach (string name in possibleNames)
        {
            GameObject panel = GameObject.Find(name);
            if (panel != null)
            {
                mainMenuPanel = panel;
                Debug.LogWarning($"[BattleUIManager] mainMenuPanel을 '{name}'(으)로 할당 (FightPanel 권장)");
                return;
            }
        }

        Debug.LogWarning("[BattleUIManager] mainMenuPanel을 찾지 못했습니다. Inspector에서 수동 할당이 필요합니다.");
    }

    // FightSubPanel 찾기
    void FindFightSubPanel()
    {
        if (fightSubPanel == null)
        {
            fightSubPanel = GameObject.Find("FightSubPanel");
            if (fightSubPanel == null)
            {
                // 여러 가능한 이름으로 찾기
                string[] possibleNames = { "FightSubPanel", "FightSubMenu", "FightMenu" };
                foreach (string name in possibleNames)
                {
                    GameObject panel = GameObject.Find(name);
                    if (panel != null)
                    {
                        fightSubPanel = panel;
                        Debug.Log($"[BattleUIManager] Found FightSubPanel: {name}");
                        break;
                    }
                }
            }
            else
            {
                Debug.Log("[BattleUIManager] Found FightSubPanel automatically");
            }
        }
        
        if (fightSubPanel != null)
        {
            Debug.Log("[BattleUIManager] FightSubPanel found: " + fightSubPanel.name);
            // 초기에는 비활성화
            fightSubPanel.SetActive(false);
            
            // FightSubPanel 내 버튼 찾기
            FindFightSubPanelButtons();
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] FightSubPanel not found! Please assign it in the Inspector.");
        }
    }

    /// <summary>
    /// 인스펙터에 비우거나 잘못 넣었을 때 SubPanels/ItemPanel을 찾고,
    /// 실수로 FightPanel의 Item 버튼이 들어간 경우 무시 후 다시 검색합니다.
    /// </summary>
    void FindBattleItemPanelIfNeeded()
    {
        ClearBattleItemPanelIfWrongButtonAssign();

        if (battleItemPanel != null)
            return;

        Transform subItem = transform.Find("SubPanels/ItemPanel");
        if (subItem != null)
        {
            battleItemPanel = subItem.gameObject;
            Debug.Log("[BattleUIManager] battleItemPanel 자동 할당: SubPanels/ItemPanel");
            return;
        }

        GameObject found = BattleItemPanel.FindItemPanelGameObjectInScene();
        if (found != null)
        {
            battleItemPanel = found;
            Debug.Log("[BattleUIManager] battleItemPanel 자동 할당: FindItemPanelGameObjectInScene");
        }
    }

    /// <summary>FightPanel의 Item 버튼이 Battle Item Panel 슬롯에 들어간 흔한 실수 제거.</summary>
    void ClearBattleItemPanelIfWrongButtonAssign()
    {
        if (battleItemPanel == null) return;

        bool looksLikeMainItemButton =
            battleItemPanel.GetComponent<Button>() != null
            && battleItemPanel.GetComponent<BattleItemPanel>() == null
            && battleItemPanel.name == "Item";

        if (!looksLikeMainItemButton)
            return;

        Transform parent = battleItemPanel.transform.parent;
        if (parent != null && parent.name == "FightPanel")
        {
            Debug.LogWarning("[BattleUIManager] Battle Item Panel에 Item 버튼이 연결되어 있었습니다. ItemPanel을 자동 검색합니다.");
            battleItemPanel = null;
        }
    }

    /// <summary>Item 버튼 → ItemPanel 표시. 외부에서도 호출 가능.</summary>
    public void OpenBattleItemPanel()
    {
        FindBattleItemPanelIfNeeded();
        if (battleItemPanel == null)
            battleItemPanel = BattleItemPanel.FindItemPanelGameObjectInScene();

        if (battleItemPanel == null)
        {
            Debug.LogWarning("[BattleUIManager] ItemPanel을 찾지 못했습니다. Hierarchy에 SubPanels/ItemPanel을 두거나 Inspector의 Battle Item Panel에 할당하세요.");
            return;
        }

        BattleItemPanel bip = battleItemPanel.GetComponent<BattleItemPanel>();
        if (bip != null)
            bip.Open();
        else
            BattleItemPanel.ActivateWithParents(battleItemPanel);
    }

    /// <summary>아이템 패널만 끕니다 (Back 버튼·토글 닫기 공용).</summary>
    public void CloseBattleItemPanel()
    {
        FindBattleItemPanelIfNeeded();
        if (battleItemPanel == null)
            battleItemPanel = BattleItemPanel.FindItemPanelGameObjectInScene();
        if (battleItemPanel == null) return;

        BattleItemPanel bip = battleItemPanel.GetComponent<BattleItemPanel>();
        if (bip != null)
            bip.Close();
        else
            battleItemPanel.SetActive(false);
    }

    /// <summary>스킬·아이템 서브 패널을 모두 끕니다 (Attack 등).</summary>
    public void CloseSkillAndItemSubPanels()
    {
        CloseBattleItemPanel();
        if (battleManager != null && battleManager.skillPanel != null)
            battleManager.skillPanel.SetActive(false);
    }

    /// <summary>Item 메인/서브: 열려 있으면 끄고, 꺼져 있으면 엽니다.</summary>
    public void ToggleBattleItemPanel()
    {
        FindBattleItemPanelIfNeeded();
        if (battleItemPanel == null)
            battleItemPanel = BattleItemPanel.FindItemPanelGameObjectInScene();

        if (battleItemPanel != null && battleItemPanel.activeInHierarchy)
        {
            CloseBattleItemPanel();
            return;
        }

        if (battleManager != null && battleManager.skillPanel != null && battleManager.skillPanel.activeInHierarchy)
            battleManager.OnSkillBack();

        OpenBattleItemPanel();
    }

    /// <summary>Skill 메인/서브: 열려 있으면 끄고, 꺼져 있으면 스킬 목록을 엽니다.</summary>
    void ToggleSkillPanel()
    {
        if (battleManager == null) return;
        GameObject sp = battleManager.skillPanel;
        if (sp != null && sp.activeInHierarchy)
        {
            battleManager.OnSkillBack();
            return;
        }

        // 아이템 패널이 열려 있으면 먼저 닫고 스킬만 표시
        CloseBattleItemPanel();

        battleManager.OnSkillButton();
    }

    /// <summary>ItemPanel 자식 Back 버튼 → 패널 닫기 (Hierarchy 이름 Back).</summary>
    void SetupItemPanelBackButton()
    {
        FindBattleItemPanelIfNeeded();
        if (battleItemPanel == null)
            battleItemPanel = BattleItemPanel.FindItemPanelGameObjectInScene();
        if (battleItemPanel == null) return;

        Transform back = battleItemPanel.transform.Find("Back");
        if (back == null)
        {
            foreach (Transform t in battleItemPanel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Back")
                {
                    back = t;
                    break;
                }
            }
        }

        Button btn = back != null ? back.GetComponent<Button>() : null;
        if (btn == null)
        {
            Debug.LogWarning("[BattleUIManager] ItemPanel에서 이름이 Back인 Button을 찾지 못했습니다.");
            return;
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            Debug.Log("[BattleUIManager] ItemPanel Back clicked");
            CloseBattleItemPanel();
        });
    }

    // FightSubPanel 내 버튼 찾기
    void FindFightSubPanelButtons()
    {
        if (fightSubPanel == null) return;

        Button[] buttons = fightSubPanel.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            string btnName = btn.name.ToLower();
            
            // Attack 버튼
            if (attackSubButton == null && (btnName.Contains("attack") || btnName.Contains("atk")))
            {
                attackSubButton = btn;
                Debug.Log("[BattleUIManager] Found AttackSubButton: " + btn.name);
            }
            // Skill 버튼
            else if (skillSubButton == null && btnName.Contains("skill"))
            {
                skillSubButton = btn;
                Debug.Log("[BattleUIManager] Found SkillSubButton: " + btn.name);
            }
            // Item 버튼
            else if (itemSubButton == null && btnName.Contains("item"))
            {
                itemSubButton = btn;
                Debug.Log("[BattleUIManager] Found ItemSubButton: " + btn.name);
            }
            // Defend 버튼
            else if (defendSubButton == null && btnName.Contains("defend"))
            {
                defendSubButton = btn;
                Debug.Log("[BattleUIManager] Found DefendSubButton: " + btn.name);
            }
        }
        
        // 버튼 연결
        SetupFightSubPanelButtons();
    }

    // FightSubPanel 버튼 연결
    void SetupFightSubPanelButtons()
    {
        // Attack 버튼
        if (attackSubButton != null)
        {
            attackSubButton.onClick.RemoveAllListeners();
            attackSubButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] AttackSubButton clicked!");
                if (battleManager != null)
                {
                    // FightSubPanel 닫기
                    if (fightSubPanel != null) fightSubPanel.SetActive(false);

                    CloseSkillAndItemSubPanels();
                    
                    // Attack 패널(기존 UI)이 있다면 숨기기
                    if (battleManager.actionPanel != null) battleManager.actionPanel.SetActive(false);

                    battleManager.OnAttackButton();
                }
            });
            Debug.Log("[BattleUIManager] AttackSubButton listener added");
        }

        // Skill 버튼
        if (skillSubButton != null)
        {
            skillSubButton.onClick.RemoveAllListeners();
            skillSubButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] SkillSubButton clicked!");
                if (battleManager != null)
                {
                    // FightSubPanel 닫기
                    if (fightSubPanel != null) fightSubPanel.SetActive(false);

                    ToggleSkillPanel();
                }
            });
            Debug.Log("[BattleUIManager] SkillSubButton listener added");
        }

        // Item 버튼
        if (itemSubButton != null)
        {
            itemSubButton.onClick.RemoveAllListeners();
            itemSubButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] ItemSubButton clicked!");
                ToggleBattleItemPanel();
            });
            Debug.Log("[BattleUIManager] ItemSubButton listener added");
        }

        // Defend 버튼
        if (defendSubButton != null)
        {
            defendSubButton.onClick.RemoveAllListeners();
            defendSubButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] DefendSubButton clicked!");
                if (battleManager != null)
                {
                    // FightSubPanel 닫기
                    if (fightSubPanel != null) fightSubPanel.SetActive(false);
                    battleManager.OnDefendButton();
                }
            });
            Debug.Log("[BattleUIManager] DefendSubButton listener added");
        }
    }

    // 메인 메뉴 버튼 찾기 및 연결 — FightPanel 자식 Attack / Skill / Item / Defend / Flee
    void FindAndConnectMainMenuButtons()
    {
        if (mainMenuPanel == null)
        {
            Debug.LogWarning("[BattleUIManager] mainMenuPanel이 없습니다! 버튼 연결을 건너뜁니다.");
            return;
        }

        Transform mainTransform = mainMenuPanel.transform;

        if (attackButton == null) attackButton = mainTransform.Find("Attack")?.GetComponent<Button>();
        if (skillButton == null) skillButton = mainTransform.Find("Skill")?.GetComponent<Button>();
        if (itemButton == null) itemButton = mainTransform.Find("Item")?.GetComponent<Button>();
        if (defendButton == null) defendButton = mainTransform.Find("Defend")?.GetComponent<Button>();
        if (fleeButton == null) fleeButton = mainTransform.Find("Flee")?.GetComponent<Button>();

        if (attackButton == null) attackButton = FindButtonInPanel("Attack", "Atk", "AttackButton");
        if (skillButton == null) skillButton = FindButtonInPanel("Skill", "SkillButton");
        if (itemButton == null) itemButton = FindButtonInPanel("Item", "ItemButton");
        if (defendButton == null) defendButton = FindButtonInPanel("Defend", "DefendButton");
        if (fleeButton == null) fleeButton = FindButtonInPanel("Flee", "FleeButton", "Run", "Escape");

        Debug.Log($"[BattleUIManager] 메인 메뉴 버튼 (Atk:{attackButton != null}, Sk:{skillButton != null}, It:{itemButton != null}, Def:{defendButton != null}, Fl:{fleeButton != null})");

        SetupMainMenuButtons();
    }

    // 패널 내에서 버튼 찾기
    Button FindButtonInPanel(params string[] names)
    {
        // 메인 패널이 있으면 그 안에서 찾기
        if (mainMenuPanel != null)
        {
            Button[] buttons = mainMenuPanel.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                string btnName = btn.name.ToLower();
                foreach (string name in names)
                {
                    if (btnName.Contains(name.ToLower()))
                    {
                        Debug.Log($"[BattleUIManager] Found button in main menu panel: {btn.name}");
                        return btn;
                    }
                }
            }
        }
        
        // 메인 패널에서 못 찾으면 씬 전체에서 찾기
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    Debug.Log($"[BattleUIManager] Found button by name: {name}");
                    return btn;
                }
            }
        }
        
        // 모든 버튼에서 검색
        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (Button btn in allButtons)
        {
            string btnName = btn.name.ToLower();
            foreach (string name in names)
            {
                if (btnName.Contains(name.ToLower()))
                {
                    Debug.Log("[BattleUIManager] Found button by name search: " + btn.name);
                    return btn;
                }
            }
        }
        
        return null;
    }

    // EventSystem 확인 및 생성
    void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("[BattleUIManager] EventSystem not found. Creating one...");
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }
        else
        {
            Debug.Log("[BattleUIManager] EventSystem found: " + eventSystem.name);
        }

        // Canvas에 GraphicRaycaster 확인
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Debug.LogWarning("[BattleUIManager] GraphicRaycaster not found on Canvas. Adding one...");
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                Debug.Log("[BattleUIManager] GraphicRaycaster found on Canvas: " + canvas.name);
            }
        }
    }


    // 메인 메뉴 버튼 설정 — Fight / Run 단계 없이 5버튼 직결 (서브 패널은 추후 Skill·Item 전용)
    void SetupMainMenuButtons()
    {
        if (attackButton != null)
        {
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] Attack (main) clicked!");
                if (battleManager == null) return;
                CloseSkillAndItemSubPanels();
                if (battleManager.actionPanel != null) battleManager.actionPanel.SetActive(false);
                battleManager.OnAttackButton();
            });
        }

        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] Skill (main) clicked!");
                ToggleSkillPanel();
            });
        }

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] Item (main) clicked!");
                ToggleBattleItemPanel();
            });
        }

        if (defendButton != null)
        {
            defendButton.onClick.RemoveAllListeners();
            defendButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] Defend (main) clicked!");
                battleManager?.OnDefendButton();
            });
        }

        if (fleeButton != null)
        {
            fleeButton.onClick.RemoveAllListeners();
            fleeButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] Flee (main) clicked!");
                if (battleManager != null)
                    battleManager.OnRunButton();
            });
        }
    }

    // 전투 명령 단계 UI 표시 — 예전엔 Fight 클릭 후 서브패널이었으나, 현재는 메인 5버튼이 곧 명령 메뉴
    public void ShowFightSubPanel()
    {
        Debug.Log("[BattleUIManager] ShowFightSubPanel → ShowMainMenu (메인 5버튼 패턴)");
        ShowMainMenu();
    }

    // 메인 메뉴 패널 표시 (전투 시작 시 호출)
    public void ShowMainMenu()
    {
        Debug.Log("[BattleUIManager] ShowMainMenu called");
        
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("[BattleUIManager] Main menu panel activated");
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] Main menu panel is null! Cannot show.");
        }
        
        // FightSubPanel은 닫기
        if (fightSubPanel != null)
        {
            fightSubPanel.SetActive(false);
        }
    }

    // 메인 메뉴 패널 숨기기
    public void HideMainMenu()
    {
        Debug.Log("[BattleUIManager] HideMainMenu called");
        
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
    }


    // 외부에서 호출 가능한 메뉴 닫기 메서드
    public void ForceCloseMenus()
    {
        if (fightSubPanel != null)
            fightSubPanel.SetActive(false);
        CloseSkillAndItemSubPanels();
    }

    // -------------------- Back 버튼 관리 --------------------
    public Button backButton;

    public void CreateBackButton()
    {
        if (backButton != null) return;

        // Canvas 찾기
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 버튼 생성
        GameObject btnObj = new GameObject("GlobalBackButton", typeof(RectTransform), typeof(Button), typeof(Image));
        btnObj.transform.SetParent(canvas.transform, false);

        // 위치 설정 (우측 하단)
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-20, 20); // 여백
        rt.sizeDelta = new Vector2(120, 60);

        // 스타일 설정
        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // 반투명 검정

        // 텍스트 추가
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI text = textObj.GetComponent<TMPro.TextMeshProUGUI>();
        text.text = "Back";
        text.fontSize = 24;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;

        backButton = btnObj.GetComponent<Button>();
        backButton.onClick.AddListener(() => 
        {
            if (battleManager != null) battleManager.OnCancelButton();
        });

        // 초기에는 숨김
        btnObj.SetActive(false);
        Debug.Log("[BattleUIManager] Global Back Button created.");
    }

    public void ShowBackButton(bool show)
    {
        if (backButton == null) CreateBackButton();
        if (backButton != null) backButton.gameObject.SetActive(show);
    }

    // 빈 공간 터치 확인 (UI가 아닌 곳을 터치했는지)
    public bool IsTouchingBackground()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 마우스 포인터가 UI 위에 있는지 확인
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }
            return true;
        }
        
        // 모바일 터치 지원
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                return false;
            }
            return true;
        }

        return false;
    }
    // -------------------- 확인 팝업 (Confirmation Dialog) --------------------
    public GameObject confirmationDialog;
    public TMPro.TextMeshProUGUI confirmationText;
    public Button confirmYesButton;
    public Button confirmNoButton;

    public void ShowConfirmationDialog(string message, System.Action onYes, System.Action onNo)
    {
        if (confirmationDialog == null)
        {
            CreateConfirmationDialog();
        }

        if (confirmationDialog != null)
        {
            confirmationDialog.SetActive(true);
            
            if (confirmationText != null)
                confirmationText.text = message;

            if (confirmYesButton != null)
            {
                confirmYesButton.onClick.RemoveAllListeners();
                confirmYesButton.onClick.AddListener(() =>
                {
                    confirmationDialog.SetActive(false);
                    onYes?.Invoke();
                });
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.onClick.RemoveAllListeners();
                confirmNoButton.onClick.AddListener(() =>
                {
                    confirmationDialog.SetActive(false);
                    onNo?.Invoke();
                });
            }
        }
    }

    private void CreateConfirmationDialog()
    {
        // Canvas 찾기
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // 패널 생성
        GameObject dialogObj = new GameObject("ConfirmationDialog", typeof(RectTransform), typeof(Image));
        dialogObj.transform.SetParent(canvas.transform, false);
        
        RectTransform rt = dialogObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 200);
        rt.anchoredPosition = Vector2.zero;

        Image img = dialogObj.GetComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // 진한 배경

        // 텍스트 생성
        GameObject textObj = new GameObject("Message", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        textObj.transform.SetParent(dialogObj.transform, false);
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.1f, 0.4f);
        textRT.anchorMax = new Vector2(0.9f, 0.9f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI text = textObj.GetComponent<TMPro.TextMeshProUGUI>();
        text.text = "Message";
        text.fontSize = 20;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TMPro.TextWrappingModes.Normal;

        // Yes 버튼 생성
        GameObject yesBtnObj = new GameObject("YesButton", typeof(RectTransform), typeof(Button), typeof(Image));
        yesBtnObj.transform.SetParent(dialogObj.transform, false);
        RectTransform yesRT = yesBtnObj.GetComponent<RectTransform>();
        yesRT.anchorMin = new Vector2(0.1f, 0.1f);
        yesRT.anchorMax = new Vector2(0.45f, 0.3f);
        yesRT.offsetMin = Vector2.zero;
        yesRT.offsetMax = Vector2.zero;

        Image yesImg = yesBtnObj.GetComponent<Image>();
        yesImg.color = new Color(0.2f, 0.6f, 0.2f, 1f); // 녹색

        GameObject yesTextObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        yesTextObj.transform.SetParent(yesBtnObj.transform, false);
        TMPro.TextMeshProUGUI yesText = yesTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        yesText.text = "Yes";
        yesText.fontSize = 18;
        yesText.alignment = TMPro.TextAlignmentOptions.Center;
        yesText.color = Color.white;
        RectTransform yesTextRT = yesTextObj.GetComponent<RectTransform>();
        yesTextRT.anchorMin = Vector2.zero;
        yesTextRT.anchorMax = Vector2.one;
        yesTextRT.offsetMin = Vector2.zero;
        yesTextRT.offsetMax = Vector2.zero;

        // No 버튼 생성
        GameObject noBtnObj = new GameObject("NoButton", typeof(RectTransform), typeof(Button), typeof(Image));
        noBtnObj.transform.SetParent(dialogObj.transform, false);
        RectTransform noRT = noBtnObj.GetComponent<RectTransform>();
        noRT.anchorMin = new Vector2(0.55f, 0.1f);
        noRT.anchorMax = new Vector2(0.9f, 0.3f);
        noRT.offsetMin = Vector2.zero;
        noRT.offsetMax = Vector2.zero;

        Image noImg = noBtnObj.GetComponent<Image>();
        noImg.color = new Color(0.6f, 0.2f, 0.2f, 1f); // 적색

        GameObject noTextObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        noTextObj.transform.SetParent(noBtnObj.transform, false);
        TMPro.TextMeshProUGUI noText = noTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        noText.text = "No";
        noText.fontSize = 18;
        noText.alignment = TMPro.TextAlignmentOptions.Center;
        noText.color = Color.white;
        RectTransform noTextRT = noTextObj.GetComponent<RectTransform>();
        noTextRT.anchorMin = Vector2.zero;
        noTextRT.anchorMax = Vector2.one;
        noTextRT.offsetMin = Vector2.zero;
        noTextRT.offsetMax = Vector2.zero;

        // 할당
        confirmationDialog = dialogObj;
        confirmationText = text;
        confirmYesButton = yesBtnObj.GetComponent<Button>();
        confirmNoButton = noBtnObj.GetComponent<Button>();
        
        // 초기 비활성화
        confirmationDialog.SetActive(false);
    }
}
