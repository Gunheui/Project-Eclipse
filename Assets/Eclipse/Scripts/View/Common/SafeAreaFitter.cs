using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 세이프 에어리어 인셋을 기존 여백에 더해 노치·홈 인디케이터를 피한다.
    /// 부모가 화면 전체를 덮는 풀스트레치 rect에만 붙인다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Canvas _rootCanvas;
        private Vector2 _baseOffsetMin;
        private Vector2 _baseOffsetMax;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _lastScaleFactor;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            _baseOffsetMin = _rect.offsetMin;
            _baseOffsetMax = _rect.offsetMax;
        }

        // 꺼져 있는 동안에는 Update가 돌지 않으므로 켜질 때마다 다시 맞춘다.
        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenWidth
                || Screen.height != _lastScreenHeight
                // 캔버스 배율이 뒤늦게 갱신되면 인셋이 어긋난 채 남으므로 배율도 비교한다.
                || CurrentScaleFactor() != _lastScaleFactor)
            {
                Apply();
            }
        }

        private float CurrentScaleFactor()
        {
            return _rootCanvas != null && _rootCanvas.scaleFactor > 0f ? _rootCanvas.scaleFactor : 1f;
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastScaleFactor = CurrentScaleFactor();

            // safeArea는 픽셀 단위라 캔버스 배율로 나눠야 rect 좌표와 맞는다.
            Vector2 insetMin = _lastSafeArea.position / _lastScaleFactor;
            Vector2 insetMax = new Vector2(
                _lastScreenWidth - _lastSafeArea.xMax,
                _lastScreenHeight - _lastSafeArea.yMax) / _lastScaleFactor;

            _rect.offsetMin = _baseOffsetMin + insetMin;
            _rect.offsetMax = _baseOffsetMax - insetMax;
        }
    }
}
