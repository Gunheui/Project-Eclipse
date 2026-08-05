using System;
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
            var flow = new ChapterRunFlow(
                session,
                new EncounterGenerator(tuning,
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Mutation))),
                new DoorDraw(doorCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Door))),
                new CardPool(cardCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Card))),
                new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Currency)),
                doorCatalog,
                rewards,
                progress,
                saveService: null,
                sceneFlow);

            int battles = 0, doorPoints = 0, cardPicks = 0, drops = 0;
            var battleSeeds = new List<int>();
            bool foughtMidBoss = false;

            flow.BeginRun().Forget();
            int guard = 0;
            while (flow.Current != RunStep.RunClear && guard++ < 200)
            {
                var offer = flow.Offer.CurrentValue;
                // 드랍은 스텝을 가리지 않고 실려 오므로 스텝 분기 밖에서 센다.
                if (offer.RoomDrops != null)
                    drops += offer.RoomDrops.Count;

                switch (offer.Step)
                {
                    case RunStep.EnteringRoom:
                        battles++;
                        battleSeeds.Add(offer.BattleSeed);
                        foughtMidBoss |= offer.IsEliteEncounter;
                        Assert.AreEqual(session.CurrentRoom.kind == RoomKind.Elite, offer.IsEliteEncounter,
                            "미드보스 문을 골랐으니 정예 자리의 방만 정예로 선다");
                        Assert.IsNotNull(offer.Encounter.Enemies, "인카운터가 생성돼 실려 온다");
                        flow.ReportBattleResult(won: true, offer.Token).Forget();
                        break;
                    case RunStep.BuffPick:
                        cardPicks++;
                        Assert.AreEqual(3, offer.Cards.Count, "3택1 후보는 항상 3장이다");
                        foreach (var option in offer.Cards)
                            Assert.AreEqual(option.Card.targetsEnemies, option.Target == RunTexts.EnemyTarget,
                                "저주 카드만 적 전체 귀속이고, 나머지는 문이 가리킨 캐릭터 이름이다");
                        flow.ReportCardPicked(offer.Cards[0].Card, offer.Token).Forget();
                        break;
                    case RunStep.DoorPoint:
                        doorPoints++;
                        if (offer.Doors.Count == 1)
                        {
                            Assert.AreEqual(DoorTier.FinalBoss, offer.Doors[0].Tier, "문 하나짜리 지점은 보스 문뿐이다");
                            CollectionAssert.IsEmpty(offer.Doors[0].Rewards, "보스 문에는 보상이 걸리지 않는다");
                            flow.ReportDoorPicked(0, offer.Token).Forget();
                            break;
                        }
                        Assert.AreEqual(3, offer.Doors.Count, "추첨 문 지점은 3개 제시다");
                        var offered = offer.Doors.SelectMany(d => d.Rewards).ToList();
                        Assert.AreEqual(offered.Count, offered.Distinct().Count(), "비복원이라 중복이 없다");
                        Assert.AreEqual(doorPoints == 3 ? 1 : 0, offer.Doors.Count(d => d.IsMidBoss),
                            "미드보스 문은 방4 직전 지점에만 선다");
                        // 미드보스 문이 있으면 그 문을 골라 정예 경로를 태운다. 없으면 재화 문 우선이다.
                        int midBoss = offer.Doors.ToList().FindIndex(d => d.IsMidBoss);
                        int currency = offer.Doors.ToList()
                            .FindIndex(d => CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
                        flow.ReportDoorPicked(midBoss >= 0 ? midBoss : Math.Max(currency, 0), offer.Token).Forget();
                        break;
                    default:
                        Assert.Fail($"예상 밖 스텝 {offer.Step}");
                        break;
                }
            }

            Assert.Less(guard, 200, "루프가 수렴한다");
            Assert.AreEqual(7, battles, "방 7전투");
            Assert.AreEqual(6, doorPoints, "추첨 문 지점 5곳 + 보스 문 지점");
            Assert.AreEqual(7, battleSeeds.Distinct().Count(), "방별 전투 시드가 전부 다르다");
            Assert.AreEqual(5, session.DoorPointsPassed, "보스 문 지점은 재화 공식의 깊이를 올리지 않는다");
            Assert.AreEqual(7, session.RoomIndex, "넘긴 방 7 = 정산 입력");
            Assert.IsTrue(progress.IsCleared(chapter), "챕터 클리어 기록");

            // 인런 공개는 화면을 거치지 않으므로 세션 장부로만 관측된다.
            Assert.IsNotEmpty(session.RunIncome, "런 중 공개된 재화가 장부에 쌓인다");
            Assert.IsTrue(foughtMidBoss, "문③에서 미드보스 문을 골랐으니 방4가 정예였다");
            Assert.AreEqual(6, drops + cardPicks, "문 5지점 = 일반 문 4종 + 미드보스 문 2종");

            // 정산: 표 7행(700)과 승리 보너스(400)가 각각의 행으로 갈라져 온다.
            var terminal = flow.Offer.CurrentValue;
            Assert.AreEqual(RunStep.RunClear, terminal.Step);
            Assert.AreEqual(700, terminal.DepthReward.Single(r => r.type == CurrencyType.Gold).amount,
                "도달 보상 = 표 7행");
            Assert.AreEqual(400, terminal.VictoryBonus.Single(r => r.type == CurrencyType.Gold).amount,
                "승리 보너스는 따로 실린다");
            Assert.AreEqual(
                terminal.ExploreReward.Concat(terminal.DepthReward).Concat(terminal.VictoryBonus)
                    .Where(r => r.type == CurrencyType.Gold).Sum(r => r.amount),
                terminal.RewardTotal.Single(r => r.type == CurrencyType.Gold).amount,
                "합계 = 탐험 + 도달 + 보너스");

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
            var flow = new ChapterRunFlow(
                session,
                new EncounterGenerator(tuning,
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Mutation))),
                new DoorDraw(doorCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Door))),
                new CardPool(cardCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Card))),
                new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Currency)),
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
                    case RunStep.BuffPick:
                        trace.Add("pick:" + string.Join(",", offer.Cards.Select(c => c.Card.id)));
                        flow.ReportCardPicked(offer.Cards[0].Card, offer.Token).Forget();
                        break;
                    case RunStep.DoorPoint:
                        // 지문에는 종류만이 아니라 슬롯과 자리 구분까지 남긴다 — 뭉치면 캐릭터 4문과
                        // 미드보스 자리가 지워진다.
                        trace.Add("doors:" + string.Join("|",
                            offer.Doors.Select(d => string.Join(",", d.Rewards))));
                        flow.ReportDoorPicked(0, offer.Token).Forget();
                        break;
                }
            }
            // 재화 문 롤은 화면에 안 뜨므로 장부를 지문에 넣는다. 재화 스트림 재현이 깨지면 여기서 잡힌다.
            trace.Add("income:" + string.Join(",", session.RunIncome.Select(r => $"{r.type}{r.amount}")));
            return trace;
        }
    }
}