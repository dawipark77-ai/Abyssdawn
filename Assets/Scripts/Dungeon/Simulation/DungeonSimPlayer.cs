using System.Collections.Generic;
using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬용 플레이어/파티 상태 누적기.
    /// BattleSimUnit를 base로 두고 한 회차(run) 동안 EXP/Gold/Potion/Level이 누적됩니다.
    /// 한 전투마다 BattleSimulator에 BattleSimUnit 리스트를 그대로 넘기고,
    /// 전투 종료 후 RestoreUnitState() 호출은 하지 않습니다(HP/MP가 자연 누적).
    /// </summary>
    public class DungeonSimPlayer
    {
        public string Name;
        public int Level;
        public int Exp;
        public int Gold;

        public int HpPotionCount;
        public int HpPotionsUsedTotal;

        // 새벽의 잔 — HP/MP %회복 + (Phase 2에서 상태이상 회복) 충전식 아이템
        public int DawnChaliceCharges;
        public int DawnChaliceUsedTotal;

        public int LevelUpsTotal;

        /// <summary>BattleSimulator에 그대로 전달되는 전투 유닛(슬롯 1~4).</summary>
        public readonly List<BattleSimUnit> Units = new List<BattleSimUnit>();

        public int GetTotalCurrentHP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.CurrentHP);
            return sum;
        }

        public int GetTotalMaxHP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.MaxHP);
            return sum;
        }

        public int GetTotalCurrentMP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.CurrentMP);
            return sum;
        }

        public int GetTotalMaxMP()
        {
            int sum = 0;
            foreach (var u in Units) sum += Mathf.Max(0, u.MaxMP);
            return sum;
        }

        public bool AnyAlive()
        {
            foreach (var u in Units) if (u.IsAlive) return true;
            return false;
        }
    }
}
