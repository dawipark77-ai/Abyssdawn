using System;
using AbyssdawnBattle;
using UnityEngine;

/// <summary>
/// [2026-10-18] 던전(맵)에서 소비 아이템 "사용" — 누구에게 쓸지 정한다.
///  - 주인공 혼자면 바로 주인공에게 (포션이면 바로 회복)
///  - 동료가 있으면 화면이 살짝 어두워지고 아래 캐릭터 카드들이 빛난다 → 누른 카드의 캐릭터에게 사용 (바깥을 누르면 취소)
/// 아이템은 대상이 정해진 뒤에 줄어든다 (취소하면 그대로).
/// </summary>
public static class DungeonItemUse
{
    public static void Use(ConsumableItemSO item, Action onDone)
    {
        var inv = ConsumableInventory.Instance;
        if (item == null || inv == null || !inv.HasItem(item)) return;

        // [2026-10-22] 횃불: 대상 없이 바로 불을 붙인다 (그 걸음 수 동안 시야 5칸 · 함정 감지 ↑)
        if (item.lightSteps > 0)
        {
            var map = UnityEngine.Object.FindFirstObjectByType<MapManager>();
            if (map == null || !inv.UseItem(item)) return;
            map.AddPlayerLight(item.lightSteps);
            Toast($"<color=#FFC870>{Ko.Eul(item.itemName)} 밝혔다.</color> <size=80%>{DungeonPersistentData.playerLightSteps}걸음 동안 주변이 밝아진다.</size>");
            onDone?.Invoke();
            return;
        }

        // [2026-10-22] 귀향의 거울: 확인 후 가장 가까운 위쪽 마을로 (갈 마을이 없으면 쓰지 않음)
        if (item.returnsToTown)
        {
            var map = UnityEngine.Object.FindFirstObjectByType<MapManager>();
            var hud = DungeonHud.Instance;
            if (map == null || hud == null) return;
            int town = map.ReturnTownFloor(); // [2026-10-22] 마지막으로 들어간 마을 (아직 없으면 가장 가까운 위쪽 마을)
            if (town < 0) { Toast("<color=#AAAAAA>거울에 비칠 마을이 없다.</color>"); return; }
            hud.Confirm($"{Ko.Eul(item.itemName)} 들여다볼까?\n<size=75%><color=#9FC8FF>B{town} 마을 문 앞으로 돌아간다. 거울은 부서진다.</color></size>", "들여다본다", "그만둔다", ok =>
            {
                if (!ok || !inv.UseItem(item)) return;
                CloseItemWindows(); // [2026-10-23] 아이템 창·상세 창이 마을 뒤에 남아, 마을을 떠나면 그 화면에 멈춰 있던 문제
                if (!map.ReturnToTown()) inv.AddItem(item, 1); // 만일 실패하면 되돌려 준다
            });
            return;
        }

        // [2026-10-22] 부활 아이템: 쓰러진 동료 카드만 빛나고 그중에서 고른다
        if (item.IsRevive)
        {
            var occR = CompanionPartyPersistence.BuildMainLineOccupancy();
            System.Func<int, bool> fallen = i => i >= 0 && i < occR.Length && !occR[i].IsEmpty && !occR[i].IsHero
                                                 && occR[i].entry != null && occR[i].entry.currentHP <= 0;
            bool any = false;
            for (int i = 0; i < occR.Length; i++) if (fallen(i)) { any = true; break; }
            if (!any || DungeonPartyBar.Instance == null) { Toast("<color=#AAAAAA>쓰러진 동료가 없다.</color>"); return; }
            DungeonPartyBar.Instance.BeginTargetPick($"{item.itemName} — 누구를 일으킬까?", index =>
            {
                var now = CompanionPartyPersistence.BuildMainLineOccupancy();
                if (index < 0 || index >= now.Length || now[index].IsEmpty || now[index].IsHero) return;
                ReviveCompanion(item, now[index].entry);
                onDone?.Invoke();
            }, fallen);
            return;
        }

        var occ = CompanionPartyPersistence.BuildMainLineOccupancy();
        bool hasCompanion = false;
        foreach (var t in occ) if (!t.IsEmpty && !t.IsHero) { hasCompanion = true; break; }

        var bar = DungeonPartyBar.Instance;
        // [2026-10-20] 새벽의 잔은 주인공 전용 — 동료(몬스터)에게는 쓸 수 없어 고르지 않고 바로 주인공에게
        if (!hasCompanion || bar == null || item.isDawnChalice)
        {
            ApplyToHero(item);
            onDone?.Invoke();
            return;
        }
        bar.BeginTargetPick("누구에게 사용할까?", index =>
        {
            var now = CompanionPartyPersistence.BuildMainLineOccupancy();
            if (index < 0 || index >= now.Length || now[index].IsEmpty) return;
            if (now[index].IsHero) ApplyToHero(item);
            else ApplyToCompanion(item, now[index].entry);
            onDone?.Invoke();
        });
    }

    private static void ApplyToHero(ConsumableItemSO item)
    {
        var hero = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        var inv = ConsumableInventory.Instance;
        if (hero == null || inv == null) return;
        if (!inv.UseItem(item)) return;
        var fx = ConsumableEffectApplier.ApplyEffects(hero, item);
        Debug.Log($"[DungeonItemUse] {item.itemName} → {hero.playerName} — HP +{fx.hpHealed}, MP +{fx.mpHealed}" +
                  (fx.curesApplied != null && fx.curesApplied.Count > 0 ? ", 해제 " + string.Join(", ", fx.curesApplied) : ""));
    }

    /// <summary>[2026-10-23] 아이템 상세 창 · 아이템 목록 · 그 밖의 던전 창을 모두 닫는다 (마을로 옮기기 전에).</summary>
    private static void CloseItemWindows()
    {
        foreach (var popup in UnityEngine.Object.FindObjectsByType<InventoryUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (popup.IsDetailPopupOpen) popup.CloseDetailPopup(); // 인벤토리 창 자체(InventoryOverlay)는 아래 CloseAll 이 닫는다
        }
        foreach (var list in UnityEngine.Object.FindObjectsByType<ItemListPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Close();
        if (DungeonPanelGroup.Instance != null) DungeonPanelGroup.Instance.CloseAll();
    }

    private static void Toast(string msg)
    {
        if (DungeonHud.Instance != null) DungeonHud.Instance.Toast(msg);
    }

    /// <summary>[2026-10-22] 부활 아이템 → 쓰러진 동료를 최대 HP × reviveHpPercent 로 일으킨다.</summary>
    private static void ReviveCompanion(ConsumableItemSO item, CompanionPartyPersistence.Entry entry)
    {
        var inv = ConsumableInventory.Instance;
        var so = entry != null ? CompanionPartyPersistence.LoadCompanion(entry.resourcePath) : null;
        if (so == null || inv == null) return;
        if (entry.currentHP > 0) { Toast($"{so.MonsterName}은(는) 쓰러져 있지 않다."); return; }
        if (!inv.UseItem(item)) return;
        entry.currentHP = Mathf.Max(1, Mathf.RoundToInt(so.HP * item.reviveHpPercent));
        Toast($"<color=#FFE6A0>{Ko.Eul(item.itemName)} 피웠다. {Ko.I(so.MonsterName)} 다시 일어났다!</color>  <color=#8CFF8C>HP {entry.currentHP}</color>");
        Debug.Log($"[DungeonItemUse] 부활 {item.itemName} → {so.MonsterName} HP {entry.currentHP}/{so.HP}");
    }

    /// <summary>동료(몬스터)는 던전에서 HP·MP 숫자만 가진다 — 회복만 적용 (상태이상 치료는 해당 없음).</summary>
    private static void ApplyToCompanion(ConsumableItemSO item, CompanionPartyPersistence.Entry entry)
    {
        var inv = ConsumableInventory.Instance;
        if (entry == null || inv == null) return;
        var so = CompanionPartyPersistence.LoadCompanion(entry.resourcePath);
        if (so == null) return;
        if (entry.currentHP <= 0)
        {
            if (DungeonHud.Instance != null) DungeonHud.Instance.Toast($"{so.MonsterName}은(는) 쓰러져 있다.");
            return;
        }
        if (!inv.UseItem(item)) return;
        float bonus = item.isDawnChalice ? ConsumableInventory.DawnChaliceBonusPercent : 0f;
        int hpBefore = entry.currentHP, mpBefore = entry.currentMP;
        if (item.hpRecoveryPercent > 0f) entry.currentHP = Mathf.Min(so.HP, entry.currentHP + Mathf.RoundToInt((item.hpRecoveryPercent + bonus) * so.HP));
        if (item.mpRecoveryPercent > 0f) entry.currentMP = Mathf.Min(so.MP, entry.currentMP + Mathf.RoundToInt((item.mpRecoveryPercent + bonus) * so.MP));
        string msg = $"{item.itemName} → {so.MonsterName}";
        if (entry.currentHP > hpBefore) msg += $"  <color=#8CFF8C>HP +{entry.currentHP - hpBefore}</color>"; // [2026-10-21] 회복은 초록 (빨강 → 초록)
        if (entry.currentMP > mpBefore) msg += $"  <color=#8CB4FF>MP +{entry.currentMP - mpBefore}</color>";
        // [2026-10-21] 동료의 맵 상태이상 치료 (붕대 → 출혈, 해독제 → 독 …)
        if (item.cureTypes != null)
            foreach (var type in item.cureTypes)
                if (DungeonFieldStatus.CompanionHas(entry.id, type))
                {
                    DungeonFieldStatus.RemoveCompanion(entry.id, type);
                    msg += $"  <color=#9FFF9F>{DungeonFieldStatus.NameOf(type)} 나음</color>";
                }
        // [2026-10-22] 부작용 (예: 약초 20% 독) — 예전엔 동료에게 쓰면 부작용 판정 자체가 없었다
        if (item.sideEffectChance > 0f && item.sideEffectTurns > 0 && UnityEngine.Random.value < item.sideEffectChance)
        {
            DungeonFieldStatus.AddCompanion(entry.id, item.sideEffectType, item.sideEffectTurns, 1);
            msg += $"  <color=#B37FFF>{DungeonFieldStatus.NameOf(item.sideEffectType)}!</color>";
        }
        if (DungeonHud.Instance != null) DungeonHud.Instance.Toast(msg);
        Debug.Log("[DungeonItemUse] " + msg);
    }
}
