using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using Eclipse.View.Theme;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 편성 화면의 슬롯 하나를 그리는 View. 슬롯의 점유자 스트림을 구독해 빈칸/채움 상태를 전환하고,
    /// 채움일 때 초상·이름·등급 프레임·역할 아이콘·레벨을 대입한다. 슬롯을 탭하면 픽 화면 진입 콜백을 호출한다.
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
        [Tooltip("초상 뒤에 겹치는 이펙트 레이어. 초상과 같은 RectTransform 값을 쓴다.")]
        [SerializeField] private Image portraitFx;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image rarityFrame;
        [SerializeField] private Image roleIcon;

        [Header("슬롯 번호")]
        [SerializeField] private TMP_Text orderText;

        [Header("참조")]
        [Tooltip("등급 프레임 색과 역할 아이콘을 여기서 읽는다.")]
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private Button selectButton;

        private readonly SerialDisposable _levelBinding = new SerialDisposable();
        private CharacterViewModel _occupant;
        private float _portraitBaseY;

        private void Awake()
        {
            if (portrait != null)
                _portraitBaseY = portrait.rectTransform.anchoredPosition.y;
        }

        /// <summary>
        /// 슬롯을 점유자 스트림에 바인딩한다. 구독은 GameObject 수명에 묶여 Destroy 시 자동 해지된다.
        /// 슬롯당 한 번만 호출한다(재바인딩 시 구독이 중첩된다).
        /// </summary>
        /// <param name="occupant">점유자 스트림. null 값이면 빈칸, 있으면 채움.</param>
        /// <param name="slotNumber">배지에 찍을 슬롯 번호(1~4). 편성 중에 바뀌지 않는다.</param>
        /// <param name="onSelected">슬롯을 탭했을 때 호출된다(픽 화면 진입은 편성 View가 담당).</param>
        public void Bind(ReadOnlyReactiveProperty<CharacterViewModel> occupant, int slotNumber, Action onSelected)
        {
            if (orderText != null)
                orderText.text = slotNumber.ToString();

            _levelBinding.AddTo(this);
            occupant.Subscribe(ApplyOccupant).AddTo(this);
            selectButton.onClick.AddListener(() => onSelected());
        }

        /// <summary>슬롯 점유자를 빈칸/채움 시각에 반영한다.</summary>
        private void ApplyOccupant(CharacterViewModel occupant)
        {
            _occupant = occupant;
            bool filled = occupant != null;
            if (emptyState != null)
                emptyState.SetActive(!filled);
            if (filledState != null)
                filledState.SetActive(filled);

            // 이전 점유자의 레벨 구독을 끊는다. 교체가 반복돼도 구독이 쌓이지 않는다.
            _levelBinding.Disposable = null;
            if (!filled)
                return;

            if (nameText != null)
                nameText.text = occupant.DisplayName;
            if (rarityFrame != null && theme != null)
                rarityFrame.color = RarityColor(occupant.Rarity);
            if (roleIcon != null && theme != null)
                roleIcon.sprite = RoleSprite(occupant.Role);
            if (levelText != null)
                _levelBinding.Disposable = occupant.Level
                    .Subscribe(level => levelText.text = $"Lv. {level}");

            ApplyPortraitAsync(occupant, this.GetCancellationTokenOnDestroy()).Forget();
            ApplyPortraitOffset(occupant.PortraitCardOffsetY);
        }

        /// <summary>카드 안 초상 높이를 캐릭터별 보정만큼 옮긴다. 프리팹 위치를 기준으로 삼는다.</summary>
        private void ApplyPortraitOffset(float offsetY)
        {
            if (portrait == null)
                return;
            var position = portrait.rectTransform.anchoredPosition;
            position.y = _portraitBaseY + offsetY;
            portrait.rectTransform.anchoredPosition = position;
            // 이펙트는 초상과 같은 프레임에 그려진 그림이라 같은 위치로 따라가야 정렬이 맞는다.
            if (portraitFx != null)
                portraitFx.rectTransform.anchoredPosition = position;
        }

        /// <summary>초상·이펙트 스프라이트를 로드해 대입한다. 로드가 비동기라도 나머지 바인딩을 막지 않는다.</summary>
        private async UniTaskVoid ApplyPortraitAsync(CharacterViewModel occupant, CancellationToken ct)
        {
            var sprite = await occupant.LoadPortraitAsync(ct);
            var fx = await occupant.LoadPortraitFxAsync(ct);
            // 로드를 기다리는 사이 슬롯이 다른 캐릭터로 바뀌었으면 늦게 온 초상을 버린다.
            if (!ReferenceEquals(_occupant, occupant))
                return;
            if (portrait != null)
                portrait.sprite = sprite;
            if (portraitFx != null)
            {
                portraitFx.sprite = fx;
                portraitFx.enabled = fx != null;
            }
        }

        private Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.SSR => theme.raritySSR,
            Rarity.SR => theme.raritySR,
            _ => theme.rarityR,
        };

        private Sprite RoleSprite(Role role) => role switch
        {
            Role.Tanker => theme.roleTanker,
            Role.Healer => theme.roleHealer,
            Role.Supporter => theme.roleSupporter,
            _ => theme.roleDealer,
        };
    }
}
