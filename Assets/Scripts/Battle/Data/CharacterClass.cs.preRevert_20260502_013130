using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 직업(클래스) 데이터 ScriptableObject.
/// 플레이어 Inspector 기본 수치(baseHP/MP, base 공격 등)에 아래 보정값을 <b>정수로 더함</b>.
/// </summary>
[CreateAssetMenu(fileName = "New Character Class", menuName = "Game/Character Class")]
public class CharacterClass : ScriptableObject
{
    [Header("기본 정보")]
    public string className = "Unknown";
    public string description = "";
    public Sprite classIcon;

    [Header("스탯 보정 (Hero·PlayerStats 기본 수치에 더함)")]
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int magicBonus = 0;
    public int agilityBonus = 0;
    public int luckBonus = 0;

    [Tooltip("Base HP(플레이어 기본 HP)에 더합니다. 예: Base 20, 여기 10 → 직업 반영 최대 HP는 baseHP+10 (+패시브·장비 등 별도). 레벨업으로 늘어난 baseHP에도 동일하게 더해집니다.")]
    [FormerlySerializedAs("hpFlatBonus")]
    public int hpBonus = 0;

    [Tooltip("Base MP에 더합니다.")]
    [FormerlySerializedAs("mpFlatBonus")]
    public int mpBonus = 0;

    [Header("레벨업 보너스 (선택)")]
    [Tooltip("레벨당 baseHP에 더해지는 증가량(직업). 레벨업 시 baseHP에 가산.")]
    public int hpPerLevel = 0;

    [Tooltip("레벨당 baseMP에 더해지는 증가량(직업).")]
    public int mpPerLevel = 0;

    [Header("레벨업 스탯 성장치 (직업별)")]
    public float attackGrowthPerLevel = 0f;
    public float defenseGrowthPerLevel = 0f;
    public float magicGrowthPerLevel = 0f;
    public float agilityGrowthPerLevel = 0f;
    public float luckGrowthPerLevel = 0f;

    public int GetFinalAttack(int baseAttack) => baseAttack + attackBonus;
    public int GetFinalDefense(int baseDefense) => baseDefense + defenseBonus;
    public int GetFinalMagic(int baseMagic) => baseMagic + magicBonus;
    public int GetFinalAgility(int baseAgility) => baseAgility + agilityBonus;
    public int GetFinalLuck(int baseLuck) => baseLuck + luckBonus;

    /// <summary>직업 가산만: runtime baseHP + 직업 hpBonus. 패시브/장비는 PlayerStats에서 추가.</summary>
    public int GetFinalMaxHP(int baseHP) => baseHP + hpBonus;

    /// <summary>runtime baseMP + 직업 mpBonus.</summary>
    public int GetFinalMaxMP(int baseMP) => baseMP + mpBonus;

    public override string ToString()
    {
        return $"[{className}] ATK:{attackBonus:+#;-#;0} DEF:{defenseBonus:+#;-#;0} MAG:{magicBonus:+#;-#;0} " +
               $"AGI:{agilityBonus:+#;-#;0} LUK:{luckBonus:+#;-#;0} HP:{hpBonus:+#;-#;0} MP:{mpBonus:+#;-#;0}";
    }
}
