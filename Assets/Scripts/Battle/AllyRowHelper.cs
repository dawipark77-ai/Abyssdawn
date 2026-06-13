namespace AbyssdawnBattle
{
    /// <summary>
    /// [슬롯 표준화 Phase 1] 아군 전용 4슬롯 전열/후열 판정 (모델 B).
    ///   전열 = 슬롯 1, 2  /  후열 = 슬롯 3, 4
    ///
    /// 적의 7슬롯 판정(<see cref="SlotHelper"/>, 모델 A: 전열=1~4, 후열=5~7)과
    /// 의도적으로 분리되어 있다. 아군은 한 줄 4슬롯이므로 다키스트 던전식 표준
    /// (앞 2칸 = 전열, 뒤 2칸 = 후열)을 따른다.
    ///
    /// 진실의 소스는 캐릭터의 currentSlot(BattleSlot) 번호다. 빈 슬롯도 번호를
    /// 보존한다(압축 없음 — Phase 2에서 보장).
    ///
    /// ※ 적(EnemyStats)에는 이 헬퍼를 쓰지 말 것. 적은 SlotHelper(모델 A)를 쓴다.
    /// </summary>
    public static class AllyRowHelper
    {
        /// <summary>아군 슬롯이 전열(Slot1 또는 Slot2)인지.</summary>
        public static bool IsFrontRow(BattleSlot slot)
        {
            return slot == BattleSlot.Slot1 || slot == BattleSlot.Slot2;
        }

        /// <summary>아군 슬롯이 후열(Slot3 또는 Slot4)인지.</summary>
        public static bool IsBackRow(BattleSlot slot)
        {
            return slot == BattleSlot.Slot3 || slot == BattleSlot.Slot4;
        }
    }
}
