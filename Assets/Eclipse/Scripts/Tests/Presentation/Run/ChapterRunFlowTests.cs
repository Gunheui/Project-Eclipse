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
    /// <summary>
    /// 런 상태기계의 스모크·멱등·커밋 검증. 화면 없이 Report*를 직접 호출해
    /// 제시물(Offer) 전이와 지급·저장 횟수를 확인한다.
    /// </summary>
    public class ChapterRunFlowTests
    {
        // 지급 창구 호출을 세면서 실제 지갑 지급은 위임하는 계수기.
        private sealed class CountingRewards : IRewardService
        {
            private readonly IRewardService _inner;
            public int GrantCalls;

            public CountingRewards(IRewardService inner) { _inner = inner; }

            public IReadOnlyList<RewardEntry> Grant(IEnumerable<RewardEntry> entries)
            {
                GrantCalls++;
                return _inner.Grant(entries);
            }
        }

        private sealed class FakeSceneFlow : ISceneFlow
        {
            public int ToMainCount;
            public UniTask ToBattleAsync() => UniTask.CompletedTask;
            public UniTask ToMainAsync() { ToMainCount++; return UniTask.CompletedTask; }
        }

        private sealed class Harness
        {
            public ChapterRunSession Session;
            public ChapterRunFlow Flow;
            public CurrencyWallet Wallet;
            public CountingRewards Rewards;
            public ChapterProgress Progress;
            public FakeSceneFlow SceneFlow;
            public ChapterSO Chapter;
            public EncounterTuningSO Tuning;
            public BattleConstantsSO Constants;

            public RunOffer Offer => Flow.Offer.CurrentValue;
        }

        private static Harness Build(ChapterSO chapter, int seed = 20260727, IReadOnlyList<OwnedCharacter> party = null)
        {
            var h = new Harness();
            h.Chapter = chapter;
            h.Tuning = RunFixtures.Tuning();
            h.Wallet = new CurrencyWallet();
            h.Rewards = new CountingRewards(new RunRewardService(new CurrencyService(h.Wallet)));
            h.Progress = new ChapterProgress();
            h.SceneFlow = new FakeSceneFlow();
            h.Constants = ScriptableObject.CreateInstance<BattleConstantsSO>();

            var runParty = party ?? RunFixtures.Party(4);
            h.Session = new ChapterRunSession(chapter, h.Tuning, runParty, seed);
            var doorCatalog = RunFixtures.DoorCatalog();
            var cardCatalog = RunFixtures.CardCatalog(
                runParty.Where(o => o != null).Select(o => o.Definition.id).ToArray());
            h.Flow = new ChapterRunFlow(
                h.Session,
                new EncounterGenerator(h.Tuning,
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Encounter)),
                    new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Mutation))),
                new DoorDraw(doorCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Door))),
                new CardPool(cardCatalog, new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Card))),
                new SeededRandom(RunSeed.For(seed, RunSeed.Stream.Currency)),
                doorCatalog,
                h.Rewards,
                h.Progress,
                saveService: null,
                h.SceneFlow);
            return h;
        }

        // --- DoD 1: 2전투 미니 루프 스모크 ---

        [UnityTest]
        public IEnumerator 스모크_방을_넘으면_엔진과_전투원이_새로_서고_버프가_다음_전투에_반영된다() => UniTask.ToCoroutine(async () =>
        {
            // 방1은 문 없이 넘어간다 — 방→방 전이만 본다(방2 뒤 보스 문 지점까지는 가지 않는다).
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, false), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            var factory = new BattleFactory(h.Constants, h.Session, h.Tuning);
            h.Flow.BeginRun().Forget();

            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            var first = factory.Create(h.Offer.Encounter, h.Offer.BattleSeed, startAuto: true);
            var firstAlly = first.Combatants.First(u => u.IsAlly);

            // 1번째 전투를 실제로 완주한다 — 아군이 맞아 HP가 깎인 상태로 방을 넘는다.
            await first.RunBattleAsync(null, CancellationToken.None);
            Assert.AreEqual(BattleResult.Victory, first.Result.CurrentValue);

            int firstToken = h.Offer.Token;
            h.Flow.ReportBattleResult(won: true, firstToken).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step, "문 없는 방은 승리 즉시 다음 방이 제시된다");

            // 전투 사이 버프: 생명력 +50%를 0번 슬롯에 배정한다(HP는 MaxHp로 밖에서 관찰 가능한 축).
            h.Session.AttachCard(new BuffCard
            {
                id = "hp", displayName = "hp", grade = CardGrade.Common,
                deltas = new[] { new StatDelta { axis = StatType.Hp, value = 0.5f } },
            }, 0);

            var second = factory.Create(h.Offer.Encounter, h.Offer.BattleSeed, startAuto: true);
            var secondAlly = second.Combatants.First(u => u.IsAlly);

            Assert.AreNotSame(first, second, "방마다 뷰모델이 새로 선다");
            Assert.AreNotSame(firstAlly, secondAlly, "전투원 뷰모델도 새로 선다");
            Assert.AreEqual(secondAlly.MaxHp, secondAlly.CurrentHp.CurrentValue, "방 진입은 풀피다(방 사이 완전 회복)");
            Assert.AreEqual(1500, secondAlly.MaxHp, "버프(+50%)가 2번째 전투 스탯에 접혔다");
            Assert.AreEqual(1000, firstAlly.MaxHp, "1번째 전투원은 버프 이전 값 그대로다");

            // 중복 결과 보고(오래된 토큰)는 무시된다.
            h.Flow.ReportBattleResult(won: false, firstToken).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step, "낡은 토큰 보고는 상태를 바꾸지 못한다");

            first.Dispose();
            second.Dispose();
        });

        [Test]
        public void 스모크_패배하면_다음_전투가_제시되지_않고_정산으로_간다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, false), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);

            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step, "패배는 곧장 정산 제시다 — 3번째 전투는 없다");
            Assert.AreEqual(100, h.Offer.DepthReward.Single(r => r.type == CurrencyType.Gold).amount,
                "넘긴 방 1 기준 도달 보상");
            CollectionAssert.IsEmpty(h.Offer.VictoryBonus, "실패는 승리 보너스가 없다");
            Assert.IsFalse(h.Progress.IsCleared(h.Chapter), "실패는 클리어를 기록하지 않는다");
        }

        [Test]
        public void 방1_전멸은_지급분이_한_푼도_없다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            int before = h.Wallet.Gold.CurrentValue;

            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            CollectionAssert.IsEmpty(h.Offer.ExploreReward, "문을 지난 적이 없다");
            CollectionAssert.IsEmpty(h.Offer.DepthReward, "정산 표 0행");
            CollectionAssert.IsEmpty(h.Offer.RewardTotal);
            Assert.AreEqual(before, h.Wallet.Gold.CurrentValue);
        }

        [Test]
        public void 파티가_다_차지_않으면_런이_시작되지_않는다()
        {
            var h = Build(RunFixtures.DocChapter(), party: RunFixtures.Party(3));

            Assert.Throws<InvalidOperationException>(() => h.Flow.BeginRun().Forget());
        }

        // --- 캐릭터 문: 슬롯이 에스크로를 지나 배정까지 간다 ---

        [Test]
        public void 캐릭터_문은_문이_가리킨_슬롯에_카드가_배정된다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);

            // 캐릭터 문이 안 뽑힌 시드면 문 자체를 검증할 수 없으므로 뽑힌 문 중에서 고른다.
            int index = IndexOf(h, d => d.Rewards[0].IsCharacterDoor);
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 캐릭터 문이 없다");
            int slot = h.Offer.Doors[index].Rewards[0].TargetPartySlot;

            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.BuffPick, h.Offer.Step);
            Assert.AreEqual(h.Session.Party[slot].Definition.displayName, h.Offer.Cards[0].Target,
                "에스크로를 지나도 귀속 대상이 남는다");

            var card = h.Offer.Cards[0].Card;
            var axis = card.deltas[0].axis;
            h.Flow.ReportCardPicked(card, h.Offer.Token).Forget();

            int other = (slot + 1) % PlayerSave.PartySlotCount;
            Assert.AreNotEqual(0f, h.Session.BuffsOf(slot).SumOf(axis), "문이 가리킨 슬롯에 붙었다");
            Assert.AreEqual(0f, h.Session.BuffsOf(other).SumOf(axis), "다른 슬롯은 그대로다");
        }

        [Test]
        public void 제시되지_않은_카드는_받지_않는다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            int index = IndexOf(h, d => !CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 버프 문이 없다");

            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.BuffPick, h.Offer.Step);

            var absent = new BuffCard
            {
                id = "제시되지_않은_카드", displayName = "위조", grade = CardGrade.Epic,
                deltas = new[] { new StatDelta { axis = StatType.Atk, value = 9f } },
            };
            h.Flow.ReportCardPicked(absent, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.BuffPick, h.Offer.Step, "제시 밖 선택은 상태를 바꾸지 못한다");
            Assert.IsTrue(Enumerable.Range(0, PlayerSave.PartySlotCount)
                .All(s => h.Session.BuffsOf(s).SumOf(StatType.Atk) == 0f), "위조 카드는 어디에도 붙지 않는다");
        }

        [Test]
        public void 같은_id로_수치만_부풀린_보고는_제시한_값으로_붙는다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            int index = IndexOf(h, d => d.Rewards[0].IsCharacterDoor);
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 캐릭터 문이 없다");
            int slot = h.Offer.Doors[index].Rewards[0].TargetPartySlot;

            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.BuffPick, h.Offer.Step);

            var offered = h.Offer.Cards[0].Card;
            var axis = offered.deltas[0].axis;
            float honest = offered.deltas[0].value;

            // id만 베끼고 증감을 부풀린 카드. deltas가 배열이라 id 대조만으로는 걸러지지 않는다.
            var inflated = offered;
            inflated.deltas = new[] { new StatDelta { axis = axis, value = honest + 9f } };
            h.Flow.ReportCardPicked(inflated, h.Offer.Token).Forget();

            Assert.AreEqual(honest, h.Session.BuffsOf(slot).SumOf(axis), 1e-4f,
                "붙는 값은 제시한 카드의 수치다");
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void 제시하지_않은_자리_보고는_받지_않는다(int optionIndex)
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            Assert.AreEqual(3, h.Offer.Doors.Count);

            h.Flow.ReportDoorPicked(optionIndex, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step, "제시 밖 선택은 상태를 바꾸지 못한다");
            Assert.IsFalse(h.Session.HasEscrow);
        }

        /// <summary> 조건에 맞는 문의 자리를 찾는다. 없으면 -1이다. </summary>
        private static int IndexOf(Harness h, Func<DoorOption, bool> match)
        {
            var doors = h.Offer.Doors;
            for (int i = 0; i < doors.Count; i++)
                if (match(doors[i])) return i;
            return -1;
        }

        // --- 재화 월드 드랍 페이로드 ---

        [Test]
        public void 재화_문_보상은_다음_제시물에_드랍으로_한_번만_실린다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            Assert.IsNull(h.Offer.RoomDrops, "첫 방 진입은 공개할 보상이 없다");

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            Assert.IsNull(h.Offer.RoomDrops, "에스크로 없는 방 승리에는 드랍이 없다");

            int index = IndexOf(h, d => CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 재화 문이 없다");
            var expectedType = CurrencyDoor.CurrencyOf(h.Offer.Doors[index].Rewards[0].Kind);

            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            Assert.IsNull(h.Offer.RoomDrops, "보류 중인 에스크로는 아직 공개되지 않는다");

            int before = Balance(h, expectedType);
            int battleToken = h.Offer.Token;
            h.Flow.ReportBattleResult(won: true, battleToken).Forget();

            var drop = h.Offer.RoomDrops.Single();
            Assert.AreEqual(expectedType, drop.type);
            Assert.AreEqual(before, Balance(h, expectedType), "드랍은 적립일 뿐 지갑은 런 종료까지 그대로다");

            // 같은 토큰 재보고(전투 종료 콜백 중복)는 드랍을 다시 만들지 않는다.
            h.Flow.ReportBattleResult(won: true, battleToken).Forget();
            Assert.AreEqual(drop.amount, h.Offer.RoomDrops.Single().amount);
            Assert.AreEqual(before, Balance(h, expectedType), "적립도 한 번뿐이다");

            // 보스 문을 지나 보스 방을 깬다. 공개할 문 보상이 없어 드랍이 비고, 적립분이 정산과 함께 지급된다.
            h.Flow.ReportDoorPicked(0, h.Offer.Token).Forget();
            Assert.IsNull(h.Offer.RoomDrops, "드랍은 1회성이라 다음 제시물에는 남지 않는다");
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.RunClear, h.Offer.Step);
            Assert.AreEqual(drop.amount, h.Offer.ExploreReward.Single(e => e.type == expectedType).amount,
                "드랍으로 공개한 수량이 종료 시 그대로 지급된다");
        }

        [Test]
        public void 런_중_적립분은_전멸해도_정산과_함께_지급된다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            int index = IndexOf(h, d => CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 재화 문이 없다");
            var type = CurrencyDoor.CurrencyOf(h.Offer.Doors[index].Rewards[0].Kind);

            // 문을 고르고 다음 방을 넘겨 적립시킨 뒤, 보스 문을 지나 보스 방에서 전멸한다.
            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            int before = Balance(h, type);
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            int earned = h.Offer.RoomDrops.Single().amount;
            Assert.AreEqual(before, Balance(h, type), "적립 단계에서는 지갑이 그대로다");

            h.Flow.ReportDoorPicked(0, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            Assert.AreEqual(earned, h.Offer.ExploreReward.Single(e => e.type == type).amount,
                "전멸도 런을 끝낸 것이라 적립분이 살아 지급된다");
            int settlement = h.Offer.DepthReward.Where(e => e.type == type).Sum(e => e.amount);
            Assert.AreEqual(before + earned + settlement, Balance(h, type), "지갑 = 적립분 + 정산");
            Assert.AreEqual(earned + settlement, h.Offer.RewardTotal.Single(e => e.type == type).amount,
                "합계는 화면에 뜬 두 행을 그대로 더한 값이다");
        }

        [Test]
        public void 버프_문_에스크로는_드랍_없이_3택1로_직행한다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            int index = IndexOf(h, d => !CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 버프 문이 없다");

            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.BuffPick, h.Offer.Step);
            Assert.IsNull(h.Offer.RoomDrops, "버프 문은 지급할 재화가 없다");
        }

        private static int Balance(Harness h, CurrencyType type) => type switch
        {
            CurrencyType.Gold => h.Wallet.Gold.CurrentValue,
            CurrencyType.Essence => h.Wallet.Essence.CurrentValue,
            _ => h.Wallet.Manual.CurrentValue,
        };

        // --- 멱등·커밋 순서 (§3-2a 검증) ---

        [Test]
        public void 낡은_토큰_보고는_지갑과_스텝을_바꾸지_못한다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            int doorToken = h.Offer.Token;

            h.Flow.ReportDoorPicked(0, doorToken).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            int gold = h.Wallet.Gold.CurrentValue;
            int grantCalls = h.Rewards.GrantCalls;

            // 문 지점 화면의 늦은 콜백(같은 종류의 이전 스텝)이 다시 도착해도 무시된다.
            h.Flow.ReportDoorPicked(0, doorToken).Forget();

            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            Assert.AreEqual(gold, h.Wallet.Gold.CurrentValue);
            Assert.AreEqual(grantCalls, h.Rewards.GrantCalls);
        }

        [Test]
        public void 종료_커밋은_두_번_불려도_지급과_클리어_기록이_한_번이다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            h.Flow.ReportDoorPicked(0, h.Offer.Token).Forget();

            // 보스 방 승리는 보상 공개를 거쳐 종료 커밋까지 한 번에 간다.
            int bossToken = h.Offer.Token;
            h.Flow.ReportBattleResult(won: true, bossToken).Forget();
            h.Flow.ReportBattleResult(won: true, bossToken).Forget(); // 늦은 중복 보고 — 무시

            Assert.AreEqual(RunStep.RunClear, h.Offer.Step);
            int grantCalls = h.Rewards.GrantCalls;
            int gold = h.Wallet.Gold.CurrentValue;
            Assert.AreEqual(1600, gold, "시작 1000 + 넘긴 방 2×100 + 승리 보너스 400");
            Assert.IsTrue(h.Progress.IsCleared(h.Chapter));

            // 정산 확인 → 로비 복귀는 Flow 단독 권한이고, 더블 탭에도 1회다.
            int settleToken = h.Offer.Token;
            h.Flow.ReportResultConfirmed(settleToken).Forget();
            h.Flow.ReportResultConfirmed(settleToken).Forget();
            Assert.AreEqual(1, h.SceneFlow.ToMainCount);
            Assert.AreEqual(grantCalls, h.Rewards.GrantCalls, "지급 호출이 늘지 않는다");
            Assert.AreEqual(gold, h.Wallet.Gold.CurrentValue);
        }

        [Test]
        public void 미공개_에스크로는_실패_시_몰수되고_정산만_지급된다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);

            // 골드 문을 골라 보류시킨 채 다음 방에서 패배한다 — 보류분은 지급되지 않아야 한다.
            int index = IndexOf(h, d => d.Rewards[0].Kind == DoorKind.Gold);
            h.Flow.ReportDoorPicked(index < 0 ? 0 : index, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            Assert.IsFalse(h.Session.HasEscrow, "종료 커밋 첫 단계에서 몰수됐다");
            Assert.AreEqual(3, h.Rewards.GrantCalls, "종료 지급은 탐험·도달·보너스 3회 고정");
            Assert.AreEqual(100, h.Wallet.Gold.CurrentValue - 1000, "지갑 증가분 = 정산(방 1)뿐 — 몰수분은 적립되지 않았다");
        }

        // --- 런 포기 ---

        [Test]
        public void 전투_중_챕터_포기는_지급과_정산_화면_없이_로비로_간다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, false), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            int gold = h.Wallet.Gold.CurrentValue;

            h.Flow.AbandonRun().Forget();

            Assert.AreEqual(1, h.SceneFlow.ToMainCount, "확인 팝업 하나로 곧장 로비다");
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step, "정산 제시물이 뜨지 않는다");
            Assert.AreEqual(0, h.Rewards.GrantCalls, "포기는 한 푼도 지급하지 않는다");
            Assert.AreEqual(gold, h.Wallet.Gold.CurrentValue);
            Assert.IsFalse(h.Progress.IsCleared(h.Chapter), "클리어 기록은 손대지 않는다");
        }

        [Test]
        public void 문_지점_챕터_포기는_에스크로와_적립분을_모두_버린다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            int index = IndexOf(h, d => CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
            if (index < 0)
                Assert.Ignore("이 시드의 문 지점에 재화 문이 없다");
            var type = CurrencyDoor.CurrencyOf(h.Offer.Doors[index].Rewards[0].Kind);

            // 재화 문을 지나 적립까지 끝낸 다음 문 지점에서 포기한다.
            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
            int before = Balance(h, type);
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            Assert.Greater(h.Session.RunIncome.Count, 0, "장부에 적립분이 쌓인 상태다");

            h.Flow.AbandonRun().Forget();

            Assert.AreEqual(1, h.SceneFlow.ToMainCount);
            Assert.AreEqual(0, h.Rewards.GrantCalls, "적립분이 지급 창구를 지나지 않는다");
            Assert.AreEqual(before, Balance(h, type), "지갑은 그대로다");
            Assert.IsFalse(h.Session.HasEscrow, "보류분도 함께 몰수된다");
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step, "정산 제시물이 뜨지 않는다");
        }

        [Test]
        public void 챕터_포기는_두_번_보고해도_로비_복귀가_한_번이다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.AbandonRun().Forget();
            h.Flow.AbandonRun().Forget();

            Assert.AreEqual(1, h.SceneFlow.ToMainCount);
            Assert.AreEqual(0, h.Rewards.GrantCalls);
        }

        [Test]
        public void 정산이_이미_선_뒤의_챕터_포기는_아무것도_바꾸지_않는다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            int grantCalls = h.Rewards.GrantCalls;
            int gold = h.Wallet.Gold.CurrentValue;

            h.Flow.AbandonRun().Forget();

            // 커밋 가드에 막혀 포기가 성립하지 않는다. 화면은 이 반환을 보고 나가기를 내려 둔다.
            Assert.AreEqual(0, h.SceneFlow.ToMainCount, "정산 확인을 거치지 않고 로비로 빠지지 않는다");
            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            Assert.AreEqual(grantCalls, h.Rewards.GrantCalls, "이미 끝난 지급을 되돌리지 않는다");
            Assert.AreEqual(gold, h.Wallet.Gold.CurrentValue);
        }

        [Test]
        public void 챕터_포기_뒤_늦은_전투와_문과_카드_보고는_무시된다()
        {
            // Emit마다 토큰이 올라 한 시나리오로는 세 종류를 동시에 유효하게 둘 수 없다. 상태를 각각 세운다.
            var battle = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            battle.Flow.BeginRun().Forget();
            int battleToken = battle.Offer.Token;
            battle.Flow.AbandonRun().Forget();
            battle.Flow.ReportBattleResult(won: true, battleToken).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, battle.Offer.Step, "다음 방이 제시되지 않는다");
            Assert.AreEqual(0, battle.Rewards.GrantCalls);
            Assert.AreEqual(1, battle.SceneFlow.ToMainCount);

            var door = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            door.Flow.BeginRun().Forget();
            door.Flow.ReportBattleResult(won: true, door.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, door.Offer.Step);
            int doorToken = door.Offer.Token;
            door.Flow.AbandonRun().Forget();
            door.Flow.ReportDoorPicked(0, doorToken).Forget();
            Assert.AreEqual(RunStep.DoorPoint, door.Offer.Step, "문 선택이 다음 방을 열지 못한다");
            Assert.IsFalse(door.Session.HasEscrow, "문 보상이 다시 보류되지 않는다");

            var pick = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, true), RunFixtures.Boss()));
            pick.Flow.BeginRun().Forget();
            pick.Flow.ReportBattleResult(won: true, pick.Offer.Token).Forget();
            int buffIndex = IndexOf(pick, d => !CurrencyDoor.IsCurrency(d.Rewards[0].Kind));
            if (buffIndex < 0)
                Assert.Ignore("이 시드의 문 지점에 버프 문이 없다");
            pick.Flow.ReportDoorPicked(buffIndex, pick.Offer.Token).Forget();
            pick.Flow.ReportBattleResult(won: true, pick.Offer.Token).Forget();
            Assert.AreEqual(RunStep.BuffPick, pick.Offer.Step);

            int pickToken = pick.Offer.Token;
            var candidate = pick.Offer.Cards[0].Card;
            int acquired = pick.Session.AcquiredCards.Count;
            pick.Flow.AbandonRun().Forget();
            pick.Flow.ReportCardPicked(candidate, pickToken).Forget();
            Assert.AreEqual(acquired, pick.Session.AcquiredCards.Count, "카드가 배정되지 않는다");
            Assert.AreEqual(RunStep.BuffPick, pick.Offer.Step);
        }

        // --- 미드보스 문 (문③) ---

        [Test]
        public void 미드보스_문을_고르면_2종이_한_단위로_보관되고_방4가_정예가_된다()
        {
            var h = Build(RunFixtures.DocChapter());
            AdvanceToMidBossPoint(h);

            int index = IndexOf(h, d => d.IsMidBoss);
            Assert.AreEqual(2, h.Offer.Doors[index].Rewards.Count, "미드보스 문은 보상 2종을 건다");
            Assert.AreEqual(2, h.Offer.Doors[index].RewardIcons.Count, "걸린 보상은 심볼 2개로 표시된다");
            Assert.AreEqual(4, h.Offer.Doors.SelectMany(d => d.Rewards).Distinct().Count(),
                "지점 안 4종은 서로 다르다");

            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();

            Assert.IsTrue(h.Session.HasEscrow);
            Assert.IsTrue(h.Session.MidBossEngaged);
            Assert.AreEqual(3, h.Session.DoorPointsPassed, "문③이라 깊이 3으로 보관된다");
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            Assert.AreEqual(RoomKind.Elite, h.Session.CurrentRoom.kind);
            Assert.IsTrue(h.Offer.IsEliteEncounter, "미드보스 문을 골랐으니 방4가 정예로 선다");
            Assert.AreSame(h.Session.Chapter.eliteBackground, h.Offer.Background, "정예 전투는 엘리트 배경으로 선다");
        }

        [Test]
        public void 일반_문을_고르면_방4가_정예_자리라도_일반_전투다()
        {
            var h = Build(RunFixtures.DocChapter());
            AdvanceToMidBossPoint(h);

            int index = IndexOf(h, d => !d.IsMidBoss);
            h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();

            Assert.IsFalse(h.Session.MidBossEngaged);
            Assert.AreEqual(RoomKind.Elite, h.Session.CurrentRoom.kind, "배치의 Elite는 정예 후보 자리일 뿐이다");
            Assert.IsFalse(h.Offer.IsEliteEncounter, "미드보스는 회피됐다");
            Assert.AreSame(h.Session.Chapter.normalBackground, h.Offer.Background,
                "미드보스를 피한 방4는 배경도 일반이다");
        }

        [Test]
        public void 미드보스_방_전멸은_걸린_2종을_함께_몰수한다()
        {
            var h = Build(RunFixtures.DocChapter());
            AdvanceToMidBossPoint(h);

            h.Flow.ReportDoorPicked(IndexOf(h, d => d.IsMidBoss), h.Offer.Token).Forget();
            int income = h.Session.RunIncome.Count;
            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            Assert.IsFalse(h.Session.HasEscrow, "2종이 한 단위로 몰수됐다");
            Assert.AreEqual(income, h.Session.RunIncome.Count, "몰수분은 장부에 닿지 않는다");
            Assert.IsNull(h.Offer.RoomDrops, "공개되지 않았으니 드랍도 없다");
        }

        // 걸린 2종의 재화/버프 조합 네 가지. 조합은 시드가 정하므로 원하는 조합이 나오는 시드를 찾아 쓴다.
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void 미드보스_문의_2종은_좌에서_우로_해소된다(bool firstIsCurrency, bool secondIsCurrency)
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                var h = Build(RunFixtures.DocChapter(), seed);
                AdvanceToMidBossPoint(h);

                int index = IndexOf(h, d => d.IsMidBoss);
                var rewards = h.Offer.Doors[index].Rewards;
                if (CurrencyDoor.IsCurrency(rewards[0].Kind) != firstIsCurrency) continue;
                if (CurrencyDoor.IsCurrency(rewards[1].Kind) != secondIsCurrency) continue;

                h.Flow.ReportDoorPicked(index, h.Offer.Token).Forget();
                h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
                AssertResolvedInOrder(h, rewards);
                return;
            }
            Assert.Ignore("120개 시드 안에 이 조합의 미드보스 문이 없다");
        }

        /// <summary> 걸린 보상이 순서대로 해소됐는지 본다. 재화는 드랍 목록, 버프는 3택1 순번으로 관측된다. </summary>
        private static void AssertResolvedInOrder(Harness h, IReadOnlyList<DoorChoice> rewards)
        {
            var expectedDrops = rewards.Where(r => CurrencyDoor.IsCurrency(r.Kind))
                .Select(r => CurrencyDoor.CurrencyOf(r.Kind))
                .ToList();
            var actualDrops = h.Offer.RoomDrops?.Select(d => d.type).ToList() ?? new List<CurrencyType>();
            CollectionAssert.AreEqual(expectedDrops, actualDrops, "재화는 걸린 순서대로 적립된다");

            foreach (var buff in rewards.Where(r => !CurrencyDoor.IsCurrency(r.Kind)))
            {
                Assert.AreEqual(RunStep.BuffPick, h.Offer.Step, "버프는 걸린 순서대로 3택1이 뜬다");
                string expectedTarget = buff.IsCharacterDoor
                    ? h.Session.Party[buff.TargetPartySlot].Definition.displayName
                    : RunTexts.EnemyTarget;
                Assert.AreEqual(expectedTarget, h.Offer.Cards[0].Target);
                h.Flow.ReportCardPicked(h.Offer.Cards[0].Card, h.Offer.Token).Forget();
            }

            Assert.AreNotEqual(RunStep.BuffPick, h.Offer.Step, "걸린 버프를 다 처리하면 3택1이 끝난다");
        }

        // --- 최종보스 문 ---

        [Test]
        public void 보스_직전_문_지점은_추첨_없이_보스_문_하나만_제시한다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            Assert.AreEqual(1, h.Offer.Doors.Count, "보스 문 지점은 문 하나다");
            Assert.AreEqual(DoorTier.FinalBoss, h.Offer.Doors[0].Tier);
            CollectionAssert.IsEmpty(h.Offer.Doors[0].Rewards, "보스 문에는 보상이 걸리지 않는다");
        }

        [Test]
        public void 보스_문_선택은_에스크로도_문_지점_깊이도_건드리지_않는다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            int depth = h.Session.DoorPointsPassed;

            h.Flow.ReportDoorPicked(0, h.Offer.Token).Forget();

            Assert.IsFalse(h.Session.HasEscrow, "보상 없는 문은 보류분을 만들지 않는다");
            Assert.AreEqual(depth, h.Session.DoorPointsPassed, "재화 공식의 깊이가 오르지 않는다");
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            Assert.AreEqual(RoomKind.Boss, h.Session.CurrentRoom.kind, "보스 문 뒤는 곧장 보스 방이다");
        }

        [Test]
        public void 보스_직전_방이_문을_열지_않는_배치는_런_시작에서_끊긴다()
        {
            var noBossDoor = RunFixtures.Chapter(RunFixtures.Normal(1, false), RunFixtures.Boss());

            Assert.Throws<ArgumentException>(() => new ChapterRunSession(
                noBossDoor, RunFixtures.Tuning(), RunFixtures.Party(4), runSeed: 1));
        }

        /// <summary> 미드보스 문이 서는 문 지점까지 전승으로 밀어 올린다. 앞선 문 지점은 첫 문을 고른다. </summary>
        private static void AdvanceToMidBossPoint(Harness h)
        {
            h.Flow.BeginRun().Forget();
            for (int guard = 0; guard < 50; guard++)
            {
                var offer = h.Offer;
                if (offer.Step == RunStep.DoorPoint && offer.Doors.Any(d => d.IsMidBoss))
                    return;

                switch (offer.Step)
                {
                    case RunStep.EnteringRoom: h.Flow.ReportBattleResult(won: true, offer.Token).Forget(); break;
                    case RunStep.BuffPick: h.Flow.ReportCardPicked(offer.Cards[0].Card, offer.Token).Forget(); break;
                    case RunStep.DoorPoint: h.Flow.ReportDoorPicked(0, offer.Token).Forget(); break;
                    default: Assert.Fail($"미드보스 문 전에 {offer.Step}으로 끝났다"); return;
                }
            }
            Assert.Fail("미드보스 문 지점에 도달하지 못했다");
        }
    }
}