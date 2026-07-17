using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Service;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 스테이지 선택 화면의 상태. 현재 장의 스테이지 아이템을 데이터로 노출하고, 잠기지 않은 셀을 탭하면
    /// 전투 씬으로 진입시킨다. 장 전환기는 배선돼 있으나 현재 콘텐츠가 1장이라 실질 no-op다.
    /// </summary>
    public sealed class StageSelectViewModel : ViewModelBase
    {
        private readonly StageProgress _progress;
        private readonly ISceneFlow _sceneFlow;
        private readonly NavigationContext _nav;
        private bool _entering;

        /// <summary> 전체 장 목록. 순서·내용 고정(런타임 변경 없음). </summary>
        public IReadOnlyList<ChapterSO> Chapters { get; }

        /// <summary> 현재 보고 있는 장. 초기값은 첫 장. 장 전환기로만 바뀐다. </summary>
        public ReactiveProperty<ChapterSO> SelectedChapter { get; }

        /// <summary> 현재 장의 셀 아이템 목록. 장 수명 내내 고정(1장 전제). </summary>
        public IReadOnlyList<StageSelectItemViewModel> Items { get; }

        /// <summary> 마지막으로 선택한 스테이지. 장 전환 시 초기화된다. </summary>
        public ReactiveProperty<StageSelectItemViewModel> SelectedStage { get; }

        /// <summary> 이전 장으로 갈 수 있는지(내비 ◀ 버튼 가용성). 첫 장에서 false. </summary>
        public ReadOnlyReactiveProperty<bool> CanSelectPrevChapter { get; }

        /// <summary> 다음 장으로 갈 수 있는지(내비 ▶ 버튼 가용성). 마지막 장에서 false. </summary>
        public ReadOnlyReactiveProperty<bool> CanSelectNextChapter { get; }

        /// <param name="chapters">표시할 장 목록. 첫 항목이 초기 선택 장.</param>
        /// <param name="progress">장별 진행/해금 상태(셀 3상태의 원천).</param>
        /// <param name="sceneFlow">셀 탭 시 전투 씬으로 전환하는 창구.</param>
        /// <param name="nav">씬 경계 선택 보관함. 진입 직전 선택 스테이지를 여기 기록해 전투 스코프가 적 편성을 읽는다.</param>
        public StageSelectViewModel(ChapterSO[] chapters, StageProgress progress, ISceneFlow sceneFlow, NavigationContext nav)
        {
            Chapters = chapters;
            _progress = progress;
            _sceneFlow = sceneFlow;
            _nav = nav;

            var first = chapters[0];
            SelectedChapter = new ReactiveProperty<ChapterSO>(first);
            SelectedStage = new ReactiveProperty<StageSelectItemViewModel>(null);
            Items = BuildItems(first);

            CanSelectPrevChapter = SelectedChapter
                .Select(c => IndexOf(c) > 0)
                .ToReadOnlyReactiveProperty(false);
            CanSelectNextChapter = SelectedChapter
                .Select(c => IndexOf(c) < Chapters.Count - 1)
                .ToReadOnlyReactiveProperty(IndexOf(first) < Chapters.Count - 1);
        }

        /// <summary>
        /// 스테이지 아이템을 골라 전투 씬으로 진입한다. 잠긴(Locked) 아이템은 무시하며(선택 차단의 권위),
        /// 씬 로드 중복 진입도 한 번만 막는다. 진입 전 선택 상태를 갱신한다.
        /// </summary>
        /// <param name="item">탭된 스테이지 아이템. State가 Locked면 아무 일도 하지 않는다.</param>
        public void Select(StageSelectItemViewModel item)
        {
            if (item == null || item.State.CurrentValue == StageState.Locked)
                return;

            SelectedStage.Value = item;

            if (_entering)
                return;
            _entering = true;
            _nav.SelectedStage = item.Stage;
            _sceneFlow.ToBattleAsync().Forget();
        }

        /// <summary>이전 장으로 전환한다. 첫 장이면 아무 일도 하지 않는다.</summary>
        public void SelectPrevChapter()
        {
            int i = IndexOf(SelectedChapter.Value);
            if (i <= 0)
                return;
            SwitchChapter(Chapters[i - 1]);
        }

        /// <summary>다음 장으로 전환한다. 마지막 장이면 아무 일도 하지 않는다.</summary>
        public void SelectNextChapter()
        {
            int i = IndexOf(SelectedChapter.Value);
            if (i < 0 || i >= Chapters.Count - 1)
                return;
            SwitchChapter(Chapters[i + 1]);
        }

        // 장을 바꾸고 선택 스테이지를 초기화한다. (현재 1장이라 도달하지 않지만 다장 확장 대비.)
        private void SwitchChapter(ChapterSO chapter)
        {
            SelectedStage.Value = null;
            SelectedChapter.Value = chapter;
        }

        // 지정 장의 스테이지들로 셀 아이템을 만든다. 인덱스+1이 스테이지 번호.
        private IReadOnlyList<StageSelectItemViewModel> BuildItems(ChapterSO chapter)
        {
            var clearedCount = _progress.ClearedCountOf(chapter.id);
            var items = new List<StageSelectItemViewModel>(chapter.stages.Length);
            for (int i = 0; i < chapter.stages.Length; i++)
                items.Add(new StageSelectItemViewModel(chapter.stages[i], i + 1, clearedCount));
            return items;
        }

        private int IndexOf(ChapterSO chapter)
        {
            for (int i = 0; i < Chapters.Count; i++)
                if (Chapters[i] == chapter)
                    return i;
            return -1;
        }

        /// <summary>보유한 리액티브 프로퍼티와 셀 아이템을 모두 해제한다.</summary>
        protected override void OnDispose()
        {
            foreach (var item in Items)
                item.Dispose();
            SelectedChapter.Dispose();
            SelectedStage.Dispose();
            CanSelectPrevChapter.Dispose();
            CanSelectNextChapter.Dispose();
        }
    }
}
