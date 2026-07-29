using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 챕터 결과 팝업. 탐험 보상·도달 보상·승리 보너스와 그 합계를 보여 주고 [확인]으로 닫힌다.
    /// 지급·저장은 팝업이 뜨기 전에 이미 끝나 있다.
    /// </summary>
    public class RunSettlementPopupView : MonoBehaviour, IPopup<bool>
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text exploreText;
        [SerializeField] private TMP_Text depthText;
        [SerializeField] private TMP_Text victoryBonusText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private Button confirmButton;

        [Header("변형별 표시")]
        [SerializeField] private GameObject victoryBonusRow;
        [SerializeField] private GameObject failNote;

        private readonly UniTaskCompletionSource<bool> _choice = new();

        /// <summary> 확인 응답. 값 자체는 의미 없고 닫힘 신호다. </summary>
        public UniTask<bool> Result => _choice.Task;

        [Inject]
        public void Construct(ChapterRunFlow flow)
        {
            var offer = flow.Offer.CurrentValue;

            titleText.text = offer.Victory ? "챕터 클리어" : "챕터 실패";
            exploreText.text = RunTexts.FormatRewards(offer.ExploreReward);
            depthText.text = RunTexts.FormatRewards(offer.DepthReward);
            victoryBonusText.text = RunTexts.FormatRewards(offer.VictoryBonus);
            totalText.text = RunTexts.FormatRewards(offer.RewardTotal);

            // 승리 보너스 행은 클리어에만 뜬다. 합계는 보이는 행의 합이어야 하므로 0행으로 남기지 않는다.
            victoryBonusRow.SetActive(offer.Victory);
            failNote.SetActive(!offer.Victory);

            confirmButton.onClick.AddListener(() => _choice.TrySetResult(true));
        }

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;
    }
}