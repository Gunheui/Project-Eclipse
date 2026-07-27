using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 런 정산 팝업. ①런 중 획득(이미 지급된 장부) ②도달 정산(지금 지급) 2블록을 보여 주고
    /// [확인]으로 닫힌다. 지급·저장은 팝업이 뜨기 전에 이미 끝나 있다.
    /// </summary>
    public class RunSettlementPopupView : MonoBehaviour, IPopup<bool>
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text runIncomeText;
        [SerializeField] private TMP_Text settlementText;
        [SerializeField] private Button confirmButton;

        private readonly UniTaskCompletionSource<bool> _choice = new();

        /// <summary> 확인 응답. 값 자체는 의미 없고 닫힘 신호다. </summary>
        public UniTask<bool> Result => _choice.Task;

        [Inject]
        public void Construct(ChapterRunFlow flow)
        {
            var offer = flow.Offer.CurrentValue;

            if (titleText != null)
                titleText.text = offer.Victory ? "챕터 클리어" : "런 실패";
            if (runIncomeText != null)
                runIncomeText.text = "런 중 획득\n" + FormatEntries(offer.RunIncome);
            if (settlementText != null)
                settlementText.text = "도달 정산\n" + FormatEntries(offer.Receipts);

            confirmButton.onClick.AddListener(() => _choice.TrySetResult(true));
        }

        private static string FormatEntries(IReadOnlyList<RewardEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return "획득 없음";
            return string.Join("\n", entries.Select(e => $"{RunTexts.CurrencyName(e.type)} +{e.amount:N0}"));
        }

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;
    }
}