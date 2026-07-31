using System;
using Eclipse.Data;
using Eclipse.Data.Enums;
using Eclipse.Domain;

namespace Eclipse.Presentation
{
    /// <summary> 스킬 강화 시도 결과. UI가 실패 사유(재화 부족 vs 만렙)를 구분해 안내하려고 enum으로 나눈다. </summary>
    public enum SkillEnhanceResult
    {
        Success,
        InsufficientCurrency,
        MaxSkillLevel,
    }

    /// <summary>
    /// 스킬 강화의 유일한 권위. 결제(골드+교본)·스킬 레벨 증가·세이브를 하나의 트랜잭션으로 묶는다.
    /// <see cref="GrowthService.TryLevelUp"/>과 같은 3단 골격(상한 가드 → 결제 → 증가·저장)을 쓴다.
    /// </summary>
    public sealed class SkillEnhanceService
    {
        private readonly ICurrencyService _currency;
        private readonly SaveService _save;
        private readonly GrowthConfigSO _config;
        private readonly CharacterGrowthSignals _signals;

        public SkillEnhanceService(ICurrencyService currency, SaveService save, GrowthConfigSO config,
            CharacterGrowthSignals signals)
        {
            _currency = currency;
            _save = save;
            _config = config;
            _signals = signals;
        }

        /// <summary>
        /// 대상 슬롯의 스킬 레벨을 1 올린다. 골드·교본은 둘 다 잔액을 확인한 뒤에만 함께 차감한다(반쪽 결제 방지).
        /// </summary>
        /// <param name="skillSlot">액티브 스킬 슬롯 번호(0부터 <see cref="OwnedCharacter.SkillSlotCount"/> 미만).</param>
        /// <returns>
        /// Success = 결제·레벨 증가·세이브 시도까지 완료. 단 세이브는 I/O 실패를 내부에서 삼키므로
        /// (<see cref="SaveService.Save"/> 계약) 영속을 보장하지는 않는다.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">skillSlot이 슬롯 범위를 벗어날 때.</exception>
        public SkillEnhanceResult TryEnhance(OwnedCharacter owned, int skillSlot)
        {
            if (skillSlot < 0 || skillSlot >= OwnedCharacter.SkillSlotCount)
                throw new ArgumentOutOfRangeException(nameof(skillSlot), skillSlot,
                    $"스킬 슬롯은 0 이상 {OwnedCharacter.SkillSlotCount} 미만이어야 한다.");

            // 스킬 레벨이 만렙인 경우
            int currentLevel = owned.SkillLevels[skillSlot];
            if (currentLevel >= OwnedCharacter.MaxSkillLevel)
                return SkillEnhanceResult.MaxSkillLevel;

            // 재화가 충분하지 않은 경우
            int goldCost = _config.skillEnhanceCostCoefficient * currentLevel;
            int manualCost = _config.skillEnhanceManualCost;
            if (!_currency.CanAfford(CurrencyType.Gold, goldCost) || !_currency.CanAfford(CurrencyType.Manual, manualCost))
                return SkillEnhanceResult.InsufficientCurrency;

            // 재화 결제 진행
            _currency.TrySpend(CurrencyType.Gold, goldCost);
            _currency.TrySpend(CurrencyType.Manual, manualCost);

            // 캐릭터 레벨 적용 및 저장
            owned.IncreaseSkillLevel(skillSlot);
            _save.Save();
            // 값이 다 반영된 뒤에 알린다. 구독자가 갱신된 스킬 레벨을 읽는 것이 보장된다.
            _signals.Notify(owned);
            return SkillEnhanceResult.Success;
        }
    }
}
