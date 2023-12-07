using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using VisualHFT.Helpers;
using VisualHFT.Model;
using Timer = System.Timers.Timer;

namespace VisualHFT.DataTradeRetriever;

public class MSSQLServerTradesRetriever : IDataTradeRetriever, IDisposable
{
    private const int POLLING_INTERVAL = 5000; // Interval for polling the database
    private readonly CancellationTokenSource _cancellationTokenSource = new(); // Added cancellation token source
    private readonly HFTEntities _DB;
    private readonly object _lock = new();
    private readonly Timer _timer;
    private bool _disposed; // to track whether the object has been disposed
    private long? _LAST_POSITION_ID;
    private List<Order> _orders;
    private readonly List<Position> _positions;
    private DateTime? _sessionDate;


    public MSSQLServerTradesRetriever()
    {
        _positions = new List<Position>();
        _orders = new List<Order>();
        _timer = new Timer(POLLING_INTERVAL);
        _timer.Elapsed += _timer_Elapsed;
        _timer.Start();
        _timer_Elapsed(null, null);

        _DB = new HFTEntities();
        _DB.Database.CommandTimeout = 6000;
        _DB.Configuration.ValidateOnSaveEnabled = false;
        _DB.Configuration.AutoDetectChangesEnabled = false;
        _DB.Configuration.LazyLoadingEnabled = false;
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
                _LAST_POSITION_ID = null;
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

    ~MSSQLServerTradesRetriever()
    {
        Dispose(false);
    }

    private async void _timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        _timer.Stop(); // Stop the timer while the operation is running
        if (_cancellationTokenSource.IsCancellationRequested) return; // Check for cancellation


        var res = await GetPositionsAsync();
        if (res != null && res.Any())
        {
            foreach (var p in res)
            {
                if (!p.PipsPnLInCurrency.HasValue || p.PipsPnLInCurrency == 0)
                {
                    p.PipsPnLInCurrency =
                        p.GetCloseQuantity * p.GetCloseAvgPrice - p.GetOpenQuantity * p.GetOpenAvgPrice;
                    if (p.Side == ePOSITIONSIDE.Sell) p.PipsPnLInCurrency *= -1;
                }

                HelperSymbol.Instance.UpdateData(p.Symbol);
                _positions.Add(p);
            }

            if (Orders == null || !Orders.Any())
            {
                _orders = res.Select(x => x.GetOrders()).SelectMany(x => x).ToList();
                RaiseOnInitialLoad(Orders);
            }
            else
            {
                var ordersToNotify = new List<Order>();
                foreach (var p in res)
                {
                    _orders.InsertRange(0, p.GetOrders());
                    ordersToNotify.AddRange(p.GetOrders());
                }

                RaiseOnDataReceived(ordersToNotify);
            }
        }

        _timer.Start(); // Restart the timer once the operation is complete
    }

    private async Task<IEnumerable<Position>> GetPositionsAsync()
    {
        if (!SessionDate.HasValue || _cancellationTokenSource.IsCancellationRequested) return null;

        if (!SessionDate.HasValue)
            return null;
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    var targetDate = SessionDate.Value.Date;
                    var simpleCheckOfNewRecords = _DB.Positions
                        .Where(x => DbFunctions.TruncateTime(x.CreationTimeStamp) == targetDate &&
                                    (!_LAST_POSITION_ID.HasValue || x.ID > _LAST_POSITION_ID.Value))
                        .ToList();

                    if (!simpleCheckOfNewRecords.Any()) return null;

                    var allProviders = _DB.Providers.ToList();
                    var result = _DB.Positions.Include("OpenExecutions").Include("CloseExecutions").Where(x =>
                        DbFunctions.TruncateTime(x.CreationTimeStamp) == targetDate &&
                        (!_LAST_POSITION_ID.HasValue || x.ID > _LAST_POSITION_ID.Value)).ToList();
                    if (result.Any())
                    {
                        _LAST_POSITION_ID = result.Max(x => x.ID);

                        var ret = result.Select(x => new Position(x)).ToList(); //convert to our model
                        //find provider's name
                        ret.ForEach(x =>
                        {
                            x.CloseProviderName = allProviders.Where(p => p.ProviderCode == x.CloseProviderId)
                                .DefaultIfEmpty(new Provider()).FirstOrDefault().ProviderName;
                            x.OpenProviderName = allProviders.Where(p => p.ProviderCode == x.OpenProviderId)
                                .DefaultIfEmpty(new Provider()).FirstOrDefault().ProviderName;

                            x.CloseExecutions.ForEach(ex => ex.ProviderName = x.CloseProviderName);
                            x.OpenExecutions.ForEach(ex => ex.ProviderName = x.OpenProviderName);
                        });
                        return ret;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return null;
                }
            }
        });

        //return null;
    }


    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _timer.Elapsed -= _timer_Elapsed;
                _timer?.Stop();
                _timer?.Dispose();
                _cancellationTokenSource?.Cancel(); // Cancel any ongoing operations
                _cancellationTokenSource?.Dispose();
                _DB.Dispose();
            }

            _disposed = true;
        }
    }
}