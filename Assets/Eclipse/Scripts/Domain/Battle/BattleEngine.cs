using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 전투 한 판의 진행을 담당한다. 스케줄러가 뽑은 행동자마다 스킬 하나를 실행하고,
    /// 매 턴 승패를 판정한다. 행동 상한은 무한 루프를 막는 종료 안전장치다.
    /// </summary>
    public class BattleEngine
    {
        private readonly List<Combatant> _allies;
        private readonly List<Combatant> _enemies;
        private readonly ITurnScheduler _scheduler;
        private readonly SkillExecutor _executor;
        private readonly IActionProvider _allyProvider;
        private readonly IActionProvider _enemyProvider;
        private readonly int _actionCap;

        private int _actionCount;

        /// <param name="actionCap">전장 누적 행동 상한. 넘으면 패배 처리한다.</param>
        public BattleEngine(
            List<Combatant> allies, List<Combatant> enemies,
            ITurnScheduler scheduler, SkillExecutor executor,
            IActionProvider allyProvider, IActionProvider enemyProvider, int actionCap)
        {
            _allies = allies;
            _enemies = enemies;
            _scheduler = scheduler;
            _executor = executor;
            _allyProvider = allyProvider;
            _enemyProvider = enemyProvider;
            _actionCap = actionCap;
        }

        /// <summary> 지금까지 실행된 누적 행동 수. </summary>
        public int ActionCount => _actionCount;

        /// <summary> 직전 턴에 벌어진 일(행동자·스킬·대상). 연출이 시전/피격 이펙트에 쓴다. 아직 한 턴도 안 지났으면 None. </summary>
        public TurnResult LastTurn { get; private set; } = TurnResult.None;

        /// <summary>
        /// 다음 행동자 한 명의 턴을 처리하고 처리 후의 전투 상태를 반환한다.
        /// 행동자 스킬 쿨 감소·스킬 실행·게이지 정산·행동 카운터 증가가 이 안에서 일어난다.
        /// </summary>
        /// <param name="ct">행동 결정 대기 취소 토큰. 수동 입력을 기다리는 중 전투를 끊을 때 쓴다.</param>
        public async UniTask<BattleOutcome> AdvanceTurnAsync(CancellationToken ct)
        {
            // null은 생존 유닛이 없는 퇴화 상태이므로 현재 생존 상황으로 판정한다.
            var actor = _scheduler.GetNextActor();
            if (actor == null) return Evaluate();

            // 턴 시작 정산. 행동자는 항상 Combatant다(스케줄러는 읽기용 ICombatant만 반환).
            ((Combatant)actor).OnTurnStart();

            // 이번 턴에 실제로 쓴 스킬·영향 대상. 스킬을 안 쓰면 null/빈 목록으로 남는다.
            SkillSO usedSkill = null;
            IReadOnlyList<ICombatant> targets = Array.Empty<ICombatant>();

            // 도트로 쓰러졌으면 행동하지 않고 턴만 정산한다.
            if (actor.IsAlive)
            {
                // 수동 프로바이더는 여기서 플레이어 입력이 올 때까지 대기한다(오토·적은 즉시 완료).
                var action = await ProviderFor(actor).ChooseActionAsync(actor, AlliesOf(actor), EnemiesOf(actor), ct);
                if (action.Skill != null)
                {
                    action.Skill.TryUse();
                    usedSkill = action.Skill.Skill;
                    targets = _executor.ApplySkill(actor, action.Skill, action.Target, AlliesOf(actor), EnemiesOf(actor));
                }
            }

            LastTurn = new TurnResult(actor, usedSkill, targets);

            _scheduler.OnActionResolved(actor);
            _actionCount++;

            return Evaluate();
        }

        /// <summary>
        /// 전투가 끝날 때까지 턴을 반복 처리한다. 오토·헤드리스 진행용이며, UI가 매 턴 상태를
        /// 갱신해야 하는 경우엔 대신 AdvanceTurnAsync를 턴 단위로 호출한다.
        /// </summary>
        public async UniTask<BattleOutcome> RunAsync(CancellationToken ct)
        {
            var outcome = BattleOutcome.Ongoing;
            while (outcome == BattleOutcome.Ongoing)
                outcome = await AdvanceTurnAsync(ct);
            return outcome;
        }

        // 행동자의 편에 따라 행동 결정 주체를 고른다.
        private IActionProvider ProviderFor(ICombatant actor)
            => actor.Team == Team.Ally ? _allyProvider : _enemyProvider;

        private BattleOutcome Evaluate()
        {
            if (_enemies.All(u => !u.IsAlive)) return BattleOutcome.Victory;
            if (_allies.All(u => !u.IsAlive)) return BattleOutcome.Defeat;
            if (_actionCount >= _actionCap) return BattleOutcome.Defeat; // 행동 상한 초과 = 미클리어
            return BattleOutcome.Ongoing;
        }

        // 행동자 관점의 아군/적 목록. IReadOnlyList 공변성으로 Combatant 목록을 그대로 넘긴다.
        private IReadOnlyList<ICombatant> AlliesOf(ICombatant actor)
            => actor.Team == Team.Ally ? (IReadOnlyList<ICombatant>)_allies : _enemies;

        private IReadOnlyList<ICombatant> EnemiesOf(ICombatant actor)
            => actor.Team == Team.Ally ? (IReadOnlyList<ICombatant>)_enemies : _allies;
    }
}
