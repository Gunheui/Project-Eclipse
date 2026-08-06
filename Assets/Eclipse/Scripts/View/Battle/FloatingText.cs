using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 대상 위에 잠깐 떠오르는 숫자. 위로 상승하며 서서히 사라진 뒤 스스로 파괴된다.
    /// 월드 공간에 배치되며, 스폰한 쪽이 Instantiate 후 Show로 값을 넣는다.
    /// 데미지·힐 숫자와 재화 드랍 수량이 같은 연출을 쓴다.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float riseHeight = 1.2f;
        [SerializeField] private float duration = 0.7f;

        // 치명타 숫자를 이만큼 키운다. 색은 이미 피해·회복·도트·리젠·실드가 나눠 쓰고 있어 크기·굵기로 알린다.
        [SerializeField] private float critScale = 1.4f;

        /// <summary>
        /// 문구와 색을 직접 정해 띄운다. 연출이 끝나면 이 오브젝트를 파괴한다.
        /// </summary>
        /// <param name="speed">연출 배속(1 또는 2). 상승·페이드 시간을 나눈다.</param>
        /// <param name="isCrit">치명타 강조(크게·굵게·느낌표)를 붙일지.</param>
        /// <returns>연출이 끝나기까지 걸리는 시간(초). 다른 연출을 여기에 맞출 때 쓴다.</returns>
        public float Show(string text, Color color, int speed, bool isCrit = false)
        {
            if (label != null)
            {
                label.text = isCrit ? text + "!" : text;
                label.color = color;
                label.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;
            }

            // 프리팹에 저작된 크기를 기준으로 곱한다. 갓 만든 오브젝트라 이전 배율이 남아 있지 않다.
            if (isCrit) transform.localScale *= critScale;

            float dur = duration / Mathf.Max(1, speed);

            var seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(transform.position.y + riseHeight, dur).SetEase(Ease.OutQuad));
            if (label != null)
                seq.Join(DOTween.To(() => label.alpha, a => label.alpha = a, 0f, dur).SetEase(Ease.InQuad));
            seq.OnComplete(() => Destroy(gameObject));
            return dur;
        }
    }
}