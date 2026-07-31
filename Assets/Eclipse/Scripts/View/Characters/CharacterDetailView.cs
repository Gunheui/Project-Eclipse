using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using Eclipse.View.Theme;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 캐릭터 상세 화면. 선택된 캐릭터의 스탯·스킬·돌파를 표시한다.
    /// </summary>
    public class CharacterDetailView : MonoBehaviour, IScreen
    {
        /// <summary>스킬 한 슬롯의 UI 묶음. 이름·쿨·스킬레벨을 표시하고, 빈 슬롯이면 root를 끈다.</summary>
        [Serializable]
        private struct SkillSlot
        {
            public GameObject root;
            public Image icon;
            public TMP_Text nameText;
            public TMP_Text cooldownText;
            public TMP_Text levelText;
        }

        [Header("헤더")]
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private AscensionStarsView ascensionStars;

        [Header("스탯")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text atkText;
        [SerializeField] private TMP_Text defText;
        [SerializeField] private TMP_Text spdText;
        [SerializeField] private TMP_Text critRateText;
        [SerializeField] private TMP_Text critDamageText;

        [Header("스킬")]
        [SerializeField] private SkillSlot basicSlot;
        [SerializeField] private SkillSlot normalSlot;
        [SerializeField] private SkillSlot ultimateSlot;

        [Header("탭")]
        [SerializeField] private ThemedTab basicTab;
        [SerializeField] private ThemedTab growthTab;
        [SerializeField] private GameObject basicPanel;
        [SerializeField] private GameObject growthPanel;
        [SerializeField] private GrowthView growthView;

        [Header("내비")]
        [SerializeField] private Button backButton;

        private CharacterDetailViewModel _viewModel;
        private GrowthViewModel _growthViewModel;
        private ScreenManager _screenManager;

        /// <summary> ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다. </summary>
        [Inject]
        public void Construct(CharacterDetailViewModel viewModel, GrowthViewModel growthViewModel,
            ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _growthViewModel = growthViewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 전면에 설 때 호출된다. 표시 값을 구독하고 탭 전환·뒤로가기를 연결한다.
        /// 성장 탭에서 값이 바뀌어도 여기를 다시 타지 않으므로 갱신은 구독이 맡는다.
        /// </summary>
        public UniTask OnEnter()
        {
            ApplyPortraitAsync(this.GetCancellationTokenOnDestroy()).Forget();
            nameText.text = _viewModel.DisplayName;
            // 등급은 R/SR/SSR 글자만 쓴다. 별 기호를 붙이면 옆의 돌파 별과 뜻이 겹친다.
            rarityText.text = _viewModel.Rarity.ToString();
            roleText.text = _viewModel.Role.ToString();

            _viewModel.Level
                .Subscribe(level => levelText.text = $"Lv. {level}")
                .AddTo(this);
            ascensionStars.Bind(_viewModel.AscensionTier);
            _viewModel.CurrentStats
                .Subscribe(stats =>
                {
                    hpText.text = $"HP  {stats.hp}";
                    atkText.text = $"ATK  {stats.atk}";
                    defText.text = $"DEF  {stats.def}";
                    spdText.text = $"SPD  {stats.spd}";
                    critRateText.text = $"치명확률  {stats.critRate:P0}";
                    critDamageText.text = $"치명배율  {stats.critDamage:0.##}x";
                })
                .AddTo(this);

            BindSkill(basicSlot, _viewModel.BasicSkill, 0);
            BindSkill(normalSlot, _viewModel.NormalSkill, 1);
            BindSkill(ultimateSlot, _viewModel.UltimateSkill, 2);

            growthView.Bind(_growthViewModel);
            basicTab.onClick.AddListener(() => _viewModel.SelectedTab.Value = DetailTab.Basic);
            growthTab.onClick.AddListener(() => _viewModel.SelectedTab.Value = DetailTab.Growth);
            _viewModel.SelectedTab
                .Subscribe(ApplyTab)
                .AddTo(this);

            backButton.onClick.AddListener(() => _screenManager.Pop().Forget());

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 이 화면 전용 Transient ViewModel을 성장 탭 것까지 정리한다.
        /// 정리하지 않으면 컨테이너 루트 스코프에 계속 매달려 회수되지 않는다.
        /// </summary>
        public UniTask OnExit()
        {
            growthView.Dispose();
            _viewModel.Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>선택된 탭에 맞춰 우측 패널을 교체한다. 좌측 초상과 이름 카드는 탭과 무관하게 남는다.</summary>
        private void ApplyTab(DetailTab tab)
        {
            basicTab.IsSelected = tab == DetailTab.Basic;
            growthTab.IsSelected = tab == DetailTab.Growth;
            basicPanel.SetActive(tab == DetailTab.Basic);
            growthPanel.SetActive(tab == DetailTab.Growth);
        }

        /// <summary>초상 스프라이트를 로드해 대입한다. 로드가 비동기라도 나머지 표시를 막지 않는다.</summary>
        private async UniTaskVoid ApplyPortraitAsync(CancellationToken ct)
        {
            portrait.sprite = await _viewModel.LoadPortraitAsync(ct);
        }

        /// <summary>한 스킬 슬롯을 채운다. skill이 null이면(빈 슬롯) 숨긴다.</summary>
        private void BindSkill(SkillSlot slot, SkillSO skill, int slotIndex)
        {
            if (skill == null)
            {
                slot.root.SetActive(false);
                return;
            }

            slot.root.SetActive(true);
            slot.icon.sprite = skill.icon;
            slot.nameText.text = skill.displayName;
            slot.cooldownText.text = skill.cooldownTurns == 0 ? "-" : $"CD {skill.cooldownTurns}";
            _viewModel.SkillLevelAt(slotIndex)
                .Subscribe(level => slot.levelText.text = $"Lv. {level}")
                .AddTo(this);
        }
    }
}
