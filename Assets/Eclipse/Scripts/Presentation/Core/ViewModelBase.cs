using System;

namespace Eclipse.Presentation
{
    /// <summary>
    /// 모든 ViewModel의 공통 베이스. 중복 Dispose를 막고 정리 훅을 제공한다.
    /// </summary>
    public abstract class ViewModelBase : IDisposable
    {
        /// <summary> 이미 정리(Dispose)됐는지 여부. </summary>
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            OnDispose();
        }

        /// <summary>
        /// 파생 ViewModel이 보유 자원(ReactiveProperty 등)을 정리하는 훅. 한 번만 호출된다.
        /// </summary>
        protected virtual void OnDispose()
        {

        }

    }
}