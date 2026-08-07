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

    /// <summary> 문의 격. 프레임 선택과 흐름 분기(에스크로·문 지점 깊이)가 같은 값을 본다. </summary>
    public enum DoorTier { Promise, MidBoss, FinalBoss }

    /// <summary> 문 지점 선택지 하나의 표시 데이터. View가 도메인을 만지지 않도록 그림까지 풀어서 담는다. </summary>
    public readonly struct DoorOption
    {
        /// <exception cref="ArgumentException">티어가 정한 보상 수(일반 1·미드보스 2·보스 0)와 어긋날 때.</exception>
        public DoorOption(DoorTier tier, IReadOnlyList<DoorChoice> rewards, Sprite icon, bool flipIcon,
            IReadOnlyList<Sprite> rewardIcons)
        {
            int expected = tier switch
            {
                DoorTier.MidBoss => 2,
                DoorTier.FinalBoss => 0,
                _ => 1,
            };
            if (rewards.Count != expected)
                throw new ArgumentException(
                    $"{tier} 문에는 보상 {expected}종이 걸려야 하는데 {rewards.Count}종이 걸렸다.", nameof(rewards));

            Tier = tier;
            Rewards = rewards;
            Icon = icon;
            FlipIcon = flipIcon;
            RewardIcons = rewardIcons;
        }

        public DoorTier Tier { get; }

        /// <summary> 이 문에 걸린 보상. 순서 = 화면 좌→우이자 해소 순서다. </summary>
        public IReadOnlyList<DoorChoice> Rewards { get; }

        public bool IsMidBoss => Tier == DoorTier.MidBoss;

        /// <summary> 거울에 거는 그림. 캐릭터 문은 얼굴 초상, 미드보스 문은 없음(null). </summary>
        public Sprite Icon { get; }

        /// <summary> 그림을 좌우 반전할지. 얼굴은 문 안쪽을 보도록 반전해 건다. </summary>
        public bool FlipIcon { get; }

        /// <summary> 걸린 보상의 심볼. 미드보스 문만 2개를 싣고 나머지는 비어 있다. </summary>
        public IReadOnlyList<Sprite> RewardIcons { get; }
    }

    /// <summary> 3택1 후보 하나의 표시 데이터. View가 도메인을 만지지 않도록 문구까지 풀어서 담는다. </summary>
    public readonly struct CardOption
    {
        public CardOption(BuffCard card, string effect, string gradeLabel, string target)
        {
            Card = card;
            Effect = effect;
            GradeLabel = gradeLabel;
            Target = target;
        }

        /// <summary> 이 후보가 확정될 때 그대로 보고되는 값. </summary>
        public BuffCard Card { get; }

        public string DisplayName => Card.displayName;

        /// <summary> 등급색을 고르는 축. 배지 문구와 짝을 이룬다. </summary>
        public CardGrade Grade => Card.grade;

        /// <summary> 효과 한 줄. </summary>
        public string Effect { get; }

        /// <summary> 등급 배지에 적히는 등급명. </summary>
        public string GradeLabel { get; }

        /// <summary> 효과가 붙는 대상. 캐릭터 문은 그 파티원 이름, 저주 문은 적 전체다. </summary>
        public string Target { get; }
    }

    /// <summary>
    /// 화면에 건네는 스텝 페이로드. 스텝 종류에 따라 쓰는 필드가 다르고, Token이 보고의 유효성 기준이다.
    /// </summary>
    public sealed class RunOffer
    {
        public RunStep Step;
        public int Token;

        /// <summary> EnteringRoom: 이번 방 배경. </summary>
        public Sprite Background;

        /// <summary> EnteringRoom: 이번 방 인카운터. </summary>
        public EncounterSpec Encounter;

        /// <summary> EnteringRoom: 이번 방 전투 시드(런 시드에서 방 인덱스로 파생). </summary>
        public int BattleSeed;

        /// <summary> EnteringRoom: 정예 미드보스 전투인지. 정예 표기는 방 종류가 아니라 이 값을 읽는다. </summary>
        public bool IsEliteEncounter;

        /// <summary> EnteringRoom: 이번 방 종류. 전투 화면이 적 자리표를 고르는 기준이다. </summary>
        public RoomKind Kind;

        /// <summary> 터미널: 문으로 번 재화. 런 내내 장부에만 쌓였다가 이번 종료에 지급된 블록이다. </summary>
        public IReadOnlyList<RewardEntry> ExploreReward;

        /// <summary> 터미널: 넘긴 방 수로 받은 정산. </summary>
        public IReadOnlyList<RewardEntry> DepthReward;

        /// <summary> 터미널: 클리어에만 붙는 보너스. 실패면 빈 목록. </summary>
        public IReadOnlyList<RewardEntry> VictoryBonus;

        /// <summary> 터미널: 위 세 블록의 합. 화면이 보여 주는 행만 더한 값이라 플레이어가 검산할 수 있다. </summary>
        public IReadOnlyList<RewardEntry> RewardTotal;

        /// <summary> 직전 방에서 적립이 끝난 재화. 화면이 드랍 연출로 공개한다. 없으면 null. </summary>
        public IReadOnlyList<RewardEntry> RoomDrops;

        /// <summary> BuffPick: 후보 3장. </summary>
        public IReadOnlyList<CardOption> Cards;

        /// <summary> DoorPoint: 제시된 문의 표시 데이터. 추첨 지점은 3개, 보스 직전 지점은 보스 문 1개다. </summary>
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

        // 제시 중인 3택1이 붙을 슬롯. 저주 문은 대상이 없어 NoPartySlot이다.
        private int _pickTargetSlot = DoorChoice.NoPartySlot;

        private bool _committed;

        /// <summary> 아직 제시하지 않은 3택1 하나 = 후보 카드 + 붙일 대상 슬롯(저주 문은 -1). </summary>
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
                CommitRunEnd(victory: false);
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
        /// 3택1 선택 보고. 배정 대상은 이 픽을 낸 문이 이미 정했으므로 화면이 고르지 않는다.
        /// 남은 픽이 있으면 다음 픽, 없으면 문 지점/전진으로 넘어간다.
        /// </summary>
        public UniTask ReportCardPicked(BuffCard card, int token)
        {
            if (token != StepToken || Current != RunStep.BuffPick)
                return UniTask.CompletedTask;

            // 제시하지 않은 카드는 받지 않는다. 토큰만으로는 화면이 만들어 낸 값을 거를 수 없다.
            var matched = _offer.Value.Cards?
                .Where(o => o.Card.id == card.id)
                .Select(o => (BuffCard?)o.Card)
                .FirstOrDefault();
            if (matched == null)
                return UniTask.CompletedTask;

            // 보고된 값이 아니라 제시한 카드를 붙인다 — id가 같아도 증감 수치는 다를 수 있다.
            _session.AttachCard(matched.Value, _pickTargetSlot);
            ProceedAfterReveal();
            return UniTask.CompletedTask;
        }

        /// <summary> 문 선택 보고. 보류분으로 기록만 하고(지연 지급) 다음 방으로 전진한다. </summary>
        /// <param name="optionIndex">제시된 문 목록에서의 자리. 범위 밖이면 무시한다.</param>
        public UniTask ReportDoorPicked(int optionIndex, int token)
        {
            if (token != StepToken || Current != RunStep.DoorPoint)
                return UniTask.CompletedTask;

            // 제시한 자리만 받는다. 보상은 화면이 보낸 값이 아니라 제시물에서 그대로 꺼낸다.
            var doors = _offer.Value.Doors;
            if (doors == null || optionIndex < 0 || optionIndex >= doors.Count)
                return UniTask.CompletedTask;

            var picked = doors[optionIndex];
            // 보스 문은 보상이 없어 보류할 것이 없다 — 에스크로도 문 지점 깊이도 건드리지 않고 전진만 한다.
            if (picked.Tier != DoorTier.FinalBoss)
                _session.HoldEscrow(picked.Rewards, picked.IsMidBoss);
            _session.AdvanceRoom();
            OfferRoom();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 런 포기. 적립분도 정산도 지급하지 않고 정산 화면 없이 로비로 돌아간다. 종료 커밋과 달리
        /// 저장도 클리어 기록도 건드리지 않는다. 어느 스텝에서 불러도 성립한다.
        /// </summary>
        public async UniTask AbandonRun()
        {
            // 커밋 가드는 어떤 대기보다 먼저 세운다. 정산이 이미 섰으면 포기가 끼어들지 않는다.
            if (_committed)
                return;
            _committed = true;

            // 화면에 나가 있는 보고를 무효화한다. 커밋 가드를 보지 않는 3택1 보고까지 여기서 걸린다.
            StepToken++;
            _session.ForfeitEscrow();
            _pendingPicks.Clear();
            await _sceneFlow.ToMainAsync();
        }

        public void Dispose() => _offer.Dispose();

        /// <summary>
        /// 현재 방을 인카운터·시드와 함께 제시하고 전투 대기 상태로 들어간다.
        /// </summary>
        private void OfferRoom()
        {
            var room = _session.CurrentRoom;

            // 배치의 Elite는 정예 후보 자리일 뿐이다. 실제 정예 여부는 문③에서 미드보스 문을 골랐는지가 정한다.
            bool isElite = room.kind == RoomKind.Elite && _session.MidBossEngaged;
            var encounter = room.kind == RoomKind.Boss
                ? _generator.Generate(EncounterGenerator.BossDepth, isEliteEncounter: false)
                : _generator.Generate(room.depth, isElite);

            Current = RunStep.InBattle;
            Emit(new RunOffer
            {
                Step = RunStep.EnteringRoom,
                Background = BackgroundFor(room.kind, isElite),
                Encounter = encounter,
                BattleSeed = RunSeed.ForRoomBattle(_session.RunSeed, _session.RoomIndex),
                IsEliteEncounter = isElite,
                Kind = room.kind,
            });
        }

        private Sprite BackgroundFor(RoomKind kind, bool isElite)
        {
            if (kind == RoomKind.Boss)
                return _session.Chapter.bossBackground;
            return isElite ? _session.Chapter.eliteBackground : _session.Chapter.normalBackground;
        }

        /// <summary>
        /// 승리 직후 직전 에스크로를 공개한다. 미드보스 문이면 걸린 2종이 좌→우로 해소된다.
        /// 장부 적립까지 여기서 끝내고, 공개 연출은 다음 제시물의 <see cref="RunOffer.RoomDrops"/>에 맡긴다 —
        /// 연출이 끊겨도 재화가 새지 않는 순서다. 지갑 반영은 런 종료 시 한 번이다(<see cref="CommitRunEnd"/>).
        /// </summary>
        private void RevealRewards()
        {
            var receipts = new List<RewardEntry>();

            // 에스크로는 적립 전에 소진한다 — 적립 후에 비우면 중복 보고가 같은 보상을 두 번 태울 수 있다.
            if (_session.HasEscrow)
            {
                var door = _session.ClaimEscrow();
                foreach (var choice in door.Choices)
                    RevealDoor(choice, door.Depth, receipts);
            }

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
        /// <param name="choice"> 공개할 문. 종류가 재화/버프 분기를 가른다. </param>
        /// <param name="depth"> 이 문을 고른 방의 깊이. 재화 금액 굴림에 들어간다. </param>
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
                    _cardPool.Pick3(choice, _session.Party, OwnedCardIds()), choice.TargetPartySlot));
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
                _pickTargetSlot = pick.TargetPartySlot;
                Current = RunStep.BuffPick;
                Emit(new RunOffer { Step = RunStep.BuffPick, Cards = BuildCardOptions(pick) });
                return;
            }

            // 문 판정은 커서 전진보다 먼저다 — 전진 후에 읽으면 다음 방의 플래그를 보게 되고,
            // 보스 방 승리 뒤에는 rooms 범위를 넘는다. 깬 방의 플래그로 문을 열고, 그다음 전진한다.
            bool doorAfter = _session.CurrentRoom.doorAfter;
            if (doorAfter)
            {
                Current = RunStep.DoorPoint;
                Emit(new RunOffer
                {
                    Step = RunStep.DoorPoint,
                    Doors = _session.NextRoomIsBoss()
                        ? new[] { FinalBossDoor() }
                        : BuildDoorOptions(_doorDraw.DrawDoorPoint(_session.NextRoomIsEliteCandidate())),
                });
                return;
            }

            _session.AdvanceRoom();
            if (_session.RoomIndex >= _session.Chapter.rooms.Length)
            {
                CommitRunEnd(victory: true);
                return;
            }
            OfferRoom();
        }

        /// <summary>
        /// 런 종료 커밋. 런 중 적립분이 지갑에 닿는 유일한 지점이며, 종료 스텝이 두 번 불려도
        /// 지급과 저장은 정확히 1회다.
        /// </summary>
        private void CommitRunEnd(bool victory)
        {
            // committed는 어떤 대기보다 먼저 세운다.
            if (_committed)
                return;
            _committed = true;

            // 순서 고정: ①몰수 ②정산 계산 ③(승리만)클리어 기록 ④세 블록 지급 ⑤저장 1회 ⑥정산 팝업 제시.
            _session.ForfeitEscrow();
            _pendingPicks.Clear();
            var settlement = RunSettlement.EntriesFor(_session.Chapter, _session.RoomIndex, victory);
            if (victory)
                _progress.MarkCleared(_session.Chapter);
            // 세 번 나눠 지급한다 — 한 번에 넘기면 재화별로 뭉쳐 나와 화면의 세 행을 채울 수 없다.
            var explore = _rewards.Grant(_session.RunIncome);
            var depth = _rewards.Grant(settlement.Depth);
            var bonus = _rewards.Grant(settlement.VictoryBonus);
            _saveService?.Save();

            Current = victory ? RunStep.RunClear : RunStep.RunFail;
            Emit(new RunOffer
            {
                Step = Current,
                ExploreReward = explore,
                DepthReward = depth,
                VictoryBonus = bonus,
                // 합계는 여기서 낸다 — 탐험 보상은 세션 장부 소관이라 도메인 정산이 모르는 값이다.
                RewardTotal = RunRewardService.Sum(explore.Concat(depth).Concat(bonus)),
                Victory = victory,
            });
        }

        /// <summary>
        /// 뽑힌 지점을 표시 데이터로 바꾼다. 보상 2종을 건 자리만 미드보스 문이 된다.
        /// </summary>
        private IReadOnlyList<DoorOption> BuildDoorOptions(IReadOnlyList<IReadOnlyList<DoorChoice>> doors)
            => doors.Select(rewards => rewards.Count == 1 ? PromiseDoor(rewards[0]) : MidBossDoor(rewards)).ToList();

        /// <summary>
        /// 일반 약속 문 하나. 재화·저주 문은 카탈로그 아이콘, 캐릭터 문은 대상 파티원의 얼굴 초상이 거울에 걸린다.
        /// </summary>
        private DoorOption PromiseDoor(DoorChoice choice)
            => new DoorOption(DoorTier.Promise, new[] { choice }, IconOf(choice),
                flipIcon: choice.IsCharacterDoor, Array.Empty<Sprite>());

        /// <summary>
        /// 미드보스 문 하나. 거울에 그림 없이 걸린 보상 2종의 심볼만 해소 순서대로 싣는다.
        /// </summary>
        private DoorOption MidBossDoor(IReadOnlyList<DoorChoice> rewards)
            => new DoorOption(DoorTier.MidBoss, rewards, icon: null, flipIcon: false,
                rewards.Select(IconOf).ToList());

        /// <summary>
        /// 보상 하나의 그림. 캐릭터 보상은 대상 파티원의 얼굴 초상, 나머지는 카탈로그 아이콘이다.
        /// 일반 문의 거울 그림과 미드보스 문의 심볼이 같은 규칙을 쓴다.
        /// </summary>
        private Sprite IconOf(DoorChoice choice)
            => choice.IsCharacterDoor
                ? _session.Party[choice.TargetPartySlot].Definition.faceIconAssetRef
                : _doorCatalog.doors.First(d => d.kind == choice.Kind).icon;

        /// <summary>
        /// 최종보스 문 하나. 보상 없이 보스 얼굴만 걸린다. 추첨 밖에서 만들어 카탈로그를 거치지 않는다.
        /// </summary>
        private DoorOption FinalBossDoor()
            => new DoorOption(DoorTier.FinalBoss, Array.Empty<DoorChoice>(),
                _session.Chapter.bossFace, flipIcon: true, Array.Empty<Sprite>());

        /// <summary> 추첨에 넘길 보유 카드 id. 유니크 1장 상한 배제의 입력이다. </summary>
        private IReadOnlyList<string> OwnedCardIds()
            => _session.AcquiredCards.Select(c => c.Card.id).ToList();

        /// <summary>
        /// 뽑힌 카드를 표시 데이터로 바꾼다. 카드명·효과·등급 라벨은 카드에서 나오고,
        /// 귀속 표시만 이 픽을 낸 문이 정한다.
        /// </summary>
        private IReadOnlyList<CardOption> BuildCardOptions(PendingBuffPick pick)
        {
            // 저주 문은 대상 슬롯이 없다. 세 장이 한 문에서 나오므로 귀속 표시도 세 장이 공유한다.
            string target = pick.TargetPartySlot >= 0
                ? _session.Party[pick.TargetPartySlot].Definition.displayName
                : RunTexts.EnemyTarget;
            return pick.Cards
                .Select(c => new CardOption(c, RunTexts.FormatCard(c), RunTexts.GradeLabel(c.grade), target))
                .ToList();
        }

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