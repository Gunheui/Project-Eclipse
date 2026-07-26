using System;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 캐릭터 최종 스탯의 유일한 계산처. 레벨·돌파·스테이지 버프를 여기서만 계산한다.
    /// 표시(상세 화면)와 전투(Combatant 조립)가 같은 공식을 공유하며, 전투 코어는 완성된 Stats만 받는다.
    /// </summary>
    public static class CharacterStats
    {
        /// <summary> 돌파 1단계당 HP·ATK 증가율(+8%). </summary>
        private const float AscensionBonusPerTier = 0.08f;

        /// <summary>
        /// 아군 최종 스탯을 계산한다. HP·ATK·DEF는 성장곡선으로 레벨 스케일하고, HP·ATK에는 돌파 배수(단계당 +8%)를
        /// 곱한 뒤, 스탯별 버프 %가산을 적용한다. 반올림(AwayFromZero)은 마지막에 한 번만 하며
        /// 정수 스탯의 하한은 1이다. 치명 계열은 %p 가산이고 치명확률은 [0, 1]로 고정한다.
        /// </summary>
        /// <param name="level">현재 레벨. 1 이상 curve.maxLevel 이하만 허용한다.</param>
        /// <param name="ascensionTier">돌파 단계. 0 이상 <see cref="OwnedCharacter.MaxAscensionTier"/> 이하만 허용한다.</param>
        /// <param name="buffs">이 캐릭터가 스테이지에서 받은 버프 합산. 없으면 null.</param>
        /// <exception cref="ArgumentNullException">정의나 성장곡선이 null일 때.</exception>
        /// <exception cref="ArgumentOutOfRangeException">level·ascensionTier가 허용 범위를 벗어날 때.</exception>
        public static Stats BuildAllyStats(CharacterSO definition, int level, int ascensionTier, StatModifierSet buffs)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            var curve = definition.growthCurve;
            if (curve == null)
                throw new ArgumentNullException(nameof(definition), "growthCurve가 없다.");
            if (level < 1 || level > curve.maxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"레벨은 1 이상 {curve.maxLevel} 이하여야 한다.");
            if (ascensionTier < 0 || ascensionTier > OwnedCharacter.MaxAscensionTier)
                throw new ArgumentOutOfRangeException(nameof(ascensionTier), ascensionTier,
                    $"돌파 단계는 0 이상 {OwnedCharacter.MaxAscensionTier} 이하여야 한다.");

            var baseStats = definition.baseStats;
            float ascension = 1f + AscensionBonusPerTier * ascensionTier;
            return new Stats
            {
                hp = ApplyBuffAndRound(curve.StatAtLevel(baseStats.hp, level) * ascension, buffs, StatType.Hp),
                atk = ApplyBuffAndRound(curve.StatAtLevel(baseStats.atk, level) * ascension, buffs, StatType.Atk),
                def = ApplyBuffAndRound(curve.StatAtLevel(baseStats.def, level), buffs, StatType.Def),
                spd = ApplyBuffAndRound(baseStats.spd, buffs, StatType.Spd),
                critRate = Math.Clamp(baseStats.critRate + SumOf(buffs, StatType.CritRate), 0f, 1f),
                critDamage = baseStats.critDamage + SumOf(buffs, StatType.CritDamage),
            };
        }

        // 버프 %가산을 곱한 뒤 한 번만 반올림한다. 반올림은 전투 파이프라인과 같은 AwayFromZero —
        // .5 처리 방식이 갈리면 결정성이 흔들린다. 하한 1은 spd가 게이지 나눗셈 분모라 0이 금지되는 규칙을 겸한다.
        private static int ApplyBuffAndRound(float value, StatModifierSet buffs, StatType axis)
            => Math.Max(1, (int)Math.Round(value * (1f + SumOf(buffs, axis)), MidpointRounding.AwayFromZero));

        private static float SumOf(StatModifierSet buffs, StatType axis)
            => buffs?.SumOf(axis) ?? 0f;
    }
}
