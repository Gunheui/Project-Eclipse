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

        /// <param name="runtime">이 슬롯이 표시할 런타임 스킬(쿨 상태의 원천).</param>
        /// <param name="stateChanged">뷰가 상태를 다시 읽어야 할 때 발화하는 신호. 이 신호에 맞춰 쿨을 다시 읽는다.</param>
        public SkillSlotViewModel(SkillRuntime runtime, Observable<Unit> stateChanged)
        {
            Runtime = runtime;
            Cooldown = stateChanged
                .Select(_ => runtime.CurrentCooldown)
                .ToReadOnlyReactiveProperty(runtime.CurrentCooldown);
            IsReady = stateChanged
                .Select(_ => runtime.IsReady)
                .ToReadOnlyReactiveProperty(runtime.IsReady);
        }

        /// <summary> 스킬 정의(아이콘·표시 이름). 전투 내내 불변. </summary>
        public SkillSO Skill => Runtime.Skill;

        /// <summary> 남은 쿨(턴). 0이면 사용 가능. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> Cooldown { get; }

        /// <summary> 지금 사용 가능한지. 버튼 활성/비활성 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<bool> IsReady { get; }

        /// <summary> 파생 프로퍼티의 구독을 해지한다. 소유자(CombatantViewModel)가 호출한다. </summary>
        public void Dispose()
        {
            Cooldown.Dispose();
            IsReady.Dispose();
        }
    }
}
