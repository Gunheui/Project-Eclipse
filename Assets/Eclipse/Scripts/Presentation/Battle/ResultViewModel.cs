using System;
using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 전투 결과 팝업의 ViewModel. 팝업이 열리는 시점에 전투는 이미 끝나 있고 보상도 지급이 끝났으므로
    /// 값은 생성 즉시 고정된다(리액티브 프로퍼티 없음).
    /// </summary>
    public sealed class ResultViewModel
    {
        /// <param name="result">확정된 전투 결과. InProgress면 패배와 같게 표시되므로 종료 후에만 생성한다.</param>
        /// <param name="rewards">실제 지급된 보상. null이면 빈 목록으로 다룬다.</param>
        public ResultViewModel(BattleResult result, IReadOnlyList<RewardEntry> rewards)
        {
            IsVictory = result == BattleResult.Victory;
            Rewards = IsVictory && rewards != null ? rewards : Array.Empty<RewardEntry>();
        }

        /// <summary> 승리 여부. 배너 색·아이콘·문구가 이 값으로 갈린다. </summary>
        public bool IsVictory { get; }

        /// <summary> 이번 전투로 획득한 보상. 패배면 빈 목록이며 팝업은 "획득 보상 없음"을 대신 띄운다. </summary>
        public IReadOnlyList<RewardEntry> Rewards { get; }
    }
}
