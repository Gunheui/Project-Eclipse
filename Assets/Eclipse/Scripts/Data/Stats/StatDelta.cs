using System;
using Eclipse.Data.Enums;

namespace Eclipse.Data
{
    /// <summary>
    /// 스탯 하나에 대한 증감량. HP·ATK·DEF·SPD는 %가산(0.25 = +25%), 치명 계열은 %p 가산(0.15 = +15%p)이다.
    /// 스테이지 버프 카드가 이 단위로 효과를 기술하며, axis에 <see cref="StatType.None"/>은 허용하지 않는다.
    /// </summary>
    [Serializable]
    public struct StatDelta
    {
        public StatType axis;
        public float value;
    }
}
