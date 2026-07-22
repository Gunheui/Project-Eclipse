using System.Collections.Generic;
using System.Linq;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 스킬 효과의 TargetSelector를 실제 대상 목록으로 변환한다. 생존자만 고르고, 단일 대상 동률은
    /// 슬롯 번호가 낮은 쪽을 택해 결과가 항상 같다. 단일-적의 "누구를 고르나"는 TargetPriorityPolicy
    /// 소관이고 여기 기본값은 방어적 폴백이다. allies/enemies는 행동자 관점의 목록이다.
    /// </summary>
    public class TargetResolver
    {
        private static readonly IReadOnlyList<ICombatant> Empty = new ICombatant[0];

        /// <summary> 선택 규칙에 해당하는 대상 목록. 유효한 대상이 없으면 빈 목록(호출부가 스킵). </summary>
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
                case TargetSelector.SingleAlly:
                    // 아군 단일의 기본값 = 최저 HP 아군.
                    return SingleOrEmpty(LowestHp(allies));
                case TargetSelector.SingleEnemy:
                    // 정책이 Target을 안 준 경우의 방어적 폴백. 실전 경로는 항상 chosenTarget이 이 기본값을 덮는다.
                    return SingleOrEmpty(FirstAliveBySlot(TauntFiltered(enemies)));
                default:
                    return Empty;
            }
        }

        /// <summary>
        /// 수동 지정을 반영해 대상 목록을 정한다. 지정은 단일-적·단일-아군 selector에서
        /// <see cref="ValidEnemyTargets"/>/<see cref="ValidAllyTargets"/>에 든 대상일 때만 존중하고
        /// (단일-적은 도발자 우선 규칙 안에서), 그 외에는 selector 기본 규칙으로 폴백한다.
        /// </summary>
        /// <param name="chosenTarget">플레이어가 찍은 대상. null이면 selector가 정한다.</param>
        public IReadOnlyList<ICombatant> Resolve(
            TargetSelector selector, ICombatant actor,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies,
            ICombatant chosenTarget)
        {
            if (chosenTarget != null && chosenTarget.IsAlive)
            {
                // 단일-적: 도발 필터를 지키되 그 안에서는 지정을 존중.
                if (IsSingleEnemy(selector)
                    && enemies.Contains(chosenTarget)
                    && (chosenTarget.IsTaunting || !AnyTaunting(enemies)))
                {
                    return new[] { chosenTarget };
                }

                // 단일-아군(힐/버프): 살아있는 아군이면 지정을 존중. 도발 상당 규칙 없음.
                if (IsSingleAlly(selector) && allies.Contains(chosenTarget))
                {
                    return new[] { chosenTarget };
                }
            }

            return Resolve(selector, actor, allies, enemies);
        }

        /// <summary>
        /// 단일-적 스킬로 직접 지정할 수 있는 후보(생존 적, 도발자가 있으면 그들만).
        /// <see cref="Resolve"/>의 지정 존중 조건과 같은 규칙이라, 조준 UI가 이 목록만 선택 가능으로 칠하면
        /// 화면과 판정이 일치한다. 생존한 적이 없으면 빈 목록.
        /// </summary>
        public IReadOnlyList<ICombatant> ValidEnemyTargets(IReadOnlyList<ICombatant> enemies)
        {
            var alive = enemies.Where(u => u.IsAlive).ToList();
            return TauntFiltered(alive);
        }

        /// <summary>
        /// 단일-아군 스킬(힐/버프)로 직접 지정할 수 있는 후보 = 생존 아군(행동자 포함, 도발 상당 규칙 없음).
        /// <see cref="Resolve"/>의 아군 지정 존중 조건과 같은 규칙이다.
        /// </summary>
        public IReadOnlyList<ICombatant> ValidAllyTargets(IReadOnlyList<ICombatant> allies)
            => allies.Where(u => u.IsAlive).ToList();

        /// <summary> 지정 대상 오버라이드가 적용되는 스코프인지(단일-적만 해당). </summary>
        public static bool IsSingleEnemy(TargetSelector selector)
            => selector == TargetSelector.SingleEnemy;

        /// <summary> 지정 대상 오버라이드가 적용되는 아군 스코프인지(단일-아군만 해당). </summary>
        public static bool IsSingleAlly(TargetSelector selector)
            => selector == TargetSelector.SingleAlly;

        private static bool AnyTaunting(IReadOnlyList<ICombatant> enemies)
            => enemies.Any(u => u.IsAlive && u.IsTaunting);

        // 도발 중인 생존 적이 있으면 그들만 후보로 좁힌다. 없으면 원래 후보 그대로.
        private static IReadOnlyList<ICombatant> TauntFiltered(IReadOnlyList<ICombatant> enemies)
        {
            var taunters = enemies.Where(u => u.IsAlive && u.IsTaunting).ToList();
            return taunters.Count > 0 ? taunters : enemies;
        }

        // 생존 유닛 중 최저 HP 하나. 동률은 슬롯 번호가 낮은 쪽. 없으면 null.
        private static ICombatant LowestHp(IReadOnlyList<ICombatant> units)
            => units.Where(u => u.IsAlive)
                    .OrderBy(u => u.CurrentHp)
                    .ThenBy(u => u.SlotIndex)
                    .FirstOrDefault();

        // 생존 유닛 중 슬롯 번호가 가장 낮은 하나. SingleEnemy 폴백 기본값. 없으면 null.
        private static ICombatant FirstAliveBySlot(IReadOnlyList<ICombatant> units)
            => units.Where(u => u.IsAlive)
                    .OrderBy(u => u.SlotIndex)
                    .FirstOrDefault();

        private static IReadOnlyList<ICombatant> SingleOrEmpty(ICombatant unit)
            => unit == null ? Empty : new[] { unit };
    }
}
