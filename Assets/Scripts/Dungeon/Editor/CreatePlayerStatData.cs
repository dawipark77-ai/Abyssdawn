using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// PlayerStatData.asset 파일을 자동으로 생성하는 에디터 도구
/// </summary>
public class CreatePlayerStatData : EditorWindow
{
    [MenuItem("Tools/Player Data/Create PlayerStatData Asset")]
    public static void CreateAsset()
    {
        // Resources 폴더 경로
        string folderPath = "Assets/Resources";
        
        // Resources 폴더가 없으면 생성
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
            Debug.Log("[CreatePlayerStatData] Resources 폴더 생성됨");
        }
        
        string assetPath = folderPath + "/PlayerStatData.asset";
        
        // 이미 있는지 확인
        PlayerStatData existing = AssetDatabase.LoadAssetAtPath<PlayerStatData>(assetPath);
        if (existing != null)
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "PlayerStatData 이미 존재", 
                "PlayerStatData.asset 파일이 이미 존재합니다.\n덮어쓰시겠습니까?", 
                "덮어쓰기", 
                "취소"
            );
            
            if (!overwrite)
            {
                Debug.Log("[CreatePlayerStatData] 취소됨");
                return;
            }
        }
        
        // PlayerStatData 생성
        PlayerStatData asset = ScriptableObject.CreateInstance<PlayerStatData>();
        
        // 초기값 설정
        asset.currentHP = 100;
        asset.currentMP = 10;
        asset.level = 1;
        asset.exp = 0;
        asset.skillPoints = 5; // 테스트용 5개
        
        // 에셋 생성
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 생성된 에셋 선택
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
        
        EditorUtility.DisplayDialog(
            "생성 완료!", 
            $"PlayerStatData.asset 파일이 생성되었습니다!\n\n위치: {assetPath}\n초기 LP: 5개\n\nInspector에서 Skill Points를 조정하여 테스트하세요!", 
            "확인"
        );
        
        Debug.Log($"[CreatePlayerStatData] ✅ PlayerStatData.asset 생성 완료 - {assetPath}");
    }
    
    [MenuItem("Tools/Player Data/Open PlayerStatData")]
    public static void OpenAsset()
    {
        string assetPath = "Assets/Resources/PlayerStatData.asset";
        PlayerStatData asset = AssetDatabase.LoadAssetAtPath<PlayerStatData>(assetPath);
        
        if (asset == null)
        {
            bool create = EditorUtility.DisplayDialog(
                "파일 없음", 
                "PlayerStatData.asset 파일이 없습니다.\n생성하시겠습니까?", 
                "생성", 
                "취소"
            );
            
            if (create)
            {
                CreateAsset();
            }
        }
        else
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            Debug.Log($"[CreatePlayerStatData] PlayerStatData 열림 - LP: {asset.skillPoints}개");
        }
    }
}










