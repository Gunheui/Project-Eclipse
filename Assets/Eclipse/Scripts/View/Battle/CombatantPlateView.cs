using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 유닛 명판 하나. 대응하는 <see cref="CombatantViewModel"/>의 이름·HP(바+숫자)·생존을 표시한다.
    /// 명판은 각 배틀러 머리 위(월드 스페이스)에 붙어 함께 움직이며 전투 시작 시 Bind로 한 번 연결된다.
    /// </summary>
    public class CombatantPlateView : MonoBehaviour
    {
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject actingMarker;

        private readonly CompositeDisposable _bindings = new();

        /// <summary>
        /// 이 명판을 한 유닛 VM에 연결한다. 이전 구독은 정리하고, 이름·HP 바·생존 흐리기를 새로 바인딩한다.
        /// </summary>
        /// <param name="unit">이 명판이 표시할 유닛 VM.</param>
        public void Bind(CombatantViewModel unit)
        {
            _bindings.Clear();
            gameObject.SetActive(true);
            if (nameLabel != null) nameLabel.text = unit.Name;
            SetActing(false);

            unit.CurrentHp
                .Subscribe(hp => OnHpChanged(hp, unit.MaxHp))
                .AddTo(_bindings);
            unit.IsAlive
                .Subscribe(alive => { if (canvasGroup != null) canvasGroup.alpha = alive ? 1f : 0.35f; })
                .AddTo(_bindings);
        }

        // HP 바 fill과 "현재/최대" 숫자 라벨을 함께 갱신한다.
        private void OnHpChanged(int hp, int maxHp)
        {
            hpFill.fillAmount = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (hpLabel != null) hpLabel.text = $"{hp}/{maxHp}";
        }

        /// <summary>대응 유닛이 없는 빈 명판을 숨긴다.</summary>
        public void Clear()
        {
            _bindings.Clear();
            gameObject.SetActive(false);
        }

        /// <summary>지금 이 유닛이 행동할 차례인지 표시한다(행동자 강조).</summary>
        /// <param name="acting">행동 차례이면 true.</param>
        public void SetActing(bool acting)
        {
            if (actingMarker != null) actingMarker.SetActive(acting);
        }

        private void OnDestroy() => _bindings.Dispose();
    }
}
