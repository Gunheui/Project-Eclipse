using System;
using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 직전 턴에 실제로 벌어진 일(행동자·사용 스킬·효과 결과). 연출이 시전/피격 이펙트와 숫자를 붙이는 데 쓴다.
    /// 스킬을 안 쓴 턴(도트 사망 등)은 Skill이 null, Hits가 빈 목록.
    /// </summary>
    public readonly struct TurnResult
    {
        /// <summary> 이 턴에 행동한 유닛. </summary>
        public ICombatant Actor { get; }

        /// <summary> 이 턴에 사용된 스킬. 안 썼으면 null. </summary>
        public SkillSO Skill { get; }

        /// <summary> 이 스킬이 남긴 결과들. 없으면 빈 목록. 여러 번 맞은 대상은 맞은 횟수만큼 들어온다. </summary>
        public IReadOnlyList<EffectResult> Hits { get; }

        public TurnResult(ICombatant actor, SkillSO skill, IReadOnlyList<EffectResult> hits)
        {
            Actor = actor;
            Skill = skill;
            Hits = hits ?? Array.Empty<EffectResult>();
        }

        /// <summary> 이 턴에 스킬을 실제로 사용했는지. </summary>
        public bool UsedSkill => Skill != null;

        /// <summary> 아직 아무 턴도 없거나 스킬을 안 쓴 상태를 나타내는 빈 결과. </summary>
        public static readonly TurnResult None = new(null, null, Array.Empty<EffectResult>());
    }
}
