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

        /// <summary> 직전에 선택된 스테이지. 전투 씬 스코프가 적 편성을 여기서 읽어 전투를 조립한다. </summary>
        public StageSO SelectedStage { get; set; }

        /// <summary>
        /// <see cref="SelectedStage"/>가 속한 장. 스테이지의 0-기반 인덱스는 이 장의 stages 배열에서 파생하므로
        /// 인덱스를 따로 싣지 않는다(진행도 마킹이 쓰는 base와 표시용 번호가 어긋나는 것을 막는다).
        /// </summary>
        public ChapterSO SelectedChapter { get; set; }

        /// <summary>
        /// 편성 화면이 확정한 아군 파티. 인덱스가 편성 칸 위치와 같고 빈 칸은 null이다(압축하지 않는다) —
        /// 전투 씬 스코프가 이 위치를 그대로 전투 진영 자리로 쓴다. null이거나 전부 null이면 세이브 로스터로 폴백한다.
        /// 편성 화면 재진입 시 스테이지 선택 단계에서 클리어된다.
        /// </summary>
        public IReadOnlyList<OwnedCharacter> SelectedParty { get; set; }
    }
}
