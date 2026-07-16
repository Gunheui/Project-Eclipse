namespace Eclipse.Domain
{
    /// <summary>
    /// 전투 시드에서 용도별 난수 스트림 시드를 파생한다. 스트림을 나누는 이유는 한 용도의 굴림이
    /// 다른 용도의 수열을 밀지 않게 하려는 것 — 예: 타겟 선택이 데미지 수열을 소비하면 기존
    /// 데미지 회귀 테스트의 기대값이 통째로 바뀐다.
    /// 파생은 프로덕션·테스트가 같은 식을 써야 스트림 분리가 실제로 보장되므로 여기 한 곳에만 둔다.
    /// </summary>
    public static class BattleSeed
    {
        // 타겟 스트림을 데미지 스트림에서 떼어내기 위한 소금값. 값 자체에 의미는 없다(서로 다르기만 하면 된다).
        private const int TargetingSalt = 0x7A16E7;

        /// <summary> 타겟 선택 전용 난수 시드. 같은 battleSeed면 항상 같은 값(재현 유지). </summary>
        /// <param name="battleSeed">전투 진입 시드(데미지 난수가 쓰는 원본 시드).</param>
        /// <returns>데미지 스트림과 겹치지 않는 타겟 전용 시드.</returns>
        public static int ForTargeting(int battleSeed) => battleSeed ^ TargetingSalt;
    }
}
