using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary>
    /// 문 라인업 구성과 가중 비복원 추첨 검증. 캐릭터 문이 파티 슬롯 수만큼 갈라지는지,
    /// 슬롯별 가중이 카탈로그 값 그대로인지, 같은 시드가 같은 라인업을 재현하는지를 본다.
    /// </summary>
    public class DoorDrawTests
    {
        // 지정한 롤 값을 그대로 돌려주는 난수. 가중 구간의 경계를 정확히 찍어 보기 위한 것이다.
        private sealed class FixedRoll : IRunRandom
        {
            private readonly int _value;
            public FixedRoll(int value) { _value = value; }
            public int NextInt(int maxExclusive) => _value;
        }

        private static DoorDraw Draw(int seed)
            => new DoorDraw(RunFixtures.DoorCatalog(), new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Door)));

        [Test]
        public void 라인업은_캐릭터_문_슬롯_넷과_고정_넷으로_여덟이다()
        {
            var draw = Draw(1);
            Assert.AreEqual(8, draw.LineupSize);

            var all = draw.DrawDistinct(8);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new DoorChoice(DoorKind.CharacterBuff, 0), new DoorChoice(DoorKind.CharacterBuff, 1),
                    new DoorChoice(DoorKind.CharacterBuff, 2), new DoorChoice(DoorKind.CharacterBuff, 3),
                    new DoorChoice(DoorKind.Curse), new DoorChoice(DoorKind.Gold),
                    new DoorChoice(DoorKind.Manual), new DoorChoice(DoorKind.Essence),
                },
                all);
        }

        // 누적 가중 경계값 → 그 구간이 가리키는 문. 슬롯별 27과 전체 합 200을 함께 고정한다.
        [TestCase(0, DoorKind.CharacterBuff, 0)]
        [TestCase(26, DoorKind.CharacterBuff, 0)]
        [TestCase(27, DoorKind.CharacterBuff, 1)]
        [TestCase(53, DoorKind.CharacterBuff, 1)]
        [TestCase(54, DoorKind.CharacterBuff, 2)]
        [TestCase(107, DoorKind.CharacterBuff, 3)]
        [TestCase(108, DoorKind.Curse, -1)]
        [TestCase(133, DoorKind.Curse, -1)]
        [TestCase(134, DoorKind.Gold, -1)]
        [TestCase(163, DoorKind.Gold, -1)]
        [TestCase(164, DoorKind.Manual, -1)]
        [TestCase(179, DoorKind.Manual, -1)]
        [TestCase(180, DoorKind.Essence, -1)]
        [TestCase(199, DoorKind.Essence, -1)]
        public void 가중_구간이_문_하나씩에_대응한다(int roll, DoorKind kind, int slot)
        {
            var draw = new DoorDraw(RunFixtures.DoorCatalog(), new FixedRoll(roll));
            Assert.AreEqual(new DoorChoice(kind, slot), draw.DrawDistinct(1).Single());
        }

        [TestCase(3)]
        [TestCase(6)]
        public void 뽑힌_문은_서로_다르다(int count)
        {
            var draw = Draw(123);
            for (int i = 0; i < 50; i++)
            {
                var picked = draw.DrawDistinct(count);
                Assert.AreEqual(count, picked.Count);
                Assert.AreEqual(count, picked.Distinct().Count(), "비복원이라 중복이 없다");
            }
        }

        [Test]
        public void 같은_캐릭터_문이라도_슬롯이_다르면_다른_문이다()
        {
            Assert.AreNotEqual(new DoorChoice(DoorKind.CharacterBuff, 0), new DoorChoice(DoorKind.CharacterBuff, 1));
            Assert.AreEqual(new DoorChoice(DoorKind.CharacterBuff, 2), new DoorChoice(DoorKind.CharacterBuff, 2));
        }

        [Test]
        public void 매_호출은_전체_라인업에서_새로_시작한다()
        {
            var draw = Draw(7);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(8, draw.DrawDistinct(8).Distinct().Count(), "직전 호출이 라인업을 소진하지 않는다");
        }

        [Test]
        public void 같은_시드는_같은_추첨_수열을_낸다()
        {
            CollectionAssert.AreEqual(Sequence(555), Sequence(555));
            CollectionAssert.AreNotEqual(Sequence(555), Sequence(556));
        }

        [Test]
        public void 카탈로그에_빠진_문이_있으면_생성에서_걸린다()
        {
            var catalog = RunFixtures.DoorCatalog();
            catalog.doors = catalog.doors.Where(d => d.kind != DoorKind.Gold).ToArray();

            Assert.Throws<System.ArgumentException>(() => new DoorDraw(catalog, new FixedRoll(0)));
        }

        // 문 지점 5회분 추첨을 슬롯까지 포함한 문자열로 남긴다 — 종류만 기록하면 슬롯 차이가 지워진다.
        private static List<string> Sequence(int seed)
        {
            var draw = Draw(seed);
            return Enumerable.Range(0, 5)
                .Select(_ => string.Join(",", draw.DrawDistinct(3)))
                .ToList();
        }
    }
}
