using System.Collections.Generic;

namespace Eclipse.Domain
{
    /// <summary>
    /// 한 유닛의 턴에 어떤 행동(스킬·대상)을 할지 고르는 결정 주체.
    /// 전투 진행(턴·데미지·효과)은 하나의 경로로 두고, 이 구현체만 갈아끼워
    /// 오토·적 AI·수동 입력을 교체한다.
    /// </summary>
    public interface IActionProvider
    {
        /// <summary>
        /// 행동자가 이번 턴에 할 행동을 결정한다.
        /// </summary>
        /// <param name="actor">행동할 유닛.</param>
        /// <param name="allies">행동자 편의 유닛 목록.</param>
        /// <param name="enemies">상대 편의 유닛 목록.</param>
        /// <returns>사용할 스킬과 (있으면) 지정 대상. 쓸 스킬이 없으면 Skill이 null.</returns>
        BattleAction Decide(
            ICombatant actor,
            IReadOnlyList<ICombatant> allies,
            IReadOnlyList<ICombatant> enemies);
    }
}