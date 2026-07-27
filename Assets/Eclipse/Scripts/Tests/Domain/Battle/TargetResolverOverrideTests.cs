using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class TargetResolverOverrideTests
    {
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

        private static Combatant E(int slot, int hp, bool taunting = false)
            => new Combatant { Team = Team.Enemy, SlotIndex = slot, CurrentHp = hp, MaxHp = 100, IsTaunting = taunting };

        private static Combatant A(int slot, int hp)
            => new Combatant { Team = Team.Ally, SlotIndex = slot, CurrentHp = hp, MaxHp = 100 };

        private static readonly IReadOnlyList<ICombatant> NoAllies = new List<ICombatant>();
        private static readonly IReadOnlyList<ICombatant> NoEnemies = new List<ICombatant>();

        [Test]
        public void 지정대상이_단일적_셀렉터면_그_대상을_친다()
        {
            var resolver = new TargetResolver();
            var low = E(0, 10);
            var chosen = E(1, 50);
            var enemies = new List<ICombatant> { low, chosen };

            var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: chosen, NoAllies, enemies, chosen);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(chosen, result[0]); // 지정이 최저HP 규칙을 덮는다
        }

        [Test]
        public void 도발중인_적이_있으면_비도발자_지정을_무시하고_도발자를_친다()
        {
            var resolver = new TargetResolver();
            var taunter = E(0, 80, taunting: true);
            var chosen = E(1, 50);
            var enemies = new List<ICombatant> { taunter, chosen };

            var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: chosen, NoAllies, enemies, chosen);

            Assert.AreSame(taunter, result[0]); // 도발 > 비도발자 수동 지정
        }

        [Test]
        public void 도발자가_여럿이면_지정한_도발자를_친다()
        {
            var resolver = new TargetResolver();
            var lowTaunter = E(0, 30, taunting: true);    // 폴백이면 슬롯 앞이라 이쪽이 맞는다
            var chosenTaunter = E(1, 90, taunting: true);
            var enemies = new List<ICombatant> { lowTaunter, chosenTaunter };

            var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: lowTaunter, NoAllies, enemies, chosenTaunter);

            Assert.AreSame(chosenTaunter, result[0]); // 도발이 범위를 좁히되 그 안에서는 지정을 존중
        }

        [Test]
        public void 유효_수동타겟은_도발자가_있으면_도발자만()
        {
            var resolver = new TargetResolver();
            var taunter = E(0, 80, taunting: true);
            var normal = E(1, 50);
            var dead = E(2, 0);
            var enemies = new List<ICombatant> { taunter, normal, dead };

            var valid = resolver.ValidEnemyTargets(enemies);

            Assert.AreEqual(1, valid.Count);
            Assert.AreSame(taunter, valid[0]);
        }

        [Test]
        public void 유효_수동타겟은_도발자가_없으면_생존한_적_전부()
        {
            var resolver = new TargetResolver();
            var a = E(0, 80);
            var b = E(1, 50);
            var dead = E(2, 0);
            var enemies = new List<ICombatant> { a, b, dead };

            var valid = resolver.ValidEnemyTargets(enemies);

            Assert.AreEqual(2, valid.Count);
            Assert.IsTrue(valid.Contains(a) && valid.Contains(b));
            Assert.IsFalse(valid.Contains(dead), "죽은 적은 지정 후보가 아니다");
        }

        // 조준 UI가 강조 표시하는 후보(ValidManualTargets)를 지정하면 반드시 그 대상이 맞아야 한다 —
        // 화면과 판정이 달라지지 않음을 계약으로 못박는다.
        [Test]
        public void 유효_수동타겟은_모두_지정이_존중된다()
        {
            var resolver = new TargetResolver();
            var t1 = E(0, 30, taunting: true);
            var t2 = E(1, 90, taunting: true);
            var normal = E(2, 10);
            var enemies = new List<ICombatant> { t1, t2, normal };

            foreach (var candidate in resolver.ValidEnemyTargets(enemies))
            {
                var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: t1, NoAllies, enemies, candidate);
                Assert.AreSame(candidate, result[0], "후보로 제시된 대상은 지정하면 그대로 맞아야 한다");
            }
        }

        [Test]
        public void 지정대상이_죽었으면_셀렉터로_폴백한다()
        {
            var resolver = new TargetResolver();
            var alive = E(0, 10);
            var dead = E(1, 0);
            var enemies = new List<ICombatant> { alive, dead };

            var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: alive, NoAllies, enemies, dead);

            Assert.AreSame(alive, result[0]); // 죽은 지정 대신 생존 적(슬롯순)
        }

        [Test]
        public void 지정이_null이면_셀렉터_기본값은_슬롯순_대상이다()
        {
            var resolver = new TargetResolver();
            var front = E(0, 50); // 슬롯 앞 · HP 높음
            var lowHp = E(1, 10); // 슬롯 뒤 · HP 낮음
            var enemies = new List<ICombatant> { front, lowHp };

            var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: front, NoAllies, enemies, null);

            // SingleEnemy 폴백은 최저HP가 아니라 슬롯 낮은 쪽(dumb) — 정책이 Target을 줄 때만 그 위를 덮는다.
            Assert.AreSame(front, result[0]);
        }

        [Test]
        public void 지정대상이_단일아군_셀렉터면_그_아군을_친다()
        {
            var resolver = new TargetResolver();
            var low = A(0, 10);      // 최저HP — 기본값이면 이쪽
            var chosen = A(1, 80);
            var allies = new List<ICombatant> { low, chosen };

            var result = resolver.Resolve(TargetSelector.SingleAlly, actor: chosen, allies, NoEnemies, chosen);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(chosen, result[0]); // 지정이 최저HP 기본값을 덮는다(힐 대상 직접 선택)
        }

        [Test]
        public void 단일아군_지정대상이_죽었으면_최저HP_아군으로_폴백한다()
        {
            var resolver = new TargetResolver();
            var low = A(0, 10);
            var dead = A(1, 0);
            var allies = new List<ICombatant> { low, dead };

            var result = resolver.Resolve(TargetSelector.SingleAlly, actor: low, allies, NoEnemies, dead);

            Assert.AreSame(low, result[0]); // 죽은 지정 대신 최저HP 생존 아군
        }

        [Test]
        public void 단일아군_지정대상이_아군목록에_없으면_폴백한다()
        {
            var resolver = new TargetResolver();
            var ally = A(0, 10);
            var enemy = E(0, 50);
            var allies = new List<ICombatant> { ally };
            var enemies = new List<ICombatant> { enemy };

            var result = resolver.Resolve(TargetSelector.SingleAlly, actor: ally, allies, enemies, enemy);

            Assert.AreSame(ally, result[0]); // 적을 아군 스킬 대상으로 지정해도 무시하고 아군 기본값
        }

        [Test]
        public void 유효_아군타겟은_생존한_아군_전부()
        {
            var resolver = new TargetResolver();
            var a = A(0, 80);
            var b = A(1, 10);
            var dead = A(2, 0);
            var allies = new List<ICombatant> { a, b, dead };

            var valid = resolver.ValidAllyTargets(allies);

            Assert.AreEqual(2, valid.Count);
            Assert.IsTrue(valid.Contains(a) && valid.Contains(b));
            Assert.IsFalse(valid.Contains(dead), "죽은 아군은 힐/버프 지정 후보가 아니다");
        }

        // 조준 UI가 강조 표시하는 아군 후보를 지정하면 반드시 그 대상이 맞아야 한다(화면 = 판정 계약).
        [Test]
        public void 유효_아군타겟은_모두_지정이_존중된다()
        {
            var resolver = new TargetResolver();
            var a = A(0, 80);
            var b = A(1, 10);
            var allies = new List<ICombatant> { a, b };

            foreach (var candidate in resolver.ValidAllyTargets(allies))
            {
                var result = resolver.Resolve(TargetSelector.SingleAlly, actor: a, allies, NoEnemies, candidate);
                Assert.AreSame(candidate, result[0], "아군 후보로 제시된 대상은 지정하면 그대로 맞아야 한다");
            }
        }

        [Test]
        public void 광역_셀렉터는_지정을_무시한다()
        {
            var resolver = new TargetResolver();
            var e0 = E(0, 10);
            var e1 = E(1, 50);
            var enemies = new List<ICombatant> { e0, e1 };

            var result = resolver.Resolve(TargetSelector.AllEnemies, actor: e0, NoAllies, enemies, e1);

            Assert.AreEqual(2, result.Count); // 지정 무관하게 전체 생존
            Assert.IsTrue(result.Contains(e0) && result.Contains(e1));
        }
    }
}
