namespace Eclipse.Domain
{
    /// <summary>
    /// 전투 계산에 쓰는 결정적 난수 이음새. 시드가 같으면 항상 같은 수열을 낸다.
    /// 치명 판정·데미지 변동(그리고 보류된 명중 판정)이 이 하나의 프리미티브를 파생해 쓴다.
    /// </summary>
    public interface IRandomService
    {
        /// <summary> [0, 1) 구간의 균등 실수 하나. 호출마다 수열이 한 칸 진행한다. </summary>
        float NextFloat();
    }
}