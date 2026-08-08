using System;
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

        // 표면과 테두리는 Material 3의 라이트 스킴 톤 계단을 따른다.
        // surface2 = surface(98), surface1 = surfaceContainerLow(96), borderDefault = outlineVariant 근처,
        // borderStrong = outlineVariant(80). surfaceDark는 일러스트나 전장 위에 까는 어두운 판이라
        // 톤이 라이트든 다크든 항상 어둡다. scrim은 tone 0, 즉 검정이며 투명도는 쓰는 자리가 정한다.
        [Header("Surface")]
        public Color surface2 = new Color(0.9804f, 0.9843f, 0.9961f);        // #FAFBFE
        public Color surface1 = new Color(0.9529f, 0.9569f, 0.9843f);        // #F3F4FB
        public Color borderDefault = new Color(0.8588f, 0.8706f, 0.9412f);   // #DBDEF0
        public Color borderStrong = new Color(0.7686f, 0.7882f, 0.8941f);    // #C4C9E4
        public Color surfaceDark = new Color(0.0902f, 0.1020f, 0.1412f);     // #171A24
        public Color scrim = Color.black;                                    // #000000

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

        // 캐릭터 등급색. 카드 프레임처럼 등급을 색으로만 알리는 자리에 쓴다.
        [Header("Rarity")]
        public Color rarityR = new Color(0.4314f, 0.6078f, 0.8392f);    // #6E9BD6
        public Color raritySR = new Color(0.6902f, 0.4157f, 0.8510f);   // #B06AD9
        public Color raritySSR = new Color(0.9098f, 0.7216f, 0.2941f);  // #E8B84B

        // 역할 아이콘. 화면마다 스프라이트를 따로 물리지 않도록 매핑을 여기 한 벌만 둔다.
        [Header("Role Icon")]
        public Sprite roleTanker;
        public Sprite roleDealer;
        public Sprite roleHealer;
        public Sprite roleSupporter;

        // 전투 화면 색. 데미지 숫자·타겟 아웃라인·타임라인 프레임·상태 아이콘 프레임이 전부 여기서 읽는다.
        // 아군 힐/버프 조준을 녹색으로 두는 건 힐 대상이 공격 대상처럼 보이지 않게 하려는 것.
        // battleEffectHarmful은 현재 battleEnemy와 같은 값이지만 축이 달라 별도 토큰으로 둔다.
        [Header("Battle")]
        public Color battleDamage = new Color(1f, 0.3500f, 0.3000f);      // #FF594D
        public Color battleHeal = new Color(0.4000f, 1f, 0.5000f);        // #66FF80
        public Color battleDot = new Color(0.9412f, 0.5412f, 0.2353f);    // #F08A3C
        public Color battleRegen = new Color(0.4980f, 0.8784f, 0.6588f);  // #7FE0A8
        public Color battleShield = new Color(0.7255f, 0.7529f, 0.8078f); // #B9C0CE

        public Color battleAlly = new Color(0.3059f, 0.6078f, 0.4784f);   // #4E9B7A
        public Color battleEnemy = new Color(0.8157f, 0.4157f, 0.3804f);  // #D06A61

        public Color battleEffectBeneficial = new Color(0.2902f, 0.4784f, 0.8471f); // #4A7AD8
        public Color battleEffectHarmful = new Color(0.8157f, 0.4157f, 0.3804f);    // #D06A61
        public Color battleEffectOverflow = new Color(0.2902f, 0.2902f, 0.3216f);   // #4A4A52

        [Header("Text")]
        public Color textHigh = new Color(0.1373f, 0.1529f, 0.2392f);        // #23273D
        public Color textMedium = new Color(0.3529f, 0.3804f, 0.5020f);      // #5A6180
        public Color textDisabled = new Color(0.6549f, 0.6745f, 0.7686f);    // #A7ACC4

        /// <summary>토큰이 가리키는 색을 돌려준다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">대응 필드가 없는 토큰. 색을 대신 흘려보내지 않는다.</exception>
        // 필드명 문자열이나 리플렉션으로 잇지 않고 손으로 나열한다. 필드를 리네임해도 컴파일러가 잡아 준다.
        public Color Resolve(UIThemeToken token) => token switch
        {
            UIThemeToken.Primary => primary,
            UIThemeToken.PrimaryHover => primaryHover,
            UIThemeToken.PrimaryPressed => primaryPressed,
            UIThemeToken.PrimarySubtle => primarySubtle,
            UIThemeToken.PrimaryDisabled => primaryDisabled,
            UIThemeToken.OnPrimary => onPrimary,
            UIThemeToken.Surface2 => surface2,
            UIThemeToken.Surface1 => surface1,
            UIThemeToken.BorderDefault => borderDefault,
            UIThemeToken.BorderStrong => borderStrong,
            UIThemeToken.SurfaceDark => surfaceDark,
            UIThemeToken.Scrim => scrim,
            UIThemeToken.PositiveSubtle => positiveSubtle,
            UIThemeToken.OnPositiveSubtle => onPositiveSubtle,
            UIThemeToken.DangerSubtle => dangerSubtle,
            UIThemeToken.OnDangerSubtle => onDangerSubtle,
            UIThemeToken.CardGradeCommon => cardGradeCommon,
            UIThemeToken.CardGradeRare => cardGradeRare,
            UIThemeToken.CardGradeEpic => cardGradeEpic,
            UIThemeToken.CardGradeUnique => cardGradeUnique,
            UIThemeToken.OnCardGradeCommon => onCardGradeCommon,
            UIThemeToken.OnCardGradeRare => onCardGradeRare,
            UIThemeToken.OnCardGradeEpic => onCardGradeEpic,
            UIThemeToken.OnCardGradeUnique => onCardGradeUnique,
            UIThemeToken.RarityR => rarityR,
            UIThemeToken.RaritySR => raritySR,
            UIThemeToken.RaritySSR => raritySSR,
            UIThemeToken.BattleDamage => battleDamage,
            UIThemeToken.BattleHeal => battleHeal,
            UIThemeToken.BattleDot => battleDot,
            UIThemeToken.BattleRegen => battleRegen,
            UIThemeToken.BattleShield => battleShield,
            UIThemeToken.BattleAlly => battleAlly,
            UIThemeToken.BattleEnemy => battleEnemy,
            UIThemeToken.BattleEffectBeneficial => battleEffectBeneficial,
            UIThemeToken.BattleEffectHarmful => battleEffectHarmful,
            UIThemeToken.BattleEffectOverflow => battleEffectOverflow,
            UIThemeToken.TextHigh => textHigh,
            UIThemeToken.TextMedium => textMedium,
            UIThemeToken.TextDisabled => textDisabled,
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "UIThemeSO에 대응 필드가 없는 토큰"),
        };

#if UNITY_EDITOR
        /// <summary>인스펙터에서 색이 바뀐 직후 발행한다. 에디터 미리보기 갱신이 구독한다.</summary>
        // 구독자는 Editor 어셈블리에 있어 여기서 직접 부를 수 없다. 알림만 내보내고 갱신은 저쪽이 맡는다.
        public static event Action<UIThemeSO> Changed;

        private void OnValidate() => Changed?.Invoke(this);
#endif
    }
}
