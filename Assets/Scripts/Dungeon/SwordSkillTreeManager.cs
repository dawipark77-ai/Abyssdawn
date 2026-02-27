using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using AbyssdawnBattle;

/// <summary>
/// Sword Lore 스킬 트리 전체를 관리하는 매니저
/// </summary>
public class SwordSkillTreeManager : MonoBehaviour
{
    [Header("스킬 포인트")]
    [Tooltip("플레이어 스탯 데이터")]
    public PlayerStatData playerStatData;
    
    [Tooltip("사용 가능한 스킬 포인트 (테스트용, 실제로는 PlayerStatData에서 가져옴)")]
    public int testSkillPoints = 5;
    
    [Header("스킬 노드들")]
    [Tooltip("이 스킬 트리의 모든 노드들")]
    public SkillTreeNode[] allNodes;
    
    [Header("UI 표시")]
    [Tooltip("스킬 포인트 표시 텍스트")]
    public TextMeshProUGUI skillPointsText;
    
    [Header("자동 검색 설정")]
    [Tooltip("시작 시 자동으로 자식 노드들을 검색할지 여부")]
    public bool autoFindNodes = true;
    
    // 배운 스킬 목록
    private HashSet<string> learnedSkillIDs = new HashSet<string>();
    
    // Play 모드 시작 시 백업 (에디터에서만)
    #if UNITY_EDITOR
    private List<SkillData> backupLearnedSkills = new List<SkillData>();
    private int backupSkillPoints = 0;
    #endif
    
    private void Awake()
    {
        // PlayerStatData 자동 검색
        if (playerStatData == null)
        {
            // 먼저 Resources에서 찾기
            playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
            
            // 없으면 HeroData 찾기
            if (playerStatData == null)
            {
                playerStatData = Resources.Load<PlayerStatData>("HeroData");
            }
            
            // 여전히 없으면 경고
            if (playerStatData == null)
            {
                Debug.LogWarning("[SwordSkillTreeManager] PlayerStatData를 찾을 수 없습니다. 테스트 모드로 실행합니다.");
            }
            else
            {
                Debug.Log($"[SwordSkillTreeManager] ✅ PlayerStatData 발견! 현재 LP: {playerStatData.skillPoints}");
            }
        }
        
        // 노드 자동 검색
        if (autoFindNodes && (allNodes == null || allNodes.Length == 0))
        {
            allNodes = GetComponentsInChildren<SkillTreeNode>(true);
            Debug.Log($"[SwordSkillTreeManager] {allNodes.Length}개의 스킬 노드를 찾았습니다.");
        }
    }
    
    private void Start()
    {
        // 🔥 Play 모드 시작 시 PlayerStatData 백업 (에디터에서만)
        #if UNITY_EDITOR
        BackupPlayerData();
        #endif
        
        // 모든 노드 초기화
        InitializeAllNodes();
        
        // PlayerStatData에서 배운 스킬 로드
        LoadLearnedSkills();
        
        // UI 업데이트
        UpdateSkillPointsUI();
        UpdateAllNodesState();
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// Play 모드 시작 시 PlayerStatData 백업
    /// </summary>
    private void BackupPlayerData()
    {
        if (playerStatData == null) return;
        
        // 배운 스킬 백업
        backupLearnedSkills.Clear();
        if (playerStatData.learnedSkills != null)
        {
            backupLearnedSkills.AddRange(playerStatData.learnedSkills);
        }
        
        // 스킬 포인트 백업
        backupSkillPoints = playerStatData.skillPoints;
        
        Debug.Log($"[SwordSkillTreeManager] 💾 Play 모드 시작 - 백업 완료 (배운 스킬: {backupLearnedSkills.Count}, LP: {backupSkillPoints})");
    }
    
    /// <summary>
    /// Play 모드 종료 시 PlayerStatData 복원
    /// </summary>
    private void RestorePlayerData()
    {
        if (playerStatData == null) return;
        
        // 배운 스킬 복원
        if (playerStatData.learnedSkills == null)
        {
            playerStatData.learnedSkills = new List<SkillData>();
        }
        playerStatData.learnedSkills.Clear();
        playerStatData.learnedSkills.AddRange(backupLearnedSkills);
        
        // 스킬 포인트 복원
        playerStatData.skillPoints = backupSkillPoints;
        
        // 변경사항 저장
        UnityEditor.EditorUtility.SetDirty(playerStatData);
        UnityEditor.AssetDatabase.SaveAssets();
        
        Debug.Log($"[SwordSkillTreeManager] 🔄 Play 모드 종료 - 복원 완료 (배운 스킬: {playerStatData.learnedSkills.Count}, LP: {playerStatData.skillPoints})");
    }
    #endif
    
    /// <summary>
    /// 모든 노드 초기화
    /// </summary>
    private void InitializeAllNodes()
    {
        if (allNodes == null) return;
        
        foreach (var node in allNodes)
        {
            if (node != null)
            {
                node.SetTreeManager(this);
                node.Initialize();
            }
        }
        
        Debug.Log($"[SwordSkillTreeManager] {allNodes.Length}개의 노드 초기화 완료");
    }
    
    /// <summary>
    /// PlayerStatData에서 배운 스킬 로드
    /// </summary>
    private void LoadLearnedSkills()
    {
        if (playerStatData == null || playerStatData.learnedSkills == null)
        {
            Debug.Log("[SwordSkillTreeManager] 배운 스킬 데이터가 없습니다.");
            return;
        }
        
        learnedSkillIDs.Clear();
        
        foreach (var skill in playerStatData.learnedSkills)
        {
            if (skill != null)
            {
                learnedSkillIDs.Add(skill.skillID);
                
                // 해당 노드 찾아서 Learned 상태로 설정
                SkillTreeNode node = FindNodeBySkillID(skill.skillID);
                if (node != null)
                {
                    node.LearnSkill();
                }
            }
        }
        
        Debug.Log($"[SwordSkillTreeManager] {learnedSkillIDs.Count}개의 배운 스킬 로드 완료");
    }
    
    /// <summary>
    /// 스킬 ID로 노드 찾기
    /// </summary>
    private SkillTreeNode FindNodeBySkillID(string skillID)
    {
        if (allNodes == null) return null;
        
        foreach (var node in allNodes)
        {
            if (node != null && node.GetSkillData() != null && node.GetSkillData().skillID == skillID)
            {
                return node;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 스킬 배우기 시도
    /// </summary>
    public void TryLearnSkill(SkillTreeNode node)
    {
        if (node == null || node.GetSkillData() == null)
        {
            Debug.LogError("[SwordSkillTreeManager] 유효하지 않은 노드입니다.");
            return;
        }
        
        SkillData skill = node.GetSkillData();
        
        // 이미 배운 스킬인지 확인
        if (learnedSkillIDs.Contains(skill.skillID))
        {
            Debug.LogWarning($"[SwordSkillTreeManager] {skill.skillName}은(는) 이미 배운 스킬입니다.");
            return;
        }
        
        // 배울 수 있는지 확인
        if (!node.CanLearn())
        {
            Debug.LogWarning($"[SwordSkillTreeManager] {skill.skillName}을(를) 배울 수 없습니다.");
            return;
        }
        
        // 스킬 포인트 확인
        int requiredPoints = node.requiredSkillPoints;
        int availablePoints = GetAvailableSkillPoints();
        
        if (availablePoints < requiredPoints)
        {
            Debug.LogWarning($"[SwordSkillTreeManager] 스킬 포인트가 부족합니다. (필요: {requiredPoints}, 보유: {availablePoints})");
            return;
        }
        
        // 스킬 배우기
        LearnSkill(node);
    }
    
    /// <summary>
    /// 스킬 배우기 (실제 처리)
    /// </summary>
    private void LearnSkill(SkillTreeNode node)
    {
        SkillData skill = node.GetSkillData();
        
        // 스킬 포인트 차감
        int requiredPoints = node.requiredSkillPoints;
        int beforeLP = GetAvailableSkillPoints();
        
        if (playerStatData != null)
        {
            // PlayerStatData에 스킬 추가
            if (playerStatData.learnedSkills == null)
            {
                playerStatData.learnedSkills = new List<SkillData>();
            }
            
            if (!playerStatData.learnedSkills.Contains(skill))
            {
                playerStatData.learnedSkills.Add(skill);
            }
            
            // 스킬 포인트 차감
            playerStatData.skillPoints -= requiredPoints;
            
            // 변경사항 저장 (에디터에서만)
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(playerStatData);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }
        else
        {
            // 테스트 모드
            testSkillPoints -= requiredPoints;
        }
        
        int afterLP = GetAvailableSkillPoints();
        
        // 배운 스킬 목록에 추가
        learnedSkillIDs.Add(skill.skillID);
        
        // 노드 상태 업데이트
        node.LearnSkill();
        
        Debug.Log($"[SwordSkillTreeManager] ✅ {skill.skillName} 배움! LP: {beforeLP} → {afterLP}");
        
        // 모든 노드 상태 업데이트 (연쇄 해금)
        UpdateAllNodesState();
        
        // UI 업데이트
        UpdateSkillPointsUI();
        
        Debug.Log($"[SwordSkillTreeManager] 🔄 연쇄 해금 확인 완료!");
    }
    
    /// <summary>
    /// 모든 노드 상태 업데이트
    /// </summary>
    private void UpdateAllNodesState()
    {
        if (allNodes == null)
        {
            Debug.LogWarning("[SwordSkillTreeManager] allNodes가 null입니다!");
            return;
        }
        
        Debug.Log($"[SwordSkillTreeManager] === 모든 노드 상태 업데이트 시작 ({allNodes.Length}개) ===");
        
        int lockedCount = 0;
        int availableCount = 0;
        int learnedCount = 0;
        
        foreach (var node in allNodes)
        {
            if (node != null)
            {
                node.UpdateState();
                
                // 상태 카운트
                switch (node.GetState())
                {
                    case SkillTreeNode.SkillState.Locked:
                        lockedCount++;
                        break;
                    case SkillTreeNode.SkillState.Available:
                        availableCount++;
                        break;
                    case SkillTreeNode.SkillState.Learned:
                        learnedCount++;
                        break;
                }
            }
        }
        
        Debug.Log($"[SwordSkillTreeManager] 상태 업데이트 완료 - Locked: {lockedCount}, Available: {availableCount}, Learned: {learnedCount}");
    }
    
    /// <summary>
    /// 사용 가능한 스킬 포인트 가져오기
    /// </summary>
    public int GetAvailableSkillPoints()
    {
        if (playerStatData != null)
        {
            // PlayerStatData의 skillPoints 필드 사용
            return playerStatData.skillPoints;
        }
        else
        {
            // 테스트 모드
            return testSkillPoints;
        }
    }
    
    /// <summary>
    /// 스킬 포인트 UI 업데이트
    /// </summary>
    private void UpdateSkillPointsUI()
    {
        if (skillPointsText != null)
        {
            int available = GetAvailableSkillPoints();
            skillPointsText.text = $"Skill Points: {available}";
        }
    }
    
    /// <summary>
    /// 스킬이 배워졌는지 확인
    /// </summary>
    public bool IsSkillLearned(string skillID)
    {
        return learnedSkillIDs.Contains(skillID);
    }
    
    /// <summary>
    /// 스킬이 배워졌는지 확인 (SkillData)
    /// </summary>
    public bool IsSkillLearned(SkillData skill)
    {
        if (skill == null) return false;
        return learnedSkillIDs.Contains(skill.skillID);
    }
    
    /// <summary>
    /// 배운 스킬 목록 가져오기
    /// </summary>
    public List<SkillData> GetLearnedSkills()
    {
        List<SkillData> skills = new List<SkillData>();
        
        if (allNodes != null)
        {
            foreach (var node in allNodes)
            {
                if (node != null && node.GetState() == SkillTreeNode.SkillState.Learned)
                {
                    skills.Add(node.GetSkillData());
                }
            }
        }
        
        return skills;
    }
    
    /// <summary>
    /// 스킬 트리 리셋 (테스트용)
    /// </summary>
    [ContextMenu("Reset Skill Tree")]
    public void ResetSkillTree()
    {
        learnedSkillIDs.Clear();
        
        if (playerStatData != null && playerStatData.learnedSkills != null)
        {
            playerStatData.learnedSkills.Clear();
        }
        
        UpdateAllNodesState();
        UpdateSkillPointsUI();
        
        Debug.Log("[SwordSkillTreeManager] 스킬 트리 리셋 완료");
    }
    
    /// <summary>
    /// 모든 노드의 아이콘 새로고침 (에디터 전용)
    /// </summary>
    [ContextMenu("Refresh All Icons")]
    public void RefreshAllIcons()
    {
        if (allNodes == null)
        {
            Debug.LogWarning("[SwordSkillTreeManager] 노드가 없습니다!");
            return;
        }
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (var node in allNodes)
        {
            if (node != null)
            {
                try
                {
                    node.RefreshIcon();
                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SwordSkillTreeManager] {node.name} 아이콘 새로고침 실패: {e.Message}");
                    failCount++;
                }
            }
        }
        
        Debug.Log($"[SwordSkillTreeManager] 아이콘 새로고침 완료 - 성공: {successCount}, 실패: {failCount}");
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// Play 모드 종료 시 자동 복원
    /// </summary>
    private void OnApplicationQuit()
    {
        RestorePlayerData();
    }
    
    /// <summary>
    /// GameObject 파괴 시에도 복원 (씬 전환 등)
    /// </summary>
    private void OnDestroy()
    {
        // 애플리케이션 종료가 아닐 때만 복원 (씬 전환 시)
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && Application.isPlaying)
        {
            RestorePlayerData();
        }
    }
    #endif
}

