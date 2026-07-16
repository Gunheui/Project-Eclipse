namespace Eclipse.Data.Enums
{
    /// <summary>
    /// 스킬 효과의 대상 '범위(스코프)'. "단일이냐 광역이냐 · 아군이냐 적이냐"만 나타낸다.
    /// 단일 스코프에서 "구체적으로 누구냐"는 이 enum이 아니라 타겟 우선순위 정책이 정한다.
    /// 숫자 값은 직렬화 에셋(SkillEffect.target)에 저장되므로 고정한다 — 재배치 금지.
    /// </summary>
    public enum TargetSelector
    {
        /// <summary> 자기 자신(단일). </summary>
        Self = 0,

        /// <summary> 아군 한 명(단일). 누구인지는 정책/리졸버가 정한다(기본 = 최저 HP 아군). </summary>
        SingleAlly = 1,

        /// <summary> 아군 전체(광역). </summary>
        AllAllies = 2,

        /// <summary> 적 한 명(단일). 누구인지는 타겟 우선순위 정책이 정한다. </summary>
        SingleEnemy = 3,

        // 4 = 폐기(구 LowestHpEnemy). 재사용 금지 — 구 에셋 데이터 오독 방지를 위해 비워 둔다.

        /// <summary> 적 전체(광역). </summary>
        AllEnemies = 5
    }
}