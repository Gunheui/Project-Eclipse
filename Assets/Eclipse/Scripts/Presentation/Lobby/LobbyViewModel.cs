using Cysharp.Threading.Tasks;
using Eclipse.Service;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 로비(코어 루프 허브) 화면의 상태. 각 메뉴 목적지가 진입 가능한지의 단일 원천이다.
    /// 값이 고정된 표시 전용 뷰모델이라 스트림을 두지 않는다. 미구현 목적지는 false로 잠근다.
    /// </summary>
    public sealed class LobbyViewModel : ViewModelBase
    {
        private readonly ISceneFlow _sceneFlow;

        public LobbyViewModel(ISceneFlow sceneFlow)
        {
            _sceneFlow = sceneFlow;
        }

        /// <summary>캐릭터 목록 진입 가능 여부.</summary>
        public bool CanOpenCharacterList => true;

        /// <summary>스토리 진입 가능 여부.</summary>
        public bool CanOpenStory => false;

        /// <summary>모집(가챠) 진입 가능 여부.</summary>
        public bool CanOpenGacha => false;

        /// <summary>전투 진입 가능 여부.</summary>
        public bool CanOpenBattle => true;

        /// <summary>상점 진입 가능 여부.</summary>
        public bool CanOpenShop => false;

        // TODO: 스테이지 선택 화면이 생기면 전투 직행 대신 그 화면을 거치도록 교체한다.
        /// <summary>전투 씬으로 진입한다. [전투] 버튼의 클릭 커맨드.</summary>
        /// <returns>씬 전환이 끝나면 완료되는 태스크.</returns>
        public UniTask EnterBattleAsync() => _sceneFlow.ToBattleAsync();
    }
}
