using UnityEngine;

namespace AbyssdawnBattle
{
    /// <summary>
    /// 모든 아이템 SO의 추상 베이스 클래스.
    /// ConsumableItemSO, EquipmentData가 이를 상속받는다.
    /// CreateAssetMenu 없음 — 직접 인스턴스화 불가.
    /// </summary>
    public abstract class BaseItemSO : ScriptableObject
    {
        [Header("기본 정보")]

        [Tooltip("아이템 이름")]
        public string itemName;

        [Tooltip("인벤토리/UI에 표시할 대표 아이콘")]
        public Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("아이템 설명")]
        public string description;
    }
}
