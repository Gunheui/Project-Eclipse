using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 방에 진입할 때 적 편성을 만든다. 일반 방은 마리수·몹·변이를 굴려 조합하고, 보스 방은 고정 편성이다.
    /// 스탯 배수(챕터 계수·변이·정예)는 여기서 접지 않고 스펙에만 실어 보낸다.
    /// 최종 스탯 계산처는 <see cref="CharacterStats"/> 하나로 유지한다.
    /// </summary>
    public sealed class EncounterGenerator
    {
        /// <summary> 보스 방 깊이. 고정 편성이라 난수를 쓰지 않는다. </summary>
        public const int BossDepth = 6;

        /// <summary> 한 방에 나올 수 있는 적의 최대 마리수. 전장 슬롯 수와 같다. </summary>
        public const int MaxEnemiesPerRoom = 4;

        // 보스 방 앞에 놓이는 일반 방 개수. 깊이 규칙은 이만큼을 정확히 덮어야 한다.
        private const int NormalDepthCount = BossDepth - 1;

        // 확률을 정수 롤로 바꾸는 분모. 0.15면 [0, 10000) 롤이 1500 미만일 때 적중이다.
        private const int ProbabilityScale = 10000;

        // 변이 배수가 실제로 걸리는 스탯. 치명 계열은 배수를 받지 않는 공식이라, 치명 축 변이를 만들면
        // 변이가 적중해도 스탯이 그대로다. 이런 데이터 실수는 눈에 띄지 않으므로 로드 시점에 막는다.
        private static readonly StatType[] MutableAxes = { StatType.Hp, StatType.Atk, StatType.Def, StatType.Spd };

        private readonly EncounterTuningSO _tuning;
        private readonly IRunRandom _encounterRng;
        private readonly IRunRandom _mutationRng;

        /// <summary>
        /// 튜닝 데이터를 검증하고 생성기를 만든다. 인카운터와 변이는 서로 다른 난수 스트림을
        /// 받는다(<see cref="RunSeed.For"/>로 파생).
        /// </summary>
        /// <exception cref="ArgumentException">튜닝 데이터가 로드 검증을 통과하지 못할 때.</exception>
        public EncounterGenerator(EncounterTuningSO tuning, IRunRandom encounterRng, IRunRandom mutationRng)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _encounterRng = encounterRng ?? throw new ArgumentNullException(nameof(encounterRng));
            // 스트림을 갈라 두면 변이 소비량이 달라져도 마리수·몹 선택 수열이 밀리지 않는다.
            _mutationRng = mutationRng ?? throw new ArgumentNullException(nameof(mutationRng));
            Validate(tuning);
        }

        /// <summary>
        /// 해당 깊이의 인카운터를 생성한다. 호출할 때마다 난수를 소비하므로 같은 인자를 넘겨도 결과가 달라진다.
        /// 같은 시드로 만든 생성기를 같은 순서로 호출하면 항상 같은 편성이 나온다.
        /// </summary>
        /// <param name="depth">방 깊이. 1 이상 <see cref="BossDepth"/> 이하만 허용한다.</param>
        /// <param name="elite">정예 인카운터 여부. 마리수를 상한으로 고정하고 전원을 변이·정예로 만든다. 보스 방에서는 무시한다.</param>
        /// <exception cref="ArgumentOutOfRangeException">튜닝에 없는 깊이일 때.</exception>
        public EncounterSpec Generate(int depth, bool elite)
        {
            if (depth == BossDepth)
                return BossEncounter();

            int index = Array.FindIndex(_tuning.depths, d => d.depth == depth);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "튜닝 데이터에 없는 깊이다.");

            var rule = _tuning.depths[index];
            int count = elite ? rule.maxCount : RollCount(rule);
            int mutationThreshold = (int)Math.Round(
                (elite ? 1f : rule.mutationChance) * ProbabilityScale, MidpointRounding.AwayFromZero);

            var enemies = new List<EnemyInstanceSpec>(count);
            for (int i = 0; i < count; i++)
            {
                var enemy = rule.allowedPool[_encounterRng.NextInt(rule.allowedPool.Length)];
                enemies.Add(new EnemyInstanceSpec(enemy, RollMutation(mutationThreshold), elite));
            }
            return new EncounterSpec(enemies);
        }

        /// <summary> 이 깊이에 나올 마리수를 하한과 상한 사이에서 균등하게 뽑는다. 정예는 상한 고정이라 부르지 않는다. </summary>
        private int RollCount(DepthPool rule)
            => rule.minCount + _encounterRng.NextInt(rule.maxCount - rule.minCount + 1);

        /// <summary> 적 한 마리에 붙일 변이를 뽑는다. 적중하지 못하면 null이고, 그 적은 변이 없이 나온다. </summary>
        private MutationSO RollMutation(int threshold)
        {
            // 적중 판정은 확률이 0이든 100이든 마리당 1회 추첨하는데, 소비량이 고정돼야 변이율을 튜닝해도
            // 기대 수열이 안 흔들린다.
            bool hit = _mutationRng.NextInt(ProbabilityScale) < threshold;
            return hit ? _tuning.mutations[_mutationRng.NextInt(_tuning.mutations.Length)] : null;
        }

        /// <summary> 보스 방 편성을 만든다. 보스 1기에 수하를 붙이며, 변이도 정예도 붙지 않는다. </summary>
        private EncounterSpec BossEncounter()
        {
            var enemies = new List<EnemyInstanceSpec> { new EnemyInstanceSpec(_tuning.boss, null, false) };
            if (_tuning.bossAdds != null)
                enemies.AddRange(_tuning.bossAdds.Select(add => new EnemyInstanceSpec(add, null, false)));
            return new EncounterSpec(enemies);
        }

        /// <summary>
        /// 튜닝 데이터를 검사하고 잘못된 데가 있으면 예외를 던진다. 잘못된 채로 추첨하면 방마다 다른 곳에서
        /// 터져 원인을 찾기 어려우므로 로드 시점에 한 번 걸러 낸다.
        /// </summary>
        private static void Validate(EncounterTuningSO tuning)
        {
            var depths = tuning.depths;
            if (depths == null || depths.Length != NormalDepthCount)
                throw new ArgumentException($"깊이 규칙은 {NormalDepthCount}행이어야 한다.", nameof(tuning));

            var expected = Enumerable.Range(1, NormalDepthCount);
            if (!depths.Select(d => d.depth).OrderBy(d => d).SequenceEqual(expected))
                throw new ArgumentException($"깊이가 1부터 {NormalDepthCount}까지를 중복·누락 없이 덮지 않는다.", nameof(tuning));

            foreach (var rule in depths)
            {
                if (rule.allowedPool == null || rule.allowedPool.Length == 0 || rule.allowedPool.Any(e => e == null))
                    throw new ArgumentException($"깊이 {rule.depth}의 몹 풀이 비었거나 빈 칸을 포함한다.", nameof(tuning));
                if (rule.minCount < 1 || rule.minCount > rule.maxCount || rule.maxCount > MaxEnemiesPerRoom)
                    throw new ArgumentException(
                        $"깊이 {rule.depth}의 마리수 범위가 1 이상 {MaxEnemiesPerRoom} 이하가 아니다.", nameof(tuning));
                if (rule.mutationChance < 0f || rule.mutationChance > 1f)
                    throw new ArgumentException($"깊이 {rule.depth}의 변이 확률이 0~1을 벗어난다.", nameof(tuning));
            }

            if (tuning.boss == null)
                throw new ArgumentException("보스가 비어 있다.", nameof(tuning));
            if (depths.Any(d => d.allowedPool.Contains(tuning.boss)))
                throw new ArgumentException("보스가 일반 깊이 풀에 섞여 있다.", nameof(tuning));
            if (tuning.bossAdds != null && tuning.bossAdds.Any(add => add == null))
                throw new ArgumentException("보스 수하에 빈 칸이 있다.", nameof(tuning));
            if (tuning.bossAdds != null && tuning.bossAdds.Contains(tuning.boss))
                throw new ArgumentException("보스가 수하로 또 들어가 있다. 보스 방의 보스는 1기다.", nameof(tuning));

            if (tuning.mutations == null || tuning.mutations.Length == 0 || tuning.mutations.Any(m => m == null))
                throw new ArgumentException("변이 후보가 비었거나 빈 칸을 포함한다.", nameof(tuning));
            if (tuning.mutations.Any(m => !MutableAxes.Contains(m.statAxis) || m.multiplier <= 0f))
                throw new ArgumentException("변이의 스탯이 배수를 받지 않는 축이거나 배수가 0 이하다.", nameof(tuning));
        }
    }
}
