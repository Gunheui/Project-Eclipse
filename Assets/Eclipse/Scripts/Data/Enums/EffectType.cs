namespace Eclipse.Data.Enums
{
    /// <summary>
    /// 스킬 효과 타입. 각 타입이 SkillEffect.value·affectedStat·duration을 어떻게 해석하는지 아래 참조.
    /// </summary>
    public enum EffectType
    {
        /// <summary> 즉시 피해. value = SkillPower(공격자 ATK 배율). </summary>
        Damage = 0,

        /// <summary> 즉시 회복. value = HealPower(시전자 ATK 배율). </summary>
        Heal = 1,

        /// <summary> 대상 스탯을 지속 상승. affectedStat = 대상 스탯, value = 상승율(0.4=+40%), duration = 지속 턴. </summary>
        Buff = 2,

        /// <summary> 대상 스탯을 지속 하락. affectedStat = 대상 스탯, value = 하락율(0.3=-30%), duration = 지속 턴. </summary>
        Debuff = 3,

        /// <summary> 대상이 duration 턴 동안 시전자를 우선 공격. value 미사용. </summary>
        Taunt = 4,

        /// <summary> 매 턴 피해(damage-over-time). value = 틱당 시전자 ATK 배율, duration = 지속 턴. </summary>
        Dot = 5,

        /// <summary> 피해 흡수막. value = 대상 최대 HP 비율(0.3=30%), duration = 지속 턴. </summary>
        Shield = 6,

        /// <summary> 매 턴 회복(heal-over-time). value = 틱당 시전자 ATK 배율, duration = 지속 턴. </summary>
        Regen = 7
    }
}
