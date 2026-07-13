using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    public class RuleBasedActionProviderTests
    {
        // 효과 1개짜리 스킬을 즉석 생성한다. cd=쿨, type=효과 종류.
        private static SkillSO Skill(string id, int cd, EffectType type)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = id;
            s.displayName = id;
            s.cooldownTurns = cd;
            s.effects = new List<SkillEffect>
            {
                new SkillEffect { type = type, target = TargetSelector.LowestHpEnemy, value = 1f }
            };
            return s;
        }

        private static SkillRuntime Runtime(SkillSO skill, int initialCooldown = 0)
            => new SkillRuntime(skill, initialCooldown);

        // TargetResolver·provider가 읽는 상태만 채우는 테스트용 유닛.
        private sealed class Combatant : ICombatant
        {
            public string DisplayName => "u";
            public Team Team { get; set; }
            public int SlotIndex { get; set; }
            public Stats EffectiveStats { get; set; }
            public int MaxHp { get; set; }
            public int CurrentHp { get; set; }
            public bool IsAlive => CurrentHp > 0;
            public IReadOnlyList<SkillRuntime> Skills { get; set; }
            public bool IsTaunting { get; set; }
        }

        private static Combatant Unit(int hp, int maxHp, params SkillRuntime[] skills)
            => new Combatant { CurrentHp = hp, MaxHp = maxHp, Skills = skills };

        private static readonly IReadOnlyList<ICombatant> NoEnemies = new List<ICombatant>();

        [UnityTest]
        public IEnumerator 아군_위급하면_준비된_힐스킬을_고른다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var heal = Runtime(Skill("h", 0, EffectType.Heal));
            var actor = Unit(hp: 30, maxHp: 100, basic, heal); // 30% < 40% 임계
            var provider = new RuleBasedActionProvider(0.4f, useHealRule: true);

            var action = await provider.DecideAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(heal, action.Skill);
        });

        [UnityTest]
        public IEnumerator 아군_멀쩡하면_힐_대신_강한_공격_액티브를_고른다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var normal = Runtime(Skill("n", 2, EffectType.Damage)); // 준비 상태(초기 쿨 0)
            var heal = Runtime(Skill("h", 0, EffectType.Heal));
            var actor = Unit(hp: 100, maxHp: 100, basic, normal, heal); // 위급 아님
            var provider = new RuleBasedActionProvider(0.4f, useHealRule: true);

            var action = await provider.DecideAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(normal, action.Skill); // 힐(슬롯 상위)이 아니라 공격 액티브
        });

        [UnityTest]
        public IEnumerator 강한_액티브가_쿨이면_기본공격으로_폴백() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var normal = Runtime(Skill("n", 2, EffectType.Damage), initialCooldown: 2); // 아직 쿨
            var actor = Unit(hp: 100, maxHp: 100, basic, normal);
            var provider = new RuleBasedActionProvider(0.4f, useHealRule: true);

            var action = await provider.DecideAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(basic, action.Skill);
        });

        [UnityTest]
        public IEnumerator 힐규칙_off면_위급해도_힐하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var heal = Runtime(Skill("h", 0, EffectType.Heal));
            var actor = Unit(hp: 10, maxHp: 100, basic, heal); // 위급하지만
            var provider = new RuleBasedActionProvider(0.4f, useHealRule: false); // 힐 규칙 off

            var action = await provider.DecideAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(basic, action.Skill); // 힐 아님
        });

        [UnityTest]
        public IEnumerator 지정_대상은_없이_결정한다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var actor = Unit(hp: 100, maxHp: 100, basic);
            var provider = new RuleBasedActionProvider(0.4f, useHealRule: true);

            var action = await provider.DecideAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.IsNull(action.Target); // 오토는 대상 미지정 → selector가 결정
        });
    }
}