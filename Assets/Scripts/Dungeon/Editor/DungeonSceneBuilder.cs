using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

#if UNITY_EDITOR
public class DungeonSceneBuilder : EditorWindow
{
    [MenuItem("Dungeon/Setup Scene 04")]
    public static void SetupScene()
    {
        Debug.Log("Start building Dungeon Scene...");

        // 0. Cleanup
        GameObject existingSys = GameObject.Find("DungeonSystem");
        if (existingSys != null) DestroyImmediate(existingSys);
        
        GameObject existingCanvas = GameObject.Find("DungeonCanvas");
        if (existingCanvas != null) DestroyImmediate(existingCanvas);

        // 1. Root Object
        GameObject dungeonRoot = new GameObject("DungeonSystem");
        MapManager mapInfo = dungeonRoot.AddComponent<MapManager>();
        DungeonGridPlayer playerInfo = dungeonRoot.AddComponent<DungeonGridPlayer>();
        dungeonRoot.AddComponent<DungeonInputBinder>(); // Auto-bind UI
        
        // 2. UI Root (Canvas)
        GameObject canvasObj = new GameObject("DungeonCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // 3. View Layers (UI elements created but not linked to view script)
        // Background
        GameObject bgObj = CreateUIElement("BackgroundLayer", canvasObj.transform, Color.gray);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one; // Full stretch
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Mid Layer Group
        GameObject midGroup = CreateUIElement("MidLayer_Group", canvasObj.transform, new Color(0,0,0,0));
        SetFullStretch(midGroup.GetComponent<RectTransform>());

        // Mid Front (Center) - The Doorway/Passage view
        GameObject midFront = CreateUIElement("Mid_Front", midGroup.transform, Color.white);
        SetRect(midFront, 0.3f, 0.7f, 0.3f, 0.7f); 
        AssignSprite(midFront.GetComponent<Image>(), "Wall_Front");

        // Mid Left
        GameObject midLeft = CreateUIElement("Mid_Left", midGroup.transform, Color.white);
        SetRect(midLeft, 0.1f, 0.3f, 0.35f, 0.65f); // Smaller trapezoid perspective
        AssignSprite(midLeft.GetComponent<Image>(), "Wall_Side_Left");

        // Mid Right
        GameObject midRight = CreateUIElement("Mid_Right", midGroup.transform, Color.white);
        SetRect(midRight, 0.7f, 0.9f, 0.35f, 0.65f); // Smaller trapezoid perspective
        AssignSprite(midRight.GetComponent<Image>(), "Wall_Side_Right");

        // Near Layer Group
        GameObject nearGroup = CreateUIElement("NearLayer_Group", canvasObj.transform, new Color(0,0,0,0));
        SetFullStretch(nearGroup.GetComponent<RectTransform>());

        // Near Left - Big Wall
        GameObject nearLeft = CreateUIElement("Near_Left", nearGroup.transform, Color.white);
        SetRect(nearLeft, 0f, 0.3f, 0.1f, 0.9f); // Full height-ish
        AssignSprite(nearLeft.GetComponent<Image>(), "Wall_Side_Left");

        // Near Right - Big Wall
        GameObject nearRight = CreateUIElement("Near_Right", nearGroup.transform, Color.white);
        SetRect(nearRight, 0.7f, 1f, 0.1f, 0.9f); // Full height-ish
        AssignSprite(nearRight.GetComponent<Image>(), "Wall_Side_Right");

        // Link references - 초기 플레이어 그리드 위치
        playerInfo.gridPos = new Vector2Int(1, 1);

        Debug.Log("Dungeon Scene Setup Complete!");
    }

    private static GameObject CreateUIElement(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(GameObject go, float minX, float maxX, float minY, float maxY)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AssignSprite(Image img, string spriteName)
    {
        string fullPath = "Assets/Images/" + spriteName + ".png";
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        if (s != null) 
        {
            img.sprite = s;
        }
        else
        {
            Debug.LogWarning("Sprite not found at: " + fullPath);
        }
    }
}
#endif
