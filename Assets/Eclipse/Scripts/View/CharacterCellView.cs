using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 캐릭터 목록의 셀 하나를 그리는 View. CharacterCellViewModel의 값을 UI에 바인딩한다.
    /// 셀은 CharacterListView가 생성하고 Bind를 호출해 연결한다.
    /// </summary>
    public class CharacterCellView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text levelText;

        /// <summary>
        /// 셀을 지정 ViewModel에 바인딩한다. 안 바뀌는 값(초상·이름·등급)은 즉시 대입하고,
        /// 레벨은 <see cref="CharacterCellViewModel.Level"/>을 구독해 갱신한다.
        /// 레벨 구독은 이 GameObject 수명에 묶여 셀이 Destroy될 때 자동 해지된다.
        /// 셀당 한 번만 호출하는 것을 전제로 한다(재바인딩 미지원 — 구독이 중첩된다).
        /// </summary>
        /// <param name="viewModel">이 셀이 표시할 캐릭터 셀 ViewModel.</param>
        public void Bind(CharacterCellViewModel viewModel)
        {
            portrait.sprite = viewModel.Portrait;
            nameText.text = viewModel.DisplayName;
            rarityText.text = viewModel.Rarity.ToString();

            viewModel.Level
                .Subscribe(level => levelText.text = $"Lv. {level}")
                .AddTo(this);
        }
    }
}
