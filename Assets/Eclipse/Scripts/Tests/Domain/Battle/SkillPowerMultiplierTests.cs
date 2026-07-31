using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class SkillPowerMultiplierTests
    {
        private static Stats S(int hp, int atk, int def, int spd)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = 0f, critDamage = 1.5f };

        private static SkillSO Skill(params SkillEffect[] effects)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = "test_skill";
            s.displayName = "test_skill";
            s.cooldownTurns = 0;
            s.effects = effects.ToList();
            return s;
        }

        private static Combatant Ally(int slot, Stats stats, int[] skillLevels, SkillSO basic)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = "테스터";
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
            so.growthCurve.maxLevel = 30;
            so.basicSkill = basic;
            var owned = new OwnedCharacter(so, 1, 0, skillLevels);
            return Combatant.FromCharacter(owned, slot, CharacterStats.BuildAllyStats(so, 1, 0, null));
        }

        private static Combatant Enemy(int slot, Stats stats)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = "허수아비";
            so.baseStats = stats;
            so.basicSkill = Skill(new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = 1f });
            return Combatant.FromEnemy(so, slot, so.baseStats);
        }

        private static SkillExecutor Executor()
        {
            // 분산 1~1 고정으로 난수 영향을 제거한다.
            var pipeline = new DamagePipeline(1f, 1f, 1f, new SeededRandom(1));
            return new SkillExecutor(new CombatPipeline(pipeline), new TargetResolver());
        }

        [Test]
        public void 배수는_강화_1레벨당_10퍼센트포인트_등차다()
        {
            Assert.AreEqual(1.00f, SkillRuntime.PowerMultiplierFor(1), 1e-5f);
            Assert.AreEqual(1.10f, SkillRuntime.PowerMultiplierFor(2), 1e-5f);
            Assert.AreEqual(1.20f, SkillRuntime.PowerMultiplierFor(3), 1e-5f);
            Assert.AreEqual(1.40f, SkillRuntime.PowerMultiplierFor(OwnedCharacter.MaxSkillLevel), 1e-5f, "상한 5 = ×1.40");
        }

        [Test]
        public void 아군은_슬롯별_스킬레벨이_배수로_전달되고_적은_항상_1이다()
        {
            var basic = Skill(new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = 1f });
            var ally = Ally(0, S(1000, 100, 50, 100), new[] { 3, 1, 1 }, basic);
            var enemy = Enemy(0, S(1000, 100, 50, 100));

            Assert.AreEqual(1.20f, ally.Skills[0].PowerMultiplier, 1e-5f, "스킬레벨 3 → ×1.20");
            Assert.AreEqual(1f, enemy.Skills[0].PowerMultiplier, 1e-5f, "적은 스킬레벨이 없다");
        }

        [Test]
        public void 위력형_피해만_배수만큼_늘고_비율형_버프_값은_불변이다()
        {
            var skill = Skill(
                new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = 1f },
                new SkillEffect { type = EffectType.Debuff, target = TargetSelector.SingleEnemy, value = 0.30f, affectedStat = StatType.Atk, duration = 2 });
            var actor = Ally(0, S(1000, 100, 0, 100), new[] { 1, 1, 1 }, skill);
            var executor = Executor();

            var plain = Enemy(0, S(10000, 100, 0, 100));
            executor.ApplySkill(actor, new SkillRuntime(skill), plain,
                new List<ICombatant> { actor }, new List<ICombatant> { plain });
            int plainDamage = plain.MaxHp - plain.CurrentHp;

            var boosted = Enemy(0, S(10000, 100, 0, 100));
            executor.ApplySkill(actor, new SkillRuntime(skill, 0, 2f), boosted,
                new List<ICombatant> { actor }, new List<ICombatant> { boosted });
            int boostedDamage = boosted.MaxHp - boosted.CurrentHp;

            Assert.LessOrEqual(System.Math.Abs(boostedDamage - plainDamage * 2), 1, "위력형은 배수에 비례(반올림 오차 1 허용)");
            Assert.AreEqual(0.30f, boosted.Effects.Single(e => e.Type == EffectType.Debuff).Value, 1e-5f,
                "비율형 효과 값에는 배수를 곱하지 않는다");
        }
    }
}
