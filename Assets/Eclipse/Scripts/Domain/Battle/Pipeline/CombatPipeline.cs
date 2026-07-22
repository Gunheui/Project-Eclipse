using System;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 스킬 효과의 HP 변화량 계산 진입점(파사드). 데미지 다단계 계산은 DamagePipeline에 위임하고,
    /// ATK 배율 기반 크기는 여기서 직접 계산한다. 순수 계산만 담당하며 타겟 선택·HP 적용은 SkillExecutor의 몫이다.
    /// </summary>
    public class CombatPipeline
    {
        private readonly DamagePipeline _damage;

        public CombatPipeline(DamagePipeline damage)
        {
            _damage = damage;
        }

        /// <summary> 공격자·대상 스탯과 스킬 배율로 피해량을 계산한다(경감·치명·변동 포함). </summary>
        public DamageResult ComputeDamage(Stats attacker, Stats target, float power)
            => _damage.ComputeDamage(attacker, target, power);

        /// <summary>
        /// 난수 없이 피해 하한을 추정한다(치명 없음·변동 하한, 1 이상). 이 값이 대상 현재 HP 이상이면 확정 처치.
        /// 오토 타겟 정책의 막타(lethal) 판정 전용으로, 실제 데미지 난수 수열을 소비하지 않는다.
        /// </summary>
        public int PreviewDamage(Stats attacker, Stats target, float power)
            => _damage.EstimateMinDamage(attacker, target, power);

        /// <summary> 시전자 ATK × 배율로 회복량을 계산한다(경감·치명·변동 없음, 최소 1). </summary>
        public int ComputeHeal(Stats attacker, float power)
            => Math.Max(1, (int)Math.Round(attacker.atk * power, MidpointRounding.AwayFromZero));

        /// <summary>
        /// 도트·리젠의 틱당 HP 변화량을 계산한다(시전자 ATK × 배율, 경감·치명·난수 없음, 최소 1).
        /// 적용 시점에 한 번 계산해 효과에 스냅샷하므로 시드 고정 없이도 결정적이다.
        /// </summary>
        public int ComputeTickAmount(Stats caster, float power)
            => ComputeHeal(caster, power);

        /// <summary> 실드가 흡수할 총 피해량을 계산한다(대상 최대 HP × 비율(0.3 = 30%), 최소 1). </summary>
        public int ComputeShield(int maxHp, float ratio)
            => Math.Max(1, (int)Math.Round(maxHp * ratio, MidpointRounding.AwayFromZero));
    }
}
