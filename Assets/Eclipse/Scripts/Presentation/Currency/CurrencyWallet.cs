using System;
using Eclipse.Data.Enums;
using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 플레이어가 보유한 재화 3종의 반응형 지갑. 값이 바뀌면 구독자(HUD)가 자동 갱신된다.
    /// 읽기는 <see cref="ReadOnlyReactiveProperty{T}"/>로만 노출하고, 변경은 <see cref="Grant"/>로만 한다.
    /// 실제 증감 규칙(보상·구매·소모)은 상위 서비스가 담당하며, 여기서는 잔액 상태만 보관한다.
    /// </summary>
    public sealed class CurrencyWallet : IDisposable
    {
        private readonly ReactiveProperty<int> _essence;
        private readonly ReactiveProperty<int> _gold;
        private readonly ReactiveProperty<int> _manual;

        /// <summary>소환(가챠) 재화 잔액.</summary>
        public ReadOnlyReactiveProperty<int> Essence => _essence;

        /// <summary>성장(레벨업·스킬강화) 재화 잔액.</summary>
        public ReadOnlyReactiveProperty<int> Gold => _gold;

        /// <summary>스킬강화 재료 재화 잔액.</summary>
        public ReadOnlyReactiveProperty<int> Manual => _manual;

        /// <summary>신규 계정 기본 잔액으로 지갑을 만든다.</summary>
        public CurrencyWallet() : this(3000, 1000, 0)
        {
        }

        /// <summary>저장된 잔액으로 지갑을 만든다(세이브 복원용). 음수는 0으로 고정한다.</summary>
        public CurrencyWallet(int essence, int gold, int manual)
        {
            _essence = new ReactiveProperty<int>(Math.Max(0, essence));
            _gold = new ReactiveProperty<int>(Math.Max(0, gold));
            _manual = new ReactiveProperty<int>(Math.Max(0, manual));
        }

        /// <summary>
        /// 지정 재화 잔액을 amount만큼 더한다(음수면 소모). 결과가 0 미만이면 0으로 고정한다.
        /// </summary>
        public void Grant(CurrencyType type, int amount)
        {
            var rp = Select(type);
            rp.Value = Math.Max(0, rp.Value + amount);
        }

        private ReactiveProperty<int> Select(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Essence: return _essence;
                case CurrencyType.Gold: return _gold;
                case CurrencyType.Manual: return _manual;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>보유한 세 재화의 <see cref="ReactiveProperty{T}"/>를 모두 해제한다.</summary>
        public void Dispose()
        {
            _essence.Dispose();
            _gold.Dispose();
            _manual.Dispose();
        }
    }
}
