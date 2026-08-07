using System;
using System.Collections.Generic;
using Eclipse.Data;
using Eclipse.Domain;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 전장에 세울 유닛 하나와 그 아트 묶음. 유닛·스프라이트를 리스트 인덱스로 짝짓지 않고
    /// 한 덩어리로 넘겨 길이가 달라진 상태를 표현할 수 없게 한다.
    /// </summary>
    public readonly struct BattleUnitEntry
    {
        public BattleUnitEntry(Combatant unit, Sprite battler, RuntimeAnimatorController battlerAnimator,
            Sprite timelineIcon, MutationSO mutation, IReadOnlyList<ActiveEffect> runEffects)
        {
            Unit = unit;
            Battler = battler;
            BattlerAnimator = battlerAnimator;
            TimelineIcon = timelineIcon;
            Mutation = mutation;
            RunEffects = runEffects ?? Array.Empty<ActiveEffect>();
        }

        /// <summary> 도메인 유닛(HP·스킬 상태의 원천). </summary>
        public Combatant Unit { get; }

        /// <summary> 전장 배틀러 스프라이트(아군 초상·적 배틀러). 없으면 null. </summary>
        public Sprite Battler { get; }

        public RuntimeAnimatorController BattlerAnimator { get; }

        /// <summary> 턴 순서 타임라인 아이콘(아군 얼굴 크롭·적 배틀러). 없으면 null(해당 칸은 비워 그린다). </summary>
        public Sprite TimelineIcon { get; }

        /// <summary> 이 적에게 붙은 침식 변이. 틴트 색·배수 표기가 전부 여기서 나온다. 없으면 null. </summary>
        public MutationSO Mutation { get; }

        /// <summary> 이 유닛에 걸린 런 카드의 표시 전용 효과. 스탯은 이미 접혀 있어 아이콘만 낸다. </summary>
        public IReadOnlyList<ActiveEffect> RunEffects { get; }
    }
}
