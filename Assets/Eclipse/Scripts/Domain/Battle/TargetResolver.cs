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
                    return SingleOrEmpty(LowestHp(TauntFiltered(enemies))); //도발 체크 후 없으면 타겟으로 설정
                case TargetSelector.HighestAtkEnemy:
                    return SingleOrEmpty(HighestAtk(TauntFiltered(enemies))); //도발 체크 후 없으면 타겟으로 설정
                default:
                    return Empty;
            }
        }

        /// <summary>
        /// 수동 지정 대상을 반영해 대상 목록을 반환한다. 지정 대상은 단일-적 selector에만,
        /// 그리고 <see cref="ValidManualTargets"/>에 든 대상일 때만 적용된다 — 도발 중인 적이 있으면
        /// 도발자 중에서만 고를 수 있다(도발이 대상 범위를 좁히되, 그 안에서는 지정을 존중한다).
        /// 그 밖의 경우는 selector 기본 규칙으로 폴백한다: 지정 없음/지정 대상 사망/적 목록 밖/
        /// 도발 중 비도발자 지정/광역·아군 selector.
        /// </summary>
        /// <param name="chosenTarget">플레이어가 찍은 대상. null이면 selector가 대상을 정한다.</param>
        public IReadOnlyList<ICombatant> Resolve(
            TargetSelector selector, ICombatant actor,
            IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies,
            ICombatant chosenTarget)
        {
            if (chosenTarget != null
                && IsSingleEnemySelector(selector)
                && chosenTarget.IsAlive
                && enemies.Contains(chosenTarget)
                && (chosenTarget.IsTaunting || !AnyTaunting(enemies)))
            {
                return new[] { chosenTarget };
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
        public IReadOnlyList<ICombatant> ValidManualTargets(IReadOnlyList<ICombatant> enemies)
        {
            var alive = enemies.Where(u => u.IsAlive).ToList();
            return TauntFiltered(alive);
        }

        /// <summary> 지정 대상 오버라이드가 적용되는 selector인지(단일-적 규칙만 해당). </summary>
        /// <param name="selector">스킬 효과의 대상 선택 규칙.</param>
        /// <returns>단일-적 selector면 true. 광역·아군·자기면 false(지정이 무시된다).</returns>
        public static bool IsSingleEnemySelector(TargetSelector selector)
            => selector == TargetSelector.LowestHpEnemy || selector == TargetSelector.HighestAtkEnemy;

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
