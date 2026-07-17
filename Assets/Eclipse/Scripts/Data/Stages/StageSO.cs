using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스테이지 선택 화면 표시 데이터이자 전투 진입 편성의 소유자.
    /// 선택 시 <see cref="enemies"/>가 전투 진입 파라미터로 넘어간다.
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

        /// <summary> 보스 스테이지 여부. 셀에 보스 프레임을 표시하고 장의 마지막 스테이지로 배치한다. </summary>
        public bool isBoss;

        /// <summary> 이 스테이지의 적 편성. 배열 순서가 전장 슬롯 순서(SlotIndex)이며, 전투 진입 시 앞에서부터 참전한다. </summary>
        public EnemySO[] enemies;
    }
}
