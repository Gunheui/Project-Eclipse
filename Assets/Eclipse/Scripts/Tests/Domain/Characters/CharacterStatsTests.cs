using Eclipse.Data;
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

        [Test]
        public void Lv1은_기본_스탯을_그대로_쓴다()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.ScaleToLevel(def, 1);

            Assert.AreEqual(1000, stats.hp);
            Assert.AreEqual(175, stats.atk);
            Assert.AreEqual(60, stats.def, "Lv1은 성장 계수 ×1.0이라 기본값 그대로");
        }

        [Test]
        public void Lv10은_HP_ATK_DEF를_성장곡선으로_스케일한다()
        {
            // g=0.07, Lv10 → 계수 1 + 0.07×9 = 1.63
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.ScaleToLevel(def, 10);

            Assert.AreEqual(1630, stats.hp, "1000 × 1.63 = 1630");
            Assert.AreEqual(285, stats.atk, "175 × 1.63 = 285.25 → 285");
            Assert.AreEqual(98, stats.def, "60 × 1.63 = 97.8 → 98");
        }

        [Test]
        public void SPD_치명확률_치명배율은_레벨과_무관하게_불변()
        {
            var def = Definition(1000, 175, 60, 120, 0.3f, 2.0f);

            var stats = CharacterStats.ScaleToLevel(def, 10);

            Assert.AreEqual(120, stats.spd, "SPD는 스케일 대상이 아니다");
            Assert.AreEqual(0.3f, stats.critRate);
            Assert.AreEqual(2.0f, stats.critDamage);
        }
    }
}
