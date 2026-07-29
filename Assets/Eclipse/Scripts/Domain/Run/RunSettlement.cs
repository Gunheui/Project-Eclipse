using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary> 정산 결과 두 갈래. 결과 화면이 각각 다른 행에 올리므로 합쳐서 넘기지 않는다. </summary>
    public readonly struct SettlementEntries
    {
        public SettlementEntries(IReadOnlyList<RewardEntry> depth, IReadOnlyList<RewardEntry> victoryBonus)
        {
            Depth = depth;
            VictoryBonus = victoryBonus;
        }

        /// <summary> 넘긴 방 수로 받는 도달 보상. 0방이어도 재화 3종이 다 들어 있다. </summary>
        public IReadOnlyList<RewardEntry> Depth { get; }

        /// <summary> 클리어에만 붙는 승리 보너스. 실패면 빈 목록. </summary>
        public IReadOnlyList<RewardEntry> VictoryBonus { get; }
    }

    /// <summary>
    /// 도달 깊이 정산액 계산. 순수 표 조회라 난수가 없고 항상 결정적이다.
    /// 지급은 하지 않는다 — 지갑 접촉은 IRewardService 한 곳이다.
    /// </summary>
    public static class RunSettlement
    {
        /// <summary> 도달 보상과 승리 보너스를 갈라 계산한다. </summary>
        /// <param name="roomsCleared">넘긴 방 수. 0 이상 정산 표 마지막 행 이하.</param>
        public static SettlementEntries EntriesFor(ChapterSO chapter, int roomsCleared, bool victory)
        {
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));
            if (roomsCleared < 0 || roomsCleared >= chapter.settlement.Length)
                throw new ArgumentOutOfRangeException(nameof(roomsCleared), roomsCleared,
                    $"정산 표 범위(0~{chapter.settlement.Length - 1})를 벗어난다.");

            return new SettlementEntries(
                RowEntries(chapter.settlement[roomsCleared]),
                victory ? RowEntries(chapter.victoryBonus) : Array.Empty<RewardEntry>());
        }

        private static IReadOnlyList<RewardEntry> RowEntries(SettlementRow row) => new[]
        {
            new RewardEntry { type = CurrencyType.Gold, amount = row.gold },
            new RewardEntry { type = CurrencyType.Manual, amount = row.manual },
            new RewardEntry { type = CurrencyType.Essence, amount = row.essence },
        };
    }
}