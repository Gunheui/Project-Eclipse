using System;
using Eclipse.Data;
using Eclipse.Domain;
using R3;

namespace Eclipse.Presentation
{
    /// <summary> 성장 화면의 탭. Ascension은 열람만 되는 잠긴 탭이다. </summary>
    public enum GrowthTab
    {
        LevelUp,
        SkillEnhance,
        Ascension,
    }

    /// <summary>
    /// 성장 화면의 ViewModel. 레벨업·스킬 강화 커맨드와 표시 값(비용·스탯 프리뷰·구매 가능 여부)을 노출한다.
    /// 대상 캐릭터는 진입 전에 확정되므로 화면 안에서 바꿀 수 없다.
    /// </summary>
    public class GrowthViewModel : ViewModelBase
    {
        private readonly OwnedCharacter _owned;
        private readonly GrowthService _growth;
        private readonly SkillEnhanceService _enhance;
        private readonly int _maxLevel;

        private readonly ReactiveProperty<int> _level;
        private readonly ReactiveProperty<int> _ascensionTier;
        private readonly GrowthSkillSlot[] _skillSlots;
        private readonly CompositeDisposable _disposables = new();

        /// <summary> 표시명(정의에서 읽음). </summary>
        public string DisplayName => _owned.Definition.displayName;

        /// <summary> 현재 선택된 탭. 탭 버튼이 직접 값을 넣고 View가 구독해 패널을 바꾼다. </summary>
        public ReactiveProperty<GrowthTab> SelectedTab { get; } = new(GrowthTab.LevelUp);

        /// <summary> 현재 레벨. </summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        /// <summary> 레벨 상한(성장곡선에서 읽음). </summary>
        public int MaxLevel => _maxLevel;

        /// <summary> 돌파 단계(0 = 미돌파). 잠긴 돌파 탭의 별 표시가 구독한다. </summary>
        public ReadOnlyReactiveProperty<int> AscensionTier => _ascensionTier;

        /// <summary> 레벨업 1회 골드 비용. 만렙이면 올릴 곳이 없어 null이다. </summary>
        public ReadOnlyReactiveProperty<int?> LevelUpCost { get; }

        /// <summary>
        /// 지금 레벨업을 누르면 나올 결과. Success가 아니면 View가 버튼을 dim 처리하고 사유를 함께 띄운다.
        /// </summary>
        public ReadOnlyReactiveProperty<LevelUpResult> LevelUpState { get; }

        /// <summary> 현재 스탯과 한 번 올렸을 때의 스탯. next가 null이면 만렙이라 올릴 곳이 없다. </summary>
        public ReadOnlyReactiveProperty<(Stats current, Stats? next)> LevelStatsPreview { get; }

        /// <summary> 스킬 강화 1회당 소모 교본 수(고정). </summary>
        public int SkillManualCost { get; }

        /// <summary> 지정 슬롯의 표시 값 묶음. 스킬이 비어 있으면 null이고, View는 그 줄을 감춘다. </summary>
        /// <param name="slot">0 = 기본 공격, 1 = 일반 스킬, 2 = 궁극기.</param>
        public GrowthSkillSlot SlotAt(int slot) => _skillSlots[slot];

        /// <exception cref="ArgumentOutOfRangeException">slot이 슬롯 범위를 벗어날 때.</exception>
        private SkillSO SkillDefinitionAt(int slot) => slot switch
        {
            0 => _owned.Definition.basicSkill,
            1 => _owned.Definition.normalSkill,
            2 => _owned.Definition.ultimateSkill,
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };

        /// <summary>
        /// 내비게이션 보관함에서 선택된 캐릭터를 읽어 성장 화면 상태를 구성한다.
        /// 전제: 생성 전에 NavigationContext.Selected가 설정돼 있어야 한다.
        /// </summary>
        /// <exception cref="InvalidOperationException">선택 캐릭터 없이 화면을 열었을 때.</exception>
        public GrowthViewModel(NavigationContext context, GrowthService growth, SkillEnhanceService enhance,
            GrowthConfigSO config, CurrencyWallet wallet, CharacterGrowthSignals growthSignals)
        {
            if (context.Selected == null)
                throw new InvalidOperationException(
                    "NavigationContext.Selected가 비어 있습니다. 성장 화면을 Push하기 전에 선택 캐릭터를 기록해야 합니다.");

            _owned = context.Selected;
            _growth = growth;
            _enhance = enhance;
            _maxLevel = _owned.Definition.growthCurve.maxLevel;
            SkillManualCost = config.skillEnhanceManualCost;

            _level = new ReactiveProperty<int>(_owned.Level);
            _ascensionTier = new ReactiveProperty<int>(_owned.AscensionTier);

            LevelUpCost = _level
                .Select(level => level >= _maxLevel ? (int?)null : config.levelUpCostCoefficient * level)
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);

            LevelUpState = Observable.CombineLatest(_level, wallet.Gold,
                    (level, gold) => level >= _maxLevel ? LevelUpResult.MaxLevel
                        : gold < config.levelUpCostCoefficient * level ? LevelUpResult.InsufficientGold
                        : LevelUpResult.Success)
                .ToReadOnlyReactiveProperty(LevelUpResult.Success)
                .AddTo(_disposables);

            LevelStatsPreview = Observable.CombineLatest(_level, _ascensionTier,
                    (level, tier) =>
                    {
                        var current = CharacterStats.BuildAllyStats(_owned.Definition, level, tier, null);
                        Stats? next = level >= _maxLevel
                            ? null
                            : CharacterStats.BuildAllyStats(_owned.Definition, level + 1, tier, null);
                        return (current, next);
                    })
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);

            _skillSlots = new GrowthSkillSlot[OwnedCharacter.SkillSlotCount];
            for (int i = 0; i < _skillSlots.Length; i++)
            {
                var skill = SkillDefinitionAt(i);
                if (skill != null)
                    _skillSlots[i] = new GrowthSkillSlot(skill, _owned.SkillLevels[i], config, wallet);
            }

            // 신호는 로스터 전체가 함께 받으므로 자기 캐릭터인지 참조로 가려낸다.
            // 레벨·돌파·스킬 레벨만 넣어 주면 비용·프리뷰·구매 가능 여부는 파생 스트림이 알아서 따라온다.
            growthSignals.Changed
                .Where(changed => ReferenceEquals(changed, _owned))
                .Subscribe(_ => ApplyGrowth())
                .AddTo(_disposables);
        }

        /// <summary> 레벨업을 시도한다. 성공하면 재화 차감·레벨 증가·세이브가 함께 일어난다. </summary>
        public LevelUpResult LevelUp() => _growth.TryLevelUp(_owned);

        /// <summary> 지정 슬롯의 스킬 강화를 시도한다. 성공하면 골드·교본 차감과 세이브가 함께 일어난다. </summary>
        public SkillEnhanceResult EnhanceSkill(int slot) => _enhance.TryEnhance(_owned, slot);

        private void ApplyGrowth()
        {
            _level.Value = _owned.Level;
            _ascensionTier.Value = _owned.AscensionTier;
            for (int i = 0; i < _skillSlots.Length; i++)
                _skillSlots[i]?.Apply(_owned.SkillLevels[i]);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _disposables.Dispose();
            SelectedTab.Dispose();
            _level.Dispose();
            _ascensionTier.Dispose();
            foreach (var slot in _skillSlots)
                slot?.Dispose();
        }
    }
}
