using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>시뮬 전용 행동 종류. 아이템·도망은 플래그로 막을 때까지 실행 경로만 예약.</summary>
    public enum BattleSimActionType
    {
        Attack,
        Defend,
        Item,
        Flee
    }

    /// <summary>시뮬 타겟 선택. 실전 <c>BattleManager.GetRandomAlivePartyMember</c> 가중과 유사한 옵션 포함.</summary>
    public enum BattleSimTargetPickMode
    {
        LowestSlot,
        RandomAlive,
        WeightedFrontHeavy,
        PreferBackRow
    }

    /// <summary>헤드리스 시뮬의 단순 AI — 패턴별 방어 성향·타겟 모드.</summary>
    public static class BattleSimAi
    {
        public static BattleSimActionType PickAction(
            BattleSimUnit self,
            System.Random rng,
            bool allowItem,
            bool allowFlee,
            float itemChance,
            float fleeChance)
        {
            if (allowItem && itemChance > 0 && rng.NextDouble() < itemChance)
                return BattleSimActionType.Item;
            if (allowFlee && fleeChance > 0 && rng.NextDouble() < fleeChance)
                return BattleSimActionType.Flee;

            float defendChance = GetDefendChance(self);
            return rng.NextDouble() < defendChance ? BattleSimActionType.Defend : BattleSimActionType.Attack;
        }

        private static float GetDefendChance(BattleSimUnit self)
        {
            float hpRatio = self.MaxHP > 0 ? self.CurrentHP / (float)self.MaxHP : 0f;
            bool lowHp = hpRatio < 0.35f;
            switch (self.SimAiPattern)
            {
                case AIPattern.Defensive:
                    return Mathf.Clamp01(lowHp ? 0.64f : 0.26f);
                case AIPattern.Support:
                    return Mathf.Clamp01(lowHp ? 0.42f : 0.14f);
                default:
                    return Mathf.Clamp01(lowHp ? 0.27f : 0.07f);
            }
        }

        public static BattleSimTargetPickMode GetTargetModeForPattern(AIPattern p)
        {
            switch (p)
            {
                case AIPattern.Defensive:
                    return BattleSimTargetPickMode.RandomAlive;
                case AIPattern.Support:
                    return BattleSimTargetPickMode.PreferBackRow;
                default:
                    return BattleSimTargetPickMode.WeightedFrontHeavy;
            }
        }

        public static BattleSimUnit PickTarget(List<BattleSimUnit> defenders, System.Random rng, BattleSimTargetPickMode mode)
        {
            var alive = defenders.Where(u => u.IsAlive).ToList();
            if (alive.Count == 0) return null;

            switch (mode)
            {
                case BattleSimTargetPickMode.LowestSlot:
                    return alive.OrderBy(u => (int)u.Slot).First();
                case BattleSimTargetPickMode.RandomAlive:
                    return alive[rng.Next(alive.Count)];
                case BattleSimTargetPickMode.WeightedFrontHeavy:
                {
                    var weighted = new List<BattleSimUnit>();
                    foreach (var u in alive)
                    {
                        int w = (int)u.Slot <= 2 ? 3 : 1;
                        for (int i = 0; i < w; i++)
                            weighted.Add(u);
                    }
                    return weighted[rng.Next(weighted.Count)];
                }
                case BattleSimTargetPickMode.PreferBackRow:
                {
                    var back = alive.Where(u => (int)u.Slot >= 3).ToList();
                    if (back.Count > 0)
                        return back[rng.Next(back.Count)];
                    return alive.OrderBy(u => (int)u.Slot).First();
                }
                default:
                    return alive.OrderBy(u => (int)u.Slot).First();
            }
        }
    }
}
