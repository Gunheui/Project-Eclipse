using Eclipse.Data;
using Eclipse.Data.Enums;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 스테이지 선택 목록의 항목 하나에 대응하는 경량 ViewModel. 스테이지 정의·번호는 고정이고,
    /// 3상태(클리어/열림/잠김)는 장의 클리어 수에서 파생해 리액티브로 노출한다.
    /// 소유자(<see cref="StageSelectViewModel"/>)가 장 아이템을 만들 때 생성하고 <see cref="Dispose"/>한다.
    /// </summary>
    public sealed class StageSelectItemViewModel
    {
        /// <param name="stageNumber">1-기반 스테이지 번호. 잠금 계산은 번호−1 인덱스를 사용한다.</param>
        public StageSelectItemViewModel(StageSO stage, int stageNumber, ReadOnlyReactiveProperty<int> clearedCount)
        {
            Stage = stage;
            StageNumber = stageNumber;

            int index = stageNumber - 1;
            State = clearedCount
                .Select(c => StageProgress.StateOf(index, c))
                .ToReadOnlyReactiveProperty(StageProgress.StateOf(index, clearedCount.CurrentValue));
        }

        /// <summary> 이 항목이 표시할 스테이지 정의. 항목 수명 내내 불변. </summary>
        public StageSO Stage { get; }

        /// <summary> 1-기반 스테이지 번호(뱃지 표기 "01" 등). 항목 수명 내내 불변. </summary>
        public int StageNumber { get; }

        /// <summary> 항목의 3상태. 장 진행도가 바뀌면 갱신된다. Locked면 선택 불가·잠금 오버레이 표시. </summary>
        public ReadOnlyReactiveProperty<StageState> State { get; }

        /// <summary> 파생 상태의 구독을 해지한다. 소유자(StageSelectViewModel)가 호출한다. </summary>
        public void Dispose()
        {
            State.Dispose();
        }
    }
}
