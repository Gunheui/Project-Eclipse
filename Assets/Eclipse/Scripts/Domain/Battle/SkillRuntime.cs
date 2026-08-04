using System.Collections.Generic;
using System.Linq;
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
            // 레벨당 10%p 등차. 레벨 3이면 1.20배, 상한 레벨 5면 1.40배.
            => 1f + 0.10f * (skillLevel - 1);

        /// <summary>
        /// 이 유닛이 실제로 쓰는 효과 목록. 유니크 카드가 붙인 효과까지 포함하며, 스킬 에셋은 공유물이라
        /// 수정본을 여기에만 둔다. 실행기와 조준 판정이 원본 대신 이 목록을 읽는다.
        /// </summary>
        public IReadOnlyList<SkillEffect> Effects { get; }

        /// <param name="initialCooldown">전투 시작 시 걸어둘 잔여 쿨(턴). 0이면 시작부터 사용 가능.</param>
        /// <param name="powerMultiplier">위력형 효과에 곱할 강화 배수. 강화가 없으면 1.</param>
        /// <param name="addedEffects">유니크 카드가 덧붙일 효과. 없으면 원본 목록을 그대로 가리킨다.</param>
        public SkillRuntime(SkillSO skill, int initialCooldown = 0, float powerMultiplier = 1f,
            IReadOnlyList<SkillEffect> addedEffects = null)
        {
            Skill = skill;
            CurrentCooldown = initialCooldown;
            PowerMultiplier = powerMultiplier;
            // 붙일 게 없으면 원본을 그대로 가리켜 방마다 사본이 쌓이지 않게 한다.
            Effects = addedEffects == null || addedEffects.Count == 0
                ? skill.effects
                : skill.effects.Concat(addedEffects).ToList();
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