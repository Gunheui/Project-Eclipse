using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 전투 화면의 루트 View. <see cref="BattleViewModel"/>을 HUD에 바인딩하고 턴 루프를 구동한다.
    /// 명판·행동 수·결과는 뷰모델의 리액티브 프로퍼티를 구독해 갱신하고, 스킬 버튼은 지금 행동하는
    /// 아군(ActingUnit)이 정해질 때만 활성화해 클릭을 뷰모델의 Submit으로 넘긴다.
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        [Header("Unit plates")]
        [SerializeField] private BattleUnitPlateView[] allyPlates;
        [SerializeField] private BattleUnitPlateView[] enemyPlates;

        [Header("Skill buttons")]
        [SerializeField] private Button[] skillButtons;
        [SerializeField] private TMP_Text[] skillLabels;
        [SerializeField] private GameObject[] skillCooldownOverlays;
        [SerializeField] private TMP_Text[] skillCooldownLabels;
        [SerializeField] private Image[] skillIcons;

        [Header("Controls")]
        [SerializeField] private Button exitButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private TMP_Text autoLabel;
        [SerializeField] private TMP_Text actionCounterLabel;
        [SerializeField] private TMP_Text resultLabel;

        private BattleViewModel _viewModel;

        [Inject]
        public void Construct(BattleViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        private void Start()
        {
            BindPlates();
            BindControls();
            BindSkillButtons();

            // 바인딩을 모두 건 뒤에 루프를 시작한다. 화면이 파괴되면 토큰이 취소돼 루프가 끊긴다.
            _viewModel.StartAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // 명판 8개를 소속·슬롯으로 유닛 VM에 연결한다. 대응 유닛이 없는 명판은 숨긴다.
        private void BindPlates()
        {
            AssignPlates(allyPlates, isAlly: true);
            AssignPlates(enemyPlates, isAlly: false);
        }

        private void AssignPlates(BattleUnitPlateView[] plates, bool isAlly)
        {
            for (int slot = 0; slot < plates.Length; slot++)
            {
                var unit = FindUnit(isAlly, slot);
                if (unit != null) plates[slot].Bind(unit);
                else plates[slot].Clear();
            }
        }

        private BattleUnitViewModel FindUnit(bool isAlly, int slot)
        {
            foreach (var unit in _viewModel.Units)
                if (unit.IsAlly == isAlly && unit.SlotIndex == slot)
                    return unit;
            return null;
        }

        // 행동 수·결과·오토 토글·나가기를 뷰모델에 잇는다.
        private void BindControls()
        {
            _viewModel.ActionCount
                .Subscribe(n => actionCounterLabel.text = n.ToString())
                .AddTo(this);

            _viewModel.Result
                .Subscribe(ShowResult)
                .AddTo(this);

            // 오토는 버튼 클릭으로 뒤집고, 뷰모델 값이 바뀌면 라벨을 갱신한다.
            autoButton.OnClickAsObservable()
                .Subscribe(_ => _viewModel.AutoMode.Value = !_viewModel.AutoMode.Value)
                .AddTo(this);
            _viewModel.AutoMode
                .Subscribe(on => { if (autoLabel != null) autoLabel.text = on ? "AUTO ●" : "AUTO ○"; })
                .AddTo(this);

            exitButton.OnClickAsObservable()
                .Subscribe(_ => _viewModel.ExitAsync().Forget())
                .AddTo(this);
        }

        // 스킬 버튼 클릭을 Submit으로 잇고, 행동자(ActingUnit)가 바뀔 때마다 버튼을 다시 채운다.
        private void BindSkillButtons()
        {
            // 더블탭은 Submit이 ActingUnit을 즉시 null로 만들어 막는다(두 번째 클릭은 OnSkillClicked에서 빠짐).
            for (int i = 0; i < skillButtons.Length; i++)
            {
                int index = i;
                skillButtons[i].OnClickAsObservable()
                    .Subscribe(_ => OnSkillClicked(index))
                    .AddTo(this);
            }

            _viewModel.ActingUnit
                .Subscribe(OnActingUnitChanged)
                .AddTo(this);
        }

        // 행동자가 정해지면 그 유닛의 스킬로 버튼을 채우고 명판을 강조한다. null이면(적 턴·오토) 버튼을 잠근다.
        private void OnActingUnitChanged(BattleUnitViewModel unit)
        {
            HighlightActing(unit);

            for (int i = 0; i < skillButtons.Length; i++)
            {
                bool hasSkill = unit != null && i < unit.Skills.Count;
                if (hasSkill)
                {
                    var slot = unit.Skills[i];
                    if (skillLabels != null && i < skillLabels.Length)
                        skillLabels[i].text = slot.Skill.displayName;
                    if (skillIcons != null && i < skillIcons.Length && slot.Skill.icon != null)
                        skillIcons[i].sprite = slot.Skill.icon;

                    bool ready = slot.IsReady.CurrentValue;
                    skillButtons[i].interactable = ready;
                    ShowCooldown(i, ready ? 0 : slot.Cooldown.CurrentValue, show: !ready);
                }
                else
                {
                    skillButtons[i].interactable = false;
                    ShowCooldown(i, 0, show: false);
                }
            }
        }

        private void OnSkillClicked(int index)
        {
            var unit = _viewModel.ActingUnit.CurrentValue;
            if (unit == null || index >= unit.Skills.Count) return;

            var slot = unit.Skills[index];
            if (!slot.IsReady.CurrentValue) return;

            _viewModel.Submit(slot);
        }

        private void HighlightActing(BattleUnitViewModel unit)
        {
            SetActing(allyPlates, unit);
            SetActing(enemyPlates, unit);
        }

        private void SetActing(BattleUnitPlateView[] plates, BattleUnitViewModel acting)
        {
            for (int slot = 0; slot < plates.Length; slot++)
            {
                var unit = FindUnit(plates == allyPlates, slot);
                plates[slot].SetActing(unit != null && unit == acting);
            }
        }

        private void ShowCooldown(int index, int turns, bool show)
        {
            if (skillCooldownOverlays != null && index < skillCooldownOverlays.Length)
                skillCooldownOverlays[index].SetActive(show);
            if (show && skillCooldownLabels != null && index < skillCooldownLabels.Length)
                skillCooldownLabels[index].text = turns.ToString();
        }

        private void ShowResult(BattleResult result)
        {
            if (resultLabel == null) return;
            switch (result)
            {
                case BattleResult.Victory:
                    resultLabel.text = "승리";
                    resultLabel.gameObject.SetActive(true);
                    break;
                case BattleResult.Defeat:
                    resultLabel.text = "패배";
                    resultLabel.gameObject.SetActive(true);
                    break;
                default:
                    resultLabel.gameObject.SetActive(false);
                    break;
            }
        }
    }
}
