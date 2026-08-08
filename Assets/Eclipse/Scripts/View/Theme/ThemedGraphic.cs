using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View.Theme
{
    /// <summary>
    /// 같은 오브젝트의 <see cref="Graphic"/> 색을 토큰 하나에서 읽어 칠한다.
    /// 상태 전이 없이 색이 하나로 고정된 자리에 붙이며, 상태별로 색이 갈리는 자리는 전용 컴포넌트가 맡는다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class ThemedGraphic : MonoBehaviour
    {
        [SerializeField] private UIThemeSO theme;
        [SerializeField] private UIThemeToken token;

        // 켜면 토큰의 RGB만 가져오고 투명도는 저작값을 지킨다.
        // 스크림이나 반투명 패널처럼 같은 색을 옅게 깐 자리를 위한 것으로, 토큰을 알파별로 늘리지 않으려는 장치다.
        [SerializeField] private bool keepAlpha;

        private Graphic _graphic;

        // 저작 시점의 투명도. 첫 적용 전에 잡아 두지 않으면 이미 칠한 값을 원본으로 착각한다.
        private float _authoredAlpha = -1f;

        private void OnEnable() => ApplyTheme();

        /// <summary>토큰 색을 그래픽에 반영한다. 테마가 비어 있으면 아무것도 하지 않는다.</summary>
        public void ApplyTheme()
        {
            if (theme == null)
                return;

            if (_graphic == null)
                _graphic = GetComponent<Graphic>();
            if (_graphic == null)
                return;

            if (_authoredAlpha < 0f)
                _authoredAlpha = _graphic.color.a;

            var color = theme.Resolve(token);
            if (keepAlpha)
                color.a = _authoredAlpha;

            _graphic.color = color;
        }

#if UNITY_EDITOR
        private void OnValidate() => ApplyTheme();
#endif
    }
}
