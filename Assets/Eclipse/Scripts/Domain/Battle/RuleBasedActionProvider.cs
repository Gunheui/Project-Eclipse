using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 우선순위 규칙으로 행동을 고르는 결정 주체. 아군 오토와 적 AI가 임계값·규칙 on/off만
    /// 달리해 같은 로직을 공유한다. 대상은 지정하지 않고(Target=null) 각 효과의 TargetSelector에 맡긴다.
    /// 규칙 우선순위(위에서부터): ① 아군 위급 시 힐 → ② 준비된 강한 공격 액티브 → ③ 기본공격.
    /// </summary>
    public class RuleBasedActionProvider : IActionProvider
    {
        private readonly float _healHpThreshold;
        private readonly bool _useHealRule;

        /// <param name="healHpThreshold">힐 규칙 발동 HP 비율(0~1). 예: 0.4는 아군이 최대 HP의 40% 이하일 때 발동.</param>
        /// <param name="useHealRule">힐 규칙 사용 여부. 힐 스킬이 없는 프로파일은 false로 둬 판정을 건너뛴다.</param>
        public RuleBasedActionProvider(float healHpThreshold, bool useHealRule)
        {
            _healHpThreshold = healHpThreshold;
            _useHealRule = useHealRule;
        }

        /// <summary>
        /// 우선순위 규칙을 위에서부터 평가해 이번 턴 행동을 고른다. 대상은 지정하지 않는다(Target=null).
        /// 기본공격은 쿨 0이라 항상 준비돼 있어 빈 행동이 나오지 않는다.
        /// </summary>
        /// <param name="actor">행동할 유닛.</param>
        /// <param name="allies">행동자 편의 유닛 목록(힐 발동 판정에 쓴다).</param>
        /// <param name="enemies">상대 편의 유닛 목록.</param>
        /// <returns>사용할 스킬을 담은 행동(대상 미지정).</returns>
        public BattleAction Decide(
            ICombatant actor,
            IReadOnlyList<ICombatant> allies,
            IReadOnlyList<ICombatant> enemies)
        {
            var skills = actor.Skills;

            if (_useHealRule && AnyAllyBelowThreshold(allies)) // 1. 힐 사용을 해야할 경우
            {
                var heal = ReadyHealSkill(skills);
                if (heal != null) return new BattleAction(heal); // 힐 스킬이 있으면 실행
            }

            var strong = StrongestReadyOffensive(skills); // 강한 공격 스킬 순 (궁극기 -> 액티브 -> 기본공격)
            if (strong != null) return new BattleAction(strong); // 공격

            return new BattleAction(BasicSkill(skills)); // 없으면 기본 스킬
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