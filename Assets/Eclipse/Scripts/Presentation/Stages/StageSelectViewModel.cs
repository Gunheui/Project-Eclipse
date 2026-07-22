using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 스테이지 선택 화면의 상태. 현재 장의 스테이지 아이템을 데이터로 노출하고, 잠기지 않은 항목을 탭하면
    /// 그 스테이지를 선택으로 기록한다(화면 전환은 View가 편성 화면 Push로 처리).
    /// </summary>
    public sealed class StageSelectViewModel : ViewModelBase
    {
        private readonly StageProgress _progress;
        private readonly NavigationContext _nav;

        /// <summary> 현재 보고 있는 장. 콘텐츠가 1장이라 화면 수명 내내 고정이다. </summary>
        public ChapterSO SelectedChapter { get; }

        /// <summary> 현재 장의 항목 목록. 장 수명 내내 고정. </summary>
        public IReadOnlyList<StageSelectItemViewModel> Items { get; }

        /// <summary> 마지막으로 선택한 스테이지. </summary>
        public ReactiveProperty<StageSelectItemViewModel> SelectedStage { get; }

        /// <param name="chapters">표시할 장 목록. 첫 항목이 표시 장이 된다.</param>
        /// <param name="progress">장별 진행/해금 상태(항목 3상태의 원천).</param>
        /// <param name="nav">씬 경계 선택 보관함. 선택 스테이지를 여기 기록해 편성·전투 스코프가 읽는다.</param>
        public StageSelectViewModel(ChapterSO[] chapters, StageProgress progress, NavigationContext nav)
        {
            _progress = progress;
            _nav = nav;

            SelectedChapter = chapters[0];
            SelectedStage = new ReactiveProperty<StageSelectItemViewModel>(null);
            Items = BuildItems(SelectedChapter);
        }

        /// <summary>
        /// 스테이지 아이템을 선택으로 기록한다. 잠긴(Locked) 아이템은 무시하며(선택 차단의 권위) false를 돌려준다.
        /// 새 편성을 시작하므로 이전에 확정했다 버린 파티(SelectedParty)를 함께 클리어한다.
        /// 화면 전환(편성 화면 Push)은 이 반환값을 보고 View가 처리한다.
        /// </summary>
        /// <param name="item">탭된 스테이지 아이템. State가 Locked거나 null이면 아무 일도 하지 않는다.</param>
        /// <returns>선택이 기록됐으면 true(편성 화면으로 진행 가능), 잠김/null이면 false.</returns>
        public bool Select(StageSelectItemViewModel item)
        {
            if (item == null || item.State.CurrentValue == StageState.Locked)
                return false;

            SelectedStage.Value = item;
            _nav.SelectedStage = item.Stage;
            _nav.SelectedChapter = SelectedChapter;
            _nav.SelectedParty = null;
            return true;
        }

        // 지정 장의 스테이지들로 항목을 만든다. 인덱스+1이 스테이지 번호.
        private IReadOnlyList<StageSelectItemViewModel> BuildItems(ChapterSO chapter)
        {
            var clearedCount = _progress.ClearedCountOf(chapter);
            var items = new List<StageSelectItemViewModel>(chapter.stages.Length);
            for (int i = 0; i < chapter.stages.Length; i++)
                items.Add(new StageSelectItemViewModel(chapter.stages[i], i + 1, clearedCount));
            return items;
        }

        /// <summary>보유한 리액티브 프로퍼티와 항목을 모두 해제한다.</summary>
        protected override void OnDispose()
        {
            foreach (var item in Items)
                item.Dispose();
            SelectedStage.Dispose();
        }
    }
}
