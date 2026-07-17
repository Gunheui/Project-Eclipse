namespace Eclipse.Data.Enums
{
    /// <summary>
    /// 스테이지 선택 화면 셀의 진행 상태 3종. 장별 클리어 수에서 파생되는 런타임 값이라 직렬화하지 않는다.
    /// </summary>
    public enum StageState
    {
        /// <summary>이미 클리어함. 재진입 가능.</summary>
        Cleared,

        /// <summary>현재 도전 가능(다음 해금 대상). 진입 가능.</summary>
        Open,

        /// <summary>선행 스테이지 미클리어로 잠김. 선택 불가.</summary>
        Locked
    }
}
