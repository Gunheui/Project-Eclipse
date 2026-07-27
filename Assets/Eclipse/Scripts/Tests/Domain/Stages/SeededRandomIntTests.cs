using System;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary>
    /// 정수 롤(<see cref="IStageRandom"/>)의 결정성·경계·균등성 검사.
    /// 기대 수열은 같은 알고리즘(xorshift128+ / Lemire 구간 샘플링)을 별도 구현으로 돌려 얻은 값이다.
    /// </summary>
    public class SeededRandomIntTests
    {
        [Test]
        public void 고정_시드의_수열이_알려진_답과_일치한다()
        {
            var rng = new SeededRandom(12345);
            var expected = new[] { 2, 4, 2, 1, 2, 3, 4, 0, 5, 3 };

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], rng.NextInt(6), "i={0}에서 수열이 갈렸다", i);
        }

        [Test]
        public void 다른_시드_다른_상한도_알려진_답과_일치한다()
        {
            var rng = new SeededRandom(2024);
            var expected = new[] { 53, 66, 93, 67, 33, 87, 80, 10 };

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], rng.NextInt(100), "i={0}에서 수열이 갈렸다", i);
        }

        [Test]
        public void 상한_1은_항상_0을_낸다()
        {
            var rng = new SeededRandom(99);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(0, rng.NextInt(1));
        }

        [Test]
        public void 상한이_1_미만이면_예외()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-3));
        }

        [Test]
        public void 결과는_0이상_상한미만()
        {
            var rng = new SeededRandom(4242);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(7);
                Assert.GreaterOrEqual(v, 0);
                Assert.Less(v, 7);
            }
        }

        [Test]
        public void 값이_한쪽으로_쏠리지_않는다()
        {
            var rng = new SeededRandom(1);
            var counts = new int[3];
            const int rolls = 60000;

            for (int i = 0; i < rolls; i++)
                counts[rng.NextInt(3)]++;

            // 균등하면 각 20000. 편향 없는 구간 샘플링이면 오차는 1% 안쪽에 든다.
            foreach (int count in counts)
                Assert.That(count, Is.EqualTo(rolls / 3).Within(rolls / 100));
        }
    }
}
