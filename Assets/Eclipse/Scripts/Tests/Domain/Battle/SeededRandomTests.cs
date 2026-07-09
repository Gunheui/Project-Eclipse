using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void 같은_시드는_같은_수열을_낸다()
        {
            var a = new SeededRandom(12345);
            var b = new SeededRandom(12345);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextFloat(), b.NextFloat(), "같은 시드인데 i={0}에서 값이 갈렸다", i);
        }

        [Test]
        public void 다른_시드는_다른_수열을_낸다()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);

            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
                if (a.NextFloat() != b.NextFloat()) { anyDifferent = true; break; }

            Assert.IsTrue(anyDifferent, "다른 시드인데 20개 연속 동일 — 시딩이 시드를 반영하지 못함");
        }

        [Test]
        public void 모든_값은_0이상_1미만()
        {
            var rng = new SeededRandom(777);
            for (int i = 0; i < 10000; i++)
            {
                float v = rng.NextFloat();
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
        }
    }
}