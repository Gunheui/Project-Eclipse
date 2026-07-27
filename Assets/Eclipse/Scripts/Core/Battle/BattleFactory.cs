using System;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;

namespace Eclipse.Core
{
    /// <summary>
    /// 전투 뷰모델 조립 전담 팩토리. 방마다 재호출되어 난수·파이프라인·엔진·전투원을 전부 새로 만든다.
    /// 파티·버프·챕터 계수는 런 세션에서 읽고, 최종 스탯은 CharacterStats 한 곳에서만 계산한다.
    /// </summary>
    public sealed class BattleFactory
    {
        private readonly BattleConstantsSO constants;
        private readonly ChapterRunSession session;
        private readonly EncounterTuningSO tuning;

        public BattleFactory(BattleConstantsSO constants, ChapterRunSession session, EncounterTuningSO tuning)
        {
            this.constants = constants;
            this.session = session;
            this.tuning = tuning;
        }

        /// <summary>
        /// 이 방의 인카운터로 전투 뷰모델을 조립한다. 순수 조립만 하고 아무 것도 구독/시작하지 않는다.
        /// </summary>
        /// <param name="encounter">이 방의 적 편성 스펙. 비어 있으면 예외.</param>
        /// <param name="battleSeed">이 방의 전투 시드(런 시드에서 방 인덱스로 파생된 값).</param>
        /// <param name="startAuto">true면 오토 모드로 시작.</param>
        public BattleViewModel Create(EncounterSpec encounter, int battleSeed, bool startAuto)
        {
            var enemies = encounter.Enemies;
            if (enemies == null || enemies.Count == 0)
                throw new InvalidOperationException("인카운터 스펙의 적 편성이 비어 있다.");
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].Enemy == null)
                    throw new InvalidOperationException($"인카운터 스펙의 적 슬롯 {i}가 비어 있다(EnemySO 누락).");

            // 인덱스가 곧 진영 자리이므로 빈칸(null)을 걷어내되 남은 유닛의 자리 번호는 원래 인덱스를 유지한다.
            var ownedParty = session.Party
                .Take(PlayerSave.PartySlotCount)
                .Select((owned, slot) => (owned, slot))
                .Where(x => x.owned != null)
                .ToList();
            if (ownedParty.Count == 0)
                throw new InvalidOperationException("런 세션 파티에 유효한 아군이 하나도 없다.");

            var enemyParty = enemies.Take(PlayerSave.PartySlotCount).ToList();

            // 유닛과 아트를 함께 만들어 넘긴다. 타임라인 아이콘은 아군=얼굴 크롭, 적=배틀러 스프라이트.
            // 아군 얼굴이 비면 그 칸은 비워 그린다. 전신 초상으로 폴백하면 데이터 누락이 감춰지기 때문이다.
            var allyEntries = ownedParty
                .Select(x => new BattleUnitEntry(
                    Combatant.FromCharacter(x.owned, x.slot, CharacterStats.BuildAllyStats(
                        x.owned.Definition, x.owned.Level, x.owned.AscensionTier, session.BuffsOf(x.slot))),
                    x.owned.Definition.portraitAssetRef,
                    x.owned.Definition.faceIconAssetRef))
                .ToList();
            var enemyEntries = enemyParty
                .Select((spec, slot) => new BattleUnitEntry(
                    BuildEnemy(spec, slot),
                    spec.Enemy.battlerAssetRef,
                    spec.Enemy.battlerAssetRef))
                .ToList();

            // 아군·적은 독립 타겟 난수 스트림을 사용한다. 둘 다 battleSeed에서 결정론적으로 파생되므로
            // 재현성은 유지되고, 한쪽의 난수 소비가 반대쪽 선택에 영향을 주지 않는다.
            var allyTargetRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.AllyTargeting));
            var enemyTargetRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.EnemyTargeting));

            // 데미지 난수·파이프라인도 방마다 새로 선다 — 상주 씬에서 7방이 한 시드를 공유하지 않게 한다.
            var damageRng = new SeededRandom(BattleSeed.For(battleSeed, BattleSeed.Stream.Damage));
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(
                constants.defenseK, constants.varianceMin, constants.varianceMax, damageRng));
            var executor = new SkillExecutor(combat, targeting);

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
                targeting);
        }

        // 적 하나를 조립한다. 챕터 계수·변이·정예 배수·런 디버프를 CharacterStats 계산으로 접고, 변이 이름 접두를 붙인다.
        private Combatant BuildEnemy(EnemyInstanceSpec spec, int slot)
        {
            var stats = CharacterStats.BuildEnemyStats(
                spec.Enemy.baseStats,
                session.Chapter.enemyStatMultiplier,
                spec.Mutation,
                spec.IsElite ? tuning.eliteStatMultiplier : 1f,
                session.EnemyDebuffs);
            string name = spec.Mutation != null
                ? spec.Mutation.namePrefix + spec.Enemy.displayName
                : null;
            return Combatant.FromEnemy(spec.Enemy, slot, stats, name);
        }
    }
}