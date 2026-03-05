using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class FixScrollViewContent : EditorWindow
{
    [MenuItem("Tools/Fix ScrollView Content")]
    public static void ShowWindow()
    {
        GetWindow<FixScrollViewContent>("Fix ScrollView Content");
    }

    void OnGUI()
    {
        GUILayout.Label("ScrollView Content 자동 수정", EditorStyles.boldLabel);
        
        if (GUILayout.Button("SkillsScrollView 수정"))
        {
            FixSkillsScrollView();
        }
    }

    void FixSkillsScrollView()
    {
        // SkillsScrollView 찾기
        GameObject scrollViewObj = GameObject.Find("SkillsScrollView");
        if (scrollViewObj == null)
        {
            Debug.LogError("[FixScrollViewContent] SkillsScrollView를 찾을 수 없습니다!");
            return;
        }

        ScrollRect scrollRect = scrollViewObj.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogError("[FixScrollViewContent] ScrollRect 컴포넌트를 찾을 수 없습니다!");
            return;
        }

        // 1. ScrollRect 설정 수정
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        Debug.Log("[FixScrollViewContent] ScrollRect: Horizontal OFF, Vertical ON");

        // 2. Viewport 찾기 및 확인
        RectTransform viewport = scrollRect.viewport;
        if (viewport == null)
        {
            Debug.LogError("[FixScrollViewContent] Viewport를 찾을 수 없습니다!");
            return;
        }

        // Viewport의 Anchor 설정 확인 및 수정 (가장 중요!)
        RectTransform viewportRT = viewport;
        bool viewportNeedsFix = (viewportRT.anchorMin != Vector2.zero || viewportRT.anchorMax != Vector2.one);
        
        if (viewportNeedsFix)
        {
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            Debug.Log("[FixScrollViewContent] ✅ Viewport Anchor를 Stretch로 설정했습니다. (이게 핵심!)");
        }
        else
        {
            Debug.Log("[FixScrollViewContent] Viewport Anchor는 이미 올바르게 설정되어 있습니다.");
        }
        
        // Viewport의 크기가 0인지 확인
        if (viewportRT.rect.width == 0 || viewportRT.rect.height == 0)
        {
            Debug.LogWarning($"[FixScrollViewContent] ⚠️ Viewport 크기가 0입니다! Width: {viewportRT.rect.width}, Height: {viewportRT.rect.height}");
            Debug.LogWarning("[FixScrollViewContent] Viewport의 Anchor를 (0,0)~(1,1)로 설정하고, Left/Right/Top/Bottom을 모두 0으로 설정하세요!");
        }

        // 3. Content 찾기 및 확인
        RectTransform content = scrollRect.content;
        if (content == null)
        {
            Debug.LogError("[FixScrollViewContent] Content를 찾을 수 없습니다!");
            return;
        }

        // Content의 ContentSizeFitter 확인
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            Debug.Log("[FixScrollViewContent] ContentSizeFitter를 추가했습니다.");
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Debug.Log("[FixScrollViewContent] ContentSizeFitter 설정 완료");

        // Content의 VerticalLayoutGroup 확인
        VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            Debug.Log("[FixScrollViewContent] VerticalLayoutGroup을 추가했습니다.");
        }
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        Debug.Log("[FixScrollViewContent] VerticalLayoutGroup 설정 완료");

        // Content의 Anchor 설정 (Top-Left)
        RectTransform contentRT = content;
        if (contentRT.anchorMin != new Vector2(0, 1) || contentRT.anchorMax != new Vector2(0, 1))
        {
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(0, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            Debug.Log("[FixScrollViewContent] Content Anchor를 Top-Left로 설정했습니다.");
        }

        // Content의 초기 Height 설정 (0이면 안 보일 수 있음)
        if (contentRT.sizeDelta.y == 0 && contentRT.childCount > 0)
        {
            // 자식들의 높이를 계산해서 설정
            float totalHeight = 0;
            foreach (RectTransform child in contentRT)
            {
                if (child.gameObject.activeSelf)
                {
                    totalHeight += child.sizeDelta.y + layoutGroup.spacing;
                }
            }
            totalHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
            contentRT.sizeDelta = new Vector2(contentRT.sizeDelta.x, totalHeight);
            Debug.Log($"[FixScrollViewContent] Content Height를 {totalHeight}로 설정했습니다.");
        }

        // 4. ScrollRect 참조 다시 설정
        scrollRect.content = contentRT;
        scrollRect.viewport = viewportRT;

        // Vertical Scrollbar 찾기
        Scrollbar verticalScrollbar = scrollRect.verticalScrollbar;
        if (verticalScrollbar == null)
        {
            Transform scrollbarTransform = scrollViewObj.transform.Find("Scrollbar Vertical");
            if (scrollbarTransform != null)
            {
                verticalScrollbar = scrollbarTransform.GetComponent<Scrollbar>();
                scrollRect.verticalScrollbar = verticalScrollbar;
                Debug.Log("[FixScrollViewContent] Vertical Scrollbar를 연결했습니다.");
            }
        }

        Debug.Log("[FixScrollViewContent] ✅ 모든 설정 완료!");
        EditorUtility.SetDirty(scrollViewObj);
    }
}

