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
#if UNITY_IOS || UNITY_ANDROID
        // 모바일은 vSync를 무시하고 targetFrameRate를 따르며, 미설정 시 기본 30fps로 돈다.
        // WebGL·데스크톱은 브라우저/모니터 vSync에 맡기므로 건드리지 않는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SetTargetFrameRate() => Application.targetFrameRate = 60;
#endif

        private ScreenManager _screenManager;

        [Inject]
        public void Construct(ScreenManager screenManager)
        {
            _screenManager = screenManager;
        }

        private void Start()
        {
            _screenManager.Push(ScreenId.Lobby).Forget();
        }
    }
}