using Eclipse.Data.Enums;
using Eclipse.Domain;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 픽 화면 로스터 항목 하나를 감싸는 얇은 래퍼. 표시 데이터(초상·이름·등급·레벨)는 공유 항목 VM에 위임하고,
    /// 이 화면에만 있는 상태(점유 슬롯 번호)만 얹는다. 편성 상태를 공유 항목 VM에 넣지 않기 위한 경계다.
    /// </summary>
    public sealed class PartyPickItemViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<int> _slotNumber = new(0);

        /// <summary> 표시 데이터(초상·이름·등급·레벨 로드)를 담당하는 공유 항목 VM. </summary>
        public CharacterItemViewModel Character { get; }

        /// <summary> 이 항목이 표시하는 보유 캐릭터. 탭 시 편성 슬롯으로 배치된다. </summary>
        public OwnedCharacter Owned => Character.Owned;

        /// <summary> 역할(정의에서 읽음). 역할 필터가 표시 여부를 가른다. </summary>
        public Role Role => Character.Owned.Definition.role;

        /// <summary> 이 캐릭터가 점유한 편성 슬롯 번호(0 = 미편성, 1~4 = 슬롯 위치). View가 구독해 배지·강조를 갱신한다. </summary>
        public ReadOnlyReactiveProperty<int> SlotNumber => _slotNumber;

        public PartyPickItemViewModel(CharacterItemViewModel character)
        {
            Character = character;
        }

        /// <summary>점유 슬롯 번호를 설정한다(0이면 미편성). PartyPickViewModel의 재계산이 호출한다.</summary>
        internal void SetSlotNumber(int slotNumber) => _slotNumber.Value = slotNumber;

        protected override void OnDispose()
        {
            _slotNumber.Dispose();
            Character.Dispose();
        }
    }
}
