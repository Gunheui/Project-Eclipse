using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// 화면 프레임 검증용 임시 화면. 버튼 이벤트가 내비게이션 메서드를 호출한다.
    /// </summary>
    public class StubScreen : MonoBehaviour, IScreen
    {
        // GoNext가 push할 목적지. 화면별로 인스펙터에서 지정한다.
        [SerializeField] private ScreenId next;

        private ScreenManager _screens;
        private PopupManager _popups;

        [Inject]
        public void Construct(ScreenManager screens, PopupManager popups)
        {
            _screens = screens;
            _popups = popups;
        }

        public UniTask OnEnter()
        {
            Debug.Log($"{name}.OnEnter()");
            return UniTask.CompletedTask;
        }

        public UniTask OnExit()
        {
            Debug.Log($"{name}.OnExit()");
            return UniTask.CompletedTask;
        }

        public void GoNext() => _screens.Push(next).Forget();

        public void GoBack() => _screens.Pop().Forget();

        // 버튼 OnClick은 void 반환만 인식하므로 async 본체를 래핑한다.
        public void ShowConfirm() => ShowConfirmAsync().Forget();

        private async UniTask ShowConfirmAsync()
        {
            var result = await _popups.Show<bool>(PopupId.Confirm);
            Debug.Log($"Confirm result: {result}");
        }
    }
}
