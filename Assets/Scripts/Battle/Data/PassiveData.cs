using UnityEngine;

/// <summary>
/// 패시브 스킬 데이터를 정의하는 ScriptableObject
/// 장착 시 HP/MP 추가 보너스 제공
/// </summary>
[CreateAssetMenu(fileName = "New Passive", menuName = "Game/Passive Data")]
public class PassiveData : ScriptableObject
{
    [Header("기본 정보")]
    public string passiveName = "Unknown Passive";
    public string description = "";
    public Sprite passiveIcon;

    [Header("스탯 보너스")]
    public int hpBonus = 0;
    public int mpBonus = 0;
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int magicBonus = 0;
    public int agilityBonus = 0;
    public int luckBonus = 0;

    [Header("특수 효과")]
    public bool hasSpecialEffect = false;
    public string specialEffectDescription = "";

    [Header("Backflow Suppression")]
    [Tooltip("Backflow suppression rate (0.0 ~ 1.0). Added across all equipped passives, capped at 0.8.")]
    [Range(0f, 1f)]
    public float backflowSuppression = 0f;
}
