using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary>
    /// 정산 분리 검증. 결과 화면이 도달 보상과 승리 보너스를 다른 행에 올리므로
    /// 두 값이 합쳐져 나오면 화면을 채울 수 없다.
    /// </summary>
    public class RunSettlementTests
    {
        // 정산 표 = 넘긴 방 수 × 100골드, 승리 보너스 = 400골드.
        private static ChapterSO Chapter()
            => RunFixtures.Chapter(RunFixtures.Normal(1, false), RunFixtures.Normal(2, false), RunFixtures.Boss());

        private static int Gold(IReadOnlyList<RewardEntry> entries)
            => entries.Single(e => e.type == CurrencyType.Gold).amount;

        [Test]
        public void 클리어는_도달_보상과_승리_보너스를_갈라서_낸다()
        {
            var entries = RunSettlement.EntriesFor(Chapter(), roomsCleared: 3, victory: true);

            Assert.AreEqual(300, Gold(entries.Depth), "도달 보상은 표 3행 그대로다");
            Assert.AreEqual(400, Gold(entries.VictoryBonus), "보너스가 도달 보상에 섞이지 않는다");
        }

        [Test]
        public void 실패는_승리_보너스가_비어_온다()
        {
            var entries = RunSettlement.EntriesFor(Chapter(), roomsCleared: 1, victory: false);

            Assert.AreEqual(100, Gold(entries.Depth));
            CollectionAssert.IsEmpty(entries.VictoryBonus);
        }

        [Test]
        public void 넘긴_방이_0이어도_재화_세_종이_다_들어_있다()
        {
            var entries = RunSettlement.EntriesFor(Chapter(), roomsCleared: 0, victory: false);

            Assert.AreEqual(3, entries.Depth.Count, "0 지급도 행을 지운 것이 아니다");
            Assert.IsTrue(entries.Depth.All(e => e.amount == 0));
        }

        [Test]
        public void 정산표_밖의_방_수는_예외다()
        {
            var chapter = Chapter();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => RunSettlement.EntriesFor(chapter, chapter.settlement.Length, victory: true));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RunSettlement.EntriesFor(chapter, -1, victory: true));
        }
    }
}
