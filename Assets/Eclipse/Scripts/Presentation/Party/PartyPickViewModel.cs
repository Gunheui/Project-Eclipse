using System.Collections.Generic;
using System.Linq;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Service;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 파티 픽 화면의 상태. 보유 로스터를 항목로 노출하고, 탭 한 번으로 편성(PartyFormation)의 앵커 슬롯에 즉시
    /// 배치한다. 편성 draft를 직접 소유하지 않고 <see cref="PartyFormationViewModel"/>에 위임한다 —
    /// 이 화면은 로스터 표시·역할 필터·앵커 슬롯 배치만 책임진다(단일선택·상세 네비의 CharacterListViewModel과 관심사 분리).
    /// 편성 VM 위로 스택에 올라오므로 함께 Singleton으로 살아 세션 간 상태를 보존한다.
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

        /// <param name="save">보유 캐릭터 원천. 로스터 항목을 만든다.</param>
        /// <param name="spriteProvider">항목 초상 로딩 경로(공유 항목 VM에 전달).</param>
        /// <param name="formation">배치 대상 편성 VM(같은 스코프의 Singleton). 앵커 슬롯도 여기서 읽는다.</param>
        public PartyPickViewModel(PlayerSave save, ISpriteProvider spriteProvider, PartyFormationViewModel formation)
        {
            _formation = formation;
            _items = save.OwnedCharacters
                .Select(owned => new PartyPickItemViewModel(new CharacterItemViewModel(owned, spriteProvider)))
                .ToList();

            ApplySort(_sortKey.Value);
        }

        /// <summary>
        /// 픽 세션을 시작한다. 각 항목의 슬롯 번호 배지를 현재 편성 상태로 시드하고 역할 필터를 전체로 초기화한다.
        /// 픽 화면이 전면에 설 때 호출한다.
        /// </summary>
        public void BeginSession()
        {
            RoleFilter.Value = null;
            RefreshSlotNumbers();
        }

        /// <summary>
        /// 탭된 항목을 앵커 슬롯(<see cref="PartyFormationViewModel.PickSlot"/>)에 배치한다. 그 캐릭터가 이미
        /// 앵커 슬롯에 있으면 재탭으로 보고 슬롯을 비운다. 다른 슬롯에 있었다면 그 슬롯이 비워지며 이동한다.
        /// View는 이 호출 뒤 화면을 닫아 편성으로 돌아간다.
        /// </summary>
        /// <param name="item">탭된 로스터 항목. null이면 아무 일도 하지 않는다.</param>
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

        // 지정 기준으로 _items를 제자리 재배열한다(항목 VM 인스턴스는 그대로 재사용).
        // 반드시 CurrentSortKey 통지보다 먼저 돌아야 View가 재배열된 Items로 항목을 다시 만든다.
        private void ApplySort(CharacterSortKey key)
        {
            var sorted = CharacterSort.Apply(_items, item => item.Character, key);
            _items.Clear();
            _items.AddRange(sorted);
        }

        // 각 항목의 배지를 현재 점유 슬롯 번호(1~4)로 다시 매긴다. 어느 슬롯에도 없으면 0.
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
