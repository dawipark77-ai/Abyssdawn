#if UNITY_EDITOR
using Abyssdawn;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleSimulator))]
public class BattleSimulatorEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!HasRosters()))
        {
            if (GUILayout.Button("Run Battle Simulation (Editor)", GUILayout.Height(28)))
            {
                ((BattleSimulator)target).RunSimulation();
            }
        }

        if (!HasRosters())
            EditorGUILayout.HelpBox("Ally / Enemy Roster를 모두 할당한 뒤 버튼을 누르세요. (플레이 모드 없이 실행 가능)", MessageType.Info);
    }

    bool HasRosters()
    {
        var sim = (BattleSimulator)target;
        return sim != null && sim.HasRostersAssigned();
    }
}
#endif
