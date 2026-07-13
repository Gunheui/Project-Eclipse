using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Domain;
using Eclipse.Service;
using R3;

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
        private readonly BattleEngine _engine;
        private readonly ManualActionProvider _manualProvider;
        private readonly ISceneFlow _sceneFlow;

        // 뷰가 상태를 다시 읽어야 할 때 발화하는 신호(턴 종료 + 스킬 선택 시).
        // 유닛 HP·스킬 쿨·행동 수·결과가 모두 여기에서 구독됨.
        private readonly Subject<Unit> _stateChanged = new();
        
        private readonly ReactiveProperty<BattleUnitViewModel> _actingUnit = new(null);
        private readonly ReactiveProperty<bool> _autoMode;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly CancellationTokenSource _cts = new();

        private BattleOutcome _outcome = BattleOutcome.Ongoing;

        /// <param name="allies">아군 파티 유닛(로스터에서 구성).</param>
        /// <param name="enemies">적 유닛(스테이지 구성).</param>
        /// <param name="executor">스킬 효과 적용기(씬 스코프 주입).</param>
        /// <param name="actionCap">전장 누적 행동 상한.</param>
        /// <param name="startAuto">시작 시 오토 전투 여부.</param>
        /// <param name="sceneFlow">전투 종료·이탈 시 씬 전환 창구.</param>
        public BattleViewModel(
            List<BattleUnit> allies,
            List<BattleUnit> enemies,
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

            var all = allies.Concat(enemies).ToList();
            var scheduler = new AtbTurnScheduler(all);
            _engine = new BattleEngine(allies, enemies, scheduler, executor, _manualProvider, enemyAi, actionCap);

            Units = all.Select(u => new BattleUnitViewModel(u, _stateChanged)).ToList();

            // 행동 수·결과는 턴 신호에서 파생. 유닛 HP·쿨(BattleUnitViewModel/SkillSlotViewModel)도 같은 신호에서 파생.
            ActionCount = _stateChanged
                .Select(_ => _engine.ActionCount)
                .ToReadOnlyReactiveProperty(0);
            Result = _stateChanged
                .Select(_ => Map(_outcome))
                .ToReadOnlyReactiveProperty(BattleResult.InProgress);

            _autoMode = new ReactiveProperty<bool>(startAuto);
            _autoMode.Subscribe(on => _manualProvider.AutoMode = on).AddTo(_subscriptions);

            // 수동 아군 턴이 열려 입력 대기에 들어가면 그 유닛을 ActingUnit으로 세운다(스킬 버튼 활성화용).
            _manualProvider.InputRequested += OnInputRequested;
        }

        /// <summary> 전투 참가 유닛 명판(아군+적). 순서는 스케줄러 입력 순. </summary>
        public IReadOnlyList<BattleUnitViewModel> Units { get; }

        /// <summary> 지금까지 실행된 누적 행동 수. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> ActionCount { get; }

        /// <summary> 전투 결과(진행/승리/패배). 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<BattleResult> Result { get; }

        /// <summary> 지금 입력을 기다리는 아군 유닛. 대기 중이 아니거나 오토면 null. </summary>
        public ReadOnlyReactiveProperty<BattleUnitViewModel> ActingUnit => _actingUnit;

        /// <summary> 오토 전투 토글. View가 값을 바꾸면 프로바이더에 반영된다. </summary>
        public ReactiveProperty<bool> AutoMode => _autoMode;

        /// <summary>
        /// 전투가 끝날 때까지 턴을 반복 구동한다. 아군 수동 턴에서는 엔진이 Submit을 기다리며 이 안에서 멈춘다.
        /// 매 턴이 끝나면 턴 신호를 쏴 뷰 상태(HP·쿨·행동 수·결과)를 갱신한다.
        /// </summary>
        /// <param name="ct">외부 취소 토큰(예: 화면 파괴). VM 내부 취소와 묶여 함께 루프를 끊는다.</param>
        public async UniTask StartAsync(CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            _outcome = BattleOutcome.Ongoing;
            while (_outcome == BattleOutcome.Ongoing)
            {
                _outcome = await _engine.AdvanceTurnAsync(linked.Token);
                _stateChanged.OnNext(Unit.Default);
            }
        }

        /// <summary>
        /// 플레이어가 스킬(과 대상)을 골랐을 때 호출한다. 대기 중이던 아군 턴을 그 행동으로 재개시킨다.
        /// </summary>
        /// <param name="skill">사용할 스킬 슬롯.</param>
        /// <param name="target">지정 대상 명판. null이면 각 효과의 TargetSelector가 대상을 정한다.</param>
        public void Submit(SkillSlotViewModel skill, BattleUnitViewModel target = null)
        {
            _actingUnit.Value = null;
            _manualProvider.Submit(skill.Runtime, target?.Model);
        }

        /// <summary> 전투 씬을 떠나 메인 씬으로 돌아간다. </summary>
        public UniTask ExitAsync() => _sceneFlow.ToMainAsync();

        // 입력 대기가 시작된 행동자를 대응하는 명판 VM으로 매핑해 ActingUnit에 세운다.
        // 결정 화면이 이번 턴 상태(쿨은 턴 시작에 감소)를 반영하도록 재읽기 신호도 함께 흘린다.
        private void OnInputRequested(ICombatant actor)
        {
            _actingUnit.Value = Units.FirstOrDefault(u => u.Model == actor);
            _stateChanged.OnNext(Unit.Default);
        }

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

            foreach (var unit in Units) unit.Dispose();
            ActionCount.Dispose();
            Result.Dispose();
            _actingUnit.Dispose();
            _autoMode.Dispose();
            _stateChanged.Dispose();
        }
    }
}