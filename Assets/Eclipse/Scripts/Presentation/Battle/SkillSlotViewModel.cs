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
            NeedsManualTarget = HasSingleEnemyEffect(runtime.Skill);
        }

        /// <summary> 스킬 정의(아이콘·표시 이름). 전투 내내 불변. </summary>
        public SkillSO Skill => Runtime.Skill;

        /// <summary> 남은 쿨(턴). 0이면 사용 가능. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> Cooldown { get; }

        /// <summary> 지금 사용 가능한지. 버튼 활성/비활성 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<bool> IsReady { get; }

        /// <summary>
        /// 이 스킬이 플레이어의 수동 대상 지정을 활용하는지. 효과 중 단일-적 셀렉터(최저HP/최고ATK 적)가
        /// 하나라도 있으면 true. true면 View가 스킬 탭 시 조준 모드로 들어가 대상을 받고, false면(광역·힐·자기)
        /// 지정 대상이 어차피 무시되므로 즉시 시전한다. 전투 내내 불변.
        /// </summary>
        public bool NeedsManualTarget { get; }

        // 효과 중 단일-적 셀렉터가 하나라도 있는지. 수동 지정이 실제로 반영되는 스킬(단일 적 공격)을 가린다.
        // 광역·아군·자기 셀렉터만 있는 스킬은 지정 대상이 무시되므로 조준 UI를 띄우지 않는다.
        // 판정은 도메인 규칙을 그대로 호출한다(셀렉터 집합이 늘어도 여기가 조용히 어긋나지 않게).
        private static bool HasSingleEnemyEffect(SkillSO skill)
            => skill.effects.Any(e => TargetResolver.IsSingleEnemySelector(e.target));

        /// <summary> 파생 프로퍼티의 구독을 해지한다. 소유자(CombatantViewModel)가 호출한다. </summary>
        public void Dispose()
        {
            Cooldown.Dispose();
            IsReady.Dispose();
        }
    }
}
