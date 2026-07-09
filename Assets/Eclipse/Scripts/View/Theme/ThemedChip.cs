using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View.Theme
{
    /// <summary>
    /// 재화 캡슐·닉네임 카드처럼 상태 전이가 없는 정적 표시 요소에 <see cref="UIThemeSO"/> 토큰 색을 적용한다.
    /// 배정된 그래픽만 갱신하며, 재화/등급 아이콘의 원색은 건드리지 않는다.
    /// </summary>
    public class ThemedChip : MonoBehaviour
    {
        [SerializeField] private UIThemeSO theme;

        [Header("Themed graphics")]
        [SerializeField] private Image pillBg;       // 캡슐/카드 배경 → surface2
        [SerializeField] private TMP_Text valueText; // 값·이름 텍스트 → text/high
        [SerializeField] private Image plusBadge;    // ＋ 원형 배지 → primary
        [SerializeField] private Graphic plusGlyph;  // ＋ 글리프(Image 또는 TMP) → on-primary

        private void Awake() => Apply();

        private void Apply()
        {
            if (theme == null)
                return;

            if (pillBg != null) pillBg.color = theme.surface2;
            if (valueText != null) valueText.color = theme.textHigh;
            if (plusBadge != null) plusBadge.color = theme.primary;
            if (plusGlyph != null) plusGlyph.color = theme.onPrimary;
        }

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif
    }
}
