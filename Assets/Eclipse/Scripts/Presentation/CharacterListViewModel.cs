using Eclipse.Domain;
using Eclipse.Service;
using ObservableCollections;
using R3;
using System.Collections.Generic;
using System.Linq;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 보유 캐릭터 목록을 셀 ViewModel들의 관측 가능한 리스트로 노출하는 ViewModel.
    /// PlayerSave의 보유 캐릭터마다 셀 VM 하나를 만들어 담고, 정렬 기준에 따라 재배열한다.
    /// </summary>
    public class CharacterListViewModel : ViewModelBase
    {
        /// <summary> 목록 정렬 기준. CycleSort가 이 순서로 순환한다. </summary>
        public enum SortKey { Rarity, Level, Name }

        private PlayerSave _playerSave;
        private NavigationContext _navigationContext;

        private ObservableList<CharacterCellViewModel> _characterList;
        private ReactiveProperty<SortKey> _sortKey = new ReactiveProperty<SortKey>(SortKey.Rarity);

        /// <summary> View가 구독하는 읽기전용 셀 목록. 항목 추가/제거가 이벤트로 흐른다. </summary>
        public IReadOnlyObservableList<CharacterCellViewModel> CharacterList => _characterList;

        /// <summary> 현재 정렬 기준. View가 구독해 정렬 라벨을 갱신한다. </summary>
        public ReadOnlyReactiveProperty<SortKey> CurrentSortKey => _sortKey;

        public CharacterListViewModel(PlayerSave save, NavigationContext navigationContext, ISpriteProvider spriteProvider)
        {
            _playerSave = save;
            _navigationContext = navigationContext;
            _characterList = new ObservableList<CharacterCellViewModel>();

            foreach (var character in _playerSave.OwnedCharacters)
            {
                _characterList.Add(new CharacterCellViewModel(character, spriteProvider));
            }

            ApplySort();
        }

        /// <summary>
        /// 정렬 기준을 다음 값으로 넘기고 목록을 재배열한다.
        /// 재배열은 관측 가능한 리스트를 타고 View의 셀 재생성으로 자동 반영된다.
        /// </summary>
        public void CycleSort()
        {
            _sortKey.Value = (SortKey)(((int)_sortKey.Value + 1) % 3);
            ApplySort();
        }

        /// <summary>
        /// 현재 정렬 기준으로 _characterList를 재배열한다. Clear는 Reset 이벤트라 View의
        /// ObserveRemove 구독이 놓치므로, 항목을 하나씩 제거(Remove 이벤트)한 뒤 정렬 순서로 다시 넣는다.
        /// </summary>
        private void ApplySort()
        {
            var sorted = SortedCells(_sortKey.Value);
            for (int i = _characterList.Count - 1; i >= 0; i--)
                _characterList.RemoveAt(i);
            foreach (var cell in sorted)
                _characterList.Add(cell);
        }

        /// <summary> 기준별로 정렬한 셀 순서를 만든다(원본 리스트는 건드리지 않는다). </summary>
        private List<CharacterCellViewModel> SortedCells(SortKey key)
        {
            var cells = _characterList.ToList();
            return key switch
            {
                SortKey.Rarity => cells.OrderByDescending(c => (int)c.Rarity).ThenBy(c => c.DisplayName).ToList(),
                SortKey.Level  => cells.OrderByDescending(c => c.Level.CurrentValue).ThenBy(c => c.DisplayName).ToList(),
                SortKey.Name   => cells.OrderBy(c => c.DisplayName).ToList(),
                _ => cells,
            };
        }

        /// <summary>
        /// index 번째 캐릭터를 선택 대상으로 보관함에 기록한다.
        /// 상세 화면 ViewModel이 생성될 때 이 값을 읽어 표시한다.
        /// </summary>
        public void Select(int index)
        {
            _navigationContext.Selected = _characterList[index].Owned;
        }

        protected override void OnDispose()
        {
            base.OnDispose();

            _sortKey.Dispose();
            _characterList.ForEach(viewModel => viewModel.Dispose());
            _characterList.Clear();
        }
    }
}
