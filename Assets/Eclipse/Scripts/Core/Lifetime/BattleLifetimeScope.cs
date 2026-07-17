using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 인게임(BattleScene) 씬 스코프. 전투 진행 동안에만 유지되는 서비스(난수·계산 파이프라인·
    /// 스킬 실행기)와 전투 화면 뷰모델을 Scoped로 등록하며, 씬이 내려가면 함께 정리된다.
    /// </summary>
    public class BattleLifetimeScope : LifetimeScope
    {
        // 파티·전장이 다루는 최대 편성 인원. 로스터·적이 이보다 많아도 앞에서부터 이 수만 참전한다.
        private const int PartySize = 4;

        [SerializeField] private BattleConstantsSO battleConstants;

        // TODO: 적 편성은 전투 진입 파라미터(스테이지)로 받도록 교체한다.
        [SerializeField] private EnemySO[] enemies;

        [SerializeField] private bool startAuto;

        // TODO: 시드는 전투 진입 파라미터(스테이지·재도전 정책)로 받도록 교체한다.
        [SerializeField] private int battleSeed = 12345;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponentInHierarchy<BattleSceneBootstrap>();
            builder.RegisterComponentInHierarchy<BattleView>();

            builder.RegisterInstance(battleConstants);
            builder.Register<IRandomService>(
                _ => new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.Damage)), Lifetime.Scoped);
            builder.Register(container => new DamagePipeline(
                battleConstants.defenseK,
                battleConstants.varianceMin,
                battleConstants.varianceMax,
                container.Resolve<IRandomService>()), Lifetime.Scoped);
            builder.Register<CombatPipeline>(Lifetime.Scoped);
            builder.Register<TargetResolver>(Lifetime.Scoped);
            builder.Register<SkillExecutor>(Lifetime.Scoped);

            // 전투 조립은 팩토리가 소유한다. 스코프는 팩토리를 등록하고, 인스펙터 값(적 편성·시드·오토·파티 인원)만
            // 넘겨 뷰모델을 위임 생성한다.
            builder.Register<BattleFactory>(Lifetime.Scoped);
            builder.Register(
                c => c.Resolve<BattleFactory>().Create(enemies, battleSeed, startAuto, PartySize),
                Lifetime.Scoped);
        }
    }
}