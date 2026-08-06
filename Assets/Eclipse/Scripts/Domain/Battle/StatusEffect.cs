using System;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 유닛에 붙어 있는 지속 효과 하나의 런타임 상태(버프·디버프·도트·리젠·실드).
    /// SkillEffect(공유 정의)로부터 적용 시점에 만들어지며, 세기는 그 순간 값으로 확정(스냅샷)한다 —
    /// 이후 시전자 스탯이 변해도 이 효과의 틱·흡수량은 바뀌지 않는다.
    /// 남은 지속턴·실드 잔여 흡수량 같은 가변 상태는 유닛별로 독립이다.
    /// </summary>
    public class StatusEffect
    {
        /// <summary> 효과 종류(Buff/Debuff/Dot/Regen/Shield). </summary>
        public EffectType Type { get; }

        /// <summary> 버프·디버프가 바꾸는 스탯. 그 외 타입은 None. </summary>
        public StatType Stat { get; }

        /// <summary> 버프·디버프의 변화율(0.4 = ±40%). 그 외 타입은 0. </summary>
        public float Value { get; }

        /// <summary> 도트·리젠의 틱당 HP 변화량(적용 시점 스냅샷, 1 이상). 그 외 타입은 0. </summary>
        public int TickAmount { get; }

        /// <summary> 실드의 남은 흡수량. 피해를 흡수할 때마다 줄어든다. 그 외 타입은 0. </summary>
        public int RemainingAbsorb { get; private set; }

        /// <summary> 남은 지속턴. 대상의 자기 턴이 끝날 때 1 줄어든다. -1이면 만료 없음(상시). </summary>
        public int RemainingTurns { get; private set; }

        /// <summary> 만료됐는지. 지속턴이 0이거나, 실드의 흡수량이 다 소진되면 참. </summary>
        public bool IsExpired =>
            RemainingTurns == 0 || (Type == EffectType.Shield && RemainingAbsorb <= 0);

        /// <summary> 이 효과를 건 스킬. 유지 이펙트가 이것을 보고 자기 수명을 맞춘다. 스킬 밖에서 만들면 null. </summary>
        public SkillSO Source { get; }

        private StatusEffect(EffectType type, StatType stat, float value, int tickAmount, int absorb, int duration,
            SkillSO source)
        {
            Type = type;
            Stat = stat;
            Value = value;
            TickAmount = tickAmount;
            RemainingAbsorb = absorb;
            RemainingTurns = duration;
            Source = source;
        }

        /// <summary> 스탯을 지속 변경하는 버프·디버프 효과를 만든다(type은 Buff/Debuff만 허용). </summary>
        /// <param name="duration">지속턴(양수) 또는 -1(상시).</param>
        public static StatusEffect StatModifier(EffectType type, StatType stat, float value, int duration,
            SkillSO source = null)
        {
            if (type != EffectType.Buff && type != EffectType.Debuff)
                throw new ArgumentException($"StatModifier는 Buff/Debuff만 허용한다: {type}", nameof(type));
            return new StatusEffect(type, stat, value, 0, 0, duration, source);
        }

        /// <summary> 매 턴 HP를 변화시키는 도트·리젠 효과를 만든다(type은 Dot/Regen만 허용). </summary>
        /// <param name="duration">지속턴(양수) 또는 -1(상시).</param>
        public static StatusEffect Periodic(EffectType type, int tickAmount, int duration, SkillSO source = null)
        {
            if (type != EffectType.Dot && type != EffectType.Regen)
                throw new ArgumentException($"Periodic은 Dot/Regen만 허용한다: {type}", nameof(type));
            return new StatusEffect(type, StatType.None, 0, tickAmount, 0, duration, source);
        }

        /// <summary> 피해를 흡수하는 실드 효과를 만든다. </summary>
        /// <param name="absorb">흡수 가능한 총 피해량(1 이상).</param>
        /// <param name="duration">지속턴(양수) 또는 -1(상시).</param>
        public static StatusEffect Shield(int absorb, int duration, SkillSO source = null)
            => new StatusEffect(EffectType.Shield, StatType.None, 0, 0, absorb, duration, source);

        /// <summary> 적의 단일-적 공격을 자신에게 끌어오는 도발 효과를 만든다(자기 대상). </summary>
        /// <param name="duration">지속턴(양수) 또는 -1(상시).</param>
        public static StatusEffect Taunt(int duration, SkillSO source = null)
            => new StatusEffect(EffectType.Taunt, StatType.None, 0, 0, 0, duration, source);

        /// <summary>
        /// 들어온 피해를 실드로 흡수하고, 실드로 다 막지 못한 나머지 피해를 반환한다.
        /// 남은 흡수량이 그만큼 줄어든다.
        /// </summary>
        /// <param name="incoming">막아야 할 피해량(0 이상).</param>
        /// <returns>실드가 흡수하고 남아 HP로 넘어갈 피해량.</returns>
        public int AbsorbDamage(int incoming)
        {
            int used = Math.Min(RemainingAbsorb, incoming);
            RemainingAbsorb -= used;
            return incoming - used;
        }

        /// <summary> 남은 지속턴을 1 줄인다. 상시(-1)나 이미 0인 효과는 그대로 둔다. </summary>
        public void TickDuration()
        {
            if (RemainingTurns > 0)
                RemainingTurns--;
        }
    }
}