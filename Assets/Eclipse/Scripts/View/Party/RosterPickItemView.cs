using System;
using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 픽 화면 로스터 항목 하나를 그리는 View. 표시값(초상·이름·등급·레벨)과 탭은 공용 셀
    /// <see cref="CharacterItemView"/>에 위임하고, 이 화면 고유의 슬롯 번호 배지·편성 강조만
    /// <see cref="PartyPickItemViewModel.SlotNumber"/>를 구독해 갱신한다.
    /// 항목은 PartyPickView가 생성하고 Bind를 호출해 연결한다.
    /// </summary>
    public class RosterPickItemView : MonoBehaviour
    {
        [Tooltip("표시값(초상·이름·등급·레벨)과 탭 버튼을 담당하는 공용 셀.")]
        [SerializeField] private CharacterItemView baseItem;

        [Header("편성 상태")]
        [Tooltip("슬롯 번호 배지 루트. 편성돼 있을 때만 켜고 슬롯 번호(1~4)를 표시한다.")]
        [SerializeField] private GameObject orderBadge;
        [SerializeField] private TMP_Text orderText;
        [Tooltip("편성 강조(아웃라인 등). 편성돼 있을 때만 켠다.")]
        [SerializeField] private GameObject selectHighlight;

        /// <summary>
        /// 항목을 지정 아이템에 바인딩한다. 표시값·탭은 공용 셀에 위임하고, 슬롯 번호는 구독해 배지·강조를 갱신한다.
        /// 구독은 이 GameObject 수명에 묶여 Destroy 시 자동 해지된다. 탭은 <paramref name="onPick"/>으로 전달한다.
        /// 항목당 한 번만 호출한다(재바인딩 미지원 — 구독이 중첩된다).
        /// </summary>
        /// <param name="item">이 항목이 표시할 픽 아이템(공유 항목 VM + 슬롯 번호 상태).</param>
        /// <param name="onPick">항목을 탭했을 때 호출되는 콜백(슬롯 배치는 픽 ViewModel이 담당).</param>
        public void Bind(PartyPickItemViewModel item, Action onPick)
        {
            baseItem.Bind(item.Character, onPick);

            item.SlotNumber
                .Subscribe(ApplySlotNumber)
                .AddTo(this);
        }

        // 점유 슬롯 번호를 배지·강조에 반영한다. 0이면 미편성(배지/강조 끔), 1~4면 그 숫자를 배지에 표시.
        private void ApplySlotNumber(int slotNumber)
        {
            bool assigned = slotNumber > 0;
            if (orderBadge != null)
                orderBadge.SetActive(assigned);
            if (selectHighlight != null)
                selectHighlight.SetActive(assigned);
            if (assigned && orderText != null)
                orderText.text = slotNumber.ToString();
        }
    }
}
