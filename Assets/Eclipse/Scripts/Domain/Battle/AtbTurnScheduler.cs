using System;
using System.Collections.Generic;
using System.Linq;

namespace Eclipse.Domain
{
    /// <summary>
    /// SPD 기반 ATB 스케줄러. 각 유닛의 행동 게이지를 SPD로 누적해, 임계값에 먼저 도달한 유닛을
    /// 다음 행동자로 결정한다. 게이지 상태는 이 스케줄러가 보유한다(ICombatant는 게이지를 모른다).
    /// 순서 판정은 도달 시각(잔여거리/SPD)을 정수 교차곱으로 비교하고, 게이지는 고정소수점 정수(long)로
    /// 누적한다 — 부동소수를 쓰지 않으므로 같은 편성은 어떤 플랫폼에서도 항상 같은 순서를 낸다(결정적).
    /// </summary>
    public class AtbTurnScheduler : ITurnScheduler
    {
        /// <summary>
        /// 게이지가 이 값에 도달하면 행동한다. 개념상 임계값 10000을 ×1000 고정소수점으로 확대한 값 —
        /// 도달 시점까지 전진할 때 나눗셈 반올림이 남는 비행동 유닛의 이월 오차를 무시 가능 수준으로 억제한다.
        /// </summary>
        public const long Threshold = 10_000_000;

        private readonly List<ICombatant> _units;
        private readonly Dictionary<ICombatant, long> _gauge;

        /// <summary>
        /// 참가 유닛으로 스케줄러를 만든다. 모든 유닛의 게이지는 0에서 시작한다.
        /// </summary>
        /// <param name="units">이 전투에 참가하는 전체 유닛(아군+적).</param>
        public AtbTurnScheduler(IEnumerable<ICombatant> units)
        {
            _units = units.ToList();
            _gauge = _units.ToDictionary(u => u, _ => 0L);
        }

        // 테스트 전용: 특정 게이지 상태(고정소수점)에서 시작하는 스케줄러. 지정되지 않은 유닛은 0에서 시작한다.
        // 특정 도달 순서를 재현·검증하기 위한 시밍이며, 프로덕션 진입점은 위 공개 생성자뿐이다.
        internal AtbTurnScheduler(IEnumerable<ICombatant> units, IReadOnlyDictionary<ICombatant, long> initialGauges)
        {
            _units = units.ToList();
            _gauge = _units.ToDictionary(u => u, u => initialGauges.TryGetValue(u, out var g) ? g : 0L);
        }

        /// <summary>
        /// 다음 행동자를 즉시 계산해 반환한다. 임계값에 가장 먼저 도달하는 유닛을 골라, 전원 게이지를
        /// 그 도달 시점까지 한 번에 전진시킨 뒤 그 유닛을 돌려준다. 생존 유닛이 없으면 null.
        /// </summary>
        public ICombatant GetNextActor()
        {
            var alive = _units.Where(u => u.IsAlive).ToList();
            if (alive.Count == 0) return null;

            // 다음 행동자 = 임계값에 먼저 도달하는(잔여거리/SPD가 최소인) 유닛.
            // 동시 도달·동률은 ArrivesBefore가 SPD → 아군 우선 → 슬롯 순으로 완전 결정적으로 가른다.
            var actor = alive.Aggregate((best, u) => ArrivesBefore(u, best) ? u : best);

            // 전원 게이지를 행동자의 도달 시점까지 한 번에 전진시킨다. 행동자는 임계값에 정확히 안착하고
            // (spd*rem/spd = rem), 다른 유닛은 그 시점의 진척만큼만 오른다(1 미만 floor 오차는 스케일로 흡수).
            long remActor = Remaining(actor);
            if (remActor > 0)
                foreach (var u in alive)
                    _gauge[u] += u.EffectiveStats.spd * remActor / actor.EffectiveStats.spd;

            return actor;
        }

        /// <summary>
        /// 행동을 마친 유닛의 게이지를 임계값만큼 차감한다. 0으로 리셋하지 않고 초과분을 이월(carry-over)해,
        /// 장기적으로 행동 빈도가 SPD에 정확히 비례하게 한다.
        /// </summary>
        /// <param name="actor">방금 행동을 마친 유닛.</param>
        public void OnActionResolved(ICombatant actor)
        {
            _gauge[actor] -= Threshold;
        }

        // 임계값까지 남은 게이지 거리. 이미 도달·초과했으면 0(도달 시각 0 → 즉시 행동).
        private long Remaining(ICombatant u) => Math.Max(0, Threshold - _gauge[u]);

        // x가 y보다 먼저 도달하는가. 도달 시각 rem/spd 비교를 나눗셈 없이 교차곱(rem_x*spd_y < rem_y*spd_x)으로
        // 정확히 판정하고, 동시 도달이면 SPD 내림차순 → 아군 우선(Team) → 슬롯 오름차순으로 가른다(랜덤 없음).
        private bool ArrivesBefore(ICombatant x, ICombatant y)
        {
            long lhs = Remaining(x) * y.EffectiveStats.spd;
            long rhs = Remaining(y) * x.EffectiveStats.spd;
            if (lhs != rhs) return lhs < rhs;
            if (x.EffectiveStats.spd != y.EffectiveStats.spd) return x.EffectiveStats.spd > y.EffectiveStats.spd;
            if (x.Team != y.Team) return x.Team < y.Team;
            return x.SlotIndex < y.SlotIndex;
        }
    }
}
