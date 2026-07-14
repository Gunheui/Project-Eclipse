using System.Collections.Generic;

namespace Eclipse.Domain
{
    /// <summary>
    /// 행동 순서를 정하는 규칙. 구현을 교체하면 순서 방식(ATB 게이지 ↔ 라운드 정렬 등)을 통째로 바꿀 수 있다.
    /// "지금 행동할 1명"(<see cref="GetNextActor"/>)과 "앞으로 행동할 N명"(<see cref="PreviewOrder"/>)은
    /// 같은 규칙에 대한 두 질문이다 — 전자는 상태를 진행시키는 명령, 후자는 상태를 두는 조회.
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

        /// <summary>
        /// 앞으로 행동할 <paramref name="count"/>명의 순서를 예보한다(0번=다음 차례). 순서 표시 UI용.
        ///
        /// 호출 규약: **상태를 바꾸지 않는 조회**다. 몇 번을 부르든 이어지는 <see cref="GetNextActor"/> 결과가
        /// 달라지지 않아야 하며, 예보 0번은 그 <see cref="GetNextActor"/>가 돌려줄 유닛과 일치해야 한다.
        /// 빠른 유닛은 구간 안에서 여러 번 등장할 수 있다. 예보 구간 내 사망·스탯 변화는 가정하지 않는
        /// 스냅샷이므로, 호출자는 상태가 바뀔 때마다 다시 물어야 한다. 생존 유닛이 없거나 count≤0이면 빈 목록.
        /// </summary>
        /// <param name="count">예보할 행동 수(≤0이면 빈 목록).</param>
        IReadOnlyList<ICombatant> PreviewOrder(int count);
    }
}
