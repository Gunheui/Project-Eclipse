using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View;
using Eclipse.View.Infra;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 아웃게임(MainScene) 씬 스코프. 씬 안에서만 사는 UI 매니저·ViewModel을 등록한다.
    /// 씬 전환에도 유지되는 상태는 부모인 AppLifetimeScope가 보유한다.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        // 런 입구(파티 편성)에 넘길 장 정의. 인스펙터에서 배선한다.
        [SerializeField] private ChapterSO[] chapters;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponentInHierarchy<GameBootstrap>();
            builder.RegisterComponentInHierarchy<ScreenManager>();
            builder.RegisterComponentInHierarchy<PopupManager>();
            builder.RegisterComponentInHierarchy<CurrencyHudView>();

            builder.Register<LobbyViewModel>(Lifetime.Singleton);
            builder.Register<CharacterListViewModel>(Lifetime.Singleton);
            builder.Register<CharacterDetailViewModel>(Lifetime.Transient);

            // 편성 draft(PartyFormation)가 그 위로 push되는 픽 화면(PartyPick) 동안 살아남아야 하므로 둘 다 Singleton.
            // ChapterSO[]를 자동 주입하면 VContainer가 컬렉션 resolve로 가로채므로 이 인자만 손수 지정한다.
            builder.Register<PartyFormationViewModel>(Lifetime.Singleton).WithParameter(chapters);
            builder.Register<PartyPickViewModel>(Lifetime.Singleton);
        }
    }
}
