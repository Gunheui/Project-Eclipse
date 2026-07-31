using System;
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

        /// <summary>
        /// 스킬 강화가 올린 위력 배수. <see cref="SkillExecutor"/>가 위력형 효과에만 곱하고 비율형에는 쓰지 않는다.
        /// </summary>
        public float PowerMultiplier { get; }

        /// <summary> 스킬 레벨에 대응하는 위력 배수를 낸다. 레벨 1이 기준값 1.0이다. </summary>
        /// <param name="skillLevel">스킬 레벨. 상한은 SkillEnhanceService가 강제하므로 여기서 자르지 않는다.</param>
        public static float PowerMultiplierFor(int skillLevel)
            // 레벨당 10% 복리. 레벨 3이면 1.21배.
            => MathF.Pow(1.10f, skillLevel - 1);

        /// <param name="initialCooldown">전투 시작 시 걸어둘 잔여 쿨(턴). 0이면 시작부터 사용 가능.</param>
        /// <param name="powerMultiplier">위력형 효과에 곱할 강화 배수. 강화가 없으면 1.</param>
        public SkillRuntime(SkillSO skill, int initialCooldown = 0, float powerMultiplier = 1f)
        {
            Skill = skill;
            CurrentCooldown = initialCooldown;
            PowerMultiplier = powerMultiplier;
        }

        /// <summary> 사용 가능하면 쿨을 최대치로 잠그고 true, 쿨이 남아 못 쓰면 false를 반환한다. </summary>
        public bool TryUse()
        {
            if (!IsReady) return false;
            
            CurrentCooldown = Skill.cooldownTurns;
            return true;

        }

        /// <summary> 라운드마다 호출해 남은 쿨을 1 줄인다. 0 밑으로는 내려가지 않는다. </summary>
        public void ReduceCooldown()
        {
            if (IsReady) return;
            
            CurrentCooldown--;
        }
    }
}