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
    
    [Header("시각적 설정")]
    [Tooltip("잠긴 상태 색상")]
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    
    [Tooltip("배울 수 있는 상태 색상")]
    public Color availableColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    
    [Tooltip("배운 상태 색상")]
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
        // 버튼이 없으면 자동으로 찾기
        if (nodeButton == null)
        {
            nodeButton = GetComponent<Button>();
        }
        
        // 버튼 클릭 이벤트 연결
        if (nodeButton != null)
        {
            nodeButton.onClick.AddListener(OnNodeClicked);
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
        
        Debug.Log($"[SkillTreeNode] {gameObject.name} 초기화 - 스킬: {skillData.skillName}");
        
        // 스킬 이름 표시
        if (skillNameText != null)
        {
            skillNameText.text = skillData.skillName;
            Debug.Log($"[SkillTreeNode] 이름 설정 완료: {skillData.skillName}");
        }
        else
        {
            Debug.LogWarning($"[SkillTreeNode] {gameObject.name}에 skillNameText가 없습니다!");
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
        
        // 초기 상태 설정
        UpdateVisualState();
    }
    
    /// <summary>
    /// 노드 클릭 시 호출
    /// </summary>
    private void OnNodeClicked()
    {
        if (treeManager == null)
        {
            Debug.LogError("[SkillTreeNode] TreeManager가 설정되지 않았습니다!");
            return;
        }
        
        // 스킬 상세 정보 팝업 표시
        ShowSkillDetailPopup();
    }
    
    /// <summary>
    /// 스킬 상세 정보 팝업 표시
    /// </summary>
    private void ShowSkillDetailPopup()
    {
        if (skillData == null)
        {
            Debug.LogWarning("[SkillTreeNode] SkillData가 없습니다!");
            return;
        }
        
        // SkillDetailPopup 찾기
        SkillDetailPopup popup = FindObjectOfType<SkillDetailPopup>(true); // 비활성화된 것도 찾기
        
        if (popup != null)
        {
            popup.ShowSkillDetail(skillData);
        }
        else
        {
            Debug.LogWarning("[SkillTreeNode] SkillDetailPopup을 찾을 수 없습니다!");
            
            // 팝업이 없으면 기존 방식 사용
            if (currentState == SkillState.Available)
            {
                treeManager.TryLearnSkill(this);
            }
            else if (currentState == SkillState.Locked)
            {
                ShowLockedReason();
            }
            else if (currentState == SkillState.Learned)
            {
                ShowSkillInfo();
            }
        }
    }
    
    /// <summary>
    /// 이 스킬을 배울 수 있는지 확인
    /// </summary>
    public bool CanLearn()
    {
        // 이미 배웠으면 불가
        if (currentState == SkillState.Learned)
            return false;
        
        // 선행 스킬 체크
        if (prerequisiteNodes != null && prerequisiteNodes.Length > 0)
        {
            foreach (var prereq in prerequisiteNodes)
            {
                if (prereq != null && prereq.currentState != SkillState.Learned)
                {
                    return false;
                }
            }
        }
        
        // 스킬 포인트 체크
        if (treeManager != null && treeManager.GetAvailableSkillPoints() < requiredSkillPoints)
        {
            return false;
        }
        
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
            return;
        }
        
        // 배울 수 있는지 확인
        bool canLearn = true;
        
        // 선행 스킬 체크
        if (prerequisiteNodes != null && prerequisiteNodes.Length > 0)
        {
            foreach (var prereq in prerequisiteNodes)
            {
                if (prereq != null && prereq.currentState != SkillState.Learned)
                {
                    canLearn = false;
                    break;
                }
            }
        }
        
        // 상태 설정
        if (canLearn)
        {
            currentState = SkillState.Available;
        }
        else
        {
            currentState = SkillState.Locked;
        }
        
        UpdateVisualState();
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
        switch (currentState)
        {
            case SkillState.Locked:
                // 잠김: 어둡게 + 자물쇠 + 클릭 불가
                if (skillIcon != null)
                    skillIcon.color = lockedColor;
                if (lockedOverlay != null)
                    lockedOverlay.SetActive(true);
                if (learnedEffect != null)
                    learnedEffect.SetActive(false);
                if (nodeButton != null)
                    nodeButton.interactable = false;
                break;
                
            case SkillState.Available:
                // 배울 수 있음: 반투명 + 자물쇠 없음 + 클릭 가능
                if (skillIcon != null)
                    skillIcon.color = availableColor;
                if (lockedOverlay != null)
                    lockedOverlay.SetActive(false);
                if (learnedEffect != null)
                    learnedEffect.SetActive(false);
                if (nodeButton != null)
                    nodeButton.interactable = true;
                break;
                
            case SkillState.Learned:
                // 배움: 밝게 + 효과 + 클릭 가능 (정보 보기용)
                if (skillIcon != null)
                    skillIcon.color = learnedColor;
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

