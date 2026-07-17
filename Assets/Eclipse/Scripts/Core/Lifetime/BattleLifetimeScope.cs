using System;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
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

        [SerializeField] private bool startAuto;

        // 0이면 진입할 때마다 새 시드(재도전=fresh). nonzero면 그 값으로 고정 — 난수 재현·디버깅용.
        [SerializeField] private int debugSeedOverride;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // 시드는 이 스코프에서 진입당 1회 확정한다. IRandomService(데미지)와 팩토리(타겟 스트림)가
            // 같은 값을 공유해야 스트림 분리가 한 전투 안에서 일관되므로 로컬 변수로 잡아 둘 다에 넘긴다.
            int battleSeed = debugSeedOverride != 0 ? debugSeedOverride : new System.Random().Next(int.MinValue, int.MaxValue);

            builder.RegisterComponentInHierarchy<BattleSceneBootstrap>();
            builder.RegisterComponentInHierarchy<BattleView>();

            builder.RegisterInstance(battleConstants);
            builder.Register<IRandomService, SeededRandom>(Lifetime.Scoped)
                .WithParameter(BattleSeed.For(battleSeed, BattleSeed.Stream.Damage));
            builder.Register(container => new DamagePipeline(
                battleConstants.defenseK,
                battleConstants.varianceMin,
                battleConstants.varianceMax,
                container.Resolve<IRandomService>()), Lifetime.Scoped);
            builder.Register<CombatPipeline>(Lifetime.Scoped);
            builder.Register<TargetResolver>(Lifetime.Scoped);
            builder.Register<SkillExecutor>(Lifetime.Scoped);

            // 전투 조립은 팩토리가 소유한다. 적 편성은 app-scope NavigationContext에 실려 온 선택 스테이지에서
            // 읽고(씬 경계 캐리어), 시드·오토·파티 인원과 함께 넘겨 뷰모델을 위임 생성한다.
            builder.Register<BattleFactory>(Lifetime.Scoped);
            builder.Register(c =>
            {
                var stage = c.Resolve<NavigationContext>().SelectedStage;
                if (stage == null)
                    throw new InvalidOperationException(
                        "BattleScene 진입에 선택 스테이지가 없다. StageSelect(S10)를 거쳐 진입해야 한다 " +
                        "— 단독 씬 Play는 지원하지 않는다(debugSeedOverride는 시드만 고정할 뿐 스테이지를 대신하지 않는다).");
                if (stage.enemies == null || stage.enemies.Length == 0)
                    throw new InvalidOperationException($"스테이지 '{stage.id}'에 적 편성(enemies)이 비어 있다.");
                for (int i = 0; i < stage.enemies.Length; i++)
                    if (stage.enemies[i] == null)
                        throw new InvalidOperationException(
                            $"스테이지 '{stage.id}'의 적 편성 슬롯 {i}가 비어 있다(Inspector EnemySO 참조 누락).");
                return c.Resolve<BattleFactory>().Create(stage.enemies, battleSeed, startAuto, PartySize);
            }, Lifetime.Scoped);
        }
    }
}