using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 효과 하나가 대상 한 명에게 남긴 결과. 효과에 적힌 수치가 아니라 실제로 움직인 양이라 화면에 그대로 띄운다.
    /// </summary>
    public readonly struct EffectResult
    {
        /// <summary> 이 결과를 낸 효과 종류. </summary>
        public EffectType Type { get; }

        /// <summary> 이 결과를 받은 유닛. 도트·리젠 틱은 자기 턴을 시작한 유닛 자신이다. </summary>
        public ICombatant Target { get; }

        /// <summary>
        /// 화면에 띄울 크기. 피해는 들어간 피해 전부, 회복은 최대 HP에 막히고 실제로 채운 양이다.
        /// 수치가 없는 효과(버프·도발 등)는 0이라 숫자가 뜨지 않는다.
        /// </summary>
        public int Amount { get; }

        /// <summary> 실드가 이 피해를 조금이라도 막았는지. 숫자 색을 실드색으로 바꾼다. </summary>
        public bool Shielded { get; }

        /// <summary> 치명타였는지. 피해가 아닌 효과는 항상 false. </summary>
        public bool IsCrit { get; }

        public EffectResult(EffectType type, ICombatant target, int amount = 0, bool shielded = false,
            bool isCrit = false)
        {
            Type = type;
            Target = target;
            Amount = amount;
            Shielded = shielded;
            IsCrit = isCrit;
        }
    }
}
