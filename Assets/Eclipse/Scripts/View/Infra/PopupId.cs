namespace Eclipse.View.Infra
{
    /// <summary>
    /// PopupManager가 팝업 프리팹을 식별하는 키. 프리팹 매핑이 인스펙터에 직렬화되므로 값은 고정한다.
    /// </summary>
    public enum PopupId
    {
        Confirm = 0,

        /// <summary> 옛 방 결과 팝업 자리. 승리 후 자동 진행으로 바뀌어 쓰지 않는다. 값은 비워 둔다. </summary>
        BattleResult = 1,

        /// <summary> 옛 문 지점 팝업 자리. 전장 월드 문 선택으로 바뀌어 쓰지 않는다. 값은 비워 둔다. </summary>
        DoorPoint = 2,

        /// <summary> 런 정산 팝업. 결과 타입은 bool — 확인 신호로만 쓴다. </summary>
        RunSettlement = 3,

        /// <summary> 버프 카드 3택1 팝업. 결과 타입은 BuffCard — 강제 1택이라 빈 결과가 없다. </summary>
        CardPick = 4
    }
}
