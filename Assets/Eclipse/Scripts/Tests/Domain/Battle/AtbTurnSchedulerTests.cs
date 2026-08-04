using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class AtbTurnSchedulerTests
    {
        // SPD 등 필요한 값만 지정하는 테스트용 전투 유닛(정의 SO 없이 스케줄러만 검증).
        private class FakeCombatant : ICombatant
        {
            public string DisplayName { get; }
            public Team Team { get; }
            public int SlotIndex { get; }
            public Stats EffectiveStats { get; }
            public int MaxHp => 1;
            public int CurrentHp { get; set; } = 1;
            public bool IsAlive => CurrentHp > 0;
            public IReadOnlyList<SkillRuntime> Skills => System.Array.Empty<SkillRuntime>();
            public bool IsTaunting => false;
            public int ShieldAbsorb => 0;

            public FakeCombatant(string name, Team team, int slot, int spd)
            {
                DisplayName = name;
                Team = team;
                SlotIndex = slot;
                EffectiveStats = new Stats { spd = spd };
            }
        }

        // 로스터 5인의 실제 SPD 값으로 구성.
        private static List<ICombatant> Roster() => new List<ICombatant>
        {
            new FakeCombatant("아린", Team.Ally, 0, 135),
            new FakeCombatant("리아", Team.Ally, 1, 125),
            new FakeCombatant("셀린", Team.Ally, 2, 105),
            new FakeCombatant("엘리아나", Team.Ally, 3, 100),
            new FakeCombatant("카엘", Team.Ally, 4, 80),
        };

        // 스케줄러를 count번 돌려 행동한 유닛 이름 시퀀스를 뽑는다.
        private static List<string> Sequence(ITurnScheduler scheduler, int count)
        {
            var seq = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var actor = scheduler.GetNextActor();
                seq.Add(actor.DisplayName);
                scheduler.OnActionResolved(actor);
            }
            return seq;
        }

        [Test]
        public void 첫_다섯_행동은_SPD_내림차순()
        {
            var seq = Sequence(new AtbTurnScheduler(Roster()), 5);
            Assert.AreEqual(new[] { "아린", "리아", "셀린", "엘리아나", "카엘" }, seq.ToArray());
        }

        [Test]
        public void SPD_높은_유닛이_더_자주_행동()
        {
            var seq = Sequence(new AtbTurnScheduler(Roster()), 100);
            int arin = seq.Count(n => n == "아린"); // SPD 135
            int kael = seq.Count(n => n == "카엘"); // SPD 80
            Assert.Greater(arin, kael);
        }

        [Test]
        public void SPD_동률은_아군_우선_그다음_슬롯()
        {
            var units = new List<ICombatant>
            {
                new FakeCombatant("적", Team.Enemy, 0, 100),
                new FakeCombatant("아군A", Team.Ally, 0, 100),
                new FakeCombatant("아군B", Team.Ally, 1, 100),
            };
            var seq = Sequence(new AtbTurnScheduler(units), 3);
            Assert.AreEqual(new[] { "아군A", "아군B", "적" }, seq.ToArray());
        }

        [Test]
        public void 먼저_도달한_유닛이_행동_SPD_높다고_우선하지_않음()
        {
            // A: 잔여 89,970 / SPD 100 → 도달 시각 899.7 (먼저 도달)
            // B: 잔여 269,940 / SPD 300 → 도달 시각 899.8 (나중, 단 SPD는 더 높음)
            var a = new FakeCombatant("A", Team.Ally, 0, 100);
            var b = new FakeCombatant("B", Team.Ally, 1, 300);
            var seeds = new Dictionary<ICombatant, long>
            {
                [a] = 9_910_030, // Threshold(10,000,000)까지 89,970 남음
                [b] = 9_730_060, // 269,940 남음
            };
            var scheduler = new AtbTurnScheduler(new List<ICombatant> { a, b }, seeds);

            // 교차곱: 89,970×300=26,991,000 < 269,940×100=26,994,000 → A가 먼저 도달.
            // 옛 정수-스텝 로직이라면 둘 다 900스텝째 동시 임계 도달, 초과분 A=30 < B=60이라
            // SPD 높은 B를 뽑았다(버그). 새 로직은 도달 시각으로 정확히 A를 고른다.
            Assert.AreEqual("A", scheduler.GetNextActor().DisplayName);
        }

        [Test]
        public void 이월된_유닛은_다음_차례에_전진없이_바로_행동()
        {
            // 두 유닛 모두 이미 임계값 이상(이월 상태)에서 시작. B는 초과분 50,000을 안고 있다.
            var a = new FakeCombatant("A", Team.Ally, 0, 100);
            var b = new FakeCombatant("B", Team.Ally, 1, 100);
            var seeds = new Dictionary<ICombatant, long>
            {
                [a] = 10_000_000, // 임계값에 정확히 도달(잔여 0)
                [b] = 10_050_000, // 임계값 초과 — 이전 행동의 이월분 50,000이 남은 상태
            };
            var scheduler = new AtbTurnScheduler(new List<ICombatant> { a, b }, seeds);

            // 1) 둘 다 잔여 0(즉시 도달) → 동률 타이브레이크(같은 SPD·같은 편)로 슬롯 앞선 A.
            var first = scheduler.GetNextActor();
            Assert.AreEqual("A", first.DisplayName);
            scheduler.OnActionResolved(first); // A 게이지 -= Threshold → 0으로 정산.

            // 2) A는 이제 잔여 10,000,000, B는 이월분 덕에 여전히 잔여 0.
            //    전진(remActor) 없이 이미 도달해 있던 B가 바로 뽑혀야 한다.
            Assert.AreEqual("B", scheduler.GetNextActor().DisplayName);
        }

        // 아군 선공 검증용 편성. 적 SPD가 아군의 3배라 규칙이 없으면 적이 첫 행동자다.
        private static List<ICombatant> AllyVsFasterEnemy() => new List<ICombatant>
        {
            new FakeCombatant("빠른적", Team.Enemy, 0, 300),
            new FakeCombatant("아군", Team.Ally, 0, 100),
        };

        [Test]
        public void 개전_첫_행동자는_적이_더_빨라도_아군이다()
        {
            var seq = Sequence(new AtbTurnScheduler(AllyVsFasterEnemy()), 2);

            Assert.AreEqual("아군", seq[0], "개전 첫 행동은 아군 몫이다");
            Assert.AreEqual("빠른적", seq[1], "개전 이후에는 SPD가 높은 적이 먼저 게이지를 채운다");
        }

        [Test]
        public void 아군_선공이_빠른_적의_연속_행동을_만들지_않는다()
        {
            var units = new List<ICombatant>
            {
                new FakeCombatant("적", Team.Enemy, 0, 140),
                new FakeCombatant("아군", Team.Ally, 0, 100),
            };

            var seq = Sequence(new AtbTurnScheduler(units), 3);

            Assert.AreEqual(new[] { "아군", "적", "아군" }, seq,
                "개전 전진에서 넘어선 초과분을 버리므로 적이 이월분으로 이어 행동하지 않는다");
        }

        [Test]
        public void 개전_제한은_첫_행동_한_번뿐이다()
        {
            var seq = Sequence(new AtbTurnScheduler(AllyVsFasterEnemy()), 9);

            Assert.Greater(seq.Count(n => n == "빠른적"), seq.Count(n => n == "아군"),
                "개전 이후에는 기존 ATB로 돌아가 SPD 3배인 적이 더 자주 행동한다");
        }

        [Test]
        public void 개전_SPD_동률에서도_아군이_먼저다()
        {
            var units = new List<ICombatant>
            {
                new FakeCombatant("적", Team.Enemy, 0, 100),
                new FakeCombatant("아군", Team.Ally, 0, 100),
            };

            Assert.AreEqual("아군", new AtbTurnScheduler(units).GetNextActor().DisplayName);
        }

        [Test]
        public void PreviewOrder는_개전_규칙까지_실제_진행과_일치()
        {
            var units = AllyVsFasterEnemy();
            var expected = Sequence(new AtbTurnScheduler(units), 6);
            var preview = new AtbTurnScheduler(units).PreviewOrder(6).Select(a => a.DisplayName).ToList();

            Assert.AreEqual(expected, preview);
        }

        [Test]
        public void PreviewOrder는_실제_진행_순서와_일치()
        {
            var roster = Roster();
            var expected = Sequence(new AtbTurnScheduler(roster), 12);
            var preview = new AtbTurnScheduler(roster).PreviewOrder(12).Select(a => a.DisplayName).ToList();
            Assert.AreEqual(expected, preview);
        }

        [Test]
        public void PreviewOrder는_실제_게이지를_바꾸지_않는다()
        {
            var scheduler = new AtbTurnScheduler(Roster());
            var previewFirst = scheduler.PreviewOrder(3)[0].DisplayName;

            // 예보를 여러 번 호출해도 실제 진행에는 영향이 없어야 한다(사본 위에서만 계산).
            scheduler.PreviewOrder(7);
            Assert.AreEqual(previewFirst, scheduler.GetNextActor().DisplayName,
                "예보가 실제 게이지를 건드렸다면 다음 행동자가 달라진다");
        }

        [Test]
        public void PreviewOrder_0이하는_빈_목록()
        {
            Assert.IsEmpty(new AtbTurnScheduler(Roster()).PreviewOrder(0));
        }
    }
}