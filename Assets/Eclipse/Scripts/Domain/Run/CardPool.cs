using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 버프 카드 3택1 추첨. 문별 풀에 조건부 배제(타 캐릭터 전용 카드)를 적용한 뒤 가중 비복원으로 뽑는다.
    /// </summary>
    public sealed class CardPool
    {
        private readonly BuffCardCatalogSO _catalog;
        private readonly IRunRandom _rng;

        /// <exception cref="ArgumentException">문 계열 후보가 3장 미만이거나 효과 없는 카드가 있을 때.</exception>
        public CardPool(BuffCardCatalogSO catalog, IRunRandom rng)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Validate(catalog);
        }

        /// <summary>
        /// 이 문의 후보 풀에서 3장을 가중 비복원으로 뽑는다.
        /// </summary>
        /// <param name="door">고른 버프 문. 재화 문을 넘기면 예외.</param>
        /// <param name="party">현재 파티(빈칸 null 포함). 캐릭터 문의 대상 판정에 쓴다.</param>
        public IReadOnlyList<BuffCard> Pick3(DoorChoice door, IReadOnlyList<OwnedCharacter> party)
        {
            var pool = EligiblePool(door, party);
            if (pool.Count < 3)
                throw new InvalidOperationException($"{door} 문의 후보가 3장 미만이다(현재 {pool.Count}장).");

            var remaining = pool.ToList();
            var picked = new List<BuffCard>(3);
            for (int i = 0; i < 3; i++)
            {
                int sum = remaining.Sum(c => _catalog.WeightOf(c.grade));
                int roll = _rng.NextInt(sum);
                for (int j = 0; j < remaining.Count; j++)
                {
                    roll -= _catalog.WeightOf(remaining[j].grade);
                    if (roll < 0)
                    {
                        picked.Add(remaining[j]);
                        remaining.RemoveAt(j);
                        break;
                    }
                }
            }
            return picked;
        }

        /// <summary> 문별 후보 풀을 만든다. 배제 규칙이 전부 여기 모인다. </summary>
        private List<BuffCard> EligiblePool(DoorChoice door, IReadOnlyList<OwnedCharacter> party)
        {
            switch (door.Kind)
            {
                case DoorKind.CharacterBuff:
                    string targetId = TargetIdOf(door, party);
                    return _catalog.cards
                        .Where(c => !c.targetsEnemies
                            && (string.IsNullOrEmpty(c.requiredCharacterId) || c.requiredCharacterId == targetId))
                        .ToList();
                case DoorKind.Curse:
                    return _catalog.cards.Where(c => c.targetsEnemies).ToList();
                default:
                    throw new ArgumentOutOfRangeException(nameof(door), door, "재화 문은 3택1 대상이 아니다.");
            }
        }

        /// <summary> 캐릭터 문이 가리키는 파티원의 id. </summary>
        /// <exception cref="ArgumentOutOfRangeException">슬롯이 파티 범위 밖이거나 빈칸일 때.</exception>
        private static string TargetIdOf(DoorChoice door, IReadOnlyList<OwnedCharacter> party)
        {
            if (party == null || door.TargetPartySlot < 0 || door.TargetPartySlot >= party.Count
                || party[door.TargetPartySlot] == null)
                throw new ArgumentOutOfRangeException(nameof(door), door,
                    "캐릭터 문의 대상 슬롯이 파티에 없다.");
            return party[door.TargetPartySlot].Definition.id;
        }

        /// <summary> 잘못된 카탈로그로 추첨하면 문마다 다른 곳에서 터지므로 로드 시점에 한 번 걸러 낸다. </summary>
        private static void Validate(BuffCardCatalogSO catalog)
        {
            if (catalog.cards == null || catalog.cards.Length == 0)
                throw new ArgumentException("카드 카탈로그가 비어 있다.", nameof(catalog));
            foreach (var card in catalog.cards)
                if (card.deltas == null || card.deltas.Length == 0 || card.deltas.Any(d => d.axis == Data.Enums.StatType.None))
                    throw new ArgumentException($"카드 '{card.id}'의 효과가 비었거나 None 축을 포함한다.", nameof(catalog));
            foreach (CardGrade grade in Enum.GetValues(typeof(CardGrade)))
                if (catalog.WeightOf(grade) <= 0)
                    throw new ArgumentException($"{grade} 등급의 가중치가 0 이하다.", nameof(catalog));

            // 유니크는 캐릭터마다 한 장씩이라 3장을 못 채운다. 후보 3장은 범용과 저주가 보증한다.
            if (catalog.cards.Count(c => !c.targetsEnemies && string.IsNullOrEmpty(c.requiredCharacterId)) < 3)
                throw new ArgumentException("범용 카드가 3장 미만이다.", nameof(catalog));
            if (catalog.cards.Count(c => c.targetsEnemies) < 3)
                throw new ArgumentException("저주 카드가 3장 미만이다.", nameof(catalog));
        }
    }
}
