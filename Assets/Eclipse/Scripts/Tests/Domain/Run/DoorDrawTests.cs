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

        // 적어 둔 롤 값을 순서대로 돌려주는 난수. 추첨과 자리 굴리기의 소비 순서를 고정하기 위한 것이다.
        private sealed class ScriptedRoll : IRunRandom
        {
            private readonly int[] _values;
            private int _next;
            public ScriptedRoll(params int[] values) { _values = values; }
            public int NextInt(int maxExclusive) => _values[_next++];
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

        // --- 지점 구성 (미드보스 문) ---

        [Test]
        public void 미드보스_없는_지점은_한_자리에_한_종씩_선다()
        {
            var point = Draw(5).DrawDoorPoint(includeMidBoss: false);

            Assert.AreEqual(3, point.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 1, 1 }, point.Select(p => p.Count));
            Assert.AreEqual(3, point.SelectMany(p => p).Distinct().Count(), "비복원이라 중복이 없다");
        }

        [Test]
        public void 미드보스_지점은_한_자리에_2종_나머지에_1종씩_선다()
        {
            var draw = Draw(99);
            for (int i = 0; i < 50; i++)
            {
                var point = draw.DrawDoorPoint(includeMidBoss: true);

                Assert.AreEqual(3, point.Count);
                CollectionAssert.AreEquivalent(new[] { 2, 1, 1 }, point.Select(p => p.Count));
                var all = point.SelectMany(p => p).ToList();
                Assert.AreEqual(4, all.Count);
                Assert.AreEqual(4, all.Distinct().Count(), "지점 안 4종은 서로 다르다");
            }
        }

        // 미드보스 자리를 바꿔 가며, 걸린 2종이 추첨 첫째·넷째인지와 자리 굴림이 추첨 뒤인지를 함께 고정한다.
        // 자리를 먼저 굴리면 첫 롤을 자리가 먹어 position 1·2에서 어긋난다.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void 미드보스_문은_추첨_첫째와_넷째를_걸고_자리는_추첨_뒤에_정해진다(int position)
        {
            var draw = new DoorDraw(RunFixtures.DoorCatalog(), new ScriptedRoll(0, 0, 0, 0, position));

            var point = draw.DrawDoorPoint(includeMidBoss: true);

            // 롤 0은 남은 라인업의 첫 항목을 집으므로 캐릭터 문 슬롯 0~3이 순서대로 뽑힌다.
            CollectionAssert.AreEqual(
                new[] { new DoorChoice(DoorKind.CharacterBuff, 0), new DoorChoice(DoorKind.CharacterBuff, 3) },
                point[position]);
            var normals = point.Where((_, i) => i != position).Select(p => p.Single()).ToList();
            CollectionAssert.AreEqual(
                new[] { new DoorChoice(DoorKind.CharacterBuff, 1), new DoorChoice(DoorKind.CharacterBuff, 2) },
                normals);
        }

        [Test]
        public void 미드보스_문_자리는_세_자리에_고르게_흩어진다()
        {
            var draw = Draw(4242);
            var counts = new int[3];

            for (int i = 0; i < 3000; i++)
                counts[MidBossSeat(draw.DrawDoorPoint(includeMidBoss: true))]++;

            foreach (int count in counts)
                Assert.That(count, Is.EqualTo(1000).Within(150), "자리 빈도가 1/3 근처로 모인다");
        }

        [Test]
        public void 같은_시드는_같은_지점_구성을_낸다()
        {
            CollectionAssert.AreEqual(PointSequence(31), PointSequence(31));
            CollectionAssert.AreNotEqual(PointSequence(31), PointSequence(32));
        }

        private static int MidBossSeat(IReadOnlyList<IReadOnlyList<DoorChoice>> point)
        {
            for (int i = 0; i < point.Count; i++)
                if (point[i].Count == 2) return i;
            throw new AssertionException("미드보스 문이 없는 지점이다.");
        }

        // 문 지점 5회분 추첨을 슬롯까지 포함한 문자열로 남긴다 — 종류만 기록하면 슬롯 차이가 지워진다.
        private static List<string> Sequence(int seed)
        {
            var draw = Draw(seed);
            return Enumerable.Range(0, 5)
                .Select(_ => string.Join(",", draw.DrawDistinct(3)))
                .ToList();
        }

        // 자리 구분(|)까지 남긴다 — 이어 붙이면 미드보스 자리가 어디였는지가 지문에서 지워진다.
        private static List<string> PointSequence(int seed)
        {
            var draw = Draw(seed);
            return Enumerable.Range(0, 5)
                .Select(_ => string.Join("|", draw.DrawDoorPoint(includeMidBoss: true)
                    .Select(seat => string.Join(",", seat))))
                .ToList();
        }
    }
}
