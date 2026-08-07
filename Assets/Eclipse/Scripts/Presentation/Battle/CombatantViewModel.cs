using System;
using System.Collections.Generic;
using System.Linq;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;
using R3;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 플레이트 아이콘 행과 상세 패널이 표시할 지속 효과 하나. View가 Domain의 StatusEffect를 직접
    /// 못 보므로 표시에 필요한 값만 담아 넘긴다.
    /// </summary>
    public readonly struct ActiveEffect
    {
        public EffectType Type { get; }

        /// <summary> 버프·디버프가 바꾸는 스탯. 축이 하나로 정해지지 않거나 그 외 타입이면 None. </summary>
        public StatType Stat { get; }

        /// <summary> 남은 지속턴. -1이면 상시(턴 라벨을 표시하지 않는다). </summary>
        public int RemainingTurns { get; }

        /// <summary>
        /// 효과의 세기. 타입마다 단위가 다르다 — 버프·디버프는 변화율(0.3 = 30%), 도트·리젠은 틱당 HP,
        /// 실드는 남은 흡수량이고 그 외 타입은 0이다.
        /// </summary>
        public float Magnitude { get; }

        public ActiveEffect(EffectType type, StatType stat, int remainingTurns, float magnitude = 0f)
        {
            Type = type;
            Stat = stat;
            RemainingTurns = remainingTurns;
            Magnitude = magnitude;
        }
    }

    /// <summary>
    /// 배틀러가 띄울 효과 결과 하나. View가 Domain의 StatusEffect를 직접 못 보듯 EffectResult도 못 보므로,
    /// 표시에 쓰는 값만 옮겨 담는다. 결과를 받은 유닛은 이미 VM 단계에서 갈렸으므로 싣지 않는다.
    /// </summary>
    public readonly struct EffectDisplay
    {
        public EffectType Type { get; }

        /// <summary> 화면에 띄울 크기. 수치가 없는 효과는 0이라 숫자가 뜨지 않는다. </summary>
        public int Amount { get; }

        /// <summary> 실드가 이 피해를 조금이라도 막았는지. 숫자 색을 실드색으로 바꾼다. </summary>
        public bool Shielded { get; }

        /// <summary> 치명타였는지. 피해가 아닌 효과는 항상 false. </summary>
        public bool IsCrit { get; }

        public EffectDisplay(EffectResult result)
        {
            Type = result.Type;
            Amount = result.Amount;
            Shielded = result.Shielded;
            IsCrit = result.IsCrit;
        }
    }

    /// <summary>
    /// 전투 유닛 하나에 대응하는 ViewModel. 이름·소속·슬롯·최대 HP는 고정이고,
    /// 현재 HP·생존 여부는 턴 신호에서 파생한 리액티브 프로퍼티로 노출한다.
    /// </summary>
    public sealed class CombatantViewModel
    {
        // 이 VM이 표시하는 도메인 유닛(HP·스킬 상태의 원천). Submit 시 타겟으로 되돌려 넘긴다.
        internal Combatant Model { get; }

        // 자기 턴에 행동했음을 알리는 신호(사용한 스킬과 이번 턴 대상 포함).
        // 배틀러가 구독해 시전 연출을 재생하고, 근접이면 대상 자리로 접근한다.
        private readonly Subject<(SkillSO Skill, IReadOnlyList<CombatantViewModel> Targets)> _acted = new();

        // 스킬 대상이 됐음을 알리는 신호(원인 스킬과 결과 포함). 배틀러가 구독해 피격 연출과 숫자를 재생한다.
        private readonly Subject<(SkillSO Skill, EffectDisplay Result)> _hit = new();

        // 자기 턴 시작에 도트·리젠이 터졌음을 알리는 신호. 배틀러가 구독해 틱마다 숫자를 띄운다.
        private readonly Subject<EffectDisplay> _ticked = new();

        // 자기 턴 시작 정산이 끝났음을 알리는 신호. 배틀러가 「턴마다」 유지 이펙트를 다시 터뜨리는 시계로 쓴다.
        private readonly Subject<Unit> _turnStarted = new();

        /// <param name="runEffects">표시 전용 상시 효과(런 버프·저주). 도메인 효과가 아니라 <see cref="AllEffects"/>에만 실린다.</param>
        public CombatantViewModel(Combatant model, Observable<Unit> stateChanged, Sprite battler,
            RuntimeAnimatorController battlerAnimator, float battlerImpactTime, Sprite timelineIcon,
            MutationSO mutation, IReadOnlyList<ActiveEffect> runEffects)
        {
            Model = model;
            BattlerSprite = battler;
            BattlerAnimator = battlerAnimator;
            BattlerImpactTime = battlerImpactTime;
            TimelineIcon = timelineIcon;
            Mutation = mutation;
            CurrentHp = stateChanged
                .Select(_ => model.CurrentHp)
                .ToReadOnlyReactiveProperty(model.CurrentHp);
            IsAlive = stateChanged
                .Select(_ => model.IsAlive)
                .ToReadOnlyReactiveProperty(model.IsAlive);
            ShieldAbsorb = stateChanged
                .Select(_ => model.ShieldAbsorb)
                .ToReadOnlyReactiveProperty(model.ShieldAbsorb);
            SkillEffects = stateChanged
                .Select(_ => BuildActiveEffects(model.Effects))
                .ToReadOnlyReactiveProperty(BuildActiveEffects(model.Effects));
            AllEffects = stateChanged
                .Select(_ => BuildActiveEffects(model.Effects, runEffects))
                .ToReadOnlyReactiveProperty(BuildActiveEffects(model.Effects, runEffects));
            EffectSources = stateChanged
                .Select(_ => BuildEffectSources(model.Effects))
                .ToReadOnlyReactiveProperty(BuildEffectSources(model.Effects));
            Skills = model.Skills
                .Select(s => new SkillSlotViewModel(s, stateChanged))
                .ToList();
        }

        /// <summary> 표시 이름. 불변. </summary>
        public string Name => Model.DisplayName;

        /// <summary> 아군이면 true, 적이면 false. View는 Domain의 Team enum을 못 보므로 bool로 노출. </summary>
        public bool IsAlly => Model.Team == Team.Ally;

        /// <summary> 슬롯 순서(0~3). 불변. </summary>
        public int SlotIndex => Model.SlotIndex;

        /// <summary> 최대 HP. HP 바의 분모. 불변. </summary>
        public int MaxHp => Model.MaxHp;

        /// <summary> 전장에 세울 배틀러 스프라이트. 없으면 null. </summary>
        public Sprite BattlerSprite { get; }

        public RuntimeAnimatorController BattlerAnimator { get; }

        /// <summary> 공격 모션에서 무기가 닿는 시점(초). 배틀러가 이 시점에 타격을 알린다. </summary>
        public float BattlerImpactTime { get; }

        /// <summary> 턴 순서 타임라인 아이콘. 아군은 얼굴 크롭, 적은 배틀러 스프라이트. </summary>
        public Sprite TimelineIcon { get; }

        /// <summary> 이 적에게 붙은 침식 변이. 없으면 null(아군은 항상 null). </summary>
        public MutationSO Mutation { get; }

        /// <summary> 배틀러 스프라이트에 곱하는 색. 변이가 없으면 흰색이라 원래 색이 그대로 남는다. </summary>
        public Color Tint => Mutation != null ? Mutation.tintColor : Color.white;

        /// <summary> 지금 이 유닛의 최종 스탯. 런 버프도 전투 중 스킬 효과도 전부 반영된 값이다. </summary>
        public Stats EffectiveStats => Model.EffectiveStats;

        /// <summary> 현재 HP. HP 바 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> CurrentHp { get; }

        /// <summary> 생존 여부. 사망 연출·플레이트 흐리기 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<bool> IsAlive { get; }

        /// <summary> 남은 실드 흡수량 합. HP 바의 실드 구간 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<int> ShieldAbsorb { get; }

        /// <summary> 스킬로 걸린 지속 효과만 표시 순서로 정렬한 목록. 상세 패널 바인딩용. 턴마다 갱신. </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<ActiveEffect>> SkillEffects { get; }

        /// <summary>
        /// 스킬 지속 효과에 런 카드 항목까지 합쳐 같은 순서로 정렬한 목록. 플레이트 아이콘 행 바인딩용. 턴마다 갱신.
        /// 카드 항목은 아이콘 자리만 차지하는 표시 전용이라 이름·등급·수치가 없다.
        /// </summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<ActiveEffect>> AllEffects { get; }

        /// <summary>
        /// 지금 이 유닛에 걸려 있는 지속 효과를 건 스킬들. 배틀러가 유지 이펙트를 걷을 시점을 여기서 판단한다. 턴마다 갱신.
        /// </summary>
        public ReadOnlyReactiveProperty<IReadOnlyCollection<SkillSO>> EffectSources { get; }

        /// <summary> 이 유닛이 행동할 때 사용 스킬·이번 턴 대상과 함께 발화. 배틀러 시전 연출 트리거. </summary>
        public Observable<(SkillSO Skill, IReadOnlyList<CombatantViewModel> Targets)> Acted => _acted;

        /// <summary> 이 유닛이 스킬 대상이 될 때 원인 스킬·결과와 함께 발화. 배틀러 피격 연출 트리거. </summary>
        public Observable<(SkillSO Skill, EffectDisplay Result)> Hit => _hit;

        /// <summary> 이 유닛의 턴 시작에 도트·리젠이 터질 때 틱마다 발화. 배틀러 틱 숫자 트리거. </summary>
        public Observable<EffectDisplay> Ticked => _ticked;

        /// <summary>
        /// 이 유닛의 턴 시작 정산이 끝날 때 발화. 「턴마다」 유지 이펙트 트리거.
        /// 도트·리젠이 터지고 화면 상태가 갱신된 뒤에 오므로, 이번 턴에 풀린 효과는 이미 걷힌 상태다.
        /// </summary>
        public Observable<Unit> TurnStarted => _turnStarted;

        /// <summary> 이 유닛의 스킬 슬롯들(기본+액티브). 행동자일 때 스킬 버튼으로 쓴다. </summary>
        public IReadOnlyList<SkillSlotViewModel> Skills { get; }

        /// <summary> Acted 신호를 발화한다. 이번 턴 행동자에 대해 BattleViewModel이 호출한다. </summary>
        /// <param name="targets">이번 턴에 맞는 대상들. 비우면 대상 없는 신호가 되어 근접 이동이 생략된다.</param>
        internal void RaiseActed(SkillSO skill, IReadOnlyList<CombatantViewModel> targets = null)
            => _acted.OnNext((skill, targets ?? Array.Empty<CombatantViewModel>()));

        /// <summary> Hit 신호를 발화한다. 이번 턴 효과 결과마다 BattleViewModel이 호출한다. </summary>
        internal void RaiseHit(SkillSO skill, EffectResult result)
            => _hit.OnNext((skill, new EffectDisplay(result)));

        /// <summary> Ticked 신호를 발화한다. 이번 턴 행동자의 틱마다 BattleViewModel이 호출한다. </summary>
        internal void RaiseTicked(EffectResult tick) => _ticked.OnNext(new EffectDisplay(tick));

        /// <summary> TurnStarted 신호를 발화한다. 이번 턴 행동자에 대해 BattleViewModel이 호출한다. </summary>
        internal void RaiseTurnStarted() => _turnStarted.OnNext(Unit.Default);

        /// <summary>
        /// 도메인 효과 목록을 표시 순서로 확정해 변환한다. 해로움(디버프→도트→도발) 먼저,
        /// 이로움(실드→리젠→버프) 다음, 그룹 안에서는 남은 턴 오름차순에 상시(-1)가 마지막이다.
        /// </summary>
        /// <param name="persistent">함께 세울 표시 전용 상시 효과. 전투 효과와 같은 정렬을 탄다.</param>
        public static IReadOnlyList<ActiveEffect> BuildActiveEffects(IReadOnlyList<StatusEffect> effects,
            IReadOnlyList<ActiveEffect> persistent = null)
            // _effects의 삽입 순서에 기대지 않으므로 같은 효과를 다시 걸어도 아이콘 위치가 튀지 않는다.
            => effects
                .Select(e => new ActiveEffect(e.Type, e.Stat, e.RemainingTurns, MagnitudeOf(e)))
                .Concat(persistent ?? Array.Empty<ActiveEffect>())
                .OrderBy(e => DisplayRank(e.Type))
                .ThenBy(e => e.RemainingTurns < 0 ? int.MaxValue : e.RemainingTurns)
                .ToList();

        /// <summary>
        /// 아직 살아 있는 효과들의 출처 스킬을 모은다. 흡수량이 0이 된 실드는 대상의 다음 턴 정산까지
        /// 목록에 남으므로 만료된 효과를 걸러 낸다.
        /// </summary>
        public static IReadOnlyCollection<SkillSO> BuildEffectSources(IReadOnlyList<StatusEffect> effects)
            => effects
                .Where(e => !e.IsExpired && e.Source != null)
                .Select(e => e.Source)
                .ToHashSet();

        /// <summary> 타입마다 세기를 싣는 필드가 달라 <see cref="ActiveEffect.Magnitude"/> 하나로 모은다. </summary>
        private static float MagnitudeOf(StatusEffect effect) => effect.Type switch
        {
            EffectType.Buff or EffectType.Debuff => effect.Value,
            EffectType.Dot or EffectType.Regen => effect.TickAmount,
            EffectType.Shield => effect.RemainingAbsorb,
            _ => 0f,
        };

        /// <summary> 표시 정렬 순위. 값이 작을수록 앞에 놓인다. </summary>
        private static int DisplayRank(EffectType type) => type switch
        {
            EffectType.Debuff => 0,
            EffectType.Dot => 1,
            EffectType.Taunt => 2,
            EffectType.Shield => 3,
            EffectType.Regen => 4,
            EffectType.Buff => 5,
            _ => 6,
        };

        /// <summary> 파생 프로퍼티와 스킬 슬롯의 구독을 해지한다. 소유자(BattleViewModel)가 호출한다. </summary>
        public void Dispose()
        {
            CurrentHp.Dispose();
            IsAlive.Dispose();
            ShieldAbsorb.Dispose();
            SkillEffects.Dispose();
            AllEffects.Dispose();
            EffectSources.Dispose();
            _acted.Dispose();
            _hit.Dispose();
            _ticked.Dispose();
            _turnStarted.Dispose();
            foreach (var slot in Skills) slot.Dispose();
        }
    }
}