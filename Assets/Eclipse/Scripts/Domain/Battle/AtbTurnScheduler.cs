using System;
using System.Collections.Generic;
using System.Linq;

namespace Eclipse.Domain
{
    /// <summary>
    /// SPD 기반 ATB 스케줄러. 행동 게이지를 SPD로 누적해 임계값에 먼저 도달한 유닛이 다음 행동자다.
    /// 게이지 상태는 이 스케줄러가 보유한다(ICombatant는 게이지를 모른다).
    /// 판정은 정수 교차곱, 누적은 고정소수점 정수(long)라 같은 편성은 어떤 플랫폼에서도 같은 순서를 낸다(결정적).
    /// </summary>
    public class AtbTurnScheduler : ITurnScheduler
    {
        /// <summary>
        /// 게이지가 이 값에 도달하면 행동한다. 임계값 10000을 ×1000 고정소수점으로 확대한 값으로,
        /// 전진 시 나눗셈 반올림이 남기는 이월 오차를 무시 가능 수준으로 억제한다.
        /// </summary>
        public const long Threshold = 10_000_000;

        private readonly List<ICombatant> _units;
        private readonly Dictionary<ICombatant, long> _gauge;

        /// <summary> 참가 유닛(아군+적) 전체로 스케줄러를 만든다. 게이지는 모두 0에서 시작한다. </summary>
        public AtbTurnScheduler(IEnumerable<ICombatant> units)
        {
            _units = units.ToList();
            _gauge = _units.ToDictionary(u => u, _ => 0L);
        }

        // 테스트 전용: 특정 게이지 상태(고정소수점)에서 시작한다. 미지정 유닛은 0. 프로덕션 진입점은 위 공개 생성자뿐이다.
        internal AtbTurnScheduler(IEnumerable<ICombatant> units, IReadOnlyDictionary<ICombatant, long> initialGauges)
        {
            _units = units.ToList();
            _gauge = _units.ToDictionary(u => u, u => initialGauges.TryGetValue(u, out var g) ? g : 0L);
        }

        /// <summary>
        /// 다음 행동자를 계산해 반환한다. 전원 게이지를 그 도달 시점까지 전진시킨다(상태 변경).
        /// 생존 유닛이 없으면 null.
        /// </summary>
        public ICombatant GetNextActor()
        {
            var alive = _units.Where(u => u.IsAlive).ToList();
            if (alive.Count == 0) return null;
            return Advance(_gauge, alive);
        }

        /// <summary>
        /// 행동을 마친 유닛의 게이지를 임계값만큼 차감한다. 0 리셋이 아니라 초과분 이월이라,
        /// 장기적으로 행동 빈도가 SPD에 정확히 비례한다.
        /// </summary>
        public void OnActionResolved(ICombatant actor)
        {
            _gauge[actor] -= Threshold;
        }

        /// <summary>
        /// 상태를 바꾸지 않고 다음 count명의 행동 순서를 예보한다. 게이지 사본 위에서 진행 로직을 그대로
        /// 재생하므로 실제 진행과 정확히 일치한다. 같은 유닛이 여러 번 등장할 수 있고, count≤0·전멸이면 빈 목록.
        /// </summary>
        public IReadOnlyList<ICombatant> PreviewOrder(int count)
        {
            var order = new List<ICombatant>(Math.Max(0, count));
            var alive = _units.Where(u => u.IsAlive).ToList();
            if (count <= 0 || alive.Count == 0) return order;

            var gauge = new Dictionary<ICombatant, long>(_gauge); // 사본 — 실제 _gauge는 불변
            for (int i = 0; i < count; i++)
            {
                var actor = Advance(gauge, alive);
                order.Add(actor);
                gauge[actor] -= Threshold; // OnActionResolved와 동일한 이월
            }
            return order;
        }

        // 다음 행동자 = 임계값에 먼저 도달하는(잔여거리/SPD 최소) 유닛. 전원 게이지를 그 도달 시점까지
        // 전진시키고 행동자를 반환한다. 동시 도달·동률은 ArrivesBefore가 결정적으로 가른다.
        //
        // ※ 인자로 받은 gauge를 직접 변경한다. 실제 진행은 _gauge를, 예보는 사본을 넘긴다 —
        //   조회 경로에서 _gauge를 넘기면 전투 순서가 조용히 망가진다.
        //   static이라 이 함수는 _gauge에 접근할 수 없다(실수 여지를 인자 선택 한 곳으로 좁힌다).
        private static ICombatant Advance(IDictionary<ICombatant, long> gauge, IReadOnlyList<ICombatant> alive)
        {
            var actor = alive.Aggregate((best, u) => ArrivesBefore(gauge, u, best) ? u : best);

            long remActor = Remaining(gauge, actor);
            if (remActor > 0)
            {
                // EffectiveStats는 접근마다 버프·디버프를 다시 계산하므로 행동자 것을 미리 읽는다.
                int actorSpd = actor.EffectiveStats.spd;
                foreach (var u in alive)
                    gauge[u] += u.EffectiveStats.spd * remActor / actorSpd;
            }

            return actor;
        }

        // 임계값까지 남은 게이지 거리. 이미 도달·초과했으면 0(도달 시각 0 → 즉시 행동).
        private static long Remaining(IDictionary<ICombatant, long> gauge, ICombatant u) => Math.Max(0, Threshold - gauge[u]);

        // x가 y보다 먼저 도달하는가. 도달 시각 rem/spd 비교를 나눗셈 없이 교차곱(rem_x*spd_y < rem_y*spd_x)으로
        // 정확히 판정하고, 동시 도달이면 SPD 내림차순 → 아군 우선(Team) → 슬롯 오름차순으로 가른다(랜덤 없음).
        private static bool ArrivesBefore(IDictionary<ICombatant, long> gauge, ICombatant x, ICombatant y)
        {
            int sx = x.EffectiveStats.spd, sy = y.EffectiveStats.spd; // 접근마다 재계산되므로 한 번만 읽는다
            long lhs = Remaining(gauge, x) * sy;
            long rhs = Remaining(gauge, y) * sx;
            if (lhs != rhs) return lhs < rhs;
            if (sx != sy) return sx > sy;
            if (x.Team != y.Team) return x.Team < y.Team;
            return x.SlotIndex < y.SlotIndex;
        }
    }
}
