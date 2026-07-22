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
    /// 파티 픽 화면. 보유 로스터를 항목로 생성해 픽 아이템에 바인딩하고, 항목을 탭하면 편성의 앵커 슬롯에 배치한 뒤
    /// 곧바로 화면을 닫아 편성으로 돌아간다(확정 단계 없음). 역할 필터로 표시를 거르고 정렬 버튼으로 순서를 바꾸며,
    /// 뒤로가기는 그냥 닫는다.
    /// </summary>
    public class PartyPickView : MonoBehaviour, IScreen
    {
        [SerializeField] private RosterPickItemView itemPrefab;
        [SerializeField] private Transform contentRoot;

        [Header("내비")]
        [SerializeField] private Button backButton;

        [Header("정렬")]
        [SerializeField] private Button sortButton;
        [SerializeField] private TMP_Text sortLabel;

        [Header("역할 필터")]
        [SerializeField] private RoleFilterBar roleFilterBar;

        private PartyPickViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly List<RosterPickItemView> _items = new List<RosterPickItemView>();
        private readonly CompositeDisposable _bindings = new CompositeDisposable();

        /// <summary> ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다. </summary>
        [Inject]
        public void Construct(PartyPickViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때 호출된다. 픽 세션을 시작(슬롯 배지 시드)하고
        /// 로스터 항목·역할 필터를 바인딩한다. 바인딩은 OnExit까지 유지된다.
        /// </summary>
        public UniTask OnEnter()
        {
            _viewModel.BeginSession();

            if (backButton != null)
                backButton.onClick.AddListener(OnBack);
            if (sortButton != null)
                sortButton.onClick.AddListener(OnSort);
            if (roleFilterBar != null)
                roleFilterBar.Selected += OnRoleFilterSelected;

            // 정렬 구독이 먼저다. 최초 통지가 항목 생성을 겸하고, 필터 구독이 그 위에 표시를 거른다.
            _viewModel.CurrentSortKey
                .Subscribe(OnSortKeyChanged)
                .AddTo(_bindings);
            _viewModel.RoleFilter
                .Subscribe(ApplyRoleFilter)
                .AddTo(_bindings);

            return UniTask.CompletedTask;
        }

        /// <summary> 화면이 스택에서 제거될 때 호출된다. 버튼 리스너·바인딩을 해지하고 항목을 모두 파괴한다. </summary>
        public UniTask OnExit()
        {
            _bindings.Clear();
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBack);
            if (sortButton != null)
                sortButton.onClick.RemoveListener(OnSort);
            if (roleFilterBar != null)
                roleFilterBar.Selected -= OnRoleFilterSelected;

            DestroyItems();
            return UniTask.CompletedTask;
        }

        // 항목 뷰 하나를 생성해 contentRoot에 붙이고 픽 항목에 바인딩한다. 탭 시 앵커 슬롯에 배치하고 화면을 닫는다.
        // 정렬로 항목 뷰가 재생성되므로 생성 시점에 현재 역할 필터를 적용한다.
        private void AddItem(PartyPickItemViewModel itemViewModel)
        {
            var item = Instantiate(itemPrefab, contentRoot);
            item.Bind(itemViewModel, () => OnPick(itemViewModel));
            item.gameObject.SetActive(Matches(itemViewModel, _viewModel.RoleFilter.CurrentValue));
            _items.Add(item);
        }

        // 정렬이 바뀌면 라벨을 갱신하고 항목을 VM의 새 순서대로 다시 만든다(로스터 규모에서 재배치보다 단순).
        private void OnSortKeyChanged(CharacterSortKey key)
        {
            if (sortLabel != null)
                sortLabel.text = $"정렬: {CharacterSort.Label(key)}";

            DestroyItems();
            foreach (var item in _viewModel.Items)
                AddItem(item);
        }

        private void DestroyItems()
        {
            foreach (var item in _items)
                Destroy(item.gameObject);
            _items.Clear();
        }

        // 역할 필터에 맞춰 항목 표시를 켜고 끈다. null이면 전체 표시. 숨겨진 항목의 슬롯 배지는 VM에 남는다.
        // 필터 바의 선택 표시도 같은 값으로 맞춘다.
        private void ApplyRoleFilter(Role? role)
        {
            if (roleFilterBar != null)
                roleFilterBar.SetSelected(role);
            var items = _viewModel.Items;
            for (int i = 0; i < _items.Count && i < items.Count; i++)
                _items[i].gameObject.SetActive(Matches(items[i], role));
        }

        private static bool Matches(PartyPickItemViewModel item, Role? role)
            => role == null || item.Role == role.Value;

        private void OnRoleFilterSelected(Role? role) => _viewModel.RoleFilter.Value = role;

        private void OnSort() => _viewModel.CycleSort();

        // 항목 탭: 앵커 슬롯에 배치(같은 캐릭터 재탭이면 제거)한 뒤 편성 화면으로 자동 복귀한다.
        private void OnPick(PartyPickItemViewModel item)
        {
            _viewModel.Place(item);
            _screenManager.Pop().Forget();
        }

        private void OnBack()
        {
            _screenManager.Pop().Forget();
        }
    }
}
