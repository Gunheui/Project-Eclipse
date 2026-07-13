using Eclipse.Domain;
using UnityEngine;
using VContainer;

namespace Eclipse.Core
{
    /// <summary>
    /// 전투 씬(BattleScene)의 진입점. 씬 스코프 주입이 살아 있는지 부팅 로그로 확인한다.
    /// 전투 진행·화면 전환은 <see cref="Eclipse.View.BattleView"/>가 담당한다.
    /// </summary>
    public class BattleSceneBootstrap : MonoBehaviour
    {
        private IAppLogger _logger;
        private SkillExecutor _skillExecutor;

        [Inject]
        public void Construct(IAppLogger logger, SkillExecutor skillExecutor)
        {
            _logger = logger;
            _skillExecutor = skillExecutor;
        }

        private void Start()
        {
            _logger.Log($"Battle scene booted. (skillExecutor injected: {_skillExecutor != null})");
        }
    }
}