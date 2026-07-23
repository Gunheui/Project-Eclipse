using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// Screen.safeArea를 앵커로 환산해 노치·홈바 영역을 피한다.
    /// 캔버스 바로 아래의 풀스트레치 컨테이너에 붙이고, 실제 UI는 그 자식으로 배치한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
                Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            Vector2 min = _lastSafeArea.position;
            Vector2 max = min + _lastSafeArea.size;
            _rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            _rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
