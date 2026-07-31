using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 캐릭터 목록의 항목 하나에 대응하는 ViewModel.
    /// 정의(CharacterSO)의 표시값과 계정별 레벨·돌파를 View에 노출하고, 성장 신호를 받아 스스로 갱신한다.
    /// </summary>
    public class CharacterItemViewModel : ViewModelBase
    {
        private OwnedCharacter _ownedCharacter;
        private readonly ISpriteProvider _spriteProvider;

        /// <summary> 이 항목이 표시하는 보유 캐릭터. 선택 기록 시 목록 VM이 읽어 간다. </summary>
        public OwnedCharacter Owned => _ownedCharacter;

        /// <summary> 표시명(정의에서 읽음). </summary>
        public string DisplayName => _ownedCharacter.Definition.displayName;

        /// <summary> 등급(정의에서 읽음). </summary>
        public Rarity Rarity => _ownedCharacter.Definition.rarity;

        /// <summary> 역할(정의에서 읽음). 역할 필터가 표시 여부를 가른다. </summary>
        public Role Role => _ownedCharacter.Definition.role;

        /// <summary> 초상 스프라이트를 로드한다. 로딩 방식은 ISpriteProvider가 감춘다. </summary>
        public UniTask<Sprite> LoadPortraitAsync(CancellationToken ct = default)
            => _spriteProvider.LoadPortraitAsync(_ownedCharacter.Definition, ct);

        private ReactiveProperty<int> _level;
        private ReactiveProperty<int> _ascensionTier;
        private IDisposable _growthSubscription;

        /// <summary> 현재 레벨. View가 구독하는 읽기전용 스트림. </summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        /// <summary> 돌파 단계(0 = 미돌파). 별 위젯이 구독한다. </summary>
        public ReadOnlyReactiveProperty<int> AscensionTier => _ascensionTier;

        public CharacterItemViewModel(OwnedCharacter owned, ISpriteProvider spriteProvider,
            CharacterGrowthSignals growthSignals)
        {
            _ownedCharacter = owned;
            _spriteProvider = spriteProvider;
            _level = new ReactiveProperty<int>(_ownedCharacter.Level);
            _ascensionTier = new ReactiveProperty<int>(_ownedCharacter.AscensionTier);

            // 신호는 로스터 전체가 함께 받으므로 자기 캐릭터인지 참조로 가려낸다.
            _growthSubscription = growthSignals.Changed
                .Where(changed => ReferenceEquals(changed, _ownedCharacter))
                .Subscribe(changed =>
                {
                    _level.Value = changed.Level;
                    _ascensionTier.Value = changed.AscensionTier;
                });
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _growthSubscription.Dispose();
            _level.Dispose();
            _ascensionTier.Dispose();
        }
    }
}
