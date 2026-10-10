namespace AbyssdawnBattle
{
    // [2026-10-11] Permanent = 상시 스킬: 스킬 칸을 쓰지 않고 배우면 계속 적용 (원정술 — 야전술·보급술). fieldSkill 과 같은 뜻
    public enum UsageType { Active, Passive, Permanent }
    public enum DamageType { None = 0, Physical, Magic }
    public enum ScaleStat { None = 0, Attack, Defense, Magic, Agility, Luck, CurrentHPPercent, CurrentMPPercent }
    public enum StatusEffect { None, Ignite, Poison, Stun, Slow, Buff }
    public enum EffectType
    {
        None = 0,
        Damage,
        Recovery,
        BuffAttack,
        BuffDefense,
        BuffAgility,
        BuffMagic,
        BuffLuck,
        PassiveAttack,
        PassiveDefense,
        PassiveAgility,
        PassiveMagic,
        PassiveLuck,
        PassiveAccuracy,
        DebuffAttack,
        DebuffDefense,
        DebuffAgility,
        DebuffMagic,
        DebuffLuck,
        Stun,
        Silence
    }
    public enum RecoveryTarget { HP, MP, Both }
    public enum TriggerCondition { None, OnDefense, OnAttacked, OnAttack, TurnStart, LowHP }
}
