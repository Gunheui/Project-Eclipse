using System;
using Eclipse.Data.Enums;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 재화 증감 정책을 전담하는 <see cref="ICurrencyService"/> 구현. 잔액은 <see cref="CurrencyWallet"/>가 보관하고,
    /// 지급·소비 규칙(음수 거부, 부족 시 원자적 실패)은 여기서 강제한다.
    /// </summary>
    public sealed class CurrencyService : ICurrencyService
    {
        private readonly CurrencyWallet _wallet;

        public CurrencyService(CurrencyWallet wallet)
        {
            _wallet = wallet;
        }

        /// <inheritdoc/>
        public void Grant(CurrencyType type, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "지급 금액은 음수일 수 없다.");
            _wallet.Add(type, amount);
        }

        /// <inheritdoc/>
        public bool TrySpend(CurrencyType type, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "소비 금액은 음수일 수 없다.");
            if (!CanAfford(type, amount))
                return false;
            _wallet.Add(type, -amount);
            return true;
        }

        /// <inheritdoc/>
        public bool CanAfford(CurrencyType type, int amount) => CurrentValue(type) >= amount;

        private int CurrentValue(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Essence: return _wallet.Essence.CurrentValue;
                case CurrencyType.Gold: return _wallet.Gold.CurrentValue;
                case CurrencyType.Manual: return _wallet.Manual.CurrentValue;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
