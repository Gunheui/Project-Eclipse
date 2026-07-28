using System;
using Eclipse.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Eclipse.View
{
    /// <summary>
    /// 전장에 세우는 문 하나. 문 프레임과 그림(아이콘 또는 파티원 초상)을 월드 스프라이트로 그리고,
    /// 이름·약속을 머리 위 월드 캔버스에 띄운다. 프레임 탭이 선택 입력이다(Bind의 onTapped).
    /// </summary>
    public class WorldDoorView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text promiseLabel;

        // 탭 판정 영역. 프레임 크기로 에디터에서 고정한다 — 그림 크기와 무관하게 손가락 여유를 보장한다.
        [SerializeField] private BoxCollider2D tapArea;

        // 그림을 이 높이(월드 단위)로 맞춰 그린다. 초상과 아이콘은 원본 크기가 달라 그대로 걸 수 없다.
        [SerializeField] private float iconFitHeight = 1.2f;

        private Action<WorldDoorView> _onTapped;

        // 프리팹에 잡아 둔 그림 자리, 곧 프레임 안에서 그림의 바닥 중앙이 놓일 지점.
        // 초상은 하단 피벗이고 아이콘은 중앙 피벗이라, 위치를 피벗에 맡기면 종류마다 다른 높이에 걸린다.
        private Vector3 _iconAnchor;

        private void Awake()
        {
            if (iconRenderer != null) _iconAnchor = iconRenderer.transform.localPosition;
        }

        /// <summary> 이 문이 표시 중인 선택지. 탭이 확정되면 이 값의 Choice가 그대로 보고된다. </summary>
        public DoorOption Option { get; private set; }

        /// <summary> 문을 한 선택지에 연결해 세운다. </summary>
        /// <param name="onTapped">프레임을 탭했을 때 이 문으로 호출된다.</param>
        public void Bind(DoorOption option, Action<WorldDoorView> onTapped)
        {
            Option = option;
            _onTapped = onTapped;
            gameObject.SetActive(true);
            if (nameLabel != null) nameLabel.text = option.DisplayName;
            if (promiseLabel != null) promiseLabel.text = option.Promise;
            FitIcon(option.Icon);
            SetTappable(true);
        }

        /// <summary> 문을 내리고 탭 통지를 끊는다. 이미 내려가 있어도 호출 안전하다. </summary>
        public void Clear()
        {
            _onTapped = null;
            gameObject.SetActive(false);
        }

        /// <summary> 탭을 받을지 정한다. 하나가 확정되면 세 문 모두 꺼서 중복 선택을 막는다. </summary>
        public void SetTappable(bool on)
        {
            if (tapArea != null) tapArea.enabled = on;
        }

        /// <summary>
        /// EventSystem 클릭 콜백. Collider2D + 카메라의 Physics2DRaycaster로 월드 스프라이트 탭이 전달된다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) => _onTapped?.Invoke(this);

        /// <summary> 그림을 프레임 높이에 맞춰 등비로 줄이고 바닥 중앙을 그림 자리에 맞춘다. 비면 렌더러를 끈다. </summary>
        private void FitIcon(Sprite sprite)
        {
            if (iconRenderer == null) return;

            iconRenderer.sprite = sprite;
            iconRenderer.enabled = sprite != null;
            if (sprite == null) return;

            var bounds = sprite.bounds;
            float scale = bounds.size.y > 0f ? iconFitHeight / bounds.size.y : 1f;
            iconRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            iconRenderer.transform.localPosition =
                _iconAnchor - new Vector3(bounds.center.x * scale, bounds.min.y * scale, 0f);
        }
    }
}
