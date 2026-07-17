using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스테이지 한 장(챕터)의 정의 데이터. 소속 스테이지 목록과 장 표시 정보를 묶는다.
    /// 스테이지 번호는 <see cref="stages"/> 인덱스+1로 파생하며 별도 필드로 두지 않는다(중복 방지).
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Stages/Chapter Data")]
    public sealed class ChapterSO : ScriptableObject
    {
        /// <summary> 진행/해금 상태의 조회 키. 세이브 데이터와 매핑된다(표시명과 분리). </summary>
        public string id;

        /// <summary> 장 번호. 패널·내비 라벨의 표기 기준(예: 1 → "01" / "1장"). </summary>
        public int number;

        /// <summary> 장 표시명(로컬라이즈 대상). </summary>
        public string displayName;

        /// <summary> 장 설명(선택 화면 상단 패널 문구). </summary>
        [TextArea] public string description;

        /// <summary> 소속 스테이지 목록. 순서가 스테이지 번호의 권위이며, 마지막이 보스인 것이 관례. </summary>
        public StageSO[] stages;
    }
}
