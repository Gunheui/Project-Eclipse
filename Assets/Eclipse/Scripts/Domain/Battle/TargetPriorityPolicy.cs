using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 단일-적 데미지 스킬의 주 타겟을 계층형 우선순위 사다리로 고른다. 아군 오토와 적 AI가 이 한 클래스를
    /// 공유하고, 활성 층·기저 규칙은 <see cref="TargetPolicyProfile"/>로만 달라진다.
    /// 후보 출처는 <see cref="TargetResolver.ValidEnemyTargets"/>(생존 + 도발 필터)라 수동 지정과 같은 규칙을
    /// 타므로 "고른 대상 = 맞는 대상" 계약이 오토에도 성립한다. 난수는 데미지와 분리된 타겟 전용 스트림을 쓴다
    /// (같은 시드 → 같은 선택, 재현 유지).
    /// </summary>
    public sealed class TargetPriorityPolicy : ITargetSelectionPolicy
    {
        // HP 비율 동률 판정 허용오차(정수 HP라 풀피 동률은 정확히 일치하고, 중간 동률만 근사 흡수).
        private const float RatioEpsilon = 1e-4f;

        private readonly TargetResolver _targeting;
        private readonly CombatPipeline _combat;
        private readonly IRandomService _rng;
        private readonly TargetPolicyProfile _profile;

        /// <param name="targeting">후보 산출(ValidManualTargets)을 위임할 리졸버.</param>
        /// <param name="combat">막타 판정용 무난수 피해 미리보기(PreviewDamage) 제공.</param>
        /// <param name="rng">타겟 선택 전용 난수 — 데미지 난수와 분리된 스트림이어야 한다.</param>
        /// <param name="profile">아군/적 프로파일(활성 층·기저 규칙).</param>
        public TargetPriorityPolicy(
            TargetResolver targeting, CombatPipeline combat,
            IRandomService rng, TargetPolicyProfile profile)
        {
            _targeting = targeting;
            _combat = combat;
            _rng = rng;
            _profile = profile;
        }

        /// <summary>
        /// 사다리를 위에서부터 평가해 주 타겟을 고른다(위→아래): 도발(후보 좁힘) → [막타] → [이음새들] →
        /// 기저(아군=최저HP 버킷+시드추첨 / 적=저HP 약가중 시드랜덤). 사다리 최상위 "수동 지정"은 이 정책이 아니라
        /// 상위(플레이어 Submit → BattleAction.Target)가 직접 처리한다.
        /// </summary>
        /// <returns>주 타겟. 단일-적 데미지 스킬이 아니거나 유효 후보가 없으면 null.</returns>
        public ICombatant ChoosePrimaryTarget(
            ICombatant actor, SkillRuntime skill,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies)
        {
            var primary = FirstSingleEnemyDamage(skill.Skill);
            if (primary == null) return null;

            // 도발 층: ValidManualTargets가 생존 적을 도발자로 좁혀 준다(도발이 없으면 생존 적 전체).
            var candidates = _targeting.ValidEnemyTargets(enemies);
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            // 막타 층: 어떤 난수값에도 처치되는 후보가 있으면 LethalChance로 그 중 하나(아군=항상, 적=부분 확률).
            if (_profile.LethalChance > 0f)
            {
                var lethal = LethalTarget(actor, primary.Value.value, candidates);
                if (lethal != null && ShouldTakeLethal()) return lethal;
            }

            // [이음새·스킬조건] 스킬별 타겟 조건 훅. 현재 no-op.
            // [이음새·속성상성] 속성 도입 시 약점 대상 우선. 현재 no-op. → DamagePipeline [보류 I]
            // [이음새·어그로] 전열 가중은 기저 층의 Weight에서 프로파일 계수로 실현된다.

            // 기저 층.
            return _profile.BaseTier == TargetBaseTier.AllyLowestHpBucket
                ? LowestHpBucketPick(candidates)   // 아군: 최저 HP비율 버킷에서 시드 추첨(전원 풀피면 랜덤)
                : WeightedLowHpPick(candidates);   // 적: 저HP 약가중 시드 랜덤
        }

        // 스킬 효과 목록에서 첫 단일-적 데미지 효과("주 효과"). 없으면 null.
        private static SkillEffect? FirstSingleEnemyDamage(SkillSO skill)
        {
            foreach (var e in skill.effects)
                if (e.type == EffectType.Damage && TargetResolver.IsSingleEnemy(e.target))
                    return e;
            return null;
        }

        // 어떤 난수값에도(치명 없음·변동 하한) 처치되는 후보 중 유효 HP가 가장 낮은 하나. 동률은 슬롯 낮은 쪽. 없으면 null.
        private ICombatant LethalTarget(ICombatant actor, float skillPower, IReadOnlyList<ICombatant> candidates)
            => candidates
                .Where(t => _combat.PreviewDamage(actor.EffectiveStats, t.EffectiveStats, skillPower) >= EffectiveHp(t))
                .OrderBy(EffectiveHp)
                .ThenBy(t => t.SlotIndex)
                .FirstOrDefault();

        // 처치 가능한 대상을 실제로 마무리할지 LethalChance로 난수를 소비해 판정한다. 실패하면 호출부가 기저 층으로 내려간다.
        // 확률 1(아군 오토)이면 난수를 소비하지 않고 통과시킨다 — 난수 소비가 없어야 막타 층 도입 전과 난수 수열이
        // 같아지고, 기존 시드 재현(같은 시드 = 같은 타겟)이 깨지지 않는다.
        private bool ShouldTakeLethal()
            => _profile.LethalChance >= 1f || _rng.NextFloat() < _profile.LethalChance;

        // 현재 실드값을 포함한 전체 체력
        private static int EffectiveHp(ICombatant unit) => unit.CurrentHp + unit.ShieldAbsorb;

        // 최저 HP비율 후보(동률 버킷)에서 시드 난수로 하나. 전원 풀피면 버킷=전체라 무작위 타겟이 된다.
        private ICombatant LowestHpBucketPick(IReadOnlyList<ICombatant> candidates)
        {
            float min = candidates.Min(HpRatio);
            var bucket = candidates
                .Where(t => HpRatio(t) <= min + RatioEpsilon)
                .OrderBy(t => t.SlotIndex)
                .ToList();
            return PickUniform(bucket);
        }

        // 저HP일수록 약하게 가중한 시드 랜덤. 가중치 = 1 + LowHpBias × (1 − HP비율). 전원 풀피면 균등 랜덤.
        private ICombatant WeightedLowHpPick(IReadOnlyList<ICombatant> candidates)
        {
            var ordered = candidates.OrderBy(t => t.SlotIndex).ToList();

            float total = 0f;
            foreach (var t in ordered) total += Weight(t);

            float roll = _rng.NextFloat() * total;
            float cumulative = 0f;
            foreach (var t in ordered)
            {
                cumulative += Weight(t);
                if (roll < cumulative) return t;
            }
            return ordered[ordered.Count - 1]; // 부동소수 잔차 방어(roll이 total에 근접한 경우).
        }

        // ordered(슬롯 오름차순)에서 시드 난수로 균등 추첨. NextFloat는 [0,1)이라 idx는 항상 범위 내.
        private ICombatant PickUniform(IReadOnlyList<ICombatant> ordered)
        {
            if (ordered.Count == 1) return ordered[0];
            int idx = (int)(_rng.NextFloat() * ordered.Count);
            if (idx >= ordered.Count) idx = ordered.Count - 1; // 방어적 클램프.
            return ordered[idx];
        }

        // 저HP 가중 × 전열 가중. 전열 계수 1(아군 프로파일 기본)이면 기존 저HP 가중과 동일하다.
        private float Weight(ICombatant unit)
            => (1f + _profile.LowHpBias * (1f - HpRatio(unit)))
               * (IsFrontLine(unit) ? _profile.FrontLineWeight : 1f);

        // 아군 편성 앵커 기준 전열 = 편성 2·4번(slotIndex 1·3). 적 진영 앵커는 전열이 0·1이라
        // 이 술어가 성립하지 않는다 — 적 프로파일(=아군을 후보로 삼는 경로) 전용.
        private static bool IsFrontLine(ICombatant unit) => unit.SlotIndex % 2 == 1;

        private static float HpRatio(ICombatant unit) => (float)unit.CurrentHp / unit.MaxHp;
    }
}
