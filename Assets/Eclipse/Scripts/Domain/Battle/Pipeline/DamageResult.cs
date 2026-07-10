namespace Eclipse.Domain
{
    /// <summary>
    /// 한 번의 피해 계산 결과. 최종 데미지와 판정 플래그를 함께 담아
    /// UI·연출(크리 표시)과 전투 로그가 같은 값을 참조하게 한다.
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary> 대상 HP에서 실제로 깎일 최종 데미지(최소 1). </summary>
        public readonly int Amount;

        /// <summary> 이 타격이 치명타였는지(연출·표시용). </summary>
        public readonly bool IsCrit;

        /// <summary> 빗나감 여부. 명중 판정(보류 H) 도입 전까지 항상 false인 예약 필드. </summary>
        public readonly bool IsMiss;

        public DamageResult(int amount, bool isCrit, bool isMiss = false)
        {
            Amount = amount;
            IsCrit = isCrit;
            IsMiss = isMiss;
        }
    }
}