using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 약속 문의 가중 비복원 추첨. 문 지점(3종)과 미드보스 보상(2종)이 같은 가중표를 쓰는 두 호출자다.
    /// 추첨은 런 RNG의 Door 스트림 뒤에서 결정적이다.
    /// </summary>
    public sealed class DoorDraw
    {
        private readonly DoorCatalogSO _catalog;
        private readonly IRunRandom _rng;

        /// <exception cref="ArgumentException">카탈로그가 비었거나 가중 합이 0 이하일 때.</exception>
        public DoorDraw(DoorCatalogSO catalog, IRunRandom rng)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Validate(catalog);
        }

        /// <summary>
        /// 문 종류를 가중 비복원으로 count종 뽑는다. 호출할 때마다 난수를 소비한다.
        /// </summary>
        /// <param name="count">뽑을 종 수. 1 이상 추첨 가능한 문 종 수 이하.</param>
        /// <param name="excluded">추첨에서 뺄 문. 파티 조건으로 제시할 수 없는 문(인연)을 거른다. null이면 전 종.</param>
        public IReadOnlyList<DoorKind> DrawDistinct(int count, DoorKind? excluded = null)
        {
            var pool = _catalog.doors.Where(d => d.kind != excluded).ToList();
            if (count < 1 || count > pool.Count)
                throw new ArgumentOutOfRangeException(nameof(count), count, "문 종 수 범위를 벗어난다.");
            var picked = new List<DoorKind>(count);
            for (int i = 0; i < count; i++)
            {
                int total = pool.Sum(d => d.weight);
                int roll = _rng.NextInt(total);
                for (int j = 0; j < pool.Count; j++)
                {
                    roll -= pool[j].weight;
                    if (roll < 0)
                    {
                        picked.Add(pool[j].kind);
                        pool.RemoveAt(j);
                        break;
                    }
                }
            }
            return picked;
        }

        // 잘못된 카탈로그로 추첨하면 방마다 다른 곳에서 터지므로 로드 시점에 한 번 걸러 낸다.
        private static void Validate(DoorCatalogSO catalog)
        {
            if (catalog.doors == null || catalog.doors.Length == 0)
                throw new ArgumentException("문 카탈로그가 비어 있다.", nameof(catalog));
            if (catalog.doors.Sum(d => d.weight) <= 0)
                throw new ArgumentException("문 가중치 합이 0 이하다.", nameof(catalog));
            if (catalog.doors.Any(d => d.weight < 0))
                throw new ArgumentException("음수 가중치 문이 있다.", nameof(catalog));
            if (catalog.doors.Select(d => d.kind).Distinct().Count() != catalog.doors.Length)
                throw new ArgumentException("같은 종류의 문이 중복 등록돼 있다.", nameof(catalog));
        }
    }
}