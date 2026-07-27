using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class EncounterGeneratorTests
    {
        // 미리 정한 값을 순서대로(부족하면 순환) 돌려주는 정수 난수 스텁.
        // 상한을 넘는 값은 실제 난수라면 나올 수 없으므로 테스트 작성 실수로 보고 즉시 실패시킨다.
        private sealed class FixedRunRandom : IRunRandom
        {
            private readonly int[] _values;
            private int _index;

            public FixedRunRandom(params int[] values) { _values = values; }

            public int Calls { get; private set; }

            public int NextInt(int maxExclusive)
            {
                Calls++;
                int value = _values[_index++ % _values.Length];
                Assert.Less(value, maxExclusive, "스텁 값 {0}이 상한 {1}을 넘는다", value, maxExclusive);
                return value;
            }
        }

        private const int Hit = 0;        // 변이 적중 판정에서 항상 적중하는 롤
        private const int Miss = 9999;    // 변이 적중 판정에서 항상 빗나가는 롤

        private static EnemySO Enemy(string id)
        {
            var enemy = ScriptableObject.CreateInstance<EnemySO>();
            enemy.id = id;
            return enemy;
        }

        private static MutationSO Mutation(string id, StatType axis, float multiplier)
        {
            var mutation = ScriptableObject.CreateInstance<MutationSO>();
            mutation.id = id;
            mutation.statAxis = axis;
            mutation.multiplier = multiplier;
            return mutation;
        }

        private static DepthPool Depth(int depth, int min, int max, float chance, params EnemySO[] pool)
            => new DepthPool { depth = depth, allowedPool = pool, minCount = min, maxCount = max, mutationChance = chance };

        private static EncounterTuningSO Tuning(params DepthPool[] depths)
        {
            var tuning = ScriptableObject.CreateInstance<EncounterTuningSO>();
            tuning.depths = depths;
            tuning.boss = Enemy("boss");
            tuning.bossAdds = new[] { Enemy("add") };
            tuning.mutations = new[]
            {
                Mutation("mut_hp", StatType.Hp, 1.5f),
                Mutation("mut_atk", StatType.Atk, 1.3f),
                Mutation("mut_spd", StatType.Spd, 1.2f),
            };
            tuning.eliteStatMultiplier = 1.15f;
            return tuning;
        }

        // 깊이 1만 검사하는 표적 테스트용. 나머지 깊이는 로드 검증의 5행 요건을 채우려고 넣는다.
        private static EncounterTuningSO FocusedTuning(int min, int max, float chance, params EnemySO[] pool)
        {
            var filler = Enemy("filler");
            return Tuning(
                Depth(1, min, max, chance, pool),
                Depth(2, 1, 1, 0f, filler),
                Depth(3, 1, 1, 0f, filler),
                Depth(4, 1, 1, 0f, filler),
                Depth(5, 1, 1, 0f, filler));
        }

        // 스펙을 문자열로 눌러 회귀 비교에 쓴다.
        private static IEnumerable<string> Describe(EncounterSpec spec)
            => spec.Enemies.Select(e => $"{e.Enemy.id}/{(e.Mutation == null ? "-" : e.Mutation.id)}/{e.IsElite}");

        private static EncounterTuningSO FiveDepthTuning(float chance)
        {
            var slime = Enemy("slime");
            var hound = Enemy("hound");
            var beast = Enemy("beast");
            var guard = Enemy("guard");
            return Tuning(
                Depth(1, 2, 3, chance, slime),
                Depth(2, 2, 3, chance, slime, hound),
                Depth(3, 3, 4, chance, slime, hound, beast),
                Depth(4, 3, 4, chance, slime, hound, beast, guard),
                Depth(5, 3, 4, chance, slime, hound, beast, guard));
        }

        private static EncounterGenerator Seeded(EncounterTuningSO tuning, int runSeed)
            => new EncounterGenerator(tuning,
                new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Encounter)),
                new SeededRandom(RunSeed.For(runSeed, RunSeed.Stream.Mutation)));

        [Test]
        public void 같은_시드는_같은_인카운터를_낸다()
        {
            var first = Seeded(FiveDepthTuning(0.45f), 20260727);
            var second = Seeded(FiveDepthTuning(0.45f), 20260727);

            for (int depth = 1; depth <= 5; depth++)
                CollectionAssert.AreEqual(
                    Describe(first.Generate(depth, false)).ToList(),
                    Describe(second.Generate(depth, false)).ToList(),
                    "깊이 {0}에서 같은 시드인데 인카운터가 갈렸다", depth);
        }

        [Test]
        public void 변이_소비량이_달라져도_몹_선택은_그대로다()
        {
            var never = Seeded(FiveDepthTuning(0f), 555);
            var always = Seeded(FiveDepthTuning(1f), 555);

            for (int depth = 1; depth <= 5; depth++)
            {
                var neverIds = never.Generate(depth, false).Enemies.Select(e => e.Enemy.id).ToList();
                var alwaysIds = always.Generate(depth, false).Enemies.Select(e => e.Enemy.id).ToList();
                CollectionAssert.AreEqual(neverIds, alwaysIds,
                    "깊이 {0}에서 변이 스트림 소비가 인카운터 스트림을 밀었다", depth);
            }
        }

        [Test]
        public void 마리수가_범위를_지킨다()
        {
            var generator = Seeded(FiveDepthTuning(0.3f), 8899);

            for (int i = 0; i < 200; i++)
                for (int depth = 1; depth <= 5; depth++)
                {
                    int count = generator.Generate(depth, false).Enemies.Count;
                    Assert.That(count, Is.InRange(depth <= 2 ? 2 : 3, depth <= 2 ? 3 : 4),
                        "깊이 {0}의 마리수 {1}이 범위를 벗어났다", depth, count);
                }
        }

        [Test]
        public void 풀에서_고른_몹이_추첨_결과와_일치한다()
        {
            var a = Enemy("a");
            var b = Enemy("b");
            var c = Enemy("c");
            var tuning = FocusedTuning(1, 3, 0f, a, b, c);
            // 마리수 롤 1 → 1+1 = 2마리, 이어서 풀 인덱스 2·0.
            var generator = new EncounterGenerator(tuning,
                new FixedRunRandom(1, 2, 0), new FixedRunRandom(Miss));

            var enemies = generator.Generate(1, false).Enemies;

            Assert.AreEqual(2, enemies.Count);
            Assert.AreEqual("c", enemies[0].Enemy.id);
            Assert.AreEqual("a", enemies[1].Enemy.id);
        }

        [Test]
        public void 변이는_마리마다_독립으로_추첨한다()
        {
            var tuning = FocusedTuning(2, 2, 0.5f, Enemy("a"));
            // 1마리째: 적중 → 변이 인덱스 1 / 2마리째: 빗나감.
            var generator = new EncounterGenerator(tuning,
                new FixedRunRandom(0), new FixedRunRandom(Hit, 1, Miss));

            var enemies = generator.Generate(1, false).Enemies;

            Assert.AreEqual("mut_atk", enemies[0].Mutation.id);
            Assert.IsNull(enemies[1].Mutation);
        }

        [Test]
        public void 변이가_적중하면_추첨_결과의_변이를_붙인다()
        {
            var tuning = FocusedTuning(1, 1, 0.5f, Enemy("a"));
            var generator = new EncounterGenerator(tuning,
                new FixedRunRandom(0), new FixedRunRandom(Hit, 2));

            Assert.AreEqual("mut_spd", generator.Generate(1, false).Enemies[0].Mutation.id);
        }

        [Test]
        public void 변이_확률이_0이어도_마리당_한_번_굴린다()
        {
            var tuning = FocusedTuning(2, 2, 0f, Enemy("a"));
            var mutationRng = new FixedRunRandom(Miss);
            new EncounterGenerator(tuning, new FixedRunRandom(0), mutationRng).Generate(1, false);

            Assert.AreEqual(2, mutationRng.Calls, "확률 0에서도 적중 판정 롤은 마리당 1회여야 한다");
        }

        [Test]
        public void 변이_확률이_100이면_마리당_적중_판정과_선택을_굴린다()
        {
            var tuning = FocusedTuning(2, 2, 1f, Enemy("a"));
            var mutationRng = new FixedRunRandom(9999, 0);
            new EncounterGenerator(tuning, new FixedRunRandom(0), mutationRng).Generate(1, false);

            Assert.AreEqual(4, mutationRng.Calls, "적중 판정 + 변이 선택 = 마리당 2회여야 한다");
        }

        [Test]
        public void 정예는_마리수_상한_고정에_전원_변이_정예다()
        {
            var tuning = FocusedTuning(1, 3, 0f, Enemy("a"));
            var encounterRng = new FixedRunRandom(0);
            var enemies = new EncounterGenerator(tuning, encounterRng, new FixedRunRandom(9999, 0))
                .Generate(1, elite: true).Enemies;

            Assert.AreEqual(3, enemies.Count);
            Assert.IsTrue(enemies.All(e => e.IsElite));
            Assert.IsTrue(enemies.All(e => e.Mutation != null), "정예는 변이 확률 100%다");
            Assert.AreEqual(3, encounterRng.Calls, "마리수가 고정이면 마리수 롤을 소비하지 않는다");
        }

        [Test]
        public void 보스_방은_변이도_정예도_없는_고정_편성이다()
        {
            var tuning = FocusedTuning(1, 1, 1f, Enemy("a"));
            var encounterRng = new FixedRunRandom(0);
            var mutationRng = new FixedRunRandom(0);

            var enemies = new EncounterGenerator(tuning, encounterRng, mutationRng)
                .Generate(EncounterGenerator.BossDepth, elite: true).Enemies;

            CollectionAssert.AreEqual(new[] { "boss", "add" }, enemies.Select(e => e.Enemy.id).ToList());
            Assert.IsTrue(enemies.All(e => e.Mutation == null && !e.IsElite));
            Assert.AreEqual(0, encounterRng.Calls + mutationRng.Calls, "고정 편성은 난수를 쓰지 않는다");
        }

        [Test]
        public void 튜닝에_없는_깊이는_예외()
        {
            var generator = Seeded(FiveDepthTuning(0f), 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(7, false));
        }

        private static void AssertInvalid(Action<EncounterTuningSO> corrupt)
        {
            var tuning = FiveDepthTuning(0.3f);
            corrupt(tuning);
            Assert.Throws<ArgumentException>(
                () => new EncounterGenerator(tuning, new FixedRunRandom(0), new FixedRunRandom(0)));
        }

        [Test]
        public void 깊이가_빠지거나_겹치면_예외()
        {
            AssertInvalid(t => t.depths[4].depth = 4);                                  // 중복
            AssertInvalid(t => t.depths = Array.Empty<DepthPool>());
            AssertInvalid(t => t.depths = t.depths.Take(4).ToArray());                  // 보스 직전 깊이가 빔
            AssertInvalid(t => t.depths =                                               // 보스 깊이를 침범
                t.depths.Append(Depth(6, 3, 4, 0f, t.depths[0].allowedPool[0])).ToArray());
        }

        [Test]
        public void 몹_풀이_비었거나_빈_칸이_있으면_예외()
        {
            AssertInvalid(t => t.depths[0].allowedPool = Array.Empty<EnemySO>());
            AssertInvalid(t => t.depths[0].allowedPool = new EnemySO[] { null });
        }

        [Test]
        public void 마리수_범위가_잘못되면_예외()
        {
            AssertInvalid(t => t.depths[0].minCount = 0);
            AssertInvalid(t => t.depths[0].minCount = 4);   // min > max
            AssertInvalid(t => t.depths[0].maxCount = 5);   // 슬롯 4칸 초과
        }

        [Test]
        public void 변이_확률이_0에서_1을_벗어나면_예외()
        {
            AssertInvalid(t => t.depths[0].mutationChance = -0.1f);
            AssertInvalid(t => t.depths[0].mutationChance = 1.1f);
        }

        [Test]
        public void 보스가_없거나_일반_풀에_섞이면_예외()
        {
            AssertInvalid(t => t.boss = null);
            AssertInvalid(t => t.boss = t.depths[0].allowedPool[0]);
        }

        [Test]
        public void 보스_수하가_비었거나_보스와_겹치면_예외()
        {
            AssertInvalid(t => t.bossAdds = new EnemySO[] { null });
            AssertInvalid(t => t.bossAdds = new[] { t.boss });                          // 보스가 2기가 된다
            AssertInvalid(t => t.bossAdds = t.bossAdds.Append(t.boss).ToArray());
        }

        [Test]
        public void 변이_후보가_비었거나_유효하지_않으면_예외()
        {
            AssertInvalid(t => t.mutations = Array.Empty<MutationSO>());
            AssertInvalid(t => t.mutations = new MutationSO[] { null });
            AssertInvalid(t => t.mutations[0].statAxis = StatType.None);
            AssertInvalid(t => t.mutations[0].statAxis = StatType.CritRate);   // 배수를 받지 않는 축
            AssertInvalid(t => t.mutations[0].statAxis = StatType.CritDamage);
            AssertInvalid(t => t.mutations[0].multiplier = 0f);
        }
    }
}
