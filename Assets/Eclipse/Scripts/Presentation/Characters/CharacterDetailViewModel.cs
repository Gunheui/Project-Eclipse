using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary> 캐릭터 상세 화면의 탭. 좌측 초상은 그대로 두고 우측 패널만 바뀐다. </summary>
    public enum DetailTab
    {
        Basic,
        Growth,
    }

    /// <summary>
    /// 캐릭터 상세 화면의 ViewModel. 선택된 캐릭터의 표시 값(레벨·돌파·스탯·스킬)을 노출하고,
    /// 성장 신호를 받아 갱신한다. 성장 탭에서 레벨을 올려도 기본 정보 탭의 값이 따라온다.
    /// </summary>
    public class CharacterDetailViewModel : ViewModelBase
    {
        /// <summary> 현재 선택된 탭. 탭 버튼이 직접 값을 넣고 View가 구독해 패널을 바꾼다. </summary>
        public ReactiveProperty<DetailTab> SelectedTab { get; } = new(DetailTab.Basic);

        private readonly OwnedCharacter _owned;
        private readonly ISpriteProvider _spriteProvider;

        private readonly ReactiveProperty<int> _level;
        private readonly ReactiveProperty<int> _ascensionTier;
        private readonly ReactiveProperty<int>[] _skillLevels;
        private readonly ReactiveProperty<Stats> _currentStats;
        private readonly IDisposable _growthSubscription;

        /// <summary> 표시명(정의에서 읽음). </summary>
        public string DisplayName => _owned.Definition.displayName;

        /// <summary> 등급(정의에서 읽음). </summary>
        public Rarity Rarity => _owned.Definition.rarity;

        /// <summary> 역할(정의에서 읽음). </summary>
        public Role Role => _owned.Definition.role;

        /// <summary> 현재 레벨. </summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        /// <summary> 돌파 단계(0 = 미돌파). </summary>
        public ReadOnlyReactiveProperty<int> AscensionTier => _ascensionTier;

        /// <summary>
        /// 현재 레벨·돌파 기준 스탯 6종. 전투 조립과 같은 계산(<see cref="CharacterStats.BuildAllyStats"/>)을 써
        /// 표시와 전투가 달라지지 않는다.
        /// </summary>
        public ReadOnlyReactiveProperty<Stats> CurrentStats => _currentStats;

        /// <summary> 기본 공격 정의. </summary>
        public SkillSO BasicSkill => _owned.Definition.basicSkill;

        /// <summary> 일반 스킬 정의. </summary>
        public SkillSO NormalSkill => _owned.Definition.normalSkill;

        /// <summary> 궁극기 정의. </summary>
        public SkillSO UltimateSkill => _owned.Definition.ultimateSkill;

        /// <summary> 지정 슬롯의 스킬 레벨. </summary>
        /// <param name="slot">0 = 기본 공격, 1 = 일반 스킬, 2 = 궁극기.</param>
        public ReadOnlyReactiveProperty<int> SkillLevelAt(int slot) => _skillLevels[slot];

        /// <summary> 초상 스프라이트를 로드한다. 로딩 방식은 ISpriteProvider가 감춘다. </summary>
        public UniTask<Sprite> LoadPortraitAsync(CancellationToken ct = default)
            => _spriteProvider.LoadPortraitAsync(_owned.Definition, ct);

        /// <summary>
        /// 내비게이션 보관함에서 선택된 캐릭터를 읽어 상세 표시 값을 구성한다.
        /// 전제: 생성 전에 NavigationContext.Selected가 설정돼 있어야 한다.
        /// </summary>
        public CharacterDetailViewModel(NavigationContext context, ISpriteProvider spriteProvider,
            CharacterGrowthSignals growthSignals)
        {
            if (context.Selected == null)
                throw new InvalidOperationException(
                    "NavigationContext.Selected가 비어 있습니다. 상세 화면을 Push하기 전에 선택 캐릭터를 기록해야 합니다.");

            _owned = context.Selected;
            _spriteProvider = spriteProvider;

            _level = new ReactiveProperty<int>(_owned.Level);
            _ascensionTier = new ReactiveProperty<int>(_owned.AscensionTier);
            _skillLevels = _owned.SkillLevels.Select(lv => new ReactiveProperty<int>(lv)).ToArray();
            _currentStats = new ReactiveProperty<Stats>(BuildStats());

            // 신호는 로스터 전체가 함께 받으므로 자기 캐릭터인지 참조로 가려낸다.
            _growthSubscription = growthSignals.Changed
                .Where(changed => ReferenceEquals(changed, _owned))
                .Subscribe(_ => ApplyGrowth());
        }

        private Stats BuildStats()
            => CharacterStats.BuildAllyStats(_owned.Definition, _owned.Level, _owned.AscensionTier, null);

        private void ApplyGrowth()
        {
            _level.Value = _owned.Level;
            _ascensionTier.Value = _owned.AscensionTier;
            for (int i = 0; i < _skillLevels.Length; i++)
                _skillLevels[i].Value = _owned.SkillLevels[i];
            _currentStats.Value = BuildStats();
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            SelectedTab.Dispose();
            _growthSubscription.Dispose();
            _level.Dispose();
            _ascensionTier.Dispose();
            foreach (var skillLevel in _skillLevels)
                skillLevel.Dispose();
            _currentStats.Dispose();
        }
    }
}
