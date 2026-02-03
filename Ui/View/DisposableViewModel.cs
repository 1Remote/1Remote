using System;
using Shawn.Utils;

namespace _1RM.View
{
    /// <summary>
    /// Base class for ViewModels that require explicit resource cleanup.
    /// Inherits from NotifyPropertyChangedBase and implements IDisposable.
    /// </summary>
    public abstract class DisposableViewModel : NotifyPropertyChangedBase, IDisposable
    {
        protected bool _isDisposed = false;

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            // Override in derived classes to clean up resources
        }

        /// <summary>
        /// Throws an exception if the instance has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}
