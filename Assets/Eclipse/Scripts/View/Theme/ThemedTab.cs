using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View.Theme
{
    /// <summary>
    /// 하단 내비게이션 탭 버튼. Button의 상호작용 상태(Normal/Highlighted/Pressed/Disabled)와
    /// <see cref="isSelected"/>를 조합해 배지·아이콘·라벨 세 그래픽에 <see cref="UIThemeSO"/>
    /// 토큰 색을 적용한다. Button 상속이라 onClick·interactable은 표준 Button과 동일하게 동작한다.
    /// </summary>
    public class ThemedTab : Button
    {
        [Header("Themed graphics")]
        [SerializeField] private Image iconBg;   // 배지 배경 (토큰 색으로 채움)
        [SerializeField] private Image icon;     // 아이콘 (틴트로 색 적용)
        [SerializeField] private TMP_Text label; // 라벨 텍스트

        [Header("State")]
        [Tooltip("현재 선택된 탭이면 true. 선택 시 primary 배지 + onPrimary 아이콘 + primary Bold 라벨.")]
        [SerializeField] private bool isSelected;

        [SerializeField] private UIThemeSO theme;

        /// <summary>
        /// 선택 상태를 갱신하고 즉시 색을 다시 적용한다. 런타임 탭 전환용(현재 미사용).
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (theme == null || iconBg == null || icon == null || label == null)
                return;

            Color badge;
            Color iconColor;   // 배지(IconBg) 위에 얹히는 글리프 색
            Color labelColor;  // 밝은 바 배경 위에 놓이는 라벨 색
            bool bold;

            if (!interactable)
            {
                badge = theme.surface2;
                iconColor = theme.textDisabled;
                labelColor = theme.textDisabled;
                bold = false;
            }
            else if (isSelected)
            {
                badge = state switch
                {
                    SelectionState.Pressed => theme.primaryPressed,
                    SelectionState.Highlighted => theme.primaryHover,
                    _ => theme.primary,
                };
                iconColor = theme.onPrimary; // primary 배지 위 → 흰색
                labelColor = badge;          // 밝은 바 위 → primary 계열(press 시 배지와 함께 진해짐)
                bold = true;
            }
            else
            {
                badge = theme.primarySubtle;
                Color c = state == SelectionState.Normal ? theme.textMedium : theme.textHigh;
                iconColor = c;
                labelColor = c;
                bold = false;
            }

            iconBg.color = badge;
            icon.color = iconColor;
            label.color = labelColor;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            DoStateTransition(currentSelectionState, true);
        }
#endif
    }
}
