using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
public class DungeonScene03Fixer : EditorWindow
{
    [MenuItem("Dungeon/Fix Scene 03 Controls")]
    public static void FixControls()
    {
        Debug.Log("Fixing Controls for Scene 03...");

        // 1. Find or Create DungeonSystem Holder
        GameObject sys = GameObject.Find("DungeonSystem_03");
        if (sys == null) sys = new GameObject("DungeonSystem_03");

        // 2. Ensure Player exists
        DungeonGridPlayer player = sys.GetComponent<DungeonGridPlayer>();
        if (player == null) player = sys.AddComponent<DungeonGridPlayer>();
        
        // 4. Find and Link Minimap Cursor
        GameObject cursorObj = GameObject.Find("PlayerCursor");
        if (cursorObj == null) cursorObj = GameObject.Find("Cursor"); // Fallback
        if (cursorObj == null) cursorObj = GameObject.Find("PlayerIcon"); // Fallback 2
        
        if (cursorObj != null)
        {
            player.playerCursor = cursorObj.GetComponent<RectTransform>();
            Debug.Log($"Linked Player Cursor: {cursorObj.name}");
        }
        else
        {
            Debug.LogWarning("Could not find object named 'PlayerCursor'. Please assign it manually to DungeonGridPlayer.");
        }

        // 5. Ensure Input Binder exists
        DungeonInputBinder binder = sys.GetComponent<DungeonInputBinder>();
        if (binder == null) binder = sys.AddComponent<DungeonInputBinder>();

        Debug.Log("Scene 03 Controls Fixed! WASD and UI Buttons should now work.");
        
        // Force selection so user sees it
        Selection.activeGameObject = sys;
    }
}
#endif
