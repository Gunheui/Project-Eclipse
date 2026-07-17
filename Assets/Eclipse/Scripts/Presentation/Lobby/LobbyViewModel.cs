namespace Eclipse.Presentation
{
    /// <summary>
    /// 로비(코어 루프 허브) 화면의 상태. 각 메뉴 목적지가 진입 가능한지의 단일 원천이다.
    /// 값이 고정된 표시 전용 뷰모델이라 스트림을 두지 않는다. 미구현 목적지는 false로 잠근다.
    /// </summary>
    public sealed class LobbyViewModel : ViewModelBase
    {
        /// <summary>캐릭터 목록 진입 가능 여부.</summary>
        public bool CanOpenCharacterList => true;

        /// <summary>스토리 진입 가능 여부.</summary>
        public bool CanOpenStory => false;

        /// <summary>모집(가챠) 진입 가능 여부.</summary>
        public bool CanOpenGacha => false;

        /// <summary>전투 진입 가능 여부.</summary>
        public bool CanOpenBattle => true;

        /// <summary>상점 진입 가능 여부.</summary>
        public bool CanOpenShop => false;
    }
}
