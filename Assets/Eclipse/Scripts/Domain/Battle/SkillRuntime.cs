using Eclipse.Data;

namespace Eclipse.Domain
{
    /// <summary>
    /// 특정 SkillSO 정의를 감싸 유닛별 사용 상태(잔여 쿨)를 관리하는 런타임 객체.
    /// </summary>
    public class SkillRuntime
    {
        /// <summary> 이 런타임이 참조하는 스킬 정의(공유·불변). </summary>
        public SkillSO Skill { get; }
        
        /// <summary> 남은 쿨(턴). 0이면 사용 가능. </summary>
        public int CurrentCooldown { get; private set; }

        /// <summary> 쿨이 다 돌아 사용 가능한 상태인지. </summary>
        public bool IsReady => CurrentCooldown == 0;

        /// <param name="skill">감쌀 스킬 정의(공유·불변).</param>
        /// <param name="initialCooldown">전투 시작 시 걸어둘 잔여 쿨(턴). 0이면 시작부터 사용 가능.</param>
        public SkillRuntime(SkillSO skill, int initialCooldown = 0)
        {
            Skill = skill;
            CurrentCooldown = initialCooldown;
        }

        /// <summary>
        /// 사용 가능하면 쿨을 최대치로 잠그고 true, 쿨이 남아 못 쓰면 false를 반환한다.
        /// </summary>
        public bool TryUse()
        {
            if (!IsReady) return false;
            
            CurrentCooldown = Skill.cooldownTurns;
            return true;

        }

        /// <summary> 라운드마다 호출해 남은 쿨을 1 줄인다(0 밑으로는 내려가지 않음). </summary>
        public void Tick()
        {
            if (IsReady) return;
            
            CurrentCooldown--;
        }
    }
}