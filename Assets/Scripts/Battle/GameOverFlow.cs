using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [2026-10-08] 게임 오버 공통 절차 — 전투 패배(BattleManager)와 던전 사망(함정 등, MapManager)이 같이 쓴다.
///  - ShowScreen: 게임 오버 창 ("Start Over" / "Last Save")
///  - Restart: 고른 대로 다시 시작. Last Save = 저장 불러오기, 아니면 B1 새 탐험 (소지품·장비·골드·스킬 초기화)
/// 내용은 BattleManager.GameOverRestart 에 있던 것을 그대로 옮김.
/// </summary>
public static class GameOverFlow
{
    public static void ShowScreen(Action onRestart, Action onLoadSave)
    {
        bool hasSave = SaveSystem.HasSave;
        string saveInfo = null;
        if (hasSave)
        {
            SaveSystem.SaveData d = SaveSystem.Peek();
            if (d != null) saveInfo = $"<color=#9FC8FF>B{d.currentFloor}  ·  Lv {d.heroLevel}\n{d.savedAt}</color>";
        }
        GameOverScreen.Show(hasSave, saveInfo, onRestart, onLoadSave);
    }

    /// <param name="destroyCompanions">전투 씬의 동료 인스턴스 정리 (던전에서는 null)</param>
    public static void Restart(bool loadSave, string restartScene, IEnumerable<PlayerStats> party, PlayerStatData playerStatData, Action destroyCompanions)
    {
        // HP/MP 최대값으로 리셋
        if (party != null)
            foreach (var member in party)
            {
                if (member == null) continue;
                member.currentHP = member.maxHP;
                member.currentMP = member.maxMP;
            }

        // 던전 영속 데이터 전체 초기화 (층수, 안개, 위치, 상태이상)
        DungeonPersistentData.ClearState();

        GameManager.EnsureInstance().ClearAllData();
        CompanionPartyPersistence.Clear();
        destroyCompanions?.Invoke();
        PlayerStats.PendingLevelUpNotes.Clear();
        DungeonEncounter.justReturnedFromBattle = false;

        // [2026-10-03] Last Save: 마지막으로 저장한 곳(마을)에서 다시 시작 — 층·지도·주인공·동료·소지품 모두 저장 당시로.
        //   Start Over 이거나 불러오기에 실패하면 아래처럼 1층부터 새 탐험 (저장 파일은 지우지 않음).
        if (loadSave && SaveSystem.HasSave)
        {
            SaveSystem.PendingNotice = "<color=#FF6B6B>Your party has fallen...</color>\n<size=80%>You awaken at your last save.</size>";
            if (SaveSystem.Load(out string loadError))
            {
                Debug.Log("[GameOverFlow] Game Over — 마지막 저장에서 다시 시작");
                return;
            }
            SaveSystem.PendingNotice = null;
            Debug.LogWarning($"[GameOverFlow] Game Over — 저장 불러오기 실패({loadError}), 1층부터 새로 시작");
            DungeonPersistentData.ClearState();
            GameManager.EnsureInstance().ClearAllData();
            CompanionPartyPersistence.Clear();
        }

        // [2026-09-30] 아이템·장비도 새 탐험 상태로 (새벽의 잔 최대 충전, 주운 아이템·장비 초기화, 장착 장비 = 시작 장비)
        ConsumableInventory.ResetForNewRun();
        EquipmentBag.ResetForNewRun(playerStatData);
        PlayerWallet.ResetForNewRun();
        // [2026-10-04] 저장 없이 죽으면 스킬도 처음부터 (배운 스킬·장착 슬롯 비움, SP 는 새 주인공 생성 시 PlayerStats.StartingSkillPoints)
        if (playerStatData != null) playerStatData.ResetSkillsForNewRun();
        PlayerStats.PendingLevelUpNotes.Clear();

        Debug.Log($"[GameOverFlow] Game Over — resetting to floor 1 in '{restartScene}'.");
        DungeonEncounter.justReturnedFromBattle = false; // 게임오버는 새 시작이므로 쿨다운 없음
        EncounterTransition.LoadSceneWithFade(restartScene);
    }
}
