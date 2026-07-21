using System.Collections.Generic;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 전투 결과 팝업의 ViewModel. 팝업이 열리는 시점에 전투는 이미 끝나 있으므로 값은 생성 즉시 고정된다
    /// (리액티브 프로퍼티 없음). 보상은 보상 데이터 계층이 생기기 전까지 고정 더미다.
    /// </summary>
    public sealed class ResultViewModel
    {
        /// <summary> 결과 팝업에 한 줄로 표시되는 획득 보상 한 건. </summary>
        public readonly struct Reward
        {
            /// <param name="label">재화 표시명(예: "길드금화").</param>
            /// <param name="amount">획득 수량. 0 이상.</param>
            public Reward(string label, int amount)
            {
                Label = label;
                Amount = amount;
            }

            /// <summary> 재화 표시명. 칩 하단 이름 라벨에 그대로 들어간다. </summary>
            public string Label { get; }

            /// <summary> 획득 수량. 천 단위 구분은 표시 단계(View)에서 붙인다. </summary>
            public int Amount { get; }
        }

        // 보상 데이터 계층(RewardService/RewardSO)이 붙기 전까지 쓰는 고정 승리 보상.
        private static readonly Reward[] VictoryRewards =
        {
            new Reward("길드금화", 1200),
            new Reward("경험치", 320),
        };

        /// <param name="result">확정된 전투 결과. InProgress로 만들면 패배와 같게 표시되므로 종료 후에만 만든다.</param>
        public ResultViewModel(BattleResult result)
        {
            IsVictory = result == BattleResult.Victory;
            Rewards = IsVictory ? VictoryRewards : System.Array.Empty<Reward>();
        }

        /// <summary> 승리 여부. 배너 색·아이콘·문구가 이 값으로 갈린다. </summary>
        public bool IsVictory { get; }

        /// <summary> 이번 전투로 획득한 보상. 패배면 빈 목록이며 팝업은 "획득 보상 없음"을 대신 띄운다. </summary>
        public IReadOnlyList<Reward> Rewards { get; }
    }
}
