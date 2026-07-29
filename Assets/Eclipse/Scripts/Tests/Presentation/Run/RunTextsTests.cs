using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class RunTextsTests
    {
        private static BuffCard Card(CardGrade grade, params StatDelta[] deltas)
            => new() { id = "c", displayName = "카드", grade = grade, deltas = deltas };

        [Test]
        public void 범용_카드는_증감_한_줄로_적힌다()
        {
            var card = Card(CardGrade.Rare, new StatDelta { axis = StatType.Atk, value = 0.15f });

            Assert.AreEqual("공격력 +15%", RunTexts.FormatCard(card));
        }

        [Test]
        public void 저주_카드는_음수_부호를_그대로_보인다()
        {
            var card = Card(CardGrade.Common, new StatDelta { axis = StatType.Def, value = -0.12f });
            card.targetsEnemies = true;

            // 대상 표기는 CardOption.Target이 따로 싣는다 — 효과 줄에 접두를 붙이지 않는다.
            Assert.AreEqual("방어력 -12%", RunTexts.FormatCard(card));
        }

        [Test]
        public void 치명_계열만_퍼센트포인트_단위로_적힌다()
        {
            Assert.AreEqual("치명확률 +6%p",
                RunTexts.FormatDelta(new StatDelta { axis = StatType.CritRate, value = 0.06f }));
            Assert.AreEqual("속도 +6%",
                RunTexts.FormatDelta(new StatDelta { axis = StatType.Spd, value = 0.06f }));
        }

        [Test]
        public void 유니크_카드는_적어_둔_설명을_그대로_쓴다()
        {
            var card = Card(CardGrade.Unique, new StatDelta { axis = StatType.Atk, value = 0.25f });
            card.description = "쾌속 베기가 2타가 된다";

            Assert.AreEqual("쾌속 베기가 2타가 된다", RunTexts.FormatCard(card));
        }

        [Test]
        public void 정산_행은_재화_세_종을_고정_순서로_적는다()
        {
            var entries = new[]
            {
                new RewardEntry { type = CurrencyType.Essence, amount = 240 },
                new RewardEntry { type = CurrencyType.Gold, amount = 1850 },
            };

            // pos 태그가 열 위치다 — 행마다 같은 자리에 서야 위아래로 비교된다.
            Assert.AreEqual("<pos=0%>골드 1,850<pos=37%>교본 0<pos=74%>보석 240",
                RunTexts.FormatRewards(entries));
        }

        [Test]
        public void 지급분이_없어도_세_재화가_0으로_남는다()
        {
            Assert.AreEqual("<pos=0%>골드 0<pos=37%>교본 0<pos=74%>보석 0",
                RunTexts.FormatRewards(null));
        }

        [Test]
        public void 등급_라벨은_네_등급_모두_한글이다()
        {
            Assert.AreEqual("커먼", RunTexts.GradeLabel(CardGrade.Common));
            Assert.AreEqual("레어", RunTexts.GradeLabel(CardGrade.Rare));
            Assert.AreEqual("에픽", RunTexts.GradeLabel(CardGrade.Epic));
            Assert.AreEqual("유니크", RunTexts.GradeLabel(CardGrade.Unique));
        }
    }
}
