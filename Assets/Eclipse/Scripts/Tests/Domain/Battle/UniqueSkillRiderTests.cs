using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    /// <summary> 유니크 카드가 붙인 효과가 스킬 에셋이 아니라 유닛별 런타임에만 얹히는지 검증한다. </summary>
    public class UniqueSkillRiderTests
    {
        private static Stats S(int hp, int atk, int def, int spd)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = 0f, critDamage = 1.5f };

        private static SkillSO Skill(string id, params SkillEffect[] effects)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = id;
            s.displayName = id;
            s.cooldownTurns = 0;
            s.effects = effects.ToList();
            return s;
        }

        private static SkillEffect Damage(float value)
            => new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = value };

        private static CharacterSO Character(Stats stats, SkillSO basic, SkillSO normal)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = "테스터";
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
            so.growthCurve.maxLevel = 30;
            so.basicSkill = basic;
            so.normalSkill = normal;
            return so;
        }

        private static Combatant Ally(CharacterSO so,
            IReadOnlyList<(SkillSlot slot, SkillEffect effect)> riders, int slot = 0)
        {
            var owned = new OwnedCharacter(so, 1, 0, new[] { 1, 1, 1 });
            return Combatant.FromCharacter(owned, slot, CharacterStats.BuildAllyStats(so, 1, 0, null), riders);
        }

        private static Combatant Enemy(Stats stats)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = "허수아비";
            so.baseStats = stats;
            so.basicSkill = Skill("enemy_basic", Damage(1f));
            return Combatant.FromEnemy(so, 0, so.baseStats);
        }

        private static SkillExecutor Executor()
        {
            var pipeline = new DamagePipeline(1f, 1f, 1f, new SeededRandom(1));
            return new SkillExecutor(new CombatPipeline(pipeline), new TargetResolver());
        }

        [Test]
        public void 라이더는_지목한_슬롯의_스킬에만_붙는다()
        {
            var basic = Skill("basic", Damage(1f));
            var normal = Skill("normal", Damage(1.5f));
            var rider = new SkillEffect
            {
                type = EffectType.Shield, target = TargetSelector.Self, value = 0.1f, duration = 2,
            };

            var unit = Ally(Character(S(1000, 100, 50, 100), basic, normal),
                new[] { (SkillSlot.Normal, rider) });

            Assert.AreEqual(1, unit.Skills[0].Effects.Count, "기본기는 그대로다");
            Assert.AreEqual(2, unit.Skills[1].Effects.Count);
            Assert.AreEqual(EffectType.Shield, unit.Skills[1].Effects[1].type);
        }

        [Test]
        public void 라이더가_붙어도_스킬_에셋의_효과_수는_그대로다()
        {
            var basic = Skill("basic", Damage(1f));
            var normal = Skill("normal", Damage(1.5f));
            var so = Character(S(1000, 100, 50, 100), basic, normal);

            Ally(so, new[] { (SkillSlot.Basic, Damage(0.7f)) });
            Ally(so, new[] { (SkillSlot.Basic, Damage(0.7f)) });

            Assert.AreEqual(1, basic.effects.Count, "공유 에셋은 몇 번 조립해도 오염되지 않는다");
        }

        [Test]
        public void 라이더가_없으면_원본_목록을_그대로_가리킨다()
        {
            var basic = Skill("basic", Damage(1f));
            var unit = Ally(Character(S(1000, 100, 50, 100), basic, null), null);

            Assert.AreSame(basic.effects, unit.Skills[0].Effects, "수정이 없으면 사본을 만들지 않는다");
        }

        [Test]
        public void 멀티히트는_타격_대상을_타수만큼_보고한다()
        {
            var basic = Skill("basic", Damage(1f));
            var actor = Ally(Character(S(1000, 100, 0, 100), basic, null),
                new[] { (SkillSlot.Basic, Damage(0.7f)) });
            var target = Enemy(S(10000, 100, 0, 100));

            var affected = Executor().ApplySkill(actor, actor.Skills[0], target,
                new List<ICombatant> { actor }, new List<ICombatant> { target });

            Assert.AreEqual(2, affected.Count, "2타가 타격 신호 2회로 나간다");
            Assert.IsTrue(affected.All(r => r.Target == target));
        }

        [Test]
        public void 피해가_아닌_효과가_같은_대상에_겹치면_타격은_한_번이다()
        {
            var basic = Skill("basic", Damage(1f));
            var dot = new SkillEffect
            {
                type = EffectType.Dot, target = TargetSelector.SingleEnemy, value = 0.12f, duration = 2,
            };
            var actor = Ally(Character(S(1000, 100, 0, 100), basic, null), new[] { (SkillSlot.Basic, dot) });
            var target = Enemy(S(10000, 100, 0, 100));

            var affected = Executor().ApplySkill(actor, actor.Skills[0], target,
                new List<ICombatant> { actor }, new List<ICombatant> { target });

            Assert.AreEqual(1, affected.Count, "라이더는 타수를 늘리지 않는다");
        }

        [Test]
        public void 남은_HP보다_큰_피해도_계산된_데미지_전부를_보고한다()
        {
            var basic = Skill("basic", Damage(1f));
            var actor = Ally(Character(S(1000, 100, 0, 100), basic, null), null);
            var target = Enemy(S(1000, 100, 0, 100));
            target.ApplyDamage(995); // HP 5

            var affected = Executor().ApplySkill(actor, actor.Skills[0], target,
                new List<ICombatant> { actor }, new List<ICombatant> { target });

            Assert.AreEqual(0, target.CurrentHp);
            Assert.AreEqual(100, affected.Single().Amount, "마무리 일격도 남은 HP가 아니라 들어간 피해로 보고한다");
        }

        [Test]
        public void 디버프를_걸고_때리는_스킬은_대상_기록이_하나다()
        {
            var debuff = new SkillEffect
            {
                type = EffectType.Debuff, target = TargetSelector.SingleEnemy,
                value = 0.3f, affectedStat = StatType.Def, duration = 2,
            };
            var normal = Skill("normal", debuff, Damage(0.5f));
            var actor = Ally(Character(S(1000, 100, 0, 100), Skill("basic", Damage(1f)), normal), null);
            var target = Enemy(S(10000, 100, 0, 100));

            var affected = Executor().ApplySkill(actor, actor.Skills[1], target,
                new List<ICombatant> { actor }, new List<ICombatant> { target });

            Assert.AreEqual(1, affected.Count, "디버프 기록 자리를 피해가 물려받아 타격 이펙트가 한 번만 나간다");
            Assert.AreEqual(EffectType.Damage, affected[0].Type);
            Assert.Greater(affected[0].Amount, 0, "숫자를 든 피해 기록이 남는다");
        }

        [Test]
        public void 최저HP_아군_라이더는_수동_지정을_따르지_않는다()
        {
            var basic = Skill("basic", Damage(1f));
            var regen = new SkillEffect
            {
                type = EffectType.Regen, target = TargetSelector.LowestHpAlly, value = 0.12f, duration = 2,
            };
            var healer = Ally(Character(S(1000, 100, 0, 100), basic, null), new[] { (SkillSlot.Basic, regen) });
            var hurt = Ally(Character(S(1000, 100, 0, 100), Skill("other", Damage(1f)), null), null, slot: 1);
            hurt.ApplyDamage(600);
            var target = Enemy(S(10000, 100, 0, 100));

            // 시전자 자신을 지정해도 회복은 체력이 가장 낮은 아군에게 간다.
            Executor().ApplySkill(healer, healer.Skills[0], healer,
                new List<ICombatant> { healer, hurt }, new List<ICombatant> { target });

            Assert.IsTrue(hurt.Effects.Any(e => e.Type == EffectType.Regen));
            Assert.IsFalse(healer.Effects.Any(e => e.Type == EffectType.Regen));
        }
    }
}
