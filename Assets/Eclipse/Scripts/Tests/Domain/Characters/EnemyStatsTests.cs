using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class EnemyStatsTests
    {
        private static Stats Base(int hp = 100, int atk = 100, int def = 100, int spd = 100,
            float critRate = 0.3f, float critDamage = 1.5f)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = critRate, critDamage = critDamage };

        private static MutationSO Mutation(StatType axis, float multiplier)
        {
            var mutation = ScriptableObject.CreateInstance<MutationSO>();
            mutation.statAxis = axis;
            mutation.multiplier = multiplier;
            return mutation;
        }

        private static StatModifierSet Debuffs(StatType axis, float value)
        {
            var set = new StatModifierSet();
            set.Add(new StatDelta { axis = axis, value = value });
            return set;
        }

        [Test]
        public void 챕터_변이_정예_배수를_모두_곱한다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 2f, Mutation(StatType.Hp, 1.5f), 1.5f, null);

            Assert.AreEqual(450, stats.hp);   // 100 × 2 × 1.5(정예) × 1.5(변이)
        }

        [Test]
        public void 변이는_지정한_스탯에만_걸린다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 2f, Mutation(StatType.Hp, 1.5f), 1.5f, null);

            Assert.AreEqual(300, stats.atk);  // 100 × 2 × 1.5(정예)
            Assert.AreEqual(300, stats.def);
            Assert.AreEqual(300, stats.spd);
        }

        [Test]
        public void 디버프_합만큼_깎는다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 1f, null, 1f, Debuffs(StatType.Def, -0.25f));

            Assert.AreEqual(75, stats.def);
            Assert.AreEqual(100, stats.atk, "디버프는 지정한 스탯만 건드린다");
        }

        [Test]
        public void 정수_스탯의_하한은_1이다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(hp: 1), 1f, null, 1f, Debuffs(StatType.Hp, -0.99f));

            Assert.AreEqual(1, stats.hp);
        }

        [Test]
        public void 반올림은_마지막에_한_번_AwayFromZero다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(hp: 5), 0.5f, null, 1f, null);

            Assert.AreEqual(3, stats.hp);   // 2.5 → 3
        }

        [Test]
        public void 치명_계열에는_배수가_걸리지_않는다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 2f, Mutation(StatType.Atk, 1.3f), 1.5f, null);

            Assert.AreEqual(0.3f, stats.critRate, 1e-5f);
            Assert.AreEqual(1.5f, stats.critDamage, 1e-5f);
        }

        [Test]
        public void 치명확률은_0아래로_내려가지_않는다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 1f, null, 1f, Debuffs(StatType.CritRate, -0.5f));

            Assert.AreEqual(0f, stats.critRate);
        }

        [Test]
        public void 치명피해는_0아래로_내려가지_않는다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 1f, null, 1f, Debuffs(StatType.CritDamage, -2f));

            Assert.AreEqual(0f, stats.critDamage);
        }

        [Test]
        public void 변이와_디버프가_없어도_계산된다()
        {
            var stats = CharacterStats.BuildEnemyStats(Base(), 1f, null, 1f, null);

            Assert.AreEqual(100, stats.hp);
            Assert.AreEqual(0.3f, stats.critRate, 1e-5f);
        }
    }
}
