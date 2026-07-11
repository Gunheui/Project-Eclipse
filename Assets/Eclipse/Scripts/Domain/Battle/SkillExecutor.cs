using System.Collections.Generic;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 한 유닛의 스킬 하나를 실행한다. 스킬의 효과 목록을 순회하며 효과마다 대상을 뽑아 적용한다.
    /// 수동·오토·적이 모두 이 경로를 공유한다(행동을 누가 골랐는지만 상위에서 다르다).
    /// 효과 크기 계산은 CombatPipeline에 맡기고, 대상 선택과 효과 적용은 이곳이 담당한다.
    /// 즉발(데미지·힐)과 지속 효과(버프·디버프·도트·리젠·실드·도발)를 적용한다.
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
        /// <param name="actor">스킬을 쓰는 유닛. 데미지·힐 세기의 기준 스탯을 제공한다.</param>
        /// <param name="skill">실행할 스킬의 런타임(정의를 통해 효과 목록을 읽는다).</param>
        /// <param name="chosenTarget">수동 지정 대상. null이면 효과별 TargetSelector가 대상을 정한다.</param>
        /// <param name="allies">행동자 편의 유닛 목록.</param>
        /// <param name="enemies">상대 편의 유닛 목록.</param>
        public void Execute(
            ICombatant actor, SkillRuntime skill, ICombatant chosenTarget,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies)
        {
            foreach (var effect in skill.Skill.effects)
            {
                var targets = _targeting.Resolve(effect.target, actor, allies, enemies, chosenTarget);

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
        }
    }
}