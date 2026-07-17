using TMPro;
using UnityEngine;

namespace Eclipse.View
{
    /// <summary>
    /// 스킬명·설명을 보여주는 경량 툴팁. 스킬 버튼 3개가 하나를 공유하며, 각 버튼의
    /// <see cref="SkillTooltipTrigger"/>가 hover·롱프레스를 감지해 Show/Hide로 구동한다.
    /// 표시 위치는 씬에 고정(가운데)이라 코드는 텍스트 채우기와 표시만 담당한다.
    /// </summary>
    public class SkillTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;

        private void Awake() => Hide();

        /// <summary>텍스트를 채우고 툴팁을 켠다(위치는 씬 고정).</summary>
        public void Show(string title, string body)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (bodyLabel != null) bodyLabel.text = body;
            canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
