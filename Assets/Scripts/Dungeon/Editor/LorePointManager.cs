using UnityEngine;
using UnityEditor;

/// <summary>
/// LP(Lore Point) 관리 에디터 도구
///
/// [2026-05-07] skillPoints가 PlayerStatData(SO)에서 PlayerStats(컴포넌트)로 분리됨.
/// Edit 모드에서는 씬의 PlayerStats GameObject가 있어야 LP 값 읽기/쓰기 가능.
/// Play 모드에서는 런타임 PlayerStats 인스턴스에 직접 적용됨.
/// learnedSkills 등 SO에 남은 데이터는 PlayerStatData를 통해 그대로 편집.
/// </summary>
public class LorePointManager : EditorWindow
{
    [MenuItem("Tools/Skill Tree/Manage Lore Points")]
    public static void ShowWindow()
    {
        GetWindow<LorePointManager>("LP 관리");
    }

    private PlayerStatData playerStatData;
    private int pointsToAdd = 1;

    // ─── skillPoints 헬퍼 (PlayerStats 컴포넌트 위임) ─────────
    private static PlayerStats FindPlayerStatsInScene()
        => Object.FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);

    private static int GetSkillPoints()
    {
        var ps = FindPlayerStatsInScene();
        return ps != null ? ps.skillPoints : 0;
    }

    private static bool SetSkillPoints(int value)
    {
        var ps = FindPlayerStatsInScene();
        if (ps == null) return false;
        ps.skillPoints = value;
        EditorUtility.SetDirty(ps);
        return true;
    }

    private void OnGUI()
    {
        GUILayout.Label("Lore Point (LP) 관리", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // PlayerStatData 자동 로드 (learnedSkills 등 SO 데이터 편집용)
        if (playerStatData == null)
        {
            playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
        }

        if (playerStatData == null)
        {
            EditorGUILayout.HelpBox("PlayerStatData를 찾을 수 없습니다!\nResources/PlayerStatData.asset 파일을 확인하세요.", MessageType.Error);
            return;
        }

        // PlayerStats(컴포넌트) 존재 여부 확인 — skillPoints 편집 가능 여부
        var psInScene = FindPlayerStatsInScene();
        if (psInScene == null)
        {
            EditorGUILayout.HelpBox(
                "씬에 PlayerStats 컴포넌트가 없어 LP를 편집할 수 없습니다.\n" +
                "[2026-05-07] skillPoints는 PlayerStats(컴포넌트)가 보유합니다.\n" +
                "씬에 Player GameObject를 추가하거나 Play 모드에서 실행하세요.",
                MessageType.Warning);
        }

        // 현재 LP 표시 (PlayerStats 우선, 없으면 0)
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label($"현재 LP: {GetSkillPoints()}", EditorStyles.largeLabel);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // LP 추가/제거
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("추가할 LP:", GUILayout.Width(100));
        pointsToAdd = EditorGUILayout.IntField(pointsToAdd, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button($"LP +{pointsToAdd} 추가", GUILayout.Height(30)))
        {
            int newValue = GetSkillPoints() + pointsToAdd;
            if (SetSkillPoints(newValue))
                Debug.Log($"[LorePointManager] ✅ LP +{pointsToAdd} 추가! 현재 LP: {GetSkillPoints()}");
            else
                Debug.LogWarning("[LorePointManager] PlayerStats 컴포넌트가 씬에 없어 적용 실패");
        }

        if (GUILayout.Button($"LP -{pointsToAdd} 제거", GUILayout.Height(30)))
        {
            int newValue = Mathf.Max(0, GetSkillPoints() - pointsToAdd);
            if (SetSkillPoints(newValue))
                Debug.Log($"[LorePointManager] ✅ LP -{pointsToAdd} 제거! 현재 LP: {GetSkillPoints()}");
            else
                Debug.LogWarning("[LorePointManager] PlayerStats 컴포넌트가 씬에 없어 적용 실패");
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // 빠른 설정 버튼들
        EditorGUILayout.LabelField("빠른 설정:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("LP = 0", GUILayout.Height(25)))
        {
            if (SetSkillPoints(0))
                Debug.Log("[LorePointManager] ✅ LP = 0으로 설정!");
        }

        if (GUILayout.Button("LP = 1", GUILayout.Height(25)))
        {
            if (SetSkillPoints(1))
                Debug.Log("[LorePointManager] ✅ LP = 1로 설정! (Basic Swordsmanship 배울 수 있음)");
        }

        if (GUILayout.Button("LP = 3", GUILayout.Height(25)))
        {
            if (SetSkillPoints(3))
                Debug.Log("[LorePointManager] ✅ LP = 3으로 설정!");
        }

        if (GUILayout.Button("LP = 6", GUILayout.Height(25)))
        {
            if (SetSkillPoints(6))
                Debug.Log("[LorePointManager] ✅ LP = 6으로 설정! (Tier 1 전부 배울 수 있음)");
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(15);

        // 스킬 트리 리셋
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("위험 구역", EditorStyles.boldLabel);

        if (GUILayout.Button("🔄 스킬 트리 리셋 (배운 스킬 모두 삭제)", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("스킬 트리 리셋", "정말로 배운 스킬을 모두 삭제하시겠습니까?\n\nLP는 유지됩니다.", "리셋", "취소"))
            {
                if (playerStatData.learnedSkills != null)
                {
                    int count = playerStatData.learnedSkills.Count;
                    playerStatData.learnedSkills.Clear();
                    EditorUtility.SetDirty(playerStatData);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[LorePointManager] ✅ 스킬 트리 리셋 완료! ({count}개 스킬 삭제)");
                }
            }
        }

        if (GUILayout.Button("🗑️ 완전 리셋 (LP + 배운 스킬 모두 삭제)", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("완전 리셋", "정말로 LP와 배운 스킬을 모두 삭제하시겠습니까?", "리셋", "취소"))
            {
                SetSkillPoints(0);
                if (playerStatData.learnedSkills != null)
                {
                    playerStatData.learnedSkills.Clear();
                }
                EditorUtility.SetDirty(playerStatData);
                AssetDatabase.SaveAssets();
                Debug.Log("[LorePointManager] ✅ 완전 리셋 완료!");
            }
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 배운 스킬 목록
        if (playerStatData.learnedSkills != null && playerStatData.learnedSkills.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"배운 스킬 ({playerStatData.learnedSkills.Count}개):", EditorStyles.boldLabel);

            foreach (var skill in playerStatData.learnedSkills)
            {
                if (skill != null)
                {
                    EditorGUILayout.LabelField($"  • {skill.skillName}");
                }
            }

            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("아직 배운 스킬이 없습니다.", MessageType.Info);
        }
    }
}
