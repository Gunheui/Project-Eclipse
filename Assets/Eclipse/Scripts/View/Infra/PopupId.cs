namespace Eclipse.View.Infra
{
    /// <summary>
    /// PopupManager가 팝업 프리팹을 식별하는 키. 프리팹 매핑이 인스펙터에 직렬화되므로 값은 고정한다.
    /// </summary>
    public enum PopupId
    {
        Confirm = 0,

        /// <summary> 방 결과(승/패 + 공개 보상) 팝업. 결과 타입은 bool — 확인 신호로만 쓴다. </summary>
        BattleResult = 1,

        /// <summary> 문 지점(3택) 팝업. 결과 타입은 DoorKind. </summary>
        DoorPoint = 2,

        /// <summary> 런 정산 팝업. 결과 타입은 bool — 확인 신호로만 쓴다. </summary>
        RunSettlement = 3,

        /// <summary> 버프 카드 3택1+배정 팝업. 결과 타입은 CardPickChoice. </summary>
        CardPick = 4
    }
}
