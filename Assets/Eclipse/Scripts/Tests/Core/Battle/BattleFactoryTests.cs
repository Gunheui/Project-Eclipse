using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Core;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Eclipse.Tests
{
    // BattleFactory가 프로덕션(ChapterRunDriver)이 위임하는 그 조립 경로를 실제로 거치는지 못박는다.
    // 파티·버프·챕터 계수는 런 세션에서 읽고, 방마다 재호출되어 전부 새 인스턴스가 선다.
    public class BattleFactoryTests
    {
        private static EncounterSpec Encounter(params EnemySO[] enemies)
            => new EncounterSpec(enemies.Select(e => new EnemyInstanceSpec(e, null, false)).ToList());

        private static ChapterRunSession Session(IReadOnlyList<OwnedCharacter> party, float enemyMultiplier = 1f)
        {
            var chapter = RunFixtures.Chapter(RunFixtures.Normal(1, false), RunFixtures.Boss());
            chapter.enemyStatMultiplier = enemyMultiplier;
            return new ChapterRunSession(chapter, RunFixtures.Tuning(), party, runSeed: 1);
        }

        private static BattleFactory Factory(ChapterRunSession session)
            => new BattleFactory(ScriptableObject.CreateInstance<BattleConstantsSO>(), session, RunFixtures.Tuning());

        private static BattleViewModel Create(IReadOnlyList<OwnedCharacter> party, EnemySO[] enemies,
            int battleSeed = 1, bool startAuto = false)
            => Factory(Session(party)).Create(Encounter(enemies), battleSeed, startAuto);

        [Test]
        public void 세션_파티가_슬롯_순서_그대로_아군에_반영된다()
        {
            var party = new List<OwnedCharacter>
            {
                RunFixtures.Owned("A2"), RunFixtures.Owned("A0"), RunFixtures.Owned("A1")
            };

            var vm = Create(party, new[] { RunFixtures.Enemy("E0") });

            var allies = vm.Combatants.Where(u => u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            CollectionAssert.AreEqual(new[] { "A2", "A0", "A1" }, allies.Select(u => u.Name).ToList(),
                "아군은 세션 파티 순서를 그대로 따른다");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, allies.Select(u => u.SlotIndex).ToList());
        }

        [Test]
        public void 중간_빈칸은_자리를_비운_채_뒤_슬롯_번호가_유지된다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0"), null, RunFixtures.Owned("A2"), null };

            var vm = Create(party, new[] { RunFixtures.Enemy("E0") });

            var allies = vm.Combatants.Where(u => u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            CollectionAssert.AreEqual(new[] { 0, 2 }, allies.Select(u => u.SlotIndex).ToList(),
                "빈칸은 건너뛰되 뒤 유닛의 자리 번호는 당겨지지 않는다");
        }

        [Test]
        public void 적은_인카운터_편성_전부_참전하며_4명_상한을_지킨다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };
            var five = Enumerable.Range(0, 5).Select(i => RunFixtures.Enemy("E" + i)).ToArray();

            var vm = Create(party, five);

            Assert.AreEqual(1, vm.Combatants.Count(u => u.IsAlly));
            Assert.AreEqual(4, vm.Combatants.Count(u => !u.IsAlly), "적은 4v4 상한까지만 참전한다");
        }

        [Test]
        public void 런_버프가_아군_최종_스탯에_접혀_들어간다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };
            var session = Session(party);
            session.AttachCard(new BuffCard
            {
                id = "hp", displayName = "hp", grade = CardGrade.Common,
                deltas = new[] { new StatDelta { axis = StatType.Hp, value = 0.5f } },
            }, 0);

            var vm = Factory(session).Create(Encounter(RunFixtures.Enemy("E0")), 1, false);

            var ally = vm.Combatants.First(u => u.IsAlly);
            Assert.AreEqual(1500, ally.MaxHp, "기본 1000 × (1 + 0.5)");
        }

        [Test]
        public void 챕터_계수와_변이_정예_디버프가_적_스탯에_접혀_들어간다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };
            var session = Session(party, enemyMultiplier: 2f);
            session.AttachCard(new BuffCard
            {
                id = "curse", displayName = "curse", grade = CardGrade.Common, targetsEnemies = true,
                deltas = new[] { new StatDelta { axis = StatType.Hp, value = -0.1f } },
            }, 0);

            var enemy = RunFixtures.Enemy("E0"); // HP 500
            var mutation = RunFixtures.Mutation("mut_hp", StatType.Hp, 1.5f);
            mutation.namePrefix = "강화된 ";
            var spec = new EncounterSpec(new[] { new EnemyInstanceSpec(enemy, mutation, isElite: true) });

            var vm = Factory(session).Create(spec, 1, false);

            var unit = vm.Combatants.First(u => !u.IsAlly);
            // 500 × 챕터2.0 × 정예1.15 × 변이1.5 × (1 + 디버프-0.1) = 1552.5 → 1553
            Assert.AreEqual(1553, unit.MaxHp);
            Assert.AreEqual("강화된 E0", unit.Name, "변이 이름 접두가 붙는다");
        }

        [Test]
        public void 런_버프_표시가_스탯이나_도메인_효과로_새지_않는다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };
            var session = Session(party);
            session.AttachCard(new BuffCard
            {
                id = "hp", displayName = "hp", grade = CardGrade.Common,
                deltas = new[] { new StatDelta { axis = StatType.Hp, value = 0.5f } },
            }, 0);
            session.AttachCard(new BuffCard
            {
                id = "curse", displayName = "curse", grade = CardGrade.Common, targetsEnemies = true,
                deltas = new[] { new StatDelta { axis = StatType.Atk, value = -0.1f } },
            }, 0);

            var vm = Factory(session).Create(Encounter(RunFixtures.Enemy("E0")), 1, false);

            var ally = vm.Combatants.First(u => u.IsAlly);
            Assert.AreEqual(1500, ally.MaxHp, "표시용 변환이 최종 스탯을 한 번 더 올리지 않는다");

            // 도메인 효과로 들어갔다면 남은 턴이 붙은 항목이 함께 서고 스탯도 한 번 더 올랐을 것이다.
            var allyIcons = ally.AllEffects.CurrentValue;
            Assert.AreEqual(1, allyIcons.Count);
            Assert.AreEqual(EffectType.Buff, allyIcons[0].Type);
            Assert.AreEqual(-1, allyIcons[0].RemainingTurns, "상시라 턴 라벨이 없다");

            var enemy = vm.Combatants.First(u => !u.IsAlly);
            var enemyIcons = enemy.AllEffects.CurrentValue;
            Assert.AreEqual(1, enemyIcons.Count);
            Assert.AreEqual(EffectType.Debuff, enemyIcons[0].Type, "저주는 적 쪽에 디버프로 선다");

            // 카드 항목은 아이콘 행에만 실린다. 상세 패널이 여기서 카드를 다시 적으면 카드 구획과 겹친다.
            CollectionAssert.IsEmpty(ally.SkillEffects.CurrentValue);
            CollectionAssert.IsEmpty(enemy.SkillEffects.CurrentValue);
        }

        [Test]
        public void 변이가_적_유닛에_실려_오고_틴트가_그_색으로_파생된다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };
            var mutation = RunFixtures.Mutation("mut_hp", StatType.Hp, 1.5f);
            mutation.tintColor = new Color(0.78f, 0.24f, 0.20f);
            var spec = new EncounterSpec(new[]
            {
                new EnemyInstanceSpec(RunFixtures.Enemy("E0"), mutation, isElite: false),
                new EnemyInstanceSpec(RunFixtures.Enemy("E1"), null, isElite: false),
            });

            var vm = Factory(Session(party)).Create(spec, 1, false);

            var enemies = vm.Combatants.Where(u => !u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            Assert.AreSame(mutation, enemies[0].Mutation, "상세 패널이 배수를 읽을 수 있게 변이가 그대로 실린다");
            Assert.AreEqual(mutation.tintColor, enemies[0].Tint);
            Assert.IsNull(enemies[1].Mutation, "변이가 없는 적은 null이다");
            Assert.AreEqual(Color.white, enemies[1].Tint, "변이가 없으면 평상시 흰색이다");

            var ally = vm.Combatants.First(u => u.IsAlly);
            Assert.IsNull(ally.Mutation, "아군에는 변이가 붙지 않는다");
            Assert.AreEqual(Color.white, ally.Tint);
        }

        [Test]
        public void 방마다_새_인스턴스가_선다()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };
            var session = Session(party);
            var factory = Factory(session);

            var first = factory.Create(Encounter(RunFixtures.Enemy("E0")), 1, false);
            var second = factory.Create(Encounter(RunFixtures.Enemy("E0")), 2, false);

            Assert.AreNotSame(first, second);
            Assert.AreNotSame(
                first.Combatants.First(u => u.IsAlly),
                second.Combatants.First(u => u.IsAlly), "전투원 뷰모델도 방마다 새로 선다");
        }

        [Test]
        public void 빈_파티_세션이나_빈_인카운터는_예외()
        {
            var party = new List<OwnedCharacter> { RunFixtures.Owned("A0") };

            Assert.Throws<InvalidOperationException>(
                () => Factory(Session(new List<OwnedCharacter> { null, null })).Create(
                    Encounter(RunFixtures.Enemy("E0")), 1, false), "전부 null 파티");
            Assert.Throws<InvalidOperationException>(
                () => Factory(Session(party)).Create(new EncounterSpec(null), 1, false), "enemies null");
            Assert.Throws<InvalidOperationException>(
                () => Factory(Session(party)).Create(Encounter(), 1, false), "enemies 빈 배열");
            Assert.Throws<InvalidOperationException>(
                () => Factory(Session(party)).Create(Encounter(RunFixtures.Enemy("E0"), null), 1, false),
                "슬롯 null(참조 누락)");
        }
    }
}