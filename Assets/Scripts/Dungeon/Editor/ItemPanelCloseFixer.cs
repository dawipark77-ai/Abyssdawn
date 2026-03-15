#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Tools > Abyssdawn > Add Close Button to Item Panel
/// Item_Equpiment_Panel 우상단에 닫기(X) 버튼을 추가합니다.
/// </summary>
public static class ItemPanelCloseFixer
{
    private const string PANEL_NAME      = "Item_Equpiment_Panel";
    private const string CLOSE_BTN_NAME  = "CloseButton_X";

    [MenuItem("Tools/Abyssdawn/Add Close Button to Item Panel", priority = 202)]
    public static void AddCloseButton()
    {
        GameObject panel = FindInScene(PANEL_NAME);
        if (panel == null)
        {
            EditorUtility.DisplayDialog("ItemPanelCloseFixer",
                $"'{PANEL_NAME}' 오브젝트를 씬에서 찾을 수 없습니다.", "확인");
            return;
        }

        // 이미 닫기 버튼이 있으면 재생성 여부 확인
        Transform existing = panel.transform.Find(CLOSE_BTN_NAME);
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog("ItemPanelCloseFixer",
                "닫기 버튼이 이미 존재합니다. 재생성하시겠습니까?", "재생성", "취소");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // ── 닫기 버튼 생성 ────────────────────────────────────
        GameObject btnObj = new GameObject(CLOSE_BTN_NAME,
                                           typeof(RectTransform),
                                           typeof(Image),
                                           typeof(Button));
        Undo.RegisterCreatedObjectUndo(btnObj, "Add Close Button");
        btnObj.transform.SetParent(panel.transform, false);

        // 우상단 고정 (52×52 px)
        RectTransform rt   = btnObj.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(1f, 1f);
        rt.anchorMax       = new Vector2(1f, 1f);
        rt.pivot           = new Vector2(1f, 1f);
        rt.sizeDelta       = new Vector2(52f, 52f);
        rt.anchoredPosition = new Vector2(-8f, -8f);

        btnObj.GetComponent<Image>().color = new Color(0.60f, 0.10f, 0.10f, 0.90f);

        // "✕" 텍스트
        GameObject textObj = new GameObject("Label",
                                            typeof(RectTransform),
                                            typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin     = Vector2.zero;
        textRT.anchorMax     = Vector2.one;
        textRT.offsetMin     = Vector2.zero;
        textRT.offsetMax     = Vector2.zero;

        TextMeshProUGUI label = textObj.GetComponent<TextMeshProUGUI>();
        label.text            = "✕";
        label.fontSize        = 22;
        label.fontStyle       = FontStyles.Bold;
        label.color           = Color.white;
        label.alignment       = TextAlignmentOptions.Center;

        // onClick → panel 비활성화
        Button btn    = btnObj.GetComponent<Button>();
        var    nav    = Navigation.defaultNavigation;
        nav.mode      = Navigation.Mode.None;
        btn.navigation = nav;

        // UnityEvent에 직접 리스너 등록 (에디터 persistent 이벤트)
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(
            btn.onClick,
            panel.SetActive,
            false);

        // 최상위 자식으로 이동 (항상 앞에 렌더링)
        btnObj.transform.SetAsLastSibling();

        EditorUtility.SetDirty(panel);

        Debug.Log($"[ItemPanelCloseFixer] '{PANEL_NAME}' 우상단에 닫기 버튼 추가 완료.");
        EditorUtility.DisplayDialog("ItemPanelCloseFixer",
            $"'{PANEL_NAME}' 우상단에 닫기(✕) 버튼을 추가했습니다.\n\n씬을 저장해 주세요. (Ctrl+S)",
            "확인");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == name) return go;
        }
        return null;
    }
}
#endif
