using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
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
            builder.Register<IRandomService>(_ => new SeededRandom(battleSeed), Lifetime.Scoped);
            builder.Register(container => new DamagePipeline(
                battleConstants.defenseK,
                battleConstants.varianceMin,
                battleConstants.varianceMax,
                container.Resolve<IRandomService>()), Lifetime.Scoped);
            builder.Register<CombatPipeline>(Lifetime.Scoped);
            builder.Register<TargetResolver>(Lifetime.Scoped);
            builder.Register<SkillExecutor>(Lifetime.Scoped);

            builder.Register(CreateBattleViewModel, Lifetime.Scoped);
        }

        // 아군은 루트 스코프의 세이브 로스터에서, 적은 인스펙터 편성에서 전투 유닛을 만들어 뷰모델을 조립한다.
        private BattleViewModel CreateBattleViewModel(IObjectResolver container)
        {
            var save = container.Resolve<PlayerSave>();

            // TODO: 편성(파티 구성) UI가 생기면 로스터 앞 4명이 아니라 선택된 파티로 교체한다.
            var allies = save.OwnedCharacters
                .Take(PartySize)
                .Select((owned, slot) => BattleUnit.FromCharacter(owned, slot))
                .ToList();
            var enemyUnits = enemies
                .Take(PartySize)
                .Select((so, slot) => BattleUnit.FromEnemy(so, slot))
                .ToList();

            return new BattleViewModel(
                allies,
                enemyUnits,
                container.Resolve<SkillExecutor>(),
                battleConstants.globalActionCap,
                startAuto,
                container.Resolve<ISceneFlow>());
        }
    }
}