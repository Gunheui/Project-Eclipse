using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 화면 전환 사이에 선택된 캐릭터를 전달하는 보관함.
    /// 목록 화면이 선택을 기록하고, 상세 ViewModel이 생성될 때 읽어 간다.
    /// </summary>
    public class NavigationContext
    {
        /// <summary> 직전에 선택된 캐릭터. 상세 화면이 표시 대상을 여기서 읽는다. </summary>
        public OwnedCharacter Selected { get; set; }
    }
}
