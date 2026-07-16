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
        }

        private static Combatant E(int slot, int hp, bool taunting = false)
            => new Combatant { Team = Team.Enemy, SlotIndex = slot, CurrentHp = hp, MaxHp = 100, IsTaunting = taunting };

        private static readonly IReadOnlyList<ICombatant> NoAllies = new List<ICombatant>();

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

            var valid = resolver.ValidManualTargets(enemies);

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

            var valid = resolver.ValidManualTargets(enemies);

            Assert.AreEqual(2, valid.Count);
            Assert.IsTrue(valid.Contains(a) && valid.Contains(b));
            Assert.IsFalse(valid.Contains(dead), "죽은 적은 지정 후보가 아니다");
        }

        // 조준 UI가 칠하는 후보(ValidManualTargets)를 찍으면 반드시 그 대상이 맞아야 한다 —
        // 화면과 판정이 어긋나지 않음을 계약으로 못박는다.
        [Test]
        public void 유효_수동타겟은_모두_지정이_존중된다()
        {
            var resolver = new TargetResolver();
            var t1 = E(0, 30, taunting: true);
            var t2 = E(1, 90, taunting: true);
            var normal = E(2, 10);
            var enemies = new List<ICombatant> { t1, t2, normal };

            foreach (var candidate in resolver.ValidManualTargets(enemies))
            {
                var result = resolver.Resolve(TargetSelector.SingleEnemy, actor: t1, NoAllies, enemies, candidate);
                Assert.AreSame(candidate, result[0], "후보로 제시된 대상은 찍으면 그대로 맞아야 한다");
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
