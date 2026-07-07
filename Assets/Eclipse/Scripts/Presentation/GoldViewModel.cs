using R3;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 보유 골드 상태를 관리하는 ViewModel. View는 <see cref="Gold"/>를 구독해 표시한다.
    /// </summary>
    public class GoldViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<int> _gold = new ReactiveProperty<int>(1000);

        /// <summary> 보유 골드(읽기 전용). 값 변경 시 구독자에게 통지된다. </summary>
        public ReadOnlyReactiveProperty<int> Gold => _gold;

        /// <summary> 골드를 지정 수량만큼 소모한다. </summary>
        public void SpendGold(int amount)
        {
            _gold.Value -= amount;
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _gold.Dispose();
        }
    }
}