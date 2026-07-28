using System;
using Eclipse.Data;
using Eclipse.Data.Enums;

namespace Eclipse.Domain
{
    /// <summary>
    /// 캐릭터 최종 스탯의 유일한 계산처. 레벨·돌파·런 버프를 여기서만 계산한다.
    /// 표시(상세 화면)와 전투(Combatant 조립)가 같은 공식을 공유하며, 전투 코어는 완성된 Stats만 받는다.
    /// </summary>
    public static class CharacterStats
    {
        /// <summary> 돌파 1단계당 HP·ATK 증가율(+8%). </summary>
        private const float AscensionBonusPerTier = 0.08f;

        /// <summary> 아군 최종 스탯을 계산한다. 레벨·돌파·런 버프가 모두 반영된 완성값이다. </summary>
        /// <param name="level">현재 레벨. 1 이상 curve.maxLevel 이하만 허용한다.</param>
        /// <param name="ascensionTier">돌파 단계. 0 이상 <see cref="OwnedCharacter.MaxAscensionTier"/> 이하만 허용한다.</param>
        /// <param name="buffs">이 캐릭터가 런에서 받은 버프 합산. 없으면 null.</param>
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
            float ascension = 1f + AscensionBonusPerTier * ascensionTier; // 돌파 배수는 HP·ATK에만 곱한다
            return new Stats
            {
                hp = ApplyBuffAndRound(curve.StatAtLevel(baseStats.hp, level) * ascension, buffs, StatType.Hp),
                atk = ApplyBuffAndRound(curve.StatAtLevel(baseStats.atk, level) * ascension, buffs, StatType.Atk),
                def = ApplyBuffAndRound(curve.StatAtLevel(baseStats.def, level), buffs, StatType.Def),
                spd = ApplyBuffAndRound(baseStats.spd, buffs, StatType.Spd), // SPD는 레벨 스케일이 없다
                // 치명 계열은 %p 가산이며 치명확률은 [0, 1]로 고정한다.
                critRate = Math.Clamp(baseStats.critRate + SumOf(buffs, StatType.CritRate), 0f, 1f),
                critDamage = baseStats.critDamage + SumOf(buffs, StatType.CritDamage),
            };
        }

        /// <summary> 적 최종 스탯을 계산한다. 챕터 난이도·변이·정예 배수와 디버프가 모두 반영된 완성값이다. </summary>
        /// <param name="baseStats">적 정의의 고정 스탯. 적은 레벨 스케일이 없다.</param>
        /// <param name="chapterMultiplier">챕터 난이도 배수(<see cref="ChapterSO.enemyStatMultiplier"/>).</param>
        /// <param name="mutation">침식 변이. 없으면 null. 변이가 지정한 스탯 하나에만 배수가 걸린다.</param>
        /// <param name="eliteMultiplier">정예 배수. 일반 인카운터면 1을 넘긴다.</param>
        /// <param name="enemyDebuffs">런 전역으로 적에게 걸린 디버프 합. 깎는 값이라 음수이며, 없으면 null.</param>
        public static Stats BuildEnemyStats(Stats baseStats, float chapterMultiplier, MutationSO mutation,
            float eliteMultiplier, StatModifierSet enemyDebuffs)
        {
            float common = chapterMultiplier * eliteMultiplier; // 배수는 HP·ATK·DEF·SPD에만 걸린다
            return new Stats
            {
                hp = ApplyBuffAndRound(baseStats.hp * common * MutationMultiplier(mutation, StatType.Hp), enemyDebuffs, StatType.Hp),
                atk = ApplyBuffAndRound(baseStats.atk * common * MutationMultiplier(mutation, StatType.Atk), enemyDebuffs, StatType.Atk),
                def = ApplyBuffAndRound(baseStats.def * common * MutationMultiplier(mutation, StatType.Def), enemyDebuffs, StatType.Def),
                spd = ApplyBuffAndRound(baseStats.spd * common * MutationMultiplier(mutation, StatType.Spd), enemyDebuffs, StatType.Spd),
                // 치명 계열에는 배수를 곱하지 않고 디버프만 더한다.
                critRate = Math.Clamp(baseStats.critRate + SumOf(enemyDebuffs, StatType.CritRate), 0f, 1f),
                critDamage = Math.Max(0f, baseStats.critDamage + SumOf(enemyDebuffs, StatType.CritDamage)),
            };
        }

        /// <summary> 이 스탯에 걸리는 변이 배수를 돌려준다. 변이가 없거나 다른 스탯을 올리는 변이면 1이다. </summary>
        private static float MutationMultiplier(MutationSO mutation, StatType axis)
            => mutation != null && mutation.statAxis == axis ? mutation.multiplier : 1f;

        /// <summary> 증감 합을 %가산으로 곱한 뒤 한 번만 반올림한다. 아군 버프와 적 디버프가 이 경로를 같이 쓴다. </summary>
        private static int ApplyBuffAndRound(float value, StatModifierSet buffs, StatType axis)
            => Round(value * (1f + SumOf(buffs, axis)));

        /// <summary> 반올림한 뒤 하한 1을 적용한다. </summary>
        private static int Round(float value)
            // AwayFromZero는 전투 파이프라인과 맞춘 것으로, .5 처리 방식이 갈리면 결정성이 흔들린다.
            // 하한 1은 spd가 게이지 나눗셈 분모라 0이 금지되는 규칙을 겸한다.
            => Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));

        private static float SumOf(StatModifierSet modifiers, StatType axis)
            => modifiers?.SumOf(axis) ?? 0f;
    }
}
