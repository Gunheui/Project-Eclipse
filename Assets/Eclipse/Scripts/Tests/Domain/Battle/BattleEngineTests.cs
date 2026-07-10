using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class BattleEngineTests
    {
        // --- 인메모리 데이터 빌더 (에셋 없이 SO를 즉석 생성) ---

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

        private static BattleUnit Ally(string name, int slot, Stats stats, SkillSO basic, SkillSO normal = null)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = name;
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>(); // Lv1이라 스케일 없음
            so.basicSkill = basic;
            so.normalSkill = normal;
            return BattleUnit.FromCharacter(new OwnedCharacter(so, 1), slot);
        }

        private static BattleUnit Enemy(string name, int slot, Stats stats, SkillSO basic)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = name;
            so.baseStats = stats;
            so.basicSkill = basic;
            return BattleUnit.FromEnemy(so, slot);
        }

        private static BattleEngine Engine(List<BattleUnit> allies, List<BattleUnit> enemies, int seed, int cap)
        {
            var scheduler = new AtbTurnScheduler(allies.Concat(enemies));
            var pipeline = new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(seed));
            var executor = new SkillExecutor(new CombatPipeline(pipeline), new TargetResolver());
            return new BattleEngine(allies, enemies, scheduler, executor, cap);
        }

        // --- 완주 + 승패 판정 (DoD #1 선행) ---

        [Test]
        public void 강한_파티는_행동상한_내_승리()
        {
            var allies = new List<BattleUnit> { Ally("전사", 0, S(2000, 500, 0, 100), Skill("b", 0, Dmg(1f, TargetSelector.LowestHpEnemy))) };
            var enemies = new List<BattleUnit> { Enemy("고블린", 0, S(100, 10, 0, 90), Skill("eb", 0, Dmg(1f, TargetSelector.LowestHpEnemy))) };
            var engine = Engine(allies, enemies, seed: 1, cap: 200);

            Assert.AreEqual(BattleOutcome.Victory, engine.Run());
            Assert.Less(engine.ActionCount, 200);
        }

        [Test]
        public void 약한_파티는_전멸로_패배()
        {
            var allies = new List<BattleUnit> { Ally("견습", 0, S(100, 10, 0, 90), Skill("b", 0, Dmg(1f, TargetSelector.LowestHpEnemy))) };
            var enemies = new List<BattleUnit> { Enemy("오우거", 0, S(2000, 500, 0, 100), Skill("eb", 0, Dmg(1f, TargetSelector.LowestHpEnemy))) };
            var engine = Engine(allies, enemies, seed: 1, cap: 200);

            Assert.AreEqual(BattleOutcome.Defeat, engine.Run());
        }

        [Test]
        public void 아무도_못죽이면_행동상한_초과로_패배()
        {
            var allies = new List<BattleUnit> { Ally("벽A", 0, S(100000, 1, 0, 100), Skill("b", 0, Dmg(0.0001f, TargetSelector.LowestHpEnemy))) };
            var enemies = new List<BattleUnit> { Enemy("벽B", 0, S(100000, 1, 0, 100), Skill("eb", 0, Dmg(0.0001f, TargetSelector.LowestHpEnemy))) };
            var engine = Engine(allies, enemies, seed: 1, cap: 5);

            Assert.AreEqual(BattleOutcome.Defeat, engine.Run());
            Assert.AreEqual(5, engine.ActionCount);
        }

        // --- 쿨다운 FSM (DoD #5) ---

        [Test]
        public void 쿨다운_사용후_잠금_매턴감소_기본공격은_항상_준비()
        {
            // 아군이 압도적으로 빨라(SPD 10000 vs 1) 첫 여러 턴이 전부 아군 차례가 되도록 한 1v1.
            // 적은 벽(HP 큼)이라 전투가 안 끝나 아군 쿨 흐름을 연속 관측할 수 있다.
            var normalSkill = Skill("n", 2, Dmg(0.0001f, TargetSelector.LowestHpEnemy));
            var basicSkill = Skill("b", 0, Dmg(0.0001f, TargetSelector.LowestHpEnemy));
            var ally = Ally("힐러", 0, S(100000, 100, 0, 10000), basicSkill, normalSkill);
            var enemy = Enemy("벽", 0, S(1000000, 1, 0, 1), Skill("eb", 0, Dmg(0.0001f, TargetSelector.LowestHpEnemy)));
            var engine = Engine(new List<BattleUnit> { ally }, new List<BattleUnit> { enemy }, seed: 1, cap: 1000);

            var basic = ally.Skills[0];  // 쿨 0
            var normal = ally.Skills[1]; // 쿨 2

            engine.AdvanceTurn(); // 턴1: 상위 슬롯(normal) 사용 → 잠김
            Assert.IsFalse(normal.IsReady);
            Assert.AreEqual(2, normal.CurrentCooldown);
            Assert.IsTrue(basic.IsReady, "기본공격은 쿨 0이라 항상 준비");

            engine.AdvanceTurn(); // 턴2: normal 쿨 −1(=1) → 아직 못 씀, 기본공격 폴백
            Assert.AreEqual(1, normal.CurrentCooldown);
            Assert.IsTrue(basic.IsReady);

            engine.AdvanceTurn(); // 턴3: normal 쿨 −1(=0) → 다시 사용 → 재잠금(2)
            Assert.AreEqual(2, normal.CurrentCooldown);
            Assert.IsTrue(basic.IsReady);
        }

        // --- 결정성 (시드 고정 회귀) ---

        [Test]
        public void 같은_시드_같은_편성은_같은_결과와_행동수()
        {
            List<BattleUnit> Allies() => new List<BattleUnit> { Ally("A", 0, S(500, 100, 20, 110, cr: 0.3f, cd: 2f), Skill("b", 0, Dmg(1f, TargetSelector.LowestHpEnemy))) };
            List<BattleUnit> Enemies() => new List<BattleUnit> { Enemy("E", 0, S(400, 80, 10, 100, cr: 0.3f, cd: 2f), Skill("eb", 0, Dmg(1f, TargetSelector.LowestHpEnemy))) };

            var e1 = Engine(Allies(), Enemies(), seed: 777, cap: 200);
            var o1 = e1.Run();
            var e2 = Engine(Allies(), Enemies(), seed: 777, cap: 200);
            var o2 = e2.Run();

            Assert.AreEqual(o1, o2, "같은 시드·편성은 같은 승패");
            Assert.AreEqual(e1.ActionCount, e2.ActionCount, "같은 시드·편성은 같은 행동 수");
        }
    }
}