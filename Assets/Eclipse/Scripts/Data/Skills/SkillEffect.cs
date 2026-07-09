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
        /// <summary>
        /// 효과 종류 (데미지 / 힐 / 버프 / 디버프 / 도발 / 도트 / 실드).
        /// </summary>
        public EffectType type;

        /// <summary>
        /// 이 효과가 적용될 대상 선택 규칙 (자기 / 최저HP 아군 / 최고ATK 적 등).
        /// </summary>
        public TargetSelector target;

        /// <summary>
        /// 효과의 세기. type에 따라 의미가 달라진다
        /// (데미지=SkillPower, 힐=HealPower, 버프/디버프=변화율 등).
        /// </summary>
        public float value;

        /// <summary>
        /// 효과 지속 턴 수 (0 = 즉발).
        /// </summary>
        public int duration;
    }
}