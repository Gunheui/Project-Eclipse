using Eclipse.Data.Enums;
using Eclipse.Domain;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 캐릭터 목록의 셀 하나에 대응하는 ViewModel.
    /// 정의(CharacterSO)의 표시값과 계정별 레벨을 View에 노출한다.
    /// </summary>
    public class CharacterCellViewModel : ViewModelBase
    {
        private OwnedCharacter _ownedCharacter;

        /// <summary> 이 셀이 표시하는 보유 캐릭터. 선택 기록 시 목록 VM이 읽어 간다. </summary>
        public OwnedCharacter Owned => _ownedCharacter;

        /// <summary> 표시명(정의에서 읽음). </summary>
        public string DisplayName => _ownedCharacter.Definition.displayName;

        /// <summary> 등급(정의에서 읽음). </summary>
        public Rarity Rarity => _ownedCharacter.Definition.rarity;

        /// <summary> 초상 스프라이트(정의에서 읽음). </summary>
        public Sprite Portrait => _ownedCharacter.Definition.portraitAssetRef;

        private ReactiveProperty<int> _level;

        /// <summary> 현재 레벨. View가 구독하는 읽기전용 스트림. </summary>
        public ReadOnlyReactiveProperty<int> Level => _level;

        public CharacterCellViewModel(OwnedCharacter owned)
        {
            _ownedCharacter = owned;
            _level = new ReactiveProperty<int>(_ownedCharacter.Level);
        }

        /// <summary>
        /// 레벨을 지정값으로 설정한다. 유효 범위(1 ~ growthCurve.maxLevel) 밖이면
        /// 아무 것도 바꾸지 않고 false를 반환한다.
        /// </summary>
        public bool SetLevel(int level)
        {
            if (level > _ownedCharacter.Definition.growthCurve.maxLevel || level < 1)
                return false;

            _level.Value = level;
            _ownedCharacter.Level = level;
            return true;
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _level.Dispose();
        }
    }
}
