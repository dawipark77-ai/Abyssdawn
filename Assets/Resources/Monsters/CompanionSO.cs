using System.Collections.Generic;
using UnityEngine;
using AbyssdawnBattle; // SlotMask, SkillData, ConsumableItemSO 참조

namespace Abyssdawn
{
    /// <summary>
    /// 동료화된 몬스터의 고정 데이터 SO.
    /// 레벨 성장 없음, 장비 착용 없음. 합류 시 고정 스탯.
    /// </summary>
    [CreateAssetMenu(menuName = "Abyssdawn/CompanionSO", fileName = "NewCompanion")]
    public class CompanionSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────
        [Header("기본 정보")]

        [Tooltip("동료의 이름")]
        [SerializeField] private string companionName;

        [Tooltip("전투/파티 UI에 표시할 초상화")]
        [SerializeField] private Sprite portrait;

        // ──────────────────────────────────────────
        // 고정 전투 스탯 (레벨 성장 없음)
        // ──────────────────────────────────────────
        [Header("전투 스탯 (고정)")]

        [Tooltip("최대 HP")]
        [SerializeField] private int hp;

        [Tooltip("최대 MP")]
        [SerializeField] private int mp;

        [Tooltip("물리 공격력")]
        [SerializeField] private int atk;

        [Tooltip("물리 방어력")]
        [SerializeField] private int def;

        [Tooltip("마법 공격력")]
        [SerializeField] private int mag;

        [Tooltip("민첩성 (명중률/회피 계산에 사용)")]
        [SerializeField] private int agi;

        [Tooltip("행운 (크리티컬 확률 계산에 사용)")]
        [SerializeField] private int luk;

        // ──────────────────────────────────────────
        // 스킬셋 (몬스터 시절과 다를 수 있음)
        // ──────────────────────────────────────────
        [Header("스킬셋")]

        [Tooltip("동료 전용 액티브 스킬 목록")]
        [SerializeField] private List<SkillData> activeSkills = new();

        [Tooltip("동료 전용 패시브 스킬 목록")]
        [SerializeField] private List<PassiveData> passiveSkills = new();

        // ──────────────────────────────────────────
        // 파티 배치
        // ──────────────────────────────────────────
        [Header("파티 배치")]

        [Tooltip("이 동료가 배치 가능한 슬롯 (SlotMask 비트 마스크)")]
        [SerializeField] private SlotMask allowedSlots = SlotMask.Any;

        // ──────────────────────────────────────────
        // 프로퍼티 (읽기 전용)
        // ──────────────────────────────────────────

        /// <summary>동료 이름</summary>
        public string CompanionName => companionName;

        /// <summary>초상화 스프라이트</summary>
        public Sprite Portrait => portrait;

        /// <summary>최대 HP</summary>
        public int HP => hp;

        /// <summary>최대 MP</summary>
        public int MP => mp;

        /// <summary>물리 공격력</summary>
        public int ATK => atk;

        /// <summary>물리 방어력</summary>
        public int DEF => def;

        /// <summary>마법 공격력</summary>
        public int MAG => mag;

        /// <summary>민첩성</summary>
        public int AGI => agi;

        /// <summary>행운</summary>
        public int LUK => luk;

        /// <summary>액티브 스킬 목록</summary>
        public IReadOnlyList<SkillData> ActiveSkills => activeSkills;

        /// <summary>패시브 스킬 목록</summary>
        public IReadOnlyList<PassiveData> PassiveSkills => passiveSkills;

        /// <summary>배치 가능 슬롯 마스크</summary>
        public SlotMask AllowedSlots => allowedSlots;
    }
}
