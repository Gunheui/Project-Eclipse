using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 스테이지 선택 화면. 현재 장의 아이템으로 셀을 데이터 구동 생성하고, 잠기지 않은 셀을 탭하면
    /// 곧바로 전투 씬으로 진입한다. 장 정보 패널·장 내비 라벨은 선택 장에 바인딩한다.
    /// </summary>
    public class StageSelectView : MonoBehaviour, IScreen
    {
        [SerializeField] private StageCellView cellPrefab;
        [SerializeField] private Transform contentRoot;

        [Header("장 정보 패널")]
        [Tooltip("장 번호 표기(\"01\" 형식).")]
        [SerializeField] private TMP_Text chapterNumberText;
        [SerializeField] private TMP_Text chapterTitleText;
        [SerializeField] private TMP_Text chapterDescText;

        [Header("장 내비")]
        [Tooltip("장 라벨(\"1장\" 형식).")]
        [SerializeField] private TMP_Text chapterNavLabel;
        [SerializeField] private Button prevChapterButton;
        [SerializeField] private Button nextChapterButton;

        [Header("내비")]
        [SerializeField] private Button backButton;

        private StageSelectViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly List<StageCellView> _cells = new List<StageCellView>();
        private readonly CompositeDisposable _bindings = new CompositeDisposable();

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">현재 장 아이템·장 전환·전투 진입을 담당하는 ViewModel(컨테이너가 주입).</param>
        /// <param name="screenManager">뒤로가기 시 로비로 되돌리는 데 사용한다.</param>
        [Inject]
        public void Construct(StageSelectViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때(주입 완료 후) 호출된다. 현재 장 아이템마다 셀을 생성하고,
        /// 장 정보 패널·내비 라벨·내비 버튼 가용성을 선택 장에 바인딩한다.
        /// </summary>
        /// <returns>등장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            foreach (var item in _viewModel.Items)
                AddCell(item);

            _viewModel.SelectedChapter
                .Subscribe(BindChapterInfo)
                .AddTo(_bindings);

            if (prevChapterButton != null)
            {
                prevChapterButton.onClick.AddListener(OnPrevChapter);
                _viewModel.CanSelectPrevChapter
                    .Subscribe(can => prevChapterButton.interactable = can)
                    .AddTo(_bindings);
            }
            if (nextChapterButton != null)
            {
                nextChapterButton.onClick.AddListener(OnNextChapter);
                _viewModel.CanSelectNextChapter
                    .Subscribe(can => nextChapterButton.interactable = can)
                    .AddTo(_bindings);
            }

            backButton.onClick.AddListener(OnBack);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 버튼 리스너·바인딩을 해지하고 생성한 셀을 모두 파괴한다.
        /// </summary>
        /// <returns>퇴장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnExit()
        {
            _bindings.Clear();
            backButton.onClick.RemoveListener(OnBack);
            if (prevChapterButton != null)
                prevChapterButton.onClick.RemoveListener(OnPrevChapter);
            if (nextChapterButton != null)
                nextChapterButton.onClick.RemoveListener(OnNextChapter);

            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();
            return UniTask.CompletedTask;
        }

        // 셀 프리팹을 하나 생성해 contentRoot 끝(장 설명 패널 뒤)에 붙이고, 탭 시 선택/편성 진입하도록 바인딩한다.
        private void AddCell(StageSelectItemViewModel item)
        {
            var cell = Instantiate(cellPrefab, contentRoot);
            cell.Bind(item, OnStageSelected);
            _cells.Add(cell);
        }

        // 스테이지를 탭하면 선택으로 기록하고(잠김이면 무시), 기록됐을 때만 편성 화면을 전면에 올린다.
        private void OnStageSelected(StageSelectItemViewModel item)
        {
            if (_viewModel.Select(item))
                _screenManager.Push(ScreenId.PartyFormation).Forget();
        }

        // 장 정보 패널·내비 라벨을 선택 장 값으로 채운다. 번호는 "01"(D2), 내비는 "N장" 표기.
        private void BindChapterInfo(ChapterSO chapter)
        {
            if (chapter == null)
                return;
            if (chapterNumberText != null)
                chapterNumberText.text = chapter.number.ToString("D2");
            if (chapterTitleText != null)
                chapterTitleText.text = chapter.displayName;
            if (chapterDescText != null)
                chapterDescText.text = chapter.description;
            if (chapterNavLabel != null)
                chapterNavLabel.text = $"{chapter.number}장";
        }

        private void OnPrevChapter() => _viewModel.SelectPrevChapter();
        private void OnNextChapter() => _viewModel.SelectNextChapter();
        private void OnBack() => _screenManager.Pop().Forget();
    }
}
