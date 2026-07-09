using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 해당 프로젝트의 루트 라이프타임 스코프
    /// </summary>
    public class AppLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            
            builder.Register<IAppLogger, ConsoleAppLogger>(Lifetime.Singleton);
        }
    }
}