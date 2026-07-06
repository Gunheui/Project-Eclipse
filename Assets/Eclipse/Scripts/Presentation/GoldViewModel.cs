using R3;

namespace Eclipse.Presentation
{
    public class GoldViewModel : ViewModelBase
    {
        private readonly ReactiveProperty<int> _gold = new ReactiveProperty<int>(1000);
        public ReadOnlyReactiveProperty<int> Gold => _gold;

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