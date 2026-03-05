using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// 해금한 스킬 목록을 SkillListItem으로 표시하는 매니저
/// </summary>
public class SkillListManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SkillListItem 프레팹")]
    public GameObject skillListItemPrefab;
    
    [Tooltip("Content (스킬 리스트가 들어갈 부모)")]
    public RectTransform content;
    
    [Header("스킬 트리 소스 선택")]
    [Tooltip("LoreSkillTree 패널 (모든 스킬 트리 매니저를 찾을 컨테이너)")]
    public Transform loreSkillTreeContainer;
    
    [Tooltip("특정 스킬 트리 매니저 (LoreSkillTree가 없을 때 사용)")]
    public SwordSkillTreeManager specificSkillTreeManager;
    
    [Tooltip("PlayerStatData (대체 방법)")]
    public PlayerStatData playerStatData;
    
    [Header("Settings")]
    [Tooltip("시작 시 자동으로 스킬 리스트 업데이트")]
    public bool autoUpdateOnStart = true;
    
    private List<GameObject> currentSkillItems = new List<GameObject>();
    
    private void Awake()
    {
        // 자동으로 참조 찾기
        if (content == null)
        {
            // Content 찾기
            Transform viewport = transform.Find("Viewport");
            if (viewport != null)
            {
                content = viewport.Find("Content")?.GetComponent<RectTransform>();
            }
            
            // Viewport가 없으면 직접 Content 찾기
            if (content == null)
            {
                content = transform.Find("Content")?.GetComponent<RectTransform>();
            }
            
            // SkillsScrollView 안에서 찾기
            if (content == null)
            {
                Transform scrollView = transform.Find("SkillsScrollView");
                if (scrollView != null)
                {
                    Transform viewportTransform = scrollView.Find("Viewport");
                    if (viewportTransform != null)
                    {
                        content = viewportTransform.Find("Content")?.GetComponent<RectTransform>();
                    }
                }
            }
        }
        
        // LoreSkillTree 컨테이너 자동 찾기
        if (loreSkillTreeContainer == null)
        {
            // 방법 1: "LoreSkillTree" 이름으로 찾기
            GameObject loreTree = GameObject.Find("LoreSkillTree");
            if (loreTree != null)
            {
                loreSkillTreeContainer = loreTree.transform;
            }
            
            // 방법 2: "LoreTree_Panel" 안에서 찾기
            if (loreSkillTreeContainer == null)
            {
                GameObject loreTreePanel = GameObject.Find("LoreTree_Panel");
                if (loreTreePanel != null)
                {
                    Transform loreTreeTransform = loreTreePanel.transform.Find("LoreSkillTree");
                    if (loreTreeTransform != null)
                    {
                        loreSkillTreeContainer = loreTreeTransform;
                    }
                    else
                    {
                        // LoreTree_Panel 자체를 컨테이너로 사용
                        loreSkillTreeContainer = loreTreePanel.transform;
                    }
                }
            }
        }
        
        // 특정 매니저가 없으면 자동 찾기 (하위 호환성)
        if (specificSkillTreeManager == null)
        {
            specificSkillTreeManager = FindObjectOfType<SwordSkillTreeManager>();
        }
        
        if (playerStatData == null)
        {
            playerStatData = Resources.Load<PlayerStatData>("HeroData");
            if (playerStatData == null)
            {
                playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
            }
        }
        
        // 프레팹 자동 찾기
        if (skillListItemPrefab == null)
        {
            skillListItemPrefab = Resources.Load<GameObject>("Prefab/SkillListItem");
            if (skillListItemPrefab == null)
            {
                // 다른 경로 시도
                skillListItemPrefab = Resources.Load<GameObject>("SkillListItem");
            }
        }
    }
    
    private void Start()
    {
        if (autoUpdateOnStart)
        {
            RefreshSkillList();
        }
    }

    /// <summary>
    /// 패널이 다시 켜질 때마다 최신 스킬 상태로 갱신
    /// </summary>
    private void OnEnable()
    {
        if (autoUpdateOnStart && Application.isPlaying)
        {
            RefreshSkillList();
        }
    }
    
    /// <summary>
    /// 스킬 리스트 새로고침 (해금한 스킬 목록으로 업데이트)
    /// </summary>
    public void RefreshSkillList()
    {
        if (content == null)
        {
            Debug.LogError("[SkillListManager] Content가 설정되지 않았습니다!");
            return;
        }
        
        if (skillListItemPrefab == null)
        {
            Debug.LogError("[SkillListManager] SkillListItem 프레팹이 설정되지 않았습니다!");
            return;
        }
        
        // 해금한 스킬 목록 가져오기
        List<SkillData> learnedSkills = GetLearnedSkills();
        
        Debug.Log($"[SkillListManager] 해금한 스킬 {learnedSkills.Count}개 발견");
        
        // 기존 아이템 제거
        ClearSkillList();
        
        // 새 아이템 생성
        foreach (var skill in learnedSkills)
        {
            if (skill != null)
            {
                CreateSkillItem(skill);
            }
        }
        
        Debug.Log($"[SkillListManager] ✅ 스킬 리스트 업데이트 완료 ({currentSkillItems.Count}개)");
    }
    
    /// <summary>
    /// 해금한 스킬 목록 가져오기 (모든 스킬 트리에서 통합)
    /// </summary>
    private List<SkillData> GetLearnedSkills()
    {
        List<SkillData> allSkills = new List<SkillData>();
        HashSet<string> skillIDs = new HashSet<string>(); // 중복 제거용
        
        // 방법 1: LoreSkillTree 컨테이너에서 모든 스킬 트리 매니저 찾기
        if (loreSkillTreeContainer != null)
        {
            Debug.Log($"[SkillListManager] LoreSkillTree 컨테이너에서 스킬 트리 매니저 검색: {loreSkillTreeContainer.name}");
            
            // 모든 자식에서 스킬 트리 매니저 찾기
            var skillTreeManagers = loreSkillTreeContainer.GetComponentsInChildren<MonoBehaviour>(true);
            
            foreach (var manager in skillTreeManagers)
            {
                if (manager == null) continue;
                
                // GetLearnedSkills() 메서드가 있는지 확인 (리플렉션 사용)
                var method = manager.GetType().GetMethod("GetLearnedSkills");
                if (method != null)
                {
                    try
                    {
                        var result = method.Invoke(manager, null);
                        if (result is List<SkillData> skills)
                        {
                            foreach (var skill in skills)
                            {
                                if (skill != null && !skillIDs.Contains(skill.skillID))
                                {
                                    allSkills.Add(skill);
                                    skillIDs.Add(skill.skillID);
                                }
                            }
                            Debug.Log($"[SkillListManager] {manager.GetType().Name}에서 {skills.Count}개 스킬 가져옴");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[SkillListManager] {manager.GetType().Name}에서 스킬 가져오기 실패: {e.Message}");
                    }
                }
            }
            
            if (allSkills.Count > 0)
            {
                Debug.Log($"[SkillListManager] ✅ LoreSkillTree에서 총 {allSkills.Count}개 스킬 가져옴");
                return allSkills;
            }
        }
        
        // 방법 2: 특정 스킬 트리 매니저에서 가져오기 (하위 호환성)
        if (specificSkillTreeManager != null)
        {
            var skills = specificSkillTreeManager.GetLearnedSkills();
            if (skills != null && skills.Count > 0)
            {
                Debug.Log($"[SkillListManager] {specificSkillTreeManager.GetType().Name}에서 {skills.Count}개 스킬 가져옴");
                return skills;
            }
        }
        
        // 방법 3: PlayerStatData에서 직접 가져오기
        if (playerStatData != null && playerStatData.learnedSkills != null)
        {
            allSkills = new List<SkillData>(playerStatData.learnedSkills);
            Debug.Log($"[SkillListManager] PlayerStatData에서 {allSkills.Count}개 스킬 가져옴");
            return allSkills;
        }
        
        Debug.LogWarning("[SkillListManager] 해금한 스킬을 찾을 수 없습니다!");
        return allSkills;
    }
    
    /// <summary>
    /// 스킬 아이템 생성
    /// </summary>
    private void CreateSkillItem(SkillData skill)
    {
        if (skill == null || content == null || skillListItemPrefab == null) return;
        
        // 프레팹 인스턴스화
        GameObject itemObj = Instantiate(skillListItemPrefab, content);
        itemObj.name = $"SkillListItem_{skill.skillName}";
        
        // SkillListItem 컴포넌트에 스킬 데이터 설정
        SkillListItem item = itemObj.GetComponent<SkillListItem>();
        if (item == null)
        {
            item = itemObj.AddComponent<SkillListItem>();
        }
        
        item.SetSkillData(skill);
        
        // 리스트에 추가
        currentSkillItems.Add(itemObj);
    }
    
    /// <summary>
    /// 기존 스킬 리스트 제거
    /// </summary>
    private void ClearSkillList()
    {
        foreach (var item in currentSkillItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        currentSkillItems.Clear();
    }
    
    /// <summary>
    /// 수동 새로고침 (에디터용)
    /// </summary>
    [ContextMenu("Refresh Skill List")]
    public void ManualRefresh()
    {
        RefreshSkillList();
    }
}

