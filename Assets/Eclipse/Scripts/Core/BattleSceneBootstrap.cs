using Cysharp.Threading.Tasks;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.Core
{
    /// <summary>
    /// 전투 씬(BattleScene)의 진입점. 씬 스코프 주입이 살아 있는지 부팅 로그로 알리고,
    /// 나가기 버튼으로 아웃게임(메인 씬) 복귀를 연결한다.
    /// </summary>
    public class BattleSceneBootstrap : MonoBehaviour
    {
        // TODO: 임시 배선 — 나가기 버튼은 정식 전투 UI(BattleViewModel + HUD View)로 이관한다.
        [SerializeField] private Button exitButton;

        private IAppLogger _logger;
        private ISceneFlow _sceneFlow;
        private SkillExecutor _skillExecutor;

        [Inject]
        public void Construct(IAppLogger logger, ISceneFlow sceneFlow, SkillExecutor skillExecutor)
        {
            _logger = logger;
            _sceneFlow = sceneFlow;
            _skillExecutor = skillExecutor;
        }

        private void Start()
        {
            _logger.Log($"Battle scene booted. (skillExecutor injected: {_skillExecutor != null})");

            exitButton.OnClickAsObservable()
                .Subscribe(_ => _sceneFlow.ToMainAsync().Forget())
                .AddTo(this);
        }
    }
}