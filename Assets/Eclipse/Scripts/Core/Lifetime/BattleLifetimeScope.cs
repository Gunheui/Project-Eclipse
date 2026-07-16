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
            var ownedParty = save.OwnedCharacters.Take(PartySize).ToList();
            var enemyParty = enemies.Take(PartySize).ToList();

            // 유닛과 그 유닛의 아트를 함께 만들어 넘긴다. 아트는 도메인이 아닌 정의 SO에서 뽑는다.
            // 타임라인 아이콘은 아군=얼굴 크롭, 적=소형 배틀러 스프라이트 그대로. 아군 얼굴이 비면 그 칸은
            // 비어 그려진다 — 전신 초상으로 대신 채우면 데이터 누락이 감춰지므로 폴백하지 않는다.
            var allyEntries = ownedParty
                .Select((owned, slot) => new BattleUnitEntry(
                    Combatant.FromCharacter(owned, slot),
                    owned.Definition.portraitAssetRef,
                    owned.Definition.faceIconAssetRef))
                .ToList();
            var enemyEntries = enemyParty
                .Select((so, slot) => new BattleUnitEntry(
                    Combatant.FromEnemy(so, slot),
                    so.battlerAssetRef,
                    so.battlerAssetRef))
                .ToList();

            var targeting = container.Resolve<TargetResolver>();
            var combat = container.Resolve<CombatPipeline>();
            var targetRng = new SeededRandom(BattleSeed.ForTargeting(battleSeed));

            // 아군·적 오토는 타겟 난수 스트림을 공유한다(굴림 순서 = 시드 재현의 근거).
            var autoRule = RuleBasedActionProvider.AllyAuto(targeting, combat, targetRng);
            var manualProvider = new ManualActionProvider(autoRule) { AutoMode = startAuto };
            var enemyAi = RuleBasedActionProvider.EnemyAi(targeting, combat, targetRng,
                battleConstants.enemyLethalChance, battleConstants.enemyLowHpBias);

            // 도메인(엔진·스케줄러)은 아트를 모르므로 유닛만 뽑아 넘긴다. 순서는 아군 먼저, 그다음 적.
            var allyUnits = allyEntries.Select(e => e.Unit).ToList();
            var enemyUnits = enemyEntries.Select(e => e.Unit).ToList();

            var scheduler = new AtbTurnScheduler(allyUnits.Concat(enemyUnits));
            var engine = new BattleEngine(allyUnits, enemyUnits, scheduler,
                container.Resolve<SkillExecutor>(), manualProvider, enemyAi, battleConstants.globalActionCap);

            return new BattleViewModel(
                allyEntries,
                enemyEntries,
                engine,
                scheduler,
                manualProvider,
                targeting,
                container.Resolve<ISceneFlow>());
        }
    }
}