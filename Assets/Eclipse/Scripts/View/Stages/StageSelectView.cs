using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 스테이지 선택 화면. StageSelectViewModel의 스테이지 목록으로 셀을 생성하고,
    /// 셀을 탭하면 곧바로 전투 씬으로 진입한다(별도 편성 단계 없음).
    /// </summary>
    public class StageSelectView : MonoBehaviour, IScreen
    {
        [SerializeField] private StageCellView cellPrefab;
        [SerializeField] private Transform contentRoot;

        [Header("내비")]
        [SerializeField] private Button backButton;

        private StageSelectViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly List<StageCellView> _cells = new List<StageCellView>();

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">표시할 스테이지 목록·전투 진입을 담당하는 ViewModel(컨테이너가 주입).</param>
        /// <param name="screenManager">뒤로가기 시 로비로 되돌리는 데 사용한다.</param>
        [Inject]
        public void Construct(StageSelectViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때(주입 완료 후) 호출된다. 스테이지마다 셀을 생성해 붙인다.
        /// </summary>
        /// <returns>등장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            foreach (var stage in _viewModel.Stages)
                AddCell(stage);

            backButton.onClick.AddListener(OnBack);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 버튼 리스너를 해지하고 생성한 셀을 모두 파괴한다.
        /// </summary>
        /// <returns>퇴장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnExit()
        {
            backButton.onClick.RemoveListener(OnBack);

            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>셀 프리팹을 하나 생성해 contentRoot 끝에 붙이고, 탭 시 전투로 진입하도록 바인딩한다.</summary>
        /// <param name="stage">새 셀이 표시할 스테이지 데이터.</param>
        private void AddCell(StageSO stage)
        {
            var cell = Instantiate(cellPrefab, contentRoot);
            cell.Bind(stage, _viewModel.EnterBattle);
            _cells.Add(cell);
        }

        private void OnBack() => _screenManager.Pop().Forget();
    }
}
