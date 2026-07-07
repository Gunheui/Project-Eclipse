using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 골드 HUD 뷰. <see cref="GoldViewModel"/>을 구독해 텍스트를 갱신하고 버튼 입력을 전달한다.
    /// </summary>
    public class GoldHudView : MonoBehaviour
    {
        [Inject]
        private GoldViewModel _goldViewModel;

        [SerializeField]
        private TMP_Text goldText;

        [SerializeField]
        private Button gachaButton;

        private void Start()
        {
            // 골드 값 변경을 텍스트에 바인딩
            _goldViewModel.Gold
                .Subscribe(v => goldText.text = v.ToString()).AddTo(this);
            // 버튼 클릭 시 골드 소모 요청
            gachaButton.OnClickAsObservable().Subscribe(_ => _goldViewModel.SpendGold(100)).AddTo(this);
        }
    }
}