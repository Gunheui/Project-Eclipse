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
    /// 파티 편성 화면. 4개 슬롯 셀을 생성해 편성 VM의 슬롯 스트림에 각각 바인딩하고, 슬롯 탭 시 픽 화면을 올린다.
    /// 상단에 스테이지 요약을, 하단에 인원수·진입 버튼을 바인딩한다. 진입 버튼은 1명 이상일 때만 활성.
    /// </summary>
    public class PartyFormationView : MonoBehaviour, IScreen
    {
        [Tooltip("SlotGrid에 고정 배치된 4개 슬롯 셀(순서 = 슬롯 0~3). 편성 슬롯 수와 개수가 맞아야 한다.")]
        [SerializeField] private PartySlotCellView[] slotCells;

        [Header("스테이지 요약")]
        [SerializeField] private TMP_Text stageNameText;
        [SerializeField] private TMP_Text stageDescText;

        [Header("하단")]
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Button enterButton;

        [Header("내비")]
        [SerializeField] private Button backButton;

        private PartyFormationViewModel _viewModel;
        private ScreenManager _screenManager;
        private readonly CompositeDisposable _bindings = new CompositeDisposable();

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">편성 슬롯·인원수·진입을 담당하는 편성 VM(컨테이너가 주입).</param>
        /// <param name="screenManager">슬롯 탭 시 픽 화면을 올리고 뒤로가기 시 스테이지 선택으로 되돌린다.</param>
        [Inject]
        public void Construct(PartyFormationViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 스택 전면에 설 때 호출된다. 스테이지 요약을 채우고 고정 배치된 슬롯 셀을 슬롯 스트림에 바인딩하며,
        /// 인원수 라벨·진입 버튼 가용성을 편성 VM에 바인딩한다. 바인딩은 OnExit까지 유지된다.
        /// </summary>
        /// <returns>등장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            var stage = _viewModel.SelectedStage;
            if (stage != null)
            {
                if (stageNameText != null)
                    stageNameText.text = stage.displayName;
                if (stageDescText != null)
                    stageDescText.text = stage.description;
            }

            int slotCount = Mathf.Min(slotCells.Length, _viewModel.SlotOccupants.Count);
            for (int i = 0; i < slotCount; i++)
            {
                int slot = i;
                slotCells[slot].Bind(_viewModel.SlotOccupants[slot], () => OnSlotTapped(slot));
            }

            if (countLabel != null)
                _viewModel.PartyCount
                    .Subscribe(count => countLabel.text = $"{count} / {PartyFormationViewModel.SlotCount}")
                    .AddTo(_bindings);

            enterButton.onClick.AddListener(OnEnterBattle);
            _viewModel.CanEnter
                .Subscribe(can => enterButton.interactable = can)
                .AddTo(_bindings);

            if (backButton != null)
                backButton.onClick.AddListener(OnBack);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 버튼 리스너·바인딩을 해지한다. 슬롯 셀은 고정 배치라 파괴하지 않으며,
        /// 셀의 R3 구독·리스너는 화면 프리팹이 pop 시 통째로 Destroy될 때 함께 정리된다.
        /// </summary>
        /// <returns>퇴장 처리가 끝났음을 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnExit()
        {
            _bindings.Clear();
            enterButton.onClick.RemoveListener(OnEnterBattle);
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBack);
            return UniTask.CompletedTask;
        }

        // 슬롯을 탭하면 그 슬롯으로 픽 세션을 열고 픽 화면을 전면에 올린다.
        private void OnSlotTapped(int index)
        {
            _viewModel.BeginPick(index);
            _screenManager.Push(ScreenId.PartyPick).Forget();
        }

        private void OnEnterBattle() => _viewModel.EnterBattle();
        private void OnBack() => _screenManager.Pop().Forget();
    }
}
