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

        // 실드 구간 마스크. HpFillArea와 같은 구조지만 HP 채움 위에 그려지며, 폭과 위치가 모두 움직인다.
        [SerializeField] private RectTransform shieldFillMask;

        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject actingMarker;

        private readonly CompositeDisposable _bindings = new();

        // 체력 100% 기준 마스크 폭. Awake에서 초기 sizeDelta.x로 캡처해 비율 계산의 기준으로 쓴다.
        private float _hpFillFullWidth;

        // 바 왼쪽 끝의 실드 마스크 x좌표. 실드 구간을 오른쪽 정렬로 배치할 때의 기준점.
        private float _shieldFillLeftX;

        private void Awake()
        {
            if (hpFillMask != null) _hpFillFullWidth = hpFillMask.sizeDelta.x;
            if (shieldFillMask != null) _shieldFillLeftX = shieldFillMask.anchoredPosition.x;
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

            // 실드 구간의 위치가 현재 HP에 물려 있어 두 값이 함께 필요하다. 둘 다 같은 턴 신호에서
            // 파생되므로 한 턴에 두 번 발화하지만, 바 갱신은 멱등이라 무해하다.
            unit.CurrentHp
                .CombineLatest(unit.ShieldAbsorb, (hp, shield) => (hp, shield))
                .Subscribe(t => OnHpChanged(t.hp, t.shield, unit.MaxHp))
                .AddTo(_bindings);
            unit.IsAlive
                .Subscribe(alive => { if (canvasGroup != null) canvasGroup.alpha = alive ? 1f : 0.35f; })
                .AddTo(_bindings);
        }

        // HP 바 채움·실드 구간(마스크 폭)과 "현재/최대 +실드" 숫자 라벨을 함께 갱신한다.
        // 바의 눈금은 항상 최대 HP 기준이라 실드가 붙어도 바 길이나 HP 한 칸의 의미가 변하지 않는다.
        private void OnHpChanged(int hp, int shield, int maxHp)
        {
            float hpFraction = maxHp > 0 ? (float)hp / maxHp : 0f;
            SetMaskWidth(hpFillMask, hpFraction);
            UpdateShieldBand(hp, shield, maxHp);
            if (hpLabel != null)
                hpLabel.text = shield > 0 ? $"{hp}/{maxHp} +{shield}" : $"{hp}/{maxHp}";
        }

        // 실드 구간을 HP 채움 위에 오른쪽 정렬로 배치한다. 구간의 오른쪽 끝을 hp+shield 위치에 맞추고
        // 폭만큼 왼쪽으로 되짚어 시작점을 잡는다. 풀피처럼 hp+shield가 바 끝을 넘는 경우 구간이 바 끝에
        // 붙은 채 HP 채움 위로 파고들어, 실드가 폭 0으로 사라지지 않고 항상 실드량만큼 보인다.
        private void UpdateShieldBand(int hp, int shield, int maxHp)
        {
            if (shieldFillMask == null) return;

            float width = maxHp > 0 ? Mathf.Clamp01((float)shield / maxHp) : 0f;
            float right = maxHp > 0 ? Mathf.Clamp01((float)(hp + shield) / maxHp) : 0f;

            SetMaskWidth(shieldFillMask, width);
            var pos = shieldFillMask.anchoredPosition;
            pos.x = _shieldFillLeftX + _hpFillFullWidth * (right - width);
            shieldFillMask.anchoredPosition = pos;
        }

        // 마스크 폭을 바 전체 폭 대비 비율로 세팅한다. 안쪽 Sliced 이미지는 그대로 두고 마스크만 줄인다.
        private void SetMaskWidth(RectTransform mask, float fraction)
        {
            if (mask == null) return;
            var size = mask.sizeDelta;
            size.x = _hpFillFullWidth * fraction;
            mask.sizeDelta = size;
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
