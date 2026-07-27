using System.Collections.Generic;
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

        /// <summary> 이번 런이 향하는 챕터. 편성 화면의 [런 시작]이 기록하고 전투 씬 스코프가 읽는다. </summary>
        public ChapterSO SelectedChapter { get; set; }

        /// <summary>
        /// 편성 화면이 확정한 아군 파티. 인덱스가 편성 칸 위치와 같고 빈 칸은 null이다(압축하지 않는다) —
        /// 전투 씬 스코프가 이 위치를 그대로 전투 진영 자리로 쓴다. null이거나 전부 null이면 세이브 로스터로 폴백한다.
        /// 런 시작 시 [런 시작]이 새로 기록한다.
        /// </summary>
        public IReadOnlyList<OwnedCharacter> SelectedParty { get; set; }
    }
}
