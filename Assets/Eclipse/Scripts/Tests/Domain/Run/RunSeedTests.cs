using System;
using System.Linq;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class RunSeedTests
    {
        [Test]
        public void 파생_시드가_고정값과_일치한다()
        {
            Assert.AreEqual(1000, RunSeed.For(1000, RunSeed.Stream.Encounter));
            Assert.AreEqual(1001, RunSeed.For(1000, RunSeed.Stream.Mutation));
            Assert.AreEqual(1002, RunSeed.For(1000, RunSeed.Stream.Door));
            Assert.AreEqual(1003, RunSeed.For(1000, RunSeed.Stream.Card));
        }

        [Test]
        public void 스트림마다_다른_시드를_낸다()
        {
            var seeds = Enum.GetValues(typeof(RunSeed.Stream))
                .Cast<RunSeed.Stream>()
                .Select(stream => RunSeed.For(777, stream))
                .ToArray();

            Assert.AreEqual(seeds.Length, seeds.Distinct().Count(), "스트림이 같은 시드로 겹쳤다");
        }

        [Test]
        public void 같은_런_시드는_항상_같은_파생값을_낸다()
        {
            Assert.AreEqual(
                RunSeed.For(-42, RunSeed.Stream.Encounter),
                RunSeed.For(-42, RunSeed.Stream.Encounter));
        }
    }
}
