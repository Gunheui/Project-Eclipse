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

        // 인런 버프 카드 등급색. 캐릭터 레어리티(R/SR/SSR)와는 별개 축이라 같은 값을 공유하지 않는다.
        // on* 은 밝은 표면 위 등급명 텍스트용 어두운 변형이다(채움색 위 흰 글씨는 대비가 모자란다).
        [Header("Card Grade")]
        public Color cardGradeCommon = new Color(0.5412f, 0.5765f, 0.6784f);      // #8A93AD
        public Color cardGradeRare = new Color(0.1843f, 0.6588f, 0.6275f);        // #2FA8A0
        public Color cardGradeEpic = new Color(0.8235f, 0.3333f, 0.6078f);        // #D2559B
        public Color cardGradeUnique = new Color(0.9412f, 0.4784f, 0.2196f);      // #F07A38
        public Color onCardGradeCommon = new Color(0.3529f, 0.3804f, 0.5020f);    // #5A6180
        public Color onCardGradeRare = new Color(0.1216f, 0.4784f, 0.4549f);      // #1F7A74
        public Color onCardGradeEpic = new Color(0.6392f, 0.1804f, 0.4392f);      // #A32E70
        public Color onCardGradeUnique = new Color(0.7059f, 0.3137f, 0.1059f);    // #B4501B

        [Header("Text")]
        public Color textHigh = new Color(0.1373f, 0.1529f, 0.2392f);        // #23273D
        public Color textMedium = new Color(0.3529f, 0.3804f, 0.5020f);      // #5A6180
        public Color textDisabled = new Color(0.6549f, 0.6745f, 0.7686f);    // #A7ACC4
    }
}
