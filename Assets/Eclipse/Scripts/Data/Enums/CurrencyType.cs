namespace Eclipse.Data.Enums
{
    /// <summary>
    /// 게임 재화 종류. 밑값은 직렬화·데이터 매핑에 쓰이므로 고정한다.
    /// </summary>
    public enum CurrencyType
    {
        /// <summary>소환(가챠) 재화. 화면 표시명은 "보석".</summary>
        Essence = 0,

        /// <summary>성장(레벨업·스킬강화) 재화.</summary>
        Gold = 1,

        /// <summary>스킬강화 재료 재화.</summary>
        Manual = 2
    }
}
