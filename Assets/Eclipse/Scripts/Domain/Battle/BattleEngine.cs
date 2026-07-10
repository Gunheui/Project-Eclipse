using System.Collections.Generic;
using System.Linq;

namespace Eclipse.Domain
{
    /// <summary>
    /// 전투 한 판의 진행을 담당한다. 스케줄러가 뽑은 행동자마다 스킬 하나를 실행하고,
    /// 매 턴 승패를 판정한다. 행동 상한은 무한 루프를 막는 종료 안전장치다.
    /// 행동 결정은 지금 플레이스홀더이며, 이후 범위에서 IActionProvider로 교체된다.
    /// </summary>
    public class BattleEngine
    {
        private readonly List<BattleUnit> _allies;
        private readonly List<BattleUnit> _enemies;
        private readonly ITurnScheduler _scheduler;
        private readonly SkillExecutor _executor;
        private readonly int _actionCap;

        private int _actionCount;

        /// <param name="allies">아군 파티 유닛.</param>
        /// <param name="enemies">적 유닛.</param>
        /// <param name="scheduler">양편 유닛으로 구성된 턴 스케줄러.</param>
        /// <param name="executor">스킬 효과 적용기.</param>
        /// <param name="actionCap">전장 누적 행동 상한. 넘으면 미클리어(패배) 처리한다.</param>
        public BattleEngine(
            List<BattleUnit> allies, List<BattleUnit> enemies,
            ITurnScheduler scheduler, SkillExecutor executor, int actionCap)
        {
            _allies = allies;
            _enemies = enemies;
            _scheduler = scheduler;
            _executor = executor;
            _actionCap = actionCap;
        }

        /// <summary> 지금까지 실행된 누적 행동 수. </summary>
        public int ActionCount => _actionCount;

        /// <summary>
        /// 다음 행동자 한 명의 턴을 처리하고 처리 후의 전투 상태를 반환한다.
        /// 행동자 스킬 쿨 감소·스킬 실행·게이지 정산·행동 카운터 증가가 이 안에서 일어난다.
        /// </summary>
        /// <returns>이 턴 처리 후의 전투 상태(진행/승리/패배).</returns>
        public BattleOutcome AdvanceTurn()
        {
            // 정상 진행 중이라면 스케줄러는 항상 생존 유닛을 준다.
            // null은 살아있는 유닛이 없는 퇴화 상태이므로 현재 생존 상황으로 판정한다.
            var actor = _scheduler.GetNextActor();
            if (actor == null) return Evaluate();

            // 턴 시작: 행동자 자기 스킬의 남은 쿨을 1 줄인다.
            foreach (var skill in actor.Skills)
                skill.Tick();

            var chosen = SelectSkill(actor);
            if (chosen != null)
            {
                chosen.TryUse();
                _executor.Execute(actor, chosen, AlliesOf(actor), EnemiesOf(actor));
            }

            _scheduler.OnActionResolved(actor);
            _actionCount++;

            return Evaluate();
        }

        /// <summary>
        /// 전투가 끝날 때까지 턴을 반복 처리한다.
        /// </summary>
        /// <returns>최종 전투 상태(승리 또는 패배).</returns>
        public BattleOutcome Run()
        {
            var outcome = BattleOutcome.Ongoing;
            while (outcome == BattleOutcome.Ongoing)
                outcome = AdvanceTurn();
            return outcome;
        }

        // 준비된 스킬 중 가장 상위 슬롯(궁 → 일반 → 기본)을 고른다.
        // 기본공격은 쿨 0이라 항상 준비돼 있어 "빈 턴 없음"을 보장한다.
        private static SkillRuntime SelectSkill(ICombatant actor)
        {
            for (int i = actor.Skills.Count - 1; i >= 0; i--)
                if (actor.Skills[i].IsReady)
                    return actor.Skills[i];
            return null;
        }

        private BattleOutcome Evaluate()
        {
            if (_enemies.All(u => !u.IsAlive)) return BattleOutcome.Victory; // 적이 모두 죽은경우
            if (_allies.All(u => !u.IsAlive)) return BattleOutcome.Defeat; // 우리팀이 모두 죽은경우
            if (_actionCount >= _actionCap) return BattleOutcome.Defeat; // 최대 액션 수를 모두 사용한 경우
            return BattleOutcome.Ongoing;
        }

        // 행동자 관점의 아군/적 목록. IReadOnlyList 공변성으로 BattleUnit 목록을 그대로 넘긴다.
        private IReadOnlyList<ICombatant> AlliesOf(ICombatant actor)
            => actor.Team == Team.Ally ? (IReadOnlyList<ICombatant>)_allies : _enemies;

        private IReadOnlyList<ICombatant> EnemiesOf(ICombatant actor)
            => actor.Team == Team.Ally ? (IReadOnlyList<ICombatant>)_enemies : _allies;
    }
}
