using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 화면이 16:9보다 좁으면 orthographicSize를 키워 카메라의 가로 시야 폭을 유지한다(fit-width).
    /// 16:9 이상 와이드에서는 인스펙터에 설정된 기본 크기를 그대로 쓴다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraWidthFitter : MonoBehaviour
    {
        private const float ReferenceAspect = 16f / 9f;

        private Camera _camera;
        private float _baseSize;
        private float _lastAspect;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _baseSize = _camera.orthographicSize;
            Apply();
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(_camera.aspect, _lastAspect))
                Apply();
        }

        private void Apply()
        {
            _lastAspect = _camera.aspect;
            _camera.orthographicSize = _lastAspect < ReferenceAspect
                ? _baseSize * ReferenceAspect / _lastAspect
                : _baseSize;
        }
    }
}
