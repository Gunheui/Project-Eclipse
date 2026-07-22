using System;
using Eclipse.Data.Enums;
using UnityEngine;

namespace Eclipse.Data
{
    /// <summary>
    /// 스킬이 일으키는 효과 한 개 (무엇을 / 누구에게 / 얼마나 / 몇 턴).
    /// 하나의 스킬은 이 효과를 여러 개 리스트로 가지며, 효과마다 타겟이 다를 수 있다.
    /// </summary>
    [Serializable]
    public struct SkillEffect
    {
        /// <summary> 효과 종류. </summary>
        public EffectType type;

        /// <summary> 대상 범위(스코프). 단일 스코프에서 구체적으로 누구를 고를지는 타겟 우선순위 정책이 정한다. </summary>
        public TargetSelector target;

        /// <summary> 효과의 세기. 의미는 type별로 다르다(EffectType 주석 참조). </summary>
        public float value;

        /// <summary> Buff·Debuff가 변경할 스탯. 그 외 타입은 None. </summary>
        public StatType affectedStat;

        /// <summary> 효과 지속 턴 수(0 = 즉발, 양수 = 지속 턴, -1 = 상시). </summary>
        public int duration;
    }
}