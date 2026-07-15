using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary> 전투 결과. Domain의 BattleOutcome을 View가 볼 수 있는 프레젠테이션 표현으로 옮긴 것. </summary>
    public enum BattleResult { InProgress, Victory, Defeat }

    /// <summary>
    /// 전투 화면 전체의 ViewModel. 전투 엔진을 턴 단위로 구동하고, 유닛·스킬·행동 수·결과를 리액티브로 노출한다.
    /// 매 턴이 끝날 때 턴 신호(_stateChanged)를 한 번 쏘고, 뷰 상태는 전부 그 신호에서 파생된다(폴링 없음).
    /// 아군 수동 턴에서는 엔진이 ManualActionProvider의 입력을 기다리며 자연히 멈춘다.
    /// </summary>
    public sealed class BattleViewModel : ViewModelBase
    {
        // 다가올 행동 순서 예보의 원천. 엔진과 같은 인스턴스를 공유해 실제 진행 상태를 그대로 조회한다.
        // 계약으로만 소비하므로 순서 규칙(ATB↔라운드제)을 갈아끼워도 타임라인은 그대로 따라온다.
        private readonly ITurnScheduler _scheduler;

        /// <summary> 타임라인에 미리 보여줄 다가올 행동 수. 표시줄의 칸 수와 맞춰야 한다. </summary>
        public const int TimelineSlots = 5;

        private readonly BattleEngine _engine;
        private readonly ManualActionProvider _manualProvider;
        private readonly ISceneFlow _sceneFlow;

        // 조준 UI 후보 산출용. 대상 규칙의 단일 출처는 도메인이며 VM은 그 결과를 명판으로 옮기기만 한다.
        private readonly TargetResolver _targeting = new();

        // 뷰가 상태를 다시 읽어야 할 때 발화하는 신호(턴 종료 + 스킬 선택 시).
        // 유닛 HP·스킬 쿨·행동 수·결과가 모두 여기에서 구독됨.
        private readonly Subject<Unit> _stateChanged = new();
        
        private readonly ReactiveProperty<CombatantViewModel> _actingCombatant = new(null);
        private readonly ReactiveProperty<bool> _autoMode;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly CancellationTokenSource _cts = new();

        private BattleOutcome _outcome = BattleOutcome.Ongoing;

        /// <param name="allies">아군 파티(유닛+아트, 로스터에서 구성).</param>
        /// <param name="enemies">적(유닛+아트, 스테이지 구성).</param>
        /// <param name="executor">스킬 효과 적용기(씬 스코프 주입).</param>
        /// <param name="actionCap">전장 누적 행동 상한.</param>
        /// <param name="startAuto">시작 시 오토 전투 여부.</param>
        /// <param name="sceneFlow">전투 종료·이탈 시 씬 전환 창구.</param>
        public BattleViewModel(
            IReadOnlyList<BattleUnitEntry> allies,
            IReadOnlyList<BattleUnitEntry> enemies,
            SkillExecutor executor,
            int actionCap,
            bool startAuto,
            ISceneFlow sceneFlow)
        {
            _sceneFlow = sceneFlow;

            // 아군 = 오토↔수동 겸용 프로바이더, 적 = AI. 둘 다 같은 규칙 정책을 공유(임계 40%·힐 on).
            var autoRule = new RuleBasedActionProvider(0.4f, useHealRule: true);
            _manualProvider = new ManualActionProvider(autoRule) { AutoMode = startAuto };
            var enemyAi = new RuleBasedActionProvider(0.4f, useHealRule: true);

            // 도메인(엔진·스케줄러)은 아트를 모르므로 유닛만 뽑아 넘긴다. 순서는 아군 먼저, 그다음 적.
            var all = allies.Concat(enemies).ToList();
            var allyUnits = allies.Select(e => e.Unit).ToList();
            var enemyUnits = enemies.Select(e => e.Unit).ToList();

            _scheduler = new AtbTurnScheduler(allyUnits.Concat(enemyUnits));
            _engine = new BattleEngine(allyUnits, enemyUnits, _scheduler, executor, _manualProvider, enemyAi, actionCap);

            Combatants = all
                .Select(e => new CombatantViewModel(e.Unit, _stateChanged, e.Battler, e.TimelineIcon))
                .ToList();

            // 행동 수·결과는 턴 신호에서 파생. 유닛 HP·쿨(CombatantViewModel/SkillSlotViewModel)도 같은 신호에서 파생.
            ActionCount = _stateChanged
                .Select(_ => _engine.ActionCount)
                .ToReadOnlyReactiveProperty(0);
            Result = _stateChanged
                .Select(_ => Map(_outcome))
                .ToReadOnlyReactiveProperty(BattleResult.InProgress);

            // 다가올 행동 순서도 같은 턴 신호에서 파생(스케줄러 예보를 명판 VM으로 옮김).
            var openingOrder = MapOrder(_scheduler.PreviewOrder(TimelineSlots)); // 첫 턴 전 화면에 세울 시작 편성 예보
            UpcomingTurns = _stateChanged
                .Select(_ => MapOrder(_scheduler.PreviewOrder(TimelineSlots)))
                .ToReadOnlyReactiveProperty(openingOrder);

            _autoMode = new ReactiveProperty<bool>(startAuto);
            _autoMode.Subscribe(on => _manualProvider.AutoMode = on).AddTo(_subscriptions);

            // 수동 아군 턴이 열려 입력 대기에 들어가면 그 유닛을 ActingCombatant으로 세운다(스킬 버튼 활성화용).
            _manualProvider.InputRequested += OnInputRequested;
        }

        /// <summary> 전투 참가 유닛 명판(아군+적). 순서는 스케줄러 입력 순. </summary>
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

        /// <summary>
        /// 전투가 끝날 때까지 턴을 반복 구동한다. 아군 수동 턴에서는 엔진이 Submit을 기다리며 이 안에서 멈춘다.
        /// 매 턴: 계산(즉시) → 행동자·상태 신호로 배틀러 연출 시작 → 그 연출이 끝날 때까지 대기 → 다음 턴.
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

                // 계산은 끝났다(HP 반영). 행동자·상태 신호를 흘리면 각 배틀러가 스스로 연출을 시작한다.
                NotifyActor();
                _stateChanged.OnNext(Unit.Default);

                // 그 연출들이 끝날 때까지 다음 턴을 미룬다(대기 = 실제 애니 길이).
                if (playTurnAnimation != null)
                    await playTurnAnimation(linked.Token);
            }
        }

        /// <summary>
        /// 플레이어가 스킬(과 대상)을 골랐을 때 호출한다. 대기 중이던 아군 턴을 그 행동으로 재개시킨다.
        /// </summary>
        /// <param name="skill">사용할 스킬 슬롯.</param>
        /// <param name="target">지정 대상 명판. null이면 각 효과의 TargetSelector가 대상을 정한다.</param>
        public void Submit(SkillSlotViewModel skill, CombatantViewModel target = null)
        {
            _actingCombatant.Value = null;
            _manualProvider.Submit(skill.Runtime, target?.Model);
        }

        /// <summary>
        /// 행동자가 단일-적 스킬로 직접 지정할 수 있는 대상 명판. 도발 규칙이 반영된다(도발 중인 적이
        /// 있으면 그들만). 여기 든 대상을 <see cref="Submit"/>에 넘기면 반드시 그 대상이 맞는다 —
        /// 조준 UI는 이 목록만 선택 가능으로 칠하면 된다.
        /// </summary>
        /// <param name="actor">지금 행동하는 유닛(이 유닛 기준의 상대 편을 후보로 삼는다).</param>
        /// <returns>지정 가능한 대상 명판. 생존한 상대가 없으면 빈 목록.</returns>
        public IReadOnlyList<CombatantViewModel> ValidManualTargets(CombatantViewModel actor)
        {
            var opponents = Combatants.Where(u => u.IsAlly != actor.IsAlly).ToList();
            var valid = _targeting.ValidManualTargets(opponents.Select(u => (ICombatant)u.Model).ToList());
            return opponents.Where(u => valid.Contains(u.Model)).ToList();
        }

        /// <summary> 전투 씬을 떠나 메인 씬으로 돌아간다. </summary>
        public UniTask ExitAsync() => _sceneFlow.ToMainAsync();

        // 입력 대기가 시작된 행동자를 대응하는 명판 VM으로 매핑해 ActingCombatant에 세운다.
        // 결정 화면이 이번 턴 상태(쿨은 턴 시작에 감소)를 반영하도록 재읽기 신호도 함께 흘린다.
        private void OnInputRequested(ICombatant actor)
        {
            _actingCombatant.Value = Combatants.FirstOrDefault(u => u.Model == actor);
            _stateChanged.OnNext(Unit.Default);
        }

        // 방금 행동한 유닛에 Acted(시전 연출), 이번 스킬 대상마다 Hit(피격 연출)을 발화시킨다.
        // 스킬을 안 쓴 턴(도트 사망 등)은 UsedSkill이 false라 아무 연출도 트리거하지 않는다.
        private void NotifyActor()
        {
            var turn = _engine.LastTurn;
            if (!turn.UsedSkill) return;

            var actor = Combatants.FirstOrDefault(u => u.Model == turn.Actor);
            actor?.RaiseActed(turn.Skill);

            foreach (var target in turn.Targets)
                Combatants.FirstOrDefault(u => u.Model == target)?.RaiseHit(turn.Skill);
        }

        // 스케줄러가 돌려준 도메인 유닛 순서를 대응 명판 VM 순서로 옮긴다. 스케줄러와 명판이 같은 유닛 목록에서
        // 만들어지므로 매칭은 항상 성공한다(실패하면 조립이 깨진 것이라 조용히 넘기지 않고 드러낸다).
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