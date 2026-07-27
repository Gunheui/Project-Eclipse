namespace Eclipse.Domain
{
    /// <summary>
    /// 스테이지 굴림(조우·변이·문·카드)에 쓰는 결정적 정수 난수 이음새.
    /// 가중 추첨과 비복원 추첨은 이 계약에 넣지 않고, 계약을 쓰는 쪽에서 조립한다.
    /// </summary>
    public interface IStageRandom
    {
        /// <summary> [0, maxExclusive) 구간의 균등 정수. 호출마다 수열이 한 칸 진행한다. </summary>
        /// <param name="maxExclusive">상한(제외). 1 이상만 허용한다.</param>
        int NextInt(int maxExclusive);
    }
}
