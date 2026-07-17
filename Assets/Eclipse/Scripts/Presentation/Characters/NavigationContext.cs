using Eclipse.Data;
using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 화면 전환 사이에 선택 상태를 전달하는 보관함. app-scope Singleton이라 씬 로드에도 살아남는다.
    /// 목록/선택 화면이 선택을 기록하고, 다음 씬의 ViewModel·스코프가 생성될 때 읽어 간다.
    /// </summary>
    public class NavigationContext
    {
        /// <summary> 직전에 선택된 캐릭터. 상세 화면이 표시 대상을 여기서 읽는다. </summary>
        public OwnedCharacter Selected { get; set; }

        /// <summary> 직전에 선택된 스테이지. 전투 씬 스코프가 적 편성을 여기서 읽어 전투를 조립한다. </summary>
        public StageSO SelectedStage { get; set; }
    }
}
