using System.Collections.Generic;

namespace Eclipse.Domain
{
    /// <summary>
    /// 행동 순서를 정하는 규칙. 구현을 교체하면 순서 방식(ATB ↔ 라운드제 등)을 통째로 바꿀 수 있다.
    /// 호출 규약: <see cref="GetNextActor"/>로 얻은 행동자는 다음 호출 전에 반드시
    /// <see cref="OnActionResolved"/>로 정산해야 한다(1:1 짝). 정산을 건너뛰면 같은 유닛이 계속 반환된다.
    /// </summary>
    public interface ITurnScheduler
    {
        /// <summary>
        /// 다음에 행동할 유닛 1명을 계산해 반환한다. 생존 유닛이 없으면 null.
        /// 반환 후에는 <see cref="OnActionResolved"/>로 정산해야 한다.
        /// </summary>
        ICombatant GetNextActor();

        /// <summary> 한 유닛의 행동 완료를 통지한다. 구현은 이 시점에 행동 비용을 정산한다. </summary>
        void OnActionResolved(ICombatant actor);

        /// <summary>
        /// 앞으로 행동할 count명의 순서를 예보한다(0번=다음 차례, UI 표시용). 상태를 바꾸지 않는 조회라
        /// 예보 0번은 이어지는 <see cref="GetNextActor"/> 결과와 일치해야 한다. 사망·스탯 변화를 가정하지
        /// 않는 스냅샷이므로 호출자는 상태가 바뀔 때마다 다시 물어야 한다. count≤0·전멸이면 빈 목록.
        /// </summary>
        IReadOnlyList<ICombatant> PreviewOrder(int count);
    }
}
