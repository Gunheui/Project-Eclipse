using System.Collections.Generic;
using System.Linq;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 스킬 효과의 TargetSelector(스코프)를 실제 대상 유닛 목록으로 바꾼다.
    /// 생존한 유닛만 고르고, 단일 대상의 동률은 슬롯 번호가 낮은 쪽을 택해 항상 같은 결과를 낸다.
    /// 범위 해석·도발 필터·수동 후보 산출만 담당한다 — 단일-적의 "누구를 고르나"는
    /// TargetPriorityPolicy가 정해 BattleAction.Target으로 넘긴다(여기 기본값은 방어적 폴백).
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
                case TargetSelector.SingleAlly:
                    // 아군 단일 = 최저 HP 아군(힐은 전술 선택이 아니라 기본값을 리졸버가 소유).
                    return SingleOrEmpty(LowestHp(allies));
                case TargetSelector.SingleEnemy:
                    // 정책이 Target을 안 준 경우의 방어적 폴백 = 도발 필터 후 슬롯 낮은 순(dumb).
                    // 실제 전투 경로는 항상 정책이 고른 Target을 chosenTarget으로 넘겨 이 기본값을 덮는다.
                    return SingleOrEmpty(FirstAliveBySlot(TauntFiltered(enemies)));
                default:
                    return Empty;
            }
        }

        /// <summary>
        /// 수동 지정 대상을 반영해 대상 목록을 반환한다. 지정은 단일-적·단일-아군 selector에만,
        /// 그리고 각 <see cref="ValidEnemyTargets"/>/<see cref="ValidAllyTargets"/>에 든 대상일 때만 적용된다.
        /// 단일-적은 도발 중인 적이 있으면 도발자 중에서만(도발이 범위를 좁히되 그 안에서는 지정 존중),
        /// 단일-아군은 살아있는 아군이면 그대로 존중한다(힐/버프엔 도발 상당 규칙 없음).
        /// 그 밖의 경우는 selector 기본 규칙으로 폴백한다: 지정 없음/지정 대상 사망/목록 밖/
        /// 도발 중 비도발자 지정/광역·자기 selector.
        /// </summary>
        /// <param name="chosenTarget">플레이어가 찍은 대상. null이면 selector가 대상을 정한다.</param>
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
        /// 플레이어가 단일-적 스킬로 직접 지정할 수 있는 후보. 생존한 적만 두되, 도발 중인 적이 있으면
        /// 그들로 좁힌다. <see cref="Resolve"/>의 지정 존중 조건과 같은 규칙이라 여기 든 대상을 찍으면
        /// 반드시 그 대상이 맞는다 — 조준 UI가 이 목록만 선택 가능으로 칠하면 화면과 판정이 일치한다.
        /// </summary>
        /// <param name="enemies">행동자 기준 상대 편의 유닛 목록.</param>
        /// <returns>지정 가능한 적 목록. 생존한 적이 없으면 빈 목록.</returns>
        public IReadOnlyList<ICombatant> ValidEnemyTargets(IReadOnlyList<ICombatant> enemies)
        {
            var alive = enemies.Where(u => u.IsAlive).ToList();
            return TauntFiltered(alive);
        }

        /// <summary>
        /// 플레이어가 단일-아군 스킬(힐/버프)로 직접 지정할 수 있는 후보 = 생존한 아군. 도발 상당 규칙이 없어
        /// 단순 생존 필터다. <see cref="Resolve"/>의 아군 지정 존중 조건과 같은 규칙이라 여기 든 대상을 찍으면
        /// 반드시 그 대상이 맞는다(조준 UI가 이 목록만 선택 가능으로 칠하면 화면과 판정이 일치).
        /// </summary>
        /// <param name="allies">행동자 편의 유닛 목록(행동자 자신 포함).</param>
        /// <returns>지정 가능한 아군 목록. 생존한 아군이 없으면 빈 목록.</returns>
        public IReadOnlyList<ICombatant> ValidAllyTargets(IReadOnlyList<ICombatant> allies)
            => allies.Where(u => u.IsAlive).ToList();

        /// <summary> 지정 대상 오버라이드가 적용되는 스코프인지(단일-적만 해당). </summary>
        /// <param name="selector">스킬 효과의 대상 스코프.</param>
        /// <returns>단일-적 스코프면 true. 광역·아군·자기면 false(지정이 무시된다).</returns>
        public static bool IsSingleEnemy(TargetSelector selector)
            => selector == TargetSelector.SingleEnemy;

        /// <summary> 지정 대상 오버라이드가 적용되는 아군 스코프인지(단일-아군만 해당). </summary>
        /// <param name="selector">스킬 효과의 대상 스코프.</param>
        /// <returns>단일-아군 스코프면 true. 광역·적·자기면 false.</returns>
        public static bool IsSingleAlly(TargetSelector selector)
            => selector == TargetSelector.SingleAlly;

        private static bool AnyTaunting(IReadOnlyList<ICombatant> enemies)
            => enemies.Any(u => u.IsAlive && u.IsTaunting);

        // 도발 중인 생존 적이 하나라도 있으면 그들만 후보로 좁힌다(단일-적 공격이 도발자에게 끌린다).
        // 없으면 원래 후보를 그대로 돌려준다.
        private static IReadOnlyList<ICombatant> TauntFiltered(IReadOnlyList<ICombatant> enemies)
        {
            var taunters = enemies.Where(u => u.IsAlive && u.IsTaunting).ToList();
            return taunters.Count > 0 ? taunters : enemies;
        }

        // 생존 유닛 중 현재 HP가 가장 낮은 하나. 동률은 슬롯 번호가 낮은 쪽. 없으면 null.
        private static ICombatant LowestHp(IReadOnlyList<ICombatant> units)
            => units.Where(u => u.IsAlive)
                    .OrderBy(u => u.CurrentHp) //HP 오름차순
                    .ThenBy(u => u.SlotIndex) //경합일때 슬롯번호 낮은 쪽
                    .FirstOrDefault();

        // 생존 유닛 중 슬롯 번호가 가장 낮은 하나. SingleEnemy 폴백 기본값(dumb). 없으면 null.
        private static ICombatant FirstAliveBySlot(IReadOnlyList<ICombatant> units)
            => units.Where(u => u.IsAlive)
                    .OrderBy(u => u.SlotIndex)
                    .FirstOrDefault();

        private static IReadOnlyList<ICombatant> SingleOrEmpty(ICombatant unit)
            => unit == null ? Empty : new[] { unit };
    }
}
