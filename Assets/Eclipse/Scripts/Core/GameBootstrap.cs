using UnityEngine;
using VContainer;

namespace Eclipse.Core
{
    /// <summary>
    /// 게임 씬 진입점. DI 주입이 끝난 뒤 초기 부팅 처리를 수행한다.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Inject]
        private IAppLogger _appLogger;

        private void Start()
        {
            _appLogger.Log("Eclipse booted");
        }
    }
}