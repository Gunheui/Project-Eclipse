using System;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    public class CharacterStatsTests
    {
        // 기본 스탯·성장곡선(g=0.07)을 얹은 인메모리 캐릭터 정의. 공식: base × (1 + g×(level−1)).
        private static CharacterSO Definition(int hp, int atk, int def, int spd, float cr, float cd)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.baseStats = new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = cr, critDamage = cd };
            var curve = ScriptableObject.CreateInstance<GrowthCurve>();
            curve.growthRate = 0.07f;
            curve.maxLevel = 30;
            so.growthCurve = curve;
            return so;
        }

        private static StatModifierSet Buffs(params (StatType axis, float value)[] deltas)
        {
            var set = new StatModifierSet();
            foreach (var (axis, value) in deltas)
                set.Add(new StatDelta { axis = axis, value = value });
            return set;
        }

        [Test]
        public void Lv1_미돌파_무버프는_기본_스탯을_그대로_쓴다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.BuildAllyStats(def, 1, 0, null);

            Assert.AreEqual(1000, stats.hp);
            Assert.AreEqual(175, stats.atk);
            Assert.AreEqual(60, stats.def, "Lv1은 성장 계수 ×1.0이라 기본값 그대로");
        }

        [Test]
        public void Lv10은_HP_ATK_DEF를_성장곡선으로_스케일한다()
        {
            // g=0.07, Lv10 → 계수 1 + 0.07×9 = 1.63
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.BuildAllyStats(def, 10, 0, null);

            Assert.AreEqual(1630, stats.hp, "1000 × 1.63 = 1630");
            Assert.AreEqual(285, stats.atk, "175 × 1.63 = 285.25 → 285");
            Assert.AreEqual(98, stats.def, "60 × 1.63 = 97.8 → 98");
        }

        [Test]
        public void Lv30은_기본_스탯의_약_3_03배다()
        {
            // g=0.07, Lv30 → 계수 1 + 0.07×29 = 3.03
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.BuildAllyStats(def, 30, 0, null);

            Assert.AreEqual(3030, stats.hp, "1000 × 3.03 = 3030");
            Assert.AreEqual(530, stats.atk, "175 × 3.03 = 530.25 → 530");
            Assert.AreEqual(182, stats.def, "60 × 3.03 = 181.8 → 182");
        }

        [Test]
        public void 돌파는_단계당_HP_ATK를_8퍼센트씩_올리고_DEF는_불변이다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.BuildAllyStats(def, 1, 3, null); // ×(1 + 0.08×3) = ×1.24

            Assert.AreEqual(1240, stats.hp, "1000 × 1.24 = 1240");
            Assert.AreEqual(217, stats.atk, "175 × 1.24 = 217 → 217");
            Assert.AreEqual(60, stats.def, "돌파는 DEF에 적용되지 않는다");
        }

        [Test]
        public void 레벨과_돌파_배수는_한_번에_접혀_마지막에_반올림된다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.BuildAllyStats(def, 10, 1, null);

            Assert.AreEqual(308, stats.atk, "175 × 1.63 × 1.08 = 308.07 → 308(중간 반올림 없음)");
            Assert.AreEqual(1760, stats.hp, "1000 × 1.63 × 1.08 = 1760.4 → 1760");
        }

        [Test]
        public void 버프는_퍼센트_가산으로_접히고_치명은_퍼센트포인트_가산이다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);
            var buffs = Buffs((StatType.Atk, 0.25f), (StatType.Spd, 0.10f), (StatType.CritRate, 0.15f), (StatType.CritDamage, 0.5f));

            var stats = CharacterStats.BuildAllyStats(def, 1, 0, buffs);

            Assert.AreEqual(219, stats.atk, "175 × 1.25 = 218.75 → 219");
            Assert.AreEqual(132, stats.spd, "120 × 1.10 = 132 — SPD는 버프로만 오른다");
            Assert.AreEqual(0.45f, stats.critRate, 1e-5f, "0.3 + 0.15%p");
            Assert.AreEqual(2.5f, stats.critDamage, 1e-5f, "2.0 + 0.5%p");
            Assert.AreEqual(1000, stats.hp, "버프 없는 축은 불변");
        }

        [Test]
        public void 치명확률은_1을_넘지_않는다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);
            var buffs = Buffs((StatType.CritRate, 0.9f));

            var stats = CharacterStats.BuildAllyStats(def, 1, 0, buffs);

            Assert.AreEqual(1f, stats.critRate, "0.3 + 0.9 = 1.2 → 1.0 고정");
        }

        [Test]
        public void 정수_스탯의_하한은_1이다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);
            var buffs = Buffs((StatType.Hp, -1.0f)); // Σ = −100% → 0으로 떨어질 값

            var stats = CharacterStats.BuildAllyStats(def, 1, 0, buffs);

            Assert.AreEqual(1, stats.hp, "0이 아니라 하한 1");
        }

        [Test]
        public void 레벨_범위를_벗어나면_예외를_던진다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f); // maxLevel = 30

            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterStats.BuildAllyStats(def, 0, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterStats.BuildAllyStats(def, 31, 0, null));
        }

        [Test]
        public void 돌파_범위를_벗어나면_예외를_던진다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterStats.BuildAllyStats(def, 1, -1, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CharacterStats.BuildAllyStats(def, 1, OwnedCharacter.MaxAscensionTier + 1, null));
        }

        [Test]
        public void SPD_치명확률_치명배율은_레벨과_무관하게_불변()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.BuildAllyStats(def, 10, 0, null);

            Assert.AreEqual(120, stats.spd, "SPD는 레벨 스케일 대상이 아니다");
            Assert.AreEqual(0.3f, stats.critRate);
            Assert.AreEqual(2.0f, stats.critDamage);
        }
    }
}
