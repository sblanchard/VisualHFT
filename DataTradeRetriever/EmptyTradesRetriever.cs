using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VisualHFT.Model;

namespace VisualHFT.DataTradeRetriever;

public class EmptyTradesRetriever : IDataTradeRetriever, IDisposable
{
    private bool _disposed;
    private readonly List<Order> _orders;
    private readonly List<Position> _positions;
    private int _providerId;
    private string _providerName;
    private DateTime? _sessionDate;

    public EmptyTradesRetriever()
    {
        _positions = new List<Position>();
        _orders = new List<Order>();
    }

    public event EventHandler<IEnumerable<Order>> OnInitialLoad;
    public event EventHandler<IEnumerable<Order>> OnDataReceived;

    public DateTime? SessionDate
    {
        get => _sessionDate;
        set
        {
            if (value != _sessionDate)
            {
                _sessionDate = value;
                _orders.Clear();
                RaiseOnInitialLoad(Orders);
            }
        }
    }

    public ReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    public ReadOnlyCollection<Position> Positions => _positions.AsReadOnly();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void RaiseOnInitialLoad(IEnumerable<Order> ord)
    {
        OnInitialLoad?.Invoke(this, ord);
    }

    protected virtual void RaiseOnDataReceived(IEnumerable<Order> ord)
    {
        OnDataReceived?.Invoke(this, ord);
    }

    ~EmptyTradesRetriever()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
            }

            _disposed = true;
        }
    }
}