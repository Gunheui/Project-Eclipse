using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.Service;
using NUnit.Framework;

namespace Eclipse.Tests
{
    /// <summary>
    /// 고정 시드로 챕터 1 배치(방 7·문 지점 5·미드보스)를 처음부터 끝까지 구동하는 풀 사이클 검증.
    /// 화면 없이 제시물을 따라가며 전투는 전승으로 보고한다 — 루프 구조와 지급·정산의 회귀 그물이다.
    /// </summary>
    public class ChapterRunCycleTests
    {
        private sealed class FakeSceneFlow : ISceneFlow
        {
            public int ToMainCount;
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() { ToMainCount++; return UniTask.CompletedTask; }
        }

        [Test]
        public void 고정_시드_챕터1_풀_사이클_전승_완주()
        {
            const int seed = 20260727;
            var chapter = RunFixtures.DocChapter();
            var tuning = RunFixtures.Tuning();
            var wallet = new CurrencyWallet();
            var rewards = new RunRewardService(new CurrencyService(wallet));
            var progress = new ChapterProgress();
            var sceneFlow = new FakeSceneFlow();
            var party = RunFixtures.Party(4);
            var session = new ChapterRunSession(chapter, tuning, party, seed);
            var doorCatalog = RunFixtures.DoorCatalog();
            var cardCatalog = RunFixtures.CardCatalog(party.Select(o => o.Definition.id).ToArray());
            var doorRng = new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Door));
            var flow = new ChapterRunFlow(
                session,
                new EncounterGenerator(tuning,
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Mutation))),
                new DoorDraw(doorCatalog, doorRng),
                new CardPool(cardCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Card))),
                doorRng,
                doorCatalog,
                rewards,
                progress,
                saveService: null,
                sceneFlow);

            int battles = 0, doorPoints = 0, cardPicks = 0;
            var battleSeeds = new List<int>();
            var revealAtRoom = new Dictionary<int, IReadOnlyList<RewardEntry>>();

            flow.BeginRun().Forget();
            int guard = 0;
            while (flow.Current != RunStep.RunClear && guard++ < 200)
            {
                var offer = flow.Offer.CurrentValue;
                switch (offer.Step)
                {
                    case RunStep.EnteringRoom:
                        battles++;
                        battleSeeds.Add(offer.BattleSeed);
                        Assert.IsNotNull(offer.Encounter.Enemies, "인카운터가 생성돼 실려 온다");
                        flow.ReportBattleResult(won: true, offer.Token).Forget();
                        break;
                    case RunStep.RevealReward:
                        revealAtRoom[session.RoomIndex] = offer.Receipts;
                        flow.ReportResultConfirmed(offer.Token).Forget();
                        break;
                    case RunStep.BuffPick:
                        cardPicks++;
                        Assert.AreEqual(3, offer.Cards.Count, "3택1 후보는 항상 3장이다");
                        Assert.IsTrue(offer.Cards.All(c => c.Odds > 0f && c.Odds <= 1f), "공시 확률이 실려 온다");
                        flow.ReportCardAssigned(offer.Cards[0].Card, 0, offer.Token).Forget();
                        break;
                    case RunStep.DoorPoint:
                        doorPoints++;
                        Assert.AreEqual(3, offer.Doors.Count, "문 지점은 3종 제시다");
                        Assert.AreEqual(3, offer.Doors.Select(d => d.Kind).Distinct().Count(), "비복원이라 중복이 없다");
                        // 재화 문이 있으면 그 문을 골라 지연 지급 경로를 태운다. 없으면 첫 문(버프)을 고른다.
                        var currency = offer.Doors.Where(d =>
                            d.Kind == DoorKind.Gold || d.Kind == DoorKind.Manual || d.Kind == DoorKind.Essence).ToList();
                        var picked = currency.Count > 0 ? currency[0].Kind : offer.Doors[0].Kind;
                        flow.ReportDoorPicked(picked, offer.Token).Forget();
                        break;
                    default:
                        Assert.Fail($"예상 밖 스텝 {offer.Step}");
                        break;
                }
            }

            Assert.Less(guard, 200, "루프가 수렴한다");
            Assert.AreEqual(7, battles, "방 7전투");
            Assert.AreEqual(5, doorPoints, "문 지점 5곳");
            Assert.AreEqual(7, battleSeeds.Distinct().Count(), "방별 전투 시드가 전부 다르다");
            Assert.AreEqual(5, session.DoorPointsPassed);
            Assert.AreEqual(7, session.RoomIndex, "넘긴 방 7 = 정산 입력");
            Assert.IsTrue(progress.IsCleared(chapter), "챕터 클리어 기록");

            // 미드보스(방4, RoomIndex 3에서 공개) 결과는 문③ 공개 + 2종 즉시 지급이 겹치는 지점이다.
            Assert.IsTrue(revealAtRoom.ContainsKey(3), "미드보스 방 결과가 있었다");

            // 정산: 표 7행(700) + 승리 보너스(400). 런 중 재화 문 수입은 시드에 따라 다르므로 하한만 본다.
            var terminal = flow.Offer.CurrentValue;
            Assert.AreEqual(RunStep.RunClear, terminal.Step);
            Assert.AreEqual(1100, terminal.Receipts.Single(r => r.type == CurrencyType.Gold).amount,
                "정산 = 700 + 승리 400");

            flow.ReportResultConfirmed(terminal.Token).Forget();
            Assert.AreEqual(1, sceneFlow.ToMainCount, "정산 확인 후 로비 복귀 1회");

            wallet.Dispose();
        }

        [Test]
        public void 같은_시드는_같은_문과_같은_인카운터를_낸다()
        {
            var first = DrawSequence(777);
            var second = DrawSequence(777);
            var different = DrawSequence(778);

            CollectionAssert.AreEqual(first, second, "같은 시드는 같은 런을 재현한다");
            CollectionAssert.AreNotEqual(first, different, "다른 시드는 다른 런이다");
        }

        // 풀 사이클을 전승으로 돌리며 (문 제시, 인카운터 구성)의 결정적 지문을 뽑는다.
        private static List<string> DrawSequence(int seed)
        {
            var chapter = RunFixtures.DocChapter();
            var tuning = RunFixtures.Tuning();
            using var wallet = new CurrencyWallet();
            var party = RunFixtures.Party(4);
            var session = new ChapterRunSession(chapter, tuning, party, seed);
            var doorCatalog = RunFixtures.DoorCatalog();
            var cardCatalog = RunFixtures.CardCatalog(party.Select(o => o.Definition.id).ToArray());
            var doorRng = new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Door));
            var flow = new ChapterRunFlow(
                session,
                new EncounterGenerator(tuning,
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Mutation))),
                new DoorDraw(doorCatalog, doorRng),
                new CardPool(cardCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Card))),
                doorRng,
                doorCatalog,
                new RunRewardService(new CurrencyService(wallet)),
                new ChapterProgress(),
                saveService: null,
                new FakeSceneFlow());

            var trace = new List<string>();
            flow.BeginRun().Forget();
            int guard = 0;
            while (flow.Current != RunStep.RunClear && guard++ < 200)
            {
                var offer = flow.Offer.CurrentValue;
                switch (offer.Step)
                {
                    case RunStep.EnteringRoom:
                        trace.Add("room:" + string.Join(",", offer.Encounter.Enemies.Select(e => e.Enemy.id)));
                        flow.ReportBattleResult(true, offer.Token).Forget();
                        break;
                    case RunStep.RevealReward:
                        trace.Add("reveal:" + string.Join(",", offer.Receipts.Select(r => $"{r.type}{r.amount}")));
                        flow.ReportResultConfirmed(offer.Token).Forget();
                        break;
                    case RunStep.BuffPick:
                        trace.Add("pick:" + string.Join(",", offer.Cards.Select(c => c.Card.id)));
                        flow.ReportCardAssigned(offer.Cards[0].Card, 0, offer.Token).Forget();
                        break;
                    case RunStep.DoorPoint:
                        trace.Add("doors:" + string.Join(",", offer.Doors.Select(d => d.Kind)));
                        flow.ReportDoorPicked(offer.Doors[0].Kind, offer.Token).Forget();
                        break;
                }
            }
            return trace;
        }
    }
}