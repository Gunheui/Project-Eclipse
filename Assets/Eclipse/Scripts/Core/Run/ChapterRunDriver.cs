using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using Eclipse.View;
using Eclipse.View.Infra;
using R3;
using UnityEngine;
using VContainer;

namespace Eclipse.Core
{
    /// <summary>
    /// BattleScene의 런 구동 글루. <see cref="ChapterRunFlow"/>의 제시물을 구독해 전투 조립·배경 스왑·
    /// 팝업 표시를 실행하고, 사용자 선택을 토큰과 함께 Flow에 보고만 한다 — 진행 판단은 전부 Flow 소관이다.
    /// </summary>
    public class ChapterRunDriver : MonoBehaviour
    {
        [SerializeField] private BattleView battleView;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private RoomTransitionFader fader;
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
            switch (offer.Step)
            {
                case RunStep.EnteringRoom: EnterRoomAsync(offer).Forget(); break;
                case RunStep.RevealReward: ShowResultAsync(offer).Forget(); break;
                case RunStep.BuffPick: ShowCardPickAsync(offer).Forget(); break;
                case RunStep.DoorPoint: ShowDoorAsync(offer).Forget(); break;
                case RunStep.RunClear:
                case RunStep.RunFail: ShowSettlementAsync(offer).Forget(); break;
            }
        }

        // 방 진입: 페이드 아웃 → 이전 전투 파기·배경 스왑·재조립 → 페이드 인 → 전투 구동 → 승패 보고.
        private async UniTaskVoid EnterRoomAsync(RunOffer offer)
        {
            _battleToken = offer.Token;
            await fader.FadeOutAsync();

            battleView.ClearBattle();
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

        // 나가기 = 런 포기. 이 방 패배로 보고해 몰수·정산·복귀가 정규 실패 경로를 그대로 탄다.
        private void OnExitRequested()
        {
            _battleCts?.Cancel();
            _flow.ReportBattleResult(false, _battleToken).Forget();
        }

        private async UniTaskVoid ShowResultAsync(RunOffer offer)
        {
            await _popups.Show<bool>(PopupId.BattleResult);
            await _flow.ReportResultConfirmed(offer.Token);
        }

        private async UniTaskVoid ShowCardPickAsync(RunOffer offer)
        {
            var pick = await _popups.Show<CardPickChoice>(PopupId.CardPick);
            await _flow.ReportCardAssigned(pick.Card, pick.Slot, offer.Token);
        }

        private async UniTaskVoid ShowDoorAsync(RunOffer offer)
        {
            var kind = await _popups.Show<Eclipse.Data.DoorKind>(PopupId.DoorPoint);
            await _flow.ReportDoorPicked(kind, offer.Token);
        }

        private async UniTaskVoid ShowSettlementAsync(RunOffer offer)
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