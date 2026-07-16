namespace Eclipse.Domain
{
    /// <summary> 타겟 우선순위 사다리의 기저(최하위) 규칙 종류. </summary>
    public enum TargetBaseTier
    {
        /// <summary> 아군: 최저 HP비율 후보(동률 버킷)에서 시드 추첨. 전원 풀피면 버킷=전체라 무작위 타겟. </summary>
        AllyLowestHpBucket,

        /// <summary> 적: 저HP일수록 약하게 가중한 시드 랜덤(하드 최저HP 저격이 아니라 약한 편향). </summary>
        EnemyWeightedLowHp
    }

    /// <summary>
    /// 타겟 우선순위 사다리의 프로파일. 아군 오토와 적 AI가 같은 사다리(<see cref="TargetPriorityPolicy"/>)를
    /// 쓰되 활성 층·기저 규칙만 이 값으로 달리한다.
    /// </summary>
    public readonly struct TargetPolicyProfile
    {
        /// <summary>
        /// 막타(lethal) 층 채택 확률. 처치 가능한 후보가 있을 때 이 확률로 그 후보를 고르고, 실패하면 기저 층으로 내려간다.
        /// 아군 오토=1(항상), 적=부분 확률 — 적이 매번 확정 처치를 몰면 힐러가 살릴 여지가 없어 난이도가 튄다.
        /// </summary>
        public float LethalChance { get; }

        /// <summary> 기저(최하위) 규칙 종류. </summary>
        public TargetBaseTier BaseTier { get; }

        /// <summary> 적 저HP 가중 강도(<see cref="TargetBaseTier.EnemyWeightedLowHp"/> 전용). 0=완전 균등, 클수록 다친 대상을 더 노림. </summary>
        public float LowHpBias { get; }

        /// <param name="lethalChance">막타 층 채택 확률(0=미사용, 1=항상).</param>
        /// <param name="baseTier">기저 규칙 종류.</param>
        /// <param name="lowHpBias">적 저HP 가중 강도(적 프로파일 전용, 아군은 0).</param>
        public TargetPolicyProfile(float lethalChance, TargetBaseTier baseTier, float lowHpBias)
        {
            LethalChance = lethalChance;
            BaseTier = baseTier;
            LowHpBias = lowHpBias;
        }

        /// <summary> 아군 오토: 막타 항상 + 최저HP 버킷 시드 추첨. 수동 지정(사다리 최상위)이 항상 이 위를 덮는다. </summary>
        public static TargetPolicyProfile AllyAuto => new(1f, TargetBaseTier.AllyLowestHpBucket, 0f);

        /// <summary> 적 AI: 막타 부분 확률 + 저HP 약가중 시드 랜덤. 일부러 완벽하지 않게 둬 난이도를 낮춘다. </summary>
        /// <param name="lethalChance">막타 층 채택 확률(BattleConstantsSO에서 튜닝).</param>
        /// <param name="lowHpBias">저HP 가중 강도(BattleConstantsSO에서 튜닝). 0이면 HP 무관 균등 랜덤.</param>
        public static TargetPolicyProfile EnemyAi(float lethalChance, float lowHpBias)
            => new(lethalChance, TargetBaseTier.EnemyWeightedLowHp, lowHpBias);
    }
}
