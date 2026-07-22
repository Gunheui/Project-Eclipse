using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class DamagePipelineTests
    {
        // 미리 정한 값을 순서대로(부족하면 순환) 돌려주는 결정적 난수 스텁.
        // NextFloat 호출 순서 = [치명 난수, 변동 난수]라, 이 두 값으로 결과를 완전히 고정한다.
        //   첫 값 0.0 → 강제 크리(0.0<critRate) / 0.99 → 강제 비크리 · 둘째 값 0.5 → 변동 1.0(평균).
        private sealed class FixedRandom : IRandomService
        {
            private readonly float[] _values;
            private int _i;
            public FixedRandom(params float[] values) { _values = values; }
            public float NextFloat() => _values[_i++ % _values.Length];
        }

        // 기본 밸런스 값: k=1, 변동 0.95~1.05.
        private static DamagePipeline Pipeline(IRandomService rng) => new DamagePipeline(1f, 0.95f, 1.05f, rng);

        private static Stats Attacker(int atk, float critRate, float critDamage)
            => new Stats { atk = atk, critRate = critRate, critDamage = critDamage };

        private static Stats Target(int def) => new Stats { def = def };

        // 크리 회피(0.99≥0.30) + 변동 평균(0.5→1.0)으로 비크리·변동1.0 기대값을 고정.
        private static readonly float[] NoCritAvg = { 0.99f, 0.5f };

        [Test]
        public void 카이_기본공격_비크리_기대값_130()
        {
            // 175 × 175/(175+60) = 175 × 0.7447 = 130.3 → 반올림 130
            var result = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(175, 0.30f, 2.0f), Target(60), 1.0f);

            Assert.AreEqual(130, result.Amount);
            Assert.IsFalse(result.IsCrit);
        }

        [Test]
        public void 카이_기본공격_치명시_261()
        {
            // 치명 난수 0.0 < 0.30 → 크리. 130.3 × 2.0 = 260.6 → 261
            var result = Pipeline(new FixedRandom(0.0f, 0.5f))
                .ComputeDamage(Attacker(175, 0.30f, 2.0f), Target(60), 1.0f);

            Assert.AreEqual(261, result.Amount);
            Assert.IsTrue(result.IsCrit);
        }

        [Test]
        public void 리엔_기본공격_비크리_기대값_46()
        {
            // 80 × 80/(80+60) = 80 × 0.5714 = 45.7 → 46
            var result = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(80, 0.05f, 1.5f), Target(60), 1.0f);

            Assert.AreEqual(46, result.Amount);
        }

        [Test]
        public void DEF가_높을수록_데미지가_준다()
        {
            var low = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(175, 0f, 1.5f), Target(60), 1.0f).Amount;
            var high = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(175, 0f, 1.5f), Target(200), 1.0f).Amount;

            Assert.Less(high, low, "비율경감이면 DEF가 오를수록 데미지가 줄어야 한다");
        }

        [Test]
        public void ATK가_높을수록_방어_관통율이_높다()
        {
            // 같은 DEF(100)를 상대로 경감계수 ATK/(ATK+DEF×k)는 ATK가 클수록 커진다.
            // skillPower=1·변동1.0이면 데미지/ATK ≈ 경감계수 → 관통율을 ATK별로 비교해 검증.
            const int lowAtk = 80, highAtk = 175;
            int lowDmg = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(lowAtk, 0f, 1.5f), Target(100), 1.0f).Amount;
            int highDmg = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(highAtk, 0f, 1.5f), Target(100), 1.0f).Amount;

            float lowPen = (float)lowDmg / lowAtk;   // ≈ 0.44
            float highPen = (float)highDmg / highAtk; // ≈ 0.64
            Assert.Greater(highPen, lowPen, "ATK가 높을수록 방어 관통율(데미지/ATK)이 높아야 한다");
        }

        [Test]
        public void 데미지는_최소_1로_클램프된다()
        {
            // 저ATK vs 고DEF: 1 × 1/(1+1000) ≈ 0.001 → 반올림 0 → 클램프 1
            var result = Pipeline(new FixedRandom(NoCritAvg))
                .ComputeDamage(Attacker(1, 0f, 1.5f), Target(1000), 1.0f);

            Assert.AreEqual(1, result.Amount);
        }
    }
}