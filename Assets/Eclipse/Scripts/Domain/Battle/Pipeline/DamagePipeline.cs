using System;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 한 번의 피해를 단일 경로로 계산한다. 모든 계수(방어경감 k·변동폭)는 데이터에서 주입받고
    /// 코드는 계산 구조만 담는다. 치명·변동 난수는 IRandomService라 시드 고정 재현이 가능하다.
    /// 계산은 즉시 끝나며(연출과 분리), 수동·오토가 이 한 경로를 공유한다.
    /// </summary>
    public class DamagePipeline
    {
        private readonly float _defenseK;

        // 데미지 난수 변동의 하한·상한 배율.
        private readonly float _varianceMin;
        private readonly float _varianceMax;
        
        private readonly IRandomService _rng;

        /// <param name="defenseK">비율경감 계수 k — 경감계수 = ATK/(ATK+DEF×k).</param>
        /// <param name="varianceMin">난수변동 하한(예: 0.95).</param>
        /// <param name="varianceMax">난수변동 상한(예: 1.05).</param>
        /// <param name="rng">치명·변동용 결정적 난수.</param>
        public DamagePipeline(float defenseK, float varianceMin, float varianceMax, IRandomService rng)
        {
            _defenseK = defenseK;
            _varianceMin = varianceMin;
            _varianceMax = varianceMax;
            _rng = rng;
        }

        /// <summary> 공격자가 대상에게 스킬계수 skillPower(기본공격 1.0)로 주는 피해를 계산한다. </summary>
        public DamageResult ComputeDamage(Stats attacker, Stats target, float skillPower)
        {
            // [보류 H · 명중 판정] 빗나감 도입 시 파이프라인 맨 앞에서 rng로 판정하고,
            // DamageResult에 IsMiss 플래그를 되살려 miss면 조기 반환한다.

            // 난수 순서: 치명 먼저, 변동 나중.
            bool isCrit = _rng.NextFloat() < attacker.critRate;
            float variance = _varianceMin + _rng.NextFloat() * (_varianceMax - _varianceMin);

            return new DamageResult(Compute(attacker, target, skillPower, isCrit, variance), isCrit);
        }

        /// <summary>
        /// 난수를 소비하지 않는 피해 하한 추정(치명 없음 + 변동 하한, 1 이상). 이 값이 대상 현재 HP 이상이면
        /// 확정 처치다(오탐 없음). 오토 타겟 정책의 막타(lethal) 판정 전용으로, 실제 난수 수열에 영향이 없다.
        /// [이음새·실드lethal] 대상 실드 잔량은 반영하지 않아 과대 추정될 수 있으나, 오판은 차선 타겟일 뿐 버그가 아니다.
        /// </summary>
        public int EstimateMinDamage(Stats attacker, Stats target, float skillPower)
            => Compute(attacker, target, skillPower, isCrit: false, variance: _varianceMin);

        /// <summary>
        /// 피해 공식 본체. 난수 결과(isCrit·variance)를 주입받아 계산만 한다(난수 비소비).
        /// 실제 피해와 하한 추정이 이 한 함수를 공유하므로 공식이 갈라지지 않는다.
        /// </summary>
        private int Compute(Stats attacker, Stats target, float skillPower, bool isCrit, float variance)
        {
            // 1) 기본값 = ATK × 스킬계수
            float raw = attacker.atk * skillPower;

            // 2) 경감후 = 기본값 × ATK/(ATK + DEF×k). 비율경감이라 DEF가 올라도 0으로 수렴하지 않고,
            //    ATK가 높을수록 방어를 잘 뚫는다.
            float mitigation = attacker.atk / (attacker.atk + target.def * _defenseK);
            float mitigated = raw * mitigation;

            // [보류 I · 속성 상성] 도입 시 여기서 mitigated *= typeMultiplier;

            // 3) 치명적용 = 경감후 × (치명 시 CRIT_D, else 1)
            float critApplied = isCrit ? mitigated * attacker.critDamage : mitigated;

            // 4) 최종 = 치명적용 × 난수변동, 반올림 후 최소 1 보장.
            //    Math.Round 기본은 짝수 쪽 반올림(은행가 방식)이라 AwayFromZero를 적용한다.
            return Math.Max(1, (int)Math.Round(critApplied * variance, MidpointRounding.AwayFromZero));
        }
    }
}