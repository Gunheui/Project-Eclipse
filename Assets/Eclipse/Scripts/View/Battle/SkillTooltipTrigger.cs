using System;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Eclipse.View
{
    /// <summary>
    /// 스킬 버튼 하나에 붙어 hover(PC)·롱프레스(터치)를 감지해 공유 <see cref="SkillTooltip"/>을 띄운다.
    /// 표시할 스킬명·설명은 <see cref="BattleView"/>가 행동자 전환마다 세팅한다.
    /// 롱프레스로 툴팁을 띄운 뒤 손을 떼면 Unity가 클릭(=스킬 시전)을 그대로 발동하므로,
    /// <see cref="ConsumeLongPress"/>로 BattleView가 그 한 번의 시전을 건너뛰게 한다.
    /// </summary>
    public class SkillTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private SkillTooltip tooltip;

        // 이 시간(초) 이상 누르고 있으면 롱프레스로 보고 툴팁을 띄운다. 이보다 짧으면 평범한 탭(시전).
        [SerializeField] private float longPressSeconds = 0.4f;

        private readonly Subject<Unit> _pressed = new();
        private readonly Subject<Unit> _released = new();
        private bool _longPressed; // 직전 프레스가 롱프레스였는가 — 다음 클릭(시전) 억제 대상

        public string SkillName { get; set; }
        public string Description { get; set; }

        private void Awake()
        {
            // 누른 뒤 임계 시간이 지나도록 떼지 않으면 롱프레스. 떼거나 버튼 밖으로 벗어나면 _released가 타이머를 끊는다.
            // 툴팁은 UI 입력이라 게임 속도에 반응이 끌려가면 안 되므로 timeScale을 무시하는 시계를 쓴다.
            _pressed
                .SelectMany(_ => Observable
                    .Timer(TimeSpan.FromSeconds(longPressSeconds), UnityTimeProvider.UpdateIgnoreTimeScale)
                    .TakeUntil(_released))
                .Subscribe(_ =>
                {
                    _longPressed = true;
                    TryShow();
                })
                .AddTo(this);

            _pressed.AddTo(this);
            _released.AddTo(this);
        }

        /// <summary>PC: 마우스가 올라오면 툴팁을 바로 표시한다.</summary>
        public void OnPointerEnter(PointerEventData eventData) => TryShow();

        public void OnPointerExit(PointerEventData eventData) => Release();

        /// <summary>
        /// 누른 순간 롱프레스 타이머를 건다. 임계 시간을 넘기면 툴팁을 띄우고 시전 억제 플래그를 세운다.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            _longPressed = false;
            _pressed.OnNext(Unit.Default);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        /// <summary>직전 프레스가 롱프레스였으면 true를 한 번 돌려주고 플래그를 내린다(시전 억제용).</summary>
        public bool ConsumeLongPress()
        {
            bool was = _longPressed;
            _longPressed = false;
            return was;
        }

        private void Release()
        {
            _released.OnNext(Unit.Default);
            if (tooltip != null) tooltip.Hide();
        }

        private void TryShow()
        {
            if (tooltip == null || string.IsNullOrEmpty(Description)) return;
            tooltip.Show(SkillName, Description);
        }
    }
}
