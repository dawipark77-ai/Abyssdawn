using UnityEngine;

/// <summary>
/// 직업(클래스) 데이터를 정의하는 ScriptableObject
/// 각 직업의 스탯 보정치와 HP/MP 배율을 설정
/// </summary>
[CreateAssetMenu(fileName = "New Character Class", menuName = "Game/Character Class")]
public class CharacterClass : ScriptableObject
{
    [Header("기본 정보")]
    public string className = "Unknown";        // 직업 이름
    public string description = "";             // 직업 설명
    public Sprite classIcon;                    // 직업 아이콘 (선택)

    [Header("스탯 보정치 (기본 스탯에 더해짐)")]
    public int attackBonus = 0;                 // 공격력 보정
    public int defenseBonus = 0;                // 방어력 보정
    public int magicBonus = 0;                  // 마력 보정
    public int agilityBonus = 0;                // 민첩 보정
    public int luckBonus = 0;                   // 행운 보정

    [Header("HP/MP 배율 (1.0 = 100%)")]
    [Tooltip("기본 HP에 곱해지는 배율 (예: 1.3 = 130%)")]
    [Range(0.5f, 2.0f)]
    public float hpMultiplier = 1.0f;           // HP 배율
    
    [Tooltip("기본 MP에 곱해지는 배율 (예: 0.9 = 90%)")]
    [Range(0.5f, 2.0f)]
    public float mpMultiplier = 1.0f;           // MP 배율

    [Header("레벨업 보너스 (선택)")]
    [Tooltip("레벨당 추가 HP (정수, 예: 8 = 레벨당 HP +8)")]
    public int hpPerLevel = 0;                  // 레벨당 추가 HP
    
    [Tooltip("레벨당 추가 MP (정수, 예: 6 = 레벨당 MP +6)")]
    public int mpPerLevel = 0;                  // 레벨당 추가 MP

    [Header("레벨업 스탯 성장치 (직업별)")]
    [Tooltip("레벨당 힘/공격 성장치 (예: 0.8 = 레벨당 +0.8 공격)")]
    public float attackGrowthPerLevel = 0f;     // 레벨당 공격력 성장
    
    [Tooltip("레벨당 방어 성장치 (예: 0.6 = 레벨당 +0.6 방어)")]
    public float defenseGrowthPerLevel = 0f;    // 레벨당 방어력 성장
    
    [Tooltip("레벨당 마력 성장치 (예: 0.4 = 레벨당 +0.4 마력)")]
    public float magicGrowthPerLevel = 0f;      // 레벨당 마력 성장
    
    [Tooltip("레벨당 민첩 성장치 (예: 0.2 = 레벨당 +0.2 민첩)")]
    public float agilityGrowthPerLevel = 0f;    // 레벨당 민첩 성장
    
    [Tooltip("레벨당 행운 성장치 (예: 0.1 = 레벨당 +0.1 행운)")]
    public float luckGrowthPerLevel = 0f;       // 레벨당 행운 성장

    /// <summary>
    /// 기본 스탯에 직업 보정치를 적용한 최종 스탯 계산
    /// </summary>
    public int GetFinalAttack(int baseAttack) => baseAttack + attackBonus;
    public int GetFinalDefense(int baseDefense) => baseDefense + defenseBonus;
    public int GetFinalMagic(int baseMagic) => baseMagic + magicBonus;
    public int GetFinalAgility(int baseAgility) => baseAgility + agilityBonus;
    public int GetFinalLuck(int baseLuck) => baseLuck + luckBonus;
    
    /// <summary>
    /// 기본 HP/MP에 배율을 적용한 최종 값 계산
    /// </summary>
    public int GetFinalMaxHP(int baseHP) => Mathf.RoundToInt(baseHP * hpMultiplier);
    public int GetFinalMaxMP(int baseMP) => Mathf.RoundToInt(baseMP * mpMultiplier);

    /// <summary>
    /// 디버그용 직업 정보 출력
    /// </summary>
    public override string ToString()
    {
        return $"[{className}] ATK:{attackBonus:+#;-#;0} DEF:{defenseBonus:+#;-#;0} MAG:{magicBonus:+#;-#;0} " +
               $"AGI:{agilityBonus:+#;-#;0} LUK:{luckBonus:+#;-#;0} HP:{hpMultiplier:P0} MP:{mpMultiplier:P0}";
    }
}





































