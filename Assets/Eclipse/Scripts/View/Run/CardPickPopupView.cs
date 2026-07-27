using System.Collections.Generic;
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
    /// <summary> 3택1 팝업이 돌려주는 선택 = 고른 카드 + 배정 슬롯. </summary>
    public readonly struct CardPickChoice
    {
        public CardPickChoice(BuffCard card, int slot)
        {
            Card = card;
            Slot = slot;
        }

        public BuffCard Card { get; }

        /// <summary> 배정 슬롯. 자동 배정 카드(저주·인연)는 0이 들어가고 Flow가 바로잡는다. </summary>
        public int Slot { get; }
    }

    /// <summary>
    /// 버프 카드 3택1 + 배정 팝업. 카드 선택 후 배정 슬롯을 고르면 완료된다.
    /// 인연·저주 카드는 배정 화면 없이 즉시 완료된다(배정은 Flow가 정한다). 닫기 없는 강제 선택이다.
    /// </summary>
    public class CardPickPopupView : MonoBehaviour, IPopup<CardPickChoice>
    {
        [Header("카드 3택1")]
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private TMP_Text[] cardNames;
        [SerializeField] private TMP_Text[] cardEffects;
        [SerializeField] private TMP_Text[] cardOdds;

        [Header("배정")]
        [SerializeField] private GameObject assignSection;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMP_Text[] slotNames;

        private IReadOnlyList<CharacterSO> _partySlots;
        private BuffCard _picked;

        private readonly UniTaskCompletionSource<CardPickChoice> _choice = new();

        /// <summary> 고른 카드와 배정 슬롯. </summary>
        public UniTask<CardPickChoice> Result => _choice.Task;

        [Inject]
        public void Construct(ChapterRunFlow flow)
        {
            var offer = flow.Offer.CurrentValue;
            _partySlots = offer.PartySlots;
            var candidates = offer.Cards;

            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (candidates == null || i >= candidates.Count)
                {
                    cardButtons[i].gameObject.SetActive(false);
                    continue;
                }

                var option = candidates[i];
                if (cardNames != null && i < cardNames.Length)
                    cardNames[i].text = option.Card.displayName;
                if (cardEffects != null && i < cardEffects.Length)
                    cardEffects[i].text = RunTexts.FormatCard(option.Card);
                if (cardOdds != null && i < cardOdds.Length)
                    cardOdds[i].text = $"등장 확률 {option.Odds * 100f:0.#}%";

                cardButtons[i].onClick.AddListener(() => OnCardPicked(option.Card));
            }

            if (assignSection != null)
                assignSection.SetActive(false);
        }

        // 카드 확정 → 자동 배정 카드(저주·인연)는 즉시 완료, 그 외에는 배정 슬롯 선택으로 넘어간다.
        private void OnCardPicked(BuffCard card)
        {
            _picked = card;

            if (card.targetsEnemies || !string.IsNullOrEmpty(card.requiredCharacterId))
            {
                _choice.TrySetResult(new CardPickChoice(card, 0));
                return;
            }
            ShowAssignSection();
        }

        private void ShowAssignSection()
        {
            foreach (var button in cardButtons)
                button.interactable = false;
            if (assignSection != null)
                assignSection.SetActive(true);

            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                bool filled = _partySlots != null && slot < _partySlots.Count && _partySlots[slot] != null;
                slotButtons[i].gameObject.SetActive(filled);
                if (!filled) continue;

                if (slotNames != null && i < slotNames.Length)
                    slotNames[i].text = _partySlots[slot].displayName;
                slotButtons[i].onClick.AddListener(() => _choice.TrySetResult(new CardPickChoice(_picked, slot)));
            }
        }

        /// <summary>팝업을 띄운다. 등장 연출이 없어 즉시 완료된다.</summary>
        public UniTask Open() => UniTask.CompletedTask;

        /// <summary>팝업을 닫는다. 파괴는 PopupManager가 한다.</summary>
        public UniTask Close() => UniTask.CompletedTask;
    }
}