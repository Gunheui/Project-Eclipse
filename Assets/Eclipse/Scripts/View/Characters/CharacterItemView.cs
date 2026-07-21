using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 캐릭터 목록의 항목 하나를 그리는 View. CharacterItemViewModel의 값을 UI에 바인딩한다.
    /// 항목은 CharacterListView가 생성하고 Bind를 호출해 연결한다.
    /// </summary>
    public class CharacterItemView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Button selectButton;

        /// <summary>
        /// 항목을 지정 ViewModel에 바인딩한다. 안 바뀌는 값(초상·이름·등급)은 즉시 대입하고,
        /// 레벨은 <see cref="CharacterItemViewModel.Level"/>을 구독해 갱신한다.
        /// 항목 탭은 <paramref name="onSelected"/>로 전달한다.
        /// 레벨 구독은 이 GameObject 수명에 묶여 항목이 Destroy될 때 자동 해지된다.
        /// 항목당 한 번만 호출하는 것을 전제로 한다(재바인딩 미지원 — 구독이 중첩된다).
        /// </summary>
        /// <param name="viewModel">이 항목이 표시할 캐릭터 항목 ViewModel.</param>
        /// <param name="onSelected">항목을 탭했을 때 호출되는 콜백(선택 처리는 목록 View가 담당).</param>
        public void Bind(CharacterItemViewModel viewModel, Action onSelected)
        {
            ApplyPortraitAsync(viewModel, this.GetCancellationTokenOnDestroy()).Forget();
            nameText.text = viewModel.DisplayName;
            rarityText.text = $"★{viewModel.Rarity}";

            viewModel.Level
                .Subscribe(level => levelText.text = $"Lv. {level}")
                .AddTo(this);

            selectButton.onClick.AddListener(() => onSelected());
        }

        /// <summary>초상 스프라이트를 로드해 대입한다. 로드가 비동기라도 나머지 바인딩을 막지 않는다.</summary>
        private async UniTaskVoid ApplyPortraitAsync(CharacterItemViewModel viewModel, CancellationToken ct)
        {
            portrait.sprite = await viewModel.LoadPortraitAsync(ct);
        }
    }
}
