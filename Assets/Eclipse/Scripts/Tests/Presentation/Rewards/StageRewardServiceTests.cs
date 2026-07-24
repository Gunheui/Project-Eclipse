using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class StageRewardServiceTests
    {
        [Test]
        public void 초회_클리어는_기본과_초회_보상을_함께_지급한다()
        {
            var wallet = new CurrencyWallet();
            int goldBefore = wallet.Gold.CurrentValue;
            int essenceBefore = wallet.Essence.CurrentValue;
            var stage = Stage(
                clear: new[] { Reward(CurrencyType.Gold, 200) },
                first: new[] { Reward(CurrencyType.Essence, 200) });

            var granted = new StageRewardService(new CurrencyService(wallet)).GrantVictory(stage, firstClear: true);

            Assert.That(granted.Count, Is.EqualTo(2));
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(goldBefore + 200));
            Assert.That(wallet.Essence.CurrentValue, Is.EqualTo(essenceBefore + 200));
        }

        [Test]
        public void 반복_클리어는_기본_보상만_지급한다()
        {
            var wallet = new CurrencyWallet();
            int essenceBefore = wallet.Essence.CurrentValue;
            var stage = Stage(
                clear: new[] { Reward(CurrencyType.Gold, 200) },
                first: new[] { Reward(CurrencyType.Essence, 200) });

            var granted = new StageRewardService(new CurrencyService(wallet)).GrantVictory(stage, firstClear: false);

            Assert.That(granted.Count, Is.EqualTo(1));
            Assert.That(granted[0].type, Is.EqualTo(CurrencyType.Gold));
            Assert.That(wallet.Essence.CurrentValue, Is.EqualTo(essenceBefore));
        }

        // stage_05는 기본 금화 1,000 + 초회 금화 1,000으로 같은 재화가 두 배열에 모두 있다.
        // 결과 팝업의 칩은 재화당 하나뿐이라 합산되지 않으면 표시가 깨진다.
        [Test]
        public void 같은_재화가_두_배열에_있으면_한_건으로_합산된다()
        {
            var wallet = new CurrencyWallet();
            int goldBefore = wallet.Gold.CurrentValue;
            var stage = Stage(
                clear: new[] { Reward(CurrencyType.Gold, 1000) },
                first: new[] { Reward(CurrencyType.Gold, 1000), Reward(CurrencyType.Essence, 1200) });

            var granted = new StageRewardService(new CurrencyService(wallet)).GrantVictory(stage, firstClear: true);

            Assert.That(granted.Count, Is.EqualTo(2));
            var gold = granted[0];
            Assert.That(gold.type, Is.EqualTo(CurrencyType.Gold));
            Assert.That(gold.amount, Is.EqualTo(2000));
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(goldBefore + 2000));
        }

        [Test]
        public void 보상이_비어도_지급없이_빈_목록을_돌려준다()
        {
            var wallet = new CurrencyWallet();
            int goldBefore = wallet.Gold.CurrentValue;
            var service = new StageRewardService(new CurrencyService(wallet));

            Assert.That(service.GrantVictory(Stage(null, null), firstClear: true), Is.Empty);
            Assert.That(service.GrantVictory(null, firstClear: true), Is.Empty);
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(goldBefore));
        }

        private static RewardEntry Reward(CurrencyType type, int amount)
            => new RewardEntry { type = type, amount = amount };

        private static StageSO Stage(RewardEntry[] clear, RewardEntry[] first)
        {
            var stage = ScriptableObject.CreateInstance<StageSO>();
            stage.clearRewards = clear;
            stage.firstClearRewards = first;
            return stage;
        }
    }
}
