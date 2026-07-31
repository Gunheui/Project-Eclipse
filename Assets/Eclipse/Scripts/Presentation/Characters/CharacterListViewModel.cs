using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using System.Collections.Generic;
using System.Linq;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 보유 캐릭터 목록을 항목 ViewModel들의 리스트로 노출하는 ViewModel.
    /// PlayerSave의 보유 캐릭터마다 항목 VM 하나를 만들어 담고, 정렬 기준에 따라 재배열한다.
    /// </summary>
    public class CharacterListViewModel : ViewModelBase
    {
        private NavigationContext _navigationContext;

        private readonly List<CharacterItemViewModel> _items = new List<CharacterItemViewModel>();
        private ReactiveProperty<CharacterSortKey> _sortKey = new ReactiveProperty<CharacterSortKey>(CharacterSortKey.Rarity);

        /// <summary> 항목 목록(현재 정렬 순서). View가 한 번 순회해 항목 뷰를 생성한다. </summary>
        public IReadOnlyList<CharacterItemViewModel> Items => _items;

        /// <summary> 현재 정렬 기준. View가 구독해 라벨을 갱신하고 항목 뷰를 다시 만든다. </summary>
        public ReadOnlyReactiveProperty<CharacterSortKey> CurrentSortKey => _sortKey;

        /// <summary>
        /// 역할 필터(null = 전체). 목록에서 항목을 빼지 않고 View가 표시만 거른다 —
        /// 항목 뷰의 인덱스가 이 목록의 인덱스와 계속 일치해야 선택이 올바른 캐릭터를 가리킨다.
        /// </summary>
        public ReactiveProperty<Role?> RoleFilter { get; } = new(null);

        public CharacterListViewModel(PlayerSave save, NavigationContext navigationContext, ISpriteProvider spriteProvider,
            CharacterGrowthSignals growthSignals)
        {
            _navigationContext = navigationContext;

            foreach (var character in save.OwnedCharacters)
            {
                _items.Add(new CharacterItemViewModel(character, spriteProvider, growthSignals));
            }

            ApplySort(_sortKey.Value);
        }

        /// <summary>
        /// 정렬 기준을 다음 값으로 넘기고 <see cref="Items"/>를 재배열한다.
        /// 리스트 자체가 바뀌므로 View는 CurrentSortKey를 받아 항목 뷰를 다시 만들어야 한다.
        /// </summary>
        public void CycleSort()
        {
            var next = CharacterSort.Next(_sortKey.Value);
            ApplySort(next);
            _sortKey.Value = next;
        }

        /// <summary>
        /// 지정 기준으로 _items를 제자리 재배열한다(항목 VM 인스턴스는 그대로 재사용).
        /// 반드시 CurrentSortKey 통지보다 먼저 돌아야 View가 재배열된 Items로 항목 뷰를 다시 만든다.
        /// </summary>
        private void ApplySort(CharacterSortKey key)
        {
            var sorted = CharacterSort.Apply(_items, item => item, key);
            _items.Clear();
            _items.AddRange(sorted);
        }

        /// <summary>
        /// index 번째 캐릭터를 선택 대상으로 보관함에 기록한다.
        /// 상세 화면 ViewModel이 생성될 때 이 값을 읽어 표시한다.
        /// </summary>
        public void Select(int index)
        {
            _navigationContext.Selected = _items[index].Owned;
        }

        protected override void OnDispose()
        {
            base.OnDispose();

            _sortKey.Dispose();
            RoleFilter.Dispose();
            _items.ForEach(viewModel => viewModel.Dispose());
            _items.Clear();
        }
    }
}
