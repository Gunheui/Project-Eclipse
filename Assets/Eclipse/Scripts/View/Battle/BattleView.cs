using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eclipse.Presentation;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// 전투 화면의 루트 View. <see cref="BattleViewModel"/>을 HUD에 바인딩하고 턴 루프를 구동한다.
    /// 상주 씬에서 방마다 새 뷰모델이 오므로 Bind/ClearBattle로 재바인딩한다 — 런 진행은
    /// ChapterRunDriver가 소유하고, 이 뷰는 전투 한 판의 표시·입력만 담당한다.
    /// 스킬 버튼은 행동하는 아군(ActingCombatant)이 정해질 때만 활성화해 클릭을 Submit으로 넘긴다.
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        [Header("Battlers")]
        [SerializeField] private BattlerView[] allyBattlers;
        [SerializeField] private BattlerView[] enemyBattlers;

        [Header("Unit plates")]
        [SerializeField] private CombatantPlateView[] allyPlates;
        [SerializeField] private CombatantPlateView[] enemyPlates;

        [Header("Skill buttons")]
        [SerializeField] private Button[] skillButtons;
        [SerializeField] private TMP_Text[] skillLabels;
        [SerializeField] private GameObject[] skillCooldownOverlays;
        [SerializeField] private TMP_Text[] skillCooldownLabels;
        [SerializeField] private Image[] skillIcons;
        [SerializeField] private SkillTooltipTrigger[] skillTooltipTriggers;
        [SerializeField] private SkillTooltip skillTooltip;

        [Header("Turn timeline")]
        [SerializeField] private TurnTimelineView turnTimeline;

        [Header("Buff detail")]
        [SerializeField] private BattleBuffPanelView buffPanel;

        [Header("Controls")]
        [SerializeField] private Button exitButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private TMP_Text autoLabel;
        [SerializeField] private Button speedButton;
        [SerializeField] private TMP_Text speedLabel;

        private BattleViewModel _viewModel;

        // 방마다 갈리는 뷰모델 구독. Bind에서 다시 채우고 ClearBattle에서 비운다.
        private readonly CompositeDisposable _vmBindings = new();

        // 연출 배속(1 또는 2). View 소유 — 계산엔 무관하고 배틀러 트윈·턴 대기 시간만 나눈다.
        private int _speedMultiplier = 1;

        // 조준 모드 상태. 스킬 탭으로 대기 중인 스킬과 그때 계산한 유효 타겟 집합. null이면 조준 중이 아니다.
        private SkillSlotViewModel _pendingSkill;
        private IReadOnlyList<CombatantViewModel> _validTargets;

        /// <summary> 나가기 버튼이 눌리면 발화한다. 런 포기 처리(패배 보고)는 드라이버가 소유한다. </summary>
        public event Action ExitRequested;

        /// <summary>
        /// 정적 입력(버튼) 구독을 인스턴스 수명에 1회만 묶는다. 뷰모델 상태 구독은 Bind에서 따로 건다.
        /// </summary>
        private void Start()
        {
            for (int i = 0; i < skillButtons.Length; i++)
            {
                int index = i;
                skillButtons[i].OnClickAsObservable()
                    .Subscribe(_ => OnSkillClicked(index))
                    .AddTo(this);
            }

            autoButton.OnClickAsObservable()
                .Subscribe(_ => { if (_viewModel != null) _viewModel.AutoMode.Value = !_viewModel.AutoMode.Value; })
                .AddTo(this);

            exitButton.OnClickAsObservable()
                .Subscribe(_ => ExitRequested?.Invoke())
                .AddTo(this);

            if (speedButton != null)
                speedButton.OnClickAsObservable()
                    .Subscribe(_ => ToggleSpeed())
                    .AddTo(this);

            UpdateSpeedLabel();
        }

        /// <summary>
        /// 새 전투 뷰모델을 HUD 전체에 바인딩한다. 이전 방의 구독은 정리된다.
        /// </summary>
        public void Bind(BattleViewModel viewModel)
        {
            _vmBindings.Clear();
            _viewModel = viewModel;

            BindBattlers();
            BindPlates();
            if (turnTimeline != null) turnTimeline.Bind(viewModel);

            viewModel.AutoMode
                .Subscribe(on =>
                {
                    if (on) ExitTargeting(); // 오토 전환 시 조준 UI 정리(대기 턴은 엔진이 오토 결정으로 해제)
                    if (autoLabel != null) autoLabel.text = on ? "AUTO ●" : "AUTO ○";
                })
                .AddTo(_vmBindings);

            viewModel.ActingCombatant
                .Subscribe(OnActingCombatantChanged)
                .AddTo(_vmBindings);
        }

        /// <summary> 나가기 버튼을 잠그거나 푼다. 입력을 받지 않는 연출 구간에 잠근다. </summary>
        public void SetExitEnabled(bool on) => exitButton.interactable = on;

        /// <summary>
        /// 이번 전투에 나온 적들의 자리. 재화 드랍이 스폰 위치로 쓴다.
        /// </summary>
        /// <returns>빈 슬롯은 이미 비활성이라 제외된다. <see cref="ClearBattle"/> 뒤에는 빈 목록이다.</returns>
        public IReadOnlyList<Vector3> EnemyPositions()
            => enemyBattlers.Where(b => b.gameObject.activeSelf).Select(b => b.transform.position).ToList();

        /// <summary> 바인딩을 해제하고 배틀러·플레이트를 비운다. 방 전환 사이(재조립 전)에 부른다. </summary>
        public void ClearBattle()
        {
            _vmBindings.Clear();
            _viewModel = null;
            ExitTargeting();
            CloseBuffPanel();
            foreach (var b in allyBattlers.Concat(enemyBattlers)) b.Clear();
            foreach (var p in allyPlates.Concat(enemyPlates)) p.Clear();
        }

        /// <summary>
        /// 바인딩된 전투를 끝까지 구동한다. 매 턴 배틀러 연출이 끝날 때까지 기다린다.
        /// </summary>
        public UniTask RunBoundBattleAsync(CancellationToken ct)
            => _viewModel.RunBattleAsync(PlayTurnAnimationAsync, ct);

        /// <summary>
        /// 전장 배틀러를 소속·슬롯으로 유닛 VM에 연결한다. 대응 유닛이 없는 앵커는 숨긴다.
        /// </summary>
        private void BindBattlers()
        {
            AssignBattlers(allyBattlers, isAlly: true);
            AssignBattlers(enemyBattlers, isAlly: false);
        }

        private void AssignBattlers(BattlerView[] battlers, bool isAlly)
        {
            for (int slot = 0; slot < battlers.Length; slot++)
            {
                var unit = FindUnit(isAlly, slot);
                if (unit != null) battlers[slot].Bind(unit, () => _speedMultiplier, OnUnitTapped, OnUnitHovered);
                else battlers[slot].Clear();
            }
        }

        /// <summary>
        /// 이번 턴에 시작된 배틀러 연출이 모두 끝날 때까지 기다린다. VM 루프가 매 턴 이 함수를 await 한다.
        /// </summary>
        private async UniTask PlayTurnAnimationAsync(CancellationToken ct)
        {
            var animations = allyBattlers.Concat(enemyBattlers).Select(b => b.WaitForAnimation());
            await UniTask.WhenAll(animations).AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 플레이트를 소속·슬롯으로 유닛 VM에 연결한다. 대응 유닛이 없는 플레이트는 숨긴다.
        /// </summary>
        private void BindPlates()
        {
            AssignPlates(allyPlates, isAlly: true);
            AssignPlates(enemyPlates, isAlly: false);
        }

        private void AssignPlates(CombatantPlateView[] plates, bool isAlly)
        {
            for (int slot = 0; slot < plates.Length; slot++)
            {
                var unit = FindUnit(isAlly, slot);
                if (unit != null) plates[slot].Bind(unit, OnUnitTapped);
                else plates[slot].Clear();
            }
        }

        private CombatantViewModel FindUnit(bool isAlly, int slot)
        {
            if (_viewModel == null) return null;
            foreach (var unit in _viewModel.Combatants)
                if (unit.IsAlly == isAlly && unit.SlotIndex == slot)
                    return unit;
            return null;
        }

        private void ToggleSpeed()
        {
            _speedMultiplier = _speedMultiplier == 1 ? 2 : 1;
            UpdateSpeedLabel();
        }

        private void UpdateSpeedLabel()
        {
            if (speedLabel != null) speedLabel.text = _speedMultiplier + "x";
        }

        /// <summary>
        /// 행동자가 정해지면 그 유닛의 스킬로 버튼을 채우고 플레이트를 강조한다.
        /// </summary>
        /// <param name="unit">null이면(적 턴·오토) 버튼을 잠근다.</param>
        private void OnActingCombatantChanged(CombatantViewModel unit)
        {
            ExitTargeting(); // 행동자가 바뀌면(턴 종료·적 턴·오토) 이전 조준 상태를 정리한다
            if (skillTooltip != null) skillTooltip.Hide(); // 떠 있던 툴팁도 함께 정리(잔상 방지)
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
                    {
                        skillIcons[i].sprite = slot.Skill.icon;
                        skillIcons[i].enabled = true; // 빈 슬롯 처리로 꺼졌을 수 있어 되살린다
                    }
                    SetTooltipContent(i, slot.Skill.displayName, slot.Skill.description);

                    if (skillButtons[i].image != null) skillButtons[i].image.enabled = true; // 버튼 배경 프레임 되살림

                    bool ready = slot.IsReady.CurrentValue;
                    skillButtons[i].interactable = ready;
                    ShowCooldown(i, ready ? 0 : slot.Cooldown.CurrentValue, show: !ready);
                }
                else
                {
                    // 행동 중인 아군이 없는 턴: 버튼 배경 프레임·라벨·아이콘까지 통째로 비워 잔상을 막는다
                    if (skillButtons[i].image != null) skillButtons[i].image.enabled = false;
                    if (skillLabels != null && i < skillLabels.Length)
                        skillLabels[i].text = "";
                    if (skillIcons != null && i < skillIcons.Length)
                        skillIcons[i].enabled = false;
                    SetTooltipContent(i, null, null);
                    skillButtons[i].interactable = false;
                    ShowCooldown(i, 0, show: false);
                }
            }
        }

        /// <summary>
        /// 스킬 툴팁 슬롯에 표시할 스킬명·설명을 채운다. 대응 트리거가 없으면(빈 슬롯) 조용히 넘어간다.
        /// </summary>
        private void SetTooltipContent(int index, string skillName, string description)
        {
            if (skillTooltipTriggers == null || index >= skillTooltipTriggers.Length) return;
            var trigger = skillTooltipTriggers[index];
            if (trigger == null) return;
            trigger.SkillName = skillName;
            trigger.Description = description;
        }

        private void OnSkillClicked(int index)
        {
            if (_viewModel == null) return;

            // 툴팁을 보려 꾹 눌렀다 뗀 경우(롱프레스)엔 그 릴리즈로 딸려오는 시전을 한 번 건너뛴다.
            if (skillTooltipTriggers != null && index < skillTooltipTriggers.Length
                && skillTooltipTriggers[index] != null && skillTooltipTriggers[index].ConsumeLongPress())
                return;

            var unit = _viewModel.ActingCombatant.CurrentValue;
            if (unit == null || index >= unit.Skills.Count) return;

            var slot = unit.Skills[index];
            if (!slot.IsReady.CurrentValue) return;

            // 광역·힐·자기 스킬은 지정 대상이 무시되므로 즉시 시전.
            if (!slot.NeedsManualTarget)
            {
                ExitTargeting();
                _viewModel.Submit(slot);
                return;
            }

            // 유효 타겟 판정은 도메인 규칙(적=도발/아군=생존)을 VM을 통해 그대로 받는다. View는 칠하기만 한다.
            var valid = _viewModel.ValidManualTargets(unit, slot);
            if (valid.Count == 0) { ExitTargeting(); _viewModel.Submit(slot); return; } // 후보 없으면 셀렉터 폴백에 맡긴다
            if (valid.Count == 1)                                       // 후보가 하나뿐이면 조준 생략하고 바로 지정
            {
                ExitTargeting();
                _viewModel.Submit(slot, valid[0]);
                return;
            }

            EnterTargeting(slot, valid);
        }

        /// <summary>
        /// 스킬 탭으로 조준 모드에 들어간다. 유효 타겟을 기억하고 배틀러에 대상 상태를 칠한다.
        /// 스킬을 다시(혹은 다른 스킬로) 탭하면 유효 집합을 새로 계산해 상태가 갱신된다.
        /// </summary>
        private void EnterTargeting(SkillSlotViewModel skill, IReadOnlyList<CombatantViewModel> valid)
        {
            _pendingSkill = skill;
            _validTargets = valid;
            CloseBuffPanel(); // 조준이 100% 우선한다

            // 아군 힐/버프 조준은 녹색, 적 공격 조준은 빨강 아웃라인(색 선택은 BattlerView가 소유).
            foreach (var u in _viewModel.Combatants)
                FindBattler(u)?.SetTargetState(
                    valid.Contains(u) ? TargetState.Selectable : TargetState.Ineligible,
                    skill.ManualTargetsAllies);
        }

        /// <summary>
        /// 조준 모드를 빠져나오고 모든 배틀러를 평상시 상태로 되돌린다. 조준 중이 아니어도 호출 안전(멱등).
        /// </summary>
        private void ExitTargeting()
        {
            _pendingSkill = null;
            _validTargets = null;
            if (_viewModel == null) return;
            foreach (var u in _viewModel.Combatants)
                FindBattler(u)?.SetTargetState(TargetState.None);
        }

        /// <summary>
        /// 배틀러 몸통·플레이트 탭의 공통 처리. 조준 중이고 유효 타겟일 때만 그 대상으로 스킬을 제출한다.
        /// </summary>
        private void OnUnitTapped(CombatantViewModel unit)
        {
            if (_pendingSkill == null) return;                                  // 조준 중이 아니면 무시
            if (_validTargets == null || !_validTargets.Contains(unit)) return; // 선택 불가 대상 무시

            var skill = _pendingSkill;
            ExitTargeting();
            _viewModel.Submit(skill, unit);
        }

        /// <summary>유닛에 대응하는 배틀러를 소속·슬롯으로 찾는다.</summary>
        /// <returns>대응이 없으면 null(빈 슬롯).</returns>
        private BattlerView FindBattler(CombatantViewModel unit)
        {
            var battlers = unit.IsAlly ? allyBattlers : enemyBattlers;
            return unit.SlotIndex < battlers.Length ? battlers[unit.SlotIndex] : null;
        }

        private void HighlightActing(CombatantViewModel unit)
        {
            SetActing(allyPlates, unit);
            SetActing(enemyPlates, unit);
        }

        private void SetActing(CombatantPlateView[] plates, CombatantViewModel acting)
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

        /// <summary>
        /// 배틀러 호버·롱프레스의 공통 처리. 조준이 우선이라 조준 중에는 상세를 열지 않는다.
        /// </summary>
        /// <param name="show">포인터가 올라왔으면 true, 벗어났으면 false.</param>
        private void OnUnitHovered(CombatantViewModel unit, bool show)
        {
            if (buffPanel == null || unit == null) return;
            if (show)
            {
                if (_pendingSkill != null) return;
                buffPanel.Show(unit);
            }
            else buffPanel.Close(unit);
        }

        private void CloseBuffPanel()
        {
            if (buffPanel != null) buffPanel.Close();
        }

        private void OnDestroy() => _vmBindings.Dispose();
    }
}