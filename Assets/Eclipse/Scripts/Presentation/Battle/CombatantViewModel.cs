using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Domain;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 전투 HUD의 유닛 명판 하나에 대응하는 ViewModel. 이름·소속·슬롯·최대 HP는 고정이고,
    /// 현재 HP·생존 여부는 엔진이 매 턴 계산하므로 턴 신호에서 파생한 리액티브 프로퍼티로 노출한다.
    /// </summary>
    public sealed class CombatantViewModel
    {
        // 이 명판이 표시하는 도메인 유닛(HP·스킬 상태의 원천). Submit 시 타겟으로 되돌려 넘긴다.
        internal Combatant Model { get; }

        // 이 유닛이 자기 턴에 행동했음을 알리는 신호(사용한 스킬을 실어 나른다). 배틀러가 구독해 시전 연출을 재생한다.
        private readonly Subject<SkillSO> _acted = new();

        // 이 유닛이 스킬 대상이 됐음을 알리는 신호(원인 스킬을 실어 나른다). 배틀러가 구독해 피격 연출을 재생한다.
        private readonly Subject<SkillSO> _hit = new();

        /// <param name="model">이 명판이 표시할 도메인 유닛.</param>
        /// <param name="stateChanged">뷰가 상태를 다시 읽어야 할 때 발화하는 신호. 이 신호에 맞춰 HP를 다시 읽는다.</param>
        /// <param name="battler">전장에 세울 배틀러 스프라이트(아군 초상·적 배틀러). 없으면 null.</param>
        public CombatantViewModel(Combatant model, Observable<Unit> stateChanged, Sprite battler = null)
        {
            Model = model;
            BattlerSprite = battler;
            CurrentHp = stateChanged
                .Select(_ => model.CurrentHp)
                .ToReadOnlyReactiveProperty(model.CurrentHp);
            IsAlive = stateChanged
                .Select(_ => model.IsAlive)
                .ToReadOnlyReactiveProperty(model.IsAlive);
            Skills = model.Skills
                .Select(s => new SkillSlotViewModel(s, stateChanged))
                .ToList();
        }

        /// <summary> 표시 이름. 불변. </summary>
        public string Name => Model.DisplayName;

        /// <summary> 아군이면 true, 적이면 false. View는 Domain의 Team enum을 못 보므로 bool로 노출. </summary>
        public bool IsAlly => Model.Team == Team.Ally;

        /// <summary> 명판 배치 순서(0~3). 불변. </summary>
        public int SlotIndex => Model.SlotIndex;

        /// <summary> 최대 HP. HP 바의 분모. 불변. </summary>
        public int MaxHp => Model.MaxHp;

        /// <summary> 전장에 세울 배틀러 스프라이트. 아트는 프레젠테이션 경계에서 실려 온다(도메인은 아트를 모름). </summary>
        public Sprite BattlerSprite { get; }

        /// <summary> 현재 HP. HP 바 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> CurrentHp { get; }

        /// <summary> 생존 여부. 사망 연출·명판 흐리기 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<bool> IsAlive { get; }

        /// <summary> 이 유닛이 행동할 때 사용 스킬과 함께 발화. 배틀러 시전 연출 트리거. </summary>
        public Observable<SkillSO> Acted => _acted;

        /// <summary> 이 유닛이 스킬 대상이 될 때 원인 스킬과 함께 발화. 배틀러 피격 연출 트리거. </summary>
        public Observable<SkillSO> Hit => _hit;

        /// <summary> 이 유닛의 스킬 슬롯들(기본+액티브). 행동자일 때 스킬 버튼으로 쓴다. </summary>
        public IReadOnlyList<SkillSlotViewModel> Skills { get; }

        /// <summary>
        /// Acted 신호를 발화한다. 이번 턴 행동자에 대해 BattleViewModel이 호출하며,
        /// Acted를 구독한 배틀러가 시전 연출을 재생한다.
        /// </summary>
        /// <param name="skill">이번 턴에 사용한 스킬(시전 이펙트 선택에 쓴다).</param>
        internal void RaiseActed(SkillSO skill) => _acted.OnNext(skill);

        /// <summary>
        /// Hit 신호를 발화한다. 이번 턴 스킬 대상마다 BattleViewModel이 호출하며,
        /// Hit를 구독한 배틀러가 피격 연출을 재생한다.
        /// </summary>
        /// <param name="skill">이 대상을 맞힌 스킬(피격 이펙트 선택에 쓴다).</param>
        internal void RaiseHit(SkillSO skill) => _hit.OnNext(skill);

        /// <summary> 파생 프로퍼티와 스킬 슬롯의 구독을 해지한다. 소유자(BattleViewModel)가 호출한다. </summary>
        public void Dispose()
        {
            CurrentHp.Dispose();
            IsAlive.Dispose();
            _acted.Dispose();
            _hit.Dispose();
            foreach (var slot in Skills) slot.Dispose();
        }
    }
}