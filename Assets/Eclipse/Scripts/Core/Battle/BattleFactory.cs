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
        // 4v4 그리드가 그릴 수 있는 한 진영 최대 인원. 적은 스테이지 편성 앞에서 이 수만 참전한다(아군 수와 무관).
        private const int MaxPartySize = 4;

        private readonly BattleConstantsSO constants;
        private readonly TargetResolver targeting;
        private readonly CombatPipeline combat;
        private readonly SkillExecutor executor;
        private readonly ISceneFlow sceneFlow;

        public BattleFactory(
            BattleConstantsSO constants,
            TargetResolver targeting,
            CombatPipeline combat,
            SkillExecutor executor,
            ISceneFlow sceneFlow)
        {
            this.constants = constants;
            this.targeting = targeting;
            this.combat = combat;
            this.executor = executor;
            this.sceneFlow = sceneFlow;
        }

        /// <summary>
        /// 편성 화면이 확정한 <paramref name="selectedParty"/>를 아군으로, 스테이지가 정의한
        /// <paramref name="enemies"/>를 적으로 삼아 전투 유닛·타임라인 아트·행동 제공자·엔진을 조립한 뷰모델을 만든다.
        /// 아군의 slotIndex는 인자에서의 인덱스를 그대로 쓴다 — 빈칸(null)은 자리를 비운 채 건너뛰므로
        /// 편성 칸이 전투 진영 자리로 이어진다. 적은 편성 앞에서 최대 4명까지 참전시킨다(아군 수에 묶이지 않는다).
        /// 순수 조립만 하며 아무 것도 구독/시작하지 않는다.
        /// </summary>
        /// <param name="selectedParty">아군 파티. 인덱스 = 진영 자리(0~3), 빈 자리는 null. 유효 아군이 하나도 없으면 예외.</param>
        /// <param name="enemies">스테이지 적 편성. 앞에서부터 최대 4명 참전. null이면 예외.</param>
        /// <param name="battleSeed">전투 난수 시드. 같은 값이면 데미지·타겟 난수가 완전히 재현된다.</param>
        /// <param name="startAuto">true면 오토 모드로 시작(수동 제공자의 AutoMode 초기값).</param>
        /// <returns>씬 진입 즉시 사용할 수 있는 전투 뷰모델. 유닛 순서는 아군 먼저, 그다음 적.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="selectedParty"/>·<paramref name="enemies"/>가 null일 때.</exception>
        /// <exception cref="ArgumentException"><paramref name="selectedParty"/>에 유효한 아군이 하나도 없을 때.</exception>
        public BattleViewModel Create(
            IReadOnlyList<OwnedCharacter> selectedParty,
            IReadOnlyList<EnemySO> enemies,
            int battleSeed,
            bool startAuto)
        {
            if (selectedParty == null)
                throw new ArgumentNullException(nameof(selectedParty));
            if (enemies == null)
                throw new ArgumentNullException(nameof(enemies));

            // 인덱스가 곧 진영 자리이므로 빈칸을 걷어내되 남은 유닛의 자리 번호는 원래 인덱스를 유지한다.
            var ownedParty = selectedParty
                .Take(MaxPartySize)
                .Select((owned, slot) => (owned, slot))
                .Where(x => x.owned != null)
                .ToList();
            if (ownedParty.Count == 0)
                throw new ArgumentException("선택 파티에 유효한 아군이 하나도 없다.", nameof(selectedParty));

            var enemyParty = enemies.Take(MaxPartySize).ToList();

            // 유닛과 그 유닛의 아트를 함께 만들어 넘긴다. 아트는 도메인이 아닌 정의 SO에서 뽑는다.
            // 타임라인 아이콘은 아군=얼굴 크롭, 적=소형 배틀러 스프라이트 그대로. 아군 얼굴이 비면 그 칸은
            // 비어 그려진다 — 전신 초상으로 대신 채우면 데이터 누락이 감춰지므로 폴백하지 않는다.
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

            // 아군·적은 각자 독립 타겟 스트림을 쓴다. 둘 다 battleSeed에서 결정론적으로 파생되므로
            // 재현성은 유지되고, 한쪽의 난수 소비량이 반대쪽 선택을 밀던 결합만 사라진다.
            var allyTargetRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.AllyTargeting));
            var enemyTargetRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.EnemyTargeting));

            var autoRule = RuleBasedActionProvider.AllyAuto(targeting, combat, allyTargetRng);
            var manualProvider = new ManualActionProvider(autoRule) { AutoMode = startAuto };
            var enemyAi = RuleBasedActionProvider.EnemyAi(targeting, combat, enemyTargetRng,
                constants.enemyLethalChance, constants.enemyLowHpBias);

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
                sceneFlow);
        }
    }
}
