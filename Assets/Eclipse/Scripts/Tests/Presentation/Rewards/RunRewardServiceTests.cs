using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class RunRewardServiceTests
    {
        private static RewardEntry Reward(CurrencyType type, int amount)
            => new RewardEntry { type = type, amount = amount };

        [Test]
        public void 보상을_지갑에_지급하고_영수증을_돌려준다()
        {
            var wallet = new CurrencyWallet();
            int goldBefore = wallet.Gold.CurrentValue;
            int essenceBefore = wallet.Essence.CurrentValue;

            var granted = new RunRewardService(new CurrencyService(wallet))
                .Grant(new[] { Reward(CurrencyType.Gold, 200), Reward(CurrencyType.Essence, 60) });

            Assert.That(granted.Count, Is.EqualTo(2));
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(goldBefore + 200));
            Assert.That(wallet.Essence.CurrentValue, Is.EqualTo(essenceBefore + 60));
        }

        [Test]
        public void 같은_재화는_한_건으로_합산된다()
        {
            // 결과 팝업의 칩이 재화당 하나라 합산되지 않으면 표시가 깨진다.
            var wallet = new CurrencyWallet();
            int goldBefore = wallet.Gold.CurrentValue;

            var granted = new RunRewardService(new CurrencyService(wallet))
                .Grant(new[] { Reward(CurrencyType.Gold, 1000), Reward(CurrencyType.Gold, 400) });

            Assert.That(granted.Count, Is.EqualTo(1));
            Assert.That(granted[0].amount, Is.EqualTo(1400));
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(goldBefore + 1400));
        }

        [Test]
        public void 영이하_보상은_지급과_영수증_모두에서_제외된다()
        {
            var wallet = new CurrencyWallet();
            int goldBefore = wallet.Gold.CurrentValue;

            var granted = new RunRewardService(new CurrencyService(wallet))
                .Grant(new[] { Reward(CurrencyType.Gold, 0), Reward(CurrencyType.Manual, -3) });

            Assert.That(granted, Is.Empty);
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(goldBefore));
        }
    }
}
