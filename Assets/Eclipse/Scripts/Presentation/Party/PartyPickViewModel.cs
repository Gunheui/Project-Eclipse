using System.Collections.Generic;
using System.Linq;
using Eclipse.Data.Enums;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 파티 픽 화면의 ViewModel. 보유 로스터를 항목으로 노출하고, 탭 한 번으로 앵커 슬롯에 즉시 배치한다.
    /// 편성 상태도 표시용 항목 VM도 소유하지 않고 <see cref="PartyFormationViewModel"/>에서 빌려 온다 —
    /// 이 화면의 책임은 로스터 표시·역할 필터·앵커 슬롯 배치까지다.
    /// </summary>
    public sealed class PartyPickViewModel : ViewModelBase
    {
        private readonly PartyFormationViewModel _formation;
        private readonly List<PartyPickItemViewModel> _items;

        /// <summary> 로스터 항목 목록(현재 정렬 순서). View가 한 번 순회해 항목을 생성한다. </summary>
        public IReadOnlyList<PartyPickItemViewModel> Items => _items;

        /// <summary> 역할 필터(null = 전체). View가 구독해 보이는 항목을 거른다. </summary>
        public ReactiveProperty<Role?> RoleFilter { get; } = new(null);

        private readonly ReactiveProperty<CharacterSortKey> _sortKey = new(CharacterSortKey.Rarity);

        /// <summary> 현재 정렬 기준. View가 구독해 라벨을 갱신하고 항목을 다시 만든다. </summary>
        public ReadOnlyReactiveProperty<CharacterSortKey> CurrentSortKey => _sortKey;

        public PartyPickViewModel(PartyFormationViewModel formation)
        {
            _formation = formation;
            _items = formation.Roster
                .Select(character => new PartyPickItemViewModel(character))
                .ToList();

            ApplySort(_sortKey.Value);
        }

        /// <summary>
        /// 픽 세션을 시작한다. 슬롯 번호 배지를 현재 편성으로 시드하고 역할 필터를 전체로 초기화한다.
        /// 픽 화면이 전면에 설 때 호출한다.
        /// </summary>
        public void BeginSession()
        {
            RoleFilter.Value = null;
            RefreshSlotNumbers();
        }

        /// <summary>
        /// 탭된 항목을 앵커 슬롯(<see cref="PartyFormationViewModel.PickSlot"/>)에 배치한다.
        /// 이미 앵커 슬롯에 있으면 재탭으로 보고 비우고, 다른 슬롯에 있었다면 이동한다. null이면 무시한다.
        /// </summary>
        public void Place(PartyPickItemViewModel item)
        {
            if (item == null)
                return;

            int anchor = _formation.PickSlot;
            if (_formation.Slots[anchor].Value == item.Owned)
                _formation.ClearSlot(anchor);
            else
                _formation.AssignToSlot(anchor, item.Owned);

            RefreshSlotNumbers();
        }

        /// <summary>
        /// 정렬 기준을 다음 값으로 넘기고 <see cref="Items"/>를 재배열한다.
        /// 리스트 자체가 바뀌므로 View는 CurrentSortKey를 받아 항목을 다시 만들어야 한다.
        /// </summary>
        public void CycleSort()
        {
            var next = CharacterSort.Next(_sortKey.Value);
            ApplySort(next);
            _sortKey.Value = next;
        }

        /// <summary>
        /// 지정 기준으로 _items를 제자리 재배열한다(항목 VM 인스턴스는 그대로 재사용).
        /// 반드시 CurrentSortKey 통지보다 먼저 돌아야 View가 재배열된 Items로 항목을 다시 만든다.
        /// </summary>
        private void ApplySort(CharacterSortKey key)
        {
            var sorted = CharacterSort.Apply(_items, item => item.Character, key);
            _items.Clear();
            _items.AddRange(sorted);
        }

        /// <summary>
        /// 각 항목의 배지를 현재 점유 슬롯 번호(1~4)로 다시 매긴다. 어느 슬롯에도 없으면 0.
        /// </summary>
        private void RefreshSlotNumbers()
        {
            var slots = _formation.Slots;
            foreach (var item in _items)
            {
                int number = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Value == item.Owned)
                    {
                        number = i + 1;
                        break;
                    }
                }
                item.SetSlotNumber(number);
            }
        }

        protected override void OnDispose()
        {
            RoleFilter.Dispose();
            _sortKey.Dispose();
            foreach (var item in _items)
                item.Dispose();
        }
    }
}
