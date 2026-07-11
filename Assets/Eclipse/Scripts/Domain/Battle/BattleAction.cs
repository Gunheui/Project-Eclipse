namespace Eclipse.Domain
{
    /// <summary>
    /// 한 유닛의 한 턴 행동 = 쓸 스킬 + (있으면) 지정 대상.
    /// Target이 null이면 각 효과의 TargetSelector가 대상을 정한다.
    /// Target이 지정되면 단일-적 효과에 한해 그 대상으로 오버라이드된다.
    /// </summary>
    public readonly struct BattleAction
    {
        /// <summary> 이번 턴에 사용할 스킬. null이면 쓸 스킬이 없는 상태(호출부가 턴을 넘긴다). </summary>
        public SkillRuntime Skill { get; }

        /// <summary> 수동 지정 대상. null이면 효과별 TargetSelector가 대상을 결정한다. </summary>
        public ICombatant Target { get; }

        public BattleAction(SkillRuntime skill, ICombatant target = null)
        {
            Skill = skill;
            Target = target;
        }
    }
}