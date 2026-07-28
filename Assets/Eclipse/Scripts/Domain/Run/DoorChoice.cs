using System;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 문 종류와 파티 슬롯 Index를 함께 저장하는 문 선택 값이다.
    /// 두 값이 모두 같을 때 같은 문 선택으로 판단한다.
    /// </summary>
    public readonly struct DoorChoice : IEquatable<DoorChoice>
    {
        /// <summary>파티 슬롯 Index를 사용하지 않는 문에 지정하는 값이다.</summary>
        public const int NoPartySlot = -1;

        public DoorChoice(DoorKind kind, int targetPartySlot = NoPartySlot)
        {
            Kind = kind;
            TargetPartySlot = targetPartySlot;
        }

        public DoorKind Kind { get; }

        /// <summary>
        /// 캐릭터 문이 대상으로 삼는 파티 슬롯 Index다(0부터 시작).
        /// 파티 슬롯 Index가 없는 문에는 <see cref="NoPartySlot"/>을 사용한다.
        /// </summary>
        public int TargetPartySlot { get; }

        public bool IsCharacterDoor => TargetPartySlot >= 0;

        public bool Equals(DoorChoice other)
            => Kind == other.Kind && TargetPartySlot == other.TargetPartySlot;

        public override bool Equals(object obj) => obj is DoorChoice other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ TargetPartySlot;

        public static bool operator ==(DoorChoice left, DoorChoice right) => left.Equals(right);

        public static bool operator !=(DoorChoice left, DoorChoice right) => !left.Equals(right);

        /// <summary>
        /// 캐릭터 문이면 문 종류와 파티 슬롯 Index를 <c>종류#슬롯</c> 형식으로 반환하고,
        /// 그 외의 문이면 문 종류만 반환한다.
        /// </summary>
        public override string ToString()
            => IsCharacterDoor ? $"{Kind}#{TargetPartySlot}" : Kind.ToString();
    }
}
