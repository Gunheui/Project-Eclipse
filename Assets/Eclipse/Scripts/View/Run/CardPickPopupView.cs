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

        /// <summary> 배정 슬롯. 배정을 사용자가 고르지 않는 픽은 0이 들어가고 Flow가 바로잡는다. </summary>
        public int Slot { get; }
    }

    /// <summary>
    /// 버프 카드 3택1 + 배정 팝업. 카드 선택 후 배정 슬롯을 고르면 완료된다.
    /// 대상이 이미 정해진 픽(캐릭터 문·저주·전용 카드)은 배정 화면 없이 즉시 완료된다(배정은 Flow가 정한다).
    /// 닫기 없는 강제 선택이다.
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
        private int _forcedSlot;
        private BuffCard _picked;

        private readonly UniTaskCompletionSource<CardPickChoice> _choice = new();

        /// <summary> 고른 카드와 배정 슬롯. </summary>
        public UniTask<CardPickChoice> Result => _choice.Task;

        [Inject]
        public void Construct(ChapterRunFlow flow)
        {
            var offer = flow.Offer.CurrentValue;
            _partySlots = offer.PartySlots;
            _forcedSlot = offer.BuffTargetPartySlot;
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
                // 확률 공시 폐지. 프리팹 교체 전까지는 텍스트 오브젝트만 꺼 둔다.
                if (cardOdds != null && i < cardOdds.Length)
                    cardOdds[i].gameObject.SetActive(false);

                cardButtons[i].onClick.AddListener(() => OnCardPicked(option.Card));
            }

            if (assignSection != null)
                assignSection.SetActive(false);
        }

        /// <summary>카드 확정. 배정이 이미 정해진 픽은 즉시 완료하고, 그 외에는 배정 슬롯 선택으로 넘어간다.</summary>
        private void OnCardPicked(BuffCard card)
        {
            _picked = card;

            if (_forcedSlot >= 0 || card.targetsEnemies || !string.IsNullOrEmpty(card.requiredCharacterId))
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