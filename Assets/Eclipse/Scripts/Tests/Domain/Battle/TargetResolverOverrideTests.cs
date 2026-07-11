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
            var low = E(0, 10);   // 원래 LowestHpEnemy가 고를 대상
            var chosen = E(1, 50);
            var enemies = new List<ICombatant> { low, chosen };

            var result = resolver.Resolve(TargetSelector.LowestHpEnemy, actor: chosen, NoAllies, enemies, chosen);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(chosen, result[0]); // 지정이 최저HP 규칙을 덮는다
        }

        [Test]
        public void 도발중인_적이_있으면_지정을_무시하고_도발자를_친다()
        {
            var resolver = new TargetResolver();
            var taunter = E(0, 80, taunting: true);
            var chosen = E(1, 50);
            var enemies = new List<ICombatant> { taunter, chosen };

            var result = resolver.Resolve(TargetSelector.LowestHpEnemy, actor: chosen, NoAllies, enemies, chosen);

            Assert.AreSame(taunter, result[0]); // 도발 > 수동 지정
        }

        [Test]
        public void 지정대상이_죽었으면_셀렉터로_폴백한다()
        {
            var resolver = new TargetResolver();
            var alive = E(0, 10);
            var dead = E(1, 0);
            var enemies = new List<ICombatant> { alive, dead };

            var result = resolver.Resolve(TargetSelector.LowestHpEnemy, actor: alive, NoAllies, enemies, dead);

            Assert.AreSame(alive, result[0]); // 죽은 지정 대신 최저HP 생존
        }

        [Test]
        public void 지정이_null이면_셀렉터가_대상을_정한다()
        {
            var resolver = new TargetResolver();
            var low = E(0, 10);
            var high = E(1, 50);
            var enemies = new List<ICombatant> { low, high };

            var result = resolver.Resolve(TargetSelector.LowestHpEnemy, actor: low, NoAllies, enemies, null);

            Assert.AreSame(low, result[0]);
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
