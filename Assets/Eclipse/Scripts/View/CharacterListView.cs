using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using ObservableCollections;
using R3;
using UnityEngine;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 캐릭터 목록 화면. CharacterListViewModel의 셀 목록을 구독해
    /// 셀 프리팹을 생성/제거한다. 구독은 화면이 보이는 동안(OnEnter~OnExit)만 유지한다.
    /// </summary>
    public class CharacterListView : MonoBehaviour, IScreen
    {
        [SerializeField] private CharacterCellView cellPrefab;
        [SerializeField] private Transform contentRoot;

        private CharacterListViewModel _viewModel;
        private readonly List<CharacterCellView> _cells = new List<CharacterCellView>();
        private CompositeDisposable _subscriptions;

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">표시할 캐릭터 목록 ViewModel(컨테이너가 주입).</param>
        [Inject]
        public void Construct(CharacterListViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때(주입 완료 후) 호출된다. 현재 목록 항목마다 셀을 생성하고,
        /// 이후의 항목 추가/제거를 구독한다. 구독은 OnExit까지 유지된다.
        /// </summary>
        /// <returns>등장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            foreach (var cellViewModel in _viewModel.CharacterList)
                AddCell(cellViewModel);

            _subscriptions = new CompositeDisposable();
            _viewModel.CharacterList.ObserveAdd()
                .Subscribe(e => AddCell(e.Value))
                .AddTo(_subscriptions);
            _viewModel.CharacterList.ObserveRemove()
                .Subscribe(e => RemoveCell(e.Index))
                .AddTo(_subscriptions);

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 목록 추가/제거 구독을 해지하고 생성한 셀을 모두 파괴한다.
        /// </summary>
        /// <returns>퇴장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnExit()
        {
            _subscriptions?.Dispose();
            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>더미 검증용: 첫 셀의 레벨을 1 올린다(만렙에서 멈춤). 버튼 OnClick에 연결한다.</summary>
        public void DebugRaiseFirst()
        {
            if (_viewModel.CharacterList.Count == 0)
                return;

            var current = _viewModel.CharacterList[0].Level.CurrentValue;
            _viewModel.SetLevel(0, current + 1);
        }

        /// <summary>셀 프리팹을 하나 생성해 contentRoot 끝에 붙이고 ViewModel에 바인딩한다.</summary>
        /// <param name="cellViewModel">새 셀이 표시할 캐릭터 셀 ViewModel.</param>
        private void AddCell(CharacterCellViewModel cellViewModel)
        {
            var cell = Instantiate(cellPrefab, contentRoot);
            cell.Bind(cellViewModel);
            _cells.Add(cell);
        }

        /// <summary>지정 위치의 셀을 목록에서 제거하고 GameObject를 파괴한다.</summary>
        /// <param name="index">제거할 셀의 위치. ViewModel 목록의 인덱스와 대응한다.</param>
        private void RemoveCell(int index)
        {
            var cell = _cells[index];
            _cells.RemoveAt(index);
            Destroy(cell.gameObject);
        }
    }
}
