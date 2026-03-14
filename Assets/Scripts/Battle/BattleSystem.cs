using UnityEngine;

public class BattleSystem : MonoBehaviour
{
    public PlayerStats player;
    public EnemyStats enemy;

    private EquipmentManager equipmentManager;
    public bool battleEnded = false;

    [Header("Item Settings")]
    public int potionCount = 3;
    public int potionHealAmount = 20;

    [Header("Defence System")]
    public bool isDefending = false;        // 방어 중인지 체크
    public float defenceReduction = 0.5f;   // 방어 시 받는 데미지 비율 (50%)

    // -------------------- 회피 / 크리티컬 --------------------
    private bool CheckHit(int attackerAgility, int targetAgility)
    {
        int hitChance = attackerAgility * 2 - targetAgility; // 간단 공식
        hitChance = Mathf.Clamp(hitChance, 10, 100);         // 최소10% 최대100%
        int roll = UnityEngine.Random.Range(0, 100);
        return roll <= hitChance;
    }

    private bool CheckCritical(int luck)
    {
        float critChance = luck * 0.05f; // 1 luck = 5%
        return UnityEngine.Random.value < critChance;
    }

    void Awake()
    {
        equipmentManager = player != null ? player.GetComponent<EquipmentManager>() : null;
    }

    // -------------------- 유틸: 방어구 파괴 데미지 계산 --------------------
    private int CalculateArmorBreakDamage(int targetDefense)
    {
        if (equipmentManager == null || equipmentManager.rightHand == null)
            return 0;

        var weapon = equipmentManager.rightHand;
        if (weapon.equipmentType != AbyssdawnBattle.EquipmentType.Hand &&
            weapon.equipmentType != AbyssdawnBattle.EquipmentType.TwoHanded)
            return 0;

        float coeff = weapon.armorBreakCoefficient;
        if (coeff <= 0f || targetDefense <= 0)
            return 0;

        // 방어 퍼뎀 = (플레이어 최종 공격력 × 계수) [%] * 대상 현재 방어력
        float percent = player.Attack * coeff * 0.01f;
        float raw = targetDefense * percent;
        return Mathf.RoundToInt(raw);
    }

    // -------------------- 플레이어 공격 --------------------
    public int PlayerAttack()
    {
        if (battleEnded) return 0;

        if (!CheckHit(player.Agility, enemy.Agility))
        {
            Debug.Log("Player attack missed!");
            return 0;
        }

        int baseDamage = Mathf.Max(0, player.attack - enemy.defense);
        int armorBreakDamage = CalculateArmorBreakDamage(enemy.defense);

        bool critical = CheckCritical(player.luck);
        if (critical)
        {
            baseDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * 1.5f));
            armorBreakDamage = Mathf.Max(0, Mathf.RoundToInt(armorBreakDamage * 1.5f));
            Debug.Log("Critical hit!");
        }

        baseDamage = Mathf.Max(baseDamage, 1);
        armorBreakDamage = Mathf.Max(armorBreakDamage, 0);

        enemy.TakeDamage(baseDamage);
        if (armorBreakDamage > 0 && !enemy.IsDead())
        {
            enemy.TakeDamage(armorBreakDamage);
        }

        // 무기 저주 부여
        if (!enemy.IsDead())
            TryApplyWeaponCurse();

        if (enemy.currentHP <= 0) battleEnded = true;

        int totalDamage = baseDamage + armorBreakDamage;
        return totalDamage;
    }

    /// <summary>
    /// 장착 무기의 weaponCurse(StatusEffectSO)를 physicalApplyChance 확률로 적에게 부여합니다.
    /// </summary>
    private void TryApplyWeaponCurse()
    {
        if (equipmentManager == null) return;

        void CheckAndApply(AbyssdawnBattle.EquipmentData weapon)
        {
            if (weapon == null || weapon.weaponCurses == null) return;
            foreach (var curse in weapon.weaponCurses)
            {
                if (curse == null) continue;
                bool applied = enemy.ApplyStatusEffect(curse);
                if (applied)
                    Debug.Log($"[WeaponCurse] {weapon.equipmentName} → {curse.effectType} applied");
            }
        }

        CheckAndApply(equipmentManager.rightHand);
        if (equipmentManager.leftHand != equipmentManager.rightHand)
            CheckAndApply(equipmentManager.leftHand);
    }

    // -------------------- 적 공격 --------------------
    public int EnemyAttack()
    {
        if (battleEnded) return 0;

        if (!CheckHit(enemy.Agility, player.Agility))
        {
            Debug.Log("Enemy attack missed!");
            return 0;
        }

        int damage = Mathf.Max(1, enemy.attack - player.defense);

        if (CheckCritical(enemy.luck))
        {
            damage = Mathf.RoundToInt(damage * 1.5f);
            Debug.Log("Enemy critical hit!");
        }

        if (isDefending)
        {
            damage = Mathf.RoundToInt(damage * defenceReduction);
            isDefending = false; // 한 턴만 적용
            Debug.Log("Player defended! Damage reduced.");
        }

        player.TakeDamage(damage);
        if (player.currentHP <= 0) battleEnded = true;

        return damage;
    }

    // -------------------- 아이템 사용 --------------------
    public int UsePotion()
    {
        if (battleEnded || potionCount <= 0) return 0;

        potionCount--;
        player.Heal(potionHealAmount);
        return potionHealAmount;
    }

    // -------------------- 방어 --------------------
    public void Defend()
    {
        if (battleEnded) return;
        isDefending = true;
    }
}


