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
        if (entry.currentHP > hpBefore) msg += $"  <color=#FF8C8C>HP +{entry.currentHP - hpBefore}</color>";
        if (entry.currentMP > mpBefore) msg += $"  <color=#8CB4FF>MP +{entry.currentMP - mpBefore}</color>";
        if (DungeonHud.Instance != null) DungeonHud.Instance.Toast(msg);
        Debug.Log("[DungeonItemUse] " + msg);
    }
}
