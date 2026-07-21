using System;
using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using Eclipse.View.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 전투 결과 팝업의 View. 승/패 배너와 획득 보상 칩을 <see cref="ResultViewModel"/>에서 채우고,
    /// 두 버튼의 선택을 결과(true=재도전 / false=확인)로 돌려준다. 값은 열리는 시점에 고정이라 구독은 없다.
    /// dim(배경 차단)은 이 프리팹이 아니라 <see cref="PopupManager"/>가 소유한다.
    /// </summary>
    public class ResultPopupView : MonoBehaviour, IPopup<bool>
    {
        // 보상 칩 하나의 텍스트 두 칸. 아이콘·배경은 프리팹에 고정이라 바인딩 대상이 아니다.
        [Serializable]
        private struct RewardChip
        {
            public TMP_Text amount;
            public TMP_Text label;
        }

        [SerializeField] private UIThemeSO theme;

        [Header("Outcome")]
        [SerializeField] private Image outcomeBand;
        [SerializeField] private Image outcomeIcon;
        [SerializeField] private TMP_Text outcomeText;
        [SerializeField] private Sprite victoryIcon;
        [SerializeField] private Sprite defeatIcon;

        [Header("Rewards")]
        [SerializeField] private GameObject rewardRow;
        [SerializeField] private GameObject noRewardLabel;
        [SerializeField] private RewardChip[] rewardChips;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button confirmButton;

        private readonly UniTaskCompletionSource<bool> _choice = new UniTaskCompletionSource<bool>();

        /// <summary> 사용자가 고른 다음 행동. true=재도전(전투 재진입), false=확인(로비 복귀). </summary>
        public UniTask<bool> Result => _choice.Task;

        /// <summary>
        /// 팝업 내용을 뷰모델 값으로 채우고 버튼 클릭을 결과에 잇는다. PopupManager가 프리팹을 생성할 때
        /// 컨테이너가 호출하며, Awake보다 늦고 <see cref="Open"/>보다 앞선다.
        /// </summary>
        /// <param name="viewModel">이 전투의 확정된 결과·보상.</param>
        [Inject]
        public void Construct(ResultViewModel viewModel)
        {
            BindOutcome(viewModel.IsVictory);
            BindRewards(viewModel);

            retryButton.onClick.AddListener(() => _choice.TrySetResult(true));
            confirmButton.onClick.AddListener(() => _choice.TrySetResult(false));
        }

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 퇴장 연출이 없어 즉시 완료되며 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;

        private void BindOutcome(bool isVictory)
        {
            outcomeBand.color = isVictory ? theme.positiveSubtle : theme.dangerSubtle;
            outcomeIcon.sprite = isVictory ? victoryIcon : defeatIcon;
            outcomeText.text = isVictory ? "승 리" : "패 배";
            outcomeText.color = isVictory ? theme.onPositiveSubtle : theme.onDangerSubtle;
        }

        // 보상이 있으면 칩 행을, 없으면(패배) "획득 보상 없음" 한 줄을 띄운다.
        // 칩은 보상 종류와 1:1로 배선되므로(아이콘이 프리팹 고정) 짝이 맞는 앞에서부터만 채운다.
        private void BindRewards(ResultViewModel viewModel)
        {
            var rewards = viewModel.Rewards;
            rewardRow.SetActive(rewards.Count > 0);
            noRewardLabel.SetActive(rewards.Count == 0);

            for (int i = 0; i < rewardChips.Length && i < rewards.Count; i++)
            {
                rewardChips[i].amount.text = rewards[i].Amount.ToString("N0");
                rewardChips[i].label.text = rewards[i].Label;
            }
        }
    }
}
