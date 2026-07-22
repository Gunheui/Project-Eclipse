using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;

namespace Eclipse.Core
{
    /// <summary>
    /// 전투 뷰모델 조립 전담 팩토리. 컨테이너가 소유하는 서비스(타겟·전투 파이프라인·스킬 실행기·씬 흐름)는
    /// 생성자로 주입받고, 전투마다 달라지는 값(선택 파티·적 편성·시드·오토 시작)은 <see cref="Create"/> 인자로 받는다.
    /// LifetimeScope는 이 팩토리를 등록하고 결과를 위임 등록할 뿐, 조립 규칙을 소유하지 않는다.
    /// </summary>
    public sealed class BattleFactory
    {
        private readonly BattleConstantsSO constants;
        private readonly TargetResolver targeting;
        private readonly CombatPipeline combat;
        private readonly SkillExecutor executor;
        private readonly ISceneFlow sceneFlow;
        private readonly StageProgress progress;
        private readonly IRewardService rewards;
        private readonly SaveService saveService;

        public BattleFactory(
            BattleConstantsSO constants,
            TargetResolver targeting,
            CombatPipeline combat,
            SkillExecutor executor,
            ISceneFlow sceneFlow,
            StageProgress progress,
            IRewardService rewards,
            SaveService saveService)
        {
            this.constants = constants;
            this.targeting = targeting;
            this.combat = combat;
            this.executor = executor;
            this.sceneFlow = sceneFlow;
            this.progress = progress;
            this.rewards = rewards;
            this.saveService = saveService;
        }

        /// <summary>
        /// 아군 파티와 스테이지 적 편성으로 전투 뷰모델을 조립한다. 장·스테이지·인덱스는 여기서 검증·확정돼
        /// 뷰모델에 불변으로 들어가며, 순수 조립만 하고 아무 것도 구독/시작하지 않는다.
        /// </summary>
        /// <param name="selectedParty">아군 파티. 인덱스 = 진영 자리(0~3), 빈 자리는 null. 유효 아군이 없으면 예외.</param>
        /// <param name="battleSeed">전투 난수 시드. 같은 값이면 데미지·타겟 난수가 완전히 재현된다.</param>
        /// <param name="startAuto">true면 오토 모드로 시작.</param>
        public BattleViewModel Create(
            IReadOnlyList<OwnedCharacter> selectedParty,
            ChapterSO chapter,
            StageSO stage,
            int battleSeed,
            bool startAuto)
        {
            if (selectedParty == null)
                throw new ArgumentNullException(nameof(selectedParty));
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));
            if (stage == null)
                throw new ArgumentNullException(nameof(stage));

            var enemies = stage.enemies;
            if (enemies == null || enemies.Length == 0)
                throw new InvalidOperationException($"스테이지 '{stage.id}'에 적 편성(enemies)이 비어 있다.");
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] == null)
                    throw new InvalidOperationException(
                        $"스테이지 '{stage.id}'의 적 편성 슬롯 {i}가 비어 있다(Inspector EnemySO 참조 누락).");

            // 클리어 마킹이 쓸 인덱스를 지금 확정한다. 장·스테이지 데이터가 어긋난 채 전투가 시작되면
            // 승리 마킹만 조용히 실패하므로, 불일치는 조립 시점에 즉시 드러낸다.
            int stageIndex = Array.IndexOf(chapter.stages ?? Array.Empty<StageSO>(), stage);
            if (stageIndex < 0)
                throw new InvalidOperationException(
                    $"스테이지 '{stage.id}'가 장 '{chapter.id}'의 stages에 없다 — 장·스테이지 선택이 어긋났다.");

            // 인덱스가 곧 진영 자리이므로 빈칸(null)을 걷어내되 남은 유닛의 자리 번호는 원래 인덱스를 유지한다.
            var ownedParty = selectedParty
                .Take(PlayerSave.PartySlotCount)
                .Select((owned, slot) => (owned, slot))
                .Where(x => x.owned != null)
                .ToList();
            if (ownedParty.Count == 0)
                throw new ArgumentException("선택 파티에 유효한 아군이 하나도 없다.", nameof(selectedParty));

            var enemyParty = enemies.Take(PlayerSave.PartySlotCount).ToList();

            // 유닛과 아트를 함께 만들어 넘긴다. 타임라인 아이콘은 아군=얼굴 크롭, 적=배틀러 스프라이트.
            // 아군 얼굴이 비면 그 칸은 비워 그린다. 전신 초상으로 폴백하면 데이터 누락이 감춰지기 때문이다.
            var allyEntries = ownedParty
                .Select(x => new BattleUnitEntry(
                    Combatant.FromCharacter(x.owned, x.slot),
                    x.owned.Definition.portraitAssetRef,
                    x.owned.Definition.faceIconAssetRef))
                .ToList();
            var enemyEntries = enemyParty
                .Select((so, slot) => new BattleUnitEntry(
                    Combatant.FromEnemy(so, slot),
                    so.battlerAssetRef,
                    so.battlerAssetRef))
                .ToList();

            // 아군·적은 독립 타겟 난수 스트림을 사용한다. 둘 다 battleSeed에서 결정론적으로 파생되므로
            // 재현성은 유지되고, 한쪽의 난수 소비가 반대쪽 선택에 영향을 주지 않는다.
            var allyTargetRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.AllyTargeting));
            var enemyTargetRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.EnemyTargeting));

            var autoRule = RuleBasedActionProvider.AllyAuto(targeting, combat, allyTargetRng);
            var manualProvider = new ManualActionProvider(autoRule) { AutoMode = startAuto };
            var enemyAi = RuleBasedActionProvider.EnemyAi(targeting, combat, enemyTargetRng,
                constants.enemyLethalChance, constants.enemyLowHpBias, constants.enemyFrontLineWeight);

            // 도메인(엔진·스케줄러)은 아트를 모르므로 유닛만 뽑아 넘긴다. 순서는 아군 먼저, 그다음 적.
            var allyUnits = allyEntries.Select(e => e.Unit).ToList();
            var enemyUnits = enemyEntries.Select(e => e.Unit).ToList();

            var scheduler = new AtbTurnScheduler(allyUnits.Concat(enemyUnits));
            var engine = new BattleEngine(allyUnits, enemyUnits, scheduler,
                executor, manualProvider, enemyAi, constants.globalActionCap);

            return new BattleViewModel(
                allyEntries,
                enemyEntries,
                engine,
                scheduler,
                manualProvider,
                targeting,
                sceneFlow,
                progress,
                chapter,
                stage,
                stageIndex,
                rewards,
                saveService);
        }
    }
}
