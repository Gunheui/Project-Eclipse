using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 해당 프로젝트 LifeTimeScope
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            
            //Logger 등록
            builder.Register<IAppLogger, ConsoleAppLogger>(Lifetime.Singleton);

            //Inject 목록
            builder.RegisterComponentInHierarchy<GameBootstrap>();
        }
    }
}