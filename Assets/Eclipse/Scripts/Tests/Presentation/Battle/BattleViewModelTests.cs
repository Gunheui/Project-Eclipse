using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace Eclipse.Tests
{
    public class BattleViewModelTests
    {
        // --- 인메모리 데이터 빌더 ---

        private static Stats S(int hp, int atk, int def, int spd)
            => new Stats { hp = hp, atk = atk, def = def, spd = spd, critRate = 0f, critDamage = 1.5f };

        private static SkillSO Skill(string id, int cooldown, float power)
        {
            var s = ScriptableObject.CreateInstance<SkillSO>();
            s.id = id;
            s.displayName = id;
            s.cooldownTurns = cooldown;
            s.effects = new List<SkillEffect>
            {
                new SkillEffect { type = EffectType.Damage, target = TargetSelector.SingleEnemy, value = power }
            };
            return s;
        }

        private static Combatant Ally(string name, int slot, Stats stats)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = name;
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
            so.basicSkill = Skill(name + "_b", 0, 1f);
            return Combatant.FromCharacter(new OwnedCharacter(so, 1), slot);
        }

        private static Combatant Enemy(string name, int slot, Stats stats)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = name;
            so.baseStats = stats;
            so.basicSkill = Skill(name + "_b", 0, 1f);
            return Combatant.FromEnemy(so, slot);
        }

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        // 아트는 스케줄러·엔진과 무관하므로 스프라이트 없이 유닛만 실어 보낸다.
        private static BattleUnitEntry Entry(Combatant unit) => new BattleUnitEntry(unit, null, null);

        // 엔진·스케줄러·프로바이더를 프로덕션(BattleLifetimeScope)과 같은 배선으로 조립한 VM. 타겟난수는 데미지난수와 분리된 스트림.
        private static BattleViewModel BuildVm(Combatant[] allies, Combatant[] enemies, int seed, bool startAuto)
        {
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(seed)));
            var executor = new SkillExecutor(combat, targeting);
            var allyTargetRng = new SeededRandom(BattleSeed.For(seed, BattleSeed.Stream.AllyTargeting));
            var enemyTargetRng = new SeededRandom(BattleSeed.For(seed, BattleSeed.Stream.EnemyTargeting));

            var manualProvider = new ManualActionProvider(
                RuleBasedActionProvider.AllyAuto(targeting, combat, allyTargetRng)) { AutoMode = startAuto };
            var enemyAi = RuleBasedActionProvider.EnemyAi(targeting, combat, enemyTargetRng, 0.6f, 0.5f);

            var scheduler = new AtbTurnScheduler(allies.Concat(enemies));
            var engine = new BattleEngine(allies.ToList(), enemies.ToList(), scheduler,
                executor, manualProvider, enemyAi, actionCap: 200);

            // 장·스테이지는 조립 시점에 확정돼 불변으로 들어간다(프로덕션은 BattleFactory가 검증·계산).
            var stage = ScriptableObject.CreateInstance<StageSO>();
            stage.id = "stage_t";
            var chapter = ScriptableObject.CreateInstance<ChapterSO>();
            chapter.id = "chapter_t";
            chapter.stages = new[] { stage };

            return new BattleViewModel(
                allies.Select(Entry).ToArray(), enemies.Select(Entry).ToArray(),
                engine, scheduler, manualProvider, targeting, new FakeSceneFlow(),
                new StageProgress(), chapter, stage, stageIndex: 0,
                new StageRewardService(new CurrencyService(new CurrencyWallet())), saveService: null);
        }

        private static BattleViewModel Vm(Combatant ally, Combatant enemy, bool startAuto)
            => BuildVm(new[] { ally }, new[] { enemy }, seed: 1, startAuto);

        // AutoMode 위임용 규칙 프로바이더(아군 프로파일). 타겟 정책까지 실제로 배선한다.
        private static RuleBasedActionProvider AutoRule(int seed = 1)
        {
            var targeting = new TargetResolver();
            var combat = new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(seed)));
            return RuleBasedActionProvider.AllyAuto(targeting, combat,
                new SeededRandom(BattleSeed.For(seed, BattleSeed.Stream.AllyTargeting)));
        }

        // --- 수동 프로바이더: Submit 전엔 대기, Submit하면 그 행동으로 완료 ---

        [UnityTest]
        public IEnumerator 수동_프로바이더는_Submit_전까지_대기하고_Submit하면_그_행동을_반환() => UniTask.ToCoroutine(async () =>
        {
            var actor = Ally("A", 0, S(1000, 100, 0, 100));
            var enemy = Enemy("E", 0, S(1000, 10, 0, 90));
            var provider = new ManualActionProvider(AutoRule()) { AutoMode = false };

            var task = provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, new List<ICombatant> { enemy }, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, task.Status, "Submit 전에는 완료되지 않는다");
            Assert.AreSame(actor, provider.PendingActor, "대기 중인 행동자가 노출된다");

            provider.Submit(actor.Skills[0], enemy);
            var action = await task;

            Assert.AreSame(actor.Skills[0], action.Skill);
            Assert.AreSame(enemy, action.Target);
            Assert.IsNull(provider.PendingActor, "완료 후 대기 상태가 정리된다");
        });

        // --- 오토 프로바이더 위임: AutoMode면 즉시 완료 ---

        [UnityTest]
        public IEnumerator 오토모드면_규칙에_위임해_즉시_완료() => UniTask.ToCoroutine(async () =>
        {
            var actor = Ally("A", 0, S(1000, 100, 0, 100));
            var enemy = Enemy("E", 0, S(1000, 10, 0, 90));
            var provider = new ManualActionProvider(AutoRule()) { AutoMode = true };

            var action = await provider.ChooseActionAsync(actor, new List<ICombatant> { actor }, new List<ICombatant> { enemy }, CancellationToken.None);

            Assert.AreSame(actor.Skills[0], action.Skill, "규칙이 기본공격을 고른다");
        });

        // --- VM 수동 구동: 제출로 턴이 진행되고 적 HP가 깎인다 ---

        [UnityTest]
        public IEnumerator 수동_제출하면_그_턴이_진행되고_적_HP가_깎인다() => UniTask.ToCoroutine(async () =>
        {
            var ally = Ally("아군", 0, S(5000, 300, 0, 200));   // 빠르고 튼튼 → 먼저·연속 행동
            var enemy = Enemy("적", 0, S(3000, 10, 0, 50));
            var vm = Vm(ally, enemy, startAuto: false);

            vm.RunBattleAsync(null, CancellationToken.None).Forget(); // 첫 아군 턴에서 입력 대기까지 동기 진행

            Assert.IsNotNull(vm.ActingCombatant.CurrentValue, "수동 아군 턴이면 ActingCombatant이 세워진다");
            Assert.AreSame(vm.Combatants.First(u => u.IsAlly), vm.ActingCombatant.CurrentValue, "행동자는 아군 명판");

            int hpBefore = enemy.CurrentHp;
            int actionsBefore = vm.ActionCount.CurrentValue;
            var acting = vm.ActingCombatant.CurrentValue;
            var enemyPlate = vm.Combatants.First(u => !u.IsAlly);

            vm.Submit(acting.Skills[0], enemyPlate); // 기본공격으로 적 타격

            Assert.Greater(vm.ActionCount.CurrentValue, actionsBefore, "제출로 최소 한 턴이 진행됐다");
            Assert.Less(enemy.CurrentHp, hpBefore, "적 HP가 깎였다");
            Assert.AreEqual(enemy.CurrentHp, enemyPlate.CurrentHp.CurrentValue, "명판 HP가 도메인과 일치(턴 신호 파생)");

            vm.Dispose();
        });

        // --- VM 수동 지정: 찍은 대상이 자동 기본(슬롯순)을 덮어써 그 적을 맞힌다 ---

        [UnityTest]
        public IEnumerator 수동_지정_대상이_자동기본이_아닌_찍은_적을_맞힌다() => UniTask.ToCoroutine(async () =>
        {
            var ally = Ally("아군", 0, S(5000, 300, 0, 200)); // 가장 빠름 → 먼저 행동
            var low = Enemy("저HP", 0, S(1000, 10, 0, 50));   // 슬롯 앞 — 자동 기본 폴백이 고를 자리
            var high = Enemy("고HP", 1, S(3000, 10, 0, 40));  // 슬롯 뒤 — 수동으로 이쪽을 지정
            var vm = BuildVm(new[] { ally }, new[] { low, high }, seed: 1, startAuto: false);

            vm.RunBattleAsync(null, CancellationToken.None).Forget(); // 첫 아군 턴 입력 대기까지 진행

            var acting = vm.ActingCombatant.CurrentValue;
            Assert.IsNotNull(acting, "수동 아군 턴이면 ActingCombatant이 세워진다");
            var highPlate = vm.Combatants.First(u => !u.IsAlly && u.SlotIndex == 1);

            int lowBefore = low.CurrentHp;
            int highBefore = high.CurrentHp;

            vm.Submit(acting.Skills[0], highPlate); // 자동 기본(저HP)이 아니라 고HP를 지정

            Assert.Less(high.CurrentHp, highBefore, "지정한 고HP 적이 맞았다");
            Assert.AreEqual(lowBefore, low.CurrentHp, "자동 기본 대상(저HP)은 맞지 않았다");

            vm.Dispose();
        });

        // --- VM 오토 구동: 강한 파티는 완주해 승리로 끝난다 ---

        [UnityTest]
        public IEnumerator 오토_구동은_강한_파티가_승리로_완주() => UniTask.ToCoroutine(async () =>
        {
            var ally = Ally("강자", 0, S(5000, 800, 0, 150));
            var enemy = Enemy("약졸", 0, S(200, 10, 0, 100));
            var vm = Vm(ally, enemy, startAuto: true);

            await vm.RunBattleAsync(null, CancellationToken.None); // 오토라 대기 없이 완주

            Assert.AreEqual(BattleResult.Victory, vm.Result.CurrentValue);
            Assert.Greater(vm.ActionCount.CurrentValue, 0);

            vm.Dispose();
        });

        // --- 배속 불변 회귀: 연출 시간이 달라도 같은 시드면 전투 결과가 완전히 동일 ---
        // 배속이 흐르는 유일한 통로는 RunBattleAsync의 playTurnAnimation(연출 대기)이고, speed는 Domain에
        // 존재하지 않는다. 같은 전투를 이 연출 콜백만 바꿔 여러 번 돌려 결과 지문이 동일함을 못박는다.

        // 한 판의 결정적 지문: 최종 결과·행동 수 + 턴마다 찍은 전 유닛 HP 스냅샷.
        private sealed class BattleTrace
        {
            public BattleResult Result;
            public int ActionCount;
            public readonly List<int[]> HpPerTurn = new();
        }

        // 연출 seam(배속이 흐르는 유일한 통로)을 흉내내는 가짜 턴 애니메이션. cost만큼 인위적 부하를
        // 태우고 동기 완료한다. 도메인 상태엔 닿지 않으므로(핸들을 받지도 돌려주지도 않음) cost가
        // 얼마든 도메인 트레이스는 불변이어야 한다. 실기의 배속은 이 콜백이 소비하는 시간을 나눌 뿐이다.
        private static Func<CancellationToken, UniTask> FakeAnimation(int cost)
            => ct =>
            {
                for (int i = 0; i < cost; i++)
                    ct.ThrowIfCancellationRequested();
                return UniTask.CompletedTask;
            };

        // 오토 전투 한 판을 끝까지 돌리고 지문을 수집한다. playTurnAnimation이 배속에 해당하는 seam.
        private static async UniTask<BattleTrace> RunAutoBattleAsync(
            int seed, Func<CancellationToken, UniTask> playTurnAnimation)
        {
            var ally = Ally("아군", 0, S(2000, 300, 20, 150));
            var enemy = Enemy("적", 0, S(1500, 120, 10, 120));
            var vm = BuildVm(new[] { ally }, new[] { enemy }, seed, startAuto: true);

            var trace = new BattleTrace();
            // ActionCount는 턴마다 갱신(증가)되므로 턴 신호 대용. 그때의 전 유닛 HP를 함께 찍는다.
            using var sub = vm.ActionCount.Subscribe(_ =>
                trace.HpPerTurn.Add(vm.Combatants.Select(u => u.CurrentHp.CurrentValue).ToArray()));

            await vm.RunBattleAsync(playTurnAnimation, CancellationToken.None);

            trace.Result = vm.Result.CurrentValue;
            trace.ActionCount = vm.ActionCount.CurrentValue;
            vm.Dispose();
            return trace;
        }

        private static void AssertSameTrace(BattleTrace expected, BattleTrace actual, string speedLabel)
        {
            Assert.AreEqual(expected.Result, actual.Result, $"{speedLabel}: 결과가 같다");
            Assert.AreEqual(expected.ActionCount, actual.ActionCount, $"{speedLabel}: 행동 수가 같다");
            Assert.AreEqual(expected.HpPerTurn.Count, actual.HpPerTurn.Count, $"{speedLabel}: 턴 수가 같다");
            for (int t = 0; t < expected.HpPerTurn.Count; t++)
                CollectionAssert.AreEqual(expected.HpPerTurn[t], actual.HpPerTurn[t],
                    $"{speedLabel}: {t}번째 턴 HP 스냅샷이 같다");
        }

        [UnityTest]
        public IEnumerator 배속이_달라도_같은_시드면_전투_결과가_불변() => UniTask.ToCoroutine(async () =>
        {
            const int seed = 4242;

            var instant = await RunAutoBattleAsync(seed, playTurnAnimation: null); // 연출 없음(무한 배속)
            var oneX = await RunAutoBattleAsync(seed, FakeAnimation(cost: 4));      // 1x(연출 부하 큼)
            var twoX = await RunAutoBattleAsync(seed, FakeAnimation(cost: 2));      // 2x(연출 부하 작음)

            // 세 경우 모두 지문이 완전 일치 → 데미지·크리·행동 순서가 연출 seam에 불변임을 못박는다.
            AssertSameTrace(instant, oneX, "1x");
            AssertSameTrace(instant, twoX, "2x");
        });
    }
}