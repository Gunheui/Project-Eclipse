using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 우선순위 규칙으로 행동을 고르는 결정 주체. 아군 오토와 적 AI가 임계값·규칙 on/off만
    /// 달리해 같은 로직을 공유한다. 스킬을 고르는 층만 여기서 담당하고, 단일-적 스킬의 주 대상은
    /// TargetSelectionPolicy에 물어 BattleAction.Target에 담는다(광역·자기·아군 효과는 Target=null로 두어
    /// 효과별 TargetSelector가 정한다).
    /// 규칙 우선순위(위에서부터): ① 아군 위급 시 힐 → ② 준비된 강한 공격 액티브 → ③ 기본공격.
    /// </summary>
    public class RuleBasedActionProvider : IActionProvider
    {
        // 아군 오토·적 AI 공통 힐 임계값. 아직 튜닝 대상이 아니라 SO가 아닌 상수로 둔다.
        private const float HealHpThreshold = 0.4f;

        private readonly float _healHpThreshold;
        private readonly bool _useHealRule;
        private readonly ITargetSelectionPolicy _targetPolicy;

        /// <param name="healHpThreshold">힐 규칙 발동 HP 비율(0~1). 예: 0.4는 아군이 최대 HP의 40% 이하일 때 발동.</param>
        /// <param name="useHealRule">힐 규칙 사용 여부. 힐 스킬이 없는 프로파일은 false로 둬 판정을 건너뛴다.</param>
        /// <param name="targetPolicy">단일-적 스킬의 주 타겟을 고르는 정책(아군/적 프로파일이 다르다).</param>
        public RuleBasedActionProvider(float healHpThreshold, bool useHealRule, ITargetSelectionPolicy targetPolicy)
        {
            _healHpThreshold = healHpThreshold;
            _useHealRule = useHealRule;
            _targetPolicy = targetPolicy;
        }

        /// <summary>
        /// 아군 오토 프로바이더. 스킬 선택 규칙은 적 AI와 공유(임계 40%·힐 on)하되 타겟 정책만
        /// 프로파일을 달리한다 — 아군 오토 = 막타 + 최저HP 버킷 시드 추첨.
        /// </summary>
        /// <param name="rng">타겟 선택 전용 난수. 데미지 난수와 분리된 스트림을 넘겨야 하며,
        /// 적 AI와 같은 인스턴스를 공유해야 굴림 순서(=시드 재현)가 유지된다.</param>
        public static RuleBasedActionProvider AllyAuto(
                TargetResolver targeting, CombatPipeline combat, IRandomService rng)
            => new(HealHpThreshold, useHealRule: true,
                   new TargetPriorityPolicy(targeting, combat, rng, TargetPolicyProfile.AllyAuto));

        /// <summary>
        /// 적 AI 프로바이더. 스킬 선택 규칙은 아군 오토와 공유하되 타겟 정책만 달리한다 —
        /// 적 AI = 부분 확률 막타 + 저HP 약가중 시드 랜덤(일부러 완벽하지 않게).
        /// </summary>
        /// <param name="rng">타겟 선택 전용 난수. <see cref="AllyAuto"/>와 같은 인스턴스를 넘긴다.</param>
        /// <param name="lethalChance">막타 층 채택 확률(0=막타 안 침, 1=아군 오토와 동일).</param>
        /// <param name="lowHpBias">저HP 가중치(0=무작위, 1=최저HP 확정).</param>
        public static RuleBasedActionProvider EnemyAi(
                TargetResolver targeting, CombatPipeline combat, IRandomService rng,
                float lethalChance, float lowHpBias)
            => new(HealHpThreshold, useHealRule: true,
                   new TargetPriorityPolicy(targeting, combat, rng,
                       TargetPolicyProfile.EnemyAi(lethalChance, lowHpBias)));

        /// <summary>
        /// 우선순위 규칙으로 스킬을 고르고, 단일-적 스킬이면 정책이 고른 주 타겟을 함께 담아 행동을 만든다.
        /// 기본공격은 쿨 0이라 항상 준비돼 있어 빈 행동이 나오지 않는다.
        /// </summary>
        /// <param name="actor">행동할 유닛.</param>
        /// <param name="allies">행동자 편의 유닛 목록(힐 발동 판정에 쓴다).</param>
        /// <param name="enemies">상대 편의 유닛 목록(주 타겟 후보 출처).</param>
        /// <param name="ct">사용하지 않는다(규칙 판정은 대기 없이 즉시 완료된다). 계약 호환을 위해 받는다.</param>
        /// <returns>사용할 스킬과 (단일-적이면) 주 타겟을 담은 행동. 즉시 완료된 태스크로 반환한다.</returns>
        public UniTask<BattleAction> ChooseActionAsync(
            ICombatant actor,
            IReadOnlyList<ICombatant> allies,
            IReadOnlyList<ICombatant> enemies,
            CancellationToken ct)
        {
            var skill = ChooseSkill(actor.Skills, allies);

            // 단일-적 데미지 스킬이면 정책이 주 타겟을 고른다. 아니면(광역·힐·자기·스킬 없음) null → 효과별 스코프가 정한다.
            var target = skill != null ? _targetPolicy.ChoosePrimaryTarget(actor, skill, allies, enemies) : null;
            return UniTask.FromResult(new BattleAction(skill, target));
        }

        // 우선순위 규칙을 위에서부터 평가해 이번 턴 사용할 스킬을 고른다. 쓸 스킬이 없으면 null.
        private SkillRuntime ChooseSkill(IReadOnlyList<SkillRuntime> skills, IReadOnlyList<ICombatant> allies)
        {
            if (_useHealRule && AnyAllyBelowThreshold(allies)) // 1. 힐 사용을 해야할 경우
            {
                var heal = ReadyHealSkill(skills);
                if (heal != null) return heal; // 힐 스킬이 있으면 실행
            }

            var strong = StrongestReadyOffensive(skills); // 강한 공격 스킬 순 (궁극기 -> 액티브 -> 기본공격)
            if (strong != null) return strong; // 공격

            return BasicSkill(skills); // 없으면 기본 스킬
        }

        private bool AnyAllyBelowThreshold(IReadOnlyList<ICombatant> allies)
            => allies.Any(u => u.IsAlive && (float)u.CurrentHp / u.MaxHp <= _healHpThreshold);

        // 준비된 스킬 중 힐 효과를 가진 첫 스킬. 없으면 null.
        private static SkillRuntime ReadyHealSkill(IReadOnlyList<SkillRuntime> skills)
            => skills.FirstOrDefault(s => s.IsReady && HasEffect(s.Skill, EffectType.Heal));

        // 기본공격(슬롯 0)을 제외한 준비된 공격 액티브 중 가장 상위 슬롯. 없으면 null.
        private static SkillRuntime StrongestReadyOffensive(IReadOnlyList<SkillRuntime> skills)
        {
            for (int i = skills.Count - 1; i >= 1; i--)
                if (skills[i].IsReady && HasEffect(skills[i].Skill, EffectType.Damage))
                    return skills[i];
            return null;
        }

        // 기본공격 = 슬롯 0(쿨 0이라 항상 준비).
        private static SkillRuntime BasicSkill(IReadOnlyList<SkillRuntime> skills)
            => skills.Count > 0 ? skills[0] : null;

        private static bool HasEffect(SkillSO skill, EffectType type)
            => skill.effects.Any(e => e.type == type);
    }
}