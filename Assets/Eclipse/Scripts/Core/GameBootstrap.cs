using Cysharp.Threading.Tasks;
using Eclipse.View.Infra;
using UnityEngine;
using VContainer;

namespace Eclipse.Core
{
    /// <summary>
    /// 게임 씬 진입점. DI 주입이 끝난 뒤 초기 부팅 처리를 수행한다.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private ScreenManager _screenManager;

        [Inject]
        public void Construct(ScreenManager screenManager)
        {
            _screenManager = screenManager;
        }

        private void Start()
        {
            // 부팅 시 첫 화면을 띄운다.
            _screenManager.Push(ScreenId.Lobby).Forget();
        }
    }
}