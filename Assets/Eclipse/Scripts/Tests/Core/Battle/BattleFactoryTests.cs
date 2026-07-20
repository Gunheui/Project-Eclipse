using System;
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

        private static BattleFactory BuildFactory()
        {
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(1)));
            var executor = new SkillExecutor(combat, targeting);
            var constants = ScriptableObject.CreateInstance<BattleConstantsSO>();
            return new BattleFactory(constants, targeting, combat, executor, new FakeSceneFlow());
        }

        [Test]
        public void 명시_파티가_로스터_순서가_아니라_그대로_아군에_반영된다()
        {
            var a0 = Owned("A0");
            var a1 = Owned("A1");
            var a2 = Owned("A2");
            // 선택 파티 순서를 로스터 순서와 다르게 → 팩토리가 파티 순서를 그대로 쓰는지 검증
            var party = new List<OwnedCharacter> { a2, a0, a1 };
            var enemies = new[] { Enemy("E0") };

            var vm = BuildFactory().Create(party, enemies, battleSeed: 12345, startAuto: false);

            var allies = vm.Combatants.Where(u => u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            CollectionAssert.AreEqual(new[] { "A2", "A0", "A1" }, allies.Select(u => u.Name).ToList(),
                "아군은 선택 파티 순서를 그대로 따른다(로스터 순서 아님)");
        }

        [Test]
        public void 아군_slotIndex는_편성_인덱스를_그대로_쓴다()
        {
            var party = new List<OwnedCharacter> { Owned("A0"), Owned("A1"), Owned("A2") };
            var vm = BuildFactory().Create(party, new[] { Enemy("E0") }, 1, false);

            var slots = vm.Combatants.Where(u => u.IsAlly).Select(u => u.SlotIndex).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, slots);
        }

        [Test]
        public void 중간_빈칸은_자리를_비운_채_뒤_슬롯_번호가_유지된다()
        {
            // 편성 1·3번 칸(인덱스 0·2)에만 배치 → 전투에서도 0·2번 자리에 서야 한다(1로 당겨지면 안 됨)
            var party = new List<OwnedCharacter> { Owned("A0"), null, Owned("A2"), null };

            var vm = BuildFactory().Create(party, new[] { Enemy("E0") }, 1, false);

            var allies = vm.Combatants.Where(u => u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            CollectionAssert.AreEqual(new[] { 0, 2 }, allies.Select(u => u.SlotIndex).ToList(),
                "빈칸은 건너뛰되 뒤 유닛의 자리 번호는 당겨지지 않는다");
            CollectionAssert.AreEqual(new[] { "A0", "A2" }, allies.Select(u => u.Name).ToList());
        }

        [Test]
        public void 아군은_최대_4자리까지만_참전한다()
        {
            var party = Enumerable.Range(0, 6).Select(i => Owned("A" + i)).ToList();

            var vm = BuildFactory().Create(party, new[] { Enemy("E0") }, 1, false);

            var slots = vm.Combatants.Where(u => u.IsAlly).Select(u => u.SlotIndex).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, slots, "진영 자리는 4개뿐이라 그 뒤는 잘린다");
        }

        [Test]
        public void 적은_스테이지_편성_전부_참전하며_아군_수를_따르지_않는다()
        {
            var party = new List<OwnedCharacter> { Owned("A0") };          // 아군 1명
            var enemies = new[] { Enemy("E0"), Enemy("E1"), Enemy("E2") }; // 적 3

            var vm = BuildFactory().Create(party, enemies, 1, false);

            Assert.AreEqual(1, vm.Combatants.Count(u => u.IsAlly));
            Assert.AreEqual(3, vm.Combatants.Count(u => !u.IsAlly),
                "적 수는 아군(1)이 아니라 스테이지 편성(3)을 따른다");
        }

        [Test]
        public void 적은_4명_상한을_넘지_않는다()
        {
            var party = new List<OwnedCharacter> { Owned("A0") };
            var enemies = new[] { Enemy("E0"), Enemy("E1"), Enemy("E2"), Enemy("E3"), Enemy("E4") };

            var vm = BuildFactory().Create(party, enemies, 1, false);

            Assert.AreEqual(4, vm.Combatants.Count(u => !u.IsAlly), "적은 4v4 상한까지만 참전한다");
        }

        [Test]
        public void 아군이_적보다_먼저_배치된다()
        {
            var party = new List<OwnedCharacter> { Owned("A0"), Owned("A1") };
            var enemies = new[] { Enemy("E0"), Enemy("E1") };

            var vm = BuildFactory().Create(party, enemies, 1, false);

            Assert.IsTrue(vm.Combatants.Take(2).All(u => u.IsAlly), "앞 2개가 전부 아군");
        }

        [Test]
        public void 빈_또는_null_파티와_null_적은_예외()
        {
            var enemies = new[] { Enemy("E0") };
            var party = new List<OwnedCharacter> { Owned("A0") };

            Assert.Throws<ArgumentException>(
                () => BuildFactory().Create(new List<OwnedCharacter>(), enemies, 1, false), "빈 파티");
            Assert.Throws<ArgumentException>(
                () => BuildFactory().Create(new List<OwnedCharacter> { null, null }, enemies, 1, false), "전부 null 파티");
            Assert.Throws<ArgumentNullException>(
                () => BuildFactory().Create(null, enemies, 1, false), "null 파티");
            Assert.Throws<ArgumentNullException>(
                () => BuildFactory().Create(party, null, 1, false), "null 적");
        }
    }
}
