using Cysharp.Threading.Tasks;
using Eclipse.Data;
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
    /// 버프 카드 3택1 팝업. 카드를 고르는 순간 완료되고, 배정 대상은 화면이 정하지 않는다.
    /// 닫기·포기 없는 강제 1택이다.
    /// </summary>
    public class CardPickPopupView : MonoBehaviour, IPopup<BuffCard>
    {
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private TMP_Text[] cardNames;
        [SerializeField] private TMP_Text[] cardEffects;
        [SerializeField] private Image[] gradeBadges;
        [SerializeField] private TMP_Text[] gradeLabels;

        private readonly UniTaskCompletionSource<BuffCard> _choice = new();

        /// <summary> 고른 카드. 강제 1택이라 빈 결과가 없다. </summary>
        public UniTask<BuffCard> Result => _choice.Task;

        [Inject]
        public void Construct(ChapterRunFlow flow)
        {
            var candidates = flow.Offer.CurrentValue.Cards;

            // 대상은 세 장이 공유하므로 카드마다 적지 않고 제목 한 줄이 대표한다.
            if (titleLabel != null && candidates != null && candidates.Count > 0)
                titleLabel.text = RunTexts.CardPickTitle(candidates[0].Target);

            for (int i = 0; i < cardButtons.Length; i++)
            {
                // 후보보다 카드 칸이 많으면 남는 칸은 끈다.
                if (candidates == null || i >= candidates.Count)
                {
                    cardButtons[i].gameObject.SetActive(false);
                    continue;
                }

                var option = candidates[i];
                SetText(cardNames, i, option.DisplayName);
                SetText(cardEffects, i, option.Effect);
                SetText(gradeLabels, i, option.GradeLabel);
                if (gradeLabels != null && i < gradeLabels.Length)
                    gradeLabels[i].color = TextColorOf(option.Grade);
                if (gradeBadges != null && i < gradeBadges.Length)
                    gradeBadges[i].color = FillColorOf(option.Grade);

                cardButtons[i].onClick.AddListener(() => _choice.TrySetResult(option.Card));
            }
        }

        private static void SetText(TMP_Text[] labels, int index, string value)
        {
            if (labels != null && index < labels.Length && labels[index] != null)
                labels[index].text = value;
        }

        /// <summary> 등급 배지 채움색. </summary>
        private Color FillColorOf(CardGrade grade) => grade switch
        {
            CardGrade.Rare => theme.cardGradeRare,
            CardGrade.Epic => theme.cardGradeEpic,
            CardGrade.Unique => theme.cardGradeUnique,
            _ => theme.cardGradeCommon,
        };

        /// <summary> 등급명 텍스트색. 밝은 카드 표면 위에서 대비를 맞춘 어두운 변형이라 채움색과 다르다. </summary>
        private Color TextColorOf(CardGrade grade) => grade switch
        {
            CardGrade.Rare => theme.onCardGradeRare,
            CardGrade.Epic => theme.onCardGradeEpic,
            CardGrade.Unique => theme.onCardGradeUnique,
            _ => theme.onCardGradeCommon,
        };

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;
    }
}
