using System.Collections;
using System.Collections.ObjectModel;

namespace VisualHFT.Helpers;

public class CachedCollection<T> : IEnumerable<T>
{
    private readonly object _lock = new();
    private ReadOnlyCollection<T> _cachedReadOnlyCollection;
    private List<T> _internalList;

    public CachedCollection(IEnumerable<T> initialData = null)
    {
        _internalList = initialData?.ToList() ?? new List<T>();
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
        {
            if (_cachedReadOnlyCollection != null)
                return _cachedReadOnlyCollection.ToList().GetEnumerator();
            return _internalList.ToList().GetEnumerator(); // Create a copy to ensure thread safety during enumeration
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public ReadOnlyCollection<T> AsReadOnly()
    {
        lock (_lock)
        {
            if (_cachedReadOnlyCollection == null) _cachedReadOnlyCollection = _internalList.AsReadOnly();
            return _cachedReadOnlyCollection;
        }
    }

    public void Update(IEnumerable<T> newData)
    {
        lock (_lock)
        {
            _internalList = new List<T>(newData);
            _cachedReadOnlyCollection = null; // Invalidate the cache
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _internalList.Clear();
            _cachedReadOnlyCollection = null; // Invalidate the cache
        }
    }

    public int Count()
    {
        lock (_lock)
        {
            if (_cachedReadOnlyCollection != null)
                return _cachedReadOnlyCollection.Count;
            return _internalList.Count;
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _internalList.Add(item);
            _cachedReadOnlyCollection = null; // Invalidate the cache
        }
    }

    public bool Remove(T item)
    {
        lock (_lock)
        {
            var result = _internalList.Remove(item);
            if (result) _cachedReadOnlyCollection = null; // Invalidate the cache
            return result;
        }
    }

    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            _internalList.RemoveAt(index);
            _cachedReadOnlyCollection = null; // Invalidate the cache
        }
    }

    public T FirstOrDefault(Func<T, bool> predicate)
    {
        lock (_lock)
        {
            if (_cachedReadOnlyCollection != null)
                return _cachedReadOnlyCollection.FirstOrDefault(predicate);
            return _internalList.FirstOrDefault(predicate);
        }
    }

    public void InvalidateCache()
    {
        _cachedReadOnlyCollection = null; // Invalidate the cache
    }
}