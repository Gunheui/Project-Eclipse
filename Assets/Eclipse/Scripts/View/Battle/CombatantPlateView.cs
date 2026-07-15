using System;
using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Eclipse.View
{
    /// <summary>
    /// 유닛 명판 하나. 대응하는 <see cref="CombatantViewModel"/>의 이름·HP(바+숫자)·생존을 표시한다.
    /// 명판은 각 배틀러 머리 위(월드 스페이스)에 붙어 함께 움직이며 전투 시작 시 Bind로 한 번 연결된다.
    /// 조준 모드에서는 배틀러 몸통과 함께 보조 탭면 역할을 한다(Bind의 onTapped). 대상 강조는 하지 않는다.
    /// </summary>
    public class CombatantPlateView : MonoBehaviour, IPointerClickHandler
    {
        // HP 채움을 왼쪽부터 드러내는 마스크 영역(RectMask2D). 폭을 체력 비율만큼 줄여 채움을 표시한다.
        // 안쪽 HpFill 이미지는 Sliced라 어떤 폭에서도 모서리가 왜곡되지 않는다.
        [SerializeField] private RectTransform hpFillMask;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject actingMarker;

        private readonly CompositeDisposable _bindings = new();

        // 체력 100% 기준 마스크 폭. Awake에서 초기 sizeDelta.x로 캡처해 비율 계산의 기준으로 쓴다.
        private float _hpFillFullWidth;

        private void Awake()
        {
            if (hpFillMask != null) _hpFillFullWidth = hpFillMask.sizeDelta.x;
        }

        // 이 명판이 표시 중인 유닛과 탭 통지처. Bind에서 세우고 Clear에서 지워 바인딩과 수명을 맞춘다.
        private CombatantViewModel _unit;
        private Action<CombatantViewModel> _onTapped;

        /// <summary>
        /// 이 명판을 한 유닛 VM에 연결한다. 이전 구독은 정리하고, 이름·HP 바·생존 흐리기를 새로 바인딩한다.
        /// </summary>
        /// <param name="unit">이 명판이 표시할 유닛 VM.</param>
        /// <param name="onTapped">명판을 탭했을 때 이 유닛으로 호출된다. null이면 탭이 무시된다.</param>
        public void Bind(CombatantViewModel unit, Action<CombatantViewModel> onTapped = null)
        {
            _bindings.Clear();
            gameObject.SetActive(true);
            _unit = unit;
            _onTapped = onTapped;
            if (nameLabel != null) nameLabel.text = unit.Name;
            SetActing(false);

            unit.CurrentHp
                .Subscribe(hp => OnHpChanged(hp, unit.MaxHp))
                .AddTo(_bindings);
            unit.IsAlive
                .Subscribe(alive => { if (canvasGroup != null) canvasGroup.alpha = alive ? 1f : 0.35f; })
                .AddTo(_bindings);
        }

        // HP 바 채움(마스크 폭)과 "현재/최대" 숫자 라벨을 함께 갱신한다.
        private void OnHpChanged(int hp, int maxHp)
        {
            float fraction = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (hpFillMask != null)
            {
                var size = hpFillMask.sizeDelta;
                size.x = _hpFillFullWidth * fraction;
                hpFillMask.sizeDelta = size;
            }
            if (hpLabel != null) hpLabel.text = $"{hp}/{maxHp}";
        }

        /// <summary>대응 유닛이 없는 빈 명판을 숨기고 탭 통지를 끊는다.</summary>
        public void Clear()
        {
            _bindings.Clear();
            _unit = null;
            _onTapped = null;
            gameObject.SetActive(false);
        }

        /// <summary>지금 이 유닛이 행동할 차례인지 표시한다(행동자 강조).</summary>
        /// <param name="acting">행동 차례이면 true.</param>
        public void SetActing(bool acting)
        {
            if (actingMarker != null) actingMarker.SetActive(acting);
        }

        /// <summary>
        /// EventSystem 클릭 콜백. 명판의 투명 TapArea(raycastTarget)에서 GraphicRaycaster로 탭이 전달된다.
        /// 바인딩된 유닛을 그대로 통지처에 넘기며, 조준 중인지·유효 대상인지 판단은 BattleView가 한다.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) => _onTapped?.Invoke(_unit);

        private void OnDestroy() => _bindings.Dispose();
    }
}
