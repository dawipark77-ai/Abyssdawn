#if UNITY_EDITOR
// Tools/Abyssdawn/Setup Inventory UI References 메뉴에서 실행.
// InventoryUIManager의 mainStatText / statusEffectRow / priceText를 씬에 생성하고
// StatusEffect_Prefab 프리팹을 Assets/Prefab/ 에 생성한 뒤 SerializedObject로 연결합니다.

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class InventoryUISetupTool
{
    [MenuItem("Tools/Abyssdawn/Setup Inventory UI References", priority = 260)]
    static void SetupInventoryUIReferences()
    {
        var manager = Object.FindFirstObjectByType<InventoryUIManager>();
        if (manager == null)
        {
            Debug.LogError("[InventoryUISetup] InventoryUIManager를 씬에서 찾을 수 없습니다." +
                           " 던전 씬을 열고 다시 실행하세요.");
            return;
        }

        var so = new SerializedObject(manager);

        var detailPanel = manager.detailPanel;
        if (detailPanel == null)
        {
            Debug.LogError("[InventoryUISetup] manager.detailPanel이 null입니다.");
            return;
        }

        // 폰트 에셋 로드 (씬 기존 TMP와 동일한 GUID)
        TMP_FontAsset font = LoadFont("8f586378b4e144a9851e7b34d9b748ee");

        // ── 1. MainStatText ────────────────────────────────────────────
        var mainStatText = EnsureTMP(detailPanel, "MainStatText", font, 48,
                                     TextAlignmentOptions.Center,
                                     new Color(0.95f, 0.85f, 0.35f, 1f));
        PositionAfterChild(detailPanel.transform, mainStatText.transform, "ItemInfoRow");
        SetRectFull(mainStatText.GetComponent<RectTransform>(), 0f, 56f);

        // ── 2. StatusEffectRow ─────────────────────────────────────────
        var statusEffectRow = EnsureContainer(detailPanel, "StatusEffectRow");
        PositionAfterChild(detailPanel.transform, statusEffectRow.transform, "StatList");
        var hlg = statusEffectRow.GetComponent<HorizontalLayoutGroup>()
               ?? statusEffectRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 8f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.padding                = new RectOffset(8, 8, 4, 4);
        SetRectFull(statusEffectRow.GetComponent<RectTransform>(), 0f, 48f);

        // ── 3. PriceText ───────────────────────────────────────────────
        var priceText = EnsureTMP(detailPanel, "PriceText", font, 26,
                                  TextAlignmentOptions.Right,
                                  new Color(0.85f, 0.75f, 0.40f, 1f));
        PositionAfterChild(detailPanel.transform, priceText.transform, "StatusEffectRow");
        SetRectFull(priceText.GetComponent<RectTransform>(), 0f, 36f);
        priceText.text = "— G";

        // ── 4. StatusEffect_Prefab ─────────────────────────────────────
        GameObject prefabAsset = EnsureStatusEffectPrefab(font);

        // ── 5. 필드 연결 ───────────────────────────────────────────────
        so.Update();
        so.FindProperty("mainStatText")      .objectReferenceValue = mainStatText;
        so.FindProperty("statusEffectRow")   .objectReferenceValue = statusEffectRow.transform;
        so.FindProperty("priceText")         .objectReferenceValue = priceText;
        so.FindProperty("statusEffectPrefab").objectReferenceValue = prefabAsset;
        so.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[InventoryUISetup] 완료! Ctrl+S로 씬을 저장하세요.");
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    static TextMeshProUGUI EnsureTMP(GameObject parent, string name,
                                     TMP_FontAsset font, float fontSize,
                                     TextAlignmentOptions align, Color color)
    {
        var existing = parent.transform.Find(name);
        if (existing != null)
        {
            var existingTMP = existing.GetComponent<TextMeshProUGUI>();
            if (existingTMP != null) return existingTMP;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);

        var tmp       = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize  = fontSize;
        tmp.alignment = align;
        tmp.color     = color;
        if (font != null) tmp.font = font;

        return tmp;
    }

    static GameObject EnsureContainer(GameObject parent, string name)
    {
        var existing = parent.transform.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void PositionAfterChild(Transform parent, Transform target, string siblingName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == siblingName)
            {
                target.SetSiblingIndex(i + 1);
                return;
            }
        }
        int actionIdx = FindSiblingIndex(parent, "ActionRow");
        target.SetSiblingIndex(actionIdx >= 0 ? actionIdx : parent.childCount - 1);
    }

    static int FindSiblingIndex(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return i;
        return -1;
    }

    static void SetRectFull(RectTransform rt, float xOffset, float height)
    {
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.sizeDelta        = new Vector2(xOffset, height);
        rt.anchoredPosition = Vector2.zero;
    }

    static GameObject EnsureStatusEffectPrefab(TMP_FontAsset font)
    {
        const string prefabPath = "Assets/Prefab/StatusEffect_Prefab.prefab";

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        // 루트
        var root    = new GameObject("StatusEffect_Prefab", typeof(RectTransform),
                                     typeof(HorizontalLayoutGroup));
        var rootHlg = root.GetComponent<HorizontalLayoutGroup>();
        rootHlg.spacing                = 4f;
        rootHlg.childAlignment         = TextAnchor.MiddleLeft;
        rootHlg.childForceExpandWidth  = false;
        rootHlg.childForceExpandHeight = false;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(80f, 40f);

        // ItemIcon
        var itemIconGO = new GameObject("ItemIcon", typeof(RectTransform),
                                        typeof(CanvasRenderer), typeof(Image));
        itemIconGO.transform.SetParent(root.transform, false);
        var le1 = itemIconGO.AddComponent<LayoutElement>();
        le1.preferredWidth  = 32f;
        le1.preferredHeight = 32f;
        le1.flexibleWidth   = 0f;

        // FlatIcon
        var flatIconGO = new GameObject("FlatIcon", typeof(RectTransform),
                                        typeof(CanvasRenderer), typeof(Image));
        flatIconGO.transform.SetParent(root.transform, false);
        var le2 = flatIconGO.AddComponent<LayoutElement>();
        le2.preferredWidth  = 24f;
        le2.preferredHeight = 24f;
        le2.flexibleWidth   = 0f;

        // ChanceText
        var chanceGO  = new GameObject("ChanceText", typeof(RectTransform),
                                       typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        chanceGO.transform.SetParent(root.transform, false);
        var tmp       = chanceGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = "0%";
        tmp.fontSize  = 20f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color     = new Color(0.95f, 0.85f, 0.35f, 1f);
        if (font != null) tmp.font = font;
        var le3 = chanceGO.AddComponent<LayoutElement>();
        le3.preferredWidth = 40f;
        le3.flexibleWidth  = 0f;

        bool success;
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
        Object.DestroyImmediate(root);

        if (!success)
            Debug.LogError($"[InventoryUISetup] StatusEffect_Prefab 저장 실패: {prefabPath}");
        else
            Debug.Log($"[InventoryUISetup] StatusEffect_Prefab 생성: {prefabPath}");

        return savedPrefab;
    }

    static TMP_FontAsset LoadFont(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }
}
#endif
