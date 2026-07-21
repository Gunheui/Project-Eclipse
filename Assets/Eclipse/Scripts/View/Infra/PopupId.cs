namespace Eclipse.View.Infra
{
    /// <summary>
    /// PopupManager가 팝업 프리팹을 식별하는 키. 프리팹 매핑 배선과 인스펙터에 직렬화되므로 값은 고정한다.
    /// </summary>
    public enum PopupId
    {
        Confirm = 0,

        /// <summary> 전투 결과(승/패 + 보상) 팝업. 결과 타입은 bool — true=재도전, false=확인. </summary>
        BattleResult = 1
    }
}
