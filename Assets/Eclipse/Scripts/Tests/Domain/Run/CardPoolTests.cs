using System;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary> 카드 풀 배제 규칙과 비복원 추첨 검증. </summary>
    public class CardPoolTests
    {
        private static CardPool Pool(params string[] uniqueTargetIds)
            => new CardPool(RunFixtures.CardCatalog(uniqueTargetIds),
                new SeededRandom(RunSeed.For(99, RunSeed.Stream.Card)));

        [Test]
        public void 뽑힌_3장은_서로_다르다()
        {
            var party = RunFixtures.Party(4);
            var pool = Pool();

            for (int i = 0; i < 50; i++)
            {
                var picked = pool.Pick3(new DoorChoice(DoorKind.CharacterBuff, 0), party);
                Assert.AreEqual(3, picked.Count);
                Assert.AreEqual(3, picked.Select(c => c.id).Distinct().Count(), "비복원이라 중복이 없다");
            }
        }

        [Test]
        public void 캐릭터_문은_타_캐릭터_전용_카드를_내지_않는다()
        {
            var party = RunFixtures.Party(4);
            var pool = Pool(party.Select(o => o.Definition.id).ToArray());
            string targetId = party[1].Definition.id;

            for (int i = 0; i < 50; i++)
                Assert.IsTrue(pool.Pick3(new DoorChoice(DoorKind.CharacterBuff, 1), party)
                        .All(c => string.IsNullOrEmpty(c.requiredCharacterId) || c.requiredCharacterId == targetId),
                    "대상 파티원 전용 카드만 후보에 든다");
        }

        [Test]
        public void 캐릭터_문은_저주_카드를_내지_않는다()
        {
            var party = RunFixtures.Party(4);
            var pool = Pool();

            for (int i = 0; i < 50; i++)
                Assert.IsTrue(pool.Pick3(new DoorChoice(DoorKind.CharacterBuff, 0), party)
                    .All(c => !c.targetsEnemies));
        }

        [Test]
        public void 저주_문은_저주_카드만_낸다()
        {
            var party = RunFixtures.Party(4);
            var pool = Pool();

            Assert.IsTrue(pool.Pick3(new DoorChoice(DoorKind.Curse), party).All(c => c.targetsEnemies));
        }

        [Test]
        public void 등급_가중이_행이_아니라_노브에서_온다()
        {
            var party = RunFixtures.Party(4);
            var pool = Pool();

            // 범용 풀은 등급마다 3장씩이라 첫 장의 등급 분포가 노브 60:30:10을 그대로 따라간다.
            const int trials = 3000;
            var counts = new int[3];
            for (int i = 0; i < trials; i++)
                counts[(int)pool.Pick3(new DoorChoice(DoorKind.CharacterBuff, 0), party)[0].grade]++;

            Assert.AreEqual(0.6f, counts[(int)CardGrade.Common] / (float)trials, 0.03f);
            Assert.AreEqual(0.3f, counts[(int)CardGrade.Rare] / (float)trials, 0.03f);
            Assert.AreEqual(0.1f, counts[(int)CardGrade.Epic] / (float)trials, 0.03f);
        }

        [Test]
        public void 재화_문은_3택1_대상이_아니다()
        {
            var party = RunFixtures.Party(4);
            var pool = Pool();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => pool.Pick3(new DoorChoice(DoorKind.Gold), party));
        }
    }
}
