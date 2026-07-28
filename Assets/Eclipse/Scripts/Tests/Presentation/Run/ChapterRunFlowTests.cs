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
            // 문 없는 3방(일반·일반·보스) — 방→방 전이만 본다.
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, false), RunFixtures.Normal(2, false), RunFixtures.Boss()));
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
                RunFixtures.Normal(1, false), RunFixtures.Normal(2, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);

            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step, "패배는 곧장 정산 제시다 — 3번째 전투는 없다");
            Assert.AreEqual(100, h.Offer.Receipts.Single(r => r.type == CurrencyType.Gold).amount,
                "넘긴 방 1 기준 정산(승리 보너스 없음)");
            Assert.IsFalse(h.Progress.IsCleared(h.Chapter), "실패는 클리어를 기록하지 않는다");
        }

        [Test]
        public void 파티가_다_차지_않으면_런이_시작되지_않는다()
        {
            var h = Build(RunFixtures.DocChapter(), party: RunFixtures.Party(3));

            Assert.Throws<InvalidOperationException>(() => h.Flow.BeginRun().Forget());
        }

        // --- 캐릭터 문: 슬롯이 에스크로를 지나 배정까지 간다 ---

        [Test]
        public void 캐릭터_문은_고른_슬롯에_카드가_배정된다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);

            // 캐릭터 문이 안 뽑힌 시드면 문 자체를 검증할 수 없으므로 뽑힌 문 중에서 고른다.
            var character = h.Offer.Doors.FirstOrDefault(d => d.Choice.IsCharacterDoor);
            if (character.DisplayName == null)
                Assert.Ignore("이 시드의 문 지점에 캐릭터 문이 없다");
            int slot = character.Choice.TargetPartySlot;

            h.Flow.ReportDoorPicked(character.Choice, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.BuffPick, h.Offer.Step);
            Assert.AreEqual(slot, h.Offer.BuffTargetPartySlot, "에스크로를 지나도 대상 슬롯이 남는다");

            // 화면이 엉뚱한 슬롯을 보고해도 강제 대상이 이긴다.
            int other = (slot + 1) % PlayerSave.PartySlotCount;
            var card = h.Offer.Cards[0].Card;
            var axis = card.deltas[0].axis;
            h.Flow.ReportCardAssigned(card, other, h.Offer.Token).Forget();

            Assert.AreNotEqual(0f, h.Session.BuffsOf(slot).SumOf(axis), "강제 대상 슬롯에 붙었다");
            Assert.AreEqual(0f, h.Session.BuffsOf(other).SumOf(axis), "보고된 슬롯은 무시됐다");
        }

        [Test]
        public void 제시되지_않은_문은_받지_않는다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);

            var offered = h.Offer.Doors.Select(d => d.Choice).ToList();
            var absent = AllDoors().First(c => !offered.Contains(c));
            h.Flow.ReportDoorPicked(absent, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step, "제시 밖 선택은 상태를 바꾸지 못한다");
            Assert.IsFalse(h.Session.HasEscrow);
        }

        private static IEnumerable<DoorChoice> AllDoors()
        {
            for (int slot = 0; slot < PlayerSave.PartySlotCount; slot++)
                yield return new DoorChoice(DoorKind.CharacterBuff, slot);
            yield return new DoorChoice(DoorKind.Curse);
            yield return new DoorChoice(DoorKind.Gold);
            yield return new DoorChoice(DoorKind.Manual);
            yield return new DoorChoice(DoorKind.Essence);
        }

        // --- 재화 월드 드랍 페이로드 ---

        [Test]
        public void 재화_문_보상은_다음_제시물에_드랍으로_한_번만_실린다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            Assert.IsNull(h.Offer.RoomDrops, "첫 방 진입은 공개할 보상이 없다");

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            Assert.IsNull(h.Offer.RoomDrops, "에스크로 없는 방 승리에는 드랍이 없다");

            var currency = h.Offer.Doors.FirstOrDefault(d => CurrencyDoor.IsCurrency(d.Choice.Kind));
            if (currency.DisplayName == null)
                Assert.Ignore("이 시드의 문 지점에 재화 문이 없다");
            var expectedType = CurrencyDoor.CurrencyOf(currency.Choice.Kind);

            h.Flow.ReportDoorPicked(currency.Choice, h.Offer.Token).Forget();
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

            // 보스 방 승리에는 공개할 문 보상이 없어 드랍이 비고, 여기서 적립분이 정산과 함께 지급된다.
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.RunClear, h.Offer.Step);
            Assert.IsNull(h.Offer.RoomDrops, "드랍은 1회성이라 다음 제시물에는 남지 않는다");
            Assert.AreEqual(drop.amount, h.Offer.RunIncome.Single(e => e.type == expectedType).amount,
                "드랍으로 공개한 수량이 종료 시 그대로 지급된다");
        }

        [Test]
        public void 런_중_적립분은_전멸해도_정산과_함께_지급된다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            var currency = h.Offer.Doors.FirstOrDefault(d => CurrencyDoor.IsCurrency(d.Choice.Kind));
            if (currency.DisplayName == null)
                Assert.Ignore("이 시드의 문 지점에 재화 문이 없다");
            var type = CurrencyDoor.CurrencyOf(currency.Choice.Kind);

            // 문을 고르고 다음 방을 넘겨 적립시킨 뒤, 보스 방에서 전멸한다.
            h.Flow.ReportDoorPicked(currency.Choice, h.Offer.Token).Forget();
            int before = Balance(h, type);
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            int earned = h.Offer.RoomDrops.Single().amount;
            Assert.AreEqual(before, Balance(h, type), "적립 단계에서는 지갑이 그대로다");

            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            Assert.AreEqual(earned, h.Offer.RunIncome.Single(e => e.type == type).amount,
                "전멸도 런을 끝낸 것이라 적립분이 살아 지급된다");
            int settlement = h.Offer.Receipts.Where(e => e.type == type).Sum(e => e.amount);
            Assert.AreEqual(before + earned + settlement, Balance(h, type), "지갑 = 적립분 + 정산");
        }

        [Test]
        public void 버프_문_에스크로는_드랍_없이_3택1로_직행한다()
        {
            var h = Build(RunFixtures.Chapter(
                RunFixtures.Normal(1, true), RunFixtures.Normal(2, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();
            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

            var buff = h.Offer.Doors.FirstOrDefault(d => !CurrencyDoor.IsCurrency(d.Choice.Kind));
            if (buff.DisplayName == null)
                Assert.Ignore("이 시드의 문 지점에 버프 문이 없다");

            h.Flow.ReportDoorPicked(buff.Choice, h.Offer.Token).Forget();
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
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);
            int doorToken = h.Offer.Token;

            var choice = h.Offer.Doors[0].Choice;
            h.Flow.ReportDoorPicked(choice, doorToken).Forget();
            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            int gold = h.Wallet.Gold.CurrentValue;
            int grantCalls = h.Rewards.GrantCalls;

            // 문 지점 화면의 늦은 콜백(같은 종류의 이전 스텝)이 다시 도착해도 무시된다.
            h.Flow.ReportDoorPicked(choice, doorToken).Forget();

            Assert.AreEqual(RunStep.EnteringRoom, h.Offer.Step);
            Assert.AreEqual(gold, h.Wallet.Gold.CurrentValue);
            Assert.AreEqual(grantCalls, h.Rewards.GrantCalls);
        }

        [Test]
        public void 종료_커밋은_두_번_불려도_지급과_클리어_기록이_한_번이다()
        {
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();

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
            var h = Build(RunFixtures.Chapter(RunFixtures.Normal(1, true), RunFixtures.Normal(2, false), RunFixtures.Boss()));
            h.Flow.BeginRun().Forget();

            h.Flow.ReportBattleResult(won: true, h.Offer.Token).Forget();
            Assert.AreEqual(RunStep.DoorPoint, h.Offer.Step);

            // 골드 문을 골라 보류시킨 채 다음 방에서 패배한다 — 보류분은 지급되지 않아야 한다.
            var goldDoor = h.Offer.Doors.FirstOrDefault(d => d.Choice.Kind == DoorKind.Gold);
            var picked = goldDoor.DisplayName != null ? goldDoor.Choice : h.Offer.Doors[0].Choice;
            h.Flow.ReportDoorPicked(picked, h.Offer.Token).Forget();
            h.Flow.ReportBattleResult(won: false, h.Offer.Token).Forget();

            Assert.AreEqual(RunStep.RunFail, h.Offer.Step);
            Assert.IsFalse(h.Session.HasEscrow, "종료 커밋 첫 단계에서 몰수됐다");
            Assert.AreEqual(2, h.Rewards.GrantCalls, "종료 지급은 적립분·정산 2회 고정");
            Assert.AreEqual(100, h.Wallet.Gold.CurrentValue - 1000, "지갑 증가분 = 정산(방 1)뿐 — 몰수분은 적립되지 않았다");
        }
    }
}