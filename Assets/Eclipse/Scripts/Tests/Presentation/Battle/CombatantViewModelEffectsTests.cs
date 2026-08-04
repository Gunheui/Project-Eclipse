using System.Collections.Generic;
using System.Linq;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class CombatantViewModelEffectsTests
    {
        [Test]
        public void 해로움이_앞서고_그룹안은_남은턴_오름차순_상시가_마지막이다()
        {
            var effects = new List<StatusEffect>
            {
                StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.4f, 2),
                StatusEffect.Shield(100, 3),
                StatusEffect.StatModifier(EffectType.Debuff, StatType.Def, 0.3f, -1),
                StatusEffect.Periodic(EffectType.Dot, 10, 1),
                StatusEffect.StatModifier(EffectType.Debuff, StatType.Atk, 0.3f, 1),
                StatusEffect.Taunt(2),
            };

            var result = CombatantViewModel.BuildActiveEffects(effects);

            var expected = new[]
            {
                (EffectType.Debuff, 1),
                (EffectType.Debuff, -1),
                (EffectType.Dot, 1),
                (EffectType.Taunt, 2),
                (EffectType.Shield, 3),
                (EffectType.Buff, 2),
            };
            CollectionAssert.AreEqual(expected, result.Select(e => (e.Type, e.RemainingTurns)).ToArray());
        }

        [Test]
        public void 런_버프는_같은_그룹의_전투_효과_뒤에_서고_저주는_맨_앞이다()
        {
            var effects = new List<StatusEffect>
            {
                StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.4f, 2),
                StatusEffect.Shield(100, 3),
                StatusEffect.StatModifier(EffectType.Debuff, StatType.Def, 0.3f, 1),
            };
            var runEffects = new[]
            {
                new ActiveEffect(EffectType.Buff, StatType.None, -1),
                new ActiveEffect(EffectType.Debuff, StatType.None, -1),
            };

            var result = CombatantViewModel.BuildActiveEffects(effects, runEffects);

            var expected = new[]
            {
                (EffectType.Debuff, 1),
                (EffectType.Debuff, -1),
                (EffectType.Shield, 3),
                (EffectType.Buff, 2),
                (EffectType.Buff, -1),
            };
            CollectionAssert.AreEqual(expected, result.Select(e => (e.Type, e.RemainingTurns)).ToArray());
        }

        [Test]
        public void 세기는_타입마다_다른_필드에서_온다()
        {
            var shield = StatusEffect.Shield(340, 2);
            shield.AbsorbDamage(40);
            var effects = new List<StatusEffect>
            {
                StatusEffect.StatModifier(EffectType.Buff, StatType.Atk, 0.3f, 2),
                StatusEffect.Periodic(EffectType.Dot, 120, 3),
                shield,
                StatusEffect.Taunt(1),
            };

            var byType = CombatantViewModel.BuildActiveEffects(effects).ToDictionary(e => e.Type);

            Assert.AreEqual(0.3f, byType[EffectType.Buff].Magnitude, 1e-4f, "버프는 변화율");
            Assert.AreEqual(120f, byType[EffectType.Dot].Magnitude, 1e-4f, "도트는 틱당 HP");
            Assert.AreEqual(300f, byType[EffectType.Shield].Magnitude, 1e-4f, "실드는 흡수하고 남은 양");
            Assert.AreEqual(0f, byType[EffectType.Taunt].Magnitude, 1e-4f, "도발은 세기가 없다");
        }

        [Test]
        public void 삽입_순서가_달라도_표시_순서는_같다()
        {
            var forward = new List<StatusEffect>
            {
                StatusEffect.StatModifier(EffectType.Debuff, StatType.Atk, 0.3f, 2),
                StatusEffect.Periodic(EffectType.Regen, 10, 1),
                StatusEffect.Shield(100, 3),
                StatusEffect.StatModifier(EffectType.Buff, StatType.Def, 0.4f, 2),
            };
            var reversed = Enumerable.Reverse(forward).ToList();

            var a = CombatantViewModel.BuildActiveEffects(forward).Select(e => (e.Type, e.RemainingTurns));
            var b = CombatantViewModel.BuildActiveEffects(reversed).Select(e => (e.Type, e.RemainingTurns));

            CollectionAssert.AreEqual(a.ToArray(), b.ToArray());
        }
    }
}
