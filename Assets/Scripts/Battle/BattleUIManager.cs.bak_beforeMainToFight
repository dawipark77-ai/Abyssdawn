using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class BattleUIManager : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    public Button fightButton;
    public Button itemButton;
    public Button runButton;
    public Button fleeButton;

    [Header("메인 패널")]
    public GameObject mainMenuPanel;

    [Header("Fight 하위 메뉴")]
    public GameObject fightSubPanel;    // Attack / Skill / Item / Defend 패널
    [Tooltip("비어 있으면 이름으로 ItemPanel 검색. 할당 시 Item 버튼은 패널만 연 후 소비하지 않음.")]
    public GameObject battleItemPanel; // SubPanels/ItemPanel 등
    public Button attackSubButton;      // Fight > Attack
    public Button skillSubButton;       // Fight > Skill (나중에 스킬 구현 시)
    public Button itemSubButton;        // Fight > Item
    public Button defendSubButton;      // Fight > Defend

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

        // Item 패널 참조는 Fight 버튼 리스너보다 먼저 (Item 버튼 동작 분기)
        FindBattleItemPanelIfNeeded();

        // FightSubPanel 찾기 (내부에서 서브 버튼 연결)
        FindFightSubPanel();

        // 메인 메뉴 버튼 찾기 및 연결
        FindAndConnectMainMenuButtons();
    }

    // 메인 메뉴 패널 찾기
    void FindMainMenuPanel()
    {
        if (mainMenuPanel == null)
        {
            // 여러 가능한 이름으로 찾기
            string[] possibleNames = { "MainMenuPanel", "MainMenu", "MainPanel", "MenuPanel", "BattleMenu" };
            foreach (string name in possibleNames)
            {
                GameObject panel = GameObject.Find(name);
                if (panel != null)
                {
                    mainMenuPanel = panel;
                    Debug.Log($"[BattleUIManager] Found main menu panel: {name}");
                    break;
                }
            }
            
            // 이름으로 못 찾으면 모든 GameObject에서 검색
            if (mainMenuPanel == null)
            {
                GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    string objName = obj.name.ToLower();
                    if ((objName.Contains("main") && objName.Contains("menu")) || 
                        (objName.Contains("main") && objName.Contains("panel")))
                    {
                        mainMenuPanel = obj;
                        Debug.Log("[BattleUIManager] Found main menu panel by name search: " + obj.name);
                        break;
                    }
                }
            }
        }
        
        if (mainMenuPanel != null)
        {
            Debug.Log("[BattleUIManager] Main menu panel found: " + mainMenuPanel.name + ", Active: " + mainMenuPanel.activeSelf);
        }
        else
        {
            Debug.LogWarning("[BattleUIManager] Main menu panel not found! Please assign it in the Inspector.");
        }
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
                string[] possibleNames = { "FightSubPanel", "FightSubMenu", "FightMenu", "FightPanel" };
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
    /// 전투 아이템 패널 (이름 ItemPanel). 인스펙터에 없으면 자동 검색.
    /// </summary>
    void FindBattleItemPanelIfNeeded()
    {
        if (battleItemPanel != null) return;

        GameObject found = GameObject.Find("ItemPanel");
        if (found != null)
        {
            battleItemPanel = found;
            Debug.Log("[BattleUIManager] battleItemPanel 자동 할당: ItemPanel");
        }
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
                    
                    // Skill 패널 활성화 (ShowActionPanel 호출하지 않음 - OnSkillButton에서 처리)
                    battleManager.OnSkillButton();
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
                // FightPanel/fightSubPanel 은 끄지 않음 — ItemPanel만 위에 겹쳐 띄움

                if (battleItemPanel != null)
                {
                    BattleItemPanel bip = battleItemPanel.GetComponent<BattleItemPanel>();
                    if (bip != null) bip.Open();
                    else battleItemPanel.SetActive(true);
                    return;
                }

                if (battleManager != null)
                    battleManager.OnItemButton();
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

    // 메인 메뉴 버튼 찾기 및 연결
    void FindAndConnectMainMenuButtons()
    {
        // Fight 버튼 찾기
        if (fightButton == null)
        {
            fightButton = FindButtonInPanel("Fight", "FightButton", "BtnFight");
        }
        
        // Flee 버튼 찾기
        if (fleeButton == null)
        {
            fleeButton = FindButtonInPanel("Flee", "FleeButton", "BtnFlee", "RunButton", "Run");
        }
        
        // 버튼 연결
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


    // 메인 메뉴 버튼 설정
    void SetupMainMenuButtons()
    {
        // Fight 버튼 설정
        if (fightButton != null)
        {
            fightButton.onClick.RemoveAllListeners();
            fightButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] FightButton clicked!");
                ShowFightSubPanel();
            });
            Debug.Log("[BattleUIManager] FightButton listener added");
        }
        else
        {
            Debug.LogError("[BattleUIManager] fightButton is null! Cannot add listener.");
        }

        // Flee 버튼 설정 (Run 기능)
        if (fleeButton != null)
        {
            fleeButton.onClick.RemoveAllListeners();
            fleeButton.onClick.AddListener(() =>
            {
                Debug.Log("[BattleUIManager] FleeButton clicked!");
                if (battleManager != null)
                {
                    battleManager.OnRunButton();
                }
                else
                {
                    Debug.LogError("[BattleUIManager] battleManager is null when FleeButton clicked!");
                }
            });
            Debug.Log("[BattleUIManager] FleeButton listener added");
        }
        else
        {
            Debug.LogError("[BattleUIManager] fleeButton is null! Cannot add listener.");
        }
    }

    // FightSubPanel 표시
    public void ShowFightSubPanel()
    {
        Debug.Log("[BattleUIManager] ShowFightSubPanel called");
        
        // 메인 메뉴 숨기기
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
            Debug.Log("[BattleUIManager] Main menu panel deactivated");
        }

        if (fightSubPanel != null)
        {
            fightSubPanel.SetActive(true);
            Debug.Log("[BattleUIManager] FightSubPanel activated");
        }
        else
        {
            Debug.LogError("[BattleUIManager] FightSubPanel is null! Cannot show.");
        }
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
