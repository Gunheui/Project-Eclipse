using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary> 전투 결과. Domain의 BattleOutcome을 View가 볼 수 있는 프레젠테이션 표현으로 옮긴 것. </summary>
    public enum BattleResult { InProgress, Victory, Defeat }

    /// <summary>
    /// 전투 화면의 ViewModel. 엔진을 턴 단위로 구동하고 유닛·스킬·행동 수·결과를 리액티브로 노출한다.
    /// 뷰 상태는 전부 턴 신호(_stateChanged)에서 파생된다(폴링 없음).
    /// </summary>
    public sealed class BattleViewModel : ViewModelBase
    {
        // 다가올 행동 순서 예보에 쓴다. 엔진과 같은 인스턴스를 공유해 실제 진행 상태를 조회한다.
        private readonly ITurnScheduler _scheduler;

        /// <summary> 타임라인에 미리 보여줄 다가올 행동 수. 표시줄의 칸 수와 맞춰야 한다. </summary>
        public const int TimelineSlots = 5;

        private readonly BattleEngine _engine;
        private readonly ManualActionProvider _manualProvider;
        private readonly ISceneFlow _sceneFlow;

        // 승리 시 클리어를 기록할 대상. 장·스테이지·인덱스(0-기반)는 조립 시점(BattleFactory)에
        // 검증된 불변 값이라, 승리 순간의 외부 상태를 다시 읽지 않는다.
        private readonly StageProgress _progress;
        private readonly ChapterSO _chapter;
        private readonly StageSO _stage;
        private readonly int _stageIndex;

        // 승리 보상 지급 창구. 초회 여부는 진행도 기록의 반환값이 정한다.
        private readonly IRewardService _rewards;

        // 승리 처리(클리어 기록·보상 지급) 완료 직후 스냅샷을 저장하는 창구. null이면 저장을 건너뛴다(테스트 조립).
        private readonly SaveService _saveService;

        // 조준 UI 후보 산출용(수동 후보). 오토 타겟 정책과 같은 리졸버 인스턴스를 공유한다.
        private readonly TargetResolver _targeting;

        // 뷰가 상태를 다시 읽어야 할 때 발화하는 신호(턴 종료 + 스킬 선택 시). HP·쿨·행동 수·결과가 전부 여기서 파생된다.
        private readonly Subject<Unit> _stateChanged = new();
        
        private readonly ReactiveProperty<CombatantViewModel> _actingCombatant = new(null);
        private readonly ReactiveProperty<bool> _autoMode;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly CancellationTokenSource _cts = new();

        private BattleOutcome _outcome = BattleOutcome.Ongoing;

        public BattleViewModel(
            IReadOnlyList<BattleUnitEntry> allies,
            IReadOnlyList<BattleUnitEntry> enemies,
            BattleEngine engine,
            ITurnScheduler scheduler,
            ManualActionProvider manualProvider,
            TargetResolver targeting,
            ISceneFlow sceneFlow,
            StageProgress progress,
            ChapterSO chapter,
            StageSO stage,
            int stageIndex,
            IRewardService rewards,
            SaveService saveService)
        {
            _rewards = rewards;
            _saveService = saveService;
            _engine = engine;
            _scheduler = scheduler;
            _manualProvider = manualProvider;
            _sceneFlow = sceneFlow;
            _targeting = targeting;
            _progress = progress;
            _chapter = chapter;
            _stage = stage;
            _stageIndex = stageIndex;

            // 순서는 아군 먼저, 그다음 적(스케줄러 입력 순과 동일).
            var all = allies.Concat(enemies).ToList();

            Combatants = all
                .Select(e => new CombatantViewModel(e.Unit, _stateChanged, e.Battler, e.TimelineIcon))
                .ToList();

            ActionCount = _stateChanged
                .Select(_ => _engine.ActionCount)
                .ToReadOnlyReactiveProperty(0);
            Result = _stateChanged
                .Select(_ => Map(_outcome))
                .ToReadOnlyReactiveProperty(BattleResult.InProgress);

            var openingOrder = MapOrder(_scheduler.PreviewOrder(TimelineSlots)); // 첫 턴 전 화면에 세울 시작 예보
            UpcomingTurns = _stateChanged
                .Select(_ => MapOrder(_scheduler.PreviewOrder(TimelineSlots)))
                .ToReadOnlyReactiveProperty(openingOrder);

            _autoMode = new ReactiveProperty<bool>(_manualProvider.AutoMode);
            _autoMode.Subscribe(on =>
            {
                _manualProvider.AutoMode = on;
                // 내 수동 턴 중 AUTO를 켜면 Submit을 거치지 않아 행동자가 남는다. 직접 비워 스킬바 잔상을 막는다.
                if (on) _actingCombatant.Value = null;
            }).AddTo(_subscriptions);

            // 수동 아군 턴이 열려 입력 대기에 들어가면 그 유닛을 ActingCombatant으로 세운다(스킬 버튼 활성화용).
            _manualProvider.InputRequested += OnInputRequested;
        }

        /// <summary> 전투 참가 유닛 VM 목록(아군+적). 순서는 스케줄러 입력 순. </summary>
        public IReadOnlyList<CombatantViewModel> Combatants { get; }

        /// <summary> 지금까지 실행된 누적 행동 수. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> ActionCount { get; }

        /// <summary> 전투 결과(진행/승리/패배). 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<BattleResult> Result { get; }

        /// <summary> 다가올 행동 순서(다음 N명, 0번=다음 차례). 타임라인 바인딩용. 턴마다 갱신. 같은 유닛이 반복될 수 있다. </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<CombatantViewModel>> UpcomingTurns { get; }

        /// <summary> 지금 입력을 기다리는 아군 유닛. 대기 중이 아니거나 오토면 null. </summary>
        public ReadOnlyReactiveProperty<CombatantViewModel> ActingCombatant => _actingCombatant;

        /// <summary> 오토 전투 토글. View가 값을 바꾸면 프로바이더에 반영된다. </summary>
        public ReactiveProperty<bool> AutoMode => _autoMode;

        /// <summary> 이번 승리로 실제 지급된 보상(재화별 1건). 결과 팝업 표시용. 승리 전·패배·지급 스킵이면 빈 목록. </summary>
        public IReadOnlyList<RewardEntry> GrantedRewards { get; private set; } = Array.Empty<RewardEntry>();

        /// <summary>
        /// 전투가 끝날 때까지 턴을 반복 구동한다. 아군 수동 턴에서는 엔진이 Submit을 기다리며 이 안에서 멈춘다.
        /// </summary>
        /// <param name="playTurnAnimation">이번 턴 배틀러 연출이 끝나면 완료되는 함수(View 제공). null이면 대기 없이 진행.</param>
        /// <param name="ct">외부 취소 토큰(예: 화면 파괴). VM 내부 취소와 묶여 함께 루프를 끊는다.</param>
        public async UniTask RunBattleAsync(Func<CancellationToken, UniTask> playTurnAnimation, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            _outcome = BattleOutcome.Ongoing;
            while (_outcome == BattleOutcome.Ongoing)
            {
                _outcome = await _engine.AdvanceTurnAsync(linked.Token);

                // 계산 적용 후(HP 반영) 신호를 흘리면 각 배틀러가 스스로 연출을 시작한다.
                NotifyActor();
                _stateChanged.OnNext(Unit.Default);

                // 연출이 끝날 때까지 다음 턴을 미룬다.
                if (playTurnAnimation != null)
                    await playTurnAnimation(linked.Token);
            }

            if (_outcome == BattleOutcome.Victory)
                MarkStageCleared();
        }

        /// <summary> 같은 스테이지로 전투 씬을 다시 로드한다. 스코프가 새로 서서 시드도 새로 뽑힌다. </summary>
        public UniTask RetryAsync() => _sceneFlow.ToBattleAsync();

        /// <summary> 플레이어가 고른 스킬(과 대상)로 대기 중이던 아군 턴을 재개한다. </summary>
        /// <param name="target">지정 대상. null이면 각 효과의 TargetSelector가 정한다.</param>
        public void Submit(SkillSlotViewModel skill, CombatantViewModel target = null)
        {
            _actingCombatant.Value = null;
            _manualProvider.Submit(skill.Runtime, target?.Model);
        }

        /// <summary>
        /// 행동자가 이 스킬로 직접 지정할 수 있는 대상 유닛. 도메인 규칙(적은 도발 반영)을 그대로 받아,
        /// 여기 든 대상을 <see cref="Submit"/>에 넘기면 반드시 적용된다 — 조준 UI는 이 목록만 선택 가능으로 칠한다.
        /// </summary>
        /// <returns>지정 가능한 대상 유닛 VM. 생존한 대상이 없으면 빈 목록.</returns>
        public IReadOnlyList<CombatantViewModel> ValidManualTargets(CombatantViewModel actor, SkillSlotViewModel skill)
        {
            bool toAllies = skill.ManualTargetsAllies;
            var pool = Combatants.Where(u => (u.IsAlly == actor.IsAlly) == toAllies).ToList();
            var models = pool.Select(u => (ICombatant)u.Model).ToList();
            var valid = toAllies ? _targeting.ValidAllyTargets(models) : _targeting.ValidEnemyTargets(models);
            return pool.Where(u => valid.Contains(u.Model)).ToList();
        }

        /// <summary> 전투 씬을 떠나 메인 씬으로 돌아간다. </summary>
        public UniTask ExitAsync() => _sceneFlow.ToMainAsync();

        // 스테이지 클리어를 기록하고 초회 여부에 따라 보상을 지급한다.
        private void MarkStageCleared()
        {
            bool firstClear = _progress.TryMarkCleared(_chapter, _stageIndex);
            GrantedRewards = _rewards.GrantVictory(_stage, firstClear);
            // 클리어 기록·보상 지급이 모두 끝난 스냅샷만 저장한다(부분 상태 저장 금지).
            _saveService?.Save();
        }

        // 입력 대기가 시작된 행동자를 ActingCombatant에 세우고, 이번 턴 상태(쿨은 턴 시작에 감소)를
        // 반영하도록 재읽기 신호도 함께 흘린다.
        private void OnInputRequested(ICombatant actor)
        {
            _actingCombatant.Value = Combatants.FirstOrDefault(u => u.Model == actor);
            _stateChanged.OnNext(Unit.Default);
        }

        // 행동자에 Acted(시전), 대상마다 Hit(피격)을 발화한다. 스킬을 안 쓴 턴(도트 사망 등)은 연출 없음.
        private void NotifyActor()
        {
            var turn = _engine.LastTurn;
            if (!turn.UsedSkill) return;

            var actor = Combatants.FirstOrDefault(u => u.Model == turn.Actor);
            actor?.RaiseActed(turn.Skill);

            foreach (var target in turn.Targets)
                Combatants.FirstOrDefault(u => u.Model == target)?.RaiseHit(turn.Skill);
        }

        // 도메인 유닛 순서를 유닛 VM 순서로 옮긴다. 매칭 실패 = 조립 오류이므로 First로 드러낸다.
        private IReadOnlyList<CombatantViewModel> MapOrder(IReadOnlyList<ICombatant> order)
            => order.Select(actor => Combatants.First(u => u.Model == actor)).ToList();

        private static BattleResult Map(BattleOutcome outcome) => outcome switch
        {
            BattleOutcome.Victory => BattleResult.Victory,
            BattleOutcome.Defeat => BattleResult.Defeat,
            _ => BattleResult.InProgress,
        };

        protected override void OnDispose()
        {
            base.OnDispose();

            _cts.Cancel();
            _cts.Dispose();

            _manualProvider.InputRequested -= OnInputRequested;
            _subscriptions.Dispose();

            foreach (var unit in Combatants) unit.Dispose();
            ActionCount.Dispose();
            Result.Dispose();
            UpcomingTurns.Dispose();
            _actingCombatant.Dispose();
            _autoMode.Dispose();
            _stateChanged.Dispose();
        }
    }
}