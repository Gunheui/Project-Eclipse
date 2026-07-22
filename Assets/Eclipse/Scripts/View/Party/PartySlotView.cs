using System;
using Eclipse.Data;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 편성 화면의 슬롯 하나를 그리는 View. 슬롯의 ReactiveProperty(OwnedCharacter)를 구독해 빈칸/채움 상태를
    /// 전환하고, 채움일 때 초상·이름을 대입한다. 슬롯을 탭하면 픽 화면 진입 콜백을 호출한다.
    /// 항목은 PartyFormationView가 생성하고 Bind를 호출해 연결한다.
    /// </summary>
    public class PartySlotView : MonoBehaviour
    {
        [Header("상태 루트")]
        [Tooltip("빈 슬롯 표시(+ 아이콘/점선 테두리). 슬롯이 비었을 때만 켠다.")]
        [SerializeField] private GameObject emptyState;
        [Tooltip("채움 표시(초상/이름 등). 슬롯이 채워졌을 때만 켠다.")]
        [SerializeField] private GameObject filledState;

        [Header("채움 내용")]
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;

        [SerializeField] private Button selectButton;

        /// <summary>
        /// 슬롯을 점유자 스트림에 바인딩한다. 구독은 GameObject 수명에 묶여 Destroy 시 자동 해지된다.
        /// 슬롯당 한 번만 호출한다(재바인딩 시 구독이 중첩된다).
        /// </summary>
        /// <param name="occupant">점유자 스트림. null 값이면 빈칸, 있으면 채움.</param>
        /// <param name="onSelected">슬롯을 탭했을 때 호출된다(픽 화면 진입은 편성 View가 담당).</param>
        public void Bind(ReadOnlyReactiveProperty<CharacterSO> occupant, Action onSelected)
        {
            occupant.Subscribe(ApplyOccupant).AddTo(this);
            selectButton.onClick.AddListener(() => onSelected());
        }

        // 슬롯 점유자를 빈칸/채움 시각에 반영한다. 초상은 정의의 스프라이트를 직접 대입한다(비동기 로딩 불필요).
        private void ApplyOccupant(CharacterSO def)
        {
            bool filled = def != null;
            if (emptyState != null)
                emptyState.SetActive(!filled);
            if (filledState != null)
                filledState.SetActive(filled);

            if (!filled)
                return;
            if (portrait != null)
                portrait.sprite = def.portraitAssetRef;
            if (nameText != null)
                nameText.text = def.displayName;
        }
    }
}
