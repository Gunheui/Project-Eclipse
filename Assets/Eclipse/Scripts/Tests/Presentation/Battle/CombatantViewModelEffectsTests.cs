using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

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
        public void 출처_집합은_스킬마다_한_번씩만_들어간다()
        {
            var shield = ScriptableObject.CreateInstance<SkillSO>();
            var curse = ScriptableObject.CreateInstance<SkillSO>();
            var effects = new List<StatusEffect>
            {
                StatusEffect.StatModifier(EffectType.Buff, StatType.Def, 0.4f, 2, shield),
                StatusEffect.Taunt(2, shield),
                StatusEffect.Periodic(EffectType.Dot, 10, 3, curse),
                StatusEffect.Shield(100, 2), // 스킬 밖에서 만든 효과는 출처가 없다
            };

            var sources = CombatantViewModel.BuildEffectSources(effects);

            CollectionAssert.AreEquivalent(new[] { shield, curse }, sources);
        }

        [Test]
        public void 한_스킬의_효과_하나가_끝나도_남은_효과가_있으면_출처는_살아_있다()
        {
            var fortress = ScriptableObject.CreateInstance<SkillSO>();
            var spentShield = StatusEffect.Shield(200, 2, fortress);
            spentShield.AbsorbDamage(200);
            var effects = new List<StatusEffect>
            {
                spentShield,
                StatusEffect.StatModifier(EffectType.Buff, StatType.Def, 0.4f, 2, fortress),
            };

            var sources = CombatantViewModel.BuildEffectSources(effects);

            CollectionAssert.AreEquivalent(new[] { fortress }, sources,
                "실드가 먼저 터져도 같은 스킬의 방어 버프가 남는 동안은 출처가 유지된다");
        }

        [Test]
        public void 만료된_효과의_출처는_빠진다()
        {
            var skill = ScriptableObject.CreateInstance<SkillSO>();
            var spent = StatusEffect.Shield(100, 3, skill);
            spent.AbsorbDamage(100);
            var expired = StatusEffect.Taunt(1, skill);
            expired.TickDuration();

            var sources = CombatantViewModel.BuildEffectSources(new List<StatusEffect> { spent, expired });

            CollectionAssert.IsEmpty(sources, "흡수량을 다 쓴 실드와 턴이 다한 도발은 정산 전까지 목록에 남는다");
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
