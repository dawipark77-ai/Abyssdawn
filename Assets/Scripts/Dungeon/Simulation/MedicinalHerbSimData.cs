using UnityEngine;

namespace Abyssdawn
{
    /// <summary>
    /// 던전 시뮬 전용 — Medicinal Herb(약초). 실전 인벤토리와는 연결하지 않습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "MedicinalHerbSim", menuName = "Abyssdawn/Simulation/Medicinal Herb (Sim Only)", order = 32)]
    public class MedicinalHerbSimData : ScriptableObject
    {
        [Tooltip("표시용 (CSV/로그 등)")]
        public string displayName = "Medicinal Herb";

        [Tooltip("1개 사용 시 최소 HP 회복")]
        [Min(0)] public int healHpMin = 32;

        [Tooltip("1개 사용 시 최대 HP 회복 (포함)")]
        [Min(0)] public int healHpMax = 35;
    }
}
