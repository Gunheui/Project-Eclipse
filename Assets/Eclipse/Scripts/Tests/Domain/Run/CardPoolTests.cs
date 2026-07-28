using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary> 카드 풀 배제 규칙과 비복원 추첨, 인연의 문 제시 가능 판정 검증. </summary>
    public class CardPoolTests
    {
        private sealed class FixedRunRandom : IRunRandom
        {
            public int NextInt(int maxExclusive) => 0;
        }

        [Test]
        public void 뽑힌_3장은_서로_다르다()
        {
            var pool = new CardPool(RunFixtures.CardCatalog(),
                new SeededRandom(RunSeed.For(99, RunSeed.Stream.Card)));

            for (int i = 0; i < 50; i++)
            {
                var picked = pool.Pick3(DoorKind.Attack, doorPoint: 2, RunFixtures.Party(1));
                Assert.AreEqual(3, picked.Count);
                Assert.AreEqual(3, picked.Select(c => c.id).Distinct().Count(), "비복원이라 중복이 없다");
            }
        }

        [Test]
        public void 특수_카드는_문_지점_1에서_후보에_들지_않는다()
        {
            var pool = new CardPool(RunFixtures.CardCatalog(),
                new SeededRandom(RunSeed.For(99, RunSeed.Stream.Card)));

            for (int i = 0; i < 50; i++)
                Assert.IsTrue(pool.Pick3(DoorKind.Attack, doorPoint: 1, RunFixtures.Party(1))
                    .All(c => c.tag != CardTag.Special), "지점 1은 특수 카드가 배제된다");
        }

        [Test]
        public void 인연의_문은_파티_전용_카드가_3장_미만이면_제시_불가다()
        {
            var party4 = RunFixtures.Party(4);
            var catalog = RunFixtures.CardCatalog(party4.Select(o => o.Definition.id).ToArray());
            var pool = new CardPool(catalog, new FixedRunRandom());

            Assert.IsTrue(pool.CanOfferBond(party4), "전용 카드 4장이면 제시 가능");
            Assert.IsFalse(pool.CanOfferBond(party4.Take(2).ToList()), "2인 파티는 전용 카드 2장뿐이라 제시 불가");
        }

        [Test]
        public void 문_추첨은_배제된_문을_내지_않는다()
        {
            var draw = new DoorDraw(RunFixtures.DoorCatalog(),
                new SeededRandom(RunSeed.For(123, RunSeed.Stream.Door)));

            for (int i = 0; i < 50; i++)
            {
                var doors = draw.DrawDistinct(3, DoorKind.Bond);
                Assert.IsFalse(doors.Contains(DoorKind.Bond), "배제한 인연의 문은 나오지 않는다");
                Assert.AreEqual(3, doors.Distinct().Count());
            }
        }
    }
}
