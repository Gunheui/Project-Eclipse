using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 돌파 단계를 별 칸으로 그리는 위젯. 캐릭터 목록·상세·성장 화면이 같은 프리팹을 함께 쓴다.
    /// </summary>
    public class AscensionStarsView : MonoBehaviour
    {
        [SerializeField] private Image[] stars;
        [SerializeField] private Sprite filledSprite;
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private GameObject maxLabel;

        /// <summary>
        /// 돌파 단계 스트림을 별 표시에 연결한다. 구독은 GameObject 수명에 묶여 Destroy 시 자동 해지된다.
        /// 위젯당 한 번만 호출한다(재호출 시 구독이 중첩된다).
        /// </summary>
        public void Bind(ReadOnlyReactiveProperty<int> tier)
        {
            tier.Subscribe(Apply).AddTo(this);
        }

        private void Apply(int tier)
        {
            for (int i = 0; i < stars.Length; i++)
                stars[i].sprite = i < tier ? filledSprite : emptySprite;
            // MAX는 별을 대신하지 않는다. 별을 다 채운 채로 라벨을 함께 띄운다.
            if (maxLabel != null)
                maxLabel.SetActive(tier >= stars.Length);
        }
    }
}
