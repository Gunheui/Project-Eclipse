namespace Eclipse.Domain
{
    /// <summary> 전투의 종료 상태. 전투 루프가 매 턴 판정해 반환한다. </summary>
    public enum BattleOutcome
    {
        /// <summary> 아직 진행 중. 루프를 계속한다. </summary>
        Ongoing = 0,

        /// <summary> 적이 전멸해 아군 승리. </summary>
        Victory = 1,

        /// <summary> 아군이 전멸하거나 행동 상한을 넘어 미클리어. </summary>
        Defeat = 2,
    }
}
