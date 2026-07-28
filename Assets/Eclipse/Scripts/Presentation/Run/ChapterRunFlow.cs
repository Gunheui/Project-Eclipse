using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Service;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary> 런 진행 스텝. 화면은 이 값이 아니라 Offer를 보고 그린다. </summary>
    public enum RunStep { EnteringRoom, InBattle, BuffPick, DoorPoint, RunClear, RunFail }

    /// <summary> 문 지점 선택지 하나의 표시 데이터. View가 도메인을 만지지 않도록 문구·그림까지 풀어서 담는다. </summary>
    public readonly struct DoorOption
    {
        public DoorOption(DoorChoice choice, string displayName, string promise, Sprite icon)
        {
            Choice = choice;
            DisplayName = displayName;
            Promise = promise;
            Icon = icon;
        }

        /// <summary> 이 선택지가 확정될 때 그대로 보고되는 값. </summary>
        public DoorChoice Choice { get; }

        public string DisplayName { get; }

        /// <summary> 문에 적히는 약속. 금액은 적지 않는다. </summary>
        public string Promise { get; }

        /// <summary> 문에 거는 그림. 캐릭터 문은 그 파티원의 초상, 나머지는 카탈로그 아이콘이다. </summary>
        public Sprite Icon { get; }
    }

    /// <summary> 3택1 후보 하나의 표시 데이터. </summary>
    public readonly struct CardOption
    {
        public CardOption(BuffCard card)
        {
            Card = card;
        }

        public BuffCard Card { get; }
    }

    /// <summary>
    /// 화면에 건네는 스텝 페이로드. 스텝 종류에 따라 쓰는 필드가 다르고, Token이 보고의 유효성 기준이다.
    /// </summary>
    public sealed class RunOffer
    {
        public RunStep Step;
        public int Token;

        /// <summary> EnteringRoom: 이번 방 배치 행. </summary>
        public RoomLayout Room;

        /// <summary> EnteringRoom: 이번 방 인카운터. </summary>
        public EncounterSpec Encounter;

        /// <summary> EnteringRoom: 이번 방 전투 시드(런 시드에서 방 인덱스로 파생). </summary>
        public int BattleSeed;

        /// <summary> 터미널: 이번에 지급된 보상 영수증. </summary>
        public IReadOnlyList<RewardEntry> Receipts;

        /// <summary> 터미널: 런 중 적립 재화 누계. 이번 종료에서 정산과 함께 지급된 블록이다. </summary>
        public IReadOnlyList<RewardEntry> RunIncome;

        /// <summary> 직전 방에서 적립이 끝난 재화. 화면이 드랍 연출로 공개한다. 없으면 null. </summary>
        public IReadOnlyList<RewardEntry> RoomDrops;

        /// <summary> BuffPick: 후보 3장. </summary>
        public IReadOnlyList<CardOption> Cards;

        /// <summary> BuffPick: 배정 화면용 파티 슬롯 정의(빈칸 null). 인덱스가 곧 배정 슬롯이다. </summary>
        public IReadOnlyList<CharacterSO> PartySlots;

        /// <summary> BuffPick: 배정 슬롯이 이미 정해진 픽이면 그 슬롯, 아니면 -1(사용자가 고른다). </summary>
        public int BuffTargetPartySlot = DoorChoice.NoPartySlot;

        /// <summary> DoorPoint: 추첨된 문 3개의 표시 데이터. </summary>
        public IReadOnlyList<DoorOption> Doors;

        /// <summary> 진행도 표시용 방 번호(1부터). </summary>
        public int RoomNumber;

        /// <summary> 진행도 표시용 챕터 전체 방 수. </summary>
        public int RoomCount;

        /// <summary> 터미널: 승리 여부. </summary>
        public bool Victory;
    }

    /// <summary>
    /// 런 진행 상태기계 — 방 전진·전투 생성 요청·에스크로 해소/몰수·적립·정산·저장·씬 이탈의 유일한 권위.
    /// 화면은 <see cref="Offer"/>를 구독해 그리고, 결과를 Report*로 보고만 한다. 토큰이 다른 보고는
    /// 조용히 무시된다(중복 탭·늦은 애니 콜백 방어).
    /// </summary>
    public sealed class ChapterRunFlow : IDisposable
    {
        private readonly ChapterRunSession _session;
        private readonly EncounterGenerator _generator;
        private readonly DoorDraw _doorDraw;
        private readonly CardPool _cardPool;
        private readonly IRunRandom _currencyRng;
        private readonly DoorCatalogSO _doorCatalog;
        private readonly IRewardService _rewards;
        private readonly ChapterProgress _progress;
        private readonly SaveService _saveService;
        private readonly ISceneFlow _sceneFlow;

        private readonly ReactiveProperty<RunOffer> _offer = new(null);

        // 이번 방 결과에서 아직 처리하지 않은 3택1 목록(에스크로 버프 문 + 미드보스 버프 문).
        private readonly Queue<PendingBuffPick> _pendingPicks = new();

        // 이번 방에서 적립했지만 아직 화면에 알리지 않은 재화. 다음 Emit이 RoomDrops로 옮기고 비운다.
        private IReadOnlyList<RewardEntry> _pendingDrops;

        private bool _committed;

        /// <summary> 아직 제시하지 않은 3택1 하나 = 후보 카드 + 배정 대상 슬롯(-1이면 사용자가 고른다). </summary>
        private readonly struct PendingBuffPick
        {
            public PendingBuffPick(IReadOnlyList<BuffCard> cards, int targetPartySlot)
            {
                Cards = cards;
                TargetPartySlot = targetPartySlot;
            }

            public IReadOnlyList<BuffCard> Cards { get; }
            public int TargetPartySlot { get; }
        }

        public ChapterRunFlow(ChapterRunSession session, EncounterGenerator generator, DoorDraw doorDraw,
            CardPool cardPool, IRunRandom currencyRng, DoorCatalogSO doorCatalog, IRewardService rewards,
            ChapterProgress progress, SaveService saveService, ISceneFlow sceneFlow)
        {
            _session = session;
            _generator = generator;
            _doorDraw = doorDraw;
            _cardPool = cardPool;
            _currencyRng = currencyRng;
            _doorCatalog = doorCatalog;
            _rewards = rewards;
            _progress = progress;
            _saveService = saveService;
            _sceneFlow = sceneFlow;
        }

        /// <summary> 현재 스텝. </summary>
        public RunStep Current { get; private set; }

        /// <summary> 전이 카운터. 페이로드와 함께 화면에 건너가고, 되돌아온 값이 다르면 보고를 무시한다. </summary>
        public int StepToken { get; private set; }

        /// <summary> 화면이 구독하는 현재 페이로드. BeginRun 전에는 null. </summary>
        public ReadOnlyReactiveProperty<RunOffer> Offer => _offer;

        /// <summary> 첫 방을 제시하며 런을 시작한다. </summary>
        /// <exception cref="InvalidOperationException">파티 4칸이 다 차 있지 않을 때.</exception>
        public UniTask BeginRun()
        {
            // 문 라인업이 캐릭터 문 4개를 전제하므로 빈칸이 있으면 여기서 끊는다.
            if (_session.Party.Count != PlayerSave.PartySlotCount || _session.Party.Any(o => o == null))
                throw new InvalidOperationException(
                    $"런은 파티 {PlayerSave.PartySlotCount}칸이 다 차야 시작할 수 있다.");

            OfferRoom();
            return UniTask.CompletedTask;
        }

        /// <summary> 전투 종료 보고(승패만). 승리는 보상 공개로, 패배는 런 종료 커밋으로 이어진다. </summary>
        public UniTask ReportBattleResult(bool won, int token)
        {
            if (token != StepToken || Current != RunStep.InBattle)
                return UniTask.CompletedTask;

            if (!won)
            {
                CommitTerminal(victory: false);
                return UniTask.CompletedTask;
            }
            RevealRewards();
            return UniTask.CompletedTask;
        }

        /// <summary> 정산 팝업의 [확인] 보고. 여기서 로비로 돌아간다. </summary>
        public async UniTask ReportResultConfirmed(int token)
        {
            if (token != StepToken)
                return;
            if (Current != RunStep.RunClear && Current != RunStep.RunFail)
                return;

            StepToken++; // 재보고 차단 — 복귀는 1회다(정산 팝업 확인 더블 탭 방어)
            await _sceneFlow.ToMainAsync();
        }

        /// <summary>
        /// 3택1 배정 결과 보고. 대상이 이미 정해진 픽(캐릭터 문)은 보고된 슬롯을 무시하고 그 대상에 붙인다.
        /// 남은 픽이 있으면 다음 픽, 없으면 문 지점/전진으로 넘어간다.
        /// </summary>
        public UniTask ReportCardAssigned(BuffCard card, int partySlot, int token)
        {
            if (token != StepToken || Current != RunStep.BuffPick)
                return UniTask.CompletedTask;

            int forced = _offer.Value.BuffTargetPartySlot;
            if (forced >= 0)
                partySlot = forced;
            else if (!card.targetsEnemies && !string.IsNullOrEmpty(card.requiredCharacterId))
                partySlot = SlotOf(card.requiredCharacterId);
            _session.AttachCard(card, partySlot);
            ProceedAfterReveal();
            return UniTask.CompletedTask;
        }

        /// <summary> 파티에서 캐릭터의 슬롯을 찾는다. </summary>
        private int SlotOf(string characterId)
        {
            for (int i = 0; i < _session.Party.Count; i++)
                if (_session.Party[i] != null && _session.Party[i].Definition.id == characterId)
                    return i;
            // 전용 카드는 파티 조건을 통과해 후보에 들었으므로 반드시 있다.
            throw new InvalidOperationException($"전용 카드 대상 '{characterId}'이 파티에 없다.");
        }

        /// <summary> 문 선택 보고. 보류분으로 기록만 하고(지연 지급) 다음 방으로 전진한다. </summary>
        public UniTask ReportDoorPicked(DoorChoice choice, int token)
        {
            if (token != StepToken || Current != RunStep.DoorPoint)
                return UniTask.CompletedTask;

            // 제시하지 않은 문은 받지 않는다. 토큰만으로는 화면이 만들어 낸 값을 거를 수 없다.
            if (_offer.Value.Doors == null || _offer.Value.Doors.All(d => d.Choice != choice))
                return UniTask.CompletedTask;

            _session.HoldEscrow(choice);
            _session.AdvanceRoom();
            OfferRoom();
            return UniTask.CompletedTask;
        }

        public void Dispose() => _offer.Dispose();

        /// <summary>
        /// 현재 방을 인카운터·시드와 함께 제시하고 전투 대기 상태로 들어간다.
        /// </summary>
        private void OfferRoom()
        {
            var room = _session.CurrentRoom;
            var encounter = room.kind == RoomKind.Boss
                ? _generator.Generate(EncounterGenerator.BossDepth, false)
                : _generator.Generate(room.depth, room.kind == RoomKind.Elite);

            Current = RunStep.InBattle;
            Emit(new RunOffer
            {
                Step = RunStep.EnteringRoom,
                Room = room,
                Encounter = encounter,
                BattleSeed = RunSeed.ForRoomBattle(_session.RunSeed, _session.RoomIndex),
            });
        }

        /// <summary>
        /// 승리 직후 직전 에스크로와 미드보스 보상 2종을 공개한다.
        /// 장부 적립까지 여기서 끝내고, 공개 연출은 다음 제시물의 <see cref="RunOffer.RoomDrops"/>에 맡긴다 —
        /// 연출이 끊겨도 재화가 새지 않는 순서다. 지갑 반영은 런 종료 시 한 번이다(<see cref="CommitTerminal"/>).
        /// </summary>
        private void RevealRewards()
        {
            var receipts = new List<RewardEntry>();

            // 에스크로는 적립 전에 소진한다 — 적립 후에 비우면 중복 보고가 같은 보상을 두 번 태울 수 있다.
            if (_session.HasEscrow)
            {
                var door = _session.ClaimEscrow();
                RevealDoor(door.Choice, door.Depth, receipts);
            }

            // 미드보스 보상: 문 2개 비복원 즉시 처리(이미 이긴 위험이라 지연시키지 않는다).
            if (_session.CurrentRoom.kind == RoomKind.Elite)
                foreach (var choice in _doorDraw.DrawDistinct(2))
                    RevealDoor(choice, Math.Max(1, _session.DoorPointsPassed), receipts);

            if (receipts.Count > 0)
            {
                _session.RecordIncome(receipts);
                _pendingDrops = receipts;
            }

            ProceedAfterReveal();
        }

        /// <summary>
        /// 문 하나를 공개 처리한다. 재화 문은 여기서 금액이 굴려져 적립되고, 버프 문은 3택1 대기열에 쌓인다.
        /// </summary>
        /// <param name="receipts"> 재화 문 적립분을 여기에 덧붙인다. </param>
        private void RevealDoor(DoorChoice choice, int depth, List<RewardEntry> receipts)
        {
            if (CurrencyDoor.IsCurrency(choice.Kind))
            {
                receipts.Add(CurrencyDoor.Roll(choice.Kind, depth, _session.Chapter.currencyMultiplier,
                    _doorCatalog, _currencyRng));
            }
            else
            {
                _pendingPicks.Enqueue(new PendingBuffPick(
                    _cardPool.Pick3(choice, depth, _session.Party), choice.TargetPartySlot));
            }
        }

        /// <summary>
        /// 공개 확인/카드 배정 후의 공통 진행: 남은 픽 → 문 지점 → 전진(마지막 방이면 런 클리어).
        /// </summary>
        private void ProceedAfterReveal()
        {
            if (_pendingPicks.Count > 0)
            {
                var pick = _pendingPicks.Dequeue();
                Current = RunStep.BuffPick;
                Emit(new RunOffer
                {
                    Step = RunStep.BuffPick,
                    Cards = pick.Cards.Select(c => new CardOption(c)).ToList(),
                    PartySlots = _session.Party.Select(o => o?.Definition).ToList(),
                    BuffTargetPartySlot = pick.TargetPartySlot,
                });
                return;
            }

            // 문 판정은 커서 전진보다 먼저다 — 전진 후에 읽으면 다음 방의 플래그를 보게 되고,
            // 보스 방 승리 뒤에는 rooms 범위를 넘는다. 깬 방의 플래그로 문을 열고, 그다음 전진한다.
            bool doorAfter = _session.CurrentRoom.doorAfter;
            if (doorAfter)
            {
                Current = RunStep.DoorPoint;
                Emit(new RunOffer { Step = RunStep.DoorPoint, Doors = BuildDoorOptions(_doorDraw.DrawDistinct(3)) });
                return;
            }

            _session.AdvanceRoom();
            if (_session.RoomIndex >= _session.Chapter.rooms.Length)
            {
                CommitTerminal(victory: true);
                return;
            }
            OfferRoom();
        }

        /// <summary>
        /// 런 종료 커밋. 런 중 적립분이 지갑에 닿는 유일한 지점이며, 종료 스텝이 두 번 불려도
        /// 지급과 저장은 정확히 1회다.
        /// </summary>
        private void CommitTerminal(bool victory)
        {
            // committed는 어떤 대기보다 먼저 세운다.
            if (_committed)
                return;
            _committed = true;

            // 순서 고정: ①몰수 ②정산 계산 ③(승리만)클리어 기록 ④적립분·정산 지급 ⑤저장 1회 ⑥정산 팝업 제시.
            _session.ForfeitEscrow();
            _pendingPicks.Clear();
            var entries = RunSettlement.EntriesFor(_session.Chapter, _session.RoomIndex, victory);
            if (victory)
                _progress.MarkCleared(_session.Chapter);
            // 적립분과 정산을 따로 지급한다 — 정산 팝업이 둘을 별개 블록으로 보여 주기 때문이다.
            var income = _rewards.Grant(_session.RunIncome);
            var receipts = _rewards.Grant(entries);
            _saveService?.Save();

            Current = victory ? RunStep.RunClear : RunStep.RunFail;
            Emit(new RunOffer
            {
                Step = Current,
                Receipts = receipts,
                RunIncome = income,
                Victory = victory,
            });
        }

        /// <summary>
        /// 뽑힌 문을 표시 데이터로 바꾼다. 표시명·약속 문구는 추첨과 같은 카탈로그에서 나오고,
        /// 캐릭터 문만 대상 파티원의 이름·초상으로 채워진다.
        /// </summary>
        private IReadOnlyList<DoorOption> BuildDoorOptions(IReadOnlyList<DoorChoice> choices)
            => choices.Select(choice =>
            {
                var definition = _doorCatalog.doors.First(d => d.kind == choice.Kind);
                if (!choice.IsCharacterDoor)
                    return new DoorOption(choice, definition.displayName, definition.promiseText, definition.icon);

                var character = _session.Party[choice.TargetPartySlot].Definition;
                return new DoorOption(choice,
                    string.Format(definition.displayName, character.displayName),
                    string.Format(definition.promiseText, character.displayName),
                    character.portraitAssetRef);
            }).ToList();

        /// <summary>
        /// 토큰을 올리며 페이로드를 내보낸다. 전이의 유일한 출구라 토큰과 페이로드가 항상 함께 움직인다.
        /// </summary>
        private void Emit(RunOffer offer)
        {
            StepToken++;
            offer.Token = StepToken;
            offer.RoomDrops = _pendingDrops;
            _pendingDrops = null;
            offer.RoomCount = _session.Chapter.rooms.Length;
            // 마지막 방을 넘긴 뒤(정산)에도 커서가 방 수를 넘지 않게 잘라 표시한다.
            offer.RoomNumber = Math.Min(_session.RoomIndex + 1, offer.RoomCount);
            _offer.Value = offer;
        }
    }
}