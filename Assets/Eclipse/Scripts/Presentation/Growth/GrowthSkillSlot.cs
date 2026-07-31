using System;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 성장 화면 스킬 한 줄의 표시 값. 스킬 레벨 하나에서 비용·위력 프리뷰·버튼 상태가 파생된다.
    /// 스킬이 없는 슬롯은 이 객체를 만들지 않는다.
    /// </summary>
    public sealed class GrowthSkillSlot : IDisposable
    {
        private readonly ReactiveProperty<int> _level;
        private readonly CompositeDisposable _disposables = new();

        /// <summary> 슬롯에 들어 있는 스킬 정의. </summary>
        public SkillSO Definition { get; }

        /// <summary> 현재 스킬 레벨. </summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        /// <summary> 강화 1회 골드 비용. 만렙이면 올릴 곳이 없어 null이다. </summary>
        public ReadOnlyReactiveProperty<int?> GoldCost { get; }

        /// <summary> 현재 위력 배수와 한 번 강화했을 때의 배수. next가 null이면 만렙이다. </summary>
        public ReadOnlyReactiveProperty<(float current, float? next)> PowerPreview { get; }

        /// <summary> 지금 강화를 누르면 나올 결과. Success가 아니면 버튼이 dim 처리된다. </summary>
        public ReadOnlyReactiveProperty<SkillEnhanceResult> EnhanceState { get; }

        public GrowthSkillSlot(SkillSO definition, int level, GrowthConfigSO config, CurrencyWallet wallet)
        {
            Definition = definition;
            _level = new ReactiveProperty<int>(level);

            GoldCost = _level
                .Select(lv => lv >= OwnedCharacter.MaxSkillLevel
                    ? (int?)null
                    : config.skillEnhanceCostCoefficient * lv)
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);

            PowerPreview = _level
                .Select(lv => (SkillRuntime.PowerMultiplierFor(lv),
                    lv >= OwnedCharacter.MaxSkillLevel
                        ? (float?)null
                        : SkillRuntime.PowerMultiplierFor(lv + 1)))
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);

            EnhanceState = Observable.CombineLatest(_level, wallet.Gold, wallet.Manual,
                    (lv, gold, manual) => lv >= OwnedCharacter.MaxSkillLevel
                        ? SkillEnhanceResult.MaxSkillLevel
                        : gold < config.skillEnhanceCostCoefficient * lv || manual < config.skillEnhanceManualCost
                            ? SkillEnhanceResult.InsufficientCurrency
                            : SkillEnhanceResult.Success)
                .ToReadOnlyReactiveProperty(SkillEnhanceResult.Success)
                .AddTo(_disposables);
        }

        /// <summary> 확정된 스킬 레벨을 반영한다. 나머지 표시 값은 파생 스트림이 따라온다. </summary>
        internal void Apply(int level) => _level.Value = level;

        public void Dispose()
        {
            _disposables.Dispose();
            _level.Dispose();
        }
    }
}
