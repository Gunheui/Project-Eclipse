using Cysharp.Threading.Tasks;
using Eclipse.Data.Enums;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 화면 위에 상주하는 상단 재화 HUD. <see cref="CurrencyWallet"/>의 재화 3종을 구독해 텍스트를 갱신하고,
    /// 각 재화의 ＋버튼으로 더미 증감을, 홈 버튼으로 로비 복귀를 처리한다.
    /// 특정 화면에 속하지 않으므로 씬에 상주하며 <see cref="ScreenManager"/> 스택과 독립적으로 동작한다.
    /// </summary>
    public class CurrencyHudView : MonoBehaviour
    {
        [Inject]
        private CurrencyWallet _wallet;

        [Inject]
        private ScreenManager _screens;

        [Header("재화 값 텍스트")]
        [SerializeField] private TMP_Text essenceText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text manualText;

        // 개발용 임시 재화 증감 버튼. ICurrencyService 도입 시 대체 예정.
        [Header("더미 증감 버튼")]
        [SerializeField] private Button essencePlus;
        [SerializeField] private Button goldPlus;
        [SerializeField] private Button manualPlus;

        [Header("내비게이션")]
        [SerializeField] private Button homeButton;

        private void Start()
        {
            // 재화 값 → 텍스트 바인딩 (천 단위 구분)
            _wallet.Essence.Subscribe(v => essenceText.text = v.ToString("N0")).AddTo(this);
            _wallet.Gold.Subscribe(v => goldText.text = v.ToString("N0")).AddTo(this);
            _wallet.Manual.Subscribe(v => manualText.text = v.ToString("N0")).AddTo(this);

            // ＋버튼 → 개발용 임시 재화 증감(구독 반응 관측용. ICurrencyService로 대체 예정)
            essencePlus.OnClickAsObservable().Subscribe(_ => _wallet.Grant(CurrencyType.Essence, 300)).AddTo(this);
            goldPlus.OnClickAsObservable().Subscribe(_ => _wallet.Grant(CurrencyType.Gold, 5000)).AddTo(this);
            manualPlus.OnClickAsObservable().Subscribe(_ => _wallet.Grant(CurrencyType.Manual, 1)).AddTo(this);

            // 홈 버튼 → 로비까지 스택 되감기 (Push는 이미 스택에 있으면 그 위를 pop한다)
            homeButton.OnClickAsObservable().Subscribe(_ => _screens.Push(ScreenId.Lobby).Forget()).AddTo(this);
        }
    }
}
