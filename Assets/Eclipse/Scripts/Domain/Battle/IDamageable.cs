namespace Eclipse.Domain
{
    /// <summary>
    /// 피해·회복을 받아 HP가 바뀌는 전투 대상이 구현하는 인터페이스.
    /// 상태를 읽기만 하는 ICombatant와 분리해, 스킬 효과 적용부만 이 인터페이스로 HP를 깎거나 채운다.
    /// 스케줄러·데미지 파이프라인은 이 인터페이스를 모르므로 HP를 바꿀 수 없다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary> 피해를 적용해 HP를 줄인다(0 밑으로는 내려가지 않음). </summary>
        /// <param name="amount">깎을 HP(0 이상 전제).</param>
        void ApplyDamage(int amount);

        /// <summary> 회복을 적용해 HP를 늘린다(최대 HP를 넘지 않음). </summary>
        /// <param name="amount">채울 HP(0 이상 전제).</param>
        void Heal(int amount);
    }
}