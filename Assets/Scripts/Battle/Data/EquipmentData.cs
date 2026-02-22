using UnityEngine;

namespace AbyssdawnBattle
{
    public enum EquipmentType { Hand, TwoHanded, Armour, Accessory }

    [CreateAssetMenu(fileName = "Equipment_", menuName = "Abyssdawn/Equipment Data", order = 2)]
    public class EquipmentData : ScriptableObject
    {
        [Header("━━━━━━━━━━ 기본 정보 ━━━━━━━━━━")]
        [Tooltip("장비 이름")]
        public string equipmentName;
        
        [Tooltip("장비 아이콘")]
        public Sprite equipmentIcon;
        
        [TextArea(2, 4)]
        [Tooltip("장비 설명")]
        public string description;
        
        [Space(5)]
        [Tooltip("장비 타입: Hand(한손), TwoHanded(양손), Armour(갑옷), Accessory(장신구)")]
        public EquipmentType equipmentType;
        
        [Tooltip("양손 무기 여부 (TwoHanded 타입이면 자동으로 true로 간주)")]
        public bool isTwoHanded = false;

        [Header("━━━━━━━━━━ 기본 스탯 보너스 ━━━━━━━━━━")]
        [Tooltip("공격력 보정치")]
        public int attackBonus = 0;
        
        [Tooltip("방어력 보정치")]
        public int defenseBonus = 0;
        
        [Tooltip("마법력 보정치")]
        public int magicBonus = 0;

        [Space(10)]
        [Header("━━━━━━━━━━ 추가 스탯 보너스 ━━━━━━━━━━")]
        [Tooltip("최대 HP 보정치")]
        public int hpBonus = 0;
        
        [Tooltip("최대 MP 보정치")]
        public int mpBonus = 0;
        
        [Tooltip("민첩 보정치")]
        public int agiBonus = 0;
        
        [Tooltip("행운 보정치")]
        public int luckBonus = 0;
        
        [Tooltip("명중률 보정치 (-1.0 ~ 1.0, 예: 0.1 = 10% 증가, -0.05 = 5% 감소)")]
        [Range(-1f, 1f)]
        public float accuracyBonus = 0f;

        [Space(10)]
        [Header("━━━━━━━━━━ 특수 효과 ━━━━━━━━━━")]
        [Tooltip("장비 시 자동으로 사용 가능한 무기 효과 스킬")]
        public SkillData weaponEffect;
        
        [Space(5)]
        [Tooltip("장비 착용 시 배우거나 사용할 수 있는 스킬")]
        public SkillData skill;
    }
}


