using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AbyssdawnBattle;

/// <summary>
/// 스킬 트리의 개별 노드 (스킬 하나)를 관리하는 클래스
/// </summary>
public class SkillTreeNode : MonoBehaviour
{
    [Header("스킬 데이터")]
    [Tooltip("이 노드가 나타내는 스킬 SO")]
    public SkillData skillData;
    
    [Header("선행 스킬")]
    [Tooltip("이 스킬을 배우기 위해 필요한 선행 스킬들")]
    public SkillTreeNode[] prerequisiteNodes;
    
    [Header("필요 스킬 포인트")]
    [Tooltip("이 스킬을 배우는 데 필요한 스킬 포인트")]
    public int requiredSkillPoints = 1;
    
    [Header("UI 참조")]
    [Tooltip("이 노드의 버튼")]
    public Button nodeButton;
    
    [Tooltip("스킬 아이콘 이미지")]
    public Image skillIcon;
    
    [Tooltip("스킬 이름 텍스트")]
    public TextMeshProUGUI skillNameText;
    
    [Tooltip("잠금 상태 표시 오브젝트 (자물쇠 아이콘 등)")]
    public GameObject lockedOverlay;
    
    [Tooltip("배움 상태 표시 (밝은 효과 등)")]
    public GameObject learnedEffect;
    
    [Header("팝업 참조")]
    [Tooltip("스킬 상세 정보 팝업 (수동으로 연결)")]
    public SkillDetailPopup skillDetailPopup;
    
    [Header("시각적 설정")]
    [Tooltip("잠긴 상태 색상 (회색, 어둡게)")]
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    
    [Tooltip("배울 수 있는 상태 색상 (밝은 색, LP 있을 때)")]
    public Color availableColor = new Color(1f, 1f, 0.8f, 1f);  // 밝은 노란빛
    
    [Tooltip("배운 상태 색상 (완전히 밝게)")]
    public Color learnedColor = Color.white;
    
    // 스킬 상태
    public enum SkillState
    {
        Locked,      // 잠김 (선행 스킬 미배움)
        Available,   // 배울 수 있음 (선행 스킬 배움)
        Learned      // 배움
    }
    
    private SkillState currentState = SkillState.Locked;
    private SwordSkillTreeManager treeManager;
    
    private void Awake()
    {
        Debug.Log($"[SkillTreeNode] ⚡ Awake 시작 - {gameObject.name}");
        
        // 버튼이 없으면 자동으로 찾기 (자신 → 자식 → 부모 → 형제 순서로 검색)
        if (nodeButton == null)
        {
            // 1. 먼저 자신에게서 찾기
            nodeButton = GetComponent<Button>();
            if (nodeButton != null)
            {
                Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: Button을 자신에게서 발견 - {nodeButton.gameObject.name}");
            }
            
            // 2. 없으면 자식에서 찾기
            if (nodeButton == null)
            {
                nodeButton = GetComponentInChildren<Button>(true);
                if (nodeButton != null)
                {
                    Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: Button을 자식에서 발견 - {nodeButton.gameObject.name}");
                }
            }
            
            // 3. 없으면 부모에서 찾기
            if (nodeButton == null && transform.parent != null)
            {
                nodeButton = transform.parent.GetComponent<Button>();
                if (nodeButton != null)
                {
                    Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: Button을 부모에서 발견 - {nodeButton.gameObject.name}");
                }
            }
            
            // 4. 없으면 형제에서 찾기
            if (nodeButton == null && transform.parent != null)
            {
                nodeButton = transform.parent.GetComponentInChildren<Button>(true);
                if (nodeButton != null)
                {
                    Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: Button을 형제에서 발견 - {nodeButton.gameObject.name}");
                }
            }
            
            // 최종 확인
            if (nodeButton == null)
            {
                Debug.LogError($"[SkillTreeNode] ❌ {gameObject.name}: Button을 찾을 수 없습니다! (자신/자식/부모/형제 모두 검색 완료)");
            }
        }
        
        // 버튼 클릭 이벤트 연결
        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners(); // 기존 리스너 제거
            nodeButton.onClick.AddListener(OnNodeClicked);
            Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: Button 클릭 이벤트 연결 완료! (Interactable: {nodeButton.interactable})");
        }
        else
        {
            Debug.LogError($"[SkillTreeNode] ❌ {gameObject.name}: Button이 null이어서 이벤트 연결 실패!");
        }
        
        // SkillDetailPopup 연결 확인
        if (skillDetailPopup != null)
        {
            Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: SkillDetailPopup 연결됨 - {skillDetailPopup.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[SkillTreeNode] ⚠️ {gameObject.name}: SkillDetailPopup이 연결되지 않았습니다!");
        }
        
        // SkillData 확인
        if (skillData != null)
        {
            Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: SkillData 연결됨 - {skillData.skillName}");
        }
        else
        {
            Debug.LogWarning($"[SkillTreeNode] ⚠️ {gameObject.name}: SkillData가 연결되지 않았습니다!");
        }
        
        // 스킬 아이콘이 없으면 자동으로 찾기
        if (skillIcon == null)
        {
            skillIcon = GetComponentInChildren<Image>();
        }
        
        // 스킬 이름 텍스트가 없으면 자동으로 찾기
        if (skillNameText == null)
        {
            skillNameText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }
    
    /// <summary>
    /// 스킬 트리 매니저 설정
    /// </summary>
    public void SetTreeManager(SwordSkillTreeManager manager)
    {
        treeManager = manager;
    }
    
    /// <summary>
    /// 노드 초기화
    /// </summary>
    public void Initialize()
    {
        if (skillData == null)
        {
            Debug.LogWarning($"[SkillTreeNode] {gameObject.name}에 SkillData가 할당되지 않았습니다!");
            return;
        }
        
        Debug.Log($"[SkillTreeNode] 🔄 {gameObject.name} 초기화 시작 - 스킬: {skillData.skillName}");
        
        // 스킬 이름 표시
        if (skillNameText != null)
        {
            skillNameText.text = skillData.skillName;
            Debug.Log($"[SkillTreeNode] ✅ 이름 설정 완료: {skillData.skillName}");
        }
        else
        {
            Debug.LogWarning($"[SkillTreeNode] ⚠️ {gameObject.name}에 skillNameText가 없습니다!");
        }
        
        // 스킬 아이콘 표시
        if (skillIcon != null)
        {
            if (skillData.skillIcon != null)
            {
                skillIcon.sprite = skillData.skillIcon;
                Debug.Log($"[SkillTreeNode] ✅ 아이콘 설정 완료: {skillData.skillName} - {skillData.skillIcon.name}");
            }
            else
            {
                Debug.LogWarning($"[SkillTreeNode] ⚠️ {skillData.skillName}의 skillIcon이 null입니다!");
            }
        }
        else
        {
            Debug.LogWarning($"[SkillTreeNode] ❌ {gameObject.name}의 skillIcon Image 컴포넌트가 연결되지 않았습니다!");
        }
        
        // 🔥 중요: 초기 상태 체크 (선행 스킬 기반)
        // UpdateState()는 나중에 UpdateAllNodesState()에서 호출됨
        // 여기서는 시각만 업데이트
        Debug.Log($"[SkillTreeNode] 🔄 {skillData.skillName} 초기화 완료 (상태는 나중에 업데이트됨)");
    }
    
    /// <summary>
    /// 노드 클릭 시 호출
    /// </summary>
    private void OnNodeClicked()
    {
        Debug.Log($"[SkillTreeNode] 🔘🔘🔘 버튼 클릭 감지!!! - {gameObject.name} (Time: {Time.time})");
        Debug.Log($"[SkillTreeNode] 현재 GameObject 활성 상태: {gameObject.activeSelf}, Enabled: {enabled}");
        
        if (treeManager == null)
        {
            Debug.LogWarning($"[SkillTreeNode] ⚠️ {gameObject.name}: TreeManager가 설정되지 않았습니다! (팝업은 열립니다)");
        }
        
        // 스킬 상세 정보 팝업 표시
        ShowSkillDetailPopup();
    }
    
    /// <summary>
    /// 스킬 상세 정보 팝업 표시
    /// </summary>
    private void ShowSkillDetailPopup()
    {
        Debug.Log($"[SkillTreeNode] 🔘 {gameObject.name} 클릭됨!");
        
        if (skillData == null)
        {
            Debug.LogWarning($"[SkillTreeNode] {gameObject.name}: SkillData가 없습니다!");
            return;
        }
        
        // skillDetailPopup이 연결되지 않았으면 자동으로 찾기
        if (skillDetailPopup == null)
        {
            Debug.Log($"[SkillTreeNode] {gameObject.name}: SkillDetailPopup을 자동으로 찾는 중...");
            skillDetailPopup = FindObjectOfType<SkillDetailPopup>(true);
            
            if (skillDetailPopup != null)
            {
                Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: SkillDetailPopup 자동 검색 성공 - {skillDetailPopup.gameObject.name}");
            }
            else
            {
                Debug.LogError($"[SkillTreeNode] ❌ {gameObject.name}: SkillDetailPopup을 찾을 수 없습니다!");
                return;
            }
        }
        
        Debug.Log($"[SkillTreeNode] ✅ {gameObject.name}: 팝업 열기 시도 - {skillData.skillName} (상태: {currentState})");
        skillDetailPopup.ShowSkillDetail(skillData, this); // 노드 정보도 함께 전달!
    }
    
    /// <summary>
    /// 이 스킬을 배울 수 있는지 확인
    /// </summary>
    public bool CanLearn()
    {
        string skillName = skillData != null ? skillData.skillName : gameObject.name;
        
        // 이미 배웠으면 불가
        if (currentState == SkillState.Learned)
        {
            Debug.Log($"[SkillTreeNode] {skillName}: 이미 배움 → CanLearn = false");
            return false;
        }
        
        // 선행 스킬 체크 (CheckPrerequisites() 사용)
        if (!CheckPrerequisites())
        {
            Debug.Log($"[SkillTreeNode] {skillName}: 선행 스킬 미충족 → CanLearn = false");
            return false;
        }
        
        // 스킬 포인트 체크
        if (treeManager != null)
        {
            int availableLP = treeManager.GetAvailableSkillPoints();
            if (availableLP < requiredSkillPoints)
            {
                Debug.Log($"[SkillTreeNode] {skillName}: LP 부족 (필요: {requiredSkillPoints}, 보유: {availableLP}) → CanLearn = false");
                return false;
            }
        }
        
        Debug.Log($"[SkillTreeNode] ✅ {skillName}: 배울 수 있음! → CanLearn = true");
        return true;
    }
    
    /// <summary>
    /// 스킬 상태 업데이트
    /// </summary>
    public void UpdateState()
    {
        if (currentState == SkillState.Learned)
        {
            // 이미 배운 스킬은 상태 변경 안 함
            Debug.Log($"[SkillTreeNode] {skillData?.skillName}: 이미 배움 (상태 유지)");
            return;
        }
        
        // 선행 스킬 체크
        bool prerequisitesMet = CheckPrerequisites();
        
        // 상태 설정
        if (prerequisitesMet)
        {
            currentState = SkillState.Available;
            Debug.Log($"[SkillTreeNode] ✨ {skillData?.skillName}: Available (선행 스킬 충족!)");
        }
        else
        {
            currentState = SkillState.Locked;
            Debug.Log($"[SkillTreeNode] 🔒 {skillData?.skillName}: Locked (선행 스킬 필요)");
        }
        
        UpdateVisualState();
    }
    
    /// <summary>
    /// 선행 스킬 충족 여부 확인 (SkillData SO 기준)
    /// </summary>
    private bool CheckPrerequisites()
    {
        if (skillData == null)
        {
            Debug.LogError($"[SkillTreeNode] {gameObject.name}: SkillData가 null입니다!");
            return false;
        }
        
        // 🔥 핵심: SkillData.prerequisiteSkills (SO)만 사용!
        if (skillData.prerequisiteSkills == null || skillData.prerequisiteSkills.Count == 0)
        {
            // 선행 스킬이 없으면 루트 스킬 → Available
            Debug.Log($"[SkillTreeNode] ✨ {skillData.skillName}: 선행 스킬 없음 (루트) → Available");
            return true;
        }
        
        // TreeManager 필수
        if (treeManager == null)
        {
            Debug.LogError($"[SkillTreeNode] ❌ {skillData.skillName}: TreeManager가 없어서 선행 스킬 체크 불가!");
            return false;
        }
        
        Debug.Log($"[SkillTreeNode] 🔍 {skillData.skillName}: {skillData.prerequisiteSkills.Count}개의 선행 스킬 확인 중...");
        
        // 모든 선행 스킬이 배워졌는지 확인
        foreach (var prereqSkillData in skillData.prerequisiteSkills)
        {
            if (prereqSkillData == null)
            {
                Debug.LogWarning($"[SkillTreeNode] ⚠️ {skillData.skillName}: 선행 스킬 데이터가 null입니다!");
                continue;
            }
            
            bool isLearned = treeManager.IsSkillLearned(prereqSkillData);
            Debug.Log($"[SkillTreeNode] 🔍 {skillData.skillName} → {prereqSkillData.skillName}: {(isLearned ? "✅ 배움" : "❌ 안 배움")}");
            
            if (!isLearned)
            {
                Debug.Log($"[SkillTreeNode] 🔒 {skillData.skillName}: {prereqSkillData.skillName}을(를) 배워야 함 → Locked");
                return false; // 하나라도 안 배웠으면 false
            }
        }
        
        Debug.Log($"[SkillTreeNode] ✨ {skillData.skillName}: 모든 선행 스킬 충족! → Available");
        return true;
    }
    
    /// <summary>
    /// 스킬 배우기
    /// </summary>
    public void LearnSkill()
    {
        if (currentState == SkillState.Learned)
        {
            Debug.LogWarning($"[SkillTreeNode] {skillData.skillName}은(는) 이미 배운 스킬입니다.");
            return;
        }
        
        currentState = SkillState.Learned;
        UpdateVisualState();
        
        Debug.Log($"[SkillTreeNode] ✅ {skillData.skillName} 배움!");
    }
    
    /// <summary>
    /// 시각적 상태 업데이트
    /// </summary>
    private void UpdateVisualState()
    {
        string skillName = skillData != null ? skillData.skillName : gameObject.name;
        
        switch (currentState)
        {
            case SkillState.Locked:
                // 잠김: 어둡게 + 자물쇠 + 클릭 불가
                if (skillIcon != null)
                {
                    skillIcon.color = lockedColor;
                    Debug.Log($"[SkillTreeNode] 🔒 {skillName}: 아이콘 색상 → Locked (RGB {lockedColor.r}, {lockedColor.g}, {lockedColor.b})");
                }
                if (lockedOverlay != null)
                    lockedOverlay.SetActive(true);
                if (learnedEffect != null)
                    learnedEffect.SetActive(false);
                if (nodeButton != null)
                    nodeButton.interactable = true; // 정보 보기는 가능
                break;
                
            case SkillState.Available:
                // 배울 수 있음: 밝게 빛남 + 자물쇠 없음 + 클릭 가능
                if (skillIcon != null)
                {
                    skillIcon.color = availableColor;
                    Debug.Log($"[SkillTreeNode] ✨ {skillName}: 아이콘 색상 → Available (RGB {availableColor.r}, {availableColor.g}, {availableColor.b})");
                }
                if (lockedOverlay != null)
                    lockedOverlay.SetActive(false);
                if (learnedEffect != null)
                    learnedEffect.SetActive(false);
                if (nodeButton != null)
                    nodeButton.interactable = true;
                break;
                
            case SkillState.Learned:
                // 배움: 완전히 밝게 + 효과 + 클릭 가능 (정보 보기용)
                if (skillIcon != null)
                {
                    skillIcon.color = learnedColor;
                    Debug.Log($"[SkillTreeNode] ✅ {skillName}: 아이콘 색상 → Learned (RGB {learnedColor.r}, {learnedColor.g}, {learnedColor.b})");
                }
                if (lockedOverlay != null)
                    lockedOverlay.SetActive(false);
                if (learnedEffect != null)
                    learnedEffect.SetActive(true);
                if (nodeButton != null)
                    nodeButton.interactable = true;
                break;
        }
    }
    
    /// <summary>
    /// 잠긴 이유 표시
    /// </summary>
    private void ShowLockedReason()
    {
        string reason = "이 스킬을 배울 수 없습니다:\n";
        
        // 선행 스킬 체크
        if (prerequisiteNodes != null && prerequisiteNodes.Length > 0)
        {
            foreach (var prereq in prerequisiteNodes)
            {
                if (prereq != null && prereq.currentState != SkillState.Learned)
                {
                    reason += $"- {prereq.skillData.skillName}을(를) 먼저 배워야 합니다.\n";
                }
            }
        }
        
        // 스킬 포인트 체크
        if (treeManager != null)
        {
            int available = treeManager.GetAvailableSkillPoints();
            if (available < requiredSkillPoints)
            {
                reason += $"- 스킬 포인트가 부족합니다. (필요: {requiredSkillPoints}, 보유: {available})\n";
            }
        }
        
        Debug.Log($"[SkillTreeNode] {reason}");
        // TODO: UI 팝업으로 표시
    }
    
    /// <summary>
    /// 스킬 정보 표시
    /// </summary>
    private void ShowSkillInfo()
    {
        string info = $"[{skillData.skillName}]\n{skillData.description}";
        Debug.Log($"[SkillTreeNode] {info}");
        // TODO: UI 팝업으로 표시
    }
    
    /// <summary>
    /// 현재 상태 가져오기
    /// </summary>
    public SkillState GetState()
    {
        return currentState;
    }
    
    /// <summary>
    /// 스킬 데이터 가져오기
    /// </summary>
    public SkillData GetSkillData()
    {
        return skillData;
    }
    
    /// <summary>
    /// 아이콘 강제 새로고침 (에디터 전용)
    /// </summary>
    [ContextMenu("Refresh Icon")]
    public void RefreshIcon()
    {
        if (skillData == null)
        {
            Debug.LogWarning($"[SkillTreeNode] {gameObject.name}에 SkillData가 없습니다!");
            return;
        }
        
        if (skillIcon == null)
        {
            // Image 컴포넌트 다시 찾기
            skillIcon = GetComponent<Image>();
            if (skillIcon == null)
            {
                skillIcon = GetComponentInChildren<Image>(true);
            }
            
            if (skillIcon == null)
            {
                Debug.LogError($"[SkillTreeNode] {gameObject.name}에 Image 컴포넌트를 찾을 수 없습니다!");
                return;
            }
        }
        
        if (skillData.skillIcon != null)
        {
            skillIcon.sprite = skillData.skillIcon;
            Debug.Log($"[SkillTreeNode] ✅ {skillData.skillName} 아이콘 새로고침 완료!");
        }
        else
        {
            Debug.LogWarning($"[SkillTreeNode] {skillData.skillName}의 skillIcon이 비어있습니다!");
        }
    }
    
    private void OnDestroy()
    {
        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveListener(OnNodeClicked);
        }
    }
}

