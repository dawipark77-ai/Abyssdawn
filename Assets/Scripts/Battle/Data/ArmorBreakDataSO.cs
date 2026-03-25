using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 방어구 파괴 데이터 SO
    /// 무기 SO에 끼우기만 하면 방어구 파괴 로직이 자동으로 적용됩니다.
    ///
    /// 계산 공식:
    ///   방어 파괴량 = 적 현재 방어력 × coefficient × 타격 배율
    ///   한손 타격 배율 = 1.0
    ///   쌍수 타격 배율 = 0.35~0.45 랜덤
    ///   방어력 최솟값 = 0
    /// </summary>
    [CreateAssetMenu(fileName = "ArmorBreakData_", menuName = "Abyssdawn/Combat/Armor Break Data", order = 10)]
    public class ArmorBreakDataSO : ScriptableObject
    {
        [Header("━━━━━━━━━━ 표시 정보 ━━━━━━━━━━")]
        [FormerlySerializedAs("icon")]
        [Tooltip("아이템창에 표시될 아이콘")]
        public Sprite itemIcon;

        [FormerlySerializedAs("flaticon")]
        [Tooltip("효과 설명에 사용될 Flaticon 이미지")]
        public Sprite flatIcon;

        [Tooltip("효과 이름 (아이템창 표시용)")]
        public string effectName = "방어구 파괴";

        [TextArea(2, 3)]
        [Tooltip("효과 설명 (아이템창 표시용)")]
        public string description = "적 방어력의 일정 비율만큼 추가 고정 피해를 입힙니다.";

        [Header("━━━━━━━━━━ 방어구 파괴 계수 ━━━━━━━━━━")]
        [Tooltip("방어구 파괴 계수.\n" +
                 "0.01 = 깡뎀 특화 (방어력의 1%)\n" +
                 "0.02 = 밸런스형 (방어력의 2%)\n" +
                 "0.03 = 방어파괴 특화 (방어력의 3%)")]
        [Range(0f, 0.1f)]
        public float coefficient = 0.02f;

        /// <summary>
        /// 단타 방어 파괴량 계산 (fn = 타격 배율)
        /// </summary>
        public float Calculate(float targetDefense, float hitMultiplier = 1.0f)
        {
            return Mathf.Max(0f, targetDefense * coefficient * hitMultiplier);
        }
    }
}
