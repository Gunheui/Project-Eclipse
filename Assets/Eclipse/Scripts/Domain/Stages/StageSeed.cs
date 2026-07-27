namespace Eclipse.Domain
{
    /// <summary>
    /// 스테이지 시드에서 시스템별 난수 스트림 시드를 파생한다. 시스템을 갈라 두면 한 시스템이 난수를 더 쓰거나
    /// 덜 써도 다른 시스템의 수열이 밀리지 않는다. 변이 굴림 횟수는 마리수에 따라 달라지는데, 그때도 몹 선택
    /// 수열이 그대로여야 "조우만 고정한 채 문은 자유" 같은 표적 테스트가 성립한다.
    /// 파생식은 프로덕션과 테스트가 같아야 분리가 실제로 보장되므로 여기 한 곳에만 둔다.
    /// </summary>
    public static class StageSeed
    {
        /// <summary> 난수 용도. 값은 "서로 다르다"만 의미 있다. Door·Card는 문·카드 구현이 소비한다. </summary>
        public enum Stream { Encounter = 0, Mutation = 1, Door = 2, Card = 3 }

        /// <summary>
        /// 용도별 난수 시드. 같은 stageSeed면 항상 같은 값을 낸다(재현 유지).
        /// 스트림 id를 XOR로 얹기만 하고, 실제 분산은 SeededRandom 생성자의 SplitMix64가 담당한다.
        /// </summary>
        public static int For(int stageSeed, Stream stream) => stageSeed ^ (int)stream;
    }
}
