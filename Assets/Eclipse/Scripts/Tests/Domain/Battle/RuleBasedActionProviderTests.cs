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
                new SkillEffect { type = type, target = TargetSelector.SingleEnemy, value = 1f }
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
            public int ShieldAbsorb { get; set; }
        }

        private static Combatant Unit(int hp, int maxHp, params SkillRuntime[] skills)
            => new Combatant { CurrentHp = hp, MaxHp = maxHp, Skills = skills };

        private static readonly IReadOnlyList<ICombatant> NoEnemies = new List<ICombatant>();

        // 스킬 선택 규칙만 검증하는 테스트라 타겟팅은 관심 밖. 정해둔 값만 돌려주는 정책 스텁을 주입한다.
        private sealed class StubTargetPolicy : ITargetSelectionPolicy
        {
            private readonly ICombatant _result;
            public StubTargetPolicy(ICombatant result = null) => _result = result;
            public ICombatant ChoosePrimaryTarget(
                ICombatant actor, SkillRuntime skill,
                IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies) => _result;
        }

        private static RuleBasedActionProvider Provider(bool useHealRule, ICombatant target = null)
            => new RuleBasedActionProvider(0.4f, useHealRule, new StubTargetPolicy(target));

        [UnityTest]
        public IEnumerator 아군_위급하면_준비된_힐스킬을_고른다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var heal = Runtime(Skill("h", 0, EffectType.Heal));
            var actor = Unit(hp: 30, maxHp: 100, basic, heal); // 30% < 40% 임계
            var provider = Provider(useHealRule: true);

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(heal, action.Skill);
        });

        [UnityTest]
        public IEnumerator 아군_멀쩡하면_힐_대신_강한_공격_액티브를_고른다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var normal = Runtime(Skill("n", 2, EffectType.Damage)); // 준비 상태(초기 쿨 0)
            var heal = Runtime(Skill("h", 0, EffectType.Heal));
            var actor = Unit(hp: 100, maxHp: 100, basic, normal, heal); // 위급 아님
            var provider = Provider(useHealRule: true);

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(normal, action.Skill); // 힐(슬롯 상위)이 아니라 공격 액티브
        });

        [UnityTest]
        public IEnumerator 강한_액티브가_쿨이면_기본공격으로_폴백() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var normal = Runtime(Skill("n", 2, EffectType.Damage), initialCooldown: 2); // 아직 쿨
            var actor = Unit(hp: 100, maxHp: 100, basic, normal);
            var provider = Provider(useHealRule: true);

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(basic, action.Skill);
        });

        [UnityTest]
        public IEnumerator 힐규칙_off면_위급해도_힐하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var heal = Runtime(Skill("h", 0, EffectType.Heal));
            var actor = Unit(hp: 10, maxHp: 100, basic, heal); // 위급하지만
            var provider = Provider(useHealRule: false); // 힐 규칙 off

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(basic, action.Skill); // 힐 아님
        });

        [UnityTest]
        public IEnumerator 정책이_고른_주_타겟을_Target에_담는다() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var actor = Unit(hp: 100, maxHp: 100, basic);
            var picked = Unit(hp: 50, maxHp: 100); // 정책이 고른 것으로 흉내낼 대상
            var provider = Provider(useHealRule: true, target: picked);

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.AreSame(picked, action.Target); // provider가 정책 결과를 그대로 Target으로 넘긴다
        });

        [UnityTest]
        public IEnumerator 정책이_null이면_Target도_null() => UniTask.ToCoroutine(async () =>
        {
            var basic = Runtime(Skill("b", 0, EffectType.Damage));
            var actor = Unit(hp: 100, maxHp: 100, basic);
            var provider = Provider(useHealRule: true); // 스텁이 null 반환(광역·힐 등)

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, NoEnemies, CancellationToken.None);

            Assert.IsNull(action.Target); // 주 타겟 없음 → 효과별 스코프가 대상을 정한다
        });
    }
}