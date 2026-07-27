namespace Eclipse.View.Infra
{
    /// <summary>
    /// ScreenManager가 화면 프리팹을 식별하는 키.
    /// </summary>
    public enum ScreenId
    {
        Lobby = 0,
        CharacterList = 1,
        CharacterDetail = 2,
        // 값 3은 삭제된 StageSelect 자리다. 인스펙터에 직렬화된 정수라 재번호하면 기존 매핑이 달라진다.
        PartyFormation = 4,
        PartyPick = 5
    }
}