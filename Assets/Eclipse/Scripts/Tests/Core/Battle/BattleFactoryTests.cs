using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Eclipse.Core;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Service;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    // BattleFactory가 프로덕션(BattleLifetimeScope)이 위임하는 그 조립 경로를 실제로 타는지 못박는다.
    // 이 이슈의 핵심 가치 = 조립 로직이 순수 C# 경계로 나와 테스트가 프로덕션과 같은 배선을 검증할 수 있다는 것.
    public class BattleFactoryTests
    {
        private static Stats S(int hp, int atk, int def, int spd)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = 0f, critDamage = 1.5f };

        private static SkillSO Skill(string id)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = id;
            s.displayName = id;
            s.cooldownTurns = 0;
            s.effects = new List<SkillEffect>
            {
                new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = 1f }
            };
            return s;
        }

        private static OwnedCharacter Owned(string name)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = name;
            so.baseStats = S(1000, 100, 0, 100);
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
            so.basicSkill = Skill(name + "_b");
            return new OwnedCharacter(so, 1);
        }

        private static EnemySO Enemy(string name)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = name;
            so.baseStats = S(1000, 50, 0, 90);
            so.basicSkill = Skill(name + "_b");
            return so;
        }

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        private static BattleFactory BuildFactory(PlayerSave save)
        {
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(1)));
            var executor = new SkillExecutor(combat, targeting);
            var constants = ScriptableObject.CreateInstance<BattleConstantsSO>();
            return new BattleFactory(save, constants, targeting, combat, executor, new FakeSceneFlow());
        }

        [Test]
        public void Create는_양_진영_앞에서_partySize명씩_뽑고_아군을_먼저_배치한다()
        {
            // 로스터·적 편성 모두 partySize보다 많게 준비 → 앞에서부터 잘라 쓰는지 검증
            var save = new PlayerSave(new List<OwnedCharacter> { Owned("A0"), Owned("A1"), Owned("A2") });
            var enemies = new[] { Enemy("E0"), Enemy("E1"), Enemy("E2") };
            const int partySize = 2;

            var vm = BuildFactory(save).Create(enemies, battleSeed: 12345, startAuto: false, partySize);

            Assert.AreEqual(partySize, vm.Combatants.Count(u => u.IsAlly), "아군은 로스터 앞 partySize명");
            Assert.AreEqual(partySize, vm.Combatants.Count(u => !u.IsAlly), "적은 편성 앞 partySize명");
            // Combatants 순서 = 스케줄러 입력 순(아군 먼저, 그다음 적) — 앞 partySize개가 전부 아군
            Assert.IsTrue(vm.Combatants.Take(partySize).All(u => u.IsAlly), "아군이 적보다 먼저 배치된다");
        }
    }
}
