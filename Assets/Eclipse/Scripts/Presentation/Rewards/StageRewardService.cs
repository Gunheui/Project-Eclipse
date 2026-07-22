using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 스테이지 에셋의 보상 테이블을 읽어 지갑에 지급하는 <see cref="IRewardService"/> 구현. 확률 없는 결정적 지급.
    /// 반환 목록은 스테이지 배열을 alias 하지 않는 새 목록이라 팝업 표시 중 에셋이 바뀌어도 영향받지 않는다.
    /// </summary>
    public sealed class StageRewardService : IRewardService
    {
        private readonly CurrencyWallet _wallet;

        public StageRewardService(CurrencyWallet wallet)
        {
            _wallet = wallet;
        }

        /// <inheritdoc/>
        public IReadOnlyList<RewardEntry> GrantVictory(StageSO stage, bool firstClear)
        {
            if (stage == null) return Array.Empty<RewardEntry>();

            var granted = Sum(Entries(stage, firstClear));
            foreach (var reward in granted)
                _wallet.Grant(reward.type, reward.amount);

            return granted;
        }

        // 이번 승리에 해당하는 보상 원본. 최초 클리어면 두 배열이 이어져 같은 재화가 중복 등장할 수 있다.
        private static IEnumerable<RewardEntry> Entries(StageSO stage, bool firstClear)
        {
            IEnumerable<RewardEntry> entries = stage.clearRewards ?? Array.Empty<RewardEntry>();
            if (firstClear && stage.firstClearRewards != null)
                entries = entries.Concat(stage.firstClearRewards);
            return entries;
        }

        // 재화별 1건으로 합산한다. 결과 팝업의 칩이 재화 종류와 1:1이라 같은 재화가 두 건이면 표시가 깨진다.
        private static IReadOnlyList<RewardEntry> Sum(IEnumerable<RewardEntry> entries) => entries
            .Where(e => e.amount > 0)
            .GroupBy(e => e.type)
            .Select(g => new RewardEntry { type = g.Key, amount = g.Sum(e => e.amount) })
            .ToList();
    }
}
