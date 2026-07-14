using Eclipse.Domain;
using UnityEngine;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 전장에 세울 유닛 하나와 그 유닛의 아트 묶음. 유닛과 스프라이트를 각각 리스트로 넘겨 인덱스로 짝짓지 않고
    /// 한 덩어리로 넘겨, 길이가 어긋난 상태 자체를 표현할 수 없게 한다. 아트는 프레젠테이션 경계에서 실린다
    /// (도메인은 아트를 모른다).
    /// </summary>
    public readonly struct BattleUnitEntry
    {
        /// <param name="unit">이 자리에 설 도메인 유닛.</param>
        /// <param name="battler">전장에 세울 배틀러 스프라이트. 없으면 null.</param>
        /// <param name="timelineIcon">턴 순서 타임라인에 쓸 아이콘. 없으면 null(해당 칸은 비워 그린다).</param>
        public BattleUnitEntry(Combatant unit, Sprite battler, Sprite timelineIcon)
        {
            Unit = unit;
            Battler = battler;
            TimelineIcon = timelineIcon;
        }

        /// <summary> 도메인 유닛(HP·스킬 상태의 원천). </summary>
        public Combatant Unit { get; }

        /// <summary> 전장 배틀러 스프라이트(아군 초상·적 배틀러). </summary>
        public Sprite Battler { get; }

        /// <summary> 턴 순서 타임라인 아이콘(아군 얼굴 크롭·적 배틀러). </summary>
        public Sprite TimelineIcon { get; }
    }
}
