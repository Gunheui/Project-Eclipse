using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 아군 행동 결정 주체. AutoMode면 내장 규칙(오토)에 위임해 즉시 결정하고,
    /// 아니면 플레이어가 스킬·대상을 고를 때까지 대기했다가 그 입력으로 결정한다.
    /// 전투 진행 경로는 하나뿐이며(BattleEngine), 오토↔수동은 이 한 구현체 안에서 전환된다.
    /// </summary>
    public sealed class ManualActionProvider : IActionProvider
    {
        private readonly IActionProvider _auto;
        private UniTaskCompletionSource<BattleAction> _pending;

        // 수동 대기 중 Auto가 켜질 때 오토 규칙에 위임하려면 필요한 컨텍스트. 대기 진입 시 보관한다.
        private IReadOnlyList<ICombatant> _pendingAllies;
        private IReadOnlyList<ICombatant> _pendingEnemies;
        private CancellationToken _pendingCt;
        private bool _autoMode;

        /// <param name="autoFallback">AutoMode일 때 결정을 위임할 규칙 기반 프로바이더.</param>
        public ManualActionProvider(IActionProvider autoFallback)
        {
            _auto = autoFallback;
        }

        /// <summary>
        /// 오토 전투 여부. true면 ChooseActionAsync가 규칙에 위임해 즉시 완료된다.
        /// 대기 중인 수동 턴이 있을 때 켜지면 그 턴도 즉시 오토 결정으로 해제한다.
        /// </summary>
        public bool AutoMode
        {
            get => _autoMode;
            set
            {
                _autoMode = value;
                if (value) ResolvePendingWithAuto();
            }
        }

        /// <summary> 지금 플레이어 입력을 기다리는 행동자. 대기 중이 아니면 null. </summary>
        public ICombatant PendingActor { get; private set; }

        /// <summary>
        /// 수동 입력 대기가 시작될 때(행동자의 턴이 열려 Submit을 기다리기 직전) 그 행동자로 발화한다.
        /// ViewModel이 구독해 "누구 차례인지"를 표시하고 해당 유닛의 스킬 버튼을 연다.
        /// </summary>
        public event Action<ICombatant> InputRequested;

        /// <summary>
        /// 이번 턴 행동을 결정한다. AutoMode면 규칙에 위임(즉시 완료). 수동이면 PendingActor를 세우고
        /// Submit이 호출될 때까지 완료되지 않는 태스크를 돌려준다(엔진이 이 태스크를 await하며 멈춘다).
        /// </summary>
        /// <param name="actor">행동할 유닛.</param>
        /// <param name="allies">행동자 편의 유닛 목록.</param>
        /// <param name="enemies">상대 편의 유닛 목록.</param>
        /// <param name="ct">대기 취소 토큰. 취소되면 대기가 끊기고 태스크가 취소로 완료된다.</param>
        /// <returns>플레이어(또는 규칙)가 고른 스킬·대상을 담은 행동.</returns>
        public async UniTask<BattleAction> ChooseActionAsync(
            ICombatant actor,
            IReadOnlyList<ICombatant> allies,
            IReadOnlyList<ICombatant> enemies,
            CancellationToken ct)
        {
            if (AutoMode) return await _auto.ChooseActionAsync(actor, allies, enemies, ct);

            PendingActor = actor;
            _pendingAllies = allies;
            _pendingEnemies = enemies;
            _pendingCt = ct;
            _pending = new UniTaskCompletionSource<BattleAction>();

            // 취소되면(씬 이탈 등) 대기를 끊는다. using이라 메서드가 끝날 때 예약이 자동 해지된다.
            using var reg = ct.Register(() => _pending?.TrySetCanceled(ct));
            InputRequested?.Invoke(actor); // 현재 인풋 중이다 라고 이벤트 실행
            try
            {
                return await _pending.Task; // Submit(또는 오토 위임)이 결과를 넣을 때까지 여기서 멈춘다.
            }
            finally
            {
                PendingActor = null;
                _pending = null;
            }
        }

        /// <summary>
        /// 플레이어가 스킬·대상을 골랐을 때 호출한다. 대기 중이던 ChooseActionAsync를 그 행동으로 완료시킨다.
        /// 대기 중이 아니면(오토이거나 행동자 턴이 아니면) 아무 일도 하지 않는다.
        /// </summary>
        /// <param name="skill">사용할 스킬.</param>
        /// <param name="target">지정 대상. null이면 각 효과의 TargetSelector가 대상을 정한다.</param>
        public void Submit(SkillRuntime skill, ICombatant target = null)
        {
            _pending?.TrySetResult(new BattleAction(skill, target));
        }

        // Auto가 켜지는 순간 대기 중인 수동 턴을 오토 규칙 결정으로 즉시 완료시킨다. 대기가 없으면 무시한다.
        private void ResolvePendingWithAuto()
        {
            if (_pending == null) return;
            ForwardAutoDecision(_pending, PendingActor, _pendingAllies, _pendingEnemies, _pendingCt).Forget();
        }

        // 오토 규칙에 이번 턴 결정을 위임하고 그 결과로 대기를 완료한다(_auto는 즉시 완료됨).
        private async UniTaskVoid ForwardAutoDecision(
            UniTaskCompletionSource<BattleAction> pending,
            ICombatant actor,
            IReadOnlyList<ICombatant> allies,
            IReadOnlyList<ICombatant> enemies,
            CancellationToken ct)
        {
            var action = await _auto.ChooseActionAsync(actor, allies, enemies, ct);
            pending.TrySetResult(action);
        }
    }
}
