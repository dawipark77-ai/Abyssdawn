using System.Collections.Generic;
using AbyssdawnBattle;
using UnityEngine;

namespace Abyssdawn
{
    public enum BattleSimTeam
    {
        Ally,
        Enemy
    }

    /// <summary>
    /// 시뮬레이션 전용 유닛 인스턴스 (씬 오브젝트 없음).
    /// </summary>
    public class BattleSimUnit
    {
        public BattleSimTeam Team;
        public string DisplayName;
        public BattleSlot Slot;

        public int MaxHP;
        public int CurrentHP;
        public int MaxMP;
        public int CurrentMP;
        public int Attack;
        public int Defense;
        /// <summary>MAG(마법) — 시뮬 전용.</summary>
        public int Magic;
        public int Agility;
        public int Luck;

        /// <summary>시뮬 AI — <see cref="MonsterSO.AIPattern"/> 또는 더미 아군 SO.</summary>
        public AIPattern SimAiPattern = AIPattern.Aggressive;

        /// <summary>아군이 방어 선택 → 같은 라운드 <b>적 페이즈</b>에서 받는 모든 피해 × 0.6 (40% 경감).</summary>
        public bool SimGuardEnemyPhase;

        /// <summary>적이 방어 선택 → <b>다음 라운드 아군 페이즈</b>에서 받는 모든 피해 × 0.6. 중첩 없음.</summary>
        public bool SimGuardNextAllyPhase;

        /// <summary>시뮬 전용 스킬 목록 (로스터 SO에서 복사).</summary>
        public List<SkillData> SimSkills = new List<SkillData>();

        /// <summary>던전/배틀 시뮬 — 더미 아군 SO의 종의 기억 3칸 (레벨업 룰렛·성장치 합산에 사용).</summary>
        public MemoryOfSpeciesData MemorySlot1;
        public MemoryOfSpeciesData MemorySlot2;
        public MemoryOfSpeciesData MemorySlot3;

        /// <summary>던전 시뮬 레벨업 시 HP/MP — <see cref="CharacterClass.hpPerLevel"/> / <see cref="CharacterClass.mpPerLevel"/>.</summary>
        public CharacterClass SimCharacterClass;

        /// <summary>던전 시뮬 전용 — 레벨업·SO 기반 수치는 여기에만 쌓고, <see cref="RecomputeSimDerivedStatsFromIntrinsicsAndEquipment"/>로 전투 스탯을 갱신합니다.</summary>
        public bool SimStatLayerEquipmentEnabled;

        /// <summary>던전 시뮬 파티 레벨 — 기본 검술 등 레벨 의존 패시브에 사용.</summary>
        public int SimDungeonPartyLevel = 1;

        /// <summary>던전 시뮬 — <see cref="MonsterSO"/>로 합류한 몬스터 동료. 레벨업·장비·Sword Lore T1 순환 대상에서 제외.</summary>
        public bool SimIsMonsterCompanion;

        /// <summary>던전 시뮬 몬스터 동료 — 합류 시점 <see cref="MonsterSO.MonsterLevel"/>(교체 판정용). 0이면 구버전 유닛으로 간주해 레벨 뒤처짐 제거를 건너뜁니다.</summary>
        public int SimCompanionMonsterLevel;

        public int IntrinsicMaxHP;
        public int IntrinsicMaxMP;
        public int IntrinsicAttack;
        public int IntrinsicDefense;
        public int IntrinsicMagic;
        public int IntrinsicAgility;
        public int IntrinsicLuck;

        /// <summary>던전 시뮬 AI 장비(우/좌 손, 몸, 악세×2). 적 유닛은 비웁니다.</summary>
        public EquipmentData SimEquipRightHand;
        public EquipmentData SimEquipLeftHand;
        public EquipmentData SimEquipBody;
        public EquipmentData SimEquipAccessory1;
        public EquipmentData SimEquipAccessory2;

        public bool IsAlive => CurrentHP > 0;

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
        }

        /// <summary>Intrinsic + 장비 보너스로 MaxHP/MP·오박 스탯을 다시 계산합니다. 비활성 유닛은 변경하지 않습니다.</summary>
        public void RecomputeSimDerivedStatsFromIntrinsicsAndEquipment()
        {
            if (!SimStatLayerEquipmentEnabled) return;

            // MaxHP가 아직 없던 최초 재계산(빌드 직후)에서는 CurrentHP 기본값 0을 ‘사망’으로 보지 않는다.
            bool wasDead = CurrentHP <= 0 && MaxHP > 0;

            int hpRatioNum = MaxHP > 0 ? CurrentHP : IntrinsicMaxHP;
            int hpRatioDen = Mathf.Max(1, MaxHP > 0 ? MaxHP : IntrinsicMaxHP);
            int mpRatioNum = MaxMP > 0 ? CurrentMP : IntrinsicMaxMP;
            int mpRatioDen = Mathf.Max(1, MaxMP > 0 ? MaxMP : IntrinsicMaxMP);

            int atk = IntrinsicAttack;
            int def = IntrinsicDefense;
            int mag = IntrinsicMagic;
            int agi = IntrinsicAgility;
            int luk = IntrinsicLuck;
            int hpFlat = IntrinsicMaxHP;
            int mpFlat = IntrinsicMaxMP;
            float mpPctSum = 0f;

            void AddEq(EquipmentData e)
            {
                if (e == null) return;
                atk += e.attackBonus;
                def += e.defenseBonus;
                mag += e.magicBonus;
                agi += e.agiBonus;
                luk += e.luckBonus;
                hpFlat += e.hpBonus;
                mpFlat += e.mpBonus;
                mpPctSum += e.mpBonusPercent;
            }

            AddEq(SimEquipRightHand);
            AddEq(SimEquipLeftHand);
            AddEq(SimEquipBody);
            AddEq(SimEquipAccessory1);
            AddEq(SimEquipAccessory2);

            atk += BattleSimLearnedSkillCombatMods.GetBasicSwordsmanshipAttackBonus(this);

            Attack = Mathf.Max(0, atk);
            Defense = Mathf.Max(0, def);
            Magic = Mathf.Max(0, mag);
            Agility = Mathf.Max(0, agi);
            Luck = Mathf.Max(0, luk);

            MaxHP = Mathf.Max(1, hpFlat);
            MaxMP = Mathf.Max(0, mpFlat + Mathf.RoundToInt(IntrinsicMaxMP * mpPctSum));

            if (wasDead)
            {
                CurrentHP = 0;
                CurrentMP = 0;
                return;
            }

            if (MaxHP > 0)
                CurrentHP = Mathf.Clamp(Mathf.RoundToInt((float)hpRatioNum * MaxHP / hpRatioDen), 1, MaxHP);
            else
                CurrentHP = 0;

            if (MaxMP > 0)
                CurrentMP = Mathf.Clamp(Mathf.RoundToInt((float)mpRatioNum * MaxMP / mpRatioDen), 0, MaxMP);
            else
                CurrentMP = 0;
        }
    }
}
