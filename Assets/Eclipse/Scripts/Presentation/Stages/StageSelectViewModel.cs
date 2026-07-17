using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Service;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 스테이지 선택 화면의 상태. 고정된 스테이지 목록을 노출하고, 셀 탭 시 전투 씬으로 진입시킨다.
    /// </summary>
    public sealed class StageSelectViewModel : ViewModelBase
    {
        private readonly ISceneFlow _sceneFlow;
        private bool _entering;

        /// <summary> 화면에 표시할 스테이지 목록. 순서·내용 고정(런타임 변경 없음). </summary>
        public IReadOnlyList<StageSO> Stages { get; }

        public StageSelectViewModel(StageSO[] stages, ISceneFlow sceneFlow)
        {
            Stages = stages;
            _sceneFlow = sceneFlow;
        }

        /// <summary>
        /// 스테이지를 골라 전투 씬으로 진입한다. 씬 로드 중복 진입을 한 번만 막는다.
        /// </summary>
        /// <param name="stage">탭된 스테이지(현재 전투 씬은 단일이라 값은 쓰지 않는다).</param>
        public void EnterBattle(StageSO stage)
        {
            if (_entering)
                return;
            _entering = true;
            _sceneFlow.ToBattleAsync().Forget();
        }
    }
}
