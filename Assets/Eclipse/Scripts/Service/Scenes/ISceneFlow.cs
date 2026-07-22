using Cysharp.Threading.Tasks;

namespace Eclipse.Service
{
    /// <summary>
    /// 아웃게임(로비류) 씬과 인게임(전투) 씬 사이의 전환 창구.
    /// 소비 측은 목적지만 고르고, 씬 이름·로드 방식은 구현이 감춘다.
    /// </summary>
    public interface ISceneFlow
    {
        /// <summary>전투 씬으로 전환한다. 현재 씬은 언로드된다.</summary>
        UniTask ToBattleAsync();

        /// <summary>아웃게임(메인) 씬으로 돌아간다. 현재 씬은 언로드된다.</summary>
        UniTask ToMainAsync();
    }
}