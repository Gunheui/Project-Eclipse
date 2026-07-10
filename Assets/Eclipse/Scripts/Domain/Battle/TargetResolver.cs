using System.Collections.Generic;
using System.Linq;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 스킬 효과의 TargetSelector를 실제 대상 유닛 목록으로 바꾼다.
    /// 생존한 유닛만 고르고, 단일 대상의 동률은 슬롯 번호가 낮은 쪽을 택해 항상 같은 결과를 낸다.
    /// allies/enemies는 행동자 관점의 아군/적 목록이다.
    /// </summary>
    public class TargetResolver
    {
        private static readonly IReadOnlyList<ICombatant> Empty = new ICombatant[0];

        /// <summary>
        /// 선택 규칙에 해당하는 대상 목록을 반환한다.
        /// </summary>
        /// <param name="selector">스킬 효과의 대상 선택 규칙.</param>
        /// <param name="actor">스킬을 쓰는 유닛(Self 대상).</param>
        /// <param name="allies">행동자 편의 유닛 목록.</param>
        /// <param name="enemies">상대 편의 유닛 목록.</param>
        /// <returns>대상 유닛 목록. 유효한 대상이 없으면 빈 목록(호출부가 스킵).</returns>
        public IReadOnlyList<ICombatant> Resolve(
            TargetSelector selector, ICombatant actor,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies)
        {
            switch (selector)
            {
                case TargetSelector.Self:
                    return actor.IsAlive ? new[] { actor } : Empty;
                case TargetSelector.AllAllies:
                    return allies.Where(u => u.IsAlive).ToList();
                case TargetSelector.AllEnemies:
                    return enemies.Where(u => u.IsAlive).ToList();
                case TargetSelector.LowestHpAlly:
                    return SingleOrEmpty(LowestHp(allies));
                case TargetSelector.LowestHpEnemy:
                    return SingleOrEmpty(LowestHp(enemies));
                case TargetSelector.HighestAtkEnemy:
                    return SingleOrEmpty(HighestAtk(enemies));
                default:
                    return Empty;
            }
        }

        // 생존 유닛 중 현재 HP가 가장 낮은 하나. 동률은 슬롯 번호가 낮은 쪽. 없으면 null.
        private static ICombatant LowestHp(IReadOnlyList<ICombatant> units)
            => units.Where(u => u.IsAlive)
                    .OrderBy(u => u.CurrentHp) //HP 오름차순
                    .ThenBy(u => u.SlotIndex) //경합일때 슬롯번호 낮은 쪽
                    .FirstOrDefault();

        // 생존 유닛 중 유효 ATK가 가장 높은 하나. 동률은 슬롯 번호가 낮은 쪽. 없으면 null.
        private static ICombatant HighestAtk(IReadOnlyList<ICombatant> units)
            => units.Where(u => u.IsAlive)
                    .OrderByDescending(u => u.EffectiveStats.atk) // 공격력 내림차순
                    .ThenBy(u => u.SlotIndex) // 경합일 때 슬롯번호 낮은 순
                    .FirstOrDefault();

        private static IReadOnlyList<ICombatant> SingleOrEmpty(ICombatant unit)
            => unit == null ? Empty : new[] { unit };
    }
}
