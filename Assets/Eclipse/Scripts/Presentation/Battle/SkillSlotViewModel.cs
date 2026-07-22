using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 전투 HUD의 스킬 버튼 하나에 대응하는 ViewModel. 스킬 정의(아이콘·이름)는 고정이고,
    /// 잔여 쿨·사용가능 여부는 매 턴 바뀌므로 턴 신호에서 파생한 리액티브 프로퍼티로 노출한다.
    /// </summary>
    public sealed class SkillSlotViewModel
    {
        // 이 슬롯이 대응하는 런타임 스킬. Submit 시 엔진에 되돌려 넘기려고 들고 있다.
        internal SkillRuntime Runtime { get; }

        public SkillSlotViewModel(SkillRuntime runtime, Observable<Unit> stateChanged)
        {
            Runtime = runtime;
            Cooldown = stateChanged
                .Select(_ => runtime.CurrentCooldown)
                .ToReadOnlyReactiveProperty(runtime.CurrentCooldown);
            IsReady = stateChanged
                .Select(_ => runtime.IsReady)
                .ToReadOnlyReactiveProperty(runtime.IsReady);
            NeedsManualTarget = HasSingleEnemyEffect(runtime.Skill) || HasSingleAllyEffect(runtime.Skill);
            ManualTargetsAllies = HasSingleAllyEffect(runtime.Skill) && !HasSingleEnemyEffect(runtime.Skill);
        }

        /// <summary> 스킬 정의(아이콘·표시 이름). 전투 내내 불변. </summary>
        public SkillSO Skill => Runtime.Skill;

        /// <summary> 남은 쿨(턴). 0이면 사용 가능. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> Cooldown { get; }

        /// <summary> 지금 사용 가능한지. 버튼 활성/비활성 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<bool> IsReady { get; }

        /// <summary>
        /// 수동 대상 지정을 사용하는 스킬인지(단일-적/단일-아군 효과 보유). true면 View가 스킬 탭 시
        /// 조준 모드로 들어가고, false면(광역·자기) 즉시 시전한다. 전투 내내 불변.
        /// </summary>
        public bool NeedsManualTarget { get; }

        /// <summary>
        /// 조준 대상이 아군인지(힐/버프). 단일-아군 효과만 있을 때 true이며 혼합 스킬은 적 우선.
        /// 후보 팀과 아웃라인 색을 정하는 단일 소스. 전투 내내 불변.
        /// </summary>
        public bool ManualTargetsAllies { get; }

        // 효과 중 단일-적 스코프가 하나라도 있는지. 판정은 도메인 규칙(TargetResolver)을 그대로 호출한다.
        private static bool HasSingleEnemyEffect(SkillSO skill)
            => skill.effects.Any(e => TargetResolver.IsSingleEnemy(e.target));

        // 효과 중 단일-아군 스코프가 하나라도 있는지.
        private static bool HasSingleAllyEffect(SkillSO skill)
            => skill.effects.Any(e => TargetResolver.IsSingleAlly(e.target));

        /// <summary> 파생 프로퍼티의 구독을 해지한다. 소유자(CombatantViewModel)가 호출한다. </summary>
        public void Dispose()
        {
            Cooldown.Dispose();
            IsReady.Dispose();
        }
    }
}
