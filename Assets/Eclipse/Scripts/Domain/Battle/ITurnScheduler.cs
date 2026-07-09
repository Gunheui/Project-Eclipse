namespace Eclipse.Domain
{
    /// <summary>
    /// 다음에 행동할 유닛을 하나씩 결정하는 규칙. 구현을 교체하면
    /// 순서 방식(ATB 게이지 ↔ 라운드 정렬 등)을 통째로 바꿀 수 있다.
    ///
    /// 호출 규약: <see cref="GetNextActor"/>로 얻은 행동자는 다음 <see cref="GetNextActor"/> 전에
    /// 반드시 <see cref="OnActionResolved"/>로 정산해야 한다(1:1 짝). 정산을 건너뛰고 다시 부르면
    /// 게이지가 그대로라 같은 유닛이 계속 반환되고, 순서가 그 유닛에 멈춰버린다(예외·크래시는 없다).
    /// </summary>
    public interface ITurnScheduler
    {
        /// <summary>
        /// 다음에 행동할 유닛 1명을 즉시 계산해 반환한다(실시간 대기가 아니라 계산으로 결정).
        /// 생존 유닛이 하나도 없으면 null. 반환 후에는 <see cref="OnActionResolved"/>로 정산해야 한다.
        /// </summary>
        ICombatant GetNextActor();

        /// <summary>
        /// 한 유닛이 행동을 마쳤음을 통지한다. 구현은 이 시점에 행동 비용을 정산한다
        /// (ATB에서는 그 유닛의 게이지를 임계값만큼 차감).
        /// </summary>
        /// <param name="actor">방금 행동을 마친 유닛.</param>
        void OnActionResolved(ICombatant actor);
    }
}
