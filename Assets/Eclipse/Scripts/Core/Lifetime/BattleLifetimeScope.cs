using Eclipse.Data;
using Eclipse.Domain;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 인게임(BattleScene) 씬 스코프. 전투 진행 동안에만 유지되는 서비스(난수·계산 파이프라인·
    /// 스킬 실행기)를 Scoped로 등록하며, 씬이 내려가면 함께 정리된다.
    /// </summary>
    public class BattleLifetimeScope : LifetimeScope
    {
        [SerializeField] private BattleConstantsSO battleConstants;

        // TODO: 시드는 전투 진입 파라미터(스테이지·재도전 정책)로 받도록 교체한다.
        [SerializeField] private int battleSeed = 12345;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponentInHierarchy<BattleSceneBootstrap>();

            builder.RegisterInstance(battleConstants);
            builder.Register<IRandomService>(_ => new SeededRandom(battleSeed), Lifetime.Scoped);
            builder.Register(container => new DamagePipeline(
                battleConstants.defenseK,
                battleConstants.varianceMin,
                battleConstants.varianceMax,
                container.Resolve<IRandomService>()), Lifetime.Scoped);
            builder.Register<CombatPipeline>(Lifetime.Scoped);
            builder.Register<TargetResolver>(Lifetime.Scoped);
            builder.Register<SkillExecutor>(Lifetime.Scoped);
        }
    }
}