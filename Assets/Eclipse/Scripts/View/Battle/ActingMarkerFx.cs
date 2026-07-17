using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 현재 턴 마커의 자가 연출. 위아래로 천천히 떠다니고, 뒤쪽 글로우가 같은 박자로 밝아졌다 어두워진다.
    /// 마커는 <see cref="CombatantPlateView.SetActing"/>이 SetActive로만 토글하므로 트윈 수명을
    /// OnEnable/OnDisable에 맞춘다 — 켜질 때 시작하고 꺼질 때 정리한다. 켜고 끄는 쪽은 이 연출을 모른다.
    /// </summary>
    public class ActingMarkerFx : MonoBehaviour
    {
        // 뒤쪽 글로우. 연결하지 않아도 부유 연출은 그대로 돈다.
        [SerializeField] private Graphic glow;

        // 위아래 진폭(로컬 UI 단위)과 한 방향에 걸리는 시간.
        [SerializeField] private float bobDistance = 10f;
        [SerializeField] private float bobDuration = 0.7f;

        // 글로우가 오가는 알파 구간. 글로우 스프라이트가 가산 합성용이라 알파가 옅어 상한을 끝까지 쓴다.
        [SerializeField] private float glowMinAlpha = 0.35f;
        [SerializeField] private float glowMaxAlpha = 1f;

        private RectTransform _rt;
        private Vector2 _home;
        private Sequence _tween;

        // 마커는 비활성으로 시작하므로 Awake는 첫 활성화 직전(OnEnable 앞)에 한 번 돈다 — _home은 항상 원위치.
        private void Awake()
        {
            _rt = (RectTransform)transform;
            _home = _rt.anchoredPosition;
        }

        private void OnEnable()
        {
            _rt.anchoredPosition = _home;
            SetGlowAlpha(glowMinAlpha);

            var seq = DOTween.Sequence();
            seq.Join(_rt.DOAnchorPosY(_home.y + bobDistance, bobDuration).SetEase(Ease.InOutSine));
            if (glow != null) seq.Join(glow.DOFade(glowMaxAlpha, bobDuration).SetEase(Ease.InOutSine));
            seq.SetLoops(-1, LoopType.Yoyo);
            _tween = seq;
        }

        private void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
            _rt.anchoredPosition = _home;
            SetGlowAlpha(glowMinAlpha);
        }

        private void SetGlowAlpha(float alpha)
        {
            if (glow == null) return;
            var c = glow.color;
            c.a = alpha;
            glow.color = c;
        }
    }
}
