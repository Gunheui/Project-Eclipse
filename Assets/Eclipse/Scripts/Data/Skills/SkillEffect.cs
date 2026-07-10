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
        /// 효과의 세기. 의미는 type별로 다르다(EffectType 주석 참조):
        /// Damage/Heal/Dot/Regen = 배율, Buff/Debuff = 변화율, Shield = 최대 HP 비율.
        /// </summary>
        public float value;

        /// <summary>
        /// Buff·Debuff가 변경할 스탯. 그 외 타입은 None.
        /// </summary>
        public StatType affectedStat;

        /// <summary>
        /// 효과 지속 턴 수 (0 = 즉발, 양수 = 지속 턴, -1 = 상시/만료 없음).
        /// </summary>
        public int duration;
    }
}