using System;
using System.Linq;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class StageSeedTests
    {
        [Test]
        public void 파생_시드가_고정값과_일치한다()
        {
            Assert.AreEqual(1000, StageSeed.For(1000, StageSeed.Stream.Encounter));
            Assert.AreEqual(1001, StageSeed.For(1000, StageSeed.Stream.Mutation));
            Assert.AreEqual(1002, StageSeed.For(1000, StageSeed.Stream.Door));
            Assert.AreEqual(1003, StageSeed.For(1000, StageSeed.Stream.Card));
        }

        [Test]
        public void 스트림마다_다른_시드를_낸다()
        {
            var seeds = Enum.GetValues(typeof(StageSeed.Stream))
                .Cast<StageSeed.Stream>()
                .Select(stream => StageSeed.For(777, stream))
                .ToArray();

            Assert.AreEqual(seeds.Length, seeds.Distinct().Count(), "스트림이 같은 시드로 겹쳤다");
        }

        [Test]
        public void 같은_스테이지_시드는_항상_같은_파생값을_낸다()
        {
            Assert.AreEqual(
                StageSeed.For(-42, StageSeed.Stream.Encounter),
                StageSeed.For(-42, StageSeed.Stream.Encounter));
        }
    }
}
