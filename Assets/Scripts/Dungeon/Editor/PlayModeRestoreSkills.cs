using UnityEngine;
using UnityEditor;
using AbyssdawnBattle;
using System.Collections.Generic;

/// <summary>
/// Play 모드 종료 시 PlayerStatData의 스킬 관련 데이터를 자동으로 복원하는 에디터 스크립트
/// </summary>
[InitializeOnLoad]
public class PlayModeRestoreSkills
{
    private static PlayerStatData playerStatData;
    private static List<SkillData> backupLearnedSkills = new List<SkillData>();
    private static int backupSkillPoints = 0;
    private static bool hasBackup = false;

    static PlayModeRestoreSkills()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Debug.Log("[PlayModeRestoreSkills] 🔄 자동 복원 시스템 활성화");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                // Edit → Play 진입 직전
                Debug.Log("[PlayModeRestoreSkills] 🎮 Play 모드 진입 중...");
                break;

            case PlayModeStateChange.EnteredPlayMode:
                // Play 모드 진입 완료 → 백업!
                BackupPlayerData();
                break;

            case PlayModeStateChange.ExitingPlayMode:
                // Play → Edit 전환 직전 → 복원!
                RestorePlayerData();
                break;

            case PlayModeStateChange.EnteredEditMode:
                // Edit 모드 진입 완료
                Debug.Log("[PlayModeRestoreSkills] 📝 Edit 모드 복귀 완료");
                break;
        }
    }

    /// <summary>
    /// Play 모드 시작 시 PlayerStatData 백업
    /// </summary>
    private static void BackupPlayerData()
    {
        // PlayerStatData 찾기
        playerStatData = Resources.Load<PlayerStatData>("PlayerStatData");
        if (playerStatData == null)
        {
            playerStatData = Resources.Load<PlayerStatData>("HeroData");
        }

        if (playerStatData == null)
        {
            Debug.LogWarning("[PlayModeRestoreSkills] ⚠️ PlayerStatData를 찾을 수 없습니다. 백업 건너뜀.");
            hasBackup = false;
            return;
        }

        // 배운 스킬 백업
        backupLearnedSkills.Clear();
        if (playerStatData.learnedSkills != null)
        {
            backupLearnedSkills.AddRange(playerStatData.learnedSkills);
        }

        // 스킬 포인트 백업
        backupSkillPoints = playerStatData.skillPoints;
        hasBackup = true;

        Debug.Log($"[PlayModeRestoreSkills] 💾 백업 완료 - 배운 스킬: {backupLearnedSkills.Count}, LP: {backupSkillPoints}");
    }

    /// <summary>
    /// Play 모드 종료 시 PlayerStatData 복원
    /// </summary>
    private static void RestorePlayerData()
    {
        if (!hasBackup)
        {
            Debug.Log("[PlayModeRestoreSkills] ℹ️ 백업 데이터 없음, 복원 건너뜀.");
            return;
        }

        if (playerStatData == null)
        {
            Debug.LogWarning("[PlayModeRestoreSkills] ⚠️ PlayerStatData가 null입니다. 복원 실패.");
            return;
        }

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
        EditorUtility.SetDirty(playerStatData);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PlayModeRestoreSkills] 🔄 복원 완료 - 배운 스킬: {playerStatData.learnedSkills.Count}, LP: {playerStatData.skillPoints}");

        hasBackup = false;
    }

    /// <summary>
    /// 수동으로 백업 데이터 확인 (디버그용)
    /// </summary>
    [MenuItem("Tools/Skill Tree/Show Backup Info")]
    public static void ShowBackupInfo()
    {
        if (hasBackup)
        {
            string skillNames = "";
            foreach (var skill in backupLearnedSkills)
            {
                if (skill != null)
                {
                    skillNames += $"\n  - {skill.skillName}";
                }
            }

            EditorUtility.DisplayDialog(
                "백업 정보",
                $"백업 상태: 있음\n\n" +
                $"스킬 포인트: {backupSkillPoints}\n" +
                $"배운 스킬 ({backupLearnedSkills.Count}개):{skillNames}",
                "확인"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "백업 정보",
                "백업 데이터가 없습니다.\n\nPlay 모드를 실행하면 자동으로 백업됩니다.",
                "확인"
            );
        }
    }
}


