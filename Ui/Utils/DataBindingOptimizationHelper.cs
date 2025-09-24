using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Data;

namespace _1RM.Utils
{
    /// <summary>
    /// Optimizations for WPF data binding performance
    /// </summary>
    public static class DataBindingOptimizationHelper
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
        private static readonly ConcurrentDictionary<string, WeakReference> _bindingCache = new();

        /// <summary>
        /// Gets cached property information for a type to avoid reflection overhead
        /// </summary>
        /// <param name="type">Type to get properties for</param>
        /// <returns>Array of PropertyInfo objects</returns>
        public static PropertyInfo[] GetCachedProperties(Type type)
        {
            return _propertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Gets cached property info for a specific property name
        /// </summary>
        /// <param name="type">Type containing the property</param>
        /// <param name="propertyName">Name of the property</param>
        /// <returns>PropertyInfo if found, null otherwise</returns>
        public static PropertyInfo? GetCachedProperty(Type type, string propertyName)
        {
            var properties = GetCachedProperties(type);
            foreach (var prop in properties)
            {
                if (prop.Name == propertyName)
                    return prop;
            }
            return null;
        }

        /// <summary>
        /// Creates a cached binding expression to avoid recreating bindings
        /// </summary>
        /// <param name="path">Property path for binding</param>
        /// <param name="source">Source object</param>
        /// <param name="mode">Binding mode</param>
        /// <returns>Cached or new Binding object</returns>
        public static Binding GetCachedBinding(string path, object? source = null, BindingMode mode = BindingMode.OneWay)
        {
            var cacheKey = $"{path}_{source?.GetType().Name}_{mode}";
            
            if (_bindingCache.TryGetValue(cacheKey, out var weakRef) && weakRef.Target is Binding cachedBinding)
            {
                return cachedBinding;
            }

            var binding = new Binding(path)
            {
                Mode = mode,
                Source = source
            };

            _bindingCache[cacheKey] = new WeakReference(binding);
            return binding;
        }

        /// <summary>
        /// Optimized property change notification that batches multiple updates
        /// </summary>
        public class BatchedPropertyNotifier : INotifyPropertyChanged
        {
            private readonly Dictionary<string, object?> _pendingChanges = new();
            private bool _notificationsSuspended = false;

            public event PropertyChangedEventHandler? PropertyChanged;

            /// <summary>
            /// Suspends property change notifications to batch multiple updates
            /// </summary>
            public void SuspendNotifications()
            {
                _notificationsSuspended = true;
            }

            /// <summary>
            /// Resumes property change notifications and fires all pending changes
            /// </summary>
            public void ResumeNotifications()
            {
                _notificationsSuspended = false;
                
                if (_pendingChanges.Count > 0)
                {
                    foreach (var change in _pendingChanges)
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(change.Key));
                    }
                    _pendingChanges.Clear();
                }
            }

            /// <summary>
            /// Sets a property value and optionally notifies of the change
            /// </summary>
            /// <typeparam name="T">Property type</typeparam>
            /// <param name="field">Backing field</param>
            /// <param name="value">New value</param>
            /// <param name="propertyName">Property name</param>
            /// <returns>True if value changed</returns>
            protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value))
                    return false;

                field = value;
                
                if (propertyName != null)
                {
                    if (_notificationsSuspended)
                    {
                        _pendingChanges[propertyName] = value;
                    }
                    else
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Clears cached property information (useful for memory management)
        /// </summary>
        public static void ClearPropertyCache()
        {
            _propertyCache.Clear();
        }

        /// <summary>
        /// Clears weak references that are no longer valid
        /// </summary>
        public static void CleanupBindingCache()
        {
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _bindingCache)
            {
                if (!kvp.Value.IsAlive)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _bindingCache.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Enhanced collection view source with performance optimizations
    /// </summary>
    public class OptimizedCollectionViewSource : CollectionViewSource
    {
        private bool _refreshPending = false;
        private readonly object _refreshLock = new object();

        public OptimizedCollectionViewSource()
        {
            // Set up default optimizations
            IsLiveFilteringRequested = false;
            IsLiveSortingRequested = false;
            IsLiveGroupingRequested = false;
        }

        /// <summary>
        /// Deferred refresh that prevents multiple rapid refreshes
        /// </summary>
        public void DeferredRefresh()
        {
            lock (_refreshLock)
            {
                if (_refreshPending) return;
                _refreshPending = true;
            }

            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    lock (_refreshLock)
                    {
                        _refreshPending = false;
                        View?.Refresh();
                    }
                }));
        }
    }
}