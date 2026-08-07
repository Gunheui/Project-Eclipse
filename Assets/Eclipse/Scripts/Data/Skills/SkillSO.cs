using System.Collections.Generic;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스킬 정의 데이터. 에셋 하나를 여러 유닛이 공유 참조하며,
    /// 전투 중 변하는 상태(잔여 쿨)는 전투 런타임에서 SkillRuntime이 보유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Skills/Skill Data")]
    public class SkillSO : ScriptableObject
    {
        /// <summary> 표시명과 분리한 참조·조회용 고정 키. </summary>
        public string id;

        /// <summary> UI 표시명. </summary>
        public string displayName;

        /// <summary> 툴팁 설명문. </summary>
        [TextArea]
        public string description;

        /// <summary> UI 슬롯 아이콘. </summary>
        public Sprite icon;

        /// <summary> 사용 후 잠기는 쿨(턴). 남은 쿨은 SkillRuntime이 보유한다. </summary>
        public int cooldownTurns;

        /// <summary> 효과 목록. 효과마다 타겟·세기가 따로다. </summary>
        public List<SkillEffect> effects;

        /// <summary>
        /// 붙어서 때리는 스킬인지. 켜면 시전자가 대상 앞까지 이동해 때리고 돌아온다. 끄면 제자리에서 시전한다.
        /// </summary>
        public bool melee;

        /// <summary> 시전 시 행동자 위치에 재생할 이펙트. null이면 연출 없음. </summary>
        public EffectSpec castEffect;

        /// <summary> 피격 시 대상 위치에 재생할 이펙트. null이면 연출 없음. </summary>
        public EffectSpec impactEffect;

        /// <summary> 시전 시 재생할 파티클 이펙트. null이면 연출 없음. </summary>
        public VfxSpec castVfx;

        /// <summary> 피격 시 재생할 파티클 이펙트. null이면 연출 없음. </summary>
        public VfxSpec impactVfx;
    }
}
