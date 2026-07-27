using System;

namespace Eclipse.Domain
{
    /// <summary>
    /// 시드 고정 결정적 난수(xorshift128+, Vigna 2014). 알고리즘을 코드가 직접 소유하므로
    /// 플랫폼·런타임(Mono/IL2CPP)·.NET 버전과 무관하게 같은 시드는 항상 같은 수열을 낸다.
    /// System.Random은 결정성이 API로 보장되지 않아 리플레이·회귀 테스트 기반으로 쓰지 않는다.
    /// </summary>
    public sealed class SeededRandom : IRandomService, IRunRandom
    {
        // 상태 2워드(총 128비트). 매 난수마다 한 워드를 시프트로 섞고 두 워드를 더해 출력한다.
        private ulong _s0, _s1;

        /// <summary> 단일 정수 시드로 난수기를 만든다. </summary>
        public SeededRandom(int seed)
        {
            ulong s = (ulong)(uint)seed; // 음수 시드의 부호 확장을 막는 이중 캐스팅
            // splitmix64로 시드를 두 워드에 흩뿌린다. 서로 닮은 초기 상태와 전부 0 상태를 피한다(0이면 xorshift가 0만 뱉는다).
            _s0 = SplitMix64(ref s);
            _s1 = SplitMix64(ref s);
            if ((_s0 | _s1) == 0) _s0 = 0x9E3779B97F4A7C15UL; // 만약의 0-상태 방지
        }

        /// <summary> [0, 1) 균등 실수를 낸다. </summary>
        public float NextFloat()
        {
            // 상위 24비트만 2^24로 나눈다. float 가수부가 23비트라 24비트를 넘으면 반올림 편향이 생기고,
            // 상위비트를 쓰면 xorshift128+의 하위비트 선형성 약점도 피한다.
            return (NextULong() >> 40) * (1.0f / (1 << 24));
        }

        /// <summary> [0, maxExclusive) 균등 정수를 낸다. </summary>
        /// <param name="maxExclusive">상한(제외). 1 이상만 허용한다.</param>
        /// <exception cref="ArgumentOutOfRangeException">maxExclusive가 1 미만일 때.</exception>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "상한은 1 이상이어야 한다.");

            // multiply-shift 구간 샘플링(Lemire 2019, "Fast Random Integer Generation in an Interval").
            // 상위 32비트에 구간 길이를 곱해 상위워드를 취한다. 상위비트만 쓰는 것은 NextFloat과 같은 이유다.
            uint range = (uint)maxExclusive;
            ulong product = NextUpper32() * range;
            if ((uint)product < range)
            {
                // 기각 구간은 2의 32제곱을 range로 나눈 나머지다. 이 구간에 든 값만 버리면 남은 값이 균등해진다.
                uint threshold = (uint)(0x1_0000_0000UL % range);
                while ((uint)product < threshold)
                    product = NextUpper32() * range;
            }
            return (int)(product >> 32);
        }

        private ulong NextUpper32() => NextULong() >> 32;

        // xorshift128+ 한 스텝 = 64비트 난수 한 개. 시프트 상수 23·18·5는 Vigna가 BigCrush를
        // 통과하도록 찾은 값(튜닝 대상 아님).
        private ulong NextULong()
        {
            ulong t = _s0;
            ulong s = _s1;
            _s0 = s;
            t ^= t << 23;
            t ^= t >> 18;
            t ^= s ^ (s >> 5);
            _s1 = t;
            return t + s;
        }

        // 단일 시드를 서로 다른 워드로 흩뿌리는 splitmix64(상태 초기화 전용). 황금비 증분 +
        // Stafford Mix13 애벌런치 곱셈수를 쓰는 표준 시딩(Vigna 권장).
        private static ulong SplitMix64(ref ulong s)
        {
            s += 0x9E3779B97F4A7C15UL;
            ulong z = s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}