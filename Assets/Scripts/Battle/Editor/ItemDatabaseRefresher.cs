#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Resources.LoadAll 방식으로 전환 후 Inspector DB 등록은 더 이상 불필요합니다.
/// 아이템 추가 시 Resources/Equipments 또는 Resources/Items 폴더에 SO 파일을 넣으면 자동 반영됩니다.
/// 기존 SO 파일은 Tools > Abyssdawn > Migrate Items to Resources 로 이동하세요.
/// </summary>
public static class ItemDatabaseRefresher
{
    [MenuItem("Tools/Abyssdawn/Refresh Item Database (구버전 — 사용 안 함)", priority = 211, validate = true)]
    public static bool RefreshDatabase_Validate() => false;   // 항목 비활성화

    [MenuItem("Tools/Abyssdawn/Refresh Item Database (구버전 — 사용 안 함)", priority = 211)]
    public static void RefreshDatabase()
    {
        EditorUtility.DisplayDialog(
            "Refresh Item Database",
            "이 메뉴는 더 이상 사용되지 않습니다.\n\n" +
            "아이템 SO 파일을 Resources 폴더에 넣기만 하면\n" +
            "게임 실행 시 Resources.LoadAll 로 자동 로드됩니다.\n\n" +
            "SO 파일 이동: Tools > Abyssdawn > Migrate Items to Resources",
            "확인");
    }
}
#endif
