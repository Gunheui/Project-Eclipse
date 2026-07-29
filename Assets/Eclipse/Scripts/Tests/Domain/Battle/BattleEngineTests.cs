using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    public class BattleEngineTests
    {
        // --- 인메모리 데이터 조립 (에셋 없이 SO를 즉석 생성) ---

        private static Stats S(int hp, int atk, int def, int spd, float cr = 0f, float cd = 1.5f)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = cr, critDamage = cd };

        private static SkillEffect Dmg(float power, TargetSelector t)
            => new SkillEffect { type = EffectType.Damage, target = t, value = power };

        private static SkillSO Skill(string id, int cooldown, params SkillEffect[] effects)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = id;
            s.displayName = id;
            s.cooldownTurns = cooldown;
            s.effects = effects.ToList();
            return s;
        }

        private static Combatant Ally(string name, int slot, Stats stats, SkillSO basic, SkillSO normal = null)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = name;
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>(); // Lv1이라 스케일 없음
            so.growthCurve.maxLevel = 30;
            so.basicSkill = basic;
            so.normalSkill = normal;
            var owned = new OwnedCharacter(so, 1);
            return Combatant.FromCharacter(owned, slot,
                CharacterStats.BuildAllyStats(so, 1, 0, null));
        }

        private static Combatant Enemy(string name, int slot, Stats stats, SkillSO basic)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = name;
            so.baseStats = stats;
            so.basicSkill = basic;
            return Combatant.FromEnemy(so, slot, so.baseStats);
        }

        private static BattleEngine Engine(List<Combatant> allies, List<Combatant> enemies, int seed, int cap)
        {
            var scheduler = new AtbTurnScheduler(allies.Concat(enemies));
            var pipeline = new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(seed));
            var combat = new CombatPipeline(pipeline);
            var targeting = new TargetResolver();
            var executor = new SkillExecutor(combat, targeting);

            // 타겟 난수는 데미지 난수와 분리된 스트림. 아군/적은 리졸버·미리보기는 공유하되 타겟 난수는 각자 독립 스트림이다.
            var allyProvider = RuleBasedActionProvider.AllyAuto(targeting, combat,
                new SeededRandom(BattleSeed.For(seed, BattleSeed.Stream.AllyTargeting)));
            var enemyProvider = RuleBasedActionProvider.EnemyAi(targeting, combat,
                new SeededRandom(BattleSeed.For(seed, BattleSeed.Stream.EnemyTargeting)), 0.6f, 0.5f);

            return new BattleEngine(allies, enemies, scheduler, executor, allyProvider, enemyProvider, cap);
        }

        // --- 완주 + 승패 판정 ---

        [UnityTest]
        public IEnumerator 강한_파티는_행동상한_내_승리() => UniTask.ToCoroutine(async () =>
        {
            var allies = new List<Combatant> { Ally("전사", 0, S(2000, 500, 0, 100), Skill("b", 0, Dmg(1f, TargetSelector.SingleEnemy))) };
            var enemies = new List<Combatant> { Enemy("고블린", 0, S(100, 10, 0, 90), Skill("eb", 0, Dmg(1f, TargetSelector.SingleEnemy))) };
            var engine = Engine(allies, enemies, seed: 1, cap: 200);

            Assert.AreEqual(BattleOutcome.Victory, await engine.RunAsync(CancellationToken.None));
            Assert.Less(engine.ActionCount, 200);
        });

        [UnityTest]
        public IEnumerator 약한_파티는_전멸로_패배() => UniTask.ToCoroutine(async () =>
        {
            var allies = new List<Combatant> { Ally("견습", 0, S(100, 10, 0, 90), Skill("b", 0, Dmg(1f, TargetSelector.SingleEnemy))) };
            var enemies = new List<Combatant> { Enemy("오우거", 0, S(2000, 500, 0, 100), Skill("eb", 0, Dmg(1f, TargetSelector.SingleEnemy))) };
            var engine = Engine(allies, enemies, seed: 1, cap: 200);

            Assert.AreEqual(BattleOutcome.Defeat, await engine.RunAsync(CancellationToken.None));
        });

        [UnityTest]
        public IEnumerator 아무도_못죽이면_행동상한_초과로_패배() => UniTask.ToCoroutine(async () =>
        {
            var allies = new List<Combatant> { Ally("벽A", 0, S(100000, 1, 0, 100), Skill("b", 0, Dmg(0.0001f, TargetSelector.SingleEnemy))) };
            var enemies = new List<Combatant> { Enemy("벽B", 0, S(100000, 1, 0, 100), Skill("eb", 0, Dmg(0.0001f, TargetSelector.SingleEnemy))) };
            var engine = Engine(allies, enemies, seed: 1, cap: 5);

            Assert.AreEqual(BattleOutcome.Defeat, await engine.RunAsync(CancellationToken.None));
            Assert.AreEqual(5, engine.ActionCount);
        });

        // --- 쿨다운 FSM ---

        [UnityTest]
        public IEnumerator 쿨다운_사용후_잠금_매턴감소_기본공격은_항상_준비() => UniTask.ToCoroutine(async () =>
        {
            // 아군이 압도적으로 빨라(SPD 10000 vs 1) 첫 여러 턴이 전부 아군 차례가 되도록 한 1v1.
            // 적은 벽(HP 큼)이라 전투가 안 끝나 아군 쿨 흐름을 연속 관측할 수 있다.
            var normalSkill = Skill("n", 2, Dmg(0.0001f, TargetSelector.SingleEnemy));
            var basicSkill = Skill("b", 0, Dmg(0.0001f, TargetSelector.SingleEnemy));
            var ally = Ally("힐러", 0, S(100000, 100, 0, 10000), basicSkill, normalSkill);
            var enemy = Enemy("벽", 0, S(1000000, 1, 0, 1), Skill("eb", 0, Dmg(0.0001f, TargetSelector.SingleEnemy)));
            var engine = Engine(new List<Combatant> { ally }, new List<Combatant> { enemy }, seed: 1, cap: 1000);

            var basic = ally.Skills[0];  // 쿨 0
            var normal = ally.Skills[1]; // 쿨 2

            await engine.AdvanceTurnAsync(CancellationToken.None); // 턴1: 상위 슬롯(normal) 사용 → 잠김
            Assert.IsFalse(normal.IsReady);
            Assert.AreEqual(2, normal.CurrentCooldown);
            Assert.IsTrue(basic.IsReady, "기본공격은 쿨 0이라 항상 준비");

            await engine.AdvanceTurnAsync(CancellationToken.None); // 턴2: normal 쿨 −1(=1) → 아직 못 씀, 기본공격 폴백
            Assert.AreEqual(1, normal.CurrentCooldown);
            Assert.IsTrue(basic.IsReady);

            await engine.AdvanceTurnAsync(CancellationToken.None); // 턴3: normal 쿨 −1(=0) → 다시 사용 → 재잠금(2)
            Assert.AreEqual(2, normal.CurrentCooldown);
            Assert.IsTrue(basic.IsReady);
        });

        /// <summary> 준비 여부와 무관하게 지정 슬롯 스킬만 내는 프로바이더. 화면이 쿨 중 스킬을 보고한 상황을 재현한다. </summary>
        private sealed class FixedSlotProvider : IActionProvider
        {
            private readonly int _slot;

            public FixedSlotProvider(int slot) => _slot = slot;

            public UniTask<BattleAction> ChooseActionAsync(ICombatant actor,
                IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies, CancellationToken ct)
                => UniTask.FromResult(new BattleAction(actor.Skills[_slot]));
        }

        [UnityTest]
        public IEnumerator 쿨_중인_스킬을_보고해도_발동하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            // 아군이 압도적으로 빨라 두 턴 모두 아군 차례다. 적은 벽이라 전투가 안 끝난다.
            var ally = Ally("공격수", 0, S(100000, 100, 0, 10000),
                Skill("b", 0, Dmg(0.0001f, TargetSelector.SingleEnemy)),
                Skill("n", 2, Dmg(1f, TargetSelector.SingleEnemy)));
            var enemy = Enemy("벽", 0, S(1000000, 1, 0, 1), Skill("eb", 0, Dmg(0.0001f, TargetSelector.SingleEnemy)));

            var allies = new List<Combatant> { ally };
            var enemies = new List<Combatant> { enemy };
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(1)));
            var engine = new BattleEngine(allies, enemies,
                new AtbTurnScheduler(allies.Concat(enemies)),
                new SkillExecutor(combat, targeting),
                new FixedSlotProvider(1), // 아군은 쿨 2짜리 일반기만 계속 보고한다
                RuleBasedActionProvider.EnemyAi(targeting, combat, new SeededRandom(2), 0.6f, 0.5f),
                200);

            await engine.AdvanceTurnAsync(CancellationToken.None); // 턴1: 준비 상태라 발동 → 쿨 잠김
            int hpAfterUse = enemy.CurrentHp;
            Assert.IsTrue(engine.LastTurn.UsedSkill);
            Assert.IsFalse(ally.Skills[1].IsReady);

            await engine.AdvanceTurnAsync(CancellationToken.None); // 턴2: 쿨 중인 같은 스킬을 다시 보고
            Assert.IsFalse(engine.LastTurn.UsedSkill, "쿨 중 스킬은 발동하지 않는다");
            Assert.AreEqual(hpAfterUse, enemy.CurrentHp, "데미지도 들어가지 않는다");
        });

        // --- 결정성 (시드 고정 회귀) ---

        [UnityTest]
        public IEnumerator 같은_시드_같은_편성은_같은_결과와_행동수() => UniTask.ToCoroutine(async () =>
        {
            List<Combatant> Allies() => new List<Combatant> { Ally("A", 0, S(500, 100, 20, 110, cr: 0.3f, cd: 2f), Skill("b", 0, Dmg(1f, TargetSelector.SingleEnemy))) };
            List<Combatant> Enemies() => new List<Combatant> { Enemy("E", 0, S(400, 80, 10, 100, cr: 0.3f, cd: 2f), Skill("eb", 0, Dmg(1f, TargetSelector.SingleEnemy))) };

            var e1 = Engine(Allies(), Enemies(), seed: 777, cap: 200);
            var o1 = await e1.RunAsync(CancellationToken.None);
            var e2 = Engine(Allies(), Enemies(), seed: 777, cap: 200);
            var o2 = await e2.RunAsync(CancellationToken.None);

            Assert.AreEqual(o1, o2, "같은 시드·편성은 같은 승패");
            Assert.AreEqual(e1.ActionCount, e2.ActionCount, "같은 시드·편성은 같은 행동 수");
        });

        // --- 시작 쿨 비대칭 (적만 액티브 쿨 걸고 시작) ---

        [Test]
        public void 적_액티브는_쿨_걸고_시작하고_아군은_바로_사용가능()
        {
            var ally = Ally("A", 0, S(100, 10, 0, 100),
                Skill("b", 0, Dmg(1f, TargetSelector.SingleEnemy)),
                Skill("na", 2, Dmg(1f, TargetSelector.SingleEnemy)));

            var eso = ScriptableObject.CreateInstance<EnemySO>();
            eso.displayName = "E"; eso.baseStats = S(100, 10, 0, 100);
            eso.basicSkill = Skill("eb", 0, Dmg(1f, TargetSelector.SingleEnemy));
            eso.normalSkill = Skill("en", 2, Dmg(1f, TargetSelector.SingleEnemy));
            var enemy = Combatant.FromEnemy(eso, 0, eso.baseStats);

            // 아군: 기본·일반 모두 시작부터 준비.
            Assert.IsTrue(ally.Skills[0].IsReady);
            Assert.IsTrue(ally.Skills[1].IsReady, "아군 액티브는 1턴부터 사용가능");

            // 적: 기본은 열려 있고(쿨 0), 일반은 자기 쿨만큼 잠긴 채 시작.
            Assert.IsTrue(enemy.Skills[0].IsReady, "적 기본공격은 쿨 0이라 항상 열림");
            Assert.IsFalse(enemy.Skills[1].IsReady, "적 액티브는 쿨 걸고 시작");
            Assert.AreEqual(2, enemy.Skills[1].CurrentCooldown);
        }
    }
}