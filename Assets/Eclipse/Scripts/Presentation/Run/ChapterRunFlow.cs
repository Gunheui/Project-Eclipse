using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Service;
using R3;

namespace Eclipse.Presentation
{
    /// <summary> 런 진행 스텝. 화면은 이 값이 아니라 Offer를 보고 그린다. </summary>
    public enum RunStep { EnteringRoom, InBattle, BuffPick, DoorPoint, RunClear, RunFail }

    /// <summary> 문 지점 선택지 하나의 표시 데이터. View가 도메인을 만지지 않도록 문구까지 풀어서 담는다. </summary>
    public readonly struct DoorOption
    {
        public DoorOption(DoorKind kind, string displayName, string promise)
        {
            Kind = kind;
            DisplayName = displayName;
            Promise = promise;
        }

        public DoorKind Kind { get; }
        public string DisplayName { get; }

        /// <summary> 문에 적히는 약속. 금액은 적지 않는다. </summary>
        public string Promise { get; }
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

        /// <summary> 터미널: 런 중 획득 재화 누계(이미 지급된 장부 블록). </summary>
        public IReadOnlyList<RewardEntry> RunIncome;

        /// <summary> BuffPick: 후보 3장. </summary>
        public IReadOnlyList<CardOption> Cards;

        /// <summary> BuffPick: 배정 화면용 파티 슬롯 정의(빈칸 null). 인덱스가 곧 배정 슬롯이다. </summary>
        public IReadOnlyList<CharacterSO> PartySlots;

        /// <summary> DoorPoint: 추첨된 문 3종의 표시 데이터. </summary>
        public IReadOnlyList<DoorOption> Doors;

        /// <summary> 터미널: 승리 여부. </summary>
        public bool Victory;
    }

    /// <summary>
    /// 런 진행 상태기계 — 방 전진·전투 생성 요청·에스크로 지급/몰수·정산·저장·씬 이탈의 유일한 권위.
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
        private readonly Queue<IReadOnlyList<BuffCard>> _pendingPicks = new();

        private bool _committed;

        // 파티 조건으로 제시 불가한 문(인연 후보 3장 미만). 런 중 편성이 불변이라 런 시작 시 1회 판정한다.
        private DoorKind? _excludedDoor;

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
        public UniTask BeginRun()
        {
            _excludedDoor = _cardPool.CanOfferBond(_session.Party) ? (DoorKind?)null : DoorKind.Bond;
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
        /// 3택1 배정 결과 보고. 인연 카드는 슬롯을 대상 캐릭터로 바로잡는다(자동 배정).
        /// 남은 픽이 있으면 다음 픽, 없으면 문 지점/전진으로 넘어간다.
        /// </summary>
        public UniTask ReportCardAssigned(BuffCard card, int partySlot, int token)
        {
            if (token != StepToken || Current != RunStep.BuffPick)
                return UniTask.CompletedTask;

            if (!card.targetsEnemies && !string.IsNullOrEmpty(card.requiredCharacterId))
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
            // 인연 카드는 파티 조건을 통과해 후보에 들었으므로 반드시 있다.
            throw new InvalidOperationException($"인연 카드 대상 '{characterId}'이 파티에 없다.");
        }

        /// <summary> 문 선택 보고. 보류분으로 기록만 하고(지연 지급) 다음 방으로 전진한다. </summary>
        public UniTask ReportDoorPicked(DoorKind kind, int token)
        {
            if (token != StepToken || Current != RunStep.DoorPoint)
                return UniTask.CompletedTask;

            _session.HoldEscrow(kind);
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
        /// 공개 결과를 보여 주는 화면은 없다. 처리만 하고 곧장 다음 진행으로 넘어간다.
        /// </summary>
        private void RevealRewards()
        {
            var receipts = new List<RewardEntry>();

            // 에스크로는 지급 전에 소진한다 — 지급 후에 비우면 중복 보고가 같은 보상을 두 번 태울 수 있다.
            if (_session.HasEscrow)
            {
                var door = _session.ClaimEscrow();
                RevealDoor(door.Kind, door.Depth, receipts);
            }

            // 미드보스 보상: 문 2종 비복원 즉시 처리(이미 이긴 위험이라 지연시키지 않는다).
            if (_session.CurrentRoom.kind == RoomKind.Elite)
                foreach (var kind in _doorDraw.DrawDistinct(2, _excludedDoor))
                    RevealDoor(kind, Math.Max(1, _session.DoorPointsPassed), receipts);

            if (receipts.Count > 0)
                _session.RecordIncome(receipts);

            ProceedAfterReveal();
        }

        /// <summary>
        /// 문 하나를 공개 처리한다. 재화 문은 여기서 롤·지급되고, 버프 문은 3택1 대기열에 쌓인다.
        /// </summary>
        /// <param name="receipts"> 재화 문 지급 영수증을 여기에 덧붙인다. </param>
        private void RevealDoor(DoorKind kind, int depth, List<RewardEntry> receipts)
        {
            if (CurrencyDoor.IsCurrency(kind))
            {
                var rolled = CurrencyDoor.Roll(kind, depth, _session.Chapter.currencyMultiplier,
                    _doorCatalog, _currencyRng);
                receipts.AddRange(_rewards.Grant(new[] { rolled }));
            }
            else
            {
                _pendingPicks.Enqueue(_cardPool.Pick3(kind, depth, _session.Party));
            }
        }

        /// <summary>
        /// 공개 확인/카드 배정 후의 공통 진행: 남은 픽 → 문 지점 → 전진(마지막 방이면 런 클리어).
        /// </summary>
        private void ProceedAfterReveal()
        {
            if (_pendingPicks.Count > 0)
            {
                var cards = _pendingPicks.Dequeue();
                Current = RunStep.BuffPick;
                Emit(new RunOffer
                {
                    Step = RunStep.BuffPick,
                    Cards = cards.Select(c => new CardOption(c)).ToList(),
                    PartySlots = _session.Party.Select(o => o?.Definition).ToList(),
                });
                return;
            }

            // 문 판정은 커서 전진보다 먼저다 — 전진 후에 읽으면 다음 방의 플래그를 보게 되고,
            // 보스 방 승리 뒤에는 rooms 범위를 넘는다. 깬 방의 플래그로 문을 열고, 그다음 전진한다.
            bool doorAfter = _session.CurrentRoom.doorAfter;
            if (doorAfter)
            {
                Current = RunStep.DoorPoint;
                Emit(new RunOffer { Step = RunStep.DoorPoint, Doors = BuildDoorOptions(_doorDraw.DrawDistinct(3, _excludedDoor)) });
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
        /// 런 종료 커밋. 종료 스텝이 두 번 불려도 지급과 저장은 정확히 1회다.
        /// </summary>
        private void CommitTerminal(bool victory)
        {
            // committed는 어떤 대기보다 먼저 세운다.
            if (_committed)
                return;
            _committed = true;

            // 순서 고정: ①몰수 ②정산 계산 ③(승리만)클리어 기록 ④지급 1회 ⑤저장 1회 ⑥정산 팝업 제시.
            _session.ForfeitEscrow();
            _pendingPicks.Clear();
            var entries = RunSettlement.EntriesFor(_session.Chapter, _session.RoomIndex, victory);
            if (victory)
                _progress.MarkCleared(_session.Chapter);
            var receipts = _rewards.Grant(entries);
            _saveService?.Save();

            Current = victory ? RunStep.RunClear : RunStep.RunFail;
            Emit(new RunOffer
            {
                Step = Current,
                Receipts = receipts,
                RunIncome = _session.RunIncome,
                Victory = victory,
            });
        }

        /// <summary>
        /// 문 종류 목록을 표시 데이터로 바꾼다. 표시명·약속 문구는 추첨과 같은 카탈로그에서 나온다.
        /// </summary>
        private IReadOnlyList<DoorOption> BuildDoorOptions(IReadOnlyList<DoorKind> kinds)
            => kinds.Select(kind =>
            {
                var definition = _doorCatalog.doors.First(d => d.kind == kind);
                return new DoorOption(kind, definition.displayName, definition.promiseText);
            }).ToList();

        /// <summary>
        /// 토큰을 올리며 페이로드를 내보낸다. 전이의 유일한 출구라 토큰과 페이로드가 항상 함께 움직인다.
        /// </summary>
        private void Emit(RunOffer offer)
        {
            StepToken++;
            offer.Token = StepToken;
            _offer.Value = offer;
        }
    }
}