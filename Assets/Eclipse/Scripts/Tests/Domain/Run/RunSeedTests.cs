using System;
using System.Collections.Generic;
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
            // 같은 splitmix64를 별도 구현으로 돌려 뽑은 값이다. 알고리즘 증명이 아니라 구현 변경을 잡는 회귀 가드다.
            Assert.AreEqual(-568286415, RunSeed.For(1000, RunSeed.Stream.Encounter));
            Assert.AreEqual(1438610509, RunSeed.For(1000, RunSeed.Stream.Mutation));
            Assert.AreEqual(-567296780, RunSeed.For(1000, RunSeed.Stream.Door));
            Assert.AreEqual(-1543786313, RunSeed.For(1000, RunSeed.Stream.Card));
            Assert.AreEqual(122710398, RunSeed.For(1000, RunSeed.Stream.Currency));
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

        // 런 스트림 → 방 전투 시드 → 전투 스트림으로 파생이 두 단 겹치는 경로 전체를 본다.
        // XOR 파생이던 시절 이 조합이 8개 시드로 뭉쳐 방과 용도가 다른데도 같은 수열을 쓰던 자리다.
        [TestCase(0)]
        [TestCase(777)]
        [TestCase(-42)]
        [TestCase(20260727)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void 방과_용도를_겹쳐_파생해도_시드가_안_겹친다(int runSeed)
        {
            const int roomHeadroom = 16; // 챕터 1은 방 7개. 챕터가 늘어도 남도록 넉넉히 본다.
            var seeds = new List<int>();

            foreach (RunSeed.Stream stream in Enum.GetValues(typeof(RunSeed.Stream)))
                seeds.Add(RunSeed.For(runSeed, stream));

            for (int room = 0; room < roomHeadroom; room++)
            {
                int battleSeed = RunSeed.ForRoomBattle(runSeed, room);
                foreach (BattleSeed.Stream stream in Enum.GetValues(typeof(BattleSeed.Stream)))
                    seeds.Add(BattleSeed.For(battleSeed, stream));
            }

            var duplicated = seeds.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            CollectionAssert.IsEmpty(duplicated, "서로 다른 (방, 용도) 조합이 같은 시드에 떨어졌다");
        }
    }
}
