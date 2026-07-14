using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 피격·힐 시 대상 위에 잠깐 떠오르는 데미지/힐 숫자. 위로 상승하며 서서히 사라진 뒤 스스로 파괴된다.
    /// 월드 공간에 배치되며, 배틀러가 Instantiate 후 <see cref="Show"/>로 값을 넣는다.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float riseHeight = 1.2f;
        [SerializeField] private float duration = 0.7f;
        [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.5f);

        /// <summary>
        /// 숫자를 세팅하고 상승·페이드 연출을 시작한다. 연출이 끝나면 이 오브젝트를 파괴한다.
        /// </summary>
        /// <param name="amount">표시할 크기(양수).</param>
        /// <param name="isHeal">힐이면 초록 "+", 데미지면 빨강 "-".</param>
        /// <param name="speed">연출 배속(1 또는 2). 상승·페이드 시간을 나눈다.</param>
        public void Show(int amount, bool isHeal, int speed)
        {
            if (label != null)
            {
                label.text = (isHeal ? "+" : "-") + amount;
                label.color = isHeal ? healColor : damageColor;
            }

            float dur = duration / Mathf.Max(1, speed);

            var seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(transform.position.y + riseHeight, dur).SetEase(Ease.OutQuad));
            if (label != null)
                seq.Join(DOTween.To(() => label.alpha, a => label.alpha = a, 0f, dur).SetEase(Ease.InQuad));
            seq.OnComplete(() => Destroy(gameObject));
        }
    }
}