using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 로비 화면. 하단 메뉴에서 각 목적지로 분기하는 코어 루프 허브다.
    /// <see cref="LobbyViewModel"/>의 진입 가능 플래그로 버튼 활성 상태를 정하고,
    /// 열린 목적지는 <see cref="ScreenManager"/> 스택으로 전환한다.
    /// </summary>
    public class LobbyView : MonoBehaviour, IScreen
    {
        [SerializeField] private Button characterButton;
        [SerializeField] private Button storyButton;
        [SerializeField] private Button gachaButton;
        [SerializeField] private Button battleButton;
        [SerializeField] private Button shopButton;

        private LobbyViewModel _viewModel;
        private ScreenManager _screens;

        [Inject]
        public void Construct(LobbyViewModel viewModel, ScreenManager screens)
        {
            _viewModel = viewModel;
            _screens = screens;
        }

        // 버튼 구독은 인스턴스 수명(this)에 1회만 묶는다. 화면 재진입에 영향받지 않고 파괴 시 자동 해제된다.
        private void Start()
        {
            characterButton.OnClickAsObservable()
                .Subscribe(_ => _screens.Push(ScreenId.CharacterList).Forget())
                .AddTo(this);

            battleButton.OnClickAsObservable()
                .Subscribe(_ => _viewModel.EnterBattleAsync().Forget())
                .AddTo(this);
        }

        /// <summary>화면 진입 시 호출된다. 뷰모델 플래그로 각 메뉴의 활성 상태를 설정한다.</summary>
        public UniTask OnEnter()
        {
            characterButton.interactable = _viewModel.CanOpenCharacterList;
            storyButton.interactable = _viewModel.CanOpenStory;
            gachaButton.interactable = _viewModel.CanOpenGacha;
            battleButton.interactable = _viewModel.CanOpenBattle;
            shopButton.interactable = _viewModel.CanOpenShop;
            return UniTask.CompletedTask;
        }

        /// <summary>화면 이탈 시 호출된다. 구독은 인스턴스 수명에 묶여 있어 별도 정리가 없다.</summary>
        public UniTask OnExit() => UniTask.CompletedTask;
    }
}
