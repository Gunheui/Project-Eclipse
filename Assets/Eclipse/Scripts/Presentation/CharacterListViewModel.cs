using Eclipse.Domain;
using ObservableCollections;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 보유 캐릭터 목록을 셀 ViewModel들의 관측 가능한 리스트로 노출하는 ViewModel.
    /// PlayerSave의 보유 캐릭터마다 셀 VM 하나를 만들어 담는다.
    /// </summary>
    public class CharacterListViewModel : ViewModelBase
    {
        private PlayerSave _playerSave;

        private ObservableList<CharacterCellViewModel> _characterList;

        /// <summary> View가 구독하는 읽기전용 셀 목록. 항목 추가/제거가 이벤트로 흐른다. </summary>
        public IReadOnlyObservableList<CharacterCellViewModel> CharacterList => _characterList;

        public CharacterListViewModel(PlayerSave save)
        {
            _playerSave = save;
            _characterList = new ObservableList<CharacterCellViewModel>();

            foreach (var character in _playerSave.OwnedCharacters)
            {
                _characterList.Add(new CharacterCellViewModel(character));
            }
        }

        /// <summary> index 번째 셀의 레벨을 지정값으로 설정한다. 성공 여부를 그대로 전달한다. </summary>
        public bool SetLevel(int index, int level)
        {
            return _characterList[index].SetLevel(level);
        }

        protected override void OnDispose()
        {
            base.OnDispose();

            _characterList.ForEach(viewModel => viewModel.Dispose());
            _characterList.Clear();
        }
    }
}
