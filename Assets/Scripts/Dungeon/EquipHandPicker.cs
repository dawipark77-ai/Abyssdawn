using System;
using AbyssdawnBattle;

/// <summary>
/// [2026-10-09] 한손 장비(한손 무기·방패)를 장착할 때 어느 손에 들지 먼저 고르게 한다.
/// 던전에서는 DungeonHud 선택창 (오른손 R / 왼손 L — 지금 그 손에 든 것도 보여줌), 선택창이 없는 곳(전투 씬 등)에서는 예전 규칙(EquipmentManager.EquipItem).
/// 양손 무기·방어구·장신구는 고를 것이 없으므로 바로 장착.
/// </summary>
public static class EquipHandPicker
{
    public static bool NeedsChoice(EquipmentData item) => item != null && item.equipmentType == EquipmentType.Hand;

    /// <param name="done">장착했으면 true (취소·실패 false)</param>
    public static void Equip(EquipmentManager mgr, EquipmentData item, Action<bool> done)
    {
        if (mgr == null || item == null) { done?.Invoke(false); return; }
        DungeonHud hud = DungeonHud.Instance;
        if (!NeedsChoice(item) || hud == null) { done?.Invoke(mgr.EquipItem(item)); return; }

        string name = !string.IsNullOrEmpty(item.equipmentName) ? item.equipmentName : item.name;
        hud.Choose($"Equip <b>{name}</b> in which hand?",
            new[]
            {
                $"<color=#FFD24A>R</color>  Right hand  <size=75%><color=#AAAAAA>({Holding(mgr.rightHand)})</color></size>",
                $"<color=#FFD24A>L</color>  Left hand  <size=75%><color=#AAAAAA>({Holding(mgr.leftHand)})</color></size>",
            },
            "Cancel", false, pick =>
            {
                if (pick < 0) { done?.Invoke(false); return; }
                done?.Invoke(mgr.EquipToHand(item, pick == 0));
            });
    }

    private static string Holding(EquipmentData e)
    {
        if (e == null) return "empty";
        return !string.IsNullOrEmpty(e.equipmentName) ? e.equipmentName : e.name;
    }
}
