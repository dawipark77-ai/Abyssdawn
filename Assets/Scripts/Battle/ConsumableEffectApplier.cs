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
        public bool buffsTodo;  // SO에 버프가 정의돼 있으나 PlayerStats 시스템 부재로 미적용

        public bool AnyApplied =>
            hpHealed > 0 || mpHealed > 0 || mpLost > 0
            || (curesApplied != null && curesApplied.Count > 0);
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

        // 1) HP 회복 (hpRecoveryPercent × maxHP, 반올림)
        if (item.hpRecoveryPercent > 0f)
        {
            int healAmount = Mathf.RoundToInt(item.hpRecoveryPercent * user.maxHP);
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
            int mpAmount = Mathf.RoundToInt(item.mpRecoveryPercent * user.maxMP);
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
                if (user.HasStatusEffect(type))
                {
                    user.RemoveStatusEffect(type);  // 내부에서 OnStatusChanged 발동
                    result.curesApplied.Add(type);
                }
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

        // 5) TODO: 버프 적용 — PlayerStats에 일반 버프 시스템(attackBuff/agilityBuff/evasionBuff/
        //    escapeChanceBuff + buffDuration 턴 카운팅)이 부재함. 현재는 SO에 정의돼 있어도 무시.
        //    향후 PlayerStats에 buff 필드 + 매 턴 만료 처리 추가 시 여기에 적용 로직 추가.
        if (item.buffDuration > 0
            && (item.attackBuffPercent > 0f || item.agilityBuff != 0
                || item.evasionBuff > 0f || item.escapeChanceBuff > 0f))
        {
            result.buffsTodo = true;
            Debug.LogWarning($"[ConsumableEffectApplier] {item.itemName} 버프 효과 미구현 — PlayerStats에 일반 버프 시스템 부재 (TODO)");
        }

        return result;
    }
}
