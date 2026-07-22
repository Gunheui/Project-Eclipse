using System;
using System.Collections.Generic;
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
    /// 인게임(BattleScene) 씬 스코프. 전투 진행 동안에만 유지되는 서비스(난수·계산 파이프라인·
    /// 스킬 실행기)와 전투 화면 뷰모델을 Scoped로 등록하며, 씬이 내려가면 함께 정리된다.
    /// </summary>
    public class BattleLifetimeScope : LifetimeScope
    {
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

            builder.RegisterComponentInHierarchy<BattleView>();

            // 팝업 매니저는 씬 인프라라 씬마다 하나씩 선다. 결과 팝업은 이 컨테이너에서 생성·주입된다.
            builder.RegisterComponentInHierarchy<PopupManager>();

            // 결과 팝업이 생성되는 시점 = 전투 종료 후라 결과·보상 모두 이미 확정값이다(그래서 값 복사로 충분).
            builder.Register(c =>
            {
                var battle = c.Resolve<BattleViewModel>();
                return new ResultViewModel(battle.Result.CurrentValue, battle.GrantedRewards);
            }, Lifetime.Transient);

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

            // 전투 조립은 팩토리가 소유한다. 스테이지(적 편성)와 아군 파티는 app-scope NavigationContext에 실려
            // 온다(씬 경계 캐리어). 파티는 편성 화면이 확정한 SelectedParty를 쓰고, 없을 때만 세이브 로스터로 폴백한다.
            builder.Register<BattleFactory>(Lifetime.Scoped);
            builder.Register(c =>
            {
                // nav 읽기는 이 조립 람다에서 끝난다 — 뷰모델은 여기서 확정된 장·스테이지를 불변으로 받는다.
                // 스테이지 데이터 내용 검증(적 편성·장 소속)은 팩토리 계약이다.
                var nav = c.Resolve<NavigationContext>();
                var stage = nav.SelectedStage;
                if (stage == null)
                    throw new InvalidOperationException(
                        "BattleScene 진입에 선택 스테이지가 없다. StageSelect(S10)를 거쳐 진입해야 한다 " +
                        "— 단독 씬 Play는 지원하지 않는다(debugSeedOverride는 시드만 고정할 뿐 스테이지를 대신하지 않는다).");
                var chapter = nav.SelectedChapter;
                if (chapter == null)
                    throw new InvalidOperationException(
                        $"BattleScene 진입에 선택 장이 없다(스테이지 '{stage.id}'만 실려 있다). " +
                        "StageSelect가 SelectedStage와 SelectedChapter를 함께 기록해야 한다.");

                // 선택 파티가 비어 있으면 세이브 로스터 앞 4명으로 대체한다(테스트용 폴백).
                var party = nav.SelectedParty?.ToList() ?? new List<OwnedCharacter>();
                if (party.All(x => x == null))
                    party = c.Resolve<PlayerSave>().OwnedCharacters
                        .Where(x => x != null).Take(PlayerSave.PartySlotCount).ToList();
                if (party.All(x => x == null))
                    throw new InvalidOperationException(
                        "전투에 세울 아군이 없다 — 선택 파티도 세이브 로스터도 비어 있다.");

                return c.Resolve<BattleFactory>().Create(party, chapter, stage, battleSeed, startAuto);
            }, Lifetime.Scoped);
        }
    }
}