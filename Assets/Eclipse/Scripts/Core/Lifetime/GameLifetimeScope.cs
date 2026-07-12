using Eclipse.Presentation;
using Eclipse.View;
using Eclipse.View.Infra;
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
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            //Inject 목록
            builder.RegisterComponentInHierarchy<GameBootstrap>();
            builder.RegisterComponentInHierarchy<ScreenManager>();
            builder.RegisterComponentInHierarchy<PopupManager>();
            builder.RegisterComponentInHierarchy<CurrencyHudView>();

            builder.Register<LobbyViewModel>(Lifetime.Singleton);
            builder.Register<CharacterListViewModel>(Lifetime.Singleton);
            builder.Register<CharacterDetailViewModel>(Lifetime.Transient);
        }
    }
}
