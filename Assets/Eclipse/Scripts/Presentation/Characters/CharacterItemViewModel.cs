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
    /// 정의(CharacterSO)의 표시값과 계정별 레벨을 View에 노출한다.
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

        /// <summary> 현재 레벨. View가 구독하는 읽기전용 스트림. </summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        public CharacterItemViewModel(OwnedCharacter owned, ISpriteProvider spriteProvider)
        {
            _ownedCharacter = owned;
            _spriteProvider = spriteProvider;
            _level = new ReactiveProperty<int>(_ownedCharacter.Level);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _level.Dispose();
        }
    }
}
