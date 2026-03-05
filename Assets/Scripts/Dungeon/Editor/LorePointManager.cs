using UnityEngine;
using UnityEditor;

/// <summary>
/// LP(Lore Point) 관리 에디터 도구
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
    
    private void OnGUI()
    {
        GUILayout.Label("Lore Point (LP) 관리", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        // PlayerStatData 자동 로드
        if (playerStatData == null)
        {
            playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
        }
        
        if (playerStatData == null)
        {
            EditorGUILayout.HelpBox("PlayerStatData를 찾을 수 없습니다!\nResources/PlayerStatData.asset 파일을 확인하세요.", MessageType.Error);
            return;
        }
        
        // 현재 LP 표시
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label($"현재 LP: {playerStatData.skillPoints}", EditorStyles.largeLabel);
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
            playerStatData.skillPoints += pointsToAdd;
            EditorUtility.SetDirty(playerStatData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LorePointManager] ✅ LP +{pointsToAdd} 추가! 현재 LP: {playerStatData.skillPoints}");
        }
        
        if (GUILayout.Button($"LP -{pointsToAdd} 제거", GUILayout.Height(30)))
        {
            playerStatData.skillPoints = Mathf.Max(0, playerStatData.skillPoints - pointsToAdd);
            EditorUtility.SetDirty(playerStatData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LorePointManager] ✅ LP -{pointsToAdd} 제거! 현재 LP: {playerStatData.skillPoints}");
        }
        
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 빠른 설정 버튼들
        EditorGUILayout.LabelField("빠른 설정:", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("LP = 0", GUILayout.Height(25)))
        {
            playerStatData.skillPoints = 0;
            EditorUtility.SetDirty(playerStatData);
            AssetDatabase.SaveAssets();
            Debug.Log("[LorePointManager] ✅ LP = 0으로 설정!");
        }
        
        if (GUILayout.Button("LP = 1", GUILayout.Height(25)))
        {
            playerStatData.skillPoints = 1;
            EditorUtility.SetDirty(playerStatData);
            AssetDatabase.SaveAssets();
            Debug.Log("[LorePointManager] ✅ LP = 1로 설정! (Basic Swordsmanship 배울 수 있음)");
        }
        
        if (GUILayout.Button("LP = 3", GUILayout.Height(25)))
        {
            playerStatData.skillPoints = 3;
            EditorUtility.SetDirty(playerStatData);
            AssetDatabase.SaveAssets();
            Debug.Log("[LorePointManager] ✅ LP = 3으로 설정!");
        }
        
        if (GUILayout.Button("LP = 6", GUILayout.Height(25)))
        {
            playerStatData.skillPoints = 6;
            EditorUtility.SetDirty(playerStatData);
            AssetDatabase.SaveAssets();
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
                playerStatData.skillPoints = 0;
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








