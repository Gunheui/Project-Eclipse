using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 보상 목록을 지갑에 지급하는 <see cref="IRewardService"/> 구현. 문 보상·미드보스 보상·정산이
    /// 전부 이 단일 창구를 지난다.
    /// </summary>
    public sealed class RunRewardService : IRewardService
    {
        private readonly ICurrencyService _currency;

        public RunRewardService(ICurrencyService currency)
        {
            _currency = currency;
        }

        /// <inheritdoc/>
        public IReadOnlyList<RewardEntry> Grant(IEnumerable<RewardEntry> entries)
        {
            var granted = Sum(entries);
            foreach (var reward in granted)
                _currency.Grant(reward.type, reward.amount);
            return granted;
        }

        /// <summary> 재화별 1건으로 합산한다. 지급 없이 합계만 필요한 화면도 이 규칙을 그대로 쓴다. </summary>
        public static IReadOnlyList<RewardEntry> Sum(IEnumerable<RewardEntry> entries) => entries
            .Where(e => e.amount > 0)
            // 결과 팝업의 칩이 재화 종류와 1:1이라 같은 재화가 두 건이면 표시가 깨진다.
            .GroupBy(e => e.type)
            .Select(g => new RewardEntry { type = g.Key, amount = g.Sum(e => e.amount) })
            .ToList();
    }
}
