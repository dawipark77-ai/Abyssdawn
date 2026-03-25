using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 방패 블록 데이터 SO
    /// 방패 SO에 끼우기만 하면 블록 로직이 자동으로 적용됩니다.
    ///
    /// 계산 공식:
    ///   블록 확률 = baseBlockChance + (캐릭터 방어력 × blockDefenseCoefficient × 0.01)
    ///   피해 감소 = blockDamageReduction + (캐릭터 방어력 × blockDefenseCoefficient)
    ///
    /// 예시 (방어력 10, 조잡한 방패):
    ///   블록 확률 = 0.05 + (10 × 0.3 × 0.01) = 0.08 = 8%
    ///   피해 감소 = 1 + (10 × 0.3) = 4
    /// </summary>
    [CreateAssetMenu(fileName = "BlockData_", menuName = "Abyssdawn/Combat/Block Data", order = 11)]
    public class BlockDataSO : ScriptableObject
    {
        [Header("━━━━━━━━━━ 표시 정보 ━━━━━━━━━━")]
        [FormerlySerializedAs("icon")]
        [Tooltip("아이템창에 표시될 아이콘")]
        public Sprite itemIcon;

        [FormerlySerializedAs("flaticon")]
        [Tooltip("효과 설명에 사용될 Flaticon 이미지")]
        public Sprite flatIcon;

        [Tooltip("효과 이름 (아이템창 표시용)")]
        public string effectName = "블록";

        [TextArea(2, 3)]
        [Tooltip("효과 설명 (아이템창 표시용)")]
        public string description = "적의 공격을 막아 피해를 감소시킵니다.";

        [Header("━━━━━━━━━━ 블록 기본 수치 ━━━━━━━━━━")]
        [Tooltip("기본 블록 확률 (0.0~1.0)\n" +
                 "버클러: 0.03\n" +
                 "방패:   0.05~0.10\n" +
                 "대방패: 0.08~0.20")]
        [Range(0f, 1f)]
        public float baseBlockChance = 0.05f;

        [Tooltip("방어력이 블록 확률에 반영되는 계수.\n" +
                 "공식: baseBlockChance + (방어력 × 이 값 × 0.01)\n" +
                 "기본값 0.3 권장")]
        [Range(0f, 1f)]
        public float blockDefenseCoefficient = 0.3f;

        [Tooltip("블록 성공 시 고정 피해 감소량 (방패의 기본 방어력에 연동).\n" +
                 "공식: 이 값 + (방어력 × blockDefenseCoefficient)")]
        public float blockDamageReduction = 0f;

        /// <summary>
        /// 블록 확률 계산 (0.0~1.0)
        /// </summary>
        public float GetBlockChance(int characterDefense)
        {
            return Mathf.Clamp01(baseBlockChance + characterDefense * blockDefenseCoefficient * 0.01f);
        }

        /// <summary>
        /// 블록 성공 시 피해 감소량 계산
        /// </summary>
        public float GetDamageReduction(int characterDefense)
        {
            return Mathf.Max(0f, blockDamageReduction + characterDefense * blockDefenseCoefficient);
        }

        /// <summary>
        /// 블록 판정 (랜덤 롤 포함)
        /// </summary>
        public bool RollBlock(int characterDefense)
        {
            return UnityEngine.Random.value <= GetBlockChance(characterDefense);
        }
    }
}
