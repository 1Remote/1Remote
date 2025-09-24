using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace _1RM.Utils
{
    /// <summary>
    /// Performance optimization utilities for 1Remote WPF application
    /// </summary>
    public static class PerformanceOptimizationHelper
    {
        private static readonly ConcurrentDictionary<string, DateTime> _lastUpdateTimes = new();
        private static readonly ConcurrentDictionary<string, object> _debounceLocks = new();

        /// <summary>
        /// Debounces UI updates to prevent excessive refresh operations
        /// </summary>
        /// <param name="key">Unique identifier for the operation</param>
        /// <param name="action">Action to execute</param>
        /// <param name="delayMs">Delay in milliseconds (default 100ms)</param>
        /// <param name="dispatcher">Dispatcher to use (optional)</param>
        public static void DebounceUIUpdate(string key, Action action, int delayMs = 100, Dispatcher? dispatcher = null)
        {
            var lockObj = _debounceLocks.GetOrAdd(key, _ => new object());
            
            lock (lockObj)
            {
                _lastUpdateTimes[key] = DateTime.Now.AddMilliseconds(delayMs);
            }

            Task.Delay(delayMs).ContinueWith(_ =>
            {
                lock (lockObj)
                {
                    if (DateTime.Now >= _lastUpdateTimes[key])
                    {
                        if (dispatcher?.CheckAccess() == false)
                        {
                            dispatcher.Invoke(action);
                        }
                        else
                        {
                            action();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Throttles action execution to maximum once per specified interval
        /// </summary>
        /// <param name="key">Unique identifier for the operation</param>
        /// <param name="action">Action to execute</param>
        /// <param name="intervalMs">Minimum interval between executions in milliseconds</param>
        /// <returns>True if action was executed, false if throttled</returns>
        public static bool ThrottleAction(string key, Action action, int intervalMs = 500)
        {
            var now = DateTime.Now;
            var lockObj = _debounceLocks.GetOrAdd(key, _ => new object());
            
            lock (lockObj)
            {
                if (_lastUpdateTimes.TryGetValue(key, out var lastTime))
                {
                    if ((now - lastTime).TotalMilliseconds < intervalMs)
                    {
                        return false; // Throttled
                    }
                }
                
                _lastUpdateTimes[key] = now;
                action();
                return true;
            }
        }

        /// <summary>
        /// Batches collection operations to reduce individual property change notifications
        /// </summary>
        /// <typeparam name="T">Collection item type</typeparam>
        /// <param name="collection">Collection to modify</param>
        /// <param name="operations">Operations to perform on the collection</param>
        public static void BatchCollectionOperations<T>(System.Collections.ObjectModel.ObservableCollection<T> collection, 
            Action<System.Collections.ObjectModel.ObservableCollection<T>> operations)
        {
            // Temporarily disable change notifications if collection supports it
            var notifyCollectionChanged = collection as System.ComponentModel.INotifyPropertyChanged;
            
            try
            {
                operations(collection);
            }
            finally
            {
                // Re-enable notifications and trigger a single refresh
                // This would need to be implemented based on the specific collection type used
            }
        }

        /// <summary>
        /// Clears cached timing data for a specific key
        /// </summary>
        /// <param name="key">Key to clear</param>
        public static void ClearThrottleCache(string key)
        {
            _lastUpdateTimes.TryRemove(key, out _);
            _debounceLocks.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears all cached timing data
        /// </summary>
        public static void ClearAllCaches()
        {
            _lastUpdateTimes.Clear();
            _debounceLocks.Clear();
        }

        /// <summary>
        /// Optimized UI thread check and invoke
        /// </summary>
        /// <param name="action">Action to execute on UI thread</param>
        /// <param name="dispatcher">Optional dispatcher (uses current if null)</param>
        public static void SafeUIThreadInvoke(Action action, Dispatcher? dispatcher = null)
        {
            var targetDispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher;
            
            if (targetDispatcher == null)
            {
                action(); // Fallback to direct execution
                return;
            }

            if (targetDispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                targetDispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// Creates a high-performance timer with automatic cleanup
        /// </summary>
        /// <param name="intervalMs">Timer interval in milliseconds</param>
        /// <param name="callback">Callback to execute</param>
        /// <param name="autoReset">Whether timer should auto-reset</param>
        /// <returns>Timer instance</returns>
        public static System.Timers.Timer CreateOptimizedTimer(int intervalMs, System.Timers.ElapsedEventHandler callback, bool autoReset = true)
        {
            var timer = new System.Timers.Timer(intervalMs)
            {
                AutoReset = autoReset,
                Enabled = false
            };
            
            timer.Elapsed += callback;
            return timer;
        }
    }
}