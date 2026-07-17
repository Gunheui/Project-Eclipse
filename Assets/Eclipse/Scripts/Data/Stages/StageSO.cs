using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스테이지 선택 화면에 뿌리는 표시 전용 정의 데이터.
    /// 적 편성·전투 파라미터는 여기 두지 않는다(전투 진입 이슈 소관).
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Stages/Stage Data")]
    public sealed class StageSO : ScriptableObject
    {
        /// <summary> 참조·조회용 고정 키(표시명과 분리). </summary>
        public string id;

        /// <summary> UI 표시명(로컬라이즈 대상). </summary>
        public string displayName;

        /// <summary> 스테이지 설명(선택 화면 상세 문구). </summary>
        [TextArea] public string description;

        /// <summary> 스테이지 셀 썸네일 스프라이트. </summary>
        public Sprite thumbnail;
    }
}
