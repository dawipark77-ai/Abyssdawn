using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AbyssdawnBattle
{
    [CreateAssetMenu(fileName = "ConsumableItem_", menuName = "Abyssdawn/Consumable Item", order = 30)]
    public class ConsumableItemSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string itemName;
        [TextArea(2, 4)]
        public string description;

        [Header("아이콘")]
        public Sprite itemIcon;   // 아이템창 대표 이미지
        [FormerlySerializedAs("icon")]
        public Sprite flatIcon;   // 전투 UI 플랫 아이콘

        [Header("회복")]
        [Range(0f, 1f)] public float hpRecoveryPercent;
        [Range(0f, 1f)] public float mpRecoveryPercent;

        [Header("상태이상 해제")]
        public List<StatusEffectType> cureTypes = new List<StatusEffectType>();

        [Header("버프")]
        [Range(0f, 1f)] public float attackBuffPercent;
        public int     agilityBuff;
        [Range(0f, 1f)] public float evasionBuff;
        [Range(0f, 1f)] public float escapeChanceBuff;
        public int     buffDuration;

        [Header("페널티")]
        [Range(0f, 1f)] public float mpPenaltyPercent;

        [Header("인벤토리")]
        public int  maxStack      = 5;
        public bool usableInBattle = true;
        public bool usableOnMap    = true;

        [Header("특수 설정")]
        [Tooltip("true면 수량이 0이 돼도 인벤토리에서 사라지지 않습니다. (새벽의 잔 등)")]
        public bool isPermanent = false;

        [Tooltip("true면 5층마다 있는 보충 장소에서만 재충전 가능합니다.")]
        public bool isDawnChalice = false;

        [Tooltip("동일 문자열끼리 합산 스택 제한을 공유합니다. 예: HealthPotion")]
        public string stackGroup = "";
    }
}
