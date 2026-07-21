using UnityEngine;

namespace Eclipse.View.Theme
{
    /// <summary>
    /// Periwinkle 디자인 시스템의 Foundation 색 토큰을 담는 단일 원천 에셋.
    /// 테마 컴포넌트(<see cref="ThemedTab"/> 등)가 상태별 색을 이 값에서 읽는다.
    /// 필드 기본값은 periwinkle-design-system.md §1 Foundation의 hex를 sRGB 0~1로 옮긴 것.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "Eclipse/UI Theme")]
    public class UIThemeSO : ScriptableObject
    {
        [Header("Primary")]
        public Color primary = new Color(0.4314f, 0.4824f, 0.9490f);         // #6E7BF2
        public Color primaryHover = new Color(0.3608f, 0.4078f, 0.8706f);    // #5C68DE
        public Color primaryPressed = new Color(0.2980f, 0.3412f, 0.7686f);  // #4C57C4
        public Color primarySubtle = new Color(0.9059f, 0.9137f, 0.9882f);   // #E7E9FC
        public Color primaryDisabled = new Color(0.7804f, 0.7922f, 0.9098f); // #C7CAE8
        public Color onPrimary = Color.white;                               // #FFFFFF

        [Header("Surface")]
        public Color surface2 = new Color(0.9804f, 0.9843f, 0.9961f);        // #FAFBFE
        public Color borderDefault = new Color(0.8588f, 0.8706f, 0.9412f);   // #DBDEF0

        [Header("Semantic")]
        public Color positiveSubtle = new Color(0.9098f, 0.9529f, 0.9333f);   // #E8F3EE
        public Color onPositiveSubtle = new Color(0.1765f, 0.4078f, 0.3176f); // #2D6851
        public Color dangerSubtle = new Color(0.9843f, 0.9176f, 0.9098f);     // #FBEAE8
        public Color onDangerSubtle = new Color(0.6510f, 0.2824f, 0.2510f);   // #A64840

        [Header("Text")]
        public Color textHigh = new Color(0.1373f, 0.1529f, 0.2392f);        // #23273D
        public Color textMedium = new Color(0.3529f, 0.3804f, 0.5020f);      // #5A6180
        public Color textDisabled = new Color(0.6549f, 0.6745f, 0.7686f);    // #A7ACC4
    }
}
