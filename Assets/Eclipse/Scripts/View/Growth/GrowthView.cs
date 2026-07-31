using System;
using Eclipse.Presentation;
using Eclipse.View.Theme;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 캐릭터 상세 화면의 성장 탭. 레벨업·스킬 강화 두 세부 탭을 조작할 수 있고, 돌파는 잠긴 채로 내용만 보여 준다.
    /// 자체 화면이 아니라 상세 화면이 소유하는 패널이라 대상 캐릭터·뒤로가기는 상세 화면 쪽에 있다.
    /// </summary>
    public class GrowthView : MonoBehaviour
    {
        /// <summary>스킬 강화 한 줄의 UI 묶음. 빈 슬롯이면 root를 끈다.</summary>
        [Serializable]
        private struct SkillRow
        {
            public GameObject root;
            public Image icon;
            public TMP_Text nameText;
            public TMP_Text levelText;
            public TMP_Text costText;
            public TMP_Text powerText;
            public Button enhanceButton;
            public TMP_Text reasonText;
        }

        [Header("탭")]
        [SerializeField] private ThemedTab levelUpTab;
        [SerializeField] private ThemedTab skillEnhanceTab;
        [SerializeField] private ThemedTab ascensionTab;
        [SerializeField] private GameObject levelUpPanel;
        [SerializeField] private GameObject skillEnhancePanel;
        [SerializeField] private GameObject ascensionPanel;

        [Header("레벨업")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text levelUpCostText;
        [SerializeField] private TMP_Text hpPreviewText;
        [SerializeField] private TMP_Text atkPreviewText;
        [SerializeField] private TMP_Text defPreviewText;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpReasonText;

        [Header("스킬 강화")]
        [SerializeField] private SkillRow basicRow;
        [SerializeField] private SkillRow normalRow;
        [SerializeField] private SkillRow ultimateRow;

        [Header("돌파(잠금)")]
        [SerializeField] private AscensionStarsView ascensionStars;

        private GrowthViewModel _viewModel;

        /// <summary> 세부 탭·패널을 연결하고 표시 값 구독을 시작한다. 상세 화면이 열릴 때 한 번 호출한다. </summary>
        public void Bind(GrowthViewModel viewModel)
        {
            _viewModel = viewModel;

            levelUpTab.onClick.AddListener(() => _viewModel.SelectedTab.Value = GrowthTab.LevelUp);
            skillEnhanceTab.onClick.AddListener(() => _viewModel.SelectedTab.Value = GrowthTab.SkillEnhance);
            ascensionTab.onClick.AddListener(() => _viewModel.SelectedTab.Value = GrowthTab.Ascension);
            _viewModel.SelectedTab
                .Subscribe(ApplyTab)
                .AddTo(this);

            BindLevelUpPanel();
            BindSkillRow(basicRow, 0);
            BindSkillRow(normalRow, 1);
            BindSkillRow(ultimateRow, 2);
            ascensionStars.Bind(_viewModel.AscensionTier);
        }

        /// <summary> 이 패널 전용 Transient ViewModel을 정리한다. 상세 화면이 스택에서 빠질 때 호출한다. </summary>
        public void Dispose()
        {
            // OnEnter가 Bind 전에 예외로 끊기면 ViewModel이 비어 있다. 여기서 터지면 상세 화면 쪽 정리까지 막힌다.
            _viewModel?.Dispose();
        }

        private void ApplyTab(GrowthTab tab)
        {
            levelUpTab.IsSelected = tab == GrowthTab.LevelUp;
            skillEnhanceTab.IsSelected = tab == GrowthTab.SkillEnhance;
            ascensionTab.IsSelected = tab == GrowthTab.Ascension;
            levelUpPanel.SetActive(tab == GrowthTab.LevelUp);
            skillEnhancePanel.SetActive(tab == GrowthTab.SkillEnhance);
            ascensionPanel.SetActive(tab == GrowthTab.Ascension);
        }

        private void BindLevelUpPanel()
        {
            _viewModel.Level
                .Subscribe(level => levelText.text = $"Lv. {level} / {_viewModel.MaxLevel}")
                .AddTo(this);
            _viewModel.LevelUpCost
                .Subscribe(cost => levelUpCostText.text = cost == null ? "--" : $"골드 {cost.Value:N0}")
                .AddTo(this);
            _viewModel.LevelStatsPreview
                .Subscribe(preview =>
                {
                    hpPreviewText.text = FormatPreview("HP", preview.current.hp, preview.next?.hp);
                    atkPreviewText.text = FormatPreview("ATK", preview.current.atk, preview.next?.atk);
                    defPreviewText.text = FormatPreview("DEF", preview.current.def, preview.next?.def);
                })
                .AddTo(this);
            _viewModel.LevelUpState
                .Subscribe(state =>
                {
                    levelUpButton.interactable = state == LevelUpResult.Success;
                    levelUpReasonText.text = state switch
                    {
                        LevelUpResult.MaxLevel => "최대 레벨",
                        LevelUpResult.InsufficientGold => "골드 부족",
                        _ => string.Empty,
                    };
                })
                .AddTo(this);
            levelUpButton.onClick.AddListener(() => _viewModel.LevelUp());
        }

        private void BindSkillRow(SkillRow row, int slotIndex)
        {
            var slot = _viewModel.SlotAt(slotIndex);
            if (slot == null)
            {
                row.root.SetActive(false);
                return;
            }

            row.root.SetActive(true);
            row.icon.sprite = slot.Definition.icon;
            row.nameText.text = slot.Definition.displayName;

            slot.Level
                .Subscribe(level => row.levelText.text = $"Lv. {level}")
                .AddTo(this);
            slot.GoldCost
                .Subscribe(cost => row.costText.text = cost == null
                    ? "--"
                    : $"골드 {cost.Value:N0} / 교본 {_viewModel.SkillManualCost}")
                .AddTo(this);
            slot.PowerPreview
                .Subscribe(preview =>
                {
                    string next = preview.next == null ? "--" : $"{preview.next.Value:0.00}배";
                    row.powerText.text = $"위력 {preview.current:0.00}배 → {next}";
                })
                .AddTo(this);
            slot.EnhanceState
                .Subscribe(state =>
                {
                    row.enhanceButton.interactable = state == SkillEnhanceResult.Success;
                    row.reasonText.text = state switch
                    {
                        SkillEnhanceResult.MaxSkillLevel => "최대 레벨",
                        SkillEnhanceResult.InsufficientCurrency => "재화 부족",
                        _ => string.Empty,
                    };
                })
                .AddTo(this);
            row.enhanceButton.onClick.AddListener(() => _viewModel.EnhanceSkill(slotIndex));
        }

        /// <summary>"HP  780 → 835" 형태로 만든다. 올릴 곳이 없으면(만렙) 다음 값 자리를 두 줄표로 채운다.</summary>
        private static string FormatPreview(string label, int current, int? next)
            => next == null ? $"{label}  {current:N0} → --" : $"{label}  {current:N0} → {next.Value:N0}";
    }
}
