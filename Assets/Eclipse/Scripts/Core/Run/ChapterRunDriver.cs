using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// BattleScene의 런 구동 글루. <see cref="ChapterRunFlow"/>의 제시물을 구독해 전투 조립·배경 스왑·
    /// 문 지점 표시·팝업 표시를 실행하고, 사용자 선택을 토큰과 함께 Flow에 보고만 한다 —
    /// 진행 판단은 전부 Flow 소관이다.
    /// </summary>
    public class ChapterRunDriver : MonoBehaviour
    {
        [SerializeField] private BattleView battleView;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private RoomTransitionFader fader;
        [SerializeField] private WorldDoorPointView doorPoint;
        [SerializeField] private CurrencyDropSpawner dropSpawner;
        [SerializeField] private TMP_Text roomProgressLabel;
        [SerializeField] private bool startAuto;

        private ChapterRunFlow _flow;
        private BattleFactory _factory;
        private PopupManager _popups;

        private BattleViewModel _battle;
        private CancellationTokenSource _battleCts;
        private int _battleToken;

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
        /// 제시물 하나를 처리한다. 직전 방에서 받은 재화가 있으면 드랍 연출을 먼저 끝내고 스텝을 그린다 —
        /// 이 await가 곧 방 전환 게이트다.
        /// </summary>
        private async UniTaskVoid HandleOfferAsync(RunOffer offer)
        {
            if (roomProgressLabel != null)
                roomProgressLabel.text = $"방 {offer.RoomNumber}/{offer.RoomCount}";

            // 전장 정리(ClearBattle) 전에 읽어야 적 좌표가 살아 있다.
            if (offer.RoomDrops != null && dropSpawner != null)
            {
                // 연출 중에는 나가기를 잠근다. 전투가 이미 끝나 포기 보고가 무시되므로,
                // 눌러도 아무 일이 없는 버튼을 살려 두지 않는다.
                battleView.SetExitEnabled(false);
                try
                {
                    await dropSpawner.PlayAsync(offer.RoomDrops, battleView.EnemyPositions(),
                        this.GetCancellationTokenOnDestroy());
                }
                finally
                {
                    // 씬이 내려가는 중이면 전투 뷰가 먼저 파괴돼 있을 수 있다. 파괴 순서는 보장되지 않는다.
                    if (battleView != null) battleView.SetExitEnabled(true);
                }
            }

            switch (offer.Step)
            {
                case RunStep.EnteringRoom: await EnterRoomAsync(offer); break;
                case RunStep.BuffPick: await ShowCardPickAsync(offer); break;
                case RunStep.DoorPoint: await ShowDoorAsync(offer); break;
                case RunStep.RunClear:
                case RunStep.RunFail: await ShowSettlementAsync(offer); break;
            }
        }

        /// <summary> 방에 진입해 전투를 구동하고 승패를 보고한다. </summary>
        private async UniTask EnterRoomAsync(RunOffer offer)
        {
            _battleToken = offer.Token;
            await fader.FadeOutAsync();

            // 페이드 아웃 뒤 이전 전투 파기·문 정리·배경 스왑·재조립.
            battleView.ClearBattle();
            doorPoint.Clear();
            _battle?.Dispose();
            if (backgroundRenderer != null && offer.Room.background != null)
                backgroundRenderer.sprite = offer.Room.background;

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
                return; // 포기·씬 파괴 — 보고는 취소한 쪽(OnExitRequested)이 이미 처리했다
            }

            bool won = _battle.Result.CurrentValue == BattleResult.Victory;
            await _flow.ReportBattleResult(won, offer.Token);
        }

        /// <summary>
        /// 나가기 = 런 포기. 이 방 패배로 보고해 몰수·정산·복귀가 정규 실패 경로를 그대로 거친다.
        /// </summary>
        private void OnExitRequested()
        {
            _battleCts?.Cancel();
            _flow.ReportBattleResult(false, _battleToken).Forget();
        }

        private async UniTask ShowCardPickAsync(RunOffer offer)
        {
            var pick = await _popups.Show<CardPickChoice>(PopupId.CardPick);
            await _flow.ReportCardAssigned(pick.Card, pick.Slot, offer.Token);
        }

        private async UniTask ShowDoorAsync(RunOffer offer)
        {
            DoorChoice choice;
            try
            {
                choice = await doorPoint.ShowAsync(offer.Doors);
            }
            catch (OperationCanceledException)
            {
                return; // 문이 선택 전에 내려갔다(씬 파괴·다음 제시로 교체) — 보고할 선택이 없다
            }
            await _flow.ReportDoorPicked(choice, offer.Token);
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