using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.View
{
    /// <summary> 런 팝업들이 공유하는 표기 변환. 표시 문구의 단일 원천이다. </summary>
    public static class RunTexts
    {
        /// <summary> 재화 표시명. </summary>
        public static string CurrencyName(CurrencyType type) => type switch
        {
            CurrencyType.Gold => "골드",
            CurrencyType.Manual => "교본",
            CurrencyType.Essence => "보석",
            _ => type.ToString(),
        };

        /// <summary> 스탯 표시명. </summary>
        public static string StatName(StatType axis) => axis switch
        {
            StatType.Hp => "생명력",
            StatType.Atk => "공격력",
            StatType.Def => "방어력",
            StatType.Spd => "속도",
            StatType.CritRate => "치명확률",
            StatType.CritDamage => "치명피해",
            _ => axis.ToString(),
        };

        /// <summary> 증감 하나의 표기("공격력 +15%" / "치명확률 +6%p"). 치명 계열만 %p 단위다. </summary>
        public static string FormatDelta(StatDelta delta)
        {
            bool point = delta.axis == StatType.CritRate || delta.axis == StatType.CritDamage;
            string sign = delta.value >= 0 ? "+" : "";
            return $"{StatName(delta.axis)} {sign}{delta.value * 100f:0.#}%{(point ? "p" : "")}";
        }

        /// <summary> 카드 효과 전체 표기(증감을 " · "로 잇는다). 저주 카드는 약화 문구로 바꾼다. </summary>
        public static string FormatCard(BuffCard card)
        {
            var parts = new string[card.deltas.Length];
            for (int i = 0; i < card.deltas.Length; i++)
            {
                var d = card.deltas[i];
                parts[i] = card.targetsEnemies
                    ? $"적 전체 {StatName(d.axis)} -{d.value * 100f:0.#}%"
                    : FormatDelta(d);
            }
            return string.Join(" · ", parts);
        }
    }
}