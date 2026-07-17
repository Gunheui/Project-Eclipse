using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 스테이지 선택 화면. StageSelectViewModel의 스테이지 목록으로 셀을 생성하고,
    /// 선택 상태를 구독해 한 셀만 강조하며 편성 버튼 활성을 갱신한다.
    /// 구독은 화면이 보이는 동안(OnEnter~OnExit)만 유지한다.
    /// </summary>
    public class StageSelectView : MonoBehaviour, IScreen
    {
        [SerializeField] private StageCellView cellPrefab;
        [SerializeField] private Transform contentRoot;

        [Header("내비")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button formationButton;

        private StageSelectViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly List<StageCellView> _cells = new List<StageCellView>();
        private CompositeDisposable _subscriptions;

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">표시할 스테이지 목록 ViewModel(컨테이너가 주입).</param>
        /// <param name="screenManager">뒤로가기 시 로비로 되돌리는 데 사용한다.</param>
        [Inject]
        public void Construct(StageSelectViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때(주입 완료 후) 호출된다. 스테이지마다 셀을 생성하고,
        /// 선택 상태를 구독해 강조·편성 버튼 활성을 반영한다. 구독은 OnExit까지 유지된다.
        /// </summary>
        /// <returns>등장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            foreach (var stage in _viewModel.Stages)
                AddCell(stage);

            _subscriptions = new CompositeDisposable();

            // 선택이 바뀔 때마다 해당 셀만 강조하고, 선택이 생기면 편성 버튼을 연다.
            _viewModel.SelectedStage
                .Subscribe(ApplySelection)
                .AddTo(_subscriptions);

            backButton.onClick.AddListener(OnBack);
            formationButton.onClick.AddListener(OnFormation);

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 선택 구독과 버튼 리스너를 해지하고 생성한 셀을 모두 파괴한다.
        /// </summary>
        /// <returns>퇴장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnExit()
        {
            _subscriptions?.Dispose();
            backButton.onClick.RemoveListener(OnBack);
            formationButton.onClick.RemoveListener(OnFormation);

            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>셀 프리팹을 하나 생성해 contentRoot 끝에 붙이고 스테이지에 바인딩한다.</summary>
        /// <param name="stage">새 셀이 표시할 스테이지 데이터.</param>
        private void AddCell(StageSO stage)
        {
            var cell = Instantiate(cellPrefab, contentRoot);
            cell.Bind(stage, _viewModel.Select);
            _cells.Add(cell);
        }

        /// <summary>선택된 스테이지의 셀만 강조하고, 선택 유무로 편성 버튼 활성을 정한다.</summary>
        /// <param name="selected">현재 선택된 스테이지(미선택이면 null).</param>
        private void ApplySelection(StageSO selected)
        {
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].SetSelected(_viewModel.Stages[i] == selected);

            formationButton.interactable = selected != null;
        }

        private void OnBack() => _screenManager.Pop().Forget();

        // TODO: S11 편성 화면이 생기면 Push(ScreenId.Formation)로 교체한다.
        private void OnFormation() { }
    }
}
