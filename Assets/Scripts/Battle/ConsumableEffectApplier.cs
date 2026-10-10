using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle;

/// <summary>
/// 소비 아이템(ConsumableItemSO)의 효과를 PlayerStats에 일관되게 적용하는 통합 헬퍼.
/// 맵 인벤토리(InventoryUIManager.ApplyItemEffect) / 전투 인벤토리(BattleManager case "item")
/// 양쪽에서 동일하게 호출되어 효과 적용 로직의 진실은 한 군데로 모입니다.
///
/// 인벤토리 차감(UseItem/RemoveItem)은 호출자 책임 — 이 메서드는 효과 적용만 담당.
/// </summary>
public static class ConsumableEffectApplier
{
    /// <summary>
    /// ApplyEffects 결과 — 메시지 출력/로그/UI 갱신용.
    /// 모든 정수 값은 "실제로 변화한 양"(클램프 후) 입니다.
    /// </summary>
    public struct EffectResult
    {
        public int hpHealed;
        public int mpHealed;
        public int mpLost;
        public List<StatusEffectType> curesApplied;
        public string buffText;  // 건 버프 요약 (예: "ATK +15% (3 turns)"). 없으면 null
        public StatusEffectType? sideEffect; // [2026-10-08] 부작용으로 걸린 상태이상 (없으면 null)

        public bool AnyApplied =>
            hpHealed > 0 || mpHealed > 0 || mpLost > 0
            || (curesApplied != null && curesApplied.Count > 0) || !string.IsNullOrEmpty(buffText);
    }

    /// <summary>
    /// 소비 아이템의 효과를 user에게 적용합니다.
    /// 적용 순서: HP회복 → MP회복 → 상태이상 해제 → MP 페널티 → 버프(미구현, TODO).
    /// </summary>
    public static EffectResult ApplyEffects(PlayerStats user, ConsumableItemSO item)
    {
        EffectResult result = new EffectResult
        {
            curesApplied = new List<StatusEffectType>()
        };

        if (user == null || item == null)
        {
            Debug.LogWarning("[ConsumableEffectApplier] user or item is null — abort");
            return result;
        }

        // [2026-10-07] 탐험 스킬 '생존 전문가': 회복 아이템 효과 +20%
        float potency = SurvivalistPotency(user);

        // 1) HP 회복 (hpRecoveryPercent × maxHP, 반올림)
        if (item.hpRecoveryPercent > 0f)
        {
            int healAmount = Mathf.RoundToInt(item.hpRecoveryPercent * potency * user.maxHP);
            if (healAmount > 0)
            {
                int before = user.currentHP;
                user.Heal(healAmount);  // 내부에서 maxHP 클램프 + OnStatusChanged 발동
                result.hpHealed = user.currentHP - before;
            }
        }

        // 2) MP 회복 (mpRecoveryPercent × maxMP, 반올림)
        if (item.mpRecoveryPercent > 0f)
        {
            int mpAmount = Mathf.RoundToInt(item.mpRecoveryPercent * potency * user.maxMP);
            if (mpAmount > 0)
            {
                int before = user.currentMP;
                // currentMP setter가 자체적으로 maxMP 클램프 + OnStatusChanged 발동
                user.currentMP = Mathf.Min(user.currentMP + mpAmount, user.maxMP);
                result.mpHealed = user.currentMP - before;
            }
        }

        // 3) 상태이상 해제 (cureTypes 리스트의 각 타입을 RemoveStatusEffect)
        if (item.cureTypes != null && item.cureTypes.Count > 0)
        {
            foreach (var type in item.cureTypes)
            {
                // 전투 상태이상 + 던전 상태이상(함정, DungeonFieldStatus) 둘 다
                bool cured = false;
                if (user.HasStatusEffect(type))
                {
                    user.RemoveStatusEffect(type);  // 내부에서 OnStatusChanged 발동
                    cured = true;
                }
                if (DungeonFieldStatus.Has(type))
                {
                    DungeonFieldStatus.Remove(type);
                    cured = true;
                }
                if (cured) result.curesApplied.Add(type);
            }
        }

        // 4) MP 페널티 (mpPenaltyPercent × maxMP)
        if (item.mpPenaltyPercent > 0f)
        {
            int mpLoss = Mathf.RoundToInt(item.mpPenaltyPercent * user.maxMP);
            if (mpLoss > 0)
            {
                int before = user.currentMP;
                user.currentMP = Mathf.Max(0, user.currentMP - mpLoss);
                result.mpLost = before - user.currentMP;
            }
        }

        // 5) 버프 (전투 전용 아이템: 숫돌·각성제·연막탄). [2026-10-07] StatModifier 로 구현 — buffDuration 턴 동안.
        //    같은 아이템을 다시 쓰면 갱신 (중첩 안 함). 도망 확률은 PlayerStats.escapeBonus.
        if (item.buffDuration > 0)
        {
            var parts = new List<string>();
            user.RemoveStatModifiersFromSource(item);
            if (item.attackBuffPercent > 0f)
            {
                user.AddStatModifier(new StatModifier { statType = ModStatType.Attack, modType = StatModType.PercentMult, value = 1f + item.attackBuffPercent }, item, item.buffDuration);
                parts.Add($"ATK +{item.attackBuffPercent * 100f:0}%");
            }
            if (item.agilityBuff != 0)
            {
                user.AddStatModifier(new StatModifier { statType = ModStatType.Speed, modType = StatModType.Flat, value = item.agilityBuff }, item, item.buffDuration);
                parts.Add($"Speed +{item.agilityBuff}");
            }
            if (item.evasionBuff > 0f)
            {
                user.AddStatModifier(new StatModifier { statType = ModStatType.Evasion, modType = StatModType.Flat, value = item.evasionBuff }, item, item.buffDuration);
                parts.Add($"Evasion +{item.evasionBuff * 100f:0}%");
            }
            if (item.escapeChanceBuff > 0f)
            {
                user.escapeBonus = item.escapeChanceBuff;
                user.escapeBonusRounds = item.buffDuration;
                parts.Add($"Escape +{item.escapeChanceBuff * 100f:0}%");
            }
            if (parts.Count > 0)
                result.buffText = string.Join(", ", parts) + $" ({item.buffDuration} turn{(item.buffDuration == 1 ? "" : "s")})";
        }

        // 6) [2026-10-08] 부작용: 확률로 상태이상 (예: 약초 20% 독 3턴)
        //    전투 중 = 전투 상태이상, 던전 = 던전 상태이상 (한 걸음 = 1턴, 매 턴 피해)
        if (item.sideEffectChance > 0f && item.sideEffectTurns > 0 && Random.value < item.sideEffectChance)
        {
            bool inBattle = Object.FindFirstObjectByType<BattleManager>() != null;
            bool applied = false;
            if (inBattle)
            {
                var so = DungeonFieldStatus.BattleAssetFor(item.sideEffectType);
                if (so != null) applied = user.ApplyStatusEffect(so, item.sideEffectTurns);
            }
            else if (!user.IsRecruitedCompanion)
            {
                DungeonFieldStatus.Add(item.sideEffectType, item.sideEffectTurns, 1);
                applied = true;
            }
            if (applied)
            {
                result.sideEffect = item.sideEffectType;
                string se = $"{DungeonFieldStatus.NameOf(item.sideEffectType)} ({item.sideEffectTurns} turns)";
                result.buffText = string.IsNullOrEmpty(result.buffText) ? se : result.buffText + ", " + se;
                Debug.Log($"[ConsumableEffectApplier] {item.itemName} 부작용 → {item.sideEffectType} {item.sideEffectTurns}턴 ({(inBattle ? "전투" : "던전")})");
            }
        }

        return result;
    }

    /// <summary>생존 전문가(보급술, 1~5등급): 회복 아이템 효과 +5/9/13/16/20%.</summary>
    private static float SurvivalistPotency(PlayerStats user)
    {
        if (user == null || user.statData == null || user.IsRecruitedCompanion) return 1f;
        int rank = user.statData.FieldSkillRank(FieldSkills.Survivalist);
        if (rank <= 0) return 1f;
        return 1f + FieldSkills.SurvivalistPotency[Mathf.Clamp(rank, 1, FieldSkills.SurvivalistPotency.Length) - 1];
    }
}
