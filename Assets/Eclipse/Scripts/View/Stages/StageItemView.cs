using System;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 스테이지 목록의 항목 하나를 그리는 View. 아이템 ViewModel의 불변값(썸네일·이름·번호·보스)을 1회 대입하고,
    /// 3상태(클리어/열림/잠김)는 <see cref="StageSelectItemViewModel.State"/>를 구독해 반영한다.
    /// 항목은 StageSelectView가 생성하고 Bind를 호출해 연결한다.
    /// </summary>
    public class StageItemView : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button selectButton;

        [Tooltip("스테이지 번호 뱃지(\"01\" 형식). 등급 뱃지 텍스트 노드를 재활용한다.")]
        [SerializeField] private TMP_Text numberText;

        [Tooltip("클리어 완료 마크. State가 Cleared일 때만 켠다.")]
        [SerializeField] private GameObject clearMark;

        [Tooltip("잠금 오버레이(dim, 회색화 아님). State가 Locked일 때만 켜고 버튼을 막는다.")]
        [SerializeField] private GameObject lockOverlay;

        [Tooltip("보스 스테이지 프레임/라벨. StageSO.isBoss일 때만 켠다.")]
        [SerializeField] private GameObject bossFrame;

        [Tooltip("선택 강조 표시(아웃라인 등). SetSelected로 켜고 끈다.")]
        [SerializeField] private GameObject selectedIndicator;

        /// <summary>
        /// 항목을 스테이지 아이템에 바인딩한다. 불변값은 즉시 대입하고 3상태만 구독한다.
        /// 잠금 차단의 최종 판정은 ViewModel이 담당하며 버튼 비활성화는 어포던스일 뿐이다.
        /// 구독은 GameObject 수명에 묶여 Destroy 시 해지된다. 항목당 한 번만 호출한다(재바인딩 미지원).
        /// </summary>
        public void Bind(StageSelectItemViewModel item, Action<StageSelectItemViewModel> onSelected)
        {
            thumbnail.sprite = item.Stage.thumbnail;
            nameText.text = item.Stage.displayName;
            if (numberText != null)
                numberText.text = item.StageNumber.ToString("D2");
            if (bossFrame != null)
                bossFrame.SetActive(item.Stage.isBoss);

            selectButton.onClick.AddListener(() => onSelected(item));
            SetSelected(false);

            item.State
                .Subscribe(ApplyState)
                .AddTo(this);
        }

        // 3상태를 시각/상호작용에 반영한다. Locked는 오버레이 표시 + 버튼 비활성(어포던스), Cleared는 클리어 마크.
        private void ApplyState(StageState state)
        {
            if (clearMark != null)
                clearMark.SetActive(state == StageState.Cleared);
            if (lockOverlay != null)
                lockOverlay.SetActive(state == StageState.Locked);
            selectButton.interactable = state != StageState.Locked;
        }

        /// <summary>선택 강조 표시를 켜거나 끈다.</summary>
        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null)
                selectedIndicator.SetActive(selected);
        }
    }
}
