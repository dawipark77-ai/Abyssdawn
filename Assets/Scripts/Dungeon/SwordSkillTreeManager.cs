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
                Debug.Log($"[SwordSkillTreeManager] ✅ PlayerStatData 발견! 현재 LP: {GetSkillPoints()}");
            }
        }
        
        // 노드 자동 검색
        if (autoFindNodes && (allNodes == null || allNodes.Length == 0))
        {
            allNodes = GetComponentsInChildren<SkillTreeNode>(true);
            Debug.Log($"[SwordSkillTreeManager] {allNodes.Length}개의 스킬 노드를 찾았습니다.");
        }

        // 씬에 이중으로 붙은 중복 노드(부모와 같은 스킬)는 제외 — 부모 노드만 관리
        if (allNodes != null)
            allNodes = System.Array.FindAll(allNodes, n => n != null && !n.IsDuplicateNode());
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
        backupSkillPoints = GetSkillPoints();
        
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
        SetSkillPoints(backupSkillPoints);
        
        // 변경사항 저장
        UnityEditor.EditorUtility.SetDirty(playerStatData);
        UnityEditor.AssetDatabase.SaveAssets();
        
        Debug.Log($"[SwordSkillTreeManager] 🔄 Play 모드 종료 - 복원 완료 (배운 스킬: {playerStatData.learnedSkills.Count}, LP: {GetSkillPoints()})");
    }
    #endif
    
    /// <summary>
    /// 모든 노드 초기화
    /// </summary>
    private void InitializeAllNodes()
    {
        if (allNodes == null)
        {
            Debug.LogWarning("[SwordSkillTreeManager] allNodes가 null입니다!");
            return;
        }
        
        int initializedCount = 0;
        int skippedCount = 0;
        
        foreach (var node in allNodes)
        {
            if (node == null)
            {
                skippedCount++;
                continue;
            }
            
            // SkillData가 할당되었는지 확인
            if (node.skillData == null)
            {
                Debug.LogError($"[SwordSkillTreeManager] ❌ {node.gameObject.name}: SkillData가 할당되지 않았습니다! Inspector에서 Skill Data 필드를 확인하세요.");
                skippedCount++;
                continue;
            }
            
            node.SetTreeManager(this);
            node.Initialize();
            initializedCount++;
        }
        
        Debug.Log($"[SwordSkillTreeManager] 노드 초기화 완료 - 성공: {initializedCount}, 건너뜀: {skippedCount} (전체: {allNodes.Length})");
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
        Debug.Log($"[Learn-DIAG] [SwordSkillTreeManager] === TryLearnSkill 진입 === node='{(node != null ? node.name : "NULL")}', skill='{(node != null && node.GetSkillData() != null ? node.GetSkillData().skillName : "NULL")}'");

        if (node == null || node.GetSkillData() == null)
        {
            Debug.LogError("[Learn-DIAG] [SwordSkillTreeManager] ✗ 유효하지 않은 노드 → early return");
            return;
        }

        SkillData skill = node.GetSkillData();

        // 이미 배운 스킬인지 확인 ([2026-10-09] 등급 스킬은 최대 등급 전까지 다시 찍을 수 있음)
        if (learnedSkillIDs.Contains(skill.skillID) && !CanRankUp(skill))
        {
            Debug.LogWarning($"[Learn-DIAG] [SwordSkillTreeManager] ✗ '{skill.skillName}' 이미 배운 스킬 → early return. learnedSkillIDs.Count={learnedSkillIDs.Count}");
            return;
        }

        // 배울 수 있는지 확인
        if (!node.CanLearn())
        {
            Debug.LogWarning($"[Learn-DIAG] [SwordSkillTreeManager] ✗ '{skill.skillName}' node.CanLearn()=false → early return. 선행 스킬 미충족 가능");
            return;
        }

        // 스킬 포인트 확인
        int requiredPoints = node.requiredSkillPoints;
        int availablePoints = GetAvailableSkillPoints();
        Debug.Log($"[Learn-DIAG] [SwordSkillTreeManager] SP 체크: 필요={requiredPoints}, 보유={availablePoints}");

        if (availablePoints < requiredPoints)
        {
            Debug.LogWarning($"[Learn-DIAG] [SwordSkillTreeManager] ✗ SP 부족 (필요: {requiredPoints}, 보유: {availablePoints}) → early return");
            return;
        }

        // 스킬 배우기
        Debug.Log($"[Learn-DIAG] [SwordSkillTreeManager] ✓ 모든 조건 통과 → LearnSkill('{skill.skillName}') 호출");
        LearnSkill(node);
        Debug.Log($"[Learn-DIAG] [SwordSkillTreeManager] LearnSkill 반환. 잔여 SP={GetAvailableSkillPoints()}");
    }
    
    /// <summary>칸이 다 찬 상태에서 배운 스킬 — 어느 칸과 바꿀지 묻는다. 이미 장착됐거나 탐험 스킬이면 묻지 않음.</summary>
    private void AskReplaceSlot(SkillData skill)
    {
        var list = playerStatData != null ? playerStatData.SlotListFor(skill) : null;
        if (list == null || list.Contains(skill)) return;
        var hud = DungeonHud.Instance;
        if (hud == null) return;
        var labels = new List<string>();
        foreach (var s in list) labels.Add(s != null ? s.skillName : "<color=#888888>Empty</color>");
        string kind = skill.IsPassive ? "passive" : "skill";
        hud.ChooseSkillSlot(
            $"<b>{skill.skillName}</b> learned!\n<size=80%>Your {kind} slots are full. Replace which one?\n<color=#AAAAAA>(Later: equip it any time from Skill Set)</color></size>",
            labels,
            index =>
            {
                if (index < 0) { hud.Toast($"{skill.skillName} learned — not equipped yet."); return; }
                SkillData old = list[index];
                playerStatData.EquipAt(skill, index);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(playerStatData);
#endif
                hud.Toast(old != null ? $"{skill.skillName} equipped <color=#AAAAAA>(replaced {old.skillName})</color>" : $"{skill.skillName} equipped");
                var ps = FindFirstObjectByType<PlayerStats>();
                if (ps != null) ps.NotifyStatusChanged(); // 상태창 스킬 아이콘 갱신
            });
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
            
            if (!playerStatData.learnedSkills.Contains(skill) || CanRankUp(skill))
            {
                playerStatData.learnedSkills.Add(skill); // [2026-10-09] 등급 스킬은 한 번 더 들어가면 등급 +1
            }

            // 빈 슬롯이 있으면 바로 장착 (전투는 장착된 스킬만 사용).
            // [2026-10-07] 칸이 다 찼으면 교체할 칸을 고르는 창 (Later = 배우기만, 나중에 Skill Set 창에서 장착)
            if (!playerStatData.AutoEquipIfFree(skill)) AskReplaceSlot(skill);

            // 스킬 포인트 차감
            SetSkillPoints(GetSkillPoints() - requiredPoints);
            
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
        int skippedCount = 0;
        
        foreach (var node in allNodes)
        {
            if (node == null)
            {
                skippedCount++;
                continue;
            }
            
            // SkillData가 null인 노드는 건너뛰기
            if (node.skillData == null)
            {
                Debug.LogWarning($"[SwordSkillTreeManager] ⚠️ {node.gameObject.name}: SkillData가 null이어서 상태 업데이트를 건너뜁니다.");
                skippedCount++;
                continue;
            }
            
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
        
        Debug.Log($"[SwordSkillTreeManager] 상태 업데이트 완료 - Locked: {lockedCount}, Available: {availableCount}, Learned: {learnedCount}, 건너뜀: {skippedCount}");

        RefreshLinks();
    }

    // ── 노드 연결선 + 배운 스킬 테두리 ─────────────────────────
    [Header("연결선 · 배운 스킬 테두리")]
    [Tooltip("켜면 선행 스킬 → 다음 스킬 연결선을 긋고, 배운 스킬에 옅은 황금 테두리를 두름")]
    public bool drawLinks = false;

    // [2026-10-06] 우선 검 트리만. [2026-10-09] 방패 트리 추가. 다른 트리도 켜려면 이 목록에 이름을 넣거나 인스펙터에서 drawLinks 체크.
    private static readonly HashSet<string> DefaultLinkedTrees = new HashSet<string> { "SwordLore", "ShieldLore", "MartialLore" }; // [2026-10-11] 격투술 추가

    private SkillTreeLinks _links;

    private bool LinksEnabled { get { return drawLinks || DefaultLinkedTrees.Contains(gameObject.name); } }

    private void RefreshLinks()
    {
        if (!LinksEnabled || allNodes == null) return;
        if (_links == null)
        {
            _links = GetComponent<SkillTreeLinks>();
            if (_links == null) _links = gameObject.AddComponent<SkillTreeLinks>();
        }
        foreach (var n in allNodes)
        {
            if (n == null || n.useLearnedBorder) continue;
            n.useLearnedBorder = true;
            n.RefreshVisual(); // 이미 배운 노드도 테두리가 바로 보이도록 (UpdateState 는 배운 노드를 건너뜀)
        }
        _links.Refresh(allNodes);
    }
    
    /// <summary>
    /// 사용 가능한 스킬 포인트 가져오기
    /// </summary>
    /// <summary>[2026-10-09] 스킬 등급 (안 배움 0). 등급 스킬(maxRank 2 이상)은 같은 칸에서 여러 번 찍는다.</summary>
    public int GetSkillRank(SkillData skill)
    {
        return playerStatData != null ? playerStatData.SkillRank(skill) : (learnedSkillIDs.Contains(skill != null ? skill.skillID : null) ? 1 : 0);
    }

    /// <summary>[2026-10-09] 이미 배운 등급 스킬을 한 등급 더 올릴 수 있는지 (최대 등급 미만).</summary>
    public bool CanRankUp(SkillData skill)
    {
        if (skill == null || skill.maxRank <= 1) return false;
        int rank = GetSkillRank(skill);
        return rank >= 1 && rank < skill.maxRank;
    }

    public int GetAvailableSkillPoints()
    {
        // [2026-05-07] skillPoints는 PlayerStats(컴포넌트)가 보유 — PlayerStatData(SO)에서 분리됨
        return GetSkillPoints();
    }

    // ────────────────────────────────────────────────────────
    // skillPoints 헬퍼 — PlayerStats(컴포넌트) 우선, 없으면 testSkillPoints 폴백
    // ────────────────────────────────────────────────────────

    private PlayerStats _cachedPlayerStats;
    private PlayerStats GetPlayerStats()
    {
        if (_cachedPlayerStats == null)
            _cachedPlayerStats = FindFirstObjectByType<PlayerStats>();
        return _cachedPlayerStats;
    }

    private int GetSkillPoints()
    {
        var ps = GetPlayerStats();
        return ps != null ? ps.skillPoints : testSkillPoints;
    }

    private void SetSkillPoints(int value)
    {
        var ps = GetPlayerStats();
        if (ps != null) ps.skillPoints = value;
        else testSkillPoints = Mathf.Max(0, value);
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

