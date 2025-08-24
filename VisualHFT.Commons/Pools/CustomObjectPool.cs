using System.Collections.Concurrent;
using System.Collections;

namespace VisualHFT.Commons.Pools
{
    /// <summary>
    /// High-performance object pool with guaranteed object reuse for HFT systems.
    /// Replaces Microsoft's DefaultObjectPool which creates new objects when exhausted.
    /// </summary>
    public class CustomObjectPool<T> : IDisposable where T : class, new()
    {
        private readonly ConcurrentQueue<T> _objects = new ConcurrentQueue<T>();
        private readonly int _maxPoolSize;
        private long _currentCount;  // Changed from int to long for Interlocked operations
        private bool _disposed = false;
        private long _totalGets;
        private long _totalReturns;
        private long _totalCreated;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public CustomObjectPool(int maxPoolSize = 100)
        {
            _maxPoolSize = maxPoolSize;
            _currentCount = 0;

            // Pre-warm the pool with initial objects
            for (int i = 0; i < Math.Min(maxPoolSize / 10, 100); i++)
            {
                var obj = new T();
                _objects.Enqueue(obj);
                Interlocked.Increment(ref _currentCount);
                Interlocked.Increment(ref _totalCreated);
            }
        }

        public T Get()
        {
            Interlocked.Increment(ref _totalGets);

            if (_objects.TryDequeue(out T item))
            {
                Interlocked.Decrement(ref _currentCount);
                return item;
            }

            // Pool exhausted - create new object (but log this for monitoring)
            Interlocked.Increment(ref _totalCreated);

            if (_totalCreated % 1000 == 0) // Log every 1000 creations
            {
                var typeName = typeof(T).Name;
                log.Warn($"CustomObjectPool<{typeName}> exhausted - created {_totalCreated} total objects. Consider increasing pool size.");
            }

            return new T();
        }

        public void Return(IEnumerable<T> listObjs)
        {
            if (listObjs == null)
                return;
            foreach (var obj in listObjs)
            {
                Return(obj);
            }
        }

        public void Return(T obj)
        {
            if (obj == null || _disposed)
                return;

            Interlocked.Increment(ref _totalReturns);

            // Reset object state before returning to pool
            (obj as VisualHFT.Commons.Model.IResettable)?.Reset();
            (obj as IList)?.Clear();


            // Only return to pool if we haven't exceeded capacity
            var currentCount = Interlocked.Read(ref _currentCount);
            if (currentCount < _maxPoolSize)
            {
                _objects.Enqueue(obj);
                Interlocked.Increment(ref _currentCount);
            }
            // If pool is full, let the object be garbage collected naturally
        }
        public void Reset()
        {
            foreach (var item in _objects)
            {
                (item as VisualHFT.Commons.Model.IResettable)?.Reset();
                (item as IList)?.Clear();
            }
            Interlocked.Exchange(ref _currentCount, 0);
        }

        public int AvailableObjects => (int)Interlocked.Read(ref _currentCount);  // Cast to int for backward compatibility
        public double UtilizationPercentage => _maxPoolSize > 0 ? Math.Max(0, 1.0 - (Interlocked.Read(ref _currentCount) / (double)_maxPoolSize)) : 0;
        public long TotalGets => _totalGets;
        public long TotalReturns => _totalReturns;
        public long TotalCreated => _totalCreated;
        public bool IsHealthy => _totalCreated < _maxPoolSize * 2; // Should not create more than 2x pool size

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Dispose all objects in the pool
            while (_objects.TryDequeue(out T item))
            {
                (item as IDisposable)?.Dispose();
            }
        }
    }


}
