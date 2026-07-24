using Eclipse.Data.Enums;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 재화 증감의 유일한 공개 경로. 지급·소비·잔액 확인을 이 인터페이스로만 처리한다.
    /// 구현은 <see cref="CurrencyWallet"/> 잔액을 변경하는 유일한 권위이며, View는 지갑을 읽기로만 참조한다.
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>지정 재화를 amount만큼 지급한다. 음수 금액은 프로그래머 오류이므로 예외를 던진다.</summary>
        void Grant(CurrencyType type, int amount);

        /// <summary>
        /// 잔액이 충분하면 amount를 차감하고 true, 부족하면 잔액을 그대로 두고 false를 반환한다.
        /// 검사와 차감이 한 흐름이라 부분 차감이 없다. 음수 금액은 예외를 던진다.
        /// </summary>
        bool TrySpend(CurrencyType type, int amount);

        /// <summary>지정 재화 잔액이 amount 이상인지 확인한다.</summary>
        bool CanAfford(CurrencyType type, int amount);
    }
}
