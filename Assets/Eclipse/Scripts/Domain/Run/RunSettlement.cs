using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 도달 깊이 정산액 계산. 순수 표 조회라 난수가 없고 항상 결정적이다.
    /// 지급은 하지 않는다 — 지갑 접촉은 IRewardService 한 곳이다.
    /// </summary>
    public static class RunSettlement
    {
        /// <summary> 정산 보상 목록(재화 3종). 승리면 승리 보너스를 더한다. </summary>
        /// <param name="roomsCleared">넘긴 방 수. 0 이상 정산 표 마지막 행 이하.</param>
        public static IReadOnlyList<RewardEntry> EntriesFor(ChapterSO chapter, int roomsCleared, bool victory)
        {
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));
            if (roomsCleared < 0 || roomsCleared >= chapter.settlement.Length)
                throw new ArgumentOutOfRangeException(nameof(roomsCleared), roomsCleared,
                    $"정산 표 범위(0~{chapter.settlement.Length - 1})를 벗어난다.");

            var row = chapter.settlement[roomsCleared];
            int gold = row.gold, manual = row.manual, essence = row.essence;
            if (victory)
            {
                gold += chapter.victoryBonus.gold;
                manual += chapter.victoryBonus.manual;
                essence += chapter.victoryBonus.essence;
            }
            return new[]
            {
                new RewardEntry { type = CurrencyType.Gold, amount = gold },
                new RewardEntry { type = CurrencyType.Manual, amount = manual },
                new RewardEntry { type = CurrencyType.Essence, amount = essence },
            };
        }
    }
}