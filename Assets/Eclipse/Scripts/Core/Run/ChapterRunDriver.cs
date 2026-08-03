using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Domain;
using Eclipse.Presentation;
using Eclipse.View;
using Eclipse.View.Infra;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace Eclipse.Core
{
    /// <summary>
    /// BattleScene과 <see cref="ChapterRunFlow"/> 사이의 중계자. Flow가 내보낸 제시물대로 전투를 생성함,
    /// 배경 변경, 다음 문 지점 표시, 팝업 표시를 실행하고, 사용자 선택을 토큰과 함께 Flow에 돌려준다.
    /// 다음에 뭘 할지는 여기서 판단하지 않는다.
    /// </summary>
    public class ChapterRunDriver : MonoBehaviour
    {
        [SerializeField] private BattleView battleView;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private RoomTransitionFader fader;
        [SerializeField] private WorldDoorPointView doorPoint;
        [SerializeField] private CurrencyDropSpawner dropSpawner;
        [SerializeField] private TMP_Text roomProgressLabel;
        [SerializeField] private GameObject eliteBadge;
        [SerializeField] private bool startAuto;

        private ChapterRunFlow _flow;
        private BattleFactory _factory;
        private PopupManager _popups;

        private BattleViewModel _battle;
        private CancellationTokenSource _battleCts;
        private bool _exitPending;

        [Inject]
        public void Construct(ChapterRunFlow flow, BattleFactory factory, PopupManager popups)
        {
            _flow = flow;
            _factory = factory;
            _popups = popups;
        }

        private void Start()
        {
            battleView.ExitRequested += OnExitRequested;
            _flow.Offer.Subscribe(OnOffer).AddTo(this);
            _flow.BeginRun().Forget();
        }

        private void OnOffer(RunOffer offer)
        {
            if (offer == null) return;
            HandleOfferAsync(offer).Forget();
        }

        /// <summary>
        /// 제시물 하나를 화면에 그린다. 스텝 종류와 무관하게 모든 제시물이 이 입구를 지난다.
        /// </summary>
        private async UniTaskVoid HandleOfferAsync(RunOffer offer)
        {
            if (roomProgressLabel != null)
                roomProgressLabel.text = $"방 {offer.RoomNumber}/{offer.RoomCount}";

            // 정예는 방 진입 제시물에만 실린다. 다른 스텝은 방이 끝났다는 뜻이라 함께 내린다.
            if (eliteBadge != null)
                eliteBadge.SetActive(offer.Step == RunStep.EnteringRoom && offer.IsEliteEncounter);

            // 터미널부터 런은 이미 끝났다. 이 뒤로 포기는 커밋 가드에 막혀 무반응이 되므로 나가기를 내린다.
            // 남는 출구는 정산 화면의 [로비로] 하나다.
            if (offer.Step == RunStep.RunClear || offer.Step == RunStep.RunFail)
                battleView.SetExitEnabled(false);

            // 스텝 처리(ClearBattle) 전에 재생해야 적 좌표에 재화를 표시할 수 있음.
            await PlayRoomDropsAsync(offer);

            switch (offer.Step)
            {
                case RunStep.EnteringRoom: await EnterRoomAsync(offer); break;
                case RunStep.BuffPick: await ShowCardPickAsync(offer); break;
                case RunStep.DoorPoint: await ShowDoorAsync(offer); break;
                case RunStep.RunClear:
                case RunStep.RunFail: await ShowSettlementAsync(offer); break;
                case RunStep.InBattle:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// 직전 방에서 적립된 재화가 있으면 애니메이션 연출을 진행핸다.
        /// </summary>
        private async UniTask PlayRoomDropsAsync(RunOffer offer)
        {
            if (offer.RoomDrops == null || dropSpawner == null) return;

            await dropSpawner.PlayAsync(offer.RoomDrops, battleView.EnemyPositions(),
                this.GetCancellationTokenOnDestroy());
        }

        /// <summary> 방에 진입해 전투를 구동하고 승패를 보고한다. </summary>
        private async UniTask EnterRoomAsync(RunOffer offer)
        {
            await fader.FadeOutAsync();

            // 페이드 아웃 뒤 이전 전투 파기·문 정리·배경 스왑·재조립.
            battleView.ClearBattle();
            doorPoint.Clear();
            _battle?.Dispose();
            if (backgroundRenderer != null && offer.Background != null)
                backgroundRenderer.sprite = offer.Background;

            _battle = _factory.Create(offer.Encounter, offer.BattleSeed, startAuto);
            battleView.Bind(_battle);
            await fader.FadeInAsync();

            _battleCts?.Dispose();
            _battleCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            try
            {
                await battleView.RunBoundBattleAsync(_battleCts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // 포기·씬 파괴 — 이 전투 결과는 보고하지 않는다
            }

            bool won = _battle.Result.CurrentValue == BattleResult.Victory;
            await _flow.ReportBattleResult(won, offer.Token);
        }

        private void OnExitRequested() => ConfirmAbandonAsync().Forget();

        /// <summary>
        /// 나가기 = 런 포기. 한 번 물어 확정되면 전투를 끊고 Flow의 포기 경로로 넘긴다.
        /// 전투 중·문 지점·3택1 어디서 눌러도 같다.
        /// </summary>
        private async UniTaskVoid ConfirmAbandonAsync()
        {
            // 확인 팝업은 한 번만 뜬다. 확정한 뒤에는 다시 열지 않으므로 되돌리지 않는다.
            if (_exitPending) return;
            _exitPending = true;
            battleView.SetExitEnabled(false);

            // 오토 전투는 팝업을 모르고 계속 돈다. 묻는 사이 방이 끝나면 정규 커밋이 먼저 서서
            // 포기했는데 정산을 받게 된다. 전투 연출은 전부 DOTween이고 unscaled 지정이 없다.
            float timeScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                bool abandon = await _popups.ShowConfirm(RunTexts.AbandonTitle, RunTexts.AbandonBody);

                // 복원은 씬 전환보다 앞이다. 시간 배율은 앱 전역 값이라 한 번 어긋나면 로비까지 끌고 간다.
                Time.timeScale = timeScale;
                if (!abandon)
                {
                    battleView.SetExitEnabled(true);
                    _exitPending = false;
                    return;
                }

                _battleCts?.Cancel();
                await _flow.AbandonRun();
            }
            finally
            {
                // 예외·취소로 위 복원을 못 지나갔을 때의 보루.
                Time.timeScale = timeScale;
            }
        }

        private async UniTask ShowCardPickAsync(RunOffer offer)
        {
            var card = await _popups.Show<BuffCard>(PopupId.CardPick);
            await _flow.ReportCardPicked(card, offer.Token);
        }

        private async UniTask ShowDoorAsync(RunOffer offer)
        {
            int picked;
            try
            {
                picked = await doorPoint.ShowAsync(offer.Doors);
            }
            catch (OperationCanceledException)
            {
                return; // 문이 선택 전에 내려갔다(씬 파괴·다음 제시로 교체) — 보고할 선택이 없다
            }
            await _flow.ReportDoorPicked(picked, offer.Token);
        }

        private async UniTask ShowSettlementAsync(RunOffer offer)
        {
            await _popups.Show<bool>(PopupId.RunSettlement);
            await _flow.ReportResultConfirmed(offer.Token);
        }

        private void OnDestroy()
        {
            battleView.ExitRequested -= OnExitRequested;
            _battleCts?.Cancel();
            _battleCts?.Dispose();
            _battle?.Dispose();
        }
    }
}