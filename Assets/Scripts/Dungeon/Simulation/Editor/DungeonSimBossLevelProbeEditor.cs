#if UNITY_EDITOR
using Abyssdawn;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonSimBossLevelProbe))]
public class DungeonSimBossLevelProbeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var probe = (DungeonSimBossLevelProbe)target;
        bool ready = probe != null && probe.IsReadyToRun();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Run Boss Level Probe (Editor)", GUILayout.Height(30)))
                probe.RunBossLevelProbe();
        }

        if (!ready)
        {
            EditorGUILayout.HelpBox(
                "Settings / AllyRoster / MonsterPool / BattleSimulator(같은 GameObject 권장)를 할당하세요.",
                MessageType.Info);
        }
    }
}
#endif
