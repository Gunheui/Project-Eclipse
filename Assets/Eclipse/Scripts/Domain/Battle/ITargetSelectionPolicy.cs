using System.Collections.Generic;

namespace Eclipse.Domain
{
    /// <summary>
    /// 단일-적 데미지 스킬의 주 타겟을 고르는 결정 계층. 오토·적 AI가 스킬을 고른 뒤
    /// 이 정책에 주 타겟을 물어 BattleAction.Target에 담는다(수동은 플레이어 지정이 직접 채운다).
    /// "범위 → 유닛" 변환은 TargetResolver가, "그 단일 범위에서 누구를"은 이 정책이 담당한다.
    /// </summary>
    public interface ITargetSelectionPolicy
    {
        /// <summary> 스킬의 첫 단일-적 데미지 효과를 주 효과로 보고 그 대상을 고른다. </summary>
        /// <returns>주 타겟. 단일-적 데미지 스킬이 아니거나 유효 후보가 없으면 null.</returns>
        ICombatant ChoosePrimaryTarget(
            ICombatant actor, SkillRuntime skill,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies);
    }
}
