#if UNITY_EDITOR
using Abyssdawn;
using UnityEditor;
using UnityEngine;

namespace Abyssdawn.Editor
{
/// <summary>
/// 플레이 모드 없이 기본 Roster로 시뮬 1000회 실행 → 리포트 파일 갱신.
/// 배치: Unity.exe -batchmode -quit -projectPath "..." -executeMethod Abyssdawn.Editor.BattleSimHeadlessRunner.RunDefault1000FromCli
/// </summary>
public static class BattleSimHeadlessRunner
{
    const string AllyPath = "Assets/Data/Simulation/BattleSim_DefaultAllyRoster.asset";
    const string EnemyPath = "Assets/Data/Simulation/BattleSim_DefaultEnemyRoster.asset";

    [MenuItem("Abyssdawn/Simulation/Run Default Roster Sim (1000)")]
    public static void RunDefault1000FromMenu()
    {
        RunInternal(quitAfter: false);
    }

    /// <summary>커맨드라인 배치 실행용 (성공 시 Exit 0).</summary>
    public static void RunDefault1000FromCli()
    {
        RunInternal(quitAfter: true);
    }

    static void RunInternal(bool quitAfter)
    {
        var ally = AssetDatabase.LoadAssetAtPath<BattleSimAllyRoster>(AllyPath);
        var enemy = AssetDatabase.LoadAssetAtPath<BattleSimEnemyRoster>(EnemyPath);
        if (ally == null || enemy == null)
        {
            Debug.LogError("[BattleSimHeadlessRunner] Roster SO 로드 실패.");
            if (quitAfter) EditorApplication.Exit(1);
            return;
        }

        var go = new GameObject("BattleSimHeadlessTemp");
        var sim = go.AddComponent<BattleSimulator>();
        var so = new SerializedObject(sim);
        so.FindProperty("allyRoster").objectReferenceValue = ally;
        so.FindProperty("enemyRoster").objectReferenceValue = enemy;
        so.FindProperty("iterations").intValue = 1000;
        so.FindProperty("baseSeed").intValue = 12345;
        so.FindProperty("writeReportFile").boolValue = true;
        so.FindProperty("reportRelativePath").stringValue = "Assets/Data/Simulation/_LastBattleSimReport.txt";
        so.ApplyModifiedPropertiesWithoutUndo();

        sim.RunSimulation();
        Object.DestroyImmediate(go);

        if (quitAfter)
            EditorApplication.Exit(0);
    }
}
}
#endif
