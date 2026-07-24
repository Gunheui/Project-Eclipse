using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary> 레벨업 시도 결과. UI가 실패 사유(금화 부족 vs 만렙)를 구분해 안내하려고 enum으로 나눈다. </summary>
    public enum LevelUpResult
    {
        Success,
        InsufficientGold,
        MaxLevel,
    }

    /// <summary>
    /// 보유 캐릭터 레벨업의 유일한 권위. 결제(재화)·레벨 증가(도메인)·세이브를 하나의 트랜잭션으로 묶는다.
    /// 순수 규칙(레벨 증가·스탯 공식)은 도메인에 있고, 이 서비스는 돈과 파일에 걸친 오케스트레이션만 담당한다.
    /// </summary>
    public sealed class GrowthService
    {
        private readonly ICurrencyService _currency;
        private readonly SaveService _save;
        private readonly GrowthConfigSO _config;

        public GrowthService(ICurrencyService currency, SaveService save, GrowthConfigSO config)
        {
            _currency = currency;
            _save = save;
            _config = config;
        }

        /// <summary>
        /// 대상 캐릭터를 1레벨 올린다. 처리 순서 자체가 "실패 시 무변경"을 보장한다.
        /// 만렙이면 결제하지 않고, 금화가 부족하면 레벨을 올리지 않는다.
        /// </summary>
        /// <returns>
        /// Success = 결제·레벨 증가·세이브 시도까지 완료. 단 세이브는 I/O 실패를 내부에서 삼키므로
        /// (<see cref="SaveService.Save"/> 계약) 영속을 보장하지는 않는다.
        /// </returns>
        public LevelUpResult TryLevelUp(OwnedCharacter owned)
        {
            if (owned.Level >= owned.Definition.growthCurve.maxLevel)
                return LevelUpResult.MaxLevel;

            int cost = _config.levelUpCostCoefficient * owned.Level;
            if (!_currency.TrySpend(CurrencyType.Gold, cost))
                return LevelUpResult.InsufficientGold;

            owned.IncreaseLevel();
            _save.Save();
            return LevelUpResult.Success;
        }
    }
}
