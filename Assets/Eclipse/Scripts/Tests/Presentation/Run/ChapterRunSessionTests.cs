using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class ChapterRunSessionTests
    {
        private static ChapterRunSession Session(int partySize = 4)
            => new ChapterRunSession(RunFixtures.DocChapter(), RunFixtures.Tuning(),
                RunFixtures.Party(partySize), runSeed: 1);

        private static BuffCard Buff(string id, StatType axis = StatType.Atk)
            => new BuffCard
            {
                id = id, displayName = id, grade = CardGrade.Common,
                deltas = new[] { new StatDelta { axis = axis, value = 0.1f } },
            };

        private static BuffCard Curse(string id)
            => new BuffCard
            {
                id = id, displayName = id, grade = CardGrade.Common, targetsEnemies = true,
                deltas = new[] { new StatDelta { axis = StatType.Atk, value = -0.1f } },
            };

        [Test]
        public void 배정한_카드가_귀속_슬롯과_함께_순서대로_기록된다()
        {
            var session = Session();

            session.AttachCard(Buff("a"), 2);
            session.AttachCard(Curse("c"), 0);
            session.AttachCard(Buff("b"), 2);

            CollectionAssert.AreEqual(new[] { "a", "c", "b" },
                session.AcquiredCards.Select(x => x.Card.id).ToList(), "획득 순서가 그대로 남는다");
            CollectionAssert.AreEqual(new[] { 2, DoorChoice.NoPartySlot, 2 },
                session.AcquiredCards.Select(x => x.PartySlot).ToList(), "저주는 귀속 슬롯을 갖지 않는다");
            Assert.IsTrue(session.AcquiredCards[1].TargetsEnemies);
        }

        [Test]
        public void 축이_빈_증감이_섞이면_합계도_기록도_남기지_않는다()
        {
            var session = Session();
            var broken = new BuffCard
            {
                id = "broken", displayName = "broken", grade = CardGrade.Common,
                deltas = new[]
                {
                    new StatDelta { axis = StatType.Atk, value = 0.1f },
                    new StatDelta { axis = StatType.None, value = 0.2f },
                },
            };

            Assert.Throws<ArgumentException>(() => session.AttachCard(broken, 0));
            Assert.AreEqual(0, session.AcquiredCards.Count);
            Assert.AreEqual(0f, session.BuffsOf(0).SumOf(StatType.Atk), "앞선 증감도 반영되지 않는다");
        }

        [Test]
        public void 빈_슬롯_배정은_합계도_기록도_남기지_않는다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("a0"), null };
            var session = new ChapterRunSession(RunFixtures.DocChapter(), RunFixtures.Tuning(), party, runSeed: 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => session.AttachCard(Buff("x"), 1));
            Assert.AreEqual(0, session.AcquiredCards.Count);
            Assert.AreEqual(0f, session.BuffsOf(1).SumOf(StatType.Atk));
        }

        [Test]
        public void 미드보스_표시는_보류분이_풀리거나_몰수되면_함께_꺼진다()
        {
            var session = Session();
            var midBoss = new[] { new DoorChoice(DoorKind.Gold), new DoorChoice(DoorKind.Essence) };

            session.HoldEscrow(midBoss, engagesMidBoss: true);
            Assert.IsTrue(session.MidBossEngaged);
            session.ClaimEscrow();
            Assert.IsFalse(session.MidBossEngaged, "정예 방을 넘기면 뒤따르는 정예 자리로 번지지 않는다");

            session.HoldEscrow(midBoss, engagesMidBoss: true);
            session.ForfeitEscrow();
            Assert.IsFalse(session.MidBossEngaged, "몰수도 같은 자리에서 함께 꺼진다");
        }

        [Test]
        public void 정예_자리_앞에_문_지점이_없는_배치는_런_시작에서_끊긴다()
        {
            var noDoorBefore = RunFixtures.Chapter(
                RunFixtures.Normal(1, false), RunFixtures.Elite(4, true), RunFixtures.Boss());
            var eliteFirst = RunFixtures.Chapter(RunFixtures.Elite(4, true), RunFixtures.Boss());

            Assert.Throws<ArgumentException>(() => new ChapterRunSession(
                noDoorBefore, RunFixtures.Tuning(), RunFixtures.Party(4), runSeed: 1));
            Assert.Throws<ArgumentException>(() => new ChapterRunSession(
                eliteFirst, RunFixtures.Tuning(), RunFixtures.Party(4), runSeed: 1),
                "첫 방이 정예 자리면 앞에 걸 문 자체가 없다");
        }
    }
}
