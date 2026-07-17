namespace Eclipse.Domain
{
    /// <summary>
    /// 전투 시드에서 용도별 난수 스트림 시드를 파생한다. 스트림을 나누는 이유는 한 용도의 난수 소비가
    /// 다른 용도의 수열을 밀지 않게 하려는 것 — 예: 타겟 선택이 데미지 수열을 소비하면 기존
    /// 데미지 회귀 테스트의 기대값이 통째로 바뀐다. 아군/적 타겟도 서로 다른 스트림이라야
    /// 한쪽의 난수 소비량(후보 수·도발·막타 성공 여부에 따라 가변)이 반대쪽 선택을 밀지 않는다.
    /// 파생은 프로덕션·테스트가 같은 식을 써야 스트림 분리가 실제로 보장되므로 여기 한 곳에만 둔다.
    /// </summary>
    public static class BattleSeed
    {
        /// <summary> 난수 용도. 값은 "서로 다르다"만 의미 있다. </summary>
        public enum Stream { Damage = 0, AllyTargeting = 1, EnemyTargeting = 2 }

        /// <summary>
        /// 용도별 난수 시드. 같은 battleSeed면 항상 같은 값(재현 유지).
        /// 스트림 id를 XOR로 얹기만 한다 — 실제 분산(avalanche)은 SeededRandom 생성자의
        /// SplitMix64가 담당하므로, 가까운 id(0·1·2)라도 파생 시드는 완전히 갈라진다.
        /// </summary>
        public static int For(int battleSeed, Stream stream) => battleSeed ^ (int)stream;
    }
}
