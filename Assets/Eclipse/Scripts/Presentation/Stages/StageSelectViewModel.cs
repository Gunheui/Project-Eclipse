using System.Collections.Generic;
using Eclipse.Data;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 스테이지 선택 화면의 상태. 고정된 스테이지 목록을 노출하고, 현재 선택된 스테이지 하나를
    /// 관측 가능한 값으로 흘린다. View가 이 값을 구독해 셀 강조·편성 버튼 활성을 갱신한다.
    /// </summary>
    public sealed class StageSelectViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<StageSO> _selectedStage = new ReactiveProperty<StageSO>(null);

        /// <summary> 화면에 표시할 스테이지 목록. 순서·내용 고정(런타임 변경 없음). </summary>
        public IReadOnlyList<StageSO> Stages { get; }

        /// <summary> 현재 선택된 스테이지. 초기값 null(미선택). View가 구독해 강조·버튼 활성을 정한다. </summary>
        public ReadOnlyReactiveProperty<StageSO> SelectedStage => _selectedStage;

        public StageSelectViewModel(StageSO[] stages)
        {
            Stages = stages;
        }

        /// <summary>
        /// 스테이지를 선택으로 기록한다. <see cref="SelectedStage"/> 구독자에게 즉시 전파된다.
        /// </summary>
        /// <param name="stage">선택할 스테이지. <see cref="Stages"/>에 속한 항목을 전제로 한다.</param>
        public void Select(StageSO stage)
        {
            _selectedStage.Value = stage;
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _selectedStage.Dispose();
        }
    }
}
