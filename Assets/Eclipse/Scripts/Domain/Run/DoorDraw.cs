using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 약속 문의 가중 비복원 추첨. 문 지점(3종)과 미드보스 보상(2종)이 같은 라인업을 쓰는 두 호출자다.
    /// 추첨은 런 RNG의 Door 스트림 뒤에서 결정적이다.
    /// </summary>
    public sealed class DoorDraw
    {
        private readonly IRunRandom _rng;
        private readonly DoorEntry[] _lineup;

        private readonly struct DoorEntry
        {
            public DoorEntry(DoorChoice choice, int weight)
            {
                Choice = choice;
                Weight = weight;
            }

            public DoorChoice Choice { get; }
            public int Weight { get; }
        }

        /// <exception cref="ArgumentException">카탈로그가 비었거나 5종을 채우지 못하거나 가중 합이 0 이하일 때.</exception>
        public DoorDraw(DoorCatalogSO catalog, IRunRandom rng)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Validate(catalog);
            _lineup = BuildLineup(catalog);
        }

        /// <summary> 라인업 항목 수. 캐릭터 문이 파티 슬롯마다 한 항목으로 갈라진 뒤의 개수다. </summary>
        public int LineupSize => _lineup.Length;

        /// <summary>
        /// 문을 가중 비복원으로 count개 뽑는다. 호출할 때마다 난수를 소비하고, 매번 전체 라인업에서 새로 시작한다.
        /// </summary>
        /// <param name="count">뽑을 개수. 1 이상 <see cref="LineupSize"/> 이하.</param>
        public IReadOnlyList<DoorChoice> DrawDistinct(int count)
        {
            if (count < 1 || count > _lineup.Length)
                throw new ArgumentOutOfRangeException(nameof(count), count, "문 라인업 범위를 벗어난다.");

            var pool = _lineup.ToList();
            var picked = new List<DoorChoice>(count);
            for (int i = 0; i < count; i++)
            {
                int total = pool.Sum(d => d.Weight);
                int roll = _rng.NextInt(total);
                for (int j = 0; j < pool.Count; j++)
                {
                    roll -= pool[j].Weight;
                    if (roll < 0)
                    {
                        picked.Add(pool[j].Choice);
                        pool.RemoveAt(j);
                        break;
                    }
                }
            }
            return picked;
        }

        /// <summary>
        /// 카탈로그를 추첨 라인업으로 편다. 캐릭터 문 한 행이 파티 슬롯 수만큼의 항목으로 갈라지며,
        /// 슬롯별 가중은 그 행의 값을 그대로 쓴다.
        /// </summary>
        private static DoorEntry[] BuildLineup(DoorCatalogSO catalog)
        {
            var lineup = new List<DoorEntry>();
            foreach (var definition in catalog.doors)
            {
                if (definition.kind != DoorKind.CharacterBuff)
                {
                    lineup.Add(new DoorEntry(new DoorChoice(definition.kind), definition.weight));
                    continue;
                }
                for (int slot = 0; slot < PlayerSave.PartySlotCount; slot++)
                    lineup.Add(new DoorEntry(new DoorChoice(definition.kind, slot), definition.weight));
            }
            return lineup.ToArray();
        }

        /// <summary> 잘못된 카탈로그로 추첨하면 방마다 다른 곳에서 터지므로 로드 시점에 한 번 걸러 낸다. </summary>
        private static void Validate(DoorCatalogSO catalog)
        {
            if (catalog.doors == null || catalog.doors.Length == 0)
                throw new ArgumentException("문 카탈로그가 비어 있다.", nameof(catalog));
            if (catalog.doors.Any(d => d.weight < 0))
                throw new ArgumentException("음수 가중치 문이 있다.", nameof(catalog));
            if (catalog.doors.Sum(d => d.weight) <= 0)
                throw new ArgumentException("문 가중치 합이 0 이하다.", nameof(catalog));
            if (catalog.doors.Select(d => d.kind).Distinct().Count() != catalog.doors.Length)
                throw new ArgumentException("같은 종류의 문이 중복 등록돼 있다.", nameof(catalog));

            var missing = Enum.GetValues(typeof(DoorKind)).Cast<DoorKind>()
                .Except(catalog.doors.Select(d => d.kind))
                .ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"문 카탈로그에 {string.Join(", ", missing)} 행이 없다.", nameof(catalog));
        }
    }
}
