using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-10-21] 저장 파일이 이름으로 다시 찾는 에셋 목록 (Resources/SaveAssetRegistry.asset).
/// 종의 기억·스킬·직업 패스 등은 Resources 밖(Scripts/Battle/Data/...)에 있어서, 게임을 새로 켜고 이어하기를 하면
/// 아직 메모리에 없는 에셋을 찾지 못했다 (예: 기억 3개 중 1개만 돌아옴 → 최대 HP 가 1 줄어듦).
/// SaveSystem.Find 가 Resources·불러온 에셋에서 못 찾으면 여기서 찾는다.
/// 목록은 에디터에서 자동으로 채운다 (Tools/Abyssdawn/Refresh Save Asset Registry, 에셋을 추가·삭제하면 자동).
/// </summary>
public class SaveAssetRegistry : ScriptableObject
{
    public const string ResourcePath = "SaveAssetRegistry";

    public List<ScriptableObject> assets = new List<ScriptableObject>();

    private static SaveAssetRegistry _instance;

    public static T Find<T>(string name) where T : Object
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_instance == null) _instance = Resources.Load<SaveAssetRegistry>(ResourcePath);
        if (_instance == null) return null;
        foreach (var a in _instance.assets)
            if (a != null && a.name == name && a is T) return a as T;
        return null;
    }
}
