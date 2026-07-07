using System.Collections.Generic;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스킬 한 개의 정의 데이터. 프로젝트에 에셋으로 하나만 존재하며 여러 유닛이 공유 참조한다.
    /// 전투 중 변하는 잔여 쿨 등 가변 상태는 여기 두지 않고 SkillRuntime이 보유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Skills/Skill Data")]
    public class SkillSO : ScriptableObject
    {
        /// <summary>
        /// 참조·조회용 고정 키 (표시명과 분리해 로컬라이즈·리네임에 안전).
        /// </summary>
        public string id;

        /// <summary>
        /// UI 표시명 (로컬라이즈 대상).
        /// </summary>
        public string displayName;

        /// <summary>
        /// 사용 후 잠기는 쿨(턴). 현재 남은 쿨(currentCooldown)은 SkillRuntime이 보유한다.
        /// </summary>
        public int cooldownTurns;

        /// <summary>
        /// 이 스킬이 일으키는 효과 목록 (효과마다 타겟·세기가 따로).
        /// </summary>
        public List<SkillEffect> effects;
    }
}