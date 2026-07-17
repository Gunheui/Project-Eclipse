using System;
using Eclipse.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 스테이지 목록의 셀 하나를 그리는 View. 값이 고정이라 셀 전용 ViewModel 없이
    /// StageSO를 직접 바인딩한다. 선택 강조는 상위 View가 SetSelected로 제어한다.
    /// 셀은 StageSelectView가 생성하고 Bind를 호출해 연결한다.
    /// </summary>
    public class StageCellView : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button selectButton;

        [Tooltip("선택 강조 표시(아웃라인 등). SetSelected로 켜고 끈다.")]
        [SerializeField] private GameObject selectedIndicator;

        /// <summary>
        /// 셀을 지정 스테이지에 바인딩한다. 표시값(썸네일·이름)을 즉시 대입하고,
        /// 탭 시 <paramref name="onSelected"/>에 이 스테이지를 넘긴다.
        /// 버튼 리스너는 이 GameObject 수명에 묶여 Destroy 시 함께 정리된다.
        /// 셀당 한 번만 호출하는 것을 전제로 한다(재바인딩 미지원).
        /// </summary>
        /// <param name="stage">이 셀이 표시할 스테이지 데이터.</param>
        /// <param name="onSelected">셀을 탭했을 때 호출되는 콜백(선택 처리는 목록 View가 담당).</param>
        public void Bind(StageSO stage, Action<StageSO> onSelected)
        {
            thumbnail.sprite = stage.thumbnail;
            nameText.text = stage.displayName;
            selectButton.onClick.AddListener(() => onSelected(stage));
            SetSelected(false);
        }

        /// <summary>선택 강조 표시를 켜거나 끈다.</summary>
        /// <param name="selected">true면 이 셀이 현재 선택 상태.</param>
        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null)
                selectedIndicator.SetActive(selected);
        }
    }
}
