namespace Eclipse.View.Theme
{
    /// <summary>
    /// <see cref="UIThemeSO"/>의 색 필드 하나를 가리키는 이름표. <see cref="ThemedGraphic"/>이 이 값을 프리팹에 저장한다.
    /// </summary>
    // 프리팹에 남는 건 이름이 아니라 정수라 값을 명시적으로 박는다. 중간에 멤버를 끼우면
    // 이미 부착된 자리가 조용히 다른 색으로 어긋나고 에러도 나지 않는다.
    // 새 토큰은 뒤에만 붙이고, 제거한 멤버의 정수는 다시 쓰지 않는다.
    public enum UIThemeToken
    {
        Primary = 0,
        PrimaryHover = 1,
        PrimaryPressed = 2,
        PrimarySubtle = 3,
        PrimaryDisabled = 4,
        OnPrimary = 5,

        Surface2 = 6,
        BorderDefault = 7,

        PositiveSubtle = 8,
        OnPositiveSubtle = 9,
        DangerSubtle = 10,
        OnDangerSubtle = 11,

        CardGradeCommon = 12,
        CardGradeRare = 13,
        CardGradeEpic = 14,
        CardGradeUnique = 15,
        OnCardGradeCommon = 16,
        OnCardGradeRare = 17,
        OnCardGradeEpic = 18,
        OnCardGradeUnique = 19,

        RarityR = 20,
        RaritySR = 21,
        RaritySSR = 22,

        BattleDamage = 23,
        BattleHeal = 24,
        BattleDot = 25,
        BattleRegen = 26,
        BattleShield = 27,
        BattleAlly = 28,
        BattleEnemy = 29,
        BattleEffectBeneficial = 30,
        BattleEffectHarmful = 31,
        BattleEffectOverflow = 32,

        TextHigh = 33,
        TextMedium = 34,
        TextDisabled = 35,

        Surface1 = 36,
        BorderStrong = 37,
        SurfaceDark = 38,
        Scrim = 39,
    }
}
