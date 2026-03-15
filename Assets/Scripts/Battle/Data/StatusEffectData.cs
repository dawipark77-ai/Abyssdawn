using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 공통 상태이상(저주) 타입
    /// </summary>
    public enum StatusEffectType
    {
        Stun,
        Bleed,
        Poison,
        Ignite,
        Weakness,   // 약화 (공격력/방어력 감소)
        Slow,       // 둔화 (임시 수치 0 — 추후 민첩 감소)
        Silence     // 침묵 (스킬 사용 불가)
    }

    /// <summary>
    /// 상태이상(저주) 공통 데이터
    /// - SO 하나 안에 "물리/마법" 수치를 모두 넣고
    ///   무기에서는 물리 수치, 스킬에서는 마법 수치를 참조하도록 사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffect_", menuName = "Abyssdawn/Status Effect", order = 40)]
    public class StatusEffectSO : ScriptableObject
    {
        [Header("기본 정보")]
        public StatusEffectType effectType;

        [Tooltip("출혈1 / 출혈2 같은 세부 구분용 ID (선택)")]
        public string variantId;

        [Header("아이콘")]
        [Tooltip("아이템창 대표 이미지 (인벤토리/장비 UI 표시용)")]
        public Sprite itemIcon;

        [FormerlySerializedAs("icon")]
        [Tooltip("전투 UI 플랫 아이콘 (전투 중 상태이상 표시용)")]
        public Sprite flatIcon;

        [Header("지속 턴 수")]
        [Tooltip("물리(무기)로 부여되었을 때의 기본 지속 턴 수")]
        public int physicalDuration = 1;

        [Tooltip("마법(스킬)로 부여되었을 때의 기본 지속 턴 수")]
        public int magicalDuration = 1;

        [Header("물리 부여 수치 (무기 전용)")]
        [Tooltip("물리 공격을 통해 상태이상이 부여될 때, 턴마다 입히는 피해량 (MaxHP 비율 등은 별도 시스템에서 해석)")]
        public float physicalDamagePerTurn = 0f;

        [Tooltip("물리 공격 1타 기준 부여 확률 (0.0~1.0, 예: 0.2 = 20%)")]
        [Range(0f, 1f)]
        public float physicalApplyChance = 0f;

        [Header("마법 부여 수치 (스킬 전용)")]
        [Tooltip("마법 스킬을 통해 상태이상이 부여될 때, 턴마다 입히는 피해량")]
        public float magicalDamagePerTurn = 0f;

        [Tooltip("마법 스킬 적중 기준 부여 확률 (0.0~1.0)")]
        [Range(0f, 1f)]
        public float magicalApplyChance = 0f;

        [Tooltip("역효과 등, 시전자에게 상태이상이 걸릴 확률 (마법 전용)")]
        [Range(0f, 1f)]
        public float selfApplyChance = 0f;

        [Header("디버프 효과 (선택사항)")]
        [Tooltip("공격력 감소 비율 % (0이면 무효)")]
        [Range(0f, 100f)]
        public float attackDebuff = 0f;

        [Tooltip("방어력 감소 비율 % (0이면 무효)")]
        [Range(0f, 100f)]
        public float defenseDebuff = 0f;

        [Tooltip("행동 불가 여부 (Stun)")]
        public bool preventAction = false;

        [Tooltip("스킬 사용 불가 여부 (Silence)")]
        public bool preventSkillUse = false;

        [Header("시각 효과")]
        [Tooltip("상태이상 이펙트 프리팹")]
        public GameObject statusVFX;

        // ─── 헬퍼 메서드 (ArmorBreakDataSO와 동일한 패턴) ───

        /// <summary>
        /// 물리(무기) 공격으로 상태이상이 부여되는지 확률 체크
        /// </summary>
        public bool RollPhysicalApply()
        {
            return UnityEngine.Random.value <= physicalApplyChance;
        }

        /// <summary>
        /// 마법(스킬)으로 상태이상이 부여되는지 확률 체크
        /// </summary>
        public bool RollMagicalApply()
        {
            return UnityEngine.Random.value <= magicalApplyChance;
        }

        /// <summary>
        /// 물리 기준 턴당 DoT 피해량 계산 (targetMaxHP 기준)
        /// </summary>
        public int CalculatePhysicalDot(int targetMaxHP)
        {
            return Mathf.Max(1, Mathf.FloorToInt(targetMaxHP * physicalDamagePerTurn));
        }

        /// <summary>
        /// 마법 기준 턴당 DoT 피해량 계산 (targetMaxHP 기준)
        /// </summary>
        public int CalculateMagicalDot(int targetMaxHP)
        {
            return Mathf.Max(1, Mathf.FloorToInt(targetMaxHP * magicalDamagePerTurn));
        }
    }
}

