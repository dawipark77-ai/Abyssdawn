#if UNITY_EDITOR
using Abyssdawn;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonSimulator))]
public class DungeonSimulatorEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var sim = (DungeonSimulator)target;
        bool ready = sim != null && sim.IsReadyToRun();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Run Dungeon Simulation (Editor)", GUILayout.Height(30)))
            {
                sim.RunDungeonSimulation();
            }
        }

        if (!ready)
        {
            EditorGUILayout.HelpBox(
                "Settings / AllyRoster / MonsterPool / BattleSimulator(같은 GameObject) 모두 준비되어야 실행 가능합니다.\n" +
                "BattleSimulator 컴포넌트를 같은 GameObject에 추가하거나 필드에 직접 할당하세요.",
                MessageType.Info);
        }
    }
}
#endif
