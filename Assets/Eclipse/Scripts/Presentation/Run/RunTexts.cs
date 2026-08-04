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

        /// <summary> 미드보스 문의 표시명. 카탈로그에 없는 문이라 문구를 코드가 보유한다. </summary>
        public const string MidBossDoorName = "미드보스의 문";

        /// <summary> 미드보스 문의 약속. 걸린 보상 2종을 일반 문과 같은 문구로 한 줄씩 적는다. </summary>
        public static string MidBossPromise(IEnumerable<string> rewards) => string.Join("\n", rewards);

        /// <summary> 포기 확인 팝업 제목. </summary>
        public const string AbandonTitle = "게임을 포기하시겠습니까?";

        /// <summary> 포기 확인 팝업 본문. 되돌릴 수 없는 조작이라 잃는 것을 적는다. </summary>
        public const string AbandonBody = "지금까지 획득한 재화는 모두 사라집니다.";

        /// <summary> 편성 미달로 런을 시작하려 할 때의 안내 팝업 제목. </summary>
        public const string PartyNotFullTitle = "편성 안내";

        /// <summary> 편성 미달 안내 본문. </summary>
        public const string PartyNotFullBody = "파티 인원(4인)이 부족합니다.";

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

        /// <summary> 지속 효과 한 줄("공격력 +30%  2턴"). 상시 효과(-1)는 턴 접미를 달지 않는다. </summary>
        public static string EffectLine(ActiveEffect effect)
        {
            string label = effect.Type switch
            {
                EffectType.Buff => FormatDelta(new StatDelta { axis = effect.Stat, value = effect.Magnitude }),
                // 디버프는 세기를 양수로 저장하고 부호를 타입이 대신한다(Combatant.ComputeEffectiveStats와 같은 규약).
                EffectType.Debuff => FormatDelta(new StatDelta { axis = effect.Stat, value = -effect.Magnitude }),
                EffectType.Dot => $"지속 피해 {effect.Magnitude:N0}",
                EffectType.Regen => $"재생 {effect.Magnitude:N0}",
                EffectType.Shield => $"보호막 {effect.Magnitude:N0}",
                EffectType.Taunt => "도발",
                _ => effect.Type.ToString(),
            };
            return effect.RemainingTurns > 0 ? $"{label}  {effect.RemainingTurns}턴" : label;
        }

        /// <summary> 변이가 거는 배수 한 줄("생명력 1.5배"). 변이 이름은 유닛 표시명에 이미 접두로 붙는다. </summary>
        public static string MutationEffect(MutationSO mutation)
            => $"{StatName(mutation.statAxis)} {mutation.multiplier:0.##}배";

        /// <summary>
        /// 최종 스탯 한 줄("공격력 1240   방어력 310   속도 118   치명 15% / 150%").
        /// 생명력은 머리 위 HP 바가 이미 보여주므로 빼고, 치명은 확률과 배율을 한 칸에 묶는다.
        /// </summary>
        public static string StatLine(Stats stats)
            => $"{StatName(StatType.Atk)} {stats.atk:N0}   {StatName(StatType.Def)} {stats.def:N0}   "
             + $"{StatName(StatType.Spd)} {stats.spd:N0}   치명 {stats.critRate * 100f:0.#}% / {stats.critDamage * 100f:0.#}%";

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
