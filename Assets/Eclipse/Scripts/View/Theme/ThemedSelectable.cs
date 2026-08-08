using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View.Theme
{
    /// <summary>
    /// ColorTint 버튼의 상태별 색을 토큰에서 채운다.
    /// 색이 <see cref="Graphic"/>이 아니라 <see cref="Selectable.colors"/>에 실리는 자리라
    /// <see cref="ThemedGraphic"/>이 덮지 못한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemedSelectable : MonoBehaviour
    {
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private UIThemeToken normal;
        [SerializeField] private UIThemeToken highlighted;
        [SerializeField] private UIThemeToken pressed;
        [SerializeField] private UIThemeToken selected;
        [SerializeField] private UIThemeToken disabled;

        // 켜면 토큰의 RGB만 가져오고 상태별 투명도는 저작값을 지킨다.
        // 평소 투명하다 눌릴 때만 드러나는 카드, 반투명 패널 버튼이 여기 해당한다.
        [SerializeField] private bool keepAlpha;

        private Selectable _selectable;

        // 저작 시점의 상태별 투명도. 순서는 normal, highlighted, pressed, selected, disabled.
        private float[] _authoredAlphas;

        private void OnEnable() => ApplyTheme();

        /// <summary>토큰 색을 상태별 색에 반영한다. 테마가 비어 있으면 아무것도 하지 않는다.</summary>
        public void ApplyTheme()
        {
            if (theme == null)
                return;

            if (_selectable == null)
                _selectable = GetComponent<Selectable>();
            if (_selectable == null)
                return;

            var colors = _selectable.colors;
            _authoredAlphas ??= new[]
            {
                colors.normalColor.a,
                colors.highlightedColor.a,
                colors.pressedColor.a,
                colors.selectedColor.a,
                colors.disabledColor.a,
            };

            colors.normalColor = Tint(normal, 0);
            colors.highlightedColor = Tint(highlighted, 1);
            colors.pressedColor = Tint(pressed, 2);
            colors.selectedColor = Tint(selected, 3);
            colors.disabledColor = Tint(disabled, 4);

            // colorMultiplier와 fadeDuration은 저작값을 그대로 둔다. 색 축이 아니다.
            _selectable.colors = colors;
        }

        private Color Tint(UIThemeToken token, int slot)
        {
            var color = theme.Resolve(token);
            if (keepAlpha)
                color.a = _authoredAlphas[slot];

            return color;
        }

#if UNITY_EDITOR
        private void OnValidate() => ApplyTheme();
#endif
    }
}
