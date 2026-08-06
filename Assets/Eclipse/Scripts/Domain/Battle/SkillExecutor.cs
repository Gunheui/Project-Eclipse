using System.Collections.Generic;
using System.Linq;
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
        /// <returns>
        /// 이 스킬이 남긴 결과들. 연출이 숫자와 피격 이펙트를 붙이는 데 쓴다.
        /// 피해는 때린 횟수만큼, 나머지 효과는 대상마다 한 번만 들어간다.
        /// </returns>
        public IReadOnlyList<EffectResult> ApplySkill(
            ICombatant actor, SkillRuntime skill, ICombatant chosenTarget,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies)
        {
            var results = new List<EffectResult>();

            // 강화 배수는 세기가 공격력 배율인 효과(Damage/Heal/Dot/Regen)에만 곱한다.
            // Buff/Debuff/Shield의 value는 증감률이라 배수를 곱하면 기획한 비율이 어긋난다.
            float power = skill.PowerMultiplier;

            foreach (var effect in skill.Effects)
            {
                var targets = _targeting.Resolve(effect.target, actor, allies, enemies, chosenTarget);

                switch (effect.type)
                {
                    case EffectType.Damage:
                        foreach (var target in targets)
                        {
                            var damage = _calc.ComputeDamage(actor.EffectiveStats, target.EffectiveStats, effect.value * power);
                            int shieldBefore = target.ShieldAbsorb;
                            ((IDamageable)target).ApplyDamage(damage.Amount);
                            // 남은 HP로 깎지 않는다 — 그러면 마무리 일격이 남은 HP만큼만 뜬다.
                            AddHit(results, new EffectResult(EffectType.Damage, target, damage.Amount,
                                shielded: target.ShieldAbsorb < shieldBefore, isCrit: damage.IsCrit));
                        }
                        break;

                    case EffectType.Heal:
                        foreach (var target in targets)
                        {
                            var amount = _calc.ComputeHeal(actor.EffectiveStats, effect.value * power);
                            int hpBefore = target.CurrentHp;
                            ((IDamageable)target).Heal(amount);
                            // 넘친 회복은 실제로 아무 일도 안 하고 대신 보여줄 짝도 없어서, 채운 만큼만 싣는다.
                            AddOnce(results, new EffectResult(EffectType.Heal, target, target.CurrentHp - hpBefore));
                        }
                        break;

                    case EffectType.Buff:
                    case EffectType.Debuff:
                        foreach (var target in targets)
                        {
                            ((IDamageable)target).ApplyEffect(StatusEffect.StatModifier(
                                effect.type, effect.affectedStat, effect.value, effect.duration, skill.Skill));
                            AddOnce(results, new EffectResult(effect.type, target));
                        }
                        break;

                    case EffectType.Dot:
                    case EffectType.Regen:
                        foreach (var target in targets)
                        {
                            var tick = _calc.ComputeTickAmount(actor.EffectiveStats, effect.value * power);
                            ((IDamageable)target).ApplyEffect(
                                StatusEffect.Periodic(effect.type, tick, effect.duration, skill.Skill));
                            AddOnce(results, new EffectResult(effect.type, target));
                        }
                        break;

                    case EffectType.Shield:
                        foreach (var target in targets)
                        {
                            var absorb = _calc.ComputeShield(target.MaxHp, effect.value);
                            ((IDamageable)target).ApplyEffect(
                                StatusEffect.Shield(absorb, effect.duration, skill.Skill));
                            AddOnce(results, new EffectResult(EffectType.Shield, target));
                        }
                        break;

                    case EffectType.Taunt:
                        foreach (var target in targets)
                        {
                            ((IDamageable)target).ApplyEffect(StatusEffect.Taunt(effect.duration, skill.Skill));
                            AddOnce(results, new EffectResult(EffectType.Taunt, target));
                        }
                        break;
                }
            }

            return results;
        }

        /// <summary> 그 대상의 기록이 아직 없을 때만 담는다. 한 스킬이 같은 대상에 여러 효과를 걸어도 피격은 한 번이다. </summary>
        private static void AddOnce(List<EffectResult> results, EffectResult result)
        {
            if (results.All(r => r.Target != result.Target)) results.Add(result);
        }

        /// <summary>
        /// 피해 기록을 담는다. 같은 대상에 수치 없는 기록이 이미 있으면 그 자리를 차지하고, 없으면 새로 쌓는다.
        /// </summary>
        private static void AddHit(List<EffectResult> results, EffectResult hit)
        {
            // 디버프를 걸고 때리는 스킬은 대상 기록이 둘이 돼 타격 이펙트가 두 번 터진다. 수치를 든 피해가
            // 자리를 물려받아 한 건으로 남는다. 피해끼리는 겹쳐 쌓여 멀티히트 타수가 화면에 보인다.
            int slot = results.FindIndex(r => r.Target == hit.Target && r.Amount == 0);
            if (slot >= 0) results[slot] = hit;
            else results.Add(hit);
        }
    }
}