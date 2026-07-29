using System;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.View;
using Eclipse.View.Infra;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Eclipse.Core
{
    /// <summary>
    /// 인게임(BattleScene) 씬 스코프 = 런 스코프. 런 1판 동안 유지되는 세션·상태기계·팩토리를
    /// Scoped로 등록하며, 씬이 내려가면(클리어·실패·앱 종료) 런 상태가 통째로 폐기된다.
    /// 전투 1판짜리 서비스(난수·파이프라인·엔진)는 여기 없다 — 팩토리가 방마다 새로 만든다.
    /// </summary>
    public class BattleLifetimeScope : LifetimeScope
    {
        [SerializeField] private BattleConstantsSO battleConstants;
        [SerializeField] private EncounterTuningSO encounterTuning;
        [SerializeField] private DoorCatalogSO doorCatalog;
        [SerializeField] private BuffCardCatalogSO buffCardCatalog;

        // 0이면 진입할 때마다 새 시드(재도전=fresh). nonzero면 그 값으로 고정 — 난수 재현·디버깅용.
        [SerializeField] private int debugSeedOverride;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // 런 시드는 이 스코프에서 진입당 1회 확정한다. 인카운터·문·카드·방별 전투 시드가 전부 여기서 파생된다.
            int runSeed = debugSeedOverride != 0 ? debugSeedOverride : new System.Random().Next(int.MinValue, int.MaxValue);

            builder.RegisterComponentInHierarchy<BattleView>();
            builder.RegisterComponentInHierarchy<BattleBuffPanelView>();
            builder.RegisterComponentInHierarchy<ChapterRunDriver>();

            // 팝업 매니저는 씬 인프라라 씬마다 하나씩 선다. 방 사이 화면(문·3택1·정산)이 전부 이 스택 위에 뜬다.
            builder.RegisterComponentInHierarchy<PopupManager>();

            builder.RegisterInstance(battleConstants);
            builder.RegisterInstance(encounterTuning);
            builder.RegisterInstance(doorCatalog);
            builder.RegisterInstance(buffCardCatalog);

            // 런 세션 = 런 휘발 상태의 단일 소유자. 챕터·파티는 app-scope NavigationContext에 실려 온다.
            builder.Register(c =>
            {
                var nav = c.Resolve<NavigationContext>();
                var chapter = nav.SelectedChapter;
                var party = nav.SelectedParty;
                if (chapter == null || party == null || party.All(x => x == null))
                    throw new InvalidOperationException(
                        "BattleScene 진입에 선택 챕터·파티가 없다. 파티 편성(S11)의 [런 시작]을 거쳐 진입해야 한다 " +
                        "— 단독 씬 Play는 지원하지 않는다(debugSeedOverride는 시드만 고정할 뿐 진입 경로를 대신하지 않는다).");

                return new ChapterRunSession(chapter, encounterTuning, party, runSeed);
            }, Lifetime.Scoped);

            // 런 상태기계. 문·카드·재화 문 폭은 전투와 분리된 런 스트림에서 굴린다(결정성 격리).
            builder.Register(c =>
            {
                var session = c.Resolve<ChapterRunSession>();
                var generator = new EncounterGenerator(encounterTuning,
                    new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Mutation)));
                var doorDraw = new DoorDraw(doorCatalog,
                    new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Door)));
                var cardPool = new CardPool(buffCardCatalog,
                    new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Card)));
                // 재화 문 폭은 문 추첨과 다른 스트림에서 굴린다. 공유하면 재화 문이 몇 번 공개됐는지가
                // 이후 문 추첨 수열을 밀어낸다.
                var currencyRng = new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Currency));

                return new ChapterRunFlow(session, generator, doorDraw, cardPool, currencyRng, doorCatalog,
                    c.Resolve<IRewardService>(), c.Resolve<ChapterProgress>(), c.Resolve<SaveService>(),
                    c.Resolve<Eclipse.Service.ISceneFlow>());
            }, Lifetime.Scoped);

            builder.Register(c => new BattleFactory(battleConstants, c.Resolve<ChapterRunSession>(), encounterTuning),
                Lifetime.Scoped);
        }
    }
}