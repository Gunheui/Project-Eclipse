using System;
using Cysharp.Threading.Tasks;
using Eclipse.Data;
using Eclipse.Presentation;
using Eclipse.View.Infra;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Eclipse.View
{
    /// <summary>
    /// 캐릭터 상세 화면. 선택된 캐릭터의 스탯·스킬·돌파를 표시한다.
    /// 값이 변하지 않는 표시 전용 화면이라 OnEnter에서 한 번 대입하고 구독은 두지 않는다.
    /// </summary>
    public class CharacterDetailView : MonoBehaviour, IScreen
    {
        /// <summary>스킬 한 슬롯의 UI 묶음. 이름·쿨을 표시하고, 빈 슬롯이면 root를 끈다.</summary>
        [Serializable]
        private struct SkillSlot
        {
            public GameObject root;
            public TMP_Text nameText;
            public TMP_Text cooldownText;
        }

        [Header("헤더")]
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text ascensionText;

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
        [SerializeField] private SkillSlot passiveSlot;

        [Header("내비")]
        [SerializeField] private Button backButton;

        private CharacterDetailViewModel _viewModel;
        private ScreenManager _screenManager;

        /// <summary>
        /// ScreenManager가 이 화면 프리팹을 주입 생성할 때 호출한다. OnEnter보다 먼저 실행된다.
        /// </summary>
        /// <param name="viewModel">표시할 캐릭터 상세 ViewModel(컨테이너가 주입).</param>
        /// <param name="screenManager">뒤로가기로 이 화면을 되돌리는 데 사용한다.</param>
        [Inject]
        public void Construct(CharacterDetailViewModel viewModel, ScreenManager screenManager)
        {
            _viewModel = viewModel;
            _screenManager = screenManager;
        }

        /// <summary>
        /// 화면이 전면에 설 때(주입 완료 후) 호출된다. ViewModel의 표시 값을 UI에 한 번 대입하고
        /// 뒤로가기 버튼을 배선한다. 값이 정적이라 구독은 만들지 않는다.
        /// </summary>
        /// <returns>등장 처리 완료를 알리는 UniTask(연출이 없어 즉시 완료).</returns>
        public UniTask OnEnter()
        {
            portrait.sprite = _viewModel.Portrait;
            nameText.text = _viewModel.DisplayName;
            rarityText.text = $"★ {_viewModel.Rarity}";
            roleText.text = _viewModel.Role.ToString();
            levelText.text = $"Lv. {_viewModel.Level}";
            ascensionText.text = $"돌파 {_viewModel.AscensionTier}";

            var stats = _viewModel.CurrentStats;
            hpText.text = $"HP  {stats.hp}";
            atkText.text = $"ATK  {stats.atk}";
            defText.text = $"DEF  {stats.def}";
            spdText.text = $"SPD  {stats.spd}";
            critRateText.text = $"치명확률  {stats.critRate:P0}";
            critDamageText.text = $"치명배율  {stats.critDamage:0.##}x";

            BindSkill(basicSlot, _viewModel.BasicSkill);
            BindSkill(normalSlot, _viewModel.NormalSkill);
            BindSkill(ultimateSlot, _viewModel.UltimateSkill);
            BindSkill(passiveSlot, _viewModel.PassiveSkill);

            backButton.onClick.AddListener(() => _screenManager.Pop().Forget());

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 화면이 스택에서 제거될 때 호출된다. 이 화면 전용으로 생성된 ViewModel(Transient)을
        /// 정리한다 — 정리하지 않으면 컨테이너 루트 스코프에 계속 매달려 회수되지 않는다.
        /// </summary>
        /// <returns>퇴장 처리 완료를 알리는 UniTask.</returns>
        public UniTask OnExit()
        {
            _viewModel.Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>한 스킬 슬롯을 채운다. skill이 null이면 슬롯을 숨긴다.</summary>
        /// <param name="slot">채울 슬롯 UI.</param>
        /// <param name="skill">표시할 스킬 정의. null이면 빈 슬롯(패시브 없음).</param>
        private static void BindSkill(SkillSlot slot, SkillSO skill)
        {
            if (skill == null)
            {
                slot.root.SetActive(false);
                return;
            }

            slot.root.SetActive(true);
            slot.nameText.text = skill.displayName;
            slot.cooldownText.text = skill.cooldownTurns == 0 ? "-" : $"CD {skill.cooldownTurns}";
        }
    }
}
