namespace AbyssdawnBattle
{
    /// <summary>
    /// 전투 슬롯(1~4 기준) 명중·피해량 보정 테이블.
    /// 수치는 기획표와 동일하게 유지하고, 밸런스 변경 시 이 클래스만 수정합니다.
    /// </summary>
    public static class SlotBalanceTable
    {
        // 인덱스 0 = 미사용, 1~4 = 전투 라인 슬롯
        private static readonly float[] HitChanceByBalanceIndex =
        {
            0f,
            0.90f,
            0.85f,
            0.40f,
            0.30f
        };

        private static readonly float[] DamageMultiplierByBalanceIndex =
        {
            0f,
            1.10f,
            1.10f,
            0.90f,
            0.90f
        };

        /// <summary>
        /// BattleSlot을 1~4 보정 행 인덱스로 환산합니다.
        /// Slot1~4: 그대로, Center·None: 전열 맨앞(1)과 동일, 후열 5~7은 후방 슬롯 3·4에 매핑.
        /// </summary>
        public static int ResolveBalanceSlotIndex(BattleSlot slot)
        {
            switch (slot)
            {
                case BattleSlot.Slot1:
                case BattleSlot.Slot2:
                case BattleSlot.Slot3:
                case BattleSlot.Slot4:
                    return (int)slot;
                case BattleSlot.Slot5:
                case BattleSlot.Slot6:
                    return 3;
                case BattleSlot.Slot7:
                    return 4;
                case BattleSlot.Center:
                case BattleSlot.None:
                default:
                    return 1;
            }
        }

        public static float GetHitChanceMultiplier(BattleSlot slot)
        {
            int idx = ResolveBalanceSlotIndex(slot);
            if (idx < 1 || idx >= HitChanceByBalanceIndex.Length)
                idx = 1;
            return HitChanceByBalanceIndex[idx];
        }

        public static float GetDamageMultiplier(BattleSlot slot)
        {
            int idx = ResolveBalanceSlotIndex(slot);
            if (idx < 1 || idx >= DamageMultiplierByBalanceIndex.Length)
                idx = 1;
            return DamageMultiplierByBalanceIndex[idx];
        }
    }
}
