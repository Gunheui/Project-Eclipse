using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 스탯별 증감량의 합산 보관소. 같은 스탯끼리 가산 누적하며 상한이 없다.
    /// 한 캐릭터가 런에서 받은 버프가 여기 모여 <see cref="CharacterStats.BuildAllyStats"/>에 전달된다.
    /// </summary>
    public sealed class StatModifierSet
    {
        private readonly Dictionary<StatType, float> _sums = new Dictionary<StatType, float>();

        /// <summary> 증감량을 해당 스탯의 합에 더한다. </summary>
        /// <exception cref="ArgumentException">axis가 <see cref="StatType.None"/>일 때.</exception>
        public void Add(StatDelta delta)
        {
            if (delta.axis == StatType.None)
                throw new ArgumentException("StatType.None은 버프 축으로 쓸 수 없다.", nameof(delta));
            _sums.TryGetValue(delta.axis, out float current);
            _sums[delta.axis] = current + delta.value;
        }

        /// <summary> 해당 스탯의 누적 합을 반환한다. 없으면 0. </summary>
        public float SumOf(StatType axis)
            => _sums.TryGetValue(axis, out float sum) ? sum : 0f;
    }
}
