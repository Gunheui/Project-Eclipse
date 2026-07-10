namespace Eclipse.Data.Enums
{
    /// <summary>
    /// 버프·디버프가 변경하는 능력치 종류. Stats 6필드에 대응하며, None은 스탯을 바꾸지 않는 효과용.
    /// </summary>
    public enum StatType
    {
        None = 0,
        Hp = 1,
        Atk = 2,
        Def = 3,
        Spd = 4,
        CritRate = 5,
        CritDamage = 6
    }
}
