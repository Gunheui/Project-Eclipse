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
                new SkillEffect { type = EffectType.Damage, target = TargetSelector.LowestHpEnemy, value = power }
            };
            return s;
        }

        private static BattleUnit Ally(string name, int slot, Stats stats)
        {
            var so = ScriptableObject.CreateInstance<CharacterSO>();
            so.displayName = name;
            so.baseStats = stats;
            so.growthCurve = ScriptableObject.CreateInstance<GrowthCurve>();
            so.basicSkill = Skill(name + "_b", 0, 1f);
            return BattleUnit.FromCharacter(new OwnedCharacter(so, 1), slot);
        }

        private static BattleUnit Enemy(string name, int slot, Stats stats)
        {
            var so = ScriptableObject.CreateInstance<EnemySO>();
            so.displayName = name;
            so.baseStats = stats;
            so.basicSkill = Skill(name + "_b", 0, 1f);
            return BattleUnit.FromEnemy(so, slot);
        }

        private static SkillExecutor Executor(int seed)
            => new SkillExecutor(new CombatPipeline(new DamagePipeline(1f, 0.95f, 1.05f, new SeededRandom(seed))), new TargetResolver());

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() => UniTask.CompletedTask;
        }

        private static BattleViewModel Vm(BattleUnit ally, BattleUnit enemy, bool startAuto)
            => new BattleViewModel(
                new List<BattleUnit> { ally }, new List<BattleUnit> { enemy },
                Executor(1), actionCap: 200, startAuto: startAuto, new FakeSceneFlow());

        // --- 수동 프로바이더: Submit 전엔 대기, Submit하면 그 행동으로 완료 ---

        [UnityTest]
        public IEnumerator 수동_프로바이더는_Submit_전까지_대기하고_Submit하면_그_행동을_반환() => UniTask.ToCoroutine(async () =>
        {
            var actor = Ally("A", 0, S(1000, 100, 0, 100));
            var enemy = Enemy("E", 0, S(1000, 10, 0, 90));
            var provider = new ManualActionProvider(new RuleBasedActionProvider(0.4f, useHealRule: true)) { AutoMode = false };

            var task = provider.DecideAsync(actor, new List<ICombatant> { actor }, new List<ICombatant> { enemy }, CancellationToken.None);
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
            var provider = new ManualActionProvider(new RuleBasedActionProvider(0.4f, useHealRule: true)) { AutoMode = true };

            var action = await provider.DecideAsync(actor, new List<ICombatant> { actor }, new List<ICombatant> { enemy }, CancellationToken.None);

            Assert.AreSame(actor.Skills[0], action.Skill, "규칙이 기본공격을 고른다");
        });

        // --- VM 수동 구동: 제출로 턴이 진행되고 적 HP가 깎인다 ---

        [UnityTest]
        public IEnumerator 수동_제출하면_그_턴이_진행되고_적_HP가_깎인다() => UniTask.ToCoroutine(async () =>
        {
            var ally = Ally("아군", 0, S(5000, 300, 0, 200));   // 빠르고 튼튼 → 먼저·연속 행동
            var enemy = Enemy("적", 0, S(3000, 10, 0, 50));
            var vm = Vm(ally, enemy, startAuto: false);

            vm.StartAsync(CancellationToken.None).Forget(); // 첫 아군 턴에서 입력 대기까지 동기 진행

            Assert.IsNotNull(vm.ActingUnit.CurrentValue, "수동 아군 턴이면 ActingUnit이 세워진다");
            Assert.AreSame(vm.Units.First(u => u.IsAlly), vm.ActingUnit.CurrentValue, "행동자는 아군 명판");

            int hpBefore = enemy.CurrentHp;
            int actionsBefore = vm.ActionCount.CurrentValue;
            var acting = vm.ActingUnit.CurrentValue;
            var enemyPlate = vm.Units.First(u => !u.IsAlly);

            vm.Submit(acting.Skills[0], enemyPlate); // 기본공격으로 적 타격

            Assert.Greater(vm.ActionCount.CurrentValue, actionsBefore, "제출로 최소 한 턴이 진행됐다");
            Assert.Less(enemy.CurrentHp, hpBefore, "적 HP가 깎였다");
            Assert.AreEqual(enemy.CurrentHp, enemyPlate.CurrentHp.CurrentValue, "명판 HP가 도메인과 일치(턴 신호 파생)");

            vm.Dispose();
        });

        // --- VM 오토 구동: 강한 파티는 완주해 승리로 끝난다 ---

        [UnityTest]
        public IEnumerator 오토_구동은_강한_파티가_승리로_완주() => UniTask.ToCoroutine(async () =>
        {
            var ally = Ally("강자", 0, S(5000, 800, 0, 150));
            var enemy = Enemy("약졸", 0, S(200, 10, 0, 100));
            var vm = Vm(ally, enemy, startAuto: true);

            await vm.StartAsync(CancellationToken.None); // 오토라 대기 없이 완주

            Assert.AreEqual(BattleResult.Victory, vm.Result.CurrentValue);
            Assert.Greater(vm.ActionCount.CurrentValue, 0);

            vm.Dispose();
        });
    }
}