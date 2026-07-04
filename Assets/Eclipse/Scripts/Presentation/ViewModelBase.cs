using System;

namespace Eclipse.Presentation
{
    public abstract class ViewModelBase : IDisposable
    {
        public bool IsDisposed { get; private set; }
        
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            OnDispose();
        }

        protected virtual void OnDispose() 
        {
            
        }

    }
}