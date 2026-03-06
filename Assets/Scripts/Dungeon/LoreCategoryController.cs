using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Lore Category 버튼들을 클릭하면 해당하는 Lore 페이지를 열고 닫는 컨트롤러
/// 예: Daggers 버튼 → DaggerLore 패널 열기
/// </summary>
public class LoreCategoryController : MonoBehaviour
{
    [System.Serializable]
    public class LorePageMapping
    {
        [Tooltip("카테고리 버튼 (예: Daggers, Swords)")]
        public Button categoryButton;
        
        [Tooltip("해당하는 Lore 페이지 패널 (예: DaggerLore, SwordLore)")]
        public GameObject lorePagePanel;
        
        [Tooltip("버튼 이름 (자동 매칭용, 비워두면 버튼 GameObject 이름 사용)")]
        public string buttonName;
    }

    [Header("Lore 페이지 매핑")]
    [Tooltip("버튼-패널 매핑 리스트")]
    public List<LorePageMapping> lorePageMappings = new List<LorePageMapping>();

    [Header("설정")]
    [Tooltip("한 번에 하나의 페이지만 열리도록 할지 여부")]
    public bool singlePageMode = true;

    [Tooltip("Lore_Page 부모 오브젝트 (자동 검색용)")]
    public Transform lorePageParent;

    private GameObject currentOpenPage = null;

    private void Awake()
    {
        // Lore_Page 부모 자동 찾기
        if (lorePageParent == null)
        {
            lorePageParent = transform.parent?.Find("Lore_Page");
            if (lorePageParent == null)
            {
                GameObject lorePageObj = GameObject.Find("Lore_Page");
                if (lorePageObj != null)
                {
                    lorePageParent = lorePageObj.transform;
                }
            }
        }

        // 매핑이 비어있으면 자동으로 찾기 시도
        if (lorePageMappings.Count == 0)
        {
            AutoMapButtonsToPages();
        }

        // 각 버튼에 클릭 이벤트 연결
        SetupButtonListeners();
    }

    private void Start()
    {
        // 초기 상태: 모든 페이지 닫기
        CloseAllPages();
    }

    /// <summary>
    /// 버튼과 패널을 자동으로 매핑 (이름 기반)
    /// </summary>
    private void AutoMapButtonsToPages()
    {
        if (lorePageParent == null)
        {
            Debug.LogWarning("[LoreCategoryController] Lore_Page 부모를 찾을 수 없습니다. 수동으로 매핑해주세요.");
            return;
        }

        // Lore_Category 안의 모든 버튼 찾기
        Button[] buttons = GetComponentsInChildren<Button>(true);
        
        foreach (Button btn in buttons)
        {
            if (btn == null) continue;

            string buttonName = btn.gameObject.name;
            
            // "Daggers" → "DaggerLore", "Swords" → "SwordLore" 같은 패턴으로 변환
            string pageName = ConvertButtonNameToPageName(buttonName);
            
            // 해당 이름의 패널 찾기
            Transform pageTransform = lorePageParent.Find(pageName);
            if (pageTransform == null)
            {
                // 대소문자 무시하고 찾기
                foreach (Transform child in lorePageParent)
                {
                    if (child.name.Equals(pageName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        pageTransform = child;
                        break;
                    }
                }
            }

            if (pageTransform != null)
            {
                LorePageMapping mapping = new LorePageMapping
                {
                    categoryButton = btn,
                    lorePagePanel = pageTransform.gameObject,
                    buttonName = buttonName
                };
                lorePageMappings.Add(mapping);
                Debug.Log($"[LoreCategoryController] ✅ 자동 매핑: {buttonName} → {pageTransform.name}");
            }
            else
            {
                Debug.LogWarning($"[LoreCategoryController] ⚠️ '{buttonName}' 버튼에 해당하는 '{pageName}' 패널을 찾을 수 없습니다.");
            }
        }
    }

    /// <summary>
    /// 버튼 이름을 페이지 이름으로 변환
    /// 예: "Daggers" → "DaggerLore", "Swords" → "SwordLore"
    /// </summary>
    private string ConvertButtonNameToPageName(string buttonName)
    {
        // 공백 제거 및 정규화
        buttonName = buttonName.Trim();
        
        // 이미 "Lore"가 포함되어 있으면 그대로 사용
        if (buttonName.Contains("Lore") || buttonName.Contains("lore"))
        {
            return buttonName;
        }

        // 복수형 제거 후 "Lore" 추가
        // 예: "Daggers" → "Dagger" → "DaggerLore"
        string singular = buttonName;
        if (singular.EndsWith("s") && singular.Length > 1)
        {
            singular = singular.Substring(0, singular.Length - 1);
        }
        
        // "Two Handed" 같은 경우 처리
        if (singular.Contains("Two Handed") || singular.Contains("Two_Handed"))
        {
            singular = singular.Replace("Two Handed", "TwoHanded").Replace("Two_Handed", "TwoHanded");
        }

        return singular + "Lore";
    }

    /// <summary>
    /// 모든 버튼에 클릭 이벤트 연결
    /// </summary>
    private void SetupButtonListeners()
    {
        foreach (var mapping in lorePageMappings)
        {
            if (mapping.categoryButton == null)
            {
                Debug.LogWarning("[LoreCategoryController] 버튼이 null인 매핑이 있습니다.");
                continue;
            }

            if (mapping.lorePagePanel == null)
            {
                Debug.LogWarning($"[LoreCategoryController] '{mapping.categoryButton.name}' 버튼에 해당하는 패널이 null입니다.");
                continue;
            }

            // 기존 리스너 제거
            mapping.categoryButton.onClick.RemoveAllListeners();
            
            // 새 리스너 추가 (클로저로 인덱스 캡처)
            GameObject pagePanel = mapping.lorePagePanel;
            mapping.categoryButton.onClick.AddListener(() => OnCategoryButtonClicked(pagePanel));
            
            Debug.Log($"[LoreCategoryController] ✅ 버튼 이벤트 연결: {mapping.categoryButton.name} → {pagePanel.name}");
        }
    }

    /// <summary>
    /// 카테고리 버튼 클릭 시 호출
    /// </summary>
    private void OnCategoryButtonClicked(GameObject targetPage)
    {
        if (targetPage == null)
        {
            Debug.LogError("[LoreCategoryController] 타겟 페이지가 null입니다!");
            return;
        }

        bool isCurrentlyOpen = targetPage.activeSelf;

        if (singlePageMode)
        {
            // 다른 모든 페이지 닫기
            CloseAllPages();
        }

        // 토글: 열려있으면 닫고, 닫혀있으면 열기
        if (isCurrentlyOpen)
        {
            targetPage.SetActive(false);
            currentOpenPage = null;
            Debug.Log($"[LoreCategoryController] ❌ {targetPage.name} 닫기");
        }
        else
        {
            targetPage.SetActive(true);
            currentOpenPage = targetPage;
            Debug.Log($"[LoreCategoryController] ✅ {targetPage.name} 열기");
        }
    }

    /// <summary>
    /// 모든 Lore 페이지 닫기
    /// </summary>
    public void CloseAllPages()
    {
        foreach (var mapping in lorePageMappings)
        {
            if (mapping.lorePagePanel != null)
            {
                mapping.lorePagePanel.SetActive(false);
            }
        }
        currentOpenPage = null;
    }

    /// <summary>
    /// 특정 페이지 열기 (외부에서 호출 가능)
    /// </summary>
    public void OpenPage(string pageName)
    {
        foreach (var mapping in lorePageMappings)
        {
            if (mapping.lorePagePanel != null && 
                (mapping.lorePagePanel.name.Equals(pageName, System.StringComparison.OrdinalIgnoreCase) ||
                 (mapping.buttonName != null && mapping.buttonName.Equals(pageName, System.StringComparison.OrdinalIgnoreCase))))
            {
                if (singlePageMode)
                {
                    CloseAllPages();
                }
                mapping.lorePagePanel.SetActive(true);
                currentOpenPage = mapping.lorePagePanel;
                Debug.Log($"[LoreCategoryController] ✅ {mapping.lorePagePanel.name} 열기 (외부 호출)");
                return;
            }
        }
        Debug.LogWarning($"[LoreCategoryController] '{pageName}' 이름의 페이지를 찾을 수 없습니다.");
    }

    /// <summary>
    /// 현재 열려있는 페이지 가져오기
    /// </summary>
    public GameObject GetCurrentOpenPage()
    {
        return currentOpenPage;
    }
}



