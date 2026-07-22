using System.Collections.Generic;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 스킬 하나를 실행한다. 효과 목록을 순회하며 효과마다 대상을 뽑아 적용하고, 수동·오토·적이 모두
    /// 이 경로를 공유한다. 효과 크기 계산은 CombatPipeline에 맡기고 대상 선택·효과 적용만 담당한다.
    /// </summary>
    public class SkillExecutor
    {
        private readonly CombatPipeline _calc;
        private readonly TargetResolver _targeting;

        public SkillExecutor(CombatPipeline calc, TargetResolver targeting)
        {
            _calc = calc;
            _targeting = targeting;
        }

        /// <summary>
        /// 스킬의 모든 효과를 대상에게 적용한다. HP 변경 등 부수효과가 대상 유닛에 즉시 반영된다.
        /// 쿨 소모(TryUse)는 호출부에서 이미 처리했다고 전제한다.
        /// </summary>
        /// <param name="chosenTarget">수동 지정 대상. null이면 효과별 TargetSelector가 정한다.</param>
        /// <returns>이 스킬로 영향받은 대상들(중복 제거). 연출이 피격 이펙트를 붙일 대상으로 쓴다.</returns>
        public IReadOnlyList<ICombatant> ApplySkill(
            ICombatant actor, SkillRuntime skill, ICombatant chosenTarget,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies)
        {
            var affected = new List<ICombatant>();

            foreach (var effect in skill.Skill.effects)
            {
                var targets = _targeting.Resolve(effect.target, actor, allies, enemies, chosenTarget);

                foreach (var target in targets)
                    if (!affected.Contains(target)) affected.Add(target);

                switch (effect.type)
                {
                    case EffectType.Damage:
                        foreach (var target in targets)
                        {
                            var result = _calc.ComputeDamage(actor.EffectiveStats, target.EffectiveStats, effect.value);
                            ((IDamageable)target).ApplyDamage(result.Amount);
                        }
                        break;

                    case EffectType.Heal:
                        foreach (var target in targets)
                        {
                            var amount = _calc.ComputeHeal(actor.EffectiveStats, effect.value);
                            ((IDamageable)target).Heal(amount);
                        }
                        break;

                    case EffectType.Buff:
                    case EffectType.Debuff:
                        foreach (var target in targets)
                            ((IDamageable)target).ApplyEffect(
                                StatusEffect.StatModifier(effect.type, effect.affectedStat, effect.value, effect.duration));
                        break;

                    case EffectType.Dot:
                    case EffectType.Regen:
                        foreach (var target in targets)
                        {
                            var tick = _calc.ComputeTickAmount(actor.EffectiveStats, effect.value);
                            ((IDamageable)target).ApplyEffect(StatusEffect.Periodic(effect.type, tick, effect.duration));
                        }
                        break;

                    case EffectType.Shield:
                        foreach (var target in targets)
                        {
                            var absorb = _calc.ComputeShield(target.MaxHp, effect.value);
                            ((IDamageable)target).ApplyEffect(StatusEffect.Shield(absorb, effect.duration));
                        }
                        break;

                    case EffectType.Taunt:
                        foreach (var target in targets)
                            ((IDamageable)target).ApplyEffect(StatusEffect.Taunt(effect.duration));
                        break;
                }
            }

            return affected;
        }
    }
}