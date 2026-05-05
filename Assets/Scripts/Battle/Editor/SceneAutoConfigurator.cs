using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class SceneAutoConfigurator : MonoBehaviour
{
    [MenuItem("Battle/Setup Battle Scene")]
    static void SetupBattleScene()
    {
        // BattleManager 찾기 또는 생성
        BattleManager battleManager = Object.FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            GameObject battleManagerObj = new GameObject("BattleManager");
            battleManager = battleManagerObj.AddComponent<BattleManager>();
        }

        // WorldRoot 생성/확인
        GameObject worldRoot = GameObject.Find("WorldRoot");
        if (worldRoot == null)
        {
            worldRoot = new GameObject("WorldRoot");
        }

        // SpawnCenter 생성/확인
        GameObject spawnCenter = GameObject.Find("SpawnCenter");
        if (spawnCenter == null)
        {
            spawnCenter = new GameObject("SpawnCenter");
            spawnCenter.transform.SetParent(worldRoot.transform, false);
            spawnCenter.transform.position = new Vector3(0, 1, 0);
        }

        // PlayerPartyRoot 생성/확인
        GameObject playerPartyRoot = GameObject.Find("PlayerPartyRoot");
        if (playerPartyRoot == null)
        {
            playerPartyRoot = new GameObject("PlayerPartyRoot");
            playerPartyRoot.transform.SetParent(worldRoot.transform, false);
        }

        // PlayerPartyCenter 생성/확인
        GameObject playerPartyCenter = GameObject.Find("PlayerPartyCenter");
        if (playerPartyCenter == null)
        {
            playerPartyCenter = new GameObject("PlayerPartyCenter");
            playerPartyCenter.transform.SetParent(playerPartyRoot.transform, false);
            playerPartyCenter.transform.position = new Vector3(0, -2, 0);
        }

        // Canvas 생성/확인
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            canvasObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // PlayerStatusPanel 생성/확인
        GameObject playerStatusPanel = GameObject.Find("PlayerStatusPanel");
        if (playerStatusPanel == null)
        {
            playerStatusPanel = new GameObject("PlayerStatusPanel");
            playerStatusPanel.transform.SetParent(canvas.transform, false);
            RectTransform rt = playerStatusPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 20f);
            rt.offsetMax = new Vector2(0f, 280f);
        }

        // EnemyStatusPanel 생성/확인
        GameObject enemyStatusPanel = GameObject.Find("EnemyStatusPanel");
        if (enemyStatusPanel == null)
        {
            enemyStatusPanel = new GameObject("EnemyStatusPanel");
            enemyStatusPanel.transform.SetParent(canvas.transform, false);
            RectTransform rt = enemyStatusPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -50);
            rt.sizeDelta = new Vector2(600, 200);
        }

        // EnemyDatabase 로드
        EnemyDatabase enemyDatabase = LoadAsset<EnemyDatabase>("EnemyDatabase");
        if (enemyDatabase == null)
        {
            Debug.LogWarning("[SceneAutoConfigurator] EnemyDatabase.asset not found! Please create it in Assets/Genesis 01/Assets/Scripts/Battle/Data/");
        }

        // SkillDataList 로드
        SkillDataList skillLibrary = LoadAsset<SkillDataList>("SkillDataList");
        if (skillLibrary == null)
        {
            Debug.LogWarning("[SceneAutoConfigurator] SkillDataList.asset not found! Please create it in Assets/Genesis 01/Assets/Scripts/Battle/");
        }

        // UI 패널 설정
        SetupUIPanels(battleManager, canvas.transform);
        
        // 버튼 설정
        SetupButtons(battleManager, canvas.transform);
        
        // BattleUIManager 설정
        SetupBattleUIManager(battleManager, canvas.transform);
        
        // PlayerStats 설정
        SetupPlayerStats(battleManager, worldRoot.transform);

        // BattleManager에 모든 참조 연결
        SerializedObject so = new SerializedObject(battleManager);
        
        // EnemyDatabase 연결
        if (enemyDatabase != null)
        {
            var enemyDbProp = so.FindProperty("enemyDatabase");
            if (enemyDbProp != null)
            {
                enemyDbProp.objectReferenceValue = enemyDatabase;
                Debug.Log($"[SceneAutoConfigurator] EnemyDatabase assigned to BattleManager: {enemyDatabase.name}");
            }
            else
            {
                Debug.LogError("[SceneAutoConfigurator] Could not find 'enemyDatabase' property in BattleManager!");
            }
        }
        else
        {
            Debug.LogError("[SceneAutoConfigurator] EnemyDatabase is null! Cannot assign to BattleManager.");
        }

        // SkillLibrary 연결
        if (skillLibrary != null)
        {
            var skillLibProp = so.FindProperty("skillLibrary");
            if (skillLibProp != null)
            {
                skillLibProp.objectReferenceValue = skillLibrary;
                Debug.Log($"[SceneAutoConfigurator] SkillLibrary assigned to BattleManager: {skillLibrary.name}");
            }
            else
            {
                Debug.LogError("[SceneAutoConfigurator] Could not find 'skillLibrary' property in BattleManager!");
            }
            
            // Fireball 스킬은 BattleManager의 CacheHeroSkills()에서 런타임에 할당됩니다.
            // SkillData는 ScriptableObject가 아니므로 SerializedObject로 직접 할당할 수 없습니다.
            // 대신 BattleManager.Start()에서 CacheHeroSkills()가 호출되어 자동으로 할당됩니다.
        }
        else
        {
            Debug.LogWarning("[SceneAutoConfigurator] SkillLibrary is null! BattleManager may not have skills available.");
        }

        // Transform 참조 연결
        so.FindProperty("worldRoot").objectReferenceValue = worldRoot.transform;
        so.FindProperty("spawnCenter").objectReferenceValue = spawnCenter.transform;
        so.FindProperty("playerPartyRoot").objectReferenceValue = playerPartyRoot.transform;
        so.FindProperty("playerPartyCenter").objectReferenceValue = playerPartyCenter.transform;
        so.FindProperty("canvas").objectReferenceValue = canvas;
        so.FindProperty("enemyStatusPanel").objectReferenceValue = enemyStatusPanel.transform;
        so.FindProperty("playerStatusPanel").objectReferenceValue = playerStatusPanel.GetComponent<RectTransform>();

        // UI 요소 연결
        SetupUIElements(battleManager, canvas.transform, so);

        so.ApplyModifiedProperties();

        EditorUtility.DisplayDialog("Battle Scene Setup", 
            "Battle scene has been configured!\n\n" +
            "✅ EnemyDatabase: " + (enemyDatabase != null ? "Loaded" : "Not Found") + "\n" +
            "✅ SkillLibrary: " + (skillLibrary != null ? "Loaded" : "Not Found") + "\n" +
            "✅ All Transforms: Connected\n" +
            "✅ All UI Elements: Connected", 
            "OK");
    }

    static T LoadAsset<T>(string assetName) where T : ScriptableObject
    {
        // 먼저 알려진 경로에서 시도
        string[] knownPaths = new string[]
        {
            $"Assets/Genesis 01/Assets/Scripts/Battle/Data/{assetName}.asset",
            $"Assets/Genesis 01/Assets/Scripts/Battle/{assetName}.asset",
            $"Assets/Scripts/Battle/Data/{assetName}.asset",
            $"Assets/Scripts/Battle/{assetName}.asset",
            $"Assets/{assetName}.asset"
        };

        foreach (string path in knownPaths)
        {
            if (System.IO.File.Exists(path))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    Debug.Log($"[SceneAutoConfigurator] Found {assetName} at: {path}");
                    return asset;
                }
            }
        }

        // 정확한 이름으로 검색
        string[] guids = AssetDatabase.FindAssets(assetName);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && asset.name == assetName)
            {
                Debug.Log($"[SceneAutoConfigurator] Found {assetName} at: {path}");
                return asset;
            }
        }

        // 타입으로 검색 (백업)
        guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null && (asset.name == assetName || asset.name.Contains(assetName)))
            {
                Debug.Log($"[SceneAutoConfigurator] Found {assetName} (by type) at: {path}");
                return asset;
            }
        }

        // 파일 이름으로 검색
        string[] allGuids = AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name);
        if (allGuids.Length == 0)
        {
            allGuids = AssetDatabase.FindAssets("", new[] { "Assets" });
        }
        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(assetName) && path.EndsWith(".asset"))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    Debug.Log($"[SceneAutoConfigurator] Found {assetName} (by filename) at: {path}");
                    return asset;
                }
            }
        }

        Debug.LogError($"[SceneAutoConfigurator] Could not find {assetName} ({typeof(T).Name}). Please check if the asset exists in the project.");
        return null;
    }

    static void SetupUIElements(BattleManager battleManager, Transform canvasParent, SerializedObject so)
    {
        // 기존 UI 요소만 찾아서 연결 (생성하지 않음)
        ScrollRect scrollRect = Object.FindFirstObjectByType<ScrollRect>();
        TextMeshProUGUI messageText = Object.FindFirstObjectByType<TextMeshProUGUI>();
        TextMeshProUGUI playerHPText = GameObject.Find("PlayerHPText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI playerMPText = GameObject.Find("PlayerMPText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI enemyHPText = GameObject.Find("EnemyHPText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI enemyMPText = GameObject.Find("EnemyMPText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI potionCountText = GameObject.Find("PotionCountText")?.GetComponent<TextMeshProUGUI>();

        // SerializedObject에 연결 (null이어도 연결 시도)
        if (scrollRect != null)
        {
            var prop = so.FindProperty("scrollRect");
            if (prop != null) prop.objectReferenceValue = scrollRect;
        }

        if (messageText != null)
        {
            var prop = so.FindProperty("messageText");
            if (prop != null) prop.objectReferenceValue = messageText;
        }

        if (playerHPText != null)
        {
            var prop = so.FindProperty("playerHPText");
            if (prop != null) prop.objectReferenceValue = playerHPText;
        }

        if (playerMPText != null)
        {
            var prop = so.FindProperty("playerMPText");
            if (prop != null) prop.objectReferenceValue = playerMPText;
        }

        if (enemyHPText != null)
        {
            var prop = so.FindProperty("enemyHPText");
            if (prop != null) prop.objectReferenceValue = enemyHPText;
        }

        if (enemyMPText != null)
        {
            var prop = so.FindProperty("enemyMPText");
            if (prop != null) prop.objectReferenceValue = enemyMPText;
        }

        if (potionCountText != null)
        {
            var prop = so.FindProperty("potionCountText");
            if (prop != null) prop.objectReferenceValue = potionCountText;
        }
    }

    static GameObject CreateText(string name, Transform parent, Vector2 position, string text)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(200, 30);
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 16;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = Color.white;
        return textObj;
    }

    static void SetupUIPanels(BattleManager battleManager, Transform canvasParent)
    {
        // 기존 UI 패널만 찾아서 연결 (생성하지 않음)
        GameObject actionPanel = GameObject.Find("ActionPanel");
        GameObject skillPanel = GameObject.Find("SkillPanel");

        SerializedObject so = new SerializedObject(battleManager);
        if (actionPanel != null)
        {
            var prop = so.FindProperty("actionPanel");
            if (prop != null) prop.objectReferenceValue = actionPanel;
        }
        if (skillPanel != null)
        {
            var prop = so.FindProperty("skillPanel");
            if (prop != null) prop.objectReferenceValue = skillPanel;
        }
        so.ApplyModifiedProperties();
    }

    static void SetupButtons(BattleManager battleManager, Transform canvasParent)
    {
        // 기존 버튼만 찾아서 연결 (생성하지 않음)
        Button attackBtn = GameObject.Find("AttackButton")?.GetComponent<Button>();
        Button skillBtn = GameObject.Find("SkillButton")?.GetComponent<Button>();
        Button skillBackBtn = GameObject.Find("SkillBackButton")?.GetComponent<Button>();
        Button itemBtn = GameObject.Find("ItemButton")?.GetComponent<Button>();
        Button runBtn = GameObject.Find("RunButton")?.GetComponent<Button>();
        Button defendBtn = GameObject.Find("DefendButton")?.GetComponent<Button>();

        SerializedObject so = new SerializedObject(battleManager);
        if (attackBtn != null)
        {
            var prop = so.FindProperty("attackButton");
            if (prop != null) prop.objectReferenceValue = attackBtn;
        }
        if (skillBtn != null)
        {
            var prop = so.FindProperty("skillButton");
            if (prop != null) prop.objectReferenceValue = skillBtn;
        }
        if (skillBackBtn != null)
        {
            var prop = so.FindProperty("skillBackButton");
            if (prop != null) prop.objectReferenceValue = skillBackBtn;
        }
        if (itemBtn != null)
        {
            var prop = so.FindProperty("itemButton");
            if (prop != null) prop.objectReferenceValue = itemBtn;
        }
        if (runBtn != null)
        {
            var prop = so.FindProperty("fleeButton");
            if (prop != null) prop.objectReferenceValue = runBtn;
        }
        if (defendBtn != null)
        {
            var prop = so.FindProperty("defendButton");
            if (prop != null) prop.objectReferenceValue = defendBtn;
        }
        so.ApplyModifiedProperties();
    }

    static GameObject CreateButton(string name, Transform parent, Vector2 position)
    {
        GameObject btn = new GameObject(name);
        btn.transform.SetParent(parent, false);
        RectTransform rt = btn.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(80, 30);
        Image img = btn.AddComponent<Image>();
        img.color = Color.gray;
        Button button = btn.AddComponent<Button>();
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btn.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = name.Replace("Button", "");
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 14;
        text.color = Color.white;
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        return btn;
    }

    static void SetupBattleUIManager(BattleManager battleManager, Transform canvasParent)
    {
        // 기존 BattleUIManager만 찾아서 연결 (생성하지 않음)
        BattleUIManager uiManager = Object.FindFirstObjectByType<BattleUIManager>();
        if (uiManager == null)
        {
            Debug.LogWarning("[SceneAutoConfigurator] BattleUIManager not found. Please add it manually.");
            return;
        }

        // 기존 UI 요소만 찾아서 연결
        Transform fightPanelTf = GameObject.Find("FightPanel")?.transform;
        Button atkMain = fightPanelTf != null ? fightPanelTf.Find("Attack")?.GetComponent<Button>() : null;
        Button skMain = fightPanelTf != null ? fightPanelTf.Find("Skill")?.GetComponent<Button>() : null;
        Button itemMain = fightPanelTf != null ? fightPanelTf.Find("Item")?.GetComponent<Button>() : null;
        Button defMain = fightPanelTf != null ? fightPanelTf.Find("Defend")?.GetComponent<Button>() : null;
        Button fleeMain = fightPanelTf != null ? fightPanelTf.Find("Flee")?.GetComponent<Button>() : null;

        GameObject fightSubPanel = GameObject.Find("FightSubPanel");
        Button attackSubBtn = GameObject.Find("AttackSubButton")?.GetComponent<Button>();
        Button skillSubBtn = GameObject.Find("SkillSubButton")?.GetComponent<Button>();
        Button itemSubBtn = GameObject.Find("ItemSubButton")?.GetComponent<Button>();
        Button defendSubBtn = GameObject.Find("DefendSubButton")?.GetComponent<Button>();

        // BattleUIManager 참조 연결
        SerializedObject uiSo = new SerializedObject(uiManager);
        void AssignBtn(string propName, Button btn)
        {
            if (btn == null) return;
            var prop = uiSo.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = btn;
        }
        AssignBtn("attackButton", atkMain);
        AssignBtn("skillButton", skMain);
        AssignBtn("itemButton", itemMain);
        AssignBtn("defendButton", defMain);
        AssignBtn("fleeButton", fleeMain);
        if (fightSubPanel != null)
        {
            var prop = uiSo.FindProperty("fightSubPanel");
            if (prop != null) prop.objectReferenceValue = fightSubPanel;
        }
        if (attackSubBtn != null)
        {
            var prop = uiSo.FindProperty("attackSubButton");
            if (prop != null) prop.objectReferenceValue = attackSubBtn;
        }
        if (skillSubBtn != null)
        {
            var prop = uiSo.FindProperty("skillSubButton");
            if (prop != null) prop.objectReferenceValue = skillSubBtn;
        }
        if (itemSubBtn != null)
        {
            var prop = uiSo.FindProperty("itemSubButton");
            if (prop != null) prop.objectReferenceValue = itemSubBtn;
        }
        if (defendSubBtn != null)
        {
            var prop = uiSo.FindProperty("defendSubButton");
            if (prop != null) prop.objectReferenceValue = defendSubBtn;
        }
        var battleMgrProp = uiSo.FindProperty("battleManager");
        if (battleMgrProp != null) battleMgrProp.objectReferenceValue = battleManager;
        uiSo.ApplyModifiedProperties();

        // BattleManager에 BattleUIManager 연결
        SerializedObject bmSo = new SerializedObject(battleManager);
        var uiMgrProp = bmSo.FindProperty("battleUIManager");
        if (uiMgrProp != null) uiMgrProp.objectReferenceValue = uiManager;
        bmSo.ApplyModifiedProperties();
    }

    static void SetupPlayerStats(BattleManager battleManager, Transform worldParent)
    {
        // Player 찾기 또는 생성
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj == null)
        {
            playerObj = new GameObject("Player");
            playerObj.transform.SetParent(worldParent.transform, false);
            playerObj.transform.position = new Vector3(0, -2, 0);
        }

        PlayerStats playerStats = playerObj.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            playerStats = playerObj.AddComponent<PlayerStats>();
        }

        // BattleManager에 Player 연결
        SerializedObject so = new SerializedObject(battleManager);
        so.FindProperty("player").objectReferenceValue = playerStats;
        so.ApplyModifiedProperties();
    }
}
