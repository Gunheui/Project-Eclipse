using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data.Enums;
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
    /// 캐릭터 목록 화면. CharacterListViewModel의 항목 목록으로 항목 뷰를 생성하고, 항목을 탭하면 상세 화면을 연다.
    /// 역할 필터로 표시를 거르고 정렬 버튼으로 순서를 바꾼다. 구독은 화면이 보이는 동안(OnEnter~OnExit)만 유지한다.
    /// </summary>
    public class CharacterListView : MonoBehaviour, IScreen
    {
        [SerializeField] private CharacterItemView itemPrefab;
        [SerializeField] private Transform contentRoot;

        [Header("내비")]
        [SerializeField] private Button backButton;

        [Header("정렬")]
        [SerializeField] private Button sortButton;
        [SerializeField] private TMP_Text sortLabel;

        [Header("역할 필터")]
        [SerializeField] private RoleFilterBar roleFilterBar;

        private CharacterListViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly List<CharacterItemView> _items = new List<CharacterItemView>();
        private CompositeDisposable _subscriptions;

        /// <summary> ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다. </summary>
        [Inject]
        public void Construct(CharacterListViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때 호출된다. 정렬·역할 필터를 구독한다.
        /// 정렬의 최초 통지가 항목 뷰 생성을 겸하므로 여기서 따로 만들지 않는다. 구독은 OnExit까지 유지된다.
        /// </summary>
        public UniTask OnEnter()
        {
            _subscriptions = new CompositeDisposable();

            backButton.onClick.AddListener(() => _screenManager.Pop().Forget());
            sortButton.onClick.AddListener(() => _viewModel.CycleSort());
            if (roleFilterBar != null)
                roleFilterBar.Selected += OnRoleFilterSelected;

            // 정렬 구독이 먼저다. 최초 통지가 항목 뷰 생성을 겸하고, 필터 구독이 그 위에 표시를 거른다.
            _viewModel.CurrentSortKey
                .Subscribe(OnSortKeyChanged)
                .AddTo(_subscriptions);
            _viewModel.RoleFilter
                .Subscribe(_ => ApplyRoleFilter())
                .AddTo(_subscriptions);

            return UniTask.CompletedTask;
        }

        /// <summary> 화면이 스택에서 제거될 때 호출된다. 정렬·필터 구독을 해지하고 항목 뷰를 모두 파괴한다. </summary>
        public UniTask OnExit()
        {
            _subscriptions?.Dispose();
            if (roleFilterBar != null)
                roleFilterBar.Selected -= OnRoleFilterSelected;

            DestroyItems();
            return UniTask.CompletedTask;
        }

        private void OnRoleFilterSelected(Role? role) => _viewModel.RoleFilter.Value = role;

        // 정렬이 바뀌면 라벨을 갱신하고 항목 뷰를 VM의 새 순서대로 다시 만든다(로스터 규모에서 재배치보다 단순).
        private void OnSortKeyChanged(CharacterSortKey key)
        {
            sortLabel.text = $"정렬: {CharacterSort.Label(key)}";

            DestroyItems();
            foreach (var itemViewModel in _viewModel.Items)
                AddItem(itemViewModel);
        }

        private void DestroyItems()
        {
            foreach (var item in _items)
                Destroy(item.gameObject);
            _items.Clear();
        }

        /// <summary>
        /// 현재 역할 필터에 맞춰 항목 표시를 켜고 끈다. null이면 전체 표시. 필터 바의 선택 표시도 함께 맞춘다.
        /// 항목 뷰를 제거하지 않고 SetActive만 토글해 인덱스를 ViewModel 목록과 일치시킨 채로 둔다.
        /// </summary>
        private void ApplyRoleFilter()
        {
            var role = _viewModel.RoleFilter.CurrentValue;
            if (roleFilterBar != null)
                roleFilterBar.SetSelected(role);
            var itemViewModels = _viewModel.Items;
            for (int i = 0; i < _items.Count && i < itemViewModels.Count; i++)
                _items[i].gameObject.SetActive(Matches(itemViewModels[i], role));
        }

        private static bool Matches(CharacterItemViewModel itemViewModel, Role? role)
            => role == null || itemViewModel.Role == role.Value;

        /// <summary>
        /// 항목 프리팹을 하나 생성해 contentRoot 끝에 붙이고 ViewModel에 바인딩한다.
        /// 정렬 시 항목 뷰가 전량 재생성되므로, 생성 시점에 현재 역할 필터를 적용해 필터 상태를 유지한다.
        /// </summary>
        private void AddItem(CharacterItemViewModel itemViewModel)
        {
            var item = Instantiate(itemPrefab, contentRoot);
            item.Bind(itemViewModel, () => OnItemSelected(item));
            item.gameObject.SetActive(Matches(itemViewModel, _viewModel.RoleFilter.CurrentValue));
            _items.Add(item);
        }

        /// <summary>항목을 탭하면 목록 위치로 선택 인덱스를 기록하고 상세 화면을 전면에 올린다.</summary>
        private void OnItemSelected(CharacterItemView item)
        {
            var index = _items.IndexOf(item);
            if (index < 0)
                return;

            _viewModel.Select(index);
            _screenManager.Push(ScreenId.CharacterDetail).Forget();
        }
    }
}
