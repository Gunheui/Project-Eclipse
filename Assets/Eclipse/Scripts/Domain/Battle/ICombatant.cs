using System.Collections.Generic;
using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 전투에 참여하는 유닛의 공통 계약. 아군(캐릭터)과 적이 같은 계약으로 다뤄진다.
    /// 턴 스케줄러·데미지 파이프라인은 정의 타입(CharacterSO/EnemySO)을 몰라도
    /// 이 계약만으로 동작한다.
    /// </summary>
    public interface ICombatant
    {
        /// <summary> 표시명. </summary>
        string DisplayName { get; }

        /// <summary> 소속 편. 타이브레이크의 아군 우선 판정에 쓰인다. </summary>
        Team Team { get; }

        /// <summary> 편성 슬롯 번호(0부터). 같은 편·같은 SPD의 타이브레이크에 쓰인다. </summary>
        int SlotIndex { get; }

        /// <summary>
        /// 버프·디버프가 반영된 현재 유효 스탯. 정렬·데미지는 이 값을 읽는다.
        /// 지금은 기본 스탯과 같고, 수정자 적용은 이후 범위에서 붙는다.
        /// </summary>
        Stats EffectiveStats { get; }

        /// <summary> 현재 HP. </summary>
        int CurrentHp { get; }

        /// <summary> 생존 여부(HP &gt; 0). </summary>
        bool IsAlive { get; }

        /// <summary> 보유 스킬의 런타임(잔여 쿨 포함). </summary>
        IReadOnlyList<SkillRuntime> Skills { get; }
    }
}