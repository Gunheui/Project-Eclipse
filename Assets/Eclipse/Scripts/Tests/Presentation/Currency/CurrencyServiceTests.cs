using System;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using NUnit.Framework;
using R3;

namespace Eclipse.Tests
{
    public class CurrencyServiceTests
    {
        [Test]
        public void Grant는_지정_재화를_더한다()
        {
            var wallet = new CurrencyWallet();
            int before = wallet.Gold.CurrentValue;

            new CurrencyService(wallet).Grant(CurrencyType.Gold, 500);

            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(before + 500));
            wallet.Dispose();
        }

        [Test]
        public void TrySpend는_잔액이_충분하면_정확히_차감하고_true를_반환한다()
        {
            var wallet = new CurrencyWallet();
            int before = wallet.Gold.CurrentValue;

            bool ok = new CurrencyService(wallet).TrySpend(CurrencyType.Gold, 300);

            Assert.That(ok, Is.True);
            Assert.That(wallet.Gold.CurrentValue, Is.EqualTo(before - 300));
            wallet.Dispose();
        }

        [Test]
        public void TrySpend는_잔액이_부족하면_무변경으로_false를_반환한다()
        {
            var wallet = new CurrencyWallet();
            int before = wallet.Manual.CurrentValue; // 신규 계정 Manual = 0

            bool ok = new CurrencyService(wallet).TrySpend(CurrencyType.Manual, 1);

            Assert.That(ok, Is.False);
            Assert.That(wallet.Manual.CurrentValue, Is.EqualTo(before));
            wallet.Dispose();
        }

        [Test]
        public void 증감은_구독자에게_리액티브로_전파된다()
        {
            var wallet = new CurrencyWallet();
            var service = new CurrencyService(wallet);
            int observed = -1;
            using var sub = wallet.Gold.Subscribe(v => observed = v);

            service.Grant(CurrencyType.Gold, 100);
            int afterGrant = observed;
            service.TrySpend(CurrencyType.Gold, 100);

            Assert.That(afterGrant, Is.EqualTo(wallet.Gold.CurrentValue + 100));
            Assert.That(observed, Is.EqualTo(wallet.Gold.CurrentValue));
            wallet.Dispose();
        }

        [Test]
        public void 음수_금액은_예외를_던진다()
        {
            var wallet = new CurrencyWallet();
            var service = new CurrencyService(wallet);

            Assert.Throws<ArgumentOutOfRangeException>(() => service.Grant(CurrencyType.Gold, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.TrySpend(CurrencyType.Gold, -1));
            wallet.Dispose();
        }
    }
}
