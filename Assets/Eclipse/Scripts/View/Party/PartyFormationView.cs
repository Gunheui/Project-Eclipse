using Cysharp.Threading.Tasks;
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
    /// 파티 편성 화면. 4개 슬롯을 생성해 편성 VM의 슬롯 스트림에 각각 바인딩하고, 슬롯 탭 시 픽 화면을 올린다.
    /// 하단에 인원수·[런 시작] 버튼을 바인딩한다. 시작 버튼은 항상 활성이고, 4인 미달로 누르면 안내 팝업이 뜬다.
    /// </summary>
    public class PartyFormationView : MonoBehaviour, IScreen
    {
        [Tooltip("SlotGrid에 고정 배치된 4개 슬롯(순서 = 슬롯 0~3). 편성 슬롯 수와 개수가 맞아야 한다.")]
        [SerializeField] private PartySlotView[] slotViews;

        [Header("하단")]
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Button enterButton;

        [Header("내비")]
        [SerializeField] private Button backButton;

        private PartyFormationViewModel _viewModel;
        private ScreenManager _screenManager;
        private PopupManager _popups;
        private readonly CompositeDisposable _bindings = new CompositeDisposable();

        /// <summary> ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다. </summary>
        [Inject]
        public void Construct(PartyFormationViewModel viewModel, ScreenManager screenManager, PopupManager popups)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
            _popups = popups;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때 호출된다. 슬롯·인원수·시작 버튼을 편성 VM에 바인딩한다.
        /// 바인딩은 OnExit까지 유지된다.
        /// </summary>
        public UniTask OnEnter()
        {
            int slotCount = Mathf.Min(slotViews.Length, _viewModel.SlotOccupants.Count);
            for (int i = 0; i < slotCount; i++)
            {
                int slot = i;
                slotViews[slot].Bind(_viewModel.SlotOccupants[slot], () => OnSlotTapped(slot));
            }

            if (countLabel != null)
                _viewModel.PartyCount
                    .Subscribe(count => countLabel.text = $"{count} / {PartyFormationViewModel.SlotCount}")
                    .AddTo(_bindings);

            enterButton.onClick.AddListener(OnEnterBattle);

            if (backButton != null)
                backButton.onClick.AddListener(OnBack);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 버튼 리스너·바인딩을 해지한다.
        /// 슬롯은 고정 배치라 파괴하지 않는다(구독·리스너는 pop 시 프리팹 Destroy와 함께 정리).
        /// </summary>
        public UniTask OnExit()
        {
            _bindings.Clear();
            enterButton.onClick.RemoveListener(OnEnterBattle);
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBack);
            return UniTask.CompletedTask;
        }

        /// <summary>슬롯을 탭하면 그 슬롯으로 픽 세션을 열고 픽 화면을 전면에 올린다.</summary>
        private void OnSlotTapped(int index)
        {
            _viewModel.BeginPick(index);
            _screenManager.Push(ScreenId.PartyPick).Forget();
        }

        /// <summary>
        /// [런 시작]. 4인이 다 차야 진입하고, 미달이면 버튼을 죽이는 대신 왜 못 가는지 팝업으로 알린다.
        /// </summary>
        private void OnEnterBattle()
        {
            if (!_viewModel.CanEnter.CurrentValue)
            {
                _popups.ShowAlert(RunTexts.PartyNotFullTitle, RunTexts.PartyNotFullBody).Forget();
                return;
            }
            _viewModel.StartRun();
        }
        private void OnBack() => _screenManager.Pop().Forget();
    }
}
