using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 파티 픽 화면. 보유 로스터를 셀로 생성해 픽 아이템에 바인딩하고, 셀을 탭하면 편성의 앵커 슬롯에 배치한 뒤
    /// 곧바로 화면을 닫아 편성으로 돌아간다(확정 단계 없음). 역할 버튼으로 표시를 거르고, 뒤로가기는 그냥 닫는다.
    /// </summary>
    public class PartyPickView : MonoBehaviour, IScreen
    {
        [SerializeField] private RosterPickCellView cellPrefab;
        [SerializeField] private Transform contentRoot;

        [Header("하단")]
        [SerializeField] private Button backButton;

        [Header("역할 필터")]
        [SerializeField] private Button filterAllButton;
        [SerializeField] private Button filterTankerButton;
        [SerializeField] private Button filterDealerButton;
        [SerializeField] private Button filterSupporterButton;
        [SerializeField] private Button filterHealerButton;

        private PartyPickViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly List<RosterPickCellView> _cells = new List<RosterPickCellView>();
        private readonly CompositeDisposable _bindings = new CompositeDisposable();

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">로스터 표시·앵커 슬롯 배치를 담당하는 픽 VM(컨테이너가 주입).</param>
        /// <param name="screenManager">배치·닫기 시 편성 화면으로 되돌린다.</param>
        [Inject]
        public void Construct(PartyPickViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때 호출된다. 픽 세션을 시작(슬롯 배지 시드)하고 로스터 셀을 생성해 바인딩하며,
        /// 역할 필터를 바인딩한다. 바인딩은 OnExit까지 유지된다.
        /// </summary>
        /// <returns>등장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            _viewModel.BeginSession();

            foreach (var item in _viewModel.Items)
                AddCell(item);

            if (backButton != null)
                backButton.onClick.AddListener(OnBack);

            WireRoleFilter();
            _viewModel.RoleFilter
                .Subscribe(ApplyRoleFilter)
                .AddTo(_bindings);

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 버튼 리스너·바인딩을 해지하고 생성한 셀을 모두 파괴한다.
        /// </summary>
        /// <returns>퇴장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnExit()
        {
            _bindings.Clear();
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBack);
            UnwireRoleFilter();

            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();
            return UniTask.CompletedTask;
        }

        // 셀 하나를 생성해 contentRoot에 붙이고 픽 아이템에 바인딩한다. 탭 시 앵커 슬롯에 배치하고 화면을 닫는다.
        private void AddCell(PartyPickItemViewModel item)
        {
            var cell = Instantiate(cellPrefab, contentRoot);
            cell.Bind(item, () => OnPick(item));
            _cells.Add(cell);
        }

        // 역할 필터에 맞춰 셀 표시를 켜고 끈다. null이면 전체 표시. 숨겨진 셀의 슬롯 배지는 VM에 남는다.
        private void ApplyRoleFilter(Role? role)
        {
            var items = _viewModel.Items;
            for (int i = 0; i < _cells.Count && i < items.Count; i++)
                _cells[i].gameObject.SetActive(role == null || items[i].Role == role.Value);
        }

        private void WireRoleFilter()
        {
            if (filterAllButton != null)
                filterAllButton.onClick.AddListener(OnFilterAll);
            if (filterTankerButton != null)
                filterTankerButton.onClick.AddListener(OnFilterTanker);
            if (filterDealerButton != null)
                filterDealerButton.onClick.AddListener(OnFilterDealer);
            if (filterSupporterButton != null)
                filterSupporterButton.onClick.AddListener(OnFilterSupporter);
            if (filterHealerButton != null)
                filterHealerButton.onClick.AddListener(OnFilterHealer);
        }

        private void UnwireRoleFilter()
        {
            if (filterAllButton != null)
                filterAllButton.onClick.RemoveListener(OnFilterAll);
            if (filterTankerButton != null)
                filterTankerButton.onClick.RemoveListener(OnFilterTanker);
            if (filterDealerButton != null)
                filterDealerButton.onClick.RemoveListener(OnFilterDealer);
            if (filterSupporterButton != null)
                filterSupporterButton.onClick.RemoveListener(OnFilterSupporter);
            if (filterHealerButton != null)
                filterHealerButton.onClick.RemoveListener(OnFilterHealer);
        }

        private void OnFilterAll() => _viewModel.SetRoleFilter(null);
        private void OnFilterTanker() => _viewModel.SetRoleFilter(Role.Tanker);
        private void OnFilterDealer() => _viewModel.SetRoleFilter(Role.Dealer);
        private void OnFilterSupporter() => _viewModel.SetRoleFilter(Role.Supporter);
        private void OnFilterHealer() => _viewModel.SetRoleFilter(Role.Healer);

        // 셀 탭: 앵커 슬롯에 배치(같은 캐릭터 재탭이면 제거)한 뒤 편성 화면으로 자동 복귀한다.
        private void OnPick(PartyPickItemViewModel item)
        {
            _viewModel.Place(item);
            _screenManager.Pop().Forget();
        }

        // 닫기: 편성을 그대로 두고 편성 화면으로 되돌린다.
        private void OnBack()
        {
            _screenManager.Pop().Forget();
        }
    }
}
