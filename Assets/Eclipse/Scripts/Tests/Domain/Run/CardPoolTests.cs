using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary> 카드 풀 배제·재정규화 공시와 인연의 문 제시 가능 판정 검증. </summary>
    public class CardPoolTests
    {
        private sealed class FixedRunRandom : IRunRandom
        {
            public int NextInt(int maxExclusive) => 0;
        }

        [Test]
        public void 공시_확률은_최종_3장_포함_실확률이다()
        {
            // 특수 카드 2장이 섞인 공격 풀(지점 2 이후): 계열 25×3 + 특수 12×2. 포함 확률 합은 3이다.
            var pool = new CardPool(RunFixtures.CardCatalog(), new FixedRunRandom());

            var result = pool.Pick3(DoorKind.Attack, doorPoint: 2, RunFixtures.Party(1));

            Assert.AreEqual(5, result.DisclosedOdds.Count, "배제 후 풀 전체가 공시된다");
            Assert.That(result.DisclosedOdds.Sum(o => o.odds), Is.EqualTo(3f).Within(0.001f),
                "포함 확률의 합 = 뽑는 장 수");
            Assert.IsTrue(result.DisclosedOdds.All(o => o.odds > 0f && o.odds <= 1f));
            // 가중이 높은 계열 카드가 특수 카드보다 포함 확률이 높다.
            float series = result.DisclosedOdds.First(o => o.card.tag == CardTag.Attack).odds;
            float special = result.DisclosedOdds.First(o => o.card.tag == CardTag.Special).odds;
            Assert.Greater(series, special);
        }

        [Test]
        public void 풀이_정확히_3장이면_전_카드_포함_확률이_1이다()
        {
            // 지점 1은 특수 카드가 배제돼 공격 풀이 계열 3장뿐이다.
            var pool = new CardPool(RunFixtures.CardCatalog(), new FixedRunRandom());

            var result = pool.Pick3(DoorKind.Attack, doorPoint: 1, RunFixtures.Party(1));

            Assert.IsTrue(result.DisclosedOdds.All(o => o.odds > 0.999f), "3장 풀에서 3장을 뽑으면 확정 등장이다");
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
