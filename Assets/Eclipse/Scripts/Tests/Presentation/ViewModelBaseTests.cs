using Eclipse.Presentation;
using NUnit.Framework;

namespace Eclipse.Tests
{
    public class FakeViewModelBase : ViewModelBase
    {
        public int DisposeCount { get; private set; }
        
        protected override void OnDispose()
        {
            base.OnDispose();
            DisposeCount++;
        }
    }
    
    public class ViewModelBaseTests
    {
        [Test]
        public void Dispose_2번호출()
        {
            var vm = new  FakeViewModelBase();
            vm.Dispose();
            vm.Dispose();

            Assert.AreEqual(1, vm.DisposeCount);
        }

        [Test]
        public void Dispose_된결과_확인()
        {
            var vm = new  FakeViewModelBase();
            vm.Dispose();
            Assert.IsTrue(vm.IsDisposed);
        }
        
        
    }
}