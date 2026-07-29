using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Presentation
{
    /// <summary> 런 화면들이 공유하는 표기 변환. 표시 문구의 단일 원천이다. </summary>
    public static class RunTexts
    {
        /// <summary> 저주 카드의 귀속 표시. 캐릭터가 아니라 남은 적 전체에 붙는다. </summary>
        public const string EnemyTarget = "적 전체";

        /// <summary> 정산 행의 재화 표기 순서. </summary>
        private static readonly CurrencyType[] RewardColumns =
        {
            CurrencyType.Gold, CurrencyType.Manual, CurrencyType.Essence,
        };

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

        /// <summary> 등급 배지 문구. 등급색과 짝을 이루는 두 번째 채널이라 생략하지 않는다. </summary>
        /// <exception cref="ArgumentOutOfRangeException">표시명이 없는 등급일 때.</exception>
        public static string GradeLabel(CardGrade grade) => grade switch
        {
            CardGrade.Common => "커먼",
            CardGrade.Rare => "레어",
            CardGrade.Epic => "에픽",
            CardGrade.Unique => "유니크",
            _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "등급 표시명이 없다."),
        };

        /// <summary> 증감 하나의 표기("공격력 +15%" / "치명확률 +6%p"). 치명 계열만 %p 단위다. </summary>
        public static string FormatDelta(StatDelta delta)
        {
            bool point = delta.axis == StatType.CritRate || delta.axis == StatType.CritDamage;
            string sign = delta.value >= 0 ? "+" : "";
            return $"{StatName(delta.axis)} {sign}{delta.value * 100f:0.#}%{(point ? "p" : "")}";
        }

        /// <summary>
        /// 카드 효과 표기. 유니크는 축이 없어 적어 둔 설명을 그대로 쓴다.
        /// 누구에게 붙는지는 <see cref="CardOption.Target"/>이 따로 실으므로 여기 적지 않는다.
        /// </summary>
        public static string FormatCard(BuffCard card)
        {
            if (card.grade == CardGrade.Unique)
                return card.description;
            // 가운뎃점은 U+2027이다 — Pretendard 아틀라스에 U+00B7이 없어 그 문자를 쓰면 두부가 뜬다.
            return string.Join(" ‧ ", card.deltas.Select(FormatDelta));
        }

        /// <summary>
        /// 정산 행 하나의 재화 표기("골드 1,850 교본 2 보석 240"). 0인 재화도 자리를 지킨다.
        /// </summary>
        /// <param name="entries">그 행의 보상. null이면 전 재화 0으로 찍는다.</param>
        public static string FormatRewards(IReadOnlyList<RewardEntry> entries) => string.Join(string.Empty,
            // 세 재화가 행마다 같은 x에 서야 위아래로 비교된다. 비례폭 글꼴이라 공백으로는 못 맞춘다.
            RewardColumns.Select((type, column) =>
                $"<pos={column * 37}%>{CurrencyName(type)} {AmountOf(entries, type):N0}"));

        private static int AmountOf(IReadOnlyList<RewardEntry> entries, CurrencyType type)
            => entries?.Where(e => e.type == type).Sum(e => e.amount) ?? 0;
    }
}
