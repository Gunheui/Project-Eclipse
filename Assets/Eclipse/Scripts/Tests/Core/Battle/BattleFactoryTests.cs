using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Core;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        // 즉시 끝나는 오토 승리용 — 마킹·보상 검증 판을 짧게 만든다.
        private static EnemySO WeakEnemy(string name)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = name;
            so.baseStats = S(50, 10, 0, 50);
            so.basicSkill = Skill(name + "_b");
            return so;
        }

        private static StageSO Stage(params EnemySO[] enemies)
        {
            var s = ScriptableObject.CreateInstance<StageSO>();
            s.id = "stage_t";
            s.enemies = enemies;
            return s;
        }

        private static ChapterSO ChapterOf(params StageSO[] stages)
        {
            var c = ScriptableObject.CreateInstance<ChapterSO>();
            c.id = "chapter_t";
            c.stages = stages;
            return c;
        }

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        private static BattleFactory BuildFactory(StageProgress progress = null)
        {
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(1)));
            var executor = new SkillExecutor(combat, targeting);
            var constants = ScriptableObject.CreateInstance<BattleConstantsSO>();
            return new BattleFactory(constants, targeting, combat, executor, new FakeSceneFlow(),
                progress ?? new StageProgress(), new StageRewardService(new CurrencyService(new CurrencyWallet())), saveService: null);
        }

        // 적 편성을 1스테이지짜리 장으로 감싸 조립한다 — 조립 규칙 테스트의 공통 경로.
        private static BattleViewModel Create(
            IReadOnlyList<OwnedCharacter> party, EnemySO[] enemies, int battleSeed, bool startAuto)
        {
            var stage = Stage(enemies);
            return BuildFactory().Create(party, ChapterOf(stage), stage, battleSeed, startAuto);
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

            var vm = Create(party, enemies, battleSeed: 12345, startAuto: false);

            var allies = vm.Combatants.Where(u => u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            CollectionAssert.AreEqual(new[] { "A2", "A0", "A1" }, allies.Select(u => u.Name).ToList(),
                "아군은 선택 파티 순서를 그대로 따른다(로스터 순서 아님)");
        }

        [Test]
        public void 아군_slotIndex는_편성_인덱스를_그대로_쓴다()
        {
            var party = new List<OwnedCharacter> { Owned("A0"), Owned("A1"), Owned("A2") };
            var vm = Create(party, new[] { Enemy("E0") }, 1, false);

            var slots = vm.Combatants.Where(u => u.IsAlly).Select(u => u.SlotIndex).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, slots);
        }

        [Test]
        public void 중간_빈칸은_자리를_비운_채_뒤_슬롯_번호가_유지된다()
        {
            // 편성 1·3번 칸(인덱스 0·2)에만 배치 → 전투에서도 0·2번 자리에 서야 한다(1로 당겨지면 안 됨)
            var party = new List<OwnedCharacter> { Owned("A0"), null, Owned("A2"), null };

            var vm = Create(party, new[] { Enemy("E0") }, 1, false);

            var allies = vm.Combatants.Where(u => u.IsAlly).OrderBy(u => u.SlotIndex).ToList();
            CollectionAssert.AreEqual(new[] { 0, 2 }, allies.Select(u => u.SlotIndex).ToList(),
                "빈칸은 건너뛰되 뒤 유닛의 자리 번호는 당겨지지 않는다");
            CollectionAssert.AreEqual(new[] { "A0", "A2" }, allies.Select(u => u.Name).ToList());
        }

        [Test]
        public void 아군은_최대_4자리까지만_참전한다()
        {
            var party = Enumerable.Range(0, 6).Select(i => Owned("A" + i)).ToList();

            var vm = Create(party, new[] { Enemy("E0") }, 1, false);

            var slots = vm.Combatants.Where(u => u.IsAlly).Select(u => u.SlotIndex).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, slots, "진영 자리는 4개뿐이라 그 뒤는 잘린다");
        }

        [Test]
        public void 적은_스테이지_편성_전부_참전하며_아군_수를_따르지_않는다()
        {
            var party = new List<OwnedCharacter> { Owned("A0") };          // 아군 1명
            var enemies = new[] { Enemy("E0"), Enemy("E1"), Enemy("E2") }; // 적 3

            var vm = Create(party, enemies, 1, false);

            Assert.AreEqual(1, vm.Combatants.Count(u => u.IsAlly));
            Assert.AreEqual(3, vm.Combatants.Count(u => !u.IsAlly),
                "적 수는 아군(1)이 아니라 스테이지 편성(3)을 따른다");
        }

        [Test]
        public void 적은_4명_상한을_넘지_않는다()
        {
            var party = new List<OwnedCharacter> { Owned("A0") };
            var enemies = new[] { Enemy("E0"), Enemy("E1"), Enemy("E2"), Enemy("E3"), Enemy("E4") };

            var vm = Create(party, enemies, 1, false);

            Assert.AreEqual(4, vm.Combatants.Count(u => !u.IsAlly), "적은 4v4 상한까지만 참전한다");
        }

        [Test]
        public void 아군이_적보다_먼저_배치된다()
        {
            var party = new List<OwnedCharacter> { Owned("A0"), Owned("A1") };
            var enemies = new[] { Enemy("E0"), Enemy("E1") };

            var vm = Create(party, enemies, 1, false);

            Assert.IsTrue(vm.Combatants.Take(2).All(u => u.IsAlly), "앞 2개가 전부 아군");
        }

        [Test]
        public void 빈_또는_null_파티와_null_장_스테이지는_예외()
        {
            var stage = Stage(Enemy("E0"));
            var chapter = ChapterOf(stage);
            var party = new List<OwnedCharacter> { Owned("A0") };

            Assert.Throws<ArgumentException>(
                () => BuildFactory().Create(new List<OwnedCharacter>(), chapter, stage, 1, false), "빈 파티");
            Assert.Throws<ArgumentException>(
                () => BuildFactory().Create(new List<OwnedCharacter> { null, null }, chapter, stage, 1, false), "전부 null 파티");
            Assert.Throws<ArgumentNullException>(
                () => BuildFactory().Create(null, chapter, stage, 1, false), "null 파티");
            Assert.Throws<ArgumentNullException>(
                () => BuildFactory().Create(party, null, stage, 1, false), "null 장");
            Assert.Throws<ArgumentNullException>(
                () => BuildFactory().Create(party, chapter, null, 1, false), "null 스테이지");
        }

        [Test]
        public void 적_편성이_비었거나_슬롯이_null이면_예외()
        {
            var party = new List<OwnedCharacter> { Owned("A0") };

            var noEnemies = Stage();
            noEnemies.enemies = null;
            Assert.Throws<InvalidOperationException>(
                () => BuildFactory().Create(party, ChapterOf(noEnemies), noEnemies, 1, false), "enemies null");

            var empty = Stage();
            Assert.Throws<InvalidOperationException>(
                () => BuildFactory().Create(party, ChapterOf(empty), empty, 1, false), "enemies 빈 배열");

            var holed = Stage(Enemy("E0"), null);
            Assert.Throws<InvalidOperationException>(
                () => BuildFactory().Create(party, ChapterOf(holed), holed, 1, false), "슬롯 null(참조 누락)");
        }

        [Test]
        public void 장에_속하지_않은_스테이지는_조립_예외()
        {
            var party = new List<OwnedCharacter> { Owned("A0") };
            var stage = Stage(Enemy("E0"));
            var otherChapter = ChapterOf(Stage(Enemy("E1"))); // stage 미포함

            Assert.Throws<InvalidOperationException>(
                () => BuildFactory().Create(party, otherChapter, stage, 1, false),
                "장·스테이지 불일치는 마킹이 조용히 실패하기 전에 조립에서 드러난다");
        }

        // --- 승리 → 클리어 마킹·보상: 조립 시점에 박제한 스테이지가 그대로 마킹되고, 최초 클리어 보상은 1회만 ---

        [UnityTest]
        public IEnumerator 승리하면_그_스테이지가_마킹되고_최초_클리어_보상은_1회만() => UniTask.ToCoroutine(async () =>
        {
            var stage = Stage(WeakEnemy("E0"));
            stage.clearRewards = new[] { new RewardEntry { type = CurrencyType.Gold, amount = 100 } };
            stage.firstClearRewards = new[] { new RewardEntry { type = CurrencyType.Gold, amount = 50 } };
            var chapter = ChapterOf(stage, Stage(WeakEnemy("E1"))); // 2스테이지 장의 index 0을 전투

            using var progress = new StageProgress();
            var factory = BuildFactory(progress);

            var first = factory.Create(new List<OwnedCharacter> { Owned("A0") }, chapter, stage, 1, startAuto: true);
            await first.RunBattleAsync(null, CancellationToken.None);

            Assert.AreEqual(BattleResult.Victory, first.Result.CurrentValue);
            Assert.AreEqual(1, progress.ClearedCountOf(chapter).CurrentValue, "승리가 그 장의 index 0을 마킹했다");
            Assert.AreEqual(1, first.GrantedRewards.Count);
            Assert.AreEqual(150, first.GrantedRewards[0].amount, "최초 클리어는 상시+초회 보상 합산");
            first.Dispose();

            var second = factory.Create(new List<OwnedCharacter> { Owned("A0") }, chapter, stage, 2, startAuto: true);
            await second.RunBattleAsync(null, CancellationToken.None);

            Assert.AreEqual(1, progress.ClearedCountOf(chapter).CurrentValue, "재클리어는 진행도를 올리지 않는다");
            Assert.AreEqual(100, second.GrantedRewards[0].amount, "재클리어는 상시 보상만");
            second.Dispose();
        });
    }
}
