using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
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
            _goldViewModel.Gold
                .Subscribe(v => goldText.text = v.ToString()).AddTo(this);
            gachaButton.OnClickAsObservable().Subscribe(_ => _goldViewModel.SpendGold(100)).AddTo(this);
        }


        
        
    }
}