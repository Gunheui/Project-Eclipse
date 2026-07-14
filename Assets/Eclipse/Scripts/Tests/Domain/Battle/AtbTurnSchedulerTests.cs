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

            public FakeCombatant(string name, Team team, int slot, int spd)
            {
                DisplayName = name;
                Team = team;
                SlotIndex = slot;
                EffectiveStats = new Stats { spd = spd };
            }
        }

        // 로스터 5인 실수치(05 §1)의 SPD로 구성.
        private static List<ICombatant> Roster() => new List<ICombatant>
        {
            new FakeCombatant("노엘", Team.Ally, 0, 135),
            new FakeCombatant("카이", Team.Ally, 1, 125),
            new FakeCombatant("미라", Team.Ally, 2, 105),
            new FakeCombatant("세라", Team.Ally, 3, 100),
            new FakeCombatant("리엔", Team.Ally, 4, 80),
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
            Assert.AreEqual(new[] { "노엘", "카이", "미라", "세라", "리엔" }, seq.ToArray());
        }

        [Test]
        public void SPD_높은_유닛이_더_자주_행동()
        {
            var seq = Sequence(new AtbTurnScheduler(Roster()), 100);
            int noel = seq.Count(n => n == "노엘"); // SPD 135
            int rien = seq.Count(n => n == "리엔"); // SPD 80
            Assert.Greater(noel, rien);
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
        public void 같은_편성은_같은_시퀀스_재현()
        {
            var seq1 = Sequence(new AtbTurnScheduler(Roster()), 30);
            var seq2 = Sequence(new AtbTurnScheduler(Roster()), 30);
            Assert.AreEqual(seq1, seq2);
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