using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Eclipse.Service
{
    /// <summary>
    /// <see cref="ISceneFlow"/>의 SceneManager 구현. 씬은 Single 모드로 로드해
    /// 이전 씬(오브젝트·씬 스코프)을 통째로 내리고 목적지 씬으로 교체한다.
    /// </summary>
    public sealed class SceneFlowService : ISceneFlow
    {
        private const string MainSceneName = "MainScene";
        private const string BattleSceneName = "BattleScene";

        public UniTask ToBattleAsync() => LoadAsync(BattleSceneName);

        public UniTask ToMainAsync() => LoadAsync(MainSceneName);

        private static async UniTask LoadAsync(string sceneName)
        {
            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single).ToUniTask();
        }
    }
}